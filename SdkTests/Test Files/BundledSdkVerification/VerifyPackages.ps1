[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$HostLabel,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$sdkVersion = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'global.json') -Raw | ConvertFrom-Json).'msbuild-sdks'.'Skyline.DataMiner.Sdk'
$reports = @(
    foreach ($project in @('MyScript', 'MyAdHocDataSource', 'HarvestingScript', 'HarvestingGqi')) {
        $package = Join-Path $PSScriptRoot "$project\bin\$Configuration\net48\DataMinerBuild\$project.1.0.0.dmapp"
        $archive = [IO.Compression.ZipFile]::OpenRead($package)
        try {
            $dlls = @($archive.Entries | Where-Object { $_.FullName.EndsWith('.dll', [StringComparison]::OrdinalIgnoreCase) })
            if (@($dlls | Where-Object { $_.FullName -match '(?i)[\\/]Skyline\.DataMiner\.SDM\.dll$' }).Count -ne 0) {
                throw "Discarded SDM payload returned in $project."
            }

            $scripts = @($archive.Entries | Where-Object { $_.FullName -match "Script_$project\.xml$" })
            if ($scripts.Count -ne 1) {
                throw "Expected exactly one script XML for $project."
            }

            $reader = [IO.StreamReader]::new($scripts[0].Open())
            try { [xml]$xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $references = @($xml.SelectNodes("//*[local-name()='Param' and @type='ref']") | ForEach-Object { $_.InnerText })
            if (@($references | Where-Object { $_ -match '(?i)skyline\.dataminer\.sdm[\\/]|[\\/]Skyline\.DataMiner\.SDM\.dll$' }).Count -ne 0) {
                throw "Discarded SDM imports returned in $project."
            }

            $runtimeIdentity = 'skyline.dataminer.sdm.sourcegenerator.runtime/1.0.3/lib/netstandard2.0/Skyline.DataMiner.SDM.SourceGenerator.Runtime.dll'
            if (@($references | Where-Object { $_.Replace('\', '/').EndsWith('/' + $runtimeIdentity, [StringComparison]::OrdinalIgnoreCase) }).Count -ne 1 -or
                @($dlls | Where-Object { $_.FullName.Replace('\', '/').EndsWith('/' + $runtimeIdentity, [StringComparison]::OrdinalIgnoreCase) }).Count -ne 1) {
                throw "The selected SourceGenerator.Runtime import or payload is missing or duplicated in $project."
            }

            if ($project.StartsWith('Harvesting', [StringComparison]::Ordinal)) {
                $version = if ($Configuration -eq 'Release') { '9.8.7.6' } else { '1.2.3.4' }
                foreach ($library in @(@{ Name = 'ProbeEntry'; Framework = 'net48' }, @{ Name = 'ProbeBase'; Framework = 'netstandard2.0' })) {
                    $identity = "$($library.Name.ToLowerInvariant())/$version/lib/$($library.Framework)/$($library.Name).dll"
                    $payloads = @($dlls | Where-Object { $_.FullName.Replace('\', '/').EndsWith('/' + $identity, [StringComparison]::OrdinalIgnoreCase) })
                    $imports = @($references | Where-Object { $_.Replace('\', '/').EndsWith('/' + $identity, [StringComparison]::OrdinalIgnoreCase) })
                    if ($payloads.Count -ne 1 -or $imports.Count -ne 1) {
                        throw "Missing or duplicated configured library in $project : $identity"
                    }

                    $source = Join-Path $PSScriptRoot "$($library.Name)\bin\$Configuration\$($library.Framework)\$($library.Name).dll"
                    $stream = $payloads[0].Open()
                    $hash = [Security.Cryptography.SHA256]::Create()
                    try { $actual = [Convert]::ToHexString($hash.ComputeHash($stream)) } finally { $hash.Dispose(); $stream.Dispose() }
                    if ($actual -ne (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash) {
                        throw "Incorrect configured library bytes in $project : $identity"
                    }
                }
            }

            if ($project -in @('MyAdHocDataSource', 'HarvestingGqi')) {
                if ($xml.SelectSingleNode("//*[local-name()='Param' and @type='preCompile']").InnerText -ne 'true' -or
                    $xml.SelectSingleNode("//*[local-name()='Param' and @type='libraryName']").InnerText -ne $project -or
                    $xml.SelectSingleNode("//*[local-name()='Value']").InnerText -notmatch 'Skyline\.DataMiner\.Analytics\.GenericInterface') {
                    throw "The legacy GQI compilation identity changed in $project."
                }
            }

            [pscustomobject]@{
                Host = $HostLabel
                SDK = $sdkVersion
                Project = $project
                Configuration = $Configuration
                SHA256 = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
                References = $references.Count
                DllPayload = $dlls.Count
                RuntimeExecution = 'Not a live DataMiner execution check'
            }
        }
        finally {
            $archive.Dispose()
        }
    }
)
$reports | ConvertTo-Json -Depth 4
