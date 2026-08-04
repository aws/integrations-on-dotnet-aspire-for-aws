# Node.js AppHost playground

A TypeScript/Node.js Aspire AppHost that exercises the `Aspire.Hosting.AWS` polyglot features against the
**locally built** integration (not the published NuGet package). It wires:

- **AWS SDK configuration** (`addAWSSDKConfig().withRegion(...)`)
- **DynamoDB Local** (`addAWSDynamoDBLocal`)
- A Lambda function using the **executable programming model** (`ToUpperFunctionExecutable`) that **uses
  DynamoDB Local**: it upper-cases the input and keeps a per-input invocation counter in a DynamoDB table (the
  input string is the hash key), so each call to the same input returns an incremented count — demonstrating the
  DynamoDB Local reference is actually exercised
- Two Lambda functions using the **class library programming model** (`CalculatorFunctions`)
- An **API Gateway emulator** fronting all three Lambda functions

## Why this differs from the C# playgrounds

The C# playgrounds reference `Aspire.Hosting.AWS` with an MSBuild `<ProjectReference>`. A Node.js AppHost
**cannot** — the Aspire CLI resolves node integrations only as **NuGet packages** declared in
`aspire.config.json`'s `packages` section. To test local source changes, we pack `Aspire.Hosting.AWS` into a
local feed and let the AppHost consume it as a package.

Because the checked-in `<Version>` (e.g. `13.5.0`) is also published on nuget.org, the pack step packs the local
build with a `100.0.<seconds-since-2020>` version. `aspire.config.json` requests `"Aspire.Hosting.AWS":
"100.0.0"`, which NuGet treats as the range `[100.0.0, )` — a floor that **only the local `100.0.*` build
satisfies**, so nuget.org's `13.5.0` is never chosen. The pack step clears `./packages` before packing so the
feed holds exactly one `100.0.*` package (NuGet resolves the lowest version in the range, so there must be only
one). The `100.0.0` floor never needs to change when the real release version is bumped.

## Prerequisites

- .NET SDK (the repo's `DefaultTargetFramework`, currently net10.0)
- Node.js 20.19+ / 22.13+ / 24+
- The Aspire CLI (`aspire`)
- Docker (for the DynamoDB Local container)

## Run

```bash
cd playground/NodeAppHost

# 1. Pack the local Aspire.Hosting.AWS build into ./packages with a unique dev version.
npm run pack-aws        # or: node ./pack-local.mjs

# 2. Install the node dependencies.
npm install

# 3. Launch the AppHost. The empty "Aspire.Hosting.AWS" version in aspire.config.json floats to the
#    newest 100.0.* package in the local ./packages feed.
aspire run
```

Re-run `npm run pack-aws` whenever you change `Aspire.Hosting.AWS` source; each pack produces a strictly higher
version that is picked up automatically.

## AWS-specific TypeScript method names

The AWS reference methods are intentionally pinned to distinct names so they do not collide with the core Aspire
`withReference` capability:

| Feature | TypeScript method |
| --- | --- |
| Reference the AWS SDK config | `withAWSSDKConfigReference(sdkConfig)` |
| Reference DynamoDB Local | `withDynamoDBLocalReference(dynamoDBLocal)` |
| Route a Lambda through API Gateway | `withAPIGatewayLambdaReference(lambda, Method, path)` |

## Exercising the routes

Once running, the API Gateway emulator exposes the Lambda functions. For example:

- `POST /toupper` with a JSON string body (e.g. `"hello"`) → executable Lambda upper-cases it and increments a
  DynamoDB Local counter, returning e.g. `HELLO (converted 1 time(s))`; repeat the same input to see the count
  increase (`converted 2 time(s)`, ...). A different input starts its own counter.
- `GET /add/2/3` → class library Lambda returns `5`.
- `GET /subtract/7/4` → class library Lambda returns `3`.
