using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;

namespace FasterResearch
{
    [BepInPlugin("me.liantian.plugin.FasterResearch", "FasterResearch", "0.0.1")]
    public class FasterResearch : BaseUnityPlugin
    {
        private const string PluginGuid = "me.liantian.plugin.FasterResearch";
        private const string PluginName = "FasterResearch";
        private const string PluginVersion = "0.0.1";

        private const int Multiplier = 24;

        internal static ManualLogSource Log;

        private Harmony harmony;

        public void Awake()
        {
            Log = Logger;
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(FasterResearch).Assembly);
            Log.LogInfo($"FasterResearch {PluginVersion} initialized, multiplier = {Multiplier}");
        }

        public void OnDestroy()
        {
            harmony?.UnpatchSelf();
            harmony = null;
        }

        [HarmonyPatch(typeof(TechProto), "GetHashNeeded")]
        private static class TechProtoGetHashNeededPatch
        {
            private static void Postfix(ref long __result)
            {
                __result = DivideRoundUp(__result, Multiplier);
            }
        }

        [HarmonyPatch(typeof(GameHistoryData), "SetForNewGame")]
        private static class GameHistoryDataSetForNewGamePatch
        {
            private static void Postfix(GameHistoryData __instance)
            {
                SyncCurrentTechHashNeeded(__instance);
            }
        }

        [HarmonyPatch(typeof(GameHistoryData), "Import")]
        private static class GameHistoryDataImportPatch
        {
            private static void Postfix(GameHistoryData __instance, bool isPreview)
            {
                if (!isPreview)
                {
                    SyncCurrentTechHashNeeded(__instance);
                }
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

            foreach (KeyValuePair<int, TechState> entry in history.techStates)
            {
                TechState state = entry.Value;
                if (state.unlocked)
                {
                    continue;
                }

                TechProto techProto = LDB.techs.Select(entry.Key);
                if (techProto == null)
                {
                    continue;
                }

                state.hashNeeded = techProto.GetHashNeeded(state.curLevel);
                history.techStates[entry.Key] = state;
            }
        }
    }
}
