using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace HardFog
{
    // 地热发电站电力线距离扩展控制器：将地热站的连接距离设为星球直径，
    // 使地热站能与星球上所有电力节点连接成电网。
    [HarmonyPatch]
    internal static class GeothermalPowerLineControl
    {
        // 独立 Harmony ID，配置关闭时只卸载本控制器的补丁。
        private const string PatchGuid = "me.liantian.plugin.HardFog.GeothermalPowerLine";

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

            // 关闭功能时恢复原版电力距离，后续清空引用方便 GC。
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

        // 安装或卸载电力距离补丁；只在状态变化时操作 Harmony，避免重复 patch。
        private static void SetActive(bool active)
        {
            if (active)
            {
                // 已安装时直接返回，防止同一个 Prefix 叠加执行。
                if (harmony != null)
                {
                    return;
                }

                harmony = Harmony.CreateAndPatchAll(typeof(GeothermalPowerLineControl), PatchGuid);
                Log?.LogInfo("GeothermalPowerLine enabled");
                return;
            }

            // 没有补丁时无需卸载，避免空引用和无意义日志。
            if (harmony == null)
            {
                return;
            }

            // 卸载后新放置的地热站回到原版距离；已放置的地热站不会被回滚。
            harmony.UnpatchSelf();
            harmony = null;
            Log?.LogInfo("GeothermalPowerLine disabled");
        }

        // 在 OnNodeAdded 执行前，检查节点对应的实体是否为地热发电站，
        // 如果是则将 nodePool 中的连接距离和覆盖半径扩展到星球直径，
        // 这样 OnNodeAdded 读取时会使用修改后的值来建立网络连接。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PowerSystem), "OnNodeAdded")]
        private static void OnNodeAddedPrefix(PowerSystem __instance, int nodeId)
        {
            // 节点 ID 无效时跳过。
            if (nodeId <= 0 || nodeId >= __instance.nodeCursor)
            {
                return;
            }

            PowerNodeComponent node = __instance.nodePool[nodeId];
            int entityId = node.entityId;
            if (entityId <= 0)
            {
                return;
            }

            EntityData entity = __instance.factory.entityPool[entityId];
            // 检查实体是否有有效的 protoId。
            if (entity.id != entityId)
            {
                return;
            }

            // 通过 LDB 查询物品原型，判断是否为地热发电站。
            ItemProto itemProto = LDB.items.Select(entity.protoId);
            if (itemProto == null || !itemProto.prefabDesc.geothermal || !itemProto.prefabDesc.isPowerNode)
            {
                return;
            }

            // 星球直径 = 2 * realRadius，作为新的连接距离；覆盖半径保持原版不变。
            float planetDiameter = __instance.factory.planet.realRadius * 2f;
            __instance.nodePool[nodeId].connectDistance = planetDiameter;

            Log?.LogInfo($"Geothermal node {nodeId} on planet {__instance.factory.planet.displayName}: connectDistance set to {planetDiameter:F1}");
        }
    }
}
