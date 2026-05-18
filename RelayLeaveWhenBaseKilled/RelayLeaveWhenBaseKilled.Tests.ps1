$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePath = Join-Path $root "RelayLeaveWhenBaseKilled.cs"
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

Assert-Contains $source 'BepInPlugin\("me\.liantian\.plugin\.RelayLeaveWhenBaseKilled",\s*"RelayLeaveWhenBaseKilled",\s*"0\.0\.1"\)' "RelayLeaveWhenBaseKilled plugin metadata is missing."
Assert-Contains $source 'HarmonyPatch\(typeof\(EnemyDFGroundSystem\),\s*"NotifyBaseKilled"\)' "NotifyBaseKilled patch is missing."
Assert-Contains $source 'DFGBaseComponent\s+\w+\s*=\s*__instance\.bases\.buffer\[baseId\]' "Patch must inspect the killed base component."
Assert-Contains $source '\.GetRelay\(\)' "Patch must use the base-bound relay reference."
Assert-Contains $source '\.LeaveBase\(\)' "Patch must force the relay to leave the destroyed base."
Assert-Contains $source 'relay\.hive\.relayNeutralizedCounter\+\+' "Patch must preserve vanilla relay neutralized accounting."
Assert-Contains $source 'relay\.baseId\s*==\s*baseId' "Patch must only affect the relay still bound to the killed base."
Assert-Contains $source 'relay\.stage\s*==\s*2' "Patch must only affect landed relays."
Assert-Contains $source 'relay\.baseState\s*==\s*2' "Patch must only affect relays maintaining a ground base."
Assert-NotContains $source 'HarmonyPatch\(typeof\(DFRelayComponent\)' "Mod must not patch DFRelayComponent directly."
Assert-NotContains $source 'NotifyBaseRemoving' "Mod must not reuse final base-removal logic."
Assert-NotContains $source 'RemoveBasePit|RemoveBase\(' "Mod must not remove the pit or base object directly."

Assert-Contains $readme 'NotifyBaseKilled' "README must document the patched base-kill hook."
Assert-Contains $readme 'LeaveBase' "README must document that relays leave immediately."
Assert-Contains $readme 'relayNeutralizedCounter' "README must document neutralized counter behavior."
Assert-Contains $readme 'does not.*base pit' "README must document that pit removal remains vanilla."
Assert-Contains $readme 'GUI|config' "README must document that the mod has no GUI/config."

Write-Host "RelayLeaveWhenBaseKilled static checks passed."
