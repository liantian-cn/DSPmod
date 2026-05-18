$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePath = Join-Path $root "SmartRelayDispatch.cs"
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

Assert-Contains $source 'BepInPlugin\("me\.liantian\.plugin\.SmartRelayDispatch",\s*"SmartRelayDispatch",\s*"0\.0\.1"\)' "SmartRelayDispatch plugin metadata is missing."
Assert-Contains $source 'HarmonyPatch\(typeof\(EnemyDFHiveSystem\),\s*"DetermineRelayDemand"\)' "DetermineRelayDemand patch is missing."
Assert-Contains $source 'ReplaceRandomProbabilityWithMarkerGate' "Probability transpiler helper is missing."
Assert-Contains $source 'relayNeutralizedCounterField' "Probability transpiler must anchor on relayNeutralizedCounter so marker target selection is not patched."
Assert-Contains $source 'FindDispatchProbabilityGate' "Probability transpiler must locate the dispatch probability gate explicitly."
Assert-NotContains $source 'HarmonyPatch\(typeof\(EnemyDFHiveSystem\),\s*"KeyTickLogic"\)' "Relay demand cadence must remain vanilla."
Assert-NotContains $source 'DispatchIntervalTicks' "Relay demand cadence acceleration must be removed."
Assert-NotContains $source 'OriginalMatterStatPeriodTicks' "Relay demand cadence acceleration must be removed."
Assert-NotContains $source 'HarmonyPatch\(typeof\(DFRelayComponent\),\s*"CheckLandCondition"\)' "Marker-targeted relays must not skip landing condition checks."
Assert-NotContains $source 'HarmonyPatch\(typeof\(EnemyDFHiveSystem\),\s*nameof\(EnemyDFHiveSystem\.CheckRelayCoLandingCondition\)' "Marker-targeted relays must not skip co-landing condition checks."
Assert-NotContains $source 'dstMarkerId\s*>\s*0[\s\S]*__result\s*=\s*true' "Marker-targeted relays must not force landing condition success."
Assert-NotContains $source 'sailingRelay\s*!=\s*null\s*&&\s*sailingRelay\.dstMarkerId\s*>\s*0[\s\S]*__result\s*=\s*false' "Marker-targeted relays must not force co-landing condition failure."
Assert-NotContains $source 'relayNeutralizedCounter\s*=\s*0' "Relay neutralized counter must not be reset."

Assert-Contains $readme '0%' "README must document the no-marker 0% probability."
Assert-Contains $readme '100%' "README must document the marker-target 100% probability."
Assert-NotContains $readme '1\s*(分钟|minute|min)' "README must not document a 1-minute relay demand cadence."
Assert-NotContains $readme '(bypass|skip|ignore)[^\r\n]*landing|landing[^\r\n]*(bypass|skip|ignore)' "README must not document landing-check bypass behavior."
Assert-Contains $readme 'GUI|config' "README must document that the mod has no GUI/config."

Write-Host "SmartRelayDispatch static checks passed."
