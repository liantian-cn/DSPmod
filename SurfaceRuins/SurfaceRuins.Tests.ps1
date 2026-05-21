$ErrorActionPreference = "Stop"

$moduleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePath = Join-Path $moduleRoot "SurfaceRuins.cs"
$projectPath = Join-Path $moduleRoot "SurfaceRuins.csproj"

if (-not (Test-Path $sourcePath)) {
    throw "Missing source file: $sourcePath"
}

if (-not (Test-Path $projectPath)) {
    throw "Missing project file: $projectPath"
}

$source = Get-Content -Raw -Encoding UTF8 $sourcePath
$project = Get-Content -Raw -Encoding UTF8 $projectPath
$menuTextCn = -join @([char]0x661f, [char]0x8868, [char]0x5e9f, [char]0x589f)
$buttonTextCn = -join @(
    [char]0x5728,
    [char]0x5f53,
    [char]0x524d,
    [char]0x661f,
    [char]0x7403,
    [char]0x6784,
    [char]0x9020,
    [char]0x5e9f,
    [char]0x589f)

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
Assert-Contains $source '"surface-ruins-construct-current-planet"' "Missing construct button localization key."
Assert-Contains $source '"Construct ruins on current planet"' "Missing construct button English localization."
Assert-Contains $source ([regex]::Escape("`"$buttonTextCn`"")) "Missing construct button Chinese localization."
Assert-Contains $source "MyConfigWindow\.OnUICreated \+= CreateUI" "SurfaceRuins must register its UXAssist UI creation callback."
Assert-Contains $source "MyConfigWindow\.OnUICreated -= CreateUI" "SurfaceRuins must unregister its UXAssist UI creation callback."
Assert-Contains $source "private const short BasePitRuinModelIndex = 406;" "Base pit ruin model index must be 406."
Assert-Contains $source "private const int Level30BasePitLifeTime = -31;" "Level 30 base pit ruin lifetime must be -31."
Assert-Contains $source "private const float DuplicateRuinRadius = 50f;" "Duplicate ruin radius must be 50."
Assert-Contains $source "AddRuinDataWithComponent" "SurfaceRuins must add ruins through PlanetFactory.AddRuinDataWithComponent."
Assert-Contains $source "UIRealtimeTip\.Popup" "SurfaceRuins should report result through UIRealtimeTip."

$pointCount = ([regex]::Matches($source, "new Vector3\(")).Count
if ($pointCount -ne 153) {
    throw "Expected 153 embedded ruin positions, found $pointCount."
}

Assert-NotContains $source "CreateEnemyPlanetBase" "Fake ruins must not create real Dark Fog bases."
Assert-NotContains $source "DFGBaseComponent" "Fake ruins must not allocate DFGBaseComponent."
Assert-NotContains $source "DFRelayComponent" "Fake ruins must not allocate DFRelayComponent."
Assert-NotContains $source "NotifyBaseKilled" "Fake ruins must not use the real base death path."

Write-Host "SurfaceRuins static tests passed."
