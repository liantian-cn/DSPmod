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
        private const float ThreatMultiplier = 0.99f;

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
