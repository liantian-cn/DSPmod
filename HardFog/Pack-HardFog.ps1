$ErrorActionPreference = "Stop"

$moduleDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionPath = Join-Path $moduleDir "HardFog.slnx"
$packageDir = Join-Path $moduleDir "Package"
$manifestPath = Join-Path $packageDir "manifest.json"

if (!(Test-Path -LiteralPath $solutionPath)) {
    throw "HardFog.slnx is missing."
}

if (!(Test-Path -LiteralPath $manifestPath)) {
    throw "Package manifest is missing: $manifestPath"
}

$manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath | ConvertFrom-Json
$version = [string] $manifest.version_number
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Package manifest version_number is empty."
}

$requiredPackageFiles = @("manifest.json", "README.md", "icon.png")
foreach ($file in $requiredPackageFiles) {
    $path = Join-Path $packageDir $file
    if (!(Test-Path -LiteralPath $path)) {
        throw "Required package file is missing: $path"
    }
}

$buildOutput = dotnet build $solutionPath -c Release -t:Rebuild 2>&1
$buildOutput | Write-Host
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build HardFog.slnx -c Release -t:Rebuild failed."
}

$releaseDir = Join-Path $moduleDir "bin\Release"
$dllPath = Join-Path $releaseDir "HardFog.dll"
if (!(Test-Path -LiteralPath $dllPath)) {
    throw "Release build did not produce HardFog.dll: $dllPath"
}

$artifactName = "liantian-HardFog-$version"
$packageOutputDir = Join-Path $releaseDir "package"
$stagingDir = Join-Path $packageOutputDir $artifactName
$zipPath = Join-Path $packageOutputDir "$artifactName.zip"

if (Test-Path -LiteralPath $stagingDir) {
    Remove-Item -LiteralPath $stagingDir -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null
Copy-Item -LiteralPath $dllPath -Destination $stagingDir -Force
Copy-Item -Path (Join-Path $packageDir "*") -Destination $stagingDir -Recurse -Force

Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $zipPath -Force
Write-Host "Package created: $zipPath"
