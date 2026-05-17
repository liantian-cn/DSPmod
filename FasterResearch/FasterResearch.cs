using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace FasterResearch
{
    [BepInPlugin("me.liantian.plugin.FasterResearch", "FasterResearch", "0.0.1")]
    public class FasterResearch : BaseUnityPlugin
    {
        private const string PluginGuid = "me.liantian.plugin.FasterResearch";
        private const string PluginName = "FasterResearch";
        private const string PluginVersion = "0.0.1";

        private const int Multiplier = 24;

        private static int lastDividedTechId = -1;

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

        [HarmonyPatch(typeof(FactorySystem), "GameTickLabResearchMode")]
        private static class FactorySystemGameTickLabResearchModePatch
        {
            private static void Postfix(FactorySystem __instance)
            {
                int techId = __instance.researchTechId;
                if (techId <= 0 || techId == lastDividedTechId)
                {
                    return;
                }

                lastDividedTechId = techId;

                int[] points = LabComponent.matrixPoints;
                for (int i = 0; i < points.Length; i++)
                {
                    if (points[i] > 0)
                    {
                        points[i] = (points[i] + Multiplier - 1) / Multiplier;
                    }
                }
            }
        }
    }
}
