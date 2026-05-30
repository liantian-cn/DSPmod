using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace HardFog
{
    [HarmonyPatch]
    internal static class FasterResearchControl
    {
        private const string PatchGuid = "me.liantian.plugin.HardFog.FasterResearch";
        private const int Multiplier = 48;

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
                if (harmony == null)
                {
                    harmony = Harmony.CreateAndPatchAll(typeof(FasterResearchControl), PatchGuid);
                    Log?.LogInfo("FasterResearch enabled, multiplier = " + Multiplier);
                }

                SyncCurrentTechHashNeeded(GameMain.history);
                return;
            }

            if (harmony == null)
            {
                return;
            }

            harmony.UnpatchSelf();
            harmony = null;
            Log?.LogInfo("FasterResearch disabled");
            SyncCurrentTechHashNeeded(GameMain.history);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TechProto), "GetHashNeeded")]
        private static void TechProtoGetHashNeededPostfix(ref long __result)
        {
            __result = DivideRoundUp(__result, Multiplier);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameHistoryData), "SetForNewGame")]
        private static void GameHistoryDataSetForNewGamePostfix(GameHistoryData __instance)
        {
            SyncCurrentTechHashNeeded(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameHistoryData), "Import")]
        private static void GameHistoryDataImportPostfix(GameHistoryData __instance, bool isPreview)
        {
            if (!isPreview)
            {
                SyncCurrentTechHashNeeded(__instance);
            }
        }

        private static long DivideRoundUp(long value, int divisor)
        {
            if (value <= 0)
            {
                return value;
            }

            return (value + divisor - 1L) / divisor;
        }

        private static void SyncCurrentTechHashNeeded(GameHistoryData history)
        {
            if (history?.techStates == null)
            {
                return;
            }

            foreach (int techId in new List<int>(history.techStates.Keys))
            {
                TechState state = history.techStates[techId];
                if (state.unlocked)
                {
                    continue;
                }

                TechProto techProto = LDB.techs.Select(techId);
                if (techProto == null)
                {
                    continue;
                }

                state.hashNeeded = techProto.GetHashNeeded(state.curLevel);
                if (state.hashUploaded >= state.hashNeeded)
                {
                    state.hashUploaded = state.hashNeeded - 1L;
                }
                history.techStates[techId] = state;
            }
        }
    }
}
