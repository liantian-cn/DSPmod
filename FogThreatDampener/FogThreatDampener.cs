using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace FogThreatDampener
{
    [BepInPlugin("me.liantian.plugin.FogThreatDampener", "FogThreatDampener", "0.0.1")]
    public class FogThreatDampener : BaseUnityPlugin
    {
        private const string PluginGuid = "me.liantian.plugin.FogThreatDampener";
        private const string PluginName = "FogThreatDampener";
        private const string PluginVersion = "0.0.1";

        private const int DampenIntervalHiveTicks = 30;
        private const float ThreatMultiplier = 0.99f;

        internal static ManualLogSource Log;

        private Harmony harmony;

        public void Awake()
        {
            Log = Logger;
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(FogThreatDampener).Assembly);
            Log.LogInfo($"{PluginName} {PluginVersion} initialized");
        }

        public void OnDestroy()
        {
            harmony?.UnpatchSelf();
            harmony = null;
        }

        [HarmonyPatch(typeof(EnemyDFHiveSystem), "KeyTickLogic")]
        private static class EnemyDFHiveSystemKeyTickLogicPatch
        {
            private static void Postfix(EnemyDFHiveSystem __instance, ref bool is_alive)
            {
                if (__instance == null || !is_alive || __instance.ticks <= 0)
                {
                    return;
                }
                if (__instance.ticks % DampenIntervalHiveTicks != 0)
                {
                    return;
                }
                if (__instance.evolve.waveTicks > 0 || __instance.evolve.waveAsmTicks > 0)
                {
                    return;
                }
                if (__instance.evolve.threat <= 0 || __instance.evolve.threat >= __instance.evolve.maxThreat)
                {
                    return;
                }

                __instance.evolve.threat = (int)((float)__instance.evolve.threat * ThreatMultiplier);
                if (__instance.evolve.threat < 0)
                {
                    __instance.evolve.threat = 0;
                }
            }
        }
    }
}
