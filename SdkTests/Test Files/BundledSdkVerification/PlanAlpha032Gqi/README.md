# PlanAlpha032Gqi

**Project Type**: Ad Hoc Data Source

**Size**: XS

## Summary

This optional **MediaOps Plan alpha032 compatibility probe** preserves the exact older Plan.Automation dependency and legacy GQI project type used during SDM-hotfix verification. It is excluded from the public-only solution because the Plan DevPack and its matching prerequisites require authorized package access. Only the generic consumer source is committed; no private package binaries or credentials are included.

## MyAdHocDataSource

The implementation emits no columns or rows. It isolates NuGet/solution-library imports and the original GQI compilation identity. This is build/package evidence, not live discovery or query execution.

**Library Name**: MyAdHocDataSource

**Interfaces**: IGQIDataSource

### Input Arguments

This data source has no input arguments.

### Output Columns

This data source has no output columns.

## Exact dependency contract

| Package | Version |
|---------|---------|
| Skyline.DataMiner.Sdk | 2.5.9-sdmfix20261005.1 |
| Skyline.DataMiner.Dev.Utils.Solutions.MediaOps.Plan.Automation | 1.7.0-alpha032 |
| Skyline.DataMiner.Dev.Automation | 10.6.9.1 |
| Skyline.DataMiner.SDM.Registration.Common | 2.1.3 |
| Skyline.DataMiner.Utils.SecureCoding.Analyzers | 1.0.0 |

Do not add an artificial direct SDM 1.0.3 pin: Registration.Common 2.1.3 requires at least SDM 1.0.5. The original package minimum dependencies are not changed by this probe.

## Reproducing the check

Supply the exact authorized private packages and their matching DevPack dependencies in an external local feed or an authenticated organizational feed. Use an external NuGet configuration that includes that feed and nuget.org, then build this project separately on Core and desktop MSBuild. Do not add a feed credential or private nupkg to this repository.

From this directory, replace the external configuration path with your authorized configuration:

```powershell
dotnet build MyAdHocDataSource.csproj -c Release --disable-build-servers -p:RestoreConfigFile=C:\ExternalFeeds\NuGet.Config
& 'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe' MyAdHocDataSource.csproj /restore /t:Rebuild /p:Configuration=Release /p:RestoreConfigFile=C:\ExternalFeeds\NuGet.Config /nr:false
```

Both persisted-source builds were checked during the rollout: Core and desktop packages each retained 39 imports, the original GQI metadata, and no discarded SDM import or DLL. This observation is not a live execution result.

The package must retain `preCompile=true`, `libraryName=MyAdHocDataSource`, and the original GenericInterface API. Its declared Plan.Automation import points to:

```text
C:\Skyline DataMiner\ProtocolScripts\DllImport\SolutionLibraries\Solutions.MediaOps.Plan.Automation\Skyline.DataMiner.Dev.Utils.Solutions.MediaOps.Plan.Automation.dll
```

That DLL is provided by the installed Plan solution, not by this probe's payload. Requiring it in the probe package would be an incorrect assertion. Discarded SDM imports/payload must not return. Installing the prerequisite solution and proving its dependent-library execution remain separate live gates.
