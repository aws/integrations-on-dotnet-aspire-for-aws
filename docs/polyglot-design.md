# Polyglot (Node.js AppHost) Design for Aspire.Hosting.AWS

## Overview

.NET Aspire supports authoring the AppHost in TypeScript/Node.js instead of C#. This document describes how the
`Aspire.Hosting.AWS` integration exposes its features to a Node.js AppHost, the design decisions behind that
exposure, and the constraints those decisions work around.

The mechanism is the **Aspire Type System (ATS)**. An integration author annotates .NET APIs with attributes;
the Aspire CLI scans the built assembly, generates a TypeScript SDK (`aspire.mts`) from those annotations, and
the Node.js AppHost calls the generated fluent wrappers. Each call is proxied back to the running .NET host over
JSON-RPC (`invokeCapability`). The .NET code is still what executes — the TypeScript layer is a generated,
strongly typed facade over it.

Only the AppHost authoring language changes. The Lambda functions, containers, and AWS resources remain .NET
based; the polyglot support is about letting a Node.js AppHost *orchestrate* them.

## Table of Contents

1. [Architecture](#architecture)
2. [The ATS Attributes](#the-ats-attributes)
3. [Capabilities, Handles, and DTOs](#capabilities-handles-and-dtos)
4. [The `withReference` Naming Decision](#the-withreference-naming-decision)
5. [AWS SDK ConstantClass Types (RegionEndpoint, LogFormat)](#aws-sdk-constantclass-types)
6. [Lambda: Generic vs. Polyglot Entry Point](#lambda-generic-vs-polyglot-entry-point)
7. [Lambda Project Path Resolution](#lambda-project-path-resolution)
8. [Lambda Build Suppression](#lambda-build-suppression)
9. [Exported Surface Reference](#exported-surface-reference)
10. [Consuming a Local Build from a Node.js AppHost](#consuming-a-local-build-from-a-nodejs-apphost)
11. [Verification](#verification)

---

## Architecture

At `aspire run`, for a TypeScript AppHost the CLI:

1. Reads `aspire.config.json`, which names the integration NuGet packages (e.g. `Aspire.Hosting.AWS`).
2. Restores those packages plus `Aspire.Hosting` and `Aspire.Hosting.CodeGeneration.TypeScript` into a hidden
   bridge project under `.aspire/integrations/`.
3. Scans the restored assemblies for ATS attributes via `Aspire.TypeSystem.AttributeDataReader` and runs
   `AtsTypeScriptCodeGenerator` to emit `.aspire/modules/aspire.mts` (plus `base.mts`, `transport.mts`).
4. Launches a **prebuilt .NET AppHost host process** that loads the integration assemblies and exposes their
   ATS capabilities over a JSON-RPC named pipe.
5. Runs the TypeScript AppHost (`apphost.mts`) with `tsx`; each generated wrapper method calls
   `invokeCapability(<capabilityId>, args)` over the pipe, and the .NET side executes the real extension method.

```
aspire.config.json ──► NuGet restore ──► ATS scan ──► aspire.mts (generated TS SDK)
                                                          │
apphost.mts (tsx) ──► invokeCapability(id, args) ──JSON-RPC──► .NET host runs AddAWSDynamoDBLocal(...) etc.
```

Key consequence: **a Node.js AppHost consumes integrations only as NuGet packages.** There is no equivalent of
the MSBuild `<ProjectReference>` that C# playgrounds use. This shapes the local-development story (see
[Consuming a Local Build](#consuming-a-local-build-from-a-nodejs-apphost)).

## The ATS Attributes

All three attributes live in the `Aspire.Hosting` namespace (in the already-referenced `Aspire.Hosting`
package), so exposing a feature requires **no new package reference** — only attributes on existing code.

| Attribute | Applies to | Effect |
| --- | --- | --- |
| `[AspireExport]` | static method | Marks it an ATS **capability** (an invokable operation). |
| `[AspireExport]` | type | Marks it an ATS **handle type** (passed by reference as an opaque handle). |
| `[AspireExport("name")]` | static method | Same, with an **explicit, pinned** capability id / TS method name. |
| `[AspireDto]` | type | Marks it a **DTO** (serialized by value as a JSON object / TS interface). |
| `[AspireExportIgnore]` | member | Excludes a member from an exported/`ExposeProperties` type. |

Capability ids auto-derive as `{AssemblyName}/{camelCaseMethodName}` — e.g. `AddAWSSDKConfig` becomes
`Aspire.Hosting.AWS/addAWSSDKConfig`. Passing an explicit string to the constructor pins both the id and the
generated TypeScript method name.

## Capabilities, Handles, and DTOs

The generator maps the three kinds of exported metadata differently:

- **Handle types** (`[AspireExport]` on a type) become TypeScript wrapper classes holding an opaque handle. A
  static extension method whose **first parameter is a handle and which returns that same handle** is generated
  as a **fluent instance method** on that wrapper. This is why
  `WithProfile(this IAWSSDKConfig, string)` becomes `sdkConfig.withProfile("...")` in TypeScript. Handle-typed
  parameters are widened to `Awaitable<T>`, so un-awaited fluent chains can be passed directly.
- **DTOs** (`[AspireDto]` on a type) become TypeScript interfaces, passed by value. Used for plain option
  objects (e.g. `DynamoDBLocalOptions`). A DTO must contain only ATS-serializable members — primitives, enums,
  arrays, and other DTOs; it must not contain framework types like `ILogger` or AWS SDK client types.
- **Capabilities** (`[AspireExport]` on a static method) whose first parameter is `IDistributedApplicationBuilder`
  become top-level builder methods (e.g. `builder.addAWSDynamoDBLocal(...)`); those whose first parameter is a
  handle become fluent methods on that handle's wrapper.

Generic capabilities are supported and are the norm: a method constrained to
`IResourceBuilder<T> where T : IResourceWithEnvironment` is placed on every concrete env-capable builder in the
generated SDK, so the methods stay generic — no non-generic wrapper is needed for the `WithReference` family.

**Design choice — the config resource is a handle, not a DTO.** `IAWSSDKConfig` is marked `[AspireExport]` (a
handle) rather than `[AspireDto]` so that `addAWSSDKConfig().withProfile(...).withRegion(...)` generates as a
fluent builder chain. The concrete `AWSSDKConfig` class stays `internal`; the handle is keyed by the exported
public interface and the instance is opaque to TypeScript.

## The `withReference` Naming Decision

**Problem.** Core Aspire owns the name `withReference` in polyglot AppHosts — the C# `WithReference` extension is
documented as *"not available in polyglot app hosts; use the ATS dispatcher overload instead."* Core exports a
single `withReference` capability (a dispatcher) and lets resource types customize it at runtime via
`IResourceWithCustomWithReference<TSelf>`. `Aspire.Hosting.AWS` has multiple `WithReference` overloads (SDK
config, DynamoDB Local) that all constrain to `IResourceWithEnvironment`. If they auto-derived their names, every
env-capable builder would receive several capabilities all named `withReference` — a collision the generator
resolves by appending numeric suffixes (`withReference`, `withReference1`, ...). That suffix order depends on
scan order and is **not stable**: adding a new overload later could silently rename an existing one and break
every TypeScript AppHost calling the old name.

**Decision.** Export every AWS reference method under an **explicit, distinct, permanent** TypeScript name via the
`[AspireExport("...")]` constructor. A pinned name never auto-suffixes and can only change by a deliberate,
reviewable edit. The C# method names stay `WithReference` (no C# breaking change); only the TypeScript-facing
name is pinned.

| C# method | Pinned TypeScript name |
| --- | --- |
| `WithReference(IResourceBuilder<T>, IAWSSDKConfig)` | `withAWSSDKConfigReference` |
| `WithReference(IResourceBuilder<T>, IResourceBuilder<DynamoDBLocalResource>)` | `withDynamoDBLocalReference` |
| API Gateway `WithReference(..., lambda, Method, path)` | `withAPIGatewayLambdaReference` |

**Alternative considered and rejected.** Implementing `IResourceWithCustomWithReference<TSelf>` so the single
core `withReference` dispatches to AWS logic. This fits DynamoDB Local (a real resource) but **not** the
SDK-config case: `IAWSSDKConfig` is not an `IResourceBuilder<IResource>` and cannot flow through the resource
dispatcher. Using explicit named capabilities keeps both features consistent and lowest-risk. The dispatcher
route can be revisited if Aspire recommends it. TypeScript authors must therefore learn these AWS-specific names
rather than the intuitive `.withReference()`; this is documented in the playground README.

## AWS SDK ConstantClass Types

Several AWS SDK types are `Amazon.Runtime.ConstantClass` — string-backed pseudo-enums with a static-instance /
`FindValue` pattern (e.g. `RegionEndpoint`, `Amazon.Lambda.LogFormat`, `Amazon.Lambda.ApplicationLogLevel`).
These have **no clean ATS representation**: they are neither primitives, real enums, nor value-serializable DTOs.

Two patterns are used to keep them out of the generated TypeScript surface:

- **String-overload for `WithRegion`.** The existing `WithRegion(this IAWSSDKConfig, RegionEndpoint)` is left
  **unannotated** (C#-only). A new `WithRegion(this IAWSSDKConfig, string systemName)` overload is annotated
  `[AspireExport("withRegion")]` and converts internally via `RegionEndpoint.GetBySystemName(systemName)` (the
  established idiom elsewhere in the repo). TypeScript authors call `.withRegion("us-west-2")`.
- **Internal TS-only DTO for Lambda options.** `LambdaFunctionOptions` exposes `LogFormat`/`ApplicationLogLevel`
  as `ConstantClass` and cannot be a clean DTO. Instead, an **`internal` `[AspireDto] LambdaFunctionPolyglotOptions`**
  exposes those as plain `string?` properties. The polyglot Lambda entry point maps the strings to the SDK types
  via `ConstantClass.FindValue`. Because the DTO is `internal`, **.NET AppHosts keep using
  `LambdaFunctionOptions`** while only the generated TypeScript SDK sees the string-based shape. This mirrors
  core Aspire, whose own ATS DTOs (`AddContainerOptions`, `CreateBuilderOptions`, ...) are all `internal`.

## Lambda: Generic vs. Polyglot Entry Point

The public `.NET` entry point is generic and cannot be called from TypeScript:

```csharp
IResourceBuilder<LambdaProjectResource> AddAWSLambdaFunction<TLambdaProject>(
    this IDistributedApplicationBuilder builder, string name, string lambdaHandler,
    LambdaFunctionOptions? options = null) where TLambdaProject : IProjectMetadata, new()
```

`TLambdaProject` is a generated `Projects.*` type; TypeScript has no way to supply it. Core Aspire solves the
analogous `AddProject<TProject>` problem with a separate **internal** `AddProjectForPolyglot(builder, name, path,
...)` marked `[AspireExport("addProject")]` that takes a project **path string**. `Aspire.Hosting.AWS` follows
the same pattern:

```csharp
[AspireExport("addAWSLambdaFunction")]
internal static IResourceBuilder<LambdaProjectResource> AddAWSLambdaFunctionForPolyglot(
    this IDistributedApplicationBuilder builder, string name, string projectPath, string lambdaHandler,
    LambdaFunctionPolyglotOptions? options = null)
```

- The generic overload stays **unexported** and unchanged for C# authors.
- The polyglot overload is **internal** (ATS scans internal members too) and takes a **project path string**.
- Both share a private `AddAWSLambdaFunctionCore(...)` so behavior is identical.
- The polyglot overload builds `IProjectMetadata` from the path (see next two sections) and maps the string log
  options to the SDK `ConstantClass` values.

Both Lambda programming models work through this single entry point, distinguished only by the handler string:

- **Executable model** — handler is the assembly name, e.g. `"ToUpperFunctionExecutable"`.
- **Class library model** — handler is `Assembly::Namespace.Type::Method`, e.g.
  `"CalculatorFunctions::CalculatorFunctions.Functions::Add"`.

## Lambda Project Path Resolution

The generic overload receives a fully-resolved `.csproj` path from generated `Projects.*` metadata. The polyglot
overload instead receives a raw path string from the TypeScript author, which may be **relative to the AppHost
directory** and may point at **either the `.csproj` file or its containing directory**. `ResolvePolyglotProjectPath`
normalizes it, mirroring core Aspire's polyglot project entry points:

1. If the path is not rooted, combine it with `builder.AppHostDirectory` and take the full path.
2. If the result is a directory, resolve it to the single project file it contains (`.csproj`/`.fsproj`),
   throwing a clear `DistributedApplicationException` if there is not exactly one.
3. Otherwise, use the path as-is (it already points at a project file).

`IProjectMetadata.ProjectPath` must be the absolute path to the project file — Aspire's launch-profile and
run machinery read it directly — which is why directory-to-project resolution is required here rather than left
to the caller.

## Lambda Build Suppression

The internal `LambdaProjectMetadata` type carries a `SuppressBuild` flag. It defaults to `true` for one specific
case: the **class-library wrapper project**, which `LambdaBeforeStartEventHandler` builds explicitly (both Debug
and Release) *before* DCP runs it — so `dotnet run --no-build` is correct there and avoids an incremental build
that would drop `runtimeconfig.json` on first launch.

For the polyglot entry point the project is **not** pre-built, so it passes `suppressBuild: false`, letting
Aspire build the project as part of `dotnet run` — matching how the generic overload behaves with generated
`Projects.*` metadata (whose `SuppressBuild` is `false`). Reusing the wrapper's `SuppressBuild = true` here was a
bug: it produced `dotnet run --no-build --configuration Release` against a binary nothing had built.

```csharp
internal sealed class LambdaProjectMetadata(string projectPath, bool suppressBuild = true) : IProjectMetadata
{
    public string ProjectPath { get; } = projectPath;
    public bool SuppressBuild { get; } = suppressBuild;
}
```

## Exported Surface Reference

| Feature | Entry point (TS) | Handle type(s) | DTO / enum types |
| --- | --- | --- | --- |
| AWS SDK config | `addAWSSDKConfig` + `withProfile`, `withRegion`, `withSdkValidation` | `IAWSSDKConfig` | — |
| Reference SDK config | `withAWSSDKConfigReference` | — | — |
| DynamoDB Local | `addAWSDynamoDBLocal` | `DynamoDBLocalResource` | `DynamoDBLocalOptions` |
| Reference DynamoDB Local | `withDynamoDBLocalReference` | — | — |
| Lambda function | `addAWSLambdaFunction` (polyglot, path-based) | `LambdaProjectResource` | `LambdaFunctionPolyglotOptions` (internal) |
| Lambda service emulator | `addAWSLambdaServiceEmulator` | `LambdaEmulatorResource` | `LambdaEmulatorOptions` |
| API Gateway emulator | `addAWSAPIGatewayEmulator` | `APIGatewayEmulatorResource` | `APIGatewayEmulatorOptions`, `APIGatewayType`, `Method` |
| Route Lambda via API Gateway | `withAPIGatewayLambdaReference` | — | — |
| SQS event source | `withSQSEventSource` (string overload only) | — | `SQSEventSourceOptions` |
| DynamoDB Streams event source | `withDynamoDBStreamsEventSource` (string overload only) | — | `DynamoDBStreamsEventSourceOptions` |

Notes:

- **Event sources — string overloads only.** The CDK-construct and CloudFormation `StackOutputReference`
  overloads of `WithSQSEventSource` / `WithDynamoDBStreamsEventSource` are **not** exported; they depend on
  CDK/CFN types that have no clean TypeScript representation. Only the `queueUrl` / `tableName` string overloads
  carry `[AspireExport]`, each with a pinned name.
- `APIGatewayType` and `Method` are real C# enums, so they generate as clean TypeScript string enums.

## Consuming a Local Build from a Node.js AppHost

Because a Node.js AppHost resolves integrations as NuGet packages (not project references), testing local source
changes requires packing `Aspire.Hosting.AWS` to a **local NuGet feed** and letting the AppHost consume it as a
package. The `playground/NodeAppHost` sample implements this:

- A playground-local `nuget.config` adds a `./packages` local feed (the repo root `nuget.config` clears all
  sources, so this is required for the local feed to be visible inside the repo).
- A `pack-local.mjs` helper packs the AWS project into `./packages`.

**Version selection.** The checked-in `<Version>` (e.g. `13.5.0`) is *also* published on nuget.org and is bumped
each release, so version alone cannot distinguish the local source build from the published package. The pack
helper therefore overrides **`PackageVersion`** (not `Version`) to `100.0.<seconds-since-2020>`:

- `100.x` is far above any real release, and `aspire.config.json` requests `"Aspire.Hosting.AWS": "100.0.0"`,
  which NuGet treats as the range `[100.0.0, )` — a floor **only the local build satisfies**, so nuget.org's
  release is never chosen. The `100.0.0` floor never changes across release bumps.
- The pack helper clears `./packages` first, so the feed holds exactly one `100.0.*` package (NuGet resolves the
  lowest version in a range, so there must be only one).
- Seconds-since-2020 (~2.1e8) fits NuGet's 32-bit version components; **raw `DateTime.Ticks` (18 digits)
  overflows and fails to parse**. `PackageVersion` is overridden rather than `Version` because `Version` also
  feeds `AssemblyVersion`/`FileVersion`, whose components are capped at 65535 and would overflow.

This is the closest supported equivalent to "project reference against local source" for a Node.js AppHost, and
it hardcodes no release version.

## Verification

- **Reflection unit tests** (`tests/Aspire.Hosting.AWS.UnitTests/AtsExportTests.cs`) assert the expected
  `[AspireExport]`/`[AspireDto]` presence and the pinned capability ids, so a rename or dropped annotation fails
  the build. `LambdaPolyglotTests.cs` covers path resolution (directory vs. `.csproj`, relative vs. rooted) and
  `SuppressBuild == false`.
- **End-to-end** via `playground/NodeAppHost`: `aspire run` starts the SDK config, DynamoDB Local, both Lambda
  programming models, and the API Gateway emulator. Invoking the emulator routes exercises the full path —
  including the executable Lambda reading/writing DynamoDB Local through the injected
  `AWS_ENDPOINT_URL_DYNAMODB` endpoint. Confirm the generated `aspire.mts` contains the AWS methods (proving the
  local source build was used) and that the run log has no `TYPE_MISMATCH`, "project file not found", or
  `--no-build` failures.
