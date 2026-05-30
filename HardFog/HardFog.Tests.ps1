$ErrorActionPreference = "Stop"

$moduleDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionPath = Join-Path $moduleDir "HardFog.slnx"

$buildOutput = dotnet build $solutionPath -t:Rebuild 2>&1
$buildOutput | Write-Host
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build HardFog.slnx -t:Rebuild failed."
}

Write-Host "HardFog compile check passed."
