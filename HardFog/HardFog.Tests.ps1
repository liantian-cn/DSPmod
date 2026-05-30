$ErrorActionPreference = "Stop"

$moduleDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $moduleDir
$projectPath = Join-Path $moduleDir "HardFog.csproj"
$solutionPath = Join-Path $moduleDir "HardFog.slnx"
$windowSourcePath = Join-Path $moduleDir "HardFogWindow.cs"
$darkFogSourcePath = Join-Path $moduleDir "DarkFogControl.cs"
$surfaceRuinSourcePath = Join-Path $moduleDir "SurfaceRuinControl.cs"
$readmePath = Join-Path $repoRoot "readme.md"
$surfaceRuinsDir = Join-Path $repoRoot "SurfaceRuins"

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    Assert-True ($Text -match $Pattern) $Message
}

function Assert-NotContains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    Assert-True ($Text -notmatch $Pattern) $Message
}

function Assert-NoCSharpWarnings {
    param(
        [string[]]$Output,
        [string]$Command
    )

    Assert-True (($Output -join "`n") -notmatch "warning CS") "$Command must not emit C# warnings."
}

Assert-True (Test-Path -LiteralPath $windowSourcePath) "HardFogWindow.cs must exist."
Assert-True (Test-Path -LiteralPath $darkFogSourcePath) "DarkFogControl.cs must exist."
Assert-True (Test-Path -LiteralPath $surfaceRuinSourcePath) "SurfaceRuinControl.cs must exist."
Assert-True (-not (Test-Path -LiteralPath $surfaceRuinsDir)) "SurfaceRuins must be removed as an independent mod."

[xml]$project = Get-Content -Raw -Encoding UTF8 $projectPath
$compileIncludes = @($project.Project.ItemGroup.Compile | ForEach-Object { $_.Include })
Assert-True ($compileIncludes -contains "HardFogWindow.cs") "HardFog.csproj must compile HardFogWindow.cs."
Assert-True ($compileIncludes -contains "DarkFogControl.cs") "HardFog.csproj must compile DarkFogControl.cs."
Assert-True ($compileIncludes -contains "SurfaceRuinControl.cs") "HardFog.csproj must compile SurfaceRuinControl.cs."
Assert-True ($compileIncludes -notcontains "SurfaceRuins.cs") "HardFog.csproj must not compile SurfaceRuins.cs."

$windowSource = Get-Content -Raw -Encoding UTF8 $windowSourcePath
$surfaceRuinSource = Get-Content -Raw -Encoding UTF8 $surfaceRuinSourcePath
$readme = Get-Content -Raw -Encoding UTF8 $readmePath
$surfaceRuinsTextCn = -join @([char]0x661f, [char]0x8868, [char]0x5e9f, [char]0x589f)

Assert-Contains $windowSource 'BepInPlugin\("me\.liantian\.plugin\.HardFog",\s*"HardFog",\s*"0\.0\.8"\)' "HardFog must publish the merged feature build as version 0.0.8."
Assert-Contains $windowSource 'DarkFogControl\.Log = Logger;' "HardFogWindow must initialize DarkFogControl logging."
Assert-Contains $windowSource 'SurfaceRuinControl\.Log = Logger;' "HardFogWindow must initialize SurfaceRuinControl logging."
Assert-True (([regex]::Matches($windowSource, 'MyConfigWindow\.OnUICreated \+= CreateUI').Count) -eq 1) "HardFogWindow must register exactly one UXAssist UI callback."
Assert-True (([regex]::Matches($windowSource, 'wnd\.AddButton\(').Count) -eq 7) "HardFogWindow must expose three Dark Fog buttons and four surface-ruin buttons."

$surfaceButtonKeys = @(
    @("ConstructLowLatitudeRuinsKey", "surface-ruins-construct-low-latitude"),
    @("ConstructMidLatitudeRuinsKey", "surface-ruins-construct-mid-latitude"),
    @("ConstructHighLatitudeRuinsKey", "surface-ruins-construct-high-latitude"),
    @("BuildGeothermalOnIdleRuinsKey", "surface-ruins-build-geothermal-on-idle-ruins")
)
foreach ($entry in $surfaceButtonKeys) {
    $constantName = $entry[0]
    $key = $entry[1]
    Assert-Contains $windowSource "private const string $constantName = `"$([regex]::Escape($key))`";" "HardFogWindow must define the '$key' localization key."
    Assert-Contains $windowSource "I18N\.Add\(\s*$constantName" "HardFogWindow must add the '$key' localization."
    Assert-Contains $windowSource "wnd\.AddButton\([^;]*$constantName" "HardFogWindow must add the '$key' button in its tab."
}
Assert-Contains $windowSource 'SurfaceRuinControl\.ConstructLowLatitudeRuinsOnCurrentPlanet' "Low-latitude button must call SurfaceRuinControl."
Assert-Contains $windowSource 'SurfaceRuinControl\.ConstructMidLatitudeRuinsOnCurrentPlanet' "Mid-latitude button must call SurfaceRuinControl."
Assert-Contains $windowSource 'SurfaceRuinControl\.ConstructHighLatitudeRuinsOnCurrentPlanet' "High-latitude button must call SurfaceRuinControl."
Assert-Contains $windowSource 'SurfaceRuinControl\.BuildGeothermalOnIdleRuinsCurrentPlanet' "Geothermal button must call SurfaceRuinControl."

Assert-Contains $surfaceRuinSource 'internal\s+static\s+class\s+SurfaceRuinControl' "Surface ruin logic must live in SurfaceRuinControl."
Assert-Contains $surfaceRuinSource 'private const short BasePitRuinModelIndex = 406;' "Base pit ruin model index must be 406."
Assert-Contains $surfaceRuinSource 'private const int Level30BasePitLifeTime = -31;' "Level 30 base pit ruin lifetime must be -31."
Assert-Contains $surfaceRuinSource 'private const float DuplicateRuinRadius = 50f;' "Duplicate ruin radius must be 50."
Assert-Contains $surfaceRuinSource 'LowLatitudeRuinPositions' "SurfaceRuinControl must define a low-latitude ruin group."
Assert-Contains $surfaceRuinSource 'MidLatitudeRuinPositions' "SurfaceRuinControl must define a mid-latitude ruin group."
Assert-Contains $surfaceRuinSource 'HighLatitudeRuinPositions' "SurfaceRuinControl must define a high-latitude ruin group."
Assert-Contains $surfaceRuinSource 'ConstructLowLatitudeRuinsOnCurrentPlanet' "SurfaceRuinControl must expose the low-latitude callback."
Assert-Contains $surfaceRuinSource 'ConstructMidLatitudeRuinsOnCurrentPlanet' "SurfaceRuinControl must expose the mid-latitude callback."
Assert-Contains $surfaceRuinSource 'ConstructHighLatitudeRuinsOnCurrentPlanet' "SurfaceRuinControl must expose the high-latitude callback."
Assert-Contains $surfaceRuinSource 'BuildGeothermalOnIdleRuinsCurrentPlanet' "SurfaceRuinControl must expose the idle-ruin geothermal callback."
Assert-Contains $surfaceRuinSource 'FindGeothermalPowerItem' "SurfaceRuinControl must find the geothermal power item dynamically."
Assert-Contains $surfaceRuinSource 'HasGeothermalOnBaseRuin' "SurfaceRuinControl must skip ruins that already have geothermal power."
Assert-Contains $surfaceRuinSource 'HasBaseOnRuin' "SurfaceRuinControl must skip ruins still bound to a Dark Fog base."
Assert-Contains $surfaceRuinSource 'AddPrebuildDataWithComponents' "SurfaceRuinControl must use the vanilla prebuild path for geothermal construction."
Assert-Contains $surfaceRuinSource 'AddEntityDataWithComponents' "SurfaceRuinControl must create the final geothermal entity."
Assert-Contains $surfaceRuinSource 'RemovePrebuildWithComponents' "SurfaceRuinControl must clean up the temporary prebuild."
Assert-Contains $surfaceRuinSource 'prebuild\.InitParametersArray\(1\)' "Geothermal prebuild must allocate one parameter."
Assert-Contains $surfaceRuinSource 'prebuild\.parameters\[0\] = baseRuinId' "Geothermal prebuild must bind the target ruin id."
Assert-Contains $surfaceRuinSource 'UIRealtimeTip\.Popup' "SurfaceRuinControl should report results through UIRealtimeTip."
Assert-Contains $surfaceRuinSource 'normalized\.y' "SurfaceRuinControl must classify ruin latitude with the Y axis."

$pointCount = ([regex]::Matches($surfaceRuinSource, "new Vector3\(")).Count
Assert-True ($pointCount -eq 120) "SurfaceRuinControl must keep exactly 120 embedded ruin positions; found $pointCount."

$removedRuntimeTokens = @(
    "CreateEnemyPlanetBase",
    "DFGBaseComponent",
    "DFRelayComponent",
    "NotifyBaseKilled",
    "CheckBaseCanRemoved",
    "RemoveBase(",
    "UseHandItems",
    "TakeTailItems",
    "FlattenTerrain",
    "TrySetPowerGeneratorInvincible"
)
foreach ($token in $removedRuntimeTokens) {
    Assert-NotContains $surfaceRuinSource ([regex]::Escape($token)) "SurfaceRuinControl must not use removed or unrelated runtime token '$token'."
}

Assert-Contains $readme '### HardFog' "Root README must document the merged HardFog mod."
Assert-Contains $readme ([regex]::Escape($surfaceRuinsTextCn)) "Root README must mention the merged surface-ruin feature under HardFog."
Assert-NotContains $readme '### SurfaceRuins' "Root README must not list SurfaceRuins as an independent mod."

$buildOutput = dotnet build $solutionPath -t:Rebuild 2>&1
$buildOutput | Write-Host
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build HardFog.slnx -t:Rebuild failed."
}
Assert-NoCSharpWarnings $buildOutput "dotnet build HardFog.slnx -t:Rebuild"

Write-Host "HardFog merge tests passed."
