# Bundled SDK verification

These are the public-only, reproducible versions of the real build fixtures used for the SDM and project-library-harvesting rollout. They exercise the **distributed SDK bundle**, not just an in-process task. The solution does not deploy to DataMiner or publish to the Catalog.

## Projects

| Project | Type | Scenario |
|---------|------|----------|
| [MyScript](MyScript/README.md) | Automation script | Direct SDM 1.0.3 and Registration.Common 2.0.0 dependencies. |
| [MyAdHocDataSource](MyAdHocDataSource/README.md) | Legacy GQI ad hoc data source | The same SDM dependency conflict through the actual GQI project type. |
| [HarvestingScript](HarvestingScript/README.md) | Automation script | Call a multitarget project library with a transitive library and SDM dependencies. |
| [HarvestingGqi](HarvestingGqi/README.md) | Legacy GQI ad hoc data source | Use the same harvested library graph from GQI without changing its API track. |
| [ProbeEntry](ProbeEntry/README.md) | C# library | net48/netstandard2.0 library calling ProbeBase. |
| [ProbeBase](ProbeBase/README.md) | C# library | netstandard2.0 transitive library with Registration.Common 2.0.0. |

`Prerequisite.csproj` only bootstraps the exact SDK package into an isolated cache. It is not part of the application solution.

The separate [Plan alpha032 GQI probe](PlanAlpha032Gqi/README.md) records the exact older solution-library compatibility case tested with the SDM-only SDK. It is not included in the public solution; its private prerequisites must be supplied through an authorized external feed.

## Expected behavior

All four packages retain `Skyline.DataMiner.SDM.SourceGenerator.Runtime.dll` but contain neither discarded `Skyline.DataMiner.SDM.dll` payload nor old SDM imports. The two GQI projects retain the `Skyline.DataMiner.Analytics.GenericInterface` API, `preCompile=true`, and their original library names.

The selected SourceGenerator.Runtime **1.0.3** import and payload must each occur once. An additional older **assembly-bearing** runtime payload can remain through the preserved legacy hint behavior; this hotfix suppresses discarded versions only for selected packages with no applicable assemblies, not every older package version or same-named DLL.

The harvesting packages include exactly one ProbeEntry net48 DLL and one ProbeBase netstandard2.0 DLL. `VerifyPackages.ps1` compares their payload bytes with the actual compiled outputs. Debug uses assembly version 1.2.3.4; Release uses 9.8.7.6. Imports use assembly versions, not NuGet package versions.

## Recorded rollout result

Public SDK **2.5.10-harvesting20261005.1** at source commit `4b24b8381cd46477d2ee5ded44f4509f0881f95e` was restored from nuget.org in separate fresh Core and desktop caches. Both hosts built Debug and Release sequentially, and all **16 package checks** passed. The [recorded JSON results](VerificationResults.json) include the SDK provenance and each package hash.

| Project | Imports | DLL payload entries | Verified configurations and hosts |
|---------|---------|---------------------|-----------------------------------|
| MyScript | 4 | 10 | Debug/Release, Core/desktop |
| MyAdHocDataSource | 4 | 10 | Debug/Release, Core/desktop |
| HarvestingScript | 6 | 12 | Debug/Release, Core/desktop |
| HarvestingGqi | 6 | 12 | Debug/Release, Core/desktop |

SDK nupkg SHA256: `A5FB3DE042AE7C7D70C6345FDD3A79F09BDB83D8D380D3CF746D2BD30DB1D996`. All five bundled producer DLLs match the public **6.3.2-harvesting20261005.1** code by PE section hashes; whole-file hashes differ because the SDK distribution signs the DLLs again.

The historical Registration.Common 2.0.0 dependency graph produces NU1701 framework-fallback warnings in the netstandard2.0 probe libraries. This intentionally retains the original packaging compatibility scenario; it is not a claim of live cross-framework execution.

## Core MSBuild

Use .NET SDK 10 and PowerShell 7. From this directory, set `NUGET_PACKAGES` and `NUGET_HTTP_CACHE_PATH` to new, task-owned directories before the first restore. `NuGet.Config` deliberately permits only nuget.org, with no local or private package source.

```powershell
dotnet restore Prerequisite.csproj --configfile NuGet.Config
dotnet build BundledSdkVerification.sln -c Debug --disable-build-servers
.\VerifyPackages.ps1 -HostLabel Core -Configuration Debug
dotnet build BundledSdkVerification.sln -c Release --disable-build-servers
.\VerifyPackages.ps1 -HostLabel Core -Configuration Release
```

## Desktop MSBuild

Use a separate fresh cache, bootstrap it with `dotnet restore Prerequisite.csproj`, and then build the same solution with Visual Studio 2022 MSBuild:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe' BundledSdkVerification.sln /restore /p:Configuration=Debug /nr:false /v:minimal
.\VerifyPackages.ps1 -HostLabel Desktop -Configuration Debug
& 'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe' BundledSdkVerification.sln /restore /p:Configuration=Release /nr:false /v:minimal
.\VerifyPackages.ps1 -HostLabel Desktop -Configuration Release
```

Adjust the MSBuild installation path for your Visual Studio edition. Build the hosts sequentially because they write the same fixture output directories.

## Coverage boundary

The [automated final-package regressions](../../Tasks/BuildOnlyPackageOutputTests.cs) additionally cover plain no-NuGet projects, solution-runner grouping, independent consumers, installation scripts, classic libraries, and conditional/diamond graphs. Producer regressions cover missing DLLs, incompatible frameworks, cycles, and build-only asset controls.

Compilation and package inspection do not prove live upgrades, older dependent-library execution, or GQI discovery/query execution. Those require an approved DataMiner target. No credentials or private MediaOps packages belong in this solution.
