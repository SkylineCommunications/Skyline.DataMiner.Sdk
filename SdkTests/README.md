# SdkTests

**Project Type**: .NET test project (MSTest)

**Size**: L

## Summary

This project verifies **SDK packaging tasks**, project selection, and generated DataMiner installation packages on **net48 and net10.0**. The final-package regressions inspect script XML and DLL payloads rather than treating a successful task return as sufficient evidence. Tests that generate isolated dependency feeds repeat packaging with fresh and populated caches. They do not install packages or execute scripts or GQI queries on a DataMiner.

## Packaging scenarios

The [final-package regressions](Tasks/BuildOnlyPackageOutputTests.cs) generate their projects from the committed [test fixtures](Test%20Files). The scenario name in each data row selects the projects; the test methods contain the exact graph construction and assertions.

| Scenario | Setup | Expected behavior |
|---------|-------|-------------------|
| Ordinary Automation script without user NuGet dependencies | Remove all `PackageReference` items from the script fixture | Preserve inline C# and script identity; no application NuGet imports or DLLs. Retain the SDK's six default installer-support DLLs. |
| GQI ad hoc data source without user NuGet dependencies | Remove all `PackageReference` items from the GQI fixture | Preserve `preCompile`, `libraryName`, source, and the SDK-owned installer dependencies; no application NuGet imports or DLLs. |
| Plain solution without user NuGet dependencies | Two solution scripts and a minimal native Automation installation script | Preserve both scripts and their solution ID; no application dependency DLL payload. |
| Automation script with build-only NuGet selection | Direct selected package, conflicting transitive older versions, and required runtime dependency | Do not emit discarded imports or DLLs; retain the selected package's runtime dependency and other packages. |
| GQI with build-only NuGet selection | Same dependency graph in an actual ad hoc data-source fixture | Same filtering, while retaining the original GQI compilation identity. |
| Solution with build-only NuGet selection | Two scripts with the same solution ID | Unify their solution dependencies without reintroducing discarded build-only assemblies. |
| Installation script with build-only NuGet selection | Package-project installation script | Keep required runtime imports and matching DLLs; exclude discarded build-only assemblies. |
| Independent consumer in the same package | An unrelated script legitimately requires the older assembly-bearing version | Keep that consumer's necessary assembly without leaking its version into the build-only consumer. |
| Harvested dependencies, Debug and Release | Automation/GQI/solution x Debug/Release; conditional, transitive, diamond, multitarget, and classic .NET Framework libraries | Preserve each configured project DLL exactly once with matching bytes and assembly-version identity; select the compatible TFM; retain runtime dependencies without stale build-only payload. |

There are three no-NuGet, five build-only, and six harvesting final-package data rows. Each repeats the package-content checks after cache population.

The existing [task controls](Tasks/DmappCreationTests.cs) and [solution fixtures](Test%20Files) also cover NuGet version unification, an independent non-solution consumer, Automation `scriptRef` libraries, manifest notices, minimum-version parsing, and ordinary package creation.

## Running the relevant tests

From the repository root:

```powershell
dotnet restore Skyline.DataMiner.Sdk.sln
dotnet test --project SdkTests\SdkTests.csproj -c Release --framework net10.0 --no-restore -p:GeneratePackageOnBuild=false --filter "FullyQualifiedName~SdkTests.Tasks"
dotnet test --project SdkTests\SdkTests.csproj -c Release --framework net48 --no-restore -p:GeneratePackageOnBuild=false --filter "FullyQualifiedName~SdkTests.Tasks"
```

Build the two hosts sequentially: both reference the same netstandard2.0 task output. Windows and Visual Studio MSBuild are required for the net48 host. The repository selects Microsoft.Testing.Platform; do not substitute VSTest's project-selection syntax.

The isolated final-package tests use SDK 2.4.7 props/targets only to evaluate fixture projects, but execute the current repository's `DmappCreation` task and its pinned assembler dependencies. They are not proof that a newly published SDK bundle contains those dependencies. Verify the distributed SDK separately with real builds on Core and desktop MSBuild.

The existing GitHub workflow supplies approved credentials for any Catalog-dependent integration controls. Never add credentials or private packages to these fixtures. Build/package evidence does not establish live upgrade compatibility, dependent-library execution, or GQI discovery/query success.
