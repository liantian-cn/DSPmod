$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePath = Join-Path $root "RelayFaster.cs"
$readmePath = Join-Path $root "README.md"

$source = Get-Content -Raw -Encoding UTF8 $sourcePath
$readme = Get-Content -Raw -Encoding UTF8 $readmePath

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

Assert-Contains $source 'BepInPlugin\("me\.liantian\.plugin\.RelayFaster",\s*"RelayFaster",\s*"0\.0\.1"\)' "RelayFaster plugin metadata is missing."
Assert-Contains $source 'HarmonyPatch\(typeof\(EnemyDFHiveSystem\),\s*"KeyTickLogic"\)' "KeyTickLogic patch is missing."
Assert-Contains $source 'HarmonyPatch\(typeof\(EnemyDFHiveSystem\),\s*"DetermineRelayDemand"\)' "DetermineRelayDemand patch is missing."
Assert-Contains $source 'DispatchIntervalTicks\s*=\s*60' "Dispatch interval must be 60 Hive ticks."
Assert-Contains $source 'OriginalMatterStatPeriodTicks\s*=\s*600' "Original 600-tick matter-stat period must remain explicit."
Assert-Contains $source 'if\s*\(__instance\.ticks\s*%\s*OriginalMatterStatPeriodTicks\s*==\s*0\)' "Extra dispatch must skip original 600-tick calls."
Assert-Contains $source 'ReplaceRandomProbabilityWithMarkerGate' "Probability transpiler helper is missing."
Assert-NotContains $source 'HarmonyPatch\(typeof\(DFRelayComponent\),\s*"RelaySailLogic"\)' "RelaySailLogic must not be patched."
Assert-NotContains $source 'relayNeutralizedCounter\s*=\s*0' "Relay neutralized counter must not be reset."

Assert-Contains $readme '10' "README must document the original 10-minute cadence."
Assert-Contains $readme '1' "README must document the new 1-minute cadence."
Assert-Contains $readme '0%' "README must document the no-marker 0% probability."
Assert-Contains $readme '100%' "README must document the marker-target 100% probability."
Assert-Contains $readme 'GUI|config' "README must document that the mod has no GUI/config."

Write-Host "RelayFaster static checks passed."
