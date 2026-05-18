using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SmartRelayDispatch
{
    [BepInPlugin("me.liantian.plugin.SmartRelayDispatch", "SmartRelayDispatch", "0.0.1")]
    public class SmartRelayDispatch : BaseUnityPlugin
    {
        private const string PluginGuid = "me.liantian.plugin.SmartRelayDispatch";
        private const string PluginName = "SmartRelayDispatch";
        private const string PluginVersion = "0.0.1";

        internal static ManualLogSource Log;

        private Harmony harmony;

        public void Awake()
        {
            Log = Logger;
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(SmartRelayDispatch).Assembly);
            Log.LogInfo("SmartRelayDispatch 0.0.1 initialized");
        }

        public void OnDestroy()
        {
            harmony?.UnpatchSelf();
            harmony = null;
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
                int gateIndex = FindDispatchProbabilityGate(codes);
                if (gateIndex < 0)
                {
                    return false;
                }

                codes[gateIndex].opcode = OpCodes.Ldc_I4_0;
                codes[gateIndex].operand = null;
                codes[gateIndex + 1].opcode = OpCodes.Nop;
                codes[gateIndex + 1].operand = null;
                codes[gateIndex + 2].opcode = OpCodes.Nop;
                codes[gateIndex + 2].operand = null;
                return true;
            }

            private static int FindDispatchProbabilityGate(IList<CodeInstruction> codes)
            {
                MethodInfo randomValueGetter = AccessTools.PropertyGetter(typeof(Random), "value");
                FieldInfo relayNeutralizedCounterField = AccessTools.Field(
                    typeof(EnemyDFHiveSystem),
                    nameof(EnemyDFHiveSystem.relayNeutralizedCounter));

                for (int i = 0; i < codes.Count - 2; i++)
                {
                    if (codes[i].opcode != OpCodes.Call ||
                        !Equals(codes[i].operand, randomValueGetter) ||
                        codes[i + 2].opcode != OpCodes.Clt)
                    {
                        continue;
                    }

                    if (HasRecentRelayNeutralizedCounterLoad(codes, i, relayNeutralizedCounterField))
                    {
                        return i;
                    }
                }

                return -1;
            }

            private static bool HasRecentRelayNeutralizedCounterLoad(
                IList<CodeInstruction> codes,
                int index,
                FieldInfo relayNeutralizedCounterField)
            {
                int start = index - 16;
                if (start < 0)
                {
                    start = 0;
                }

                for (int i = start; i < index; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldfld &&
                        Equals(codes[i].operand, relayNeutralizedCounterField))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
