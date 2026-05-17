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

        [HarmonyPatch(typeof(LabComponent), "InternalUpdateResearch")]
        private static class LabComponentInternalUpdateResearchPatch
        {
            private static void Prefix(ref float research_speed)
            {
                ApplyCurrentTechMultiplier();
                research_speed *= Multiplier;
            }
        }

        private static void ApplyCurrentTechMultiplier()
        {
            int currentTech = GameMain.history.currentTech;
            if (currentTech <= 0)
            {
                return;
            }

            TechProto techProto = LDB.techs.Select(currentTech);
            if (techProto == null || !techProto.IsLabTech)
            {
                return;
            }

            int[] points = LabComponent.matrixPoints;
            int baseId = LabComponent.matrixIds[0];
            int len = techProto.Items.Length < techProto.ItemPoints.Length ? techProto.Items.Length : techProto.ItemPoints.Length;

            for (int i = 0; i < len; i++)
            {
                int idx = techProto.Items[i] - baseId;
                if (idx < 0 || idx >= points.Length)
                {
                    continue;
                }

                int original = techProto.ItemPoints[i];
                if (original > 0 && points[idx] == original)
                {
                    points[idx] = (original + Multiplier - 1) / Multiplier;
                }
            }
        }
    }
}
