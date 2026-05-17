$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePath = Join-Path $root "FogThreatDampener.cs"
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

Assert-Contains $source 'BepInPlugin\("me\.liantian\.plugin\.FogThreatDampener",\s*"FogThreatDampener",\s*"0\.0\.1"\)' "FogThreatDampener plugin metadata is missing."
Assert-Contains $source 'HarmonyPatch\(typeof\(EnemyDFHiveSystem\),\s*"KeyTickLogic"\)' "KeyTickLogic patch is missing."
Assert-Contains $source 'DampenIntervalHiveTicks\s*=\s*30' "Dampening interval must be 30 Hive ticks."
Assert-Contains $source 'ThreatMultiplier\s*=\s*0\.99f' "Threat multiplier must be 0.99f."
Assert-Contains $source '__instance\.ticks\s*%\s*DampenIntervalHiveTicks\s*!=\s*0' "Patch must run only at the configured interval."
Assert-Contains $source '__instance\.evolve\.waveTicks\s*>\s*0' "Patch must skip active assault waves."
Assert-Contains $source '__instance\.evolve\.waveAsmTicks\s*>\s*0' "Patch must skip assembly state."
Assert-Contains $source '__instance\.evolve\.threat\s*>=\s*__instance\.evolve\.maxThreat' "Patch must skip full-threat assembly trigger state."
Assert-Contains $source '__instance\.evolve\.threat\s*=\s*\(int\)\(\(float\)__instance\.evolve\.threat\s*\*\s*ThreatMultiplier\)' "Patch must multiply current threat by the configured multiplier."
Assert-NotContains $source 'threatshr\s*=' "Patch must not assign threatshr."
Assert-NotContains $source 'DetermineRelayDemand' "Relay demand logic must not be copied into this mod."
Assert-NotContains $source 'HarmonyPatch\(typeof\(DFRelayComponent\),\s*"RelaySailLogic"\)' "RelaySailLogic must not be patched."

Assert-Contains $readme '30 Hive ticks' "README must document the 30 Hive tick interval."
Assert-Contains $readme '1%' "README must document the 1% reduction."
Assert-Contains $readme 'threatshr' "README must document that active threat buffering is not modified."
Assert-Contains $readme 'GUI|config' "README must document that the mod has no GUI/config."

Write-Host "FogThreatDampener static checks passed."
