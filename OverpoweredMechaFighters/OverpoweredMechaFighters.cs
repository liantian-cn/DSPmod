using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace OverpoweredMechaFighters
{
    [BepInPlugin("me.liantian.plugin.OverpoweredMechaFighters", "OverpoweredMechaFighters", "0.0.1")]
    public class OverpoweredMechaFighters : BaseUnityPlugin
    {
        private const string PluginGuid = "me.liantian.plugin.OverpoweredMechaFighters";
        private const string PluginName = "OverpoweredMechaFighters";
        private const string PluginVersion = "0.0.1";

        private const float RangeMultiplier = 10f;
        private const float DamageMultiplier = 10f;

        internal static ManualLogSource Log;

        private Harmony harmony;

        public void Awake()
        {
            Log = Logger;
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(OverpoweredMechaFighters).Assembly);
            Log.LogInfo($"{PluginName} {PluginVersion} initialized");
        }

        public void OnDestroy()
        {
            harmony?.UnpatchSelf();
            harmony = null;
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
            if (!IsMechaFighter(ref craft, craftPool))
            {
                return;
            }

            craft.isInvincible = true;
            if (skillSystem == null || craft.combatStatId <= 0)
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

        private static void RefreshGroundMechaFighters(CombatGroundSystem combatSystem)
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
                MakeInvincible(ref craft, craftPool, combatSystem.factory.skillSystem);
            }
        }

        private static void RefreshSpaceMechaFighters(CombatSpaceSystem combatSystem)
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
                MakeInvincible(ref craft, craftPool, combatSystem.spaceSector.skillSystem);
            }
        }

        [HarmonyPatch(typeof(CombatGroundSystem), "NewUnitComponent")]
        private static class CombatGroundSystemNewUnitComponentPatch
        {
            private static void Postfix(CombatGroundSystem __instance, int craftId)
            {
                if (__instance?.factory?.craftPool == null || craftId <= 0 || craftId >= __instance.factory.craftPool.Length)
                {
                    return;
                }

                ref CraftData craft = ref __instance.factory.craftPool[craftId];
                MakeInvincible(ref craft, __instance.factory.craftPool, __instance.factory.skillSystem);
            }
        }

        [HarmonyPatch(typeof(CombatSpaceSystem), "NewUnitComponent")]
        private static class CombatSpaceSystemNewUnitComponentPatch
        {
            private static void Postfix(CombatSpaceSystem __instance, int craftId)
            {
                if (__instance?.spaceSector?.craftPool == null || craftId <= 0 || craftId >= __instance.spaceSector.craftPool.Length)
                {
                    return;
                }

                ref CraftData craft = ref __instance.spaceSector.craftPool[craftId];
                MakeInvincible(ref craft, __instance.spaceSector.craftPool, __instance.spaceSector.skillSystem);
            }
        }

        [HarmonyPatch(typeof(CombatGroundSystem), "GameTick")]
        private static class CombatGroundSystemGameTickPatch
        {
            private static void Postfix(CombatGroundSystem __instance, long tick)
            {
                if (tick % 60 == 0)
                {
                    RefreshGroundMechaFighters(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(CombatSpaceSystem), "GameTick")]
        private static class CombatSpaceSystemGameTickPatch
        {
            private static void Postfix(CombatSpaceSystem __instance, long tick)
            {
                if (tick % 60 == 0)
                {
                    RefreshSpaceMechaFighters(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(CombatModuleComponent), "DiscoverLocalEnemy")]
        private static class CombatModuleComponentDiscoverLocalEnemyPatch
        {
            private static void Prefix(CombatModuleComponent __instance, ref SensorRangeState __state)
            {
                BoostSensorRange(__instance, ref __state);
            }

            private static void Postfix(CombatModuleComponent __instance, SensorRangeState __state)
            {
                RestoreSensorRange(__instance, __state);
            }
        }

        [HarmonyPatch(typeof(CombatModuleComponent), "DiscoverSpaceEnemy")]
        private static class CombatModuleComponentDiscoverSpaceEnemyPatch
        {
            private static void Prefix(CombatModuleComponent __instance, ref SensorRangeState __state)
            {
                BoostSensorRange(__instance, ref __state);
            }

            private static void Postfix(CombatModuleComponent __instance, SensorRangeState __state)
            {
                RestoreSensorRange(__instance, __state);
            }
        }

        [HarmonyPatch(typeof(FleetComponent), "SensorLogic_Ground")]
        private static class FleetComponentSensorLogicGroundPatch
        {
            private static void Prefix(ref CraftData craft, PrefabDesc pdesc, ref FleetDescState __state)
            {
                BoostFleetDescForMechaFleet(ref craft, pdesc, ref __state);
            }

            private static void Finalizer(PrefabDesc pdesc, FleetDescState __state)
            {
                RestoreFleetDesc(__state);
            }
        }

        [HarmonyPatch(typeof(FleetComponent), "SensorLogic_Space")]
        private static class FleetComponentSensorLogicSpacePatch
        {
            private static void Prefix(ref CraftData craft, PrefabDesc pdesc, ref FleetDescState __state)
            {
                BoostFleetDescForMechaFleet(ref craft, pdesc, ref __state);
            }

            private static void Finalizer(PrefabDesc pdesc, FleetDescState __state)
            {
                RestoreFleetDesc(__state);
            }
        }

        [HarmonyPatch(typeof(FleetComponent), "ActiveEnemyUnits_Ground")]
        private static class FleetComponentActiveEnemyUnitsGroundPatch
        {
            private static void Prefix(FleetComponent __instance, PlanetFactory factory, PrefabDesc pdesc, ref FleetDescState __state)
            {
                __state = default(FleetDescState);
                if (factory?.craftPool == null || __instance.craftId <= 0 || __instance.craftId >= factory.craftPool.Length)
                {
                    return;
                }

                ref CraftData craft = ref factory.craftPool[__instance.craftId];
                BoostFleetDescForMechaFleet(ref craft, pdesc, ref __state);
            }

            private static void Finalizer(PrefabDesc pdesc, FleetDescState __state)
            {
                RestoreFleetDesc(__state);
            }
        }

        [HarmonyPatch(typeof(FleetComponent), "ActiveEnemyUnits_Space")]
        private static class FleetComponentActiveEnemyUnitsSpacePatch
        {
            private static void Prefix(FleetComponent __instance, SpaceSector sector, PrefabDesc pdesc, ref FleetDescState __state)
            {
                __state = default(FleetDescState);
                if (sector?.craftPool == null || __instance.craftId <= 0 || __instance.craftId >= sector.craftPool.Length)
                {
                    return;
                }

                ref CraftData craft = ref sector.craftPool[__instance.craftId];
                BoostFleetDescForMechaFleet(ref craft, pdesc, ref __state);
            }

            private static void Finalizer(PrefabDesc pdesc, FleetDescState __state)
            {
                RestoreFleetDesc(__state);
            }
        }

        [HarmonyPatch(typeof(UnitComponent), "PostGameTick_Ground")]
        private static class UnitComponentPostGameTickGroundPatch
        {
            private static void Prefix(PlanetFactory factory, ref CraftData craft, ref FleetDescState __state)
            {
                BoostFighterFleetDesc(ref craft, factory?.craftPool, ref __state);
            }

            private static void Finalizer(FleetDescState __state)
            {
                RestoreFleetDesc(__state);
            }
        }

        [HarmonyPatch(typeof(UnitComponent), "PostGameTick_Space")]
        private static class UnitComponentPostGameTickSpacePatch
        {
            private static void Prefix(SpaceSector sector, ref CraftData craft, ref FleetDescState __state)
            {
                BoostFighterFleetDesc(ref craft, sector?.craftPool, ref __state);
            }

            private static void Finalizer(FleetDescState __state)
            {
                RestoreFleetDesc(__state);
            }
        }

        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_AttackLaser_Ground")]
        private static class UnitComponentAttackLaserGroundPatch
        {
            private static void Prefix(PlanetFactory factory, ref CraftData craft, ref CombatUpgradeData combatUpgradeData, ref FighterBehaviorState __state)
            {
                BoostFighterBehavior(ref craft, factory?.craftPool, ref combatUpgradeData, ref __state);
            }

            private static void Finalizer(ref CombatUpgradeData combatUpgradeData, FighterBehaviorState __state)
            {
                RestoreFighterBehavior(ref combatUpgradeData, __state);
            }

        }

        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_AttackPlasma_Ground")]
        private static class UnitComponentAttackPlasmaGroundPatch
        {
            private static void Prefix(PlanetFactory factory, ref CraftData craft, ref CombatUpgradeData combatUpgradeData, ref FighterBehaviorState __state)
            {
                BoostFighterBehavior(ref craft, factory?.craftPool, ref combatUpgradeData, ref __state);
            }

            private static void Finalizer(ref CombatUpgradeData combatUpgradeData, FighterBehaviorState __state)
            {
                RestoreFighterBehavior(ref combatUpgradeData, __state);
            }

        }

        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_DefenseShield_Ground")]
        private static class UnitComponentDefenseShieldGroundPatch
        {
            private static void Prefix(PlanetFactory factory, ref CraftData craft, ref CombatUpgradeData combatUpgradeData, ref FighterBehaviorState __state)
            {
                BoostFighterBehavior(ref craft, factory?.craftPool, ref combatUpgradeData, ref __state);
            }

            private static void Finalizer(ref CombatUpgradeData combatUpgradeData, FighterBehaviorState __state)
            {
                RestoreFighterBehavior(ref combatUpgradeData, __state);
            }

        }

        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_SAttackLaser_Large")]
        private static class UnitComponentSAttackLaserLargePatch
        {
            private static void Prefix(SpaceSector sector, ref CraftData craft, ref CombatUpgradeData combatUpgradeData, ref FighterBehaviorState __state)
            {
                BoostFighterBehavior(ref craft, sector?.craftPool, ref combatUpgradeData, ref __state);
            }

            private static void Finalizer(ref CombatUpgradeData combatUpgradeData, FighterBehaviorState __state)
            {
                RestoreFighterBehavior(ref combatUpgradeData, __state);
            }

        }

        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_SAttackPlasma_Small")]
        private static class UnitComponentSAttackPlasmaSmallPatch
        {
            private static void Prefix(SpaceSector sector, ref CraftData craft, ref CombatUpgradeData combatUpgradeData, ref FighterBehaviorState __state)
            {
                BoostFighterBehavior(ref craft, sector?.craftPool, ref combatUpgradeData, ref __state);
            }

            private static void Finalizer(ref CombatUpgradeData combatUpgradeData, FighterBehaviorState __state)
            {
                RestoreFighterBehavior(ref combatUpgradeData, __state);
            }

        }
    }
}
