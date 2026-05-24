$ErrorActionPreference = "Stop"

$moduleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePath = Join-Path $moduleRoot "SurfaceRuins.cs"
$projectPath = Join-Path $moduleRoot "SurfaceRuins.csproj"
$generatorPath = Join-Path $moduleRoot "generate_icosphere_ruin_positions.py"
$csvPath = Join-Path $moduleRoot "generate_icosphere_ruin_positions.csv"

if (-not (Test-Path $sourcePath)) {
    throw "Missing source file: $sourcePath"
}

if (-not (Test-Path $projectPath)) {
    throw "Missing project file: $projectPath"
}

if (-not (Test-Path $generatorPath)) {
    throw "Missing ruin position generator: $generatorPath"
}

$source = Get-Content -Raw -Encoding UTF8 $sourcePath
$project = Get-Content -Raw -Encoding UTF8 $projectPath
$generator = Get-Content -Raw -Encoding UTF8 $generatorPath
$menuTextCn = -join @([char]0x661f, [char]0x8868, [char]0x5e9f, [char]0x589f)
$lowButtonTextCn = -join @([char]0x6784, [char]0x9020, [char]0x4f4e, [char]0x7eac, [char]0x5ea6, [char]0x5e9f, [char]0x589f)
$midButtonTextCn = -join @([char]0x6784, [char]0x9020, [char]0x4e2d, [char]0x7eac, [char]0x5ea6, [char]0x5e9f, [char]0x589f)
$highButtonTextCn = -join @([char]0x6784, [char]0x9020, [char]0x9ad8, [char]0x7eac, [char]0x5ea6, [char]0x5e9f, [char]0x589f)
$geothermalButtonTextCn = -join @(
    [char]0x5728,
    [char]0x7a7a,
    [char]0x95f2,
    [char]0x5e9f,
    [char]0x589f,
    [char]0x4e0a,
    [char]0x5efa,
    [char]0x9020,
    [char]0x5730,
    [char]0x70ed,
    [char]0x53d1,
    [char]0x7535,
    [char]0x7ad9)

function Assert-Contains {
    param(
        [string] $Text,
        [string] $Pattern,
        [string] $Message
    )

    if ($Text -notmatch $Pattern) {
        throw $Message
    }
}

function Assert-NotContains {
    param(
        [string] $Text,
        [string] $Pattern,
        [string] $Message
    )

    if ($Text -match $Pattern) {
        throw $Message
    }
}

Assert-Contains $project "<TargetFrameworkVersion>v4\.7\.2</TargetFrameworkVersion>" "SurfaceRuins must target .NET Framework 4.7.2 for UXAssist."
Assert-Contains $source "\[BepInDependency\(UXAssist\.PluginInfo\.PLUGIN_GUID\)\]" "SurfaceRuins must declare the UXAssist dependency."
Assert-Contains $source '"surface-ruins-menu"' "Missing Surface Ruins menu localization key."
Assert-Contains $source '"Surface Ruins"' "Missing Surface Ruins English localization."
Assert-Contains $source ([regex]::Escape("`"$menuTextCn`"")) "Missing Surface Ruins Chinese localization."
Assert-Contains $source 'surface-ruins-construct-low-latitude' "Missing low-latitude construct button key."
Assert-Contains $source 'surface-ruins-construct-mid-latitude' "Missing mid-latitude construct button key."
Assert-Contains $source 'surface-ruins-construct-high-latitude' "Missing high-latitude construct button key."
Assert-Contains $source 'Construct low-latitude ruins' "Missing low-latitude construct button English localization."
Assert-Contains $source 'Construct mid-latitude ruins' "Missing mid-latitude construct button English localization."
Assert-Contains $source 'Construct high-latitude ruins' "Missing high-latitude construct button English localization."
Assert-Contains $source ([regex]::Escape("`"$lowButtonTextCn`"")) "Missing low-latitude construct button Chinese localization."
Assert-Contains $source ([regex]::Escape("`"$midButtonTextCn`"")) "Missing mid-latitude construct button Chinese localization."
Assert-Contains $source ([regex]::Escape("`"$highButtonTextCn`"")) "Missing high-latitude construct button Chinese localization."
Assert-Contains $source 'surface-ruins-build-geothermal-on-idle-ruins' "Missing geothermal button localization key."
Assert-Contains $source 'Build geothermal power stations on idle ruins' "Missing geothermal button English localization."
Assert-Contains $source ([regex]::Escape("`"$geothermalButtonTextCn`"")) "Missing geothermal button Chinese localization."
Assert-Contains $source 'MyConfigWindow\.OnUICreated \+= CreateUI' "SurfaceRuins must register its UXAssist UI creation callback."
Assert-Contains $source 'MyConfigWindow\.OnUICreated -= CreateUI' "SurfaceRuins must unregister its UXAssist UI creation callback."
Assert-Contains $source 'private const short BasePitRuinModelIndex = 406;' "Base pit ruin model index must be 406."
Assert-Contains $source 'private const int Level30BasePitLifeTime = -31;' "Level 30 base pit ruin lifetime must be -31."
Assert-Contains $source 'private const float DuplicateRuinRadius = 50f;' "Duplicate ruin radius must be 50."
Assert-Contains $source 'LowLatitudeRuinPositions' "SurfaceRuins must define a low-latitude ruin group."
Assert-Contains $source 'MidLatitudeRuinPositions' "SurfaceRuins must define a mid-latitude ruin group."
Assert-Contains $source 'HighLatitudeRuinPositions' "SurfaceRuins must define a high-latitude ruin group."
Assert-Contains $source 'ConstructLowLatitudeRuinsOnCurrentPlanet' "SurfaceRuins must expose the low-latitude construct button callback."
Assert-Contains $source 'ConstructMidLatitudeRuinsOnCurrentPlanet' "SurfaceRuins must expose the mid-latitude construct button callback."
Assert-Contains $source 'ConstructHighLatitudeRuinsOnCurrentPlanet' "SurfaceRuins must expose the high-latitude construct button callback."
Assert-Contains $source 'BuildGeothermalOnIdleRuinsCurrentPlanet' "SurfaceRuins must expose the idle-ruin geothermal button callback."
Assert-Contains $source 'FindGeothermalPowerItem' "SurfaceRuins must find the geothermal power item dynamically."
Assert-Contains $source 'HasGeothermalOnBaseRuin' "SurfaceRuins must skip ruins that already have geothermal power."
Assert-Contains $source 'HasBaseOnRuin' "SurfaceRuins must skip ruins still bound to a Dark Fog base."
Assert-Contains $source 'AddPrebuildDataWithComponents' "SurfaceRuins must use the vanilla prebuild path for geothermal construction."
Assert-Contains $source 'AddEntityDataWithComponents' "SurfaceRuins must create the final geothermal entity."
Assert-Contains $source 'RemovePrebuildWithComponents' "SurfaceRuins must clean up the temporary prebuild."
Assert-Contains $source 'prebuild\.InitParametersArray\(1\)' "Geothermal prebuild must allocate one parameter."
Assert-Contains $source 'prebuild\.parameters\[0\] = baseRuinId' "Geothermal prebuild must bind the target ruin id."
Assert-Contains $source 'UIRealtimeTip\.Popup' "SurfaceRuins should report result through UIRealtimeTip."

$pointCount = ([regex]::Matches($source, "new Vector3\(")).Count
if ($pointCount -ne 162) {
    throw "Expected 162 embedded ruin positions, found $pointCount."
}

Assert-Contains $generator 'DEFAULT_RADIUS = 200\.2' "Ruin position generator must default to DSP orbit radius 200.2."
Assert-Contains $generator 'DEFAULT_FREQUENCY = 4' "Ruin position generator must use frequency 4 for 162 icosphere vertices."
Assert-Contains $generator 'DEFAULT_CSV_PATH = Path\(__file__\)\.with_suffix\("\.csv"\)' "Generator must default to a CSV sidecar path."
Assert-Contains $generator 'ratio = max\(-1\.0, min\(1\.0, y / length\)\)' "Generator must compute latitude from the Y axis."
Assert-Contains $generator 'latitude_deg' "Generator must compute latitude metadata."
Assert-Contains $generator 'band_for_latitude' "Generator must classify points into latitude bands."
Assert-Contains $generator 'format_csharp_grouped_arrays' "Generator must emit the grouped C# arrays."
Assert-Contains $generator 'write_csv' "Generator must export a CSV file."
Assert-Contains $generator 'parser\.add_argument\("--csv"' "Generator must accept a CSV output path."

if (-not (Test-Path $csvPath)) {
    throw "Missing generated CSV file: $csvPath"
}

$csv = Get-Content -Encoding UTF8 $csvPath
if ($csv.Count -lt 2) {
    throw "CSV sidecar must contain a header and data rows."
}

if ($csv[0] -ne 'index,x,y,z,latitude_deg,abs_latitude_deg,band') {
    throw "CSV header does not match the expected schema."
}

$bandCounts = @{}
foreach ($line in $csv | Select-Object -Skip 1) {
    $parts = $line.Split(',')
    if ($parts.Count -ne 7) {
        throw "Unexpected CSV column count in line: $line"
    }
    $band = $parts[6]
    if (-not $bandCounts.ContainsKey($band)) {
        $bandCounts[$band] = 0
    }
    $bandCounts[$band]++
}

if ($bandCounts['low'] -ne 76 -or $bandCounts['mid'] -ne 48 -or $bandCounts['high'] -ne 38) {
    throw "Unexpected CSV band counts: low=$($bandCounts['low']) mid=$($bandCounts['mid']) high=$($bandCounts['high'])"
}

Assert-Contains $source 'GetLatitudeBand' "SurfaceRuins must classify ruin positions by latitude."
Assert-Contains $source 'normalized\.y' "SurfaceRuins must use the Y axis for latitude."

Assert-NotContains $source 'CreateEnemyPlanetBase' "Fake ruins must not create real Dark Fog bases."
Assert-NotContains $source 'DFGBaseComponent' "Fake ruins must not allocate DFGBaseComponent."
Assert-NotContains $source 'DFRelayComponent' "Fake ruins must not allocate DFRelayComponent."
Assert-NotContains $source 'NotifyBaseKilled' "Fake ruins must not use the real base death path."
Assert-NotContains $source 'CheckBaseCanRemoved' "Idle-ruin geothermal build must not use the removable-base path."
Assert-NotContains $source 'RemoveBase\(' "Idle-ruin geothermal build must not remove real Dark Fog bases."
Assert-NotContains $source 'UseHandItems' "Idle-ruin geothermal build must not consume hand items."
Assert-NotContains $source 'TakeTailItems' "Idle-ruin geothermal build must not consume package items."
Assert-NotContains $source 'FlattenTerrain' "Idle-ruin geothermal build must not require terrain flattening."
Assert-NotContains $source 'TrySetPowerGeneratorInvincible' "Idle-ruin geothermal build must not add generator invincibility."

Write-Host "SurfaceRuins static tests passed."
