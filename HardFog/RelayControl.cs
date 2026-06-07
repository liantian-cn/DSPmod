using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HardFog
{
    [HarmonyPatch]
    internal static class RelayControl
    {
        private const string PatchGuid = "me.liantian.plugin.HardFog.RelayControl";
        private const int VanillaRelayDemandInterval = 600;
        private const int FasterRelayDemandInterval = 120;

        internal static ConfigEntry<bool> FasterRelayLaunchEnabledConfig { get; private set; }
        internal static ConfigEntry<bool> SmartRelayDispatchEnabledConfig { get; private set; }

        private static ManualLogSource Log;
        private static Harmony harmony;
        private static EventHandler fasterRelayLaunchChangedHandler;

        internal static void Init(
            ConfigEntry<bool> fasterRelayLaunchEnabledConfig,
            ConfigEntry<bool> smartRelayDispatchEnabledConfig,
            ManualLogSource log)
        {
            if (FasterRelayLaunchEnabledConfig != null && fasterRelayLaunchChangedHandler != null)
            {
                FasterRelayLaunchEnabledConfig.SettingChanged -= fasterRelayLaunchChangedHandler;
            }

            Log = log;
            FasterRelayLaunchEnabledConfig = fasterRelayLaunchEnabledConfig;
            SmartRelayDispatchEnabledConfig = smartRelayDispatchEnabledConfig;
            fasterRelayLaunchChangedHandler = OnFasterRelayLaunchChanged;
            FasterRelayLaunchEnabledConfig.SettingChanged += fasterRelayLaunchChangedHandler;
            SetActive(FasterRelayLaunchEnabledConfig.Value);
        }

        internal static void Uninit()
        {
            if (FasterRelayLaunchEnabledConfig != null && fasterRelayLaunchChangedHandler != null)
            {
                FasterRelayLaunchEnabledConfig.SettingChanged -= fasterRelayLaunchChangedHandler;
            }

            SetActive(false);
            fasterRelayLaunchChangedHandler = null;
            FasterRelayLaunchEnabledConfig = null;
            SmartRelayDispatchEnabledConfig = null;
            Log = null;
        }

        private static void OnFasterRelayLaunchChanged(object sender, EventArgs args)
        {
            SetActive(FasterRelayLaunchEnabledConfig != null && FasterRelayLaunchEnabledConfig.Value);
        }

        private static void SetActive(bool active)
        {
            if (active)
            {
                if (harmony != null)
                {
                    return;
                }

                harmony = Harmony.CreateAndPatchAll(typeof(RelayControl), PatchGuid);
                Log?.LogInfo("RelayControl enabled");
                return;
            }

            if (harmony == null)
            {
                return;
            }

            harmony.UnpatchSelf();
            harmony = null;
            Log?.LogInfo("RelayControl disabled");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EnemyDFHiveSystem), "DetermineRelayDemand")]
        private static bool EnemyDFHiveSystemDetermineRelayDemandPrefix(EnemyDFHiveSystem __instance)
        {
            DispatchOneIdleRelayIfAllowed(__instance);
            return false;
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(EnemyDFHiveSystem), "UpdateMatterStatisticsVirtual")]
        private static IEnumerable<CodeInstruction> EnemyDFHiveSystemUpdateMatterStatisticsVirtualTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceRelayDemandInterval(instructions);
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(EnemyDFHiveSystem), "UpdateMatterStatisticsRealized")]
        private static IEnumerable<CodeInstruction> EnemyDFHiveSystemUpdateMatterStatisticsRealizedTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceRelayDemandInterval(instructions);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DFRelayComponent), "SearchTargetPlaceProcess")]
        private static bool DFRelayComponentSearchTargetPlaceProcessPrefix(DFRelayComponent __instance, ref bool __result)
        {
            if (__instance == null)
            {
                return true;
            }

            if (IsMarkerOnlyEnabled())
            {
                if (IsRelayMarkerStillValid(__instance))
                {
                    return true;
                }

                CancelDockDispatch(__instance);
                __result = false;
                return false;
            }

            if (__instance.dstMarkerAstroId > 0 ||
                __instance.dstMarkerId > 0 ||
                __instance.searchAstroId != 0)
            {
                return true;
            }

            EnemyDFHiveSystem hive = __instance.hive;
            StarData starData = hive?.starData;
            if (starData == null)
            {
                return true;
            }

            int nonGasPlanetCount = CountNonGasPlanets(starData);
            if (nonGasPlanetCount <= 0)
            {
                return true;
            }

            int pick = RandomTable.Integer(ref hive.rtseed, nonGasPlanetCount);
            PlanetData planet = PickNonGasPlanet(starData, pick);
            if (planet == null)
            {
                return true;
            }

            __instance.searchAstroId = planet.astroId;
            __instance.searchBaseId = 0;
            __instance.searchChance = 5;
            __instance.searchLPos = Vector3.zero;
            __instance.searchEntityCursor = 0;
            __result = false;
            return false;
        }

        private static IEnumerable<CodeInstruction> ReplaceRelayDemandInterval(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            int replacements = 0;

            for (int i = 0; i < codes.Count; i++)
            {
                if (!LoadsInt(codes[i], VanillaRelayDemandInterval))
                {
                    continue;
                }

                codes[i].opcode = OpCodes.Ldc_I4;
                codes[i].operand = FasterRelayDemandInterval;
                replacements++;
            }

            if (replacements == 0)
            {
                Log?.LogWarning("Failed to patch relay demand interval.");
            }

            return codes;
        }

        private static bool LoadsInt(CodeInstruction code, int value)
        {
            if (code.opcode == OpCodes.Ldc_I4 && code.operand is int intValue)
            {
                return intValue == value;
            }

            if (code.opcode == OpCodes.Ldc_I4_S && code.operand is sbyte sbyteValue)
            {
                return sbyteValue == value;
            }

            if (code.opcode == OpCodes.Ldc_I4_0)
            {
                return value == 0;
            }

            if (code.opcode == OpCodes.Ldc_I4_1)
            {
                return value == 1;
            }

            if (code.opcode == OpCodes.Ldc_I4_2)
            {
                return value == 2;
            }

            if (code.opcode == OpCodes.Ldc_I4_3)
            {
                return value == 3;
            }

            if (code.opcode == OpCodes.Ldc_I4_4)
            {
                return value == 4;
            }

            if (code.opcode == OpCodes.Ldc_I4_5)
            {
                return value == 5;
            }

            if (code.opcode == OpCodes.Ldc_I4_6)
            {
                return value == 6;
            }

            if (code.opcode == OpCodes.Ldc_I4_7)
            {
                return value == 7;
            }

            if (code.opcode == OpCodes.Ldc_I4_8)
            {
                return value == 8;
            }

            return false;
        }

        private static void DispatchOneIdleRelayIfAllowed(EnemyDFHiveSystem hive)
        {
            if (hive == null || hive.relays == null || hive.idleRelayCount <= 0)
            {
                return;
            }

            if (HasOutgoingRelay(hive))
            {
                return;
            }

            if (IsMarkerOnlyEnabled())
            {
                DispatchOneIdleRelayToMarkerIfAllowed(hive);
                return;
            }

            for (int i = 0; i < hive.idleRelayCount; i++)
            {
                int relayId = hive.idleRelayIds[i];
                if (relayId <= 0)
                {
                    continue;
                }

                DFRelayComponent relay = hive.relays.buffer[relayId];
                if (relay == null || relay.id != relayId)
                {
                    continue;
                }

                if (relay.TryDispatchFromHive())
                {
                    return;
                }
            }
        }

        private static void DispatchOneIdleRelayToMarkerIfAllowed(EnemyDFHiveSystem hive)
        {
            PlanetFactory markerFactory;
            int markerId;
            if (!TryPickAvailableRelayMarker(hive, out markerFactory, out markerId))
            {
                return;
            }

            for (int i = 0; i < hive.idleRelayCount; i++)
            {
                int relayId = hive.idleRelayIds[i];
                if (relayId <= 0)
                {
                    continue;
                }

                DFRelayComponent relay = hive.relays.buffer[relayId];
                if (relay == null || relay.id != relayId)
                {
                    continue;
                }

                if (!relay.TryDispatchFromHive())
                {
                    continue;
                }

                relay.SetTargetedMarker(markerFactory, markerId);
                if (!IsRelayMarkerStillValid(relay))
                {
                    CancelDockDispatch(relay);
                }

                return;
            }
        }

        private static bool TryPickAvailableRelayMarker(EnemyDFHiveSystem hive, out PlanetFactory factory, out int markerId)
        {
            factory = null;
            markerId = 0;

            int candidateCount = CountAvailableRelayMarkers(hive);
            if (candidateCount <= 0)
            {
                return false;
            }

            int remaining = candidateCount;
            StarData starData = hive.starData;
            for (int i = 0; i < starData.planetCount; i++)
            {
                PlanetData planet = starData.planets[i];
                PlanetFactory planetFactory = GetMarkerPlanetFactory(hive, planet);
                if (planetFactory == null)
                {
                    continue;
                }

                ObjectPool<MarkerComponent> markers = planetFactory.digitalSystem.markers;
                for (int j = 1; j < markers.cursor; j++)
                {
                    MarkerComponent marker = markers[j];
                    if (!IsAvailableRelayMarker(hive, planetFactory, marker, j))
                    {
                        continue;
                    }

                    float chance = marker.power / (float)remaining;
                    if (UnityEngine.Random.value < chance)
                    {
                        factory = planetFactory;
                        markerId = j;
                        return true;
                    }

                    remaining--;
                }
            }

            return false;
        }

        private static int CountAvailableRelayMarkers(EnemyDFHiveSystem hive)
        {
            int count = 0;
            StarData starData = hive.starData;
            for (int i = 0; i < starData.planetCount; i++)
            {
                PlanetData planet = starData.planets[i];
                PlanetFactory planetFactory = GetMarkerPlanetFactory(hive, planet);
                if (planetFactory == null)
                {
                    continue;
                }

                ObjectPool<MarkerComponent> markers = planetFactory.digitalSystem.markers;
                for (int j = 1; j < markers.cursor; j++)
                {
                    if (IsAvailableRelayMarker(hive, planetFactory, markers[j], j))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static PlanetFactory GetMarkerPlanetFactory(EnemyDFHiveSystem hive, PlanetData planet)
        {
            if (hive == null ||
                planet == null ||
                planet.type == EPlanetType.Gas ||
                planet.star != hive.starData ||
                planet.factory == null ||
                planet.factory.entityCount == 0 ||
                planet.factory.digitalSystem == null)
            {
                return null;
            }

            return planet.factory;
        }

        private static bool IsAvailableRelayMarker(
            EnemyDFHiveSystem hive,
            PlanetFactory factory,
            MarkerComponent marker,
            int markerId)
        {
            return marker != null &&
                marker.id == markerId &&
                marker.attractsDFRelay &&
                !IsMarkerAlreadyTargeted(hive, factory.planet.astroId, markerId);
        }

        private static bool IsMarkerAlreadyTargeted(EnemyDFHiveSystem hive, int astroId, int markerId)
        {
            for (EnemyDFHiveSystem sibling = hive.firstSibling; sibling != null; sibling = sibling.nextSibling)
            {
                DFRelayComponent[] buffer = sibling.relays.buffer;
                int cursor = sibling.relays.cursor;
                for (int i = 1; i < cursor; i++)
                {
                    DFRelayComponent relay = buffer[i];
                    if (relay != null &&
                        relay.id == i &&
                        relay.direction > 0 &&
                        relay.dstMarkerAstroId == astroId &&
                        relay.dstMarkerId == markerId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasOutgoingRelay(EnemyDFHiveSystem hive)
        {
            DFRelayComponent[] buffer = hive.relays.buffer;
            int cursor = hive.relays.cursor;
            for (int i = 1; i < cursor; i++)
            {
                DFRelayComponent relay = buffer[i];
                if (relay != null && relay.id == i && relay.direction > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRelayMarkerStillValid(DFRelayComponent relay)
        {
            if (relay.dstMarkerAstroId <= 0 || relay.dstMarkerId <= 0 || relay.hive == null)
            {
                return false;
            }

            PlanetFactory factory = relay.hive.sector.galaxy.astrosFactory[relay.dstMarkerAstroId];
            if (factory == null ||
                factory.planet.type == EPlanetType.Gas ||
                factory.planet.star != relay.hive.starData ||
                factory.digitalSystem == null ||
                relay.dstMarkerId >= factory.digitalSystem.markers.cursor)
            {
                return false;
            }

            MarkerComponent marker = factory.digitalSystem.markers[relay.dstMarkerId];
            if (marker == null || marker.id != relay.dstMarkerId || !marker.attractsDFRelay)
            {
                return false;
            }

            if (relay.dstMarkerLPos.sqrMagnitude < (factory.planet.realRadius - 2f) * (factory.planet.realRadius - 2f))
            {
                return false;
            }

            if (marker.entityId <= 0 || marker.entityId >= factory.entityPool.Length)
            {
                return false;
            }

            return (factory.entityPool[marker.entityId].pos - relay.dstMarkerLPos).sqrMagnitude <= 0.25f;
        }

        private static void CancelDockDispatch(DFRelayComponent relay)
        {
            relay.targetAstroId = 0;
            relay.targetLPos = Vector3.zero;
            relay.targetYaw = 0f;
            relay.baseState = 0;
            relay.baseId = 0;
            relay.baseTicks = 0;
            relay.baseEvolve = default(EvolveData);
            relay.baseRespawnCD = 0;
            relay.direction = 0;
            relay.param0 = 0f;
            relay.uSpeed = 0f;
            relay.ResetSearchStates();
            relay.ResetTargetedMarker();
        }

        private static bool IsMarkerOnlyEnabled()
        {
            return FasterRelayLaunchEnabledConfig != null &&
                FasterRelayLaunchEnabledConfig.Value &&
                SmartRelayDispatchEnabledConfig != null &&
                SmartRelayDispatchEnabledConfig.Value;
        }

        private static int CountNonGasPlanets(StarData starData)
        {
            int count = 0;
            for (int i = 0; i < starData.planetCount; i++)
            {
                PlanetData planet = starData.planets[i];
                if (planet != null && planet.type != EPlanetType.Gas)
                {
                    count++;
                }
            }

            return count;
        }

        private static PlanetData PickNonGasPlanet(StarData starData, int pick)
        {
            for (int i = 0; i < starData.planetCount; i++)
            {
                PlanetData planet = starData.planets[i];
                if (planet == null || planet.type == EPlanetType.Gas)
                {
                    continue;
                }

                if (pick == 0)
                {
                    return planet;
                }

                pick--;
            }

            return null;
        }
    }
}
