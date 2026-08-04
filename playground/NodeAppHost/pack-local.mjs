// Packs the local Aspire.Hosting.AWS build into ./packages with a unique, always-higher dev version.
//
// The checked-in <Version> is the currently shipped version (e.g. 13.5.0) which is also published on
// nuget.org and lacks the local source changes. To make the empty version in aspire.config.json resolve to
// THIS local build, we pack with version 100.0.<seconds-since-2020>:
//   - 100.x is far above any real release (13.x), so highest-version-wins selects it.
//   - seconds-since-2020 is strictly increasing and unique per build, so no NuGet cache clearing is needed.
//   - it fits NuGet's 32-bit version components (raw DateTime ticks would overflow and fail to parse).
//
// We override PackageVersion (not Version): Version also feeds AssemblyVersion/FileVersion, whose components
// are capped at 65535 (UInt16) and would overflow. PackageVersion affects only the .nupkg version, leaving the
// assembly/file version at the project's checked-in default.

import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { rmSync, mkdirSync, readdirSync } from 'node:fs';

const scriptDir = dirname(fileURLToPath(import.meta.url));
const projectPath = join(scriptDir, '..', '..', 'src', 'Aspire.Hosting.AWS', 'Aspire.Hosting.AWS.csproj');
const outputDir = join(scriptDir, 'packages');

// aspire.config.json requests "Aspire.Hosting.AWS": "100.0.0", which NuGet treats as the range [100.0.0, ) and
// resolves to the LOWEST matching version. So the local feed must contain exactly ONE 100.0.* package — the
// freshly packed one. Clear out previously packed .nupkg files first so a previous (lower) dev version is not
// restored instead. We delete only the packages (not the whole folder) so the checked-in README.md is preserved.
mkdirSync(outputDir, { recursive: true });
for (const entry of readdirSync(outputDir)) {
    if (entry.endsWith('.nupkg')) {
        rmSync(join(outputDir, entry), { force: true });
    }
}

// Seconds since 2020-01-01T00:00:00Z. 1577836800 is that instant in Unix seconds.
const seconds = Math.floor(Date.now() / 1000) - 1577836800;
const version = `100.0.${seconds}`;

console.log(`Packing ${projectPath}`);
console.log(`  -> version ${version}`);
console.log(`  -> output  ${outputDir}`);

const result = spawnSync(
    'dotnet',
    ['pack', projectPath, '-o', outputDir, `-p:PackageVersion=${version}`, '-p:NuGetAudit=false'],
    { stdio: 'inherit', shell: true }
);

process.exit(result.status ?? 1);
