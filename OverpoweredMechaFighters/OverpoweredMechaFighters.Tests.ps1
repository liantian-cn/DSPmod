$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePath = Join-Path $root "OverpoweredMechaFighters.cs"
$readmePath = Join-Path $root "README.md"

if (!(Test-Path $sourcePath)) {
    throw "OverpoweredMechaFighters.cs is missing."
}

if (!(Test-Path $readmePath)) {
    throw "README.md is missing."
}

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

Assert-Contains $source 'BepInPlugin\("me\.liantian\.plugin\.OverpoweredMechaFighters",\s*"OverpoweredMechaFighters",\s*"0\.0\.1"\)' "Plugin metadata is missing."
Assert-Contains $source 'private\s+const\s+float\s+RangeMultiplier\s*=\s*10f' "Range multiplier must be 10."
Assert-Contains $source 'private\s+const\s+float\s+DamageMultiplier\s*=\s*10f' "Damage multiplier must be 10."
Assert-Contains $source 'owner\s*==\s*-1' "Only mecha-owned fighters should be affected."
Assert-Contains $source 'craft\.owner\s*<=\s*0' "Fighter units must be identified through their owning fleet craft."
Assert-Contains $source 'craftPool\[craft\.owner\]' "Fighter units must inspect their owning fleet craft."
Assert-Contains $source 'ownerFleet\.owner\s*==\s*-1' "Fighter units must be scoped to mecha-owned fleet craft."
Assert-Contains $source 'isInvincible\s*=\s*true' "Mecha fighters must be made invincible."
Assert-Contains $source 'HarmonyPatch\(typeof\(CombatGroundSystem\),\s*"NewUnitComponent"\)' "Ground unit creation patch is missing."
Assert-Contains $source 'HarmonyPatch\(typeof\(CombatSpaceSystem\),\s*"NewUnitComponent"\)' "Space unit creation patch is missing."
Assert-Contains $source 'HarmonyPatch\(typeof\(CombatModuleComponent\),\s*"DiscoverLocalEnemy"\)' "Ground discovery range patch is missing."
Assert-Contains $source 'HarmonyPatch\(typeof\(CombatModuleComponent\),\s*"DiscoverSpaceEnemy"\)' "Space discovery range patch is missing."
Assert-Contains $source 'RestoreSensorRange' "Sensor range must be restored after discovery calls."
Assert-Contains $source 'BoostDamageForMechaFighter' "Damage multiplier helper is missing."
Assert-Contains $source 'RestoreDamageRatio' "Damage ratio must be restored after fighter behavior calls."
Assert-Contains $source 'BoostFighterBehavior' "Fighter behavior helper must combine damage and active-area boosts."
Assert-Contains $source 'Finalizer\(.*FighterBehaviorState' "Fighter behavior patches must restore temporary values through finalizers."
Assert-Contains $source 'BoostFighterFleetDesc' "Fighter range helper must boost the owning fleet descriptor."
Assert-NotContains $source 'private\s+static\s+bool\s+Prefix' "Harmony prefixes must not return bool; non-mecha fighters still need the vanilla original to run."
Assert-Contains $source 'RunBehavior_Engage_AttackLaser_Ground' "Ground laser fighter damage patch is missing."
Assert-Contains $source 'RunBehavior_Engage_AttackPlasma_Ground' "Ground plasma fighter damage patch is missing."
Assert-Contains $source 'RunBehavior_Engage_DefenseShield_Ground' "Ground shield fighter damage patch is missing."
Assert-Contains $source 'RunBehavior_Engage_SAttackLaser_Large' "Large space fighter damage patch is missing."
Assert-Contains $source 'RunBehavior_Engage_SAttackPlasma_Small' "Small space fighter damage patch is missing."
Assert-Contains $source 'HarmonyPatch\(typeof\(FleetComponent\),\s*"SensorLogic_Ground"\)' "Ground fleet sensor range patch is missing."
Assert-Contains $source 'HarmonyPatch\(typeof\(FleetComponent\),\s*"SensorLogic_Space"\)' "Space fleet sensor range patch is missing."
Assert-Contains $source 'HarmonyPatch\(typeof\(FleetComponent\),\s*"ActiveEnemyUnits_Ground"\)' "Ground active-enemy wake range patch is missing."
Assert-Contains $source 'HarmonyPatch\(typeof\(FleetComponent\),\s*"ActiveEnemyUnits_Space"\)' "Space active-enemy wake range patch is missing."
Assert-Contains $source 'HarmonyPatch\(typeof\(UnitComponent\),\s*"PostGameTick_Ground"\)' "Ground post-tick target retention range patch is missing."
Assert-Contains $source 'HarmonyPatch\(typeof\(UnitComponent\),\s*"PostGameTick_Space"\)' "Space post-tick target retention range patch is missing."
Assert-Contains $source 'BoostFleetDescForMechaFleet' "Fleet descriptor range multiplier helper is missing."
Assert-Contains $source 'RestoreFleetDesc' "Fleet descriptor range values must be restored after vanilla calls."
Assert-NotContains $source 'Transpiler' "Range patch must avoid fragile IL transpilers for this mod."
Assert-Contains $source 'HarmonyPatch\(typeof\(CombatGroundSystem\),\s*"GameTick"\)' "Loaded or already-spawned ground fighters must be refreshed from a combat-system tick hook."
Assert-Contains $source 'HarmonyPatch\(typeof\(CombatSpaceSystem\),\s*"GameTick"\)' "Loaded or already-spawned space fighters must be refreshed from a combat-system tick hook."
Assert-NotContains $source 'GameMain\.history\.combatDroneDamageRatio\s*=' "Mod must not permanently write GameHistoryData.combatDroneDamageRatio."
Assert-NotContains $source 'GameMain\.history\.combatShipDamageRatio\s*=' "Mod must not permanently write GameHistoryData.combatShipDamageRatio."

Assert-Contains $readme 'owner == -1' "README must document the mecha-owned fighter scope."
Assert-Contains $readme '10x' "README must document 10x range/damage behavior."
Assert-Contains $readme 'isInvincible' "README must document the invincibility mechanism."
Assert-Contains $readme 'does not affect.*battle base' "README must document that battle base fighters are excluded."
Assert-Contains $readme 'GUI|config' "README must document that the mod has no GUI/config."

Write-Host "OverpoweredMechaFighters static checks passed."
