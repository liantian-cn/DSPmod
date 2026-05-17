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
Assert-Contains $source 'HarmonyPatch\(typeof\(TechProto\),\s*"GetHashNeeded"\)' "TechProto.GetHashNeeded patch is missing."
Assert-Contains $source 'Multiplier\s*=\s*24' "Research multiplier must remain 24."
Assert-Contains $source 'ref\s+long\s+__result' "GetHashNeeded patch must modify the returned total hash requirement."
Assert-Contains $source 'DivideRoundUp\(__result,\s*Multiplier\)' "Hash requirement must be divided with round-up so tiny requirements do not become zero."
Assert-Contains $source 'SyncCurrentTechHashNeeded' "Existing TechState.hashNeeded must be synchronized for already-loaded saves."
Assert-Contains $source 'HarmonyPatch\(typeof\(GameHistoryData\),\s*"SetForNewGame"\)' "New games must synchronize TechState.hashNeeded after initialization."
Assert-Contains $source 'HarmonyPatch\(typeof\(GameHistoryData\),\s*"Import"\)' "Loaded saves must synchronize TechState.hashNeeded after import."
Assert-NotContains $source 'HarmonyPatch\(typeof\(LabComponent\),\s*"InternalUpdateResearch"\)' "Research acceleration should come from total hash requirement, not lab tick speed/material patches."
Assert-NotContains $source 'research_speed\s*\*=' "Patch must not multiply lab research speed in hash-needed mode."
Assert-NotContains $source 'LabComponent\.matrixPoints' "Patch must not alter matrixPoints in hash-needed mode."
Assert-NotContains $source 'lastDividedTechId' "Patch must not use global tech-id dedupe; multiple factories can reload the same static matrixPoints for the same tech."
Assert-NotContains $source 'techId\s*==\s*lastDividedTechId' "Global same-tech skip would leave matrixPoints unmodified after another factory reloads them."

Write-Host "FasterResearch static checks passed."
