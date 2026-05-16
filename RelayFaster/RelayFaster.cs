using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace RelayFaster
{
    [BepInPlugin("me.liantian.plugin.RelayFaster", "RelayFaster", "0.0.1")]
    public class RelayFaster : BaseUnityPlugin
    {
        private const string PluginGuid = "me.liantian.plugin.RelayFaster";
        private const string PluginName = "RelayFaster";
        private const string PluginVersion = "0.0.1";

        private const int DispatchIntervalTicks = 60;
        private const int OriginalMatterStatPeriodTicks = 600;

        internal static ManualLogSource Log;

        private Harmony harmony;

        public void Awake()
        {
            Log = Logger;
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(RelayFaster).Assembly);
            Log.LogInfo("RelayFaster 0.0.1 initialized");
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
                if (!__instance.matterStatComplete)
                {
                    return;
                }
                if (__instance.ticks % DispatchIntervalTicks != 0)
                {
                    return;
                }
                if (__instance.ticks % OriginalMatterStatPeriodTicks == 0)
                {
                    return;
                }

                __instance.DetermineRelayDemand();
            }
        }

        [HarmonyPatch(typeof(EnemyDFHiveSystem), "DetermineRelayDemand")]
        private static class EnemyDFHiveSystemDetermineRelayDemandPatch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
                if (!ReplaceRandomProbabilityWithMarkerGate(codes))
                {
                    Log?.LogWarning("Failed to patch relay dispatch probability gate");
                }
                return codes;
            }

            private static bool ReplaceRandomProbabilityWithMarkerGate(IList<CodeInstruction> codes)
            {
                object randomValueGetter = AccessTools.PropertyGetter(typeof(Random), "value");
                for (int i = 0; i < codes.Count - 2; i++)
                {
                    if (codes[i].opcode == OpCodes.Call &&
                        Equals(codes[i].operand, randomValueGetter) &&
                        codes[i + 2].opcode == OpCodes.Clt)
                    {
                        codes[i].opcode = OpCodes.Ldc_I4_0;
                        codes[i].operand = null;
                        codes[i + 1].opcode = OpCodes.Nop;
                        codes[i + 1].operand = null;
                        codes[i + 2].opcode = OpCodes.Nop;
                        codes[i + 2].operand = null;
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
