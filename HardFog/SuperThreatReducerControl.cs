using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace HardFog
{
    /// <summary>
    /// 超级降压药 — 独立控制太空巢穴和地面基地的威胁度，直接跳过重型威胁累积运算。
    /// Super Threat Reducer — independently suppresses space hive and ground base threat,
    /// bypassing the heavy threat accumulation computations entirely.
    /// </summary>
    [HarmonyPatch]
    internal static class SuperThreatReducerControl
    {
        private const string PatchGuid = "me.liantian.plugin.HardFog.SuperThreatReducer";

        internal static ConfigEntry<bool> EnabledConfigHive { get; private set; }
        internal static ConfigEntry<bool> EnabledConfigGround { get; private set; }

        private static ManualLogSource Log;
        private static Harmony harmony;
        private static EventHandler settingChangedHandler;

        internal static void Init(
            ConfigEntry<bool> enabledConfigHive,
            ConfigEntry<bool> enabledConfigGround,
            ManualLogSource log)
        {
            if (EnabledConfigHive != null && settingChangedHandler != null)
            {
                EnabledConfigHive.SettingChanged -= settingChangedHandler;
                EnabledConfigGround.SettingChanged -= settingChangedHandler;
            }

            Log = log;
            EnabledConfigHive = enabledConfigHive;
            EnabledConfigGround = enabledConfigGround;
            settingChangedHandler = OnSettingChanged;
            EnabledConfigHive.SettingChanged += settingChangedHandler;
            EnabledConfigGround.SettingChanged += settingChangedHandler;
            UpdateHarmonyState();
        }

        internal static void Uninit()
        {
            if (EnabledConfigHive != null && settingChangedHandler != null)
            {
                EnabledConfigHive.SettingChanged -= settingChangedHandler;
                EnabledConfigGround.SettingChanged -= settingChangedHandler;
            }

            DestroyHarmony();
            settingChangedHandler = null;
            EnabledConfigHive = null;
            EnabledConfigGround = null;
            Log = null;
        }

        private static void OnSettingChanged(object sender, EventArgs args)
        {
            UpdateHarmonyState();
        }

        private static bool IsAnyEnabled =>
            (EnabledConfigHive != null && EnabledConfigHive.Value) ||
            (EnabledConfigGround != null && EnabledConfigGround.Value);

        private static void UpdateHarmonyState()
        {
            if (IsAnyEnabled)
            {
                EnsureHarmony();
            }
            else
            {
                DestroyHarmony();
            }
        }

        private static void EnsureHarmony()
        {
            if (harmony != null)
            {
                return;
            }

            harmony = Harmony.CreateAndPatchAll(typeof(SuperThreatReducerControl), PatchGuid);
            Log?.LogInfo("SuperThreatReducer enabled");
        }

        private static void DestroyHarmony()
        {
            if (harmony == null)
            {
                return;
            }

            harmony.UnpatchSelf();
            harmony = null;
            Log?.LogInfo("SuperThreatReducer disabled");
        }

        // ──────────────────────────────────────────────
        //  Space Hive patches
        // ──────────────────────────────────────────────

        /// <summary>
        /// After DecisionAI runs (sensor logic + hatred updates), zero out threat so it never accumulates.
        /// SensorLogic and UpdateHatred still execute normally — only the threat result is discarded.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(EnemyDFHiveSystem), "DecisionAI")]
        private static void EnemyDFHiveSystem_DecisionAI_Postfix(EnemyDFHiveSystem __instance)
        {
            if (EnabledConfigHive == null || !EnabledConfigHive.Value)
            {
                return;
            }

            if (__instance == null || !__instance.realized || !__instance.isAlive)
            {
                return;
            }

            __instance.evolve.threat = 0;
            __instance.evolve.threatshr = 0;
        }

        /// <summary>
        /// Completely skip the cross-planet power-grid scan + lancer assault launch.
        /// This is the heavy computation path for space hives.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(EnemyDFHiveSystem), "AssaultingWavesDetermineAI")]
        private static bool EnemyDFHiveSystem_AssaultingWavesDetermineAI_Prefix()
        {
            if (EnabledConfigHive == null || !EnabledConfigHive.Value)
            {
                return true;
            }

            return false;
        }

        // ──────────────────────────────────────────────
        //  Ground Base patch
        // ──────────────────────────────────────────────

        /// <summary>
        /// Completely skip the per-base power-grid scan + threat accumulation + ground assault launch.
        /// This is the single heaviest computation in the threat system.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DFGBaseComponent), "UpdateFactoryThreat")]
        private static bool DFGBaseComponent_UpdateFactoryThreat_Prefix()
        {
            if (EnabledConfigGround == null || !EnabledConfigGround.Value)
            {
                return true;
            }

            return false;
        }
    }
}
