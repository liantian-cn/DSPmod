using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace HardFog
{
    [HarmonyPatch]
    internal static class OverpoweredMechaFightersControl
    {
        private const string PatchGuid = "me.liantian.plugin.HardFog.OverpoweredMechaFighters";
        private const float RangeMultiplier = 5f;
        private const float DamageMultiplier = 5f;

        internal static ConfigEntry<bool> EnabledConfig { get; private set; }

        private static ManualLogSource Log;
        private static Harmony harmony;
        private static EventHandler settingChangedHandler;

        internal static void Init(ConfigEntry<bool> enabledConfig, ManualLogSource log)
        {
            if (EnabledConfig != null && settingChangedHandler != null)
            {
                EnabledConfig.SettingChanged -= settingChangedHandler;
            }

            Log = log;
            EnabledConfig = enabledConfig;
            settingChangedHandler = OnSettingChanged;
            EnabledConfig.SettingChanged += settingChangedHandler;
            SetActive(EnabledConfig.Value);
        }

        internal static void Uninit()
        {
            if (EnabledConfig != null && settingChangedHandler != null)
            {
                EnabledConfig.SettingChanged -= settingChangedHandler;
            }

            SetActive(false);
            settingChangedHandler = null;
            EnabledConfig = null;
            Log = null;
        }

        private static void OnSettingChanged(object sender, EventArgs args)
        {
            SetActive(EnabledConfig != null && EnabledConfig.Value);
        }

        private static void SetActive(bool active)
        {
            if (active)
            {
                if (harmony != null)
                {
                    return;
                }

                harmony = Harmony.CreateAndPatchAll(typeof(OverpoweredMechaFightersControl), PatchGuid);
                SetLoadedMechaFightersInvincible(true);
                Log?.LogInfo("OverpoweredMechaFighters enabled");
                return;
            }

            if (harmony == null)
            {
                return;
            }

            harmony.UnpatchSelf();
            harmony = null;
            SetLoadedMechaFightersInvincible(false);
            Log?.LogInfo("OverpoweredMechaFighters disabled");
        }

        private static bool IsMechaFighter(ref CraftData craft, CraftData[] craftPool)
        {
            if (craftPool == null || craft.id <= 0 || craft.owner <= 0 || craft.owner >= craftPool.Length || craft.unitId <= 0)
            {
                return false;
            }

            ref CraftData ownerFleet = ref craftPool[craft.owner];
            return ownerFleet.id == craft.owner && ownerFleet.owner == -1;
        }

        private static bool TryGetMechaFleetDesc(ref CraftData craft, CraftData[] craftPool, out PrefabDesc fleetDesc)
        {
            fleetDesc = null;
            if (!IsMechaFighter(ref craft, craftPool))
            {
                return false;
            }

            ref CraftData ownerFleet = ref craftPool[craft.owner];
            if (ownerFleet.modelIndex < 0 || ownerFleet.modelIndex >= PlanetFactory.PrefabDescByModelIndex.Length)
            {
                return false;
            }

            fleetDesc = PlanetFactory.PrefabDescByModelIndex[ownerFleet.modelIndex];
            return fleetDesc != null;
        }

        private static void MakeInvincible(ref CraftData craft, CraftData[] craftPool, SkillSystem skillSystem)
        {
            SetInvincible(ref craft, craftPool, skillSystem, true);
        }

        private static void SetInvincible(ref CraftData craft, CraftData[] craftPool, SkillSystem skillSystem, bool invincible)
        {
            if (!IsMechaFighter(ref craft, craftPool))
            {
                return;
            }

            craft.isInvincible = invincible;
            if (!invincible || skillSystem == null || craft.combatStatId <= 0)
            {
                return;
            }

            ref CombatStat combatStat = ref skillSystem.combatStats.buffer[craft.combatStatId];
            if (combatStat.id == craft.combatStatId)
            {
                combatStat.hp = combatStat.hpMax;
                combatStat.hpIncoming = 0;
            }
        }

        private struct SensorRangeState
        {
            public bool boosted;
            public float originalRange;
        }

        private struct DamageRatioState
        {
            public bool boosted;
            public float originalRatio;
        }

        private struct FleetDescState
        {
            public bool boosted;
            public PrefabDesc desc;
            public float originalSensorRange;
            public float originalActiveArea;
        }

        private struct FighterBehaviorState
        {
            public DamageRatioState damageRatio;
            public FleetDescState fleetDesc;
        }

        private static void BoostSensorRange(CombatModuleComponent module, ref SensorRangeState __state)
        {
            __state = default(SensorRangeState);
            if (module == null || module.entityId != 0)
            {
                return;
            }

            __state.boosted = true;
            __state.originalRange = module.sensorRange;
            module.sensorRange *= RangeMultiplier;
        }

        private static void RestoreSensorRange(CombatModuleComponent module, SensorRangeState state)
        {
            if (state.boosted && module != null)
            {
                module.sensorRange = state.originalRange;
            }
        }

        private static void BoostDamageForMechaFighter(ref CraftData craft, CraftData[] craftPool, ref CombatUpgradeData combatUpgradeData, ref DamageRatioState __state)
        {
            __state = default(DamageRatioState);
            if (!IsMechaFighter(ref craft, craftPool))
            {
                return;
            }

            __state.boosted = true;
            __state.originalRatio = combatUpgradeData.combatDroneDamageRatio;
            combatUpgradeData.combatDroneDamageRatio *= DamageMultiplier;
        }

        private static void RestoreDamageRatio(ref CombatUpgradeData combatUpgradeData, DamageRatioState state)
        {
            if (state.boosted)
            {
                combatUpgradeData.combatDroneDamageRatio = state.originalRatio;
            }
        }

        private static void BoostFleetDesc(PrefabDesc pdesc, ref FleetDescState __state)
        {
            __state = default(FleetDescState);
            if (pdesc == null)
            {
                return;
            }

            __state.boosted = true;
            __state.desc = pdesc;
            __state.originalSensorRange = pdesc.fleetSensorRange;
            __state.originalActiveArea = pdesc.fleetMaxActiveArea;
            pdesc.fleetSensorRange *= RangeMultiplier;
            pdesc.fleetMaxActiveArea *= RangeMultiplier;
        }

        private static void BoostFleetDescForMechaFleet(ref CraftData fleetCraft, PrefabDesc pdesc, ref FleetDescState __state)
        {
            __state = default(FleetDescState);
            if (fleetCraft.owner == -1)
            {
                BoostFleetDesc(pdesc, ref __state);
            }
        }

        private static void RestoreFleetDesc(FleetDescState state)
        {
            if (!state.boosted || state.desc == null)
            {
                return;
            }

            state.desc.fleetSensorRange = state.originalSensorRange;
            state.desc.fleetMaxActiveArea = state.originalActiveArea;
        }

        private static void BoostFighterBehavior(ref CraftData craft, CraftData[] craftPool, ref CombatUpgradeData combatUpgradeData, ref FighterBehaviorState __state)
        {
            __state = default(FighterBehaviorState);
            BoostDamageForMechaFighter(ref craft, craftPool, ref combatUpgradeData, ref __state.damageRatio);
            BoostFighterFleetDesc(ref craft, craftPool, ref __state.fleetDesc);
        }

        private static void BoostFighterFleetDesc(ref CraftData craft, CraftData[] craftPool, ref FleetDescState __state)
        {
            __state = default(FleetDescState);
            if (TryGetMechaFleetDesc(ref craft, craftPool, out PrefabDesc fleetDesc))
            {
                BoostFleetDesc(fleetDesc, ref __state);
            }
        }

        private static void RestoreFighterBehavior(ref CombatUpgradeData combatUpgradeData, FighterBehaviorState state)
        {
            RestoreDamageRatio(ref combatUpgradeData, state.damageRatio);
            RestoreFleetDesc(state.fleetDesc);
        }

        private static void SetLoadedMechaFightersInvincible(bool invincible)
        {
            GameData data = GameMain.data;
            if (data?.factories != null)
            {
                for (int i = 0; i < data.factories.Length; i++)
                {
                    RefreshGroundMechaFighters(data.factories[i]?.combatGroundSystem, invincible);
                }
            }

            RefreshSpaceMechaFighters(GameMain.spaceSector?.combatSpaceSystem, invincible);
        }

        private static void RefreshGroundMechaFighters(CombatGroundSystem combatSystem)
        {
            RefreshGroundMechaFighters(combatSystem, true);
        }

        private static void RefreshGroundMechaFighters(CombatGroundSystem combatSystem, bool invincible)
        {
            if (combatSystem?.factory?.craftPool == null || combatSystem.units == null)
            {
                return;
            }

            CraftData[] craftPool = combatSystem.factory.craftPool;
            UnitComponent[] units = combatSystem.units.buffer;
            int cursor = combatSystem.units.cursor;
            for (int i = 1; i < cursor; i++)
            {
                ref UnitComponent unit = ref units[i];
                if (unit.id != i || unit.craftId <= 0)
                {
                    continue;
                }

                ref CraftData craft = ref craftPool[unit.craftId];
                SetInvincible(ref craft, craftPool, combatSystem.factory.skillSystem, invincible);
            }
        }

        private static void RefreshSpaceMechaFighters(CombatSpaceSystem combatSystem)
        {
            RefreshSpaceMechaFighters(combatSystem, true);
        }

        private static void RefreshSpaceMechaFighters(CombatSpaceSystem combatSystem, bool invincible)
        {
            if (combatSystem?.spaceSector?.craftPool == null || combatSystem.units == null)
            {
                return;
            }

            CraftData[] craftPool = combatSystem.spaceSector.craftPool;
            UnitComponent[] units = combatSystem.units.buffer;
            int cursor = combatSystem.units.cursor;
            for (int i = 1; i < cursor; i++)
            {
                ref UnitComponent unit = ref units[i];
                if (unit.id != i || unit.craftId <= 0)
                {
                    continue;
                }

                ref CraftData craft = ref craftPool[unit.craftId];
                SetInvincible(ref craft, craftPool, combatSystem.spaceSector.skillSystem, invincible);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CombatGroundSystem), "NewUnitComponent")]
        private static void CombatGroundSystemNewUnitComponentPostfix(CombatGroundSystem __instance, int craftId)
        {
            if (__instance?.factory?.craftPool == null || craftId <= 0 || craftId >= __instance.factory.craftPool.Length)
            {
                return;
            }

            ref CraftData craft = ref __instance.factory.craftPool[craftId];
            MakeInvincible(ref craft, __instance.factory.craftPool, __instance.factory.skillSystem);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CombatSpaceSystem), "NewUnitComponent")]
        private static void CombatSpaceSystemNewUnitComponentPostfix(CombatSpaceSystem __instance, int craftId)
        {
            if (__instance?.spaceSector?.craftPool == null || craftId <= 0 || craftId >= __instance.spaceSector.craftPool.Length)
            {
                return;
            }

            ref CraftData craft = ref __instance.spaceSector.craftPool[craftId];
            MakeInvincible(ref craft, __instance.spaceSector.craftPool, __instance.spaceSector.skillSystem);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CombatGroundSystem), "GameTick")]
        private static void CombatGroundSystemGameTickPostfix(CombatGroundSystem __instance, long tick)
        {
            if (tick % 60 == 0)
            {
                RefreshGroundMechaFighters(__instance);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CombatSpaceSystem), "GameTick")]
        private static void CombatSpaceSystemGameTickPostfix(CombatSpaceSystem __instance, long tick)
        {
            if (tick % 60 == 0)
            {
                RefreshSpaceMechaFighters(__instance);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CombatModuleComponent), "DiscoverLocalEnemy")]
        private static void CombatModuleComponentDiscoverLocalEnemyPrefix(CombatModuleComponent __instance, ref SensorRangeState __state)
        {
            BoostSensorRange(__instance, ref __state);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CombatModuleComponent), "DiscoverLocalEnemy")]
        private static void CombatModuleComponentDiscoverLocalEnemyPostfix(CombatModuleComponent __instance, SensorRangeState __state)
        {
            RestoreSensorRange(__instance, __state);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CombatModuleComponent), "DiscoverSpaceEnemy")]
        private static void CombatModuleComponentDiscoverSpaceEnemyPrefix(CombatModuleComponent __instance, ref SensorRangeState __state)
        {
            BoostSensorRange(__instance, ref __state);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CombatModuleComponent), "DiscoverSpaceEnemy")]
        private static void CombatModuleComponentDiscoverSpaceEnemyPostfix(CombatModuleComponent __instance, SensorRangeState __state)
        {
            RestoreSensorRange(__instance, __state);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FleetComponent), "SensorLogic_Ground")]
        private static void FleetComponentSensorLogicGroundPrefix(ref CraftData craft, PrefabDesc pdesc, ref FleetDescState __state)
        {
            BoostFleetDescForMechaFleet(ref craft, pdesc, ref __state);
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(FleetComponent), "SensorLogic_Ground")]
        private static void FleetComponentSensorLogicGroundFinalizer(PrefabDesc pdesc, FleetDescState __state)
        {
            RestoreFleetDesc(__state);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FleetComponent), "SensorLogic_Space")]
        private static void FleetComponentSensorLogicSpacePrefix(ref CraftData craft, PrefabDesc pdesc, ref FleetDescState __state)
        {
            BoostFleetDescForMechaFleet(ref craft, pdesc, ref __state);
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(FleetComponent), "SensorLogic_Space")]
        private static void FleetComponentSensorLogicSpaceFinalizer(PrefabDesc pdesc, FleetDescState __state)
        {
            RestoreFleetDesc(__state);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FleetComponent), "ActiveEnemyUnits_Ground")]
        private static void FleetComponentActiveEnemyUnitsGroundPrefix(FleetComponent __instance, PlanetFactory factory, PrefabDesc pdesc, ref FleetDescState __state)
        {
            __state = default(FleetDescState);
            if (factory?.craftPool == null || __instance.craftId <= 0 || __instance.craftId >= factory.craftPool.Length)
            {
                return;
            }

            ref CraftData craft = ref factory.craftPool[__instance.craftId];
            BoostFleetDescForMechaFleet(ref craft, pdesc, ref __state);
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(FleetComponent), "ActiveEnemyUnits_Ground")]
        private static void FleetComponentActiveEnemyUnitsGroundFinalizer(PrefabDesc pdesc, FleetDescState __state)
        {
            RestoreFleetDesc(__state);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FleetComponent), "ActiveEnemyUnits_Space")]
        private static void FleetComponentActiveEnemyUnitsSpacePrefix(FleetComponent __instance, SpaceSector sector, PrefabDesc pdesc, ref FleetDescState __state)
        {
            __state = default(FleetDescState);
            if (sector?.craftPool == null || __instance.craftId <= 0 || __instance.craftId >= sector.craftPool.Length)
            {
                return;
            }

            ref CraftData craft = ref sector.craftPool[__instance.craftId];
            BoostFleetDescForMechaFleet(ref craft, pdesc, ref __state);
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(FleetComponent), "ActiveEnemyUnits_Space")]
        private static void FleetComponentActiveEnemyUnitsSpaceFinalizer(PrefabDesc pdesc, FleetDescState __state)
        {
            RestoreFleetDesc(__state);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnitComponent), "PostGameTick_Ground")]
        private static void UnitComponentPostGameTickGroundPrefix(PlanetFactory factory, ref CraftData craft, ref FleetDescState __state)
        {
            BoostFighterFleetDesc(ref craft, factory?.craftPool, ref __state);
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(UnitComponent), "PostGameTick_Ground")]
        private static void UnitComponentPostGameTickGroundFinalizer(FleetDescState __state)
        {
            RestoreFleetDesc(__state);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnitComponent), "PostGameTick_Space")]
        private static void UnitComponentPostGameTickSpacePrefix(SpaceSector sector, ref CraftData craft, ref FleetDescState __state)
        {
            BoostFighterFleetDesc(ref craft, sector?.craftPool, ref __state);
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(UnitComponent), "PostGameTick_Space")]
        private static void UnitComponentPostGameTickSpaceFinalizer(FleetDescState __state)
        {
            RestoreFleetDesc(__state);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_AttackLaser_Ground")]
        private static void UnitComponentAttackLaserGroundPrefix(PlanetFactory factory, ref CraftData craft, ref CombatUpgradeData combatUpgradeData, ref FighterBehaviorState __state)
        {
            BoostFighterBehavior(ref craft, factory?.craftPool, ref combatUpgradeData, ref __state);
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_AttackLaser_Ground")]
        private static void UnitComponentAttackLaserGroundFinalizer(ref CombatUpgradeData combatUpgradeData, FighterBehaviorState __state)
        {
            RestoreFighterBehavior(ref combatUpgradeData, __state);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_AttackPlasma_Ground")]
        private static void UnitComponentAttackPlasmaGroundPrefix(PlanetFactory factory, ref CraftData craft, ref CombatUpgradeData combatUpgradeData, ref FighterBehaviorState __state)
        {
            BoostFighterBehavior(ref craft, factory?.craftPool, ref combatUpgradeData, ref __state);
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_AttackPlasma_Ground")]
        private static void UnitComponentAttackPlasmaGroundFinalizer(ref CombatUpgradeData combatUpgradeData, FighterBehaviorState __state)
        {
            RestoreFighterBehavior(ref combatUpgradeData, __state);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_DefenseShield_Ground")]
        private static void UnitComponentDefenseShieldGroundPrefix(PlanetFactory factory, ref CraftData craft, ref CombatUpgradeData combatUpgradeData, ref FighterBehaviorState __state)
        {
            BoostFighterBehavior(ref craft, factory?.craftPool, ref combatUpgradeData, ref __state);
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_DefenseShield_Ground")]
        private static void UnitComponentDefenseShieldGroundFinalizer(ref CombatUpgradeData combatUpgradeData, FighterBehaviorState __state)
        {
            RestoreFighterBehavior(ref combatUpgradeData, __state);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_SAttackLaser_Large")]
        private static void UnitComponentSAttackLaserLargePrefix(SpaceSector sector, ref CraftData craft, ref CombatUpgradeData combatUpgradeData, ref FighterBehaviorState __state)
        {
            BoostFighterBehavior(ref craft, sector?.craftPool, ref combatUpgradeData, ref __state);
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_SAttackLaser_Large")]
        private static void UnitComponentSAttackLaserLargeFinalizer(ref CombatUpgradeData combatUpgradeData, FighterBehaviorState __state)
        {
            RestoreFighterBehavior(ref combatUpgradeData, __state);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_SAttackPlasma_Small")]
        private static void UnitComponentSAttackPlasmaSmallPrefix(SpaceSector sector, ref CraftData craft, ref CombatUpgradeData combatUpgradeData, ref FighterBehaviorState __state)
        {
            BoostFighterBehavior(ref craft, sector?.craftPool, ref combatUpgradeData, ref __state);
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_SAttackPlasma_Small")]
        private static void UnitComponentSAttackPlasmaSmallFinalizer(ref CombatUpgradeData combatUpgradeData, FighterBehaviorState __state)
        {
            RestoreFighterBehavior(ref combatUpgradeData, __state);
        }
    }
}
