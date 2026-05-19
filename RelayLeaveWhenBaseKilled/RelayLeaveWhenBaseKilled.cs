using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace RelayLeaveWhenBaseKilled
{
    [BepInPlugin("me.liantian.plugin.RelayLeaveWhenBaseKilled", "RelayLeaveWhenBaseKilled", "0.0.1")]
    public class RelayLeaveWhenBaseKilled : BaseUnityPlugin
    {
        private const string PluginGuid = "me.liantian.plugin.RelayLeaveWhenBaseKilled";
        private const string PluginName = "RelayLeaveWhenBaseKilled";
        private const string PluginVersion = "0.0.1";

        internal static ManualLogSource Log;

        private Harmony harmony;

        public void Awake()
        {
            Log = Logger;
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(RelayLeaveWhenBaseKilled).Assembly);
            Log.LogInfo($"{PluginName} {PluginVersion} initialized");
        }

        public void OnDestroy()
        {
            harmony?.UnpatchSelf();
            harmony = null;
        }

        [HarmonyPatch(typeof(EnemyDFGroundSystem), "NotifyBaseKilled")]
        private static class EnemyDFGroundSystemNotifyBaseKilledPatch
        {
            private static void Postfix(EnemyDFGroundSystem __instance, int baseId)
            {
                if (__instance?.bases?.buffer == null || baseId <= 0 || baseId >= __instance.bases.cursor)
                {
                    return;
                }

                DFGBaseComponent baseComponent = __instance.bases.buffer[baseId];
                if (baseComponent == null || baseComponent.id != baseId)
                {
                    return;
                }

                DFRelayComponent relay = baseComponent.GetRelay();
                if (relay == null)
                {
                    return;
                }

                bool isRelayBoundToKilledBase = relay.baseId == baseId;
                bool isLandedRelay = relay.stage == 2;
                bool isMaintainingGroundBase = relay.baseState == 2;
                if (!isRelayBoundToKilledBase || !isLandedRelay || !isMaintainingGroundBase)
                {
                    return;
                }

                relay.LeaveBase();
                if (relay.hive != null)
                {
                    relay.hive.relayNeutralizedCounter++;
                }

                TryBuildGeothermalOnEmptyBase(__instance, baseComponent, baseId);
            }

            private static void TryBuildGeothermalOnEmptyBase(EnemyDFGroundSystem groundSystem, DFGBaseComponent baseComponent, int baseId)
            {
                if (groundSystem?.factory?.powerSystem == null || baseComponent == null || baseComponent.id != baseId || baseComponent.ruinId <= 0)
                {
                    return;
                }

                PlanetFactory factory = groundSystem.factory;
                if (factory.planet?.aux == null)
                {
                    Log?.LogWarning($"Skip geothermal build for base {baseId}: planet auxiliary data is unavailable.");
                    return;
                }

                if (groundSystem.CheckBaseCanRemoved(baseId) != 0)
                {
                    return;
                }

                int baseRuinId = baseComponent.ruinId;
                if (HasGeothermalOnBaseRuin(factory, baseRuinId) || factory.ruinPool == null || baseRuinId >= factory.ruinPool.Length)
                {
                    return;
                }

                RuinData ruin = factory.ruinPool[baseRuinId];
                if (ruin.id != baseRuinId)
                {
                    return;
                }

                ItemProto geothermalItem = FindGeothermalPowerItem();
                if (geothermalItem == null || geothermalItem.prefabDesc == null || !geothermalItem.prefabDesc.geothermal)
                {
                    return;
                }

                int entityId = BuildGeothermalEntity(factory, geothermalItem, baseRuinId, ruin.pos);
                if (entityId <= 0)
                {
                    return;
                }

                TrySetPowerGeneratorInvincible(factory, entityId);
                groundSystem.RemoveBase(baseId);
                GameMain.gameScenario?.NotifyOnRemovePit();
            }

            private static int BuildGeothermalEntity(PlanetFactory factory, ItemProto geothermalItem, int baseRuinId, Vector3 ruinPos)
            {
                if (factory?.planet?.aux == null || geothermalItem == null)
                {
                    return 0;
                }

                Vector3 buildPos = factory.planet.aux.Snap(ruinPos, onTerrain: true);
                Quaternion buildRot = Maths.SphericalRotation(buildPos, 0f);

                PrebuildData prebuild = default(PrebuildData);
                prebuild.isDestroyed = false;
                prebuild.protoId = (short)geothermalItem.ID;
                prebuild.modelIndex = (short)geothermalItem.ModelIndex;
                prebuild.pos = buildPos;
                prebuild.pos2 = buildPos;
                prebuild.rot = buildRot;
                prebuild.rot2 = buildRot;
                prebuild.InitParametersArray(1);
                prebuild.parameters[0] = baseRuinId;

                int prebuildId = factory.AddPrebuildDataWithComponents(prebuild);
                if (prebuildId <= 0)
                {
                    return 0;
                }

                EntityData entity = default(EntityData);
                entity.protoId = prebuild.protoId;
                entity.modelIndex = prebuild.modelIndex;
                entity.pos = prebuild.pos;
                entity.rot = prebuild.rot;
                entity.alt = entity.pos.magnitude;
                entity.tilt = prebuild.tilt;
                entity.localized = factory.planet == GameMain.localPlanet && factory.planet.factoryLoaded;

                int entityId = factory.AddEntityDataWithComponents(entity, prebuildId);
                if (entityId <= 0)
                {
                    factory.RemovePrebuildWithComponents(prebuildId);
                    return 0;
                }

                GameMain.mainPlayer?.controller?.actionBuild?.NotifyBuilt(-prebuildId, entityId);
                factory.RemovePrebuildWithComponents(prebuildId);
                GameMain.history?.MarkItemBuilt(prebuild.protoId);
                if (factory.entityPool[entityId].beltId > 0)
                {
                    factory.OnBeltBuilt(entityId);
                }
                if (factory.entityPool[entityId].inserterId > 0)
                {
                    factory.OnInserterBuilt(entityId);
                }
                if (geothermalItem.prefabDesc.addonType != EAddonType.None)
                {
                    factory.OnAddonBuilt(entityId);
                }
                factory.OnBuildEntity(entityId, prebuildId);
                if (!PlanetFactory.batchBuild)
                {
                    factory.OnSinglyBuildEntity(entityId, prebuildId);
                }
                GameMain.gameScenario?.NotifyOnBuild(factory.planet.id, factory.entityPool[entityId].protoId, entityId);
                return entityId;
            }

            private static bool HasGeothermalOnBaseRuin(PlanetFactory factory, int baseRuinId)
            {
                if (factory == null || factory.powerSystem == null || baseRuinId <= 0)
                {
                    return false;
                }

                PowerGeneratorComponent[] genPool = factory.powerSystem.genPool;
                for (int i = 1; i < factory.powerSystem.genCursor; i++)
                {
                    ref PowerGeneratorComponent gen = ref genPool[i];
                    if (gen.id == i && gen.geothermal && gen.baseRuinId == baseRuinId)
                    {
                        return true;
                    }
                }

                PrebuildData[] prebuildPool = factory.prebuildPool;
                for (int i = 1; i < factory.prebuildCursor; i++)
                {
                    ref PrebuildData prebuild = ref prebuildPool[i];
                    if (prebuild.id == i && prebuild.paramCount > 0 && prebuild.parameters != null && prebuild.parameters[0] == baseRuinId)
                    {
                        ItemProto itemProto = LDB.items.Select(prebuild.protoId);
                        if (itemProto?.prefabDesc != null && itemProto.prefabDesc.geothermal)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            private static ItemProto FindGeothermalPowerItem()
            {
                if (LDB.items?.dataArray == null)
                {
                    return null;
                }

                ItemProto[] dataArray = LDB.items.dataArray;
                for (int i = 0; i < dataArray.Length; i++)
                {
                    ItemProto item = dataArray[i];
                    if (item?.prefabDesc == null || !item.CanBuild || !item.IsEntity)
                    {
                        continue;
                    }

                    if (item.prefabDesc.isPowerGen && item.prefabDesc.geothermal)
                    {
                        return item;
                    }
                }

                return null;
            }

            private static void TrySetPowerGeneratorInvincible(PlanetFactory factory, int entityId)
            {
                int powerGenId = factory.entityPool[entityId].powerGenId;
                if (powerGenId <= 0 || powerGenId >= factory.powerSystem.genCursor)
                {
                    return;
                }

                ref PowerGeneratorComponent gen = ref factory.powerSystem.genPool[powerGenId];
                if (gen.id != powerGenId)
                {
                    return;
                }

                object boxed = gen;
                FieldInfo isInvincibleField = typeof(PowerGeneratorComponent).GetField("isInvincible");
                if (isInvincibleField == null || isInvincibleField.FieldType != typeof(bool))
                {
                    return;
                }

                isInvincibleField.SetValue(boxed, true);
                gen = (PowerGeneratorComponent)boxed;
            }
        }
    }
}
