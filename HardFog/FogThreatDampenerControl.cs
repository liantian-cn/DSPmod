// ═══════════════════════════════════════════════════════════════
// DEPRECATED — 已弃用 — 仅保留作为学习参考（幽灵代码）
//
// 该功能已被 SuperThreatReducerControl 取代。
// SuperThreatReducerControl 提供两个独立开关(太空/地面)，
// 且使用 Harmony Prefix/Postfix 直接跳过重型运算，
// 性能远优于本文件的 ×0.999 乘法衰减。
//
// 本文件不再被编译(.csproj 中已注释)、不再在 UI 中显示。
//
// This file is superseded by SuperThreatReducerControl.
// Kept for reference only — not compiled or activated.
// ═══════════════════════════════════════════════════════════════

using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace HardFog
{
    [HarmonyPatch]
    internal static class FogThreatDampenerControl
    {
        private const string PatchGuid = "me.liantian.plugin.HardFog.FogThreatDampener";
        private const int DampenIntervalHiveTicks = 30;
        private const float ThreatMultiplier = 0.999f;

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

                harmony = Harmony.CreateAndPatchAll(typeof(FogThreatDampenerControl), PatchGuid);
                Log?.LogInfo("FogThreatDampener enabled");
                return;
            }

            if (harmony == null)
            {
                return;
            }

            harmony.UnpatchSelf();
            harmony = null;
            Log?.LogInfo("FogThreatDampener disabled");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(EnemyDFHiveSystem), "KeyTickLogic")]
        private static void EnemyDFHiveSystemKeyTickLogicPostfix(EnemyDFHiveSystem __instance, ref bool is_alive)
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
