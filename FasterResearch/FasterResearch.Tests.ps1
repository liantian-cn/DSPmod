$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePath = Join-Path $root "FasterResearch.cs"
$source = Get-Content -Raw -Encoding UTF8 $sourcePath

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

Assert-Contains $source 'BepInPlugin\("me\.liantian\.plugin\.FasterResearch",\s*"FasterResearch",\s*"0\.0\.1"\)' "FasterResearch plugin metadata is missing."
Assert-Contains $source 'HarmonyPatch\(typeof\(LabComponent\),\s*"InternalUpdateResearch"\)' "LabComponent.InternalUpdateResearch patch is missing."
Assert-Contains $source 'Multiplier\s*=\s*24' "Research multiplier must remain 24."
Assert-Contains $source 'ref\s+float\s+research_speed' "Patch must modify InternalUpdateResearch's research_speed argument before vanilla code runs."
Assert-Contains $source 'research_speed\s*\*=\s*Multiplier' "Patch must increase the research speed cap; lowering matrixPoints alone only reduces material cost."
Assert-Contains $source 'ApplyCurrentTechMultiplier\(\)' "Patch must lower matrixPoints before vanilla InternalUpdateResearch consumes matrices."
Assert-NotContains $source 'lastDividedTechId' "Patch must not use global tech-id dedupe; multiple factories can reload the same static matrixPoints for the same tech."
Assert-NotContains $source 'techId\s*==\s*lastDividedTechId' "Global same-tech skip would leave matrixPoints unmodified after another factory reloads them."

Write-Host "FasterResearch static checks passed."
