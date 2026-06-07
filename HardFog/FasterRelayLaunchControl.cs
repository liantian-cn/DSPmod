using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HardFog
{
    // Ghost file: kept for reference only. RelayControl.cs replaces this class and
    // HardFog.csproj no longer compiles this file.
    [HarmonyPatch]
    internal static class FasterRelayLaunchControl
    {
        private const string PatchGuid = "me.liantian.plugin.HardFog.FasterRelayLaunch";
        private const int VanillaRelayDemandInterval = 600;
        private const int FasterRelayDemandInterval = 120;

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

                harmony = Harmony.CreateAndPatchAll(typeof(FasterRelayLaunchControl), PatchGuid);
                Log?.LogInfo("FasterRelayLaunch enabled");
                return;
            }

            if (harmony == null)
            {
                return;
            }

            harmony.UnpatchSelf();
            harmony = null;
            Log?.LogInfo("FasterRelayLaunch disabled");
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
            if (__instance == null ||
                __instance.dstMarkerAstroId > 0 ||
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
