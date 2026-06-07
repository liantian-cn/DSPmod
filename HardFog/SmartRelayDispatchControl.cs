// ═══════════════════════════════════════════════════════════════
// DEPRECATED — 已弃用 — 仅保留作为学习参考（幽灵代码）
//
// 该功能已从 HardFog UI 和项目编译中移除。
// 本文件仅保留作为过去智能中继调度逻辑的学习参考。
// 相关功能已在 RelayControl.cs 中重新实现。
//
// Kept for reference only — not compiled or activated.
// Replaced by RelayControl.cs.
// ═══════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace HardFog
{
    [HarmonyPatch]
    internal static class SmartRelayDispatchControl
    {
        private const string PatchGuid = "me.liantian.plugin.HardFog.SmartRelayDispatch";

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

                harmony = Harmony.CreateAndPatchAll(typeof(SmartRelayDispatchControl), PatchGuid);
                Log?.LogInfo("SmartRelayDispatch enabled");
                return;
            }

            if (harmony == null)
            {
                return;
            }

            harmony.UnpatchSelf();
            harmony = null;
            Log?.LogInfo("SmartRelayDispatch disabled");
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(EnemyDFHiveSystem), "DetermineRelayDemand")]
        private static IEnumerable<CodeInstruction> EnemyDFHiveSystemDetermineRelayDemandTranspiler(IEnumerable<CodeInstruction> instructions)
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
            MethodInfo randomValueGetter = AccessTools.PropertyGetter(typeof(UnityEngine.Random), "value");
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
