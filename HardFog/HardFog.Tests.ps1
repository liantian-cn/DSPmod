$ErrorActionPreference = "Stop"

$moduleDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $moduleDir
$projectPath = Join-Path $moduleDir "HardFog.csproj"
$solutionPath = Join-Path $moduleDir "HardFog.slnx"
$sourcePath = Join-Path $moduleDir "HardFog.cs"
$hiveSourcePath = Join-Path $repoRoot "Assembly-CSharp\EnemyDFHiveSystem.cs"
$factorySourcePath = Join-Path $repoRoot "Assembly-CSharp\PlanetFactory.cs"

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-NoCSharpWarnings {
    param(
        [string[]]$Output,
        [string]$Command
    )

    Assert-True (($Output -join "`n") -notmatch "warning CS") "$Command must not emit C# warnings."
}

[xml]$project = Get-Content -Raw $projectPath
$compileIncludes = @($project.Project.ItemGroup.Compile | ForEach-Object { $_.Include })
Assert-True ($compileIncludes -contains "HardFog.cs") "HardFog.csproj must compile HardFog.cs."

$source = Get-Content -Raw $sourcePath
$hiveSource = Get-Content -Raw $hiveSourcePath
$factorySource = Get-Content -Raw $factorySourcePath
Assert-True ($hiveSource -match "public\s+int\s+tindersArrivingInTransit\s*;") "Current game dump must expose EnemyDFHiveSystem.tindersArrivingInTransit."
Assert-True ($source -notmatch "\btindersInTransit\b") "HardFog must not reference removed EnemyDFHiveSystem.tindersInTransit."
Assert-True ($source -match "\bhive\.SetForNewGame\(\);") "HardFog should initialize newly created hives through EnemyDFHiveSystem.SetForNewGame()."
Assert-True ($factorySource -match "public\s+void\s+KillEnemyFinally\s*\(\s*int\s+enemyId\s*,\s*ref\s+CombatStat\s+combatStat\s*\)") "Current game dump must expose PlanetFactory.KillEnemyFinally(int, ref CombatStat)."
Assert-True ($factorySource -notmatch "KillEnemyFinally\s*\(\s*Player\s+") "Current game dump must not expose the removed PlanetFactory.KillEnemyFinally(Player, int, ref CombatStat) overload."
Assert-True ($source -match "planetFactory\.KillEnemyFinally\s*\(\s*i\s*,\s*ref\s+CombatStat\.empty\s*\)") "HardFog must call the current PlanetFactory.KillEnemyFinally(int, ref CombatStat) overload."
Assert-True ($source -notmatch "KillEnemyFinally\s*\(\s*player\s*,") "HardFog must not call the removed PlanetFactory.KillEnemyFinally(Player, int, ref CombatStat) overload."
Assert-True ($source -match 'BepInPlugin\("me\.liantian\.plugin\.HardFog",\s*"HardFog",\s*"0\.0\.7"\)') "HardFog compatibility build should publish as version 0.0.7."

$expectedButtonKeys = @("clear_current_planet", "clear_current_star", "fill_hive")
Assert-True (([regex]::Matches($source, 'wnd\.AddButton\(').Count) -eq 3) "HardFog UI must expose exactly three buttons."
foreach ($key in $expectedButtonKeys) {
    Assert-True ($source -match "wnd\.AddButton\([^;]*`"$([regex]::Escape($key))`"") "HardFog UI must keep the '$key' button."
    Assert-True ($source -match "I18N\.Add\(\s*`"$([regex]::Escape($key))`"") "HardFog must keep the '$key' translation."
}

$removedFeatureTokens = @(
    "ConfigEntry",
    "Harmony",
    "MyCheckBox",
    "MoreFrequentRelays",
    "StarResetHive",
    "StarFillHive",
    "ClearAllVein",
    "BatchAddVein",
    "AddVein",
    "AddRuins",
    "GeneratePoints",
    "Clear all vein",
    "add-vein",
    "add-ruins"
)
foreach ($token in $removedFeatureTokens) {
    Assert-True ($source -notmatch [regex]::Escape($token)) "HardFog must remove redundant feature token '$token'."
}

$projectBuildOutput = dotnet build $projectPath -c Release -t:Rebuild 2>&1
$projectBuildOutput | Write-Host
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build HardFog.csproj -c Release -t:Rebuild failed."
}
Assert-NoCSharpWarnings $projectBuildOutput "dotnet build HardFog.csproj -c Release -t:Rebuild"

$solutionBuildOutput = dotnet build $solutionPath -t:Rebuild 2>&1
$solutionBuildOutput | Write-Host
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build HardFog.slnx -t:Rebuild failed."
}
Assert-True (($solutionBuildOutput -join "`n") -match "HardFog\s+->") "dotnet build HardFog.slnx must build the HardFog project."
Assert-NoCSharpWarnings $solutionBuildOutput "dotnet build HardFog.slnx -t:Rebuild"

Write-Host "HardFog checks passed."
