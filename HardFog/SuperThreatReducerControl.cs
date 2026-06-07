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
        // 独立 Harmony ID 让本模块可以单独卸载，不影响 HardFog 其他补丁。
        private const string PatchGuid = "me.liantian.plugin.HardFog.SuperThreatReducer";

        // 太空巢穴和地面基地分成两个开关，因为玩家可能只想关闭其中一种威胁来源。
        internal static ConfigEntry<bool> EnabledConfigHive { get; private set; }
        internal static ConfigEntry<bool> EnabledConfigGround { get; private set; }

        private static ManualLogSource Log;
        private static Harmony harmony;
        private static EventHandler settingChangedHandler;

        // 初始化两个配置项，并根据任意一个开关是否启用来决定是否安装补丁。
        internal static void Init(
            ConfigEntry<bool> enabledConfigHive,
            ConfigEntry<bool> enabledConfigGround,
            ManualLogSource log)
        {
            // 防御性解绑旧配置，避免热重载或重复 Init 后事件处理器叠加。
            if (EnabledConfigHive != null && settingChangedHandler != null)
            {
                EnabledConfigHive.SettingChanged -= settingChangedHandler;
                EnabledConfigGround.SettingChanged -= settingChangedHandler;
            }

            // 保存依赖并同时监听两个开关；Harmony 只装一套，具体行为在补丁函数里按开关分流。
            Log = log;
            EnabledConfigHive = enabledConfigHive;
            EnabledConfigGround = enabledConfigGround;
            settingChangedHandler = OnSettingChanged;
            EnabledConfigHive.SettingChanged += settingChangedHandler;
            EnabledConfigGround.SettingChanged += settingChangedHandler;
            UpdateHarmonyState();
        }

        // 插件卸载时解绑配置事件并卸载补丁，防止静态事件保留已失效的控制器状态。
        internal static void Uninit()
        {
            // 先解绑事件，再清空配置引用，避免旧 ConfigEntry 继续回调。
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

        // 任一配置变更后重新计算整体 Harmony 状态。
        private static void OnSettingChanged(object sender, EventArgs args)
        {
            UpdateHarmonyState();
        }

        // 只要太空或地面降压有一个启用，就需要安装补丁；具体行为由各补丁函数判断。
        private static bool IsAnyEnabled =>
            (EnabledConfigHive != null && EnabledConfigHive.Value) ||
            (EnabledConfigGround != null && EnabledConfigGround.Value);

        // 根据两个开关的组合决定安装或卸载 Harmony，避免无功能启用时仍拦截游戏逻辑。
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

        // 确保补丁只安装一次；重复安装会让 Prefix/Postfix 多次执行。
        private static void EnsureHarmony()
        {
            if (harmony != null)
            {
                return;
            }

            harmony = Harmony.CreateAndPatchAll(typeof(SuperThreatReducerControl), PatchGuid);
            Log?.LogInfo("SuperThreatReducer enabled");
        }

        // 卸载本模块所有补丁；两个开关都关闭时恢复原版威胁逻辑。
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
            // 太空降压未启用时不修改任何巢穴状态。
            if (EnabledConfigHive == null || !EnabledConfigHive.Value)
            {
                return;
            }

            // 只处理已经实体化并存活的巢穴，避免改到未加载或已死亡的系统数据。
            if (__instance == null || !__instance.realized || !__instance.isAlive)
            {
                return;
            }

            // DecisionAI 已经跑完，直接丢弃威胁结果即可阻止威胁累计触发后续攻击。
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
            // 开关关闭时返回 true，允许原版继续进行跨星球电网扫描和攻击判定。
            if (EnabledConfigHive == null || !EnabledConfigHive.Value)
            {
                return true;
            }

            // 开关打开时跳过原函数，这是减少太空巢穴威胁计算开销的核心。
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
            // 地面降压关闭时保留原版逻辑。
            if (EnabledConfigGround == null || !EnabledConfigGround.Value)
            {
                return true;
            }

            // 地面降压打开时跳过原函数，直接阻断威胁累计和地面攻击波生成。
            return false;
        }
    }
}
