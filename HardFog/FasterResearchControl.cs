using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace HardFog
{
    // 研究加速控制器：通过 Harmony 修改科技需求哈希，并同步当前存档里已存在的研究状态。
    [HarmonyPatch]
    internal static class FasterResearchControl
    {
        // 单独的 Harmony ID 方便只卸载本功能的补丁，不影响 HardFog 其他模块。
        private const string PatchGuid = "me.liantian.plugin.HardFog.FasterResearch";
        // 所有科技需求都会按这个倍率向上取整缩小，向上取整可以避免低需求科技被除成 0。
        private const int Multiplier = 48;

        // UI 复选框绑定这个配置；暴露给 HardFogWindow 是为了直接生成 UXAssist 控件。
        internal static ConfigEntry<bool> EnabledConfig { get; private set; }

        private static ManualLogSource Log;
        private static Harmony harmony;
        private static EventHandler settingChangedHandler;

        // 初始化时绑定配置变更事件，并立即按当前配置值安装或卸载补丁。
        internal static void Init(ConfigEntry<bool> enabledConfig, ManualLogSource log)
        {
            // 防御性解绑旧配置，避免热重载或重复初始化后同一个变更事件触发多次。
            if (EnabledConfig != null && settingChangedHandler != null)
            {
                EnabledConfig.SettingChanged -= settingChangedHandler;
            }

            // 保存运行时依赖，并让配置变化成为唯一的开关入口。
            Log = log;
            EnabledConfig = enabledConfig;
            settingChangedHandler = OnSettingChanged;
            EnabledConfig.SettingChanged += settingChangedHandler;
            SetActive(EnabledConfig.Value);
        }

        // 卸载时先解绑配置事件，再关闭补丁，保证插件销毁后不再引用旧对象。
        internal static void Uninit()
        {
            // 先解绑事件，否则 EnabledConfig 被清空后旧事件仍可能回调到本类。
            if (EnabledConfig != null && settingChangedHandler != null)
            {
                EnabledConfig.SettingChanged -= settingChangedHandler;
            }

            // 关闭补丁会顺带同步当前科技需求，让禁用功能后存档状态回到原版计算结果。
            SetActive(false);
            settingChangedHandler = null;
            EnabledConfig = null;
            Log = null;
        }

        // 配置文件或 UI 变更都会走这里，统一把 bool 配置转换为补丁状态。
        private static void OnSettingChanged(object sender, EventArgs args)
        {
            SetActive(EnabledConfig != null && EnabledConfig.Value);
        }

        // 根据开关安装/卸载 Harmony Patch，并立即刷新当前科技状态，避免 UI 需要重载存档才生效。
        private static void SetActive(bool active)
        {
            if (active)
            {
                // 已经安装过就不重复 Patch；重复 Patch 会导致 Postfix 叠加，使倍率被重复应用。
                if (harmony == null)
                {
                    harmony = Harmony.CreateAndPatchAll(typeof(FasterResearchControl), PatchGuid);
                    Log?.LogInfo("FasterResearch enabled, multiplier = " + Multiplier);
                }

                // 当前研究项目的 hashNeeded 是缓存值，开关打开后必须重算，正在研究的科技才会立刻变快。
                SyncCurrentTechHashNeeded(GameMain.history);
                return;
            }

            // 未安装时关闭不需要做任何事，避免空引用和多余日志。
            if (harmony == null)
            {
                return;
            }

            // 卸载补丁后再同步，TechProto.GetHashNeeded 会回到原版值，当前科技需求也跟着恢复。
            harmony.UnpatchSelf();
            harmony = null;
            Log?.LogInfo("FasterResearch disabled");
            SyncCurrentTechHashNeeded(GameMain.history);
        }

        // 修改科技原型返回的哈希需求；这是所有科技需求计算的公共入口，覆盖面最小也最稳定。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(TechProto), "GetHashNeeded")]
        private static void TechProtoGetHashNeededPostfix(ref long __result)
        {
            // Postfix 在原版计算后执行，只缩放结果，不需要复制原版科技公式。
            __result = DivideRoundUp(__result, Multiplier);
        }

        // 新游戏初始化科技状态后同步缓存值，避免刚开局时历史数据仍保存原版需求。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameHistoryData), "SetForNewGame")]
        private static void GameHistoryDataSetForNewGamePostfix(GameHistoryData __instance)
        {
            SyncCurrentTechHashNeeded(__instance);
        }

        // 读档后同步科技状态；预览导入不是实际游戏状态，不能改它。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameHistoryData), "Import")]
        private static void GameHistoryDataImportPostfix(GameHistoryData __instance, bool isPreview)
        {
            if (!isPreview)
            {
                SyncCurrentTechHashNeeded(__instance);
            }
        }

        // 整数向上取整除法，确保缩小后的需求仍至少覆盖原始需求的一小部分。
        private static long DivideRoundUp(long value, int divisor)
        {
            // 非正值按原样返回，避免把异常或特殊值改成普通需求。
            if (value <= 0)
            {
                return value;
            }

            return (value + divisor - 1L) / divisor;
        }

        // 刷新当前存档里每个未解锁科技的 hashNeeded 缓存，让开关变化立即反映到研究面板和进度计算。
        private static void SyncCurrentTechHashNeeded(GameHistoryData history)
        {
            // 主菜单或没有存档历史时直接退出；这些时机没有可同步的科技状态。
            if (history?.techStates == null)
            {
                return;
            }

            // 复制 key 列表后再遍历，避免写回 techStates 时枚举器失效。
            foreach (int techId in new List<int>(history.techStates.Keys))
            {
                TechState state = history.techStates[techId];
                // 已解锁科技不需要调整，否则可能影响游戏对完成状态的统计。
                if (state.unlocked)
                {
                    continue;
                }

                // 通过 LDB 找科技原型，再走 GetHashNeeded，确保当前 Harmony 状态下的公式被统一应用。
                TechProto techProto = LDB.techs.Select(techId);
                if (techProto == null)
                {
                    continue;
                }

                state.hashNeeded = techProto.GetHashNeeded(state.curLevel);
                // 如果旧进度已经超过新的需求，压到“差 1 点完成”，让游戏正常触发后续研究完成逻辑。
                if (state.hashUploaded >= state.hashNeeded)
                {
                    state.hashUploaded = state.hashNeeded - 1L;
                }
                // TechState 是值类型，修改局部副本后必须写回字典才会真正更新历史数据。
                history.techStates[techId] = state;
            }
        }
    }
}
