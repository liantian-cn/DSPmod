using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace HardFog
{
    // 建造条件放宽控制器：把“需要地基/地面支撑”视为可建造，用于水面或无地基位置的建筑摆放。
    [HarmonyPatch]
    internal static class BuildAnywhereOnWaterControl
    {
        // 使用独立 PatchGuid，配置关闭时只卸载本控制器的补丁。
        private const string PatchGuid = "me.liantian.plugin.HardFog.BuildAnywhereOnWater";

        // 暴露给配置 UI 的开关；值变更会动态安装或卸载 Harmony Patch。
        internal static ConfigEntry<bool> EnabledConfig { get; private set; }

        private static ManualLogSource Log;
        private static Harmony harmony;
        private static EventHandler settingChangedHandler;

        // 初始化配置监听，并按当前配置立即切换补丁状态。
        internal static void Init(ConfigEntry<bool> enabledConfig, ManualLogSource log)
        {
            // 防止重复 Init 时把旧配置事件留在内存里，避免一次切换触发多次 SetActive。
            if (EnabledConfig != null && settingChangedHandler != null)
            {
                EnabledConfig.SettingChanged -= settingChangedHandler;
            }

            // 保存配置和日志引用，后续只通过 SettingChanged 驱动补丁开关。
            Log = log;
            EnabledConfig = enabledConfig;
            settingChangedHandler = OnSettingChanged;
            EnabledConfig.SettingChanged += settingChangedHandler;
            SetActive(EnabledConfig.Value);
        }

        // 插件卸载时解除事件和 Patch，避免旧回调在下一次加载后继续存在。
        internal static void Uninit()
        {
            // 先解绑事件，再清空静态字段；否则旧 ConfigEntry 仍可能回调到已经卸载的模块。
            if (EnabledConfig != null && settingChangedHandler != null)
            {
                EnabledConfig.SettingChanged -= settingChangedHandler;
            }

            // 关闭补丁恢复原版建造条件，后续清空引用方便 GC。
            SetActive(false);
            settingChangedHandler = null;
            EnabledConfig = null;
            Log = null;
        }

        // 配置变化入口；统一把配置值转换为补丁激活状态。
        private static void OnSettingChanged(object sender, EventArgs args)
        {
            SetActive(EnabledConfig != null && EnabledConfig.Value);
        }

        // 安装或卸载建造条件补丁；只在状态变化时操作 Harmony，避免重复 patch。
        private static void SetActive(bool active)
        {
            if (active)
            {
                // 已安装时直接返回，防止同一个 Postfix 叠加执行。
                if (harmony != null)
                {
                    return;
                }

                // Patch 手动建造和蓝图建造两个入口，因为两者使用不同的 BuildTool。
                harmony = Harmony.CreateAndPatchAll(typeof(BuildAnywhereOnWaterControl), PatchGuid);
                Log?.LogInfo("BuildAnywhereOnWater enabled");
                return;
            }

            // 没有补丁时无需卸载，避免空引用和无意义日志。
            if (harmony == null)
            {
                return;
            }

            // 关闭功能时恢复原版条件检查，让游戏继续按原逻辑阻止缺地基建造。
            harmony.UnpatchSelf();
            harmony = null;
            Log?.LogInfo("BuildAnywhereOnWater disabled");
        }

        // 手动点击建造的条件检查后处理：只清除 NeedGround，并重新计算最终能否建造和光标提示。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildTool_Click), "CheckBuildConditions")]
        private static void BuildToolClickCheckBuildConditionsPostfix(BuildTool_Click __instance, ref bool __result)
        {
            // 主菜单或工具未准备好时没有预览列表，不能继续读取。
            if (__instance?.buildPreviews == null)
            {
                return;
            }

            // 只在确实清理了 NeedGround 时接管结果，避免误改其他建造失败原因。
            if (!ClearNeedGroundConditions(__instance.buildPreviews))
            {
                return;
            }

            // 清理后还要保留碰撞、缺连接等真实错误，所以重新扫描所有预览决定最终结果。
            __result = IsManualBuildAllowed(__instance.buildPreviews);
            RefreshManualCursor(__instance, __result);
        }

        // 蓝图粘贴的条件检查后处理：蓝图会维护额外错误列表，所以除了预览状态，还要清理错误缓存。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildTool_BlueprintPaste), "CheckBuildConditions")]
        private static void BuildToolBlueprintPasteCheckBuildConditionsPostfix(BuildTool_BlueprintPaste __instance, ref bool __result)
        {
            // 没有蓝图预览时不处理，避免访问空池或无效 cursor。
            if (__instance?.bpPool == null || __instance.bpCursor <= 0)
            {
                return;
            }

            // 只有发现 NeedGround 才继续；否则说明原版没有因为地面支撑失败。
            bool clearedNeedGround = ClearNeedGroundConditions(__instance.bpPool, __instance.bpCursor);
            if (!clearedNeedGround)
            {
                return;
            }

            // 有些蓝图连接错误只是由邻接预览仍带 NeedGround 引起，清理后需要再消除这些派生错误。
            bool clearedConnectionErrors = ClearConnectionErrors(__instance.bpPool, __instance.bpCursor);
            RemoveClearedBlueprintErrors(__instance, clearedConnectionErrors);
            __result = IsBlueprintBuildAllowed(__instance);
            // 蓝图最终允许建造时，把光标状态归零，避免 UI 继续显示原先的红色错误状态。
            if (__result && __instance.actionBuild?.model != null)
            {
                __instance.actionBuild.model.cursorState = 0;
            }
        }

        // 清理手动建造预览列表里的 NeedGround 条件；返回值表示是否真的改过状态。
        private static bool ClearNeedGroundConditions(IList<BuildPreview> previews)
        {
            bool changed = false;
            // 手动建造使用 IList，逐个预览清理可以兼容单体建筑和带连接的建筑。
            for (int i = 0; i < previews.Count; i++)
            {
                changed |= ClearNeedGroundCondition(previews[i]);
            }

            return changed;
        }

        // 清理蓝图预览数组中的 NeedGround 条件；count 来自 bpCursor，防止遍历未使用的池元素。
        private static bool ClearNeedGroundConditions(BuildPreview[] previews, int count)
        {
            bool changed = false;
            int limit = Math.Min(count, previews.Length);
            // 使用 Math.Min 保护数组边界，因为游戏对象池 cursor 理论上不该越界，但 mod 不能依赖这一点。
            for (int i = 0; i < limit; i++)
            {
                changed |= ClearNeedGroundCondition(previews[i]);
            }

            return changed;
        }

        // 把单个预览的 NeedGround 改成 Ok；其他错误保持原样，避免绕过碰撞、缺物品等限制。
        private static bool ClearNeedGroundCondition(BuildPreview bp)
        {
            // 预览槽位可能为空，蓝图对象池尤其常见。
            if (bp == null)
            {
                return false;
            }

            // 只处理缺地基/地面支撑，这是本功能的唯一放宽点。
            if (bp.condition == EBuildCondition.NeedGround)
            {
                bp.condition = EBuildCondition.Ok;
                return true;
            }

            return false;
        }

        // 清理因相邻预览带错误而产生的 ConnWithErrorBuilding；只有连接两端都可接受时才放行。
        private static bool ClearConnectionErrors(BuildPreview[] previews, int count)
        {
            bool changed = false;
            int limit = Math.Min(count, previews.Length);
            // 蓝图粘贴的连接件会因为 input/output 预览错误被标红，这里逐个检查是否只是 NeedGround 派生错误。
            for (int i = 0; i < limit; i++)
            {
                BuildPreview bp = previews[i];
                // 不是连接错误就跳过，避免修改其他真实失败条件。
                if (bp == null || bp.condition != EBuildCondition.ConnWithErrorBuilding)
                {
                    continue;
                }

                // 只有连接到的预览都处于允许状态，当前连接错误才可以清掉。
                if (!AreConnectionsAllowed(bp))
                {
                    continue;
                }

                // 清理后标记 changed，后续会同步清掉蓝图错误缓存里的对应条目。
                if (bp.condition == EBuildCondition.ConnWithErrorBuilding)
                {
                    bp.condition = EBuildCondition.Ok;
                    changed = true;
                }
            }

            return changed;
        }

        // 检查一个蓝图预览的输入和输出连接是否都已经没有必须保留的错误。
        private static bool AreConnectionsAllowed(BuildPreview bp)
        {
            return IsConnectedPreviewAllowed(bp.input) && IsConnectedPreviewAllowed(bp.output);
        }

        // 判断连接目标是否可接受；蓝图重叠在这里视为可接受，因为它是连接检查里的特殊占位状态。
        private static bool IsConnectedPreviewAllowed(BuildPreview bp)
        {
            // 空连接表示没有这一端；BlueprintBPOverlap 通常代表连接目标已经在蓝图中处理，不应该阻塞当前连接。
            if (bp == null || bp.condition == EBuildCondition.BlueprintBPOverlap)
            {
                return true;
            }

            return IsBlueprintAllowedCondition(bp.condition);
        }

        // 手动建造最终判定：除了 Ok 和等待连接 NeedConn，其余条件仍然视为不可建造。
        private static bool IsManualBuildAllowed(IList<BuildPreview> previews)
        {
            // 重新扫描全部预览，确保清掉 NeedGround 后没有其他错误被顺手放行。
            for (int i = 0; i < previews.Count; i++)
            {
                BuildPreview bp = previews[i];
                if (bp != null && bp.condition != EBuildCondition.Ok && bp.condition != EBuildCondition.NeedConn)
                {
                    return false;
                }
            }

            return true;
        }

        // 蓝图最终判定：只检查实际有 UI 模型的预览，过滤对象池中的空槽和辅助数据。
        private static bool IsBlueprintBuildAllowed(BuildTool_BlueprintPaste tool)
        {
            int limit = Math.Min(tool.bpCursor, tool.bpPool.Length);
            // 保留 NotEnoughItem 作为允许条件，是因为原版蓝图可在缺物品时继续进入待建状态。
            for (int i = 0; i < limit; i++)
            {
                BuildPreview bp = tool.bpPool[i];
                if (bp != null && bp.bpgpuiModelId > 0 && !IsBlueprintAllowedCondition(bp.condition))
                {
                    return false;
                }
            }

            return true;
        }

        // 蓝图粘贴中允许保留的条件集合；这里不包含碰撞、禁区等真实建造错误。
        private static bool IsBlueprintAllowedCondition(EBuildCondition condition)
        {
            return condition == EBuildCondition.Ok ||
                condition == EBuildCondition.NeedConn ||
                condition == EBuildCondition.NotEnoughItem;
        }

        // 同步清理蓝图工具内部缓存的错误类型、错误建筑和根网格错误，否则 UI 仍会显示已被清掉的错误。
        private static void RemoveClearedBlueprintErrors(BuildTool_BlueprintPaste tool, bool removeConnectionErrors)
        {
            // NeedGround 是本功能固定清理的错误；对应的错误建筑列表也必须一起压缩。
            RemoveCondition(tool._tmp_error_types, EBuildCondition.NeedGround);
            RemoveConditionErrorBuildings(tool, EBuildCondition.NeedGround);
            if (removeConnectionErrors)
            {
                // 连接错误只有在确认是 NeedGround 派生时才清，避免掩盖真实连接失败。
                RemoveCondition(tool._tmp_error_types, EBuildCondition.ConnWithErrorBuilding);
                RemoveConditionErrorBuildings(tool, EBuildCondition.ConnWithErrorBuilding);
            }

            // rootGridErrors 用于蓝图根网格的错误汇总；为空时说明没有额外缓存需要清理。
            if (tool.rootGridErrors == null)
            {
                return;
            }

            List<uint> emptyKeys = new List<uint>();
            // 遍历字典时不能直接删除 key，所以先记录空集合，后面再统一 Remove。
            foreach (KeyValuePair<uint, HashSet<EBuildCondition>> entry in tool.rootGridErrors)
            {
                if (entry.Value == null)
                {
                    emptyKeys.Add(entry.Key);
                    continue;
                }

                entry.Value.Remove(EBuildCondition.NeedGround);
                if (removeConnectionErrors)
                {
                    // 同步删除 ConnWithErrorBuilding，保证蓝图格子错误汇总和预览 condition 保持一致。
                    entry.Value.Remove(EBuildCondition.ConnWithErrorBuilding);
                }

                // 清理后没有错误的格子要删除，否则 UI 仍可能按空集合显示错误提示。
                if (entry.Value.Count == 0)
                {
                    emptyKeys.Add(entry.Key);
                }
            }

            for (int i = 0; i < emptyKeys.Count; i++)
            {
                tool.rootGridErrors.Remove(emptyKeys[i]);
            }
        }

        // 从错误类型列表里删除所有指定条件；while 是为了清掉重复出现的同一种错误。
        private static void RemoveCondition(List<EBuildCondition> conditions, EBuildCondition condition)
        {
            if (conditions == null)
            {
                return;
            }

            while (conditions.Remove(condition))
            {
                // Remove 一次只删一个元素，循环直到列表里没有该条件。
            }
        }

        // 压缩蓝图错误建筑数组，移除指定条件对应的条目，并保持 cursor 和数组内容一致。
        private static void RemoveConditionErrorBuildings(BuildTool_BlueprintPaste tool, EBuildCondition condition)
        {
            // 没有错误数组或 cursor 为 0 时无需处理。
            if (tool._tmpErrorBuildings == null || tool._tmpErrorTipsCursor <= 0)
            {
                return;
            }

            int writeIndex = 0;
            int limit = Math.Min(tool._tmpErrorTipsCursor, tool._tmpErrorBuildings.Length);
            // 使用读写指针原地压缩数组，避免重新分配并保持游戏工具对象的缓存结构。
            for (int readIndex = 0; readIndex < limit; readIndex++)
            {
                ulong error = tool._tmpErrorBuildings[readIndex];
                // 高 16 位保存 EBuildCondition；匹配目标条件的错误直接跳过。
                if ((EBuildCondition)(error >> 48) == condition)
                {
                    continue;
                }

                // 需要保留的错误向前写回，填补被删除条目留下的空洞。
                if (writeIndex != readIndex)
                {
                    tool._tmpErrorBuildings[writeIndex] = error;
                }

                writeIndex++;
            }

            // 清空尾部旧数据，避免 cursor 缩短后 UI 或调试代码读到残留错误。
            for (int i = writeIndex; i < limit; i++)
            {
                tool._tmpErrorBuildings[i] = 0UL;
            }

            // cursor 必须同步到压缩后的长度，否则后续错误提示仍会遍历已删除条目。
            tool._tmpErrorTipsCursor = writeIndex;
        }

        // 手动建造时同步光标状态；否则条件被清掉后 UI 可能仍显示原版的红色失败提示。
        private static void RefreshManualCursor(BuildTool_Click tool, bool allowed)
        {
            // 没有 actionBuild 模型时说明当前没有可刷新的建造光标。
            if (tool.actionBuild?.model == null)
            {
                return;
            }

            // 全部允许时恢复普通光标和 Ok 文本。
            if (allowed)
            {
                tool.actionBuild.model.cursorState = 0;
                tool.actionBuild.model.cursorText = BuildPreview.GetConditionText(EBuildCondition.Ok);
                return;
            }

            // 如果仍有真实错误，显示第一个不可忽略错误，避免用户不知道为什么不能建造。
            for (int i = 0; i < tool.buildPreviews.Count; i++)
            {
                BuildPreview bp = tool.buildPreviews[i];
                if (bp == null || bp.condition == EBuildCondition.Ok || bp.condition == EBuildCondition.NeedConn)
                {
                    continue;
                }

                tool.actionBuild.model.cursorState = -1;
                tool.actionBuild.model.cursorText = bp.conditionText;
                return;
            }
        }
    }
}
