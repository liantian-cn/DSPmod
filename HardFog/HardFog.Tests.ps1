$ErrorActionPreference = "Stop"

$moduleDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionPath = Join-Path $moduleDir "HardFog.slnx"
$veinPlacementPath = Join-Path $moduleDir "VeinPlacementControl.cs"
$veinPlacementSource = Get-Content -LiteralPath $veinPlacementPath -Raw

function Assert-SourceContains([string]$needle, [string]$message) {
    if (-not $veinPlacementSource.Contains($needle)) {
        throw $message
    }
}

function Assert-SourceNotContains([string]$needle, [string]$message) {
    if ($veinPlacementSource.Contains($needle)) {
        throw $message
    }
}

Assert-SourceContains "private const int GroupPlacementAttempts = 24;" "Vein placement should try each placement phase 24 times."
Assert-SourceContains "private const int LongitudeExpansionPasses = 100;" "Vein placement should run 100 expanded longitude fallback passes."
Assert-SourceContains "private const float LongitudeExpansionStepDegrees = 30f;" "Vein placement should expand longitude by 30 degrees per fallback pass."
Assert-SourceContains "EnsureBirthPointDirection(planet);" "Vein placement should ensure PlanetData.birthPoint exists before placing groups."
Assert-SourceContains "float centerLongitude = LongitudeFromDirection(birthDirection);" "Vein placement target window should be centered on birthPoint longitude."
Assert-SourceContains "placedCenters.Add(birthDirection);" "The first vein group should be placed at birthPoint."
Assert-SourceContains "lastSuccessfulCenter" "Failed group placement should keep later groups anchored to the last successful center."
Assert-SourceContains "GetOriginalGroupCenter" "Failed group placement should preserve and block against the original group center."
Assert-SourceNotContains "groups.Sort(" "Vein groups should keep generation order instead of sorting by vein type."
Assert-SourceNotContains "initialLongitude" "Vein placement should not choose a random target longitude."

$buildOutput = dotnet build $solutionPath -t:Rebuild 2>&1
$buildOutput | Write-Host
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build HardFog.slnx -t:Rebuild failed."
}

Write-Host "HardFog compile check passed."
