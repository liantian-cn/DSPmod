using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace HardFog
{
    // 强化机甲战机控制器：临时放大机甲战机的索敌范围、攻击倍率，并让已生成战机保持无敌。
    [HarmonyPatch]
    internal static class OverpoweredMechaFightersControl
    {
        // 单独的 Harmony ID 用来只卸载战机强化补丁。
        private const string PatchGuid = "me.liantian.plugin.HardFog.OverpoweredMechaFighters";
        // 范围和伤害都通过临时倍增实现，避免永久改写原型数据。
        private const float RangeMultiplier = 5f;
        private const float DamageMultiplier = 5f;

        // 配置 UI 绑定的开关；开关变化会动态启停全部战机补丁。
        internal static ConfigEntry<bool> EnabledConfig { get; private set; }

        private static ManualLogSource Log;
        private static Harmony harmony;
        private static EventHandler settingChangedHandler;

        // 初始化配置监听，并按当前配置立即启用或禁用战机强化。
        internal static void Init(ConfigEntry<bool> enabledConfig, ManualLogSource log)
        {
            // 防止重复初始化时旧配置事件没有解绑，导致开关变更重复执行。
            if (EnabledConfig != null && settingChangedHandler != null)
            {
                EnabledConfig.SettingChanged -= settingChangedHandler;
            }

            // 保存依赖并把配置变化统一转发给 SetActive。
            Log = log;
            EnabledConfig = enabledConfig;
            settingChangedHandler = OnSettingChanged;
            EnabledConfig.SettingChanged += settingChangedHandler;
            SetActive(EnabledConfig.Value);
        }

        // 卸载时解绑配置事件、卸载补丁，并把已加载机甲战机的无敌状态恢复。
        internal static void Uninit()
        {
            // 先解绑事件，避免旧 ConfigEntry 在字段清空后继续回调。
            if (EnabledConfig != null && settingChangedHandler != null)
            {
                EnabledConfig.SettingChanged -= settingChangedHandler;
            }

            SetActive(false);
            settingChangedHandler = null;
            EnabledConfig = null;
            Log = null;
        }

        // 配置变更入口；用当前配置值控制 Harmony 和已加载战机状态。
        private static void OnSettingChanged(object sender, EventArgs args)
        {
            SetActive(EnabledConfig != null && EnabledConfig.Value);
        }

        // 安装或卸载战机强化补丁；启用/禁用时都会扫描已加载单位，保证当前存档立即生效。
        private static void SetActive(bool active)
        {
            if (active)
            {
                // 已经启用时不重复安装，避免多个 Prefix 叠加造成范围/伤害重复放大。
                if (harmony != null)
                {
                    return;
                }

                // 安装补丁后立刻把现有机甲战机设为无敌，避免只有新生成单位生效。
                harmony = Harmony.CreateAndPatchAll(typeof(OverpoweredMechaFightersControl), PatchGuid);
                SetLoadedMechaFightersInvincible(true);
                Log?.LogInfo("OverpoweredMechaFighters enabled");
                return;
            }

            // 未启用时关闭没有需要恢复的补丁状态。
            if (harmony == null)
            {
                return;
            }

            // 卸载补丁后取消已加载战机的无敌，临时范围/伤害 boost 会由各 Finalizer 自行恢复。
            harmony.UnpatchSelf();
            harmony = null;
            SetLoadedMechaFightersInvincible(false);
            Log?.LogInfo("OverpoweredMechaFighters disabled");
        }

        // 判断 CraftData 是否是玩家机甲舰队里的战机，而不是普通敌人、物流单位或舰队母体。
        private static bool IsMechaFighter(ref CraftData craft, CraftData[] craftPool)
        {
            // craft.owner 指向所属舰队，unitId 表示它已经对应了战斗单位；这些边界先校验对象池安全。
            if (craftPool == null || craft.id <= 0 || craft.owner <= 0 || craft.owner >= craftPool.Length || craft.unitId <= 0)
            {
                return false;
            }

            ref CraftData ownerFleet = ref craftPool[craft.owner];
            // 机甲舰队母体的 owner 为 -1；战机的 owner 指向这个舰队，所以用这个特征区分玩家战机。
            return ownerFleet.id == craft.owner && ownerFleet.owner == -1;
        }

        // 从战机找到所属机甲舰队的 PrefabDesc；范围参数存在舰队原型上，不在单个战机上。
        private static bool TryGetMechaFleetDesc(ref CraftData craft, CraftData[] craftPool, out PrefabDesc fleetDesc)
        {
            fleetDesc = null;
            // 只有确认是机甲战机才继续找舰队原型，避免误改其他 Craft 的描述数据。
            if (!IsMechaFighter(ref craft, craftPool))
            {
                return false;
            }

            ref CraftData ownerFleet = ref craftPool[craft.owner];
            // modelIndex 必须落在 PrefabDescByModelIndex 范围内，否则不能安全取原型描述。
            if (ownerFleet.modelIndex < 0 || ownerFleet.modelIndex >= PlanetFactory.PrefabDescByModelIndex.Length)
            {
                return false;
            }

            fleetDesc = PlanetFactory.PrefabDescByModelIndex[ownerFleet.modelIndex];
            return fleetDesc != null;
        }

        // 将指定战机设为无敌的便捷入口；用于新生成单位的 Postfix。
        private static void MakeInvincible(ref CraftData craft, CraftData[] craftPool, SkillSystem skillSystem)
        {
            SetInvincible(ref craft, craftPool, skillSystem, true);
        }

        // 设置机甲战机的无敌状态；启用无敌时顺带把血量回满，避免刚受伤的单位继续显示低血量。
        private static void SetInvincible(ref CraftData craft, CraftData[] craftPool, SkillSystem skillSystem, bool invincible)
        {
            // 只处理玩家机甲战机，避免把敌人或其他无人机一起设为无敌。
            if (!IsMechaFighter(ref craft, craftPool))
            {
                return;
            }

            craft.isInvincible = invincible;
            // 取消无敌或没有战斗统计对象时不需要修血量。
            if (!invincible || skillSystem == null || craft.combatStatId <= 0)
            {
                return;
            }

            ref CombatStat combatStat = ref skillSystem.combatStats.buffer[craft.combatStatId];
            // CombatStat 的 id 也要匹配对象池索引，防止写到空槽或旧对象。
            if (combatStat.id == craft.combatStatId)
            {
                combatStat.hp = combatStat.hpMax;
                combatStat.hpIncoming = 0;
            }
        }

        // 保存 CombatModuleComponent.sensorRange 的临时修改状态，Postfix/Finalizer 用它恢复原值。
        private struct SensorRangeState
        {
            public bool boosted;
            public float originalRange;
        }

        // 保存战斗升级伤害倍率的临时修改状态，避免战机攻击后影响其他单位。
        private struct DamageRatioState
        {
            public bool boosted;
            public float originalRatio;
        }

        // 保存舰队 PrefabDesc 的临时范围修改状态；PrefabDesc 是共享原型，必须严格恢复。
        private struct FleetDescState
        {
            public bool boosted;
            public PrefabDesc desc;
            public float originalSensorRange;
            public float originalActiveArea;
        }

        // 同一次战机行为可能同时改伤害倍率和舰队范围，用组合状态统一传给 Finalizer。
        private struct FighterBehaviorState
        {
            public DamageRatioState damageRatio;
            public FleetDescState fleetDesc;
        }

        // 临时放大玩家机甲模块的索敌范围；只在原函数执行期间生效。
        private static void BoostSensorRange(CombatModuleComponent module, ref SensorRangeState __state)
        {
            __state = default(SensorRangeState);
            // entityId == 0 表示玩家机甲本体模块；建筑或其他实体模块不应该被放大。
            if (module == null || module.entityId != 0)
            {
                return;
            }

            // 记录原值后再修改，Postfix 会恢复，避免永久污染模块状态。
            __state.boosted = true;
            __state.originalRange = module.sensorRange;
            module.sensorRange *= RangeMultiplier;
        }

        // 恢复临时放大的索敌范围；只有实际 boost 过才写回。
        private static void RestoreSensorRange(CombatModuleComponent module, SensorRangeState state)
        {
            if (state.boosted && module != null)
            {
                module.sensorRange = state.originalRange;
            }
        }

        // 对机甲战机攻击临时放大战斗升级里的无人机伤害倍率。
        private static void BoostDamageForMechaFighter(ref CraftData craft, CraftData[] craftPool, ref CombatUpgradeData combatUpgradeData, ref DamageRatioState __state)
        {
            __state = default(DamageRatioState);
            // 不是机甲战机就不改倍率，避免普通战斗单位受影响。
            if (!IsMechaFighter(ref craft, craftPool))
            {
                return;
            }

            // CombatUpgradeData 通常是本次行为使用的参数，仍然保存原值以防它被复用。
            __state.boosted = true;
            __state.originalRatio = combatUpgradeData.combatDroneDamageRatio;
            combatUpgradeData.combatDroneDamageRatio *= DamageMultiplier;
        }

        // 恢复临时伤害倍率，避免一次攻击后全局伤害倍率保持放大。
        private static void RestoreDamageRatio(ref CombatUpgradeData combatUpgradeData, DamageRatioState state)
        {
            if (state.boosted)
            {
                combatUpgradeData.combatDroneDamageRatio = state.originalRatio;
            }
        }

        // 临时放大舰队原型上的探测范围和活跃范围；原型是共享对象，所以必须成对恢复。
        private static void BoostFleetDesc(PrefabDesc pdesc, ref FleetDescState __state)
        {
            __state = default(FleetDescState);
            // 没有原型描述时无法修改舰队范围。
            if (pdesc == null)
            {
                return;
            }

            // 记录原始值，Finalizer 即使原函数抛异常也能恢复共享原型。
            __state.boosted = true;
            __state.desc = pdesc;
            __state.originalSensorRange = pdesc.fleetSensorRange;
            __state.originalActiveArea = pdesc.fleetMaxActiveArea;
            pdesc.fleetSensorRange *= RangeMultiplier;
            pdesc.fleetMaxActiveArea *= RangeMultiplier;
        }

        // 只有机甲舰队母体才放大舰队原型范围，避免普通舰队逻辑被误改。
        private static void BoostFleetDescForMechaFleet(ref CraftData fleetCraft, PrefabDesc pdesc, ref FleetDescState __state)
        {
            __state = default(FleetDescState);
            if (fleetCraft.owner == -1)
            {
                BoostFleetDesc(pdesc, ref __state);
            }
        }

        // 恢复舰队原型范围；这是保护共享 PrefabDesc 不被永久改写的关键。
        private static void RestoreFleetDesc(FleetDescState state)
        {
            if (!state.boosted || state.desc == null)
            {
                return;
            }

            state.desc.fleetSensorRange = state.originalSensorRange;
            state.desc.fleetMaxActiveArea = state.originalActiveArea;
        }

        // 在战机攻击行为前同时放大战斗伤害和舰队范围，保证更远距离也能正确执行攻击逻辑。
        private static void BoostFighterBehavior(ref CraftData craft, CraftData[] craftPool, ref CombatUpgradeData combatUpgradeData, ref FighterBehaviorState __state)
        {
            __state = default(FighterBehaviorState);
            BoostDamageForMechaFighter(ref craft, craftPool, ref combatUpgradeData, ref __state.damageRatio);
            BoostFighterFleetDesc(ref craft, craftPool, ref __state.fleetDesc);
        }

        // 从单个战机反查所属舰队原型，并临时放大舰队范围。
        private static void BoostFighterFleetDesc(ref CraftData craft, CraftData[] craftPool, ref FleetDescState __state)
        {
            __state = default(FleetDescState);
            if (TryGetMechaFleetDesc(ref craft, craftPool, out PrefabDesc fleetDesc))
            {
                BoostFleetDesc(fleetDesc, ref __state);
            }
        }

        // 还原一次战机行为临时修改过的全部状态。
        private static void RestoreFighterBehavior(ref CombatUpgradeData combatUpgradeData, FighterBehaviorState state)
        {
            RestoreDamageRatio(ref combatUpgradeData, state.damageRatio);
            RestoreFleetDesc(state.fleetDesc);
        }

        // 扫描当前已经加载的地面和太空战机，统一设置或取消无敌。
        private static void SetLoadedMechaFightersInvincible(bool invincible)
        {
            GameData data = GameMain.data;
            // 地面战机分布在各个已加载工厂的 CombatGroundSystem 中。
            if (data?.factories != null)
            {
                for (int i = 0; i < data.factories.Length; i++)
                {
                    RefreshGroundMechaFighters(data.factories[i]?.combatGroundSystem, invincible);
                }
            }

            // 太空战机在全局 SpaceSector 的 CombatSpaceSystem 中。
            RefreshSpaceMechaFighters(GameMain.spaceSector?.combatSpaceSystem, invincible);
        }

        // 默认刷新为无敌状态；GameTick 周期性调用这个重载，防止游戏逻辑把无敌位改回去。
        private static void RefreshGroundMechaFighters(CombatGroundSystem combatSystem)
        {
            RefreshGroundMechaFighters(combatSystem, true);
        }

        // 扫描地面战斗单位对象池，把属于机甲舰队的战机设置为目标无敌状态。
        private static void RefreshGroundMechaFighters(CombatGroundSystem combatSystem, bool invincible)
        {
            // 没有工厂、craftPool 或单位池时说明当前星球没有可处理的地面战机。
            if (combatSystem?.factory?.craftPool == null || combatSystem.units == null)
            {
                return;
            }

            CraftData[] craftPool = combatSystem.factory.craftPool;
            UnitComponent[] units = combatSystem.units.buffer;
            int cursor = combatSystem.units.cursor;
            // UnitComponent 对象池从 1 开始，id 必须匹配索引才是有效单位。
            for (int i = 1; i < cursor; i++)
            {
                ref UnitComponent unit = ref units[i];
                if (unit.id != i || unit.craftId <= 0)
                {
                    continue;
                }

                ref CraftData craft = ref craftPool[unit.craftId];
                // SetInvincible 内部会再次确认 craft 是否是机甲战机。
                SetInvincible(ref craft, craftPool, combatSystem.factory.skillSystem, invincible);
            }
        }

        // 默认刷新太空战机为无敌状态；用于周期性维护。
        private static void RefreshSpaceMechaFighters(CombatSpaceSystem combatSystem)
        {
            RefreshSpaceMechaFighters(combatSystem, true);
        }

        // 扫描太空战斗单位对象池，把属于机甲舰队的战机设置为目标无敌状态。
        private static void RefreshSpaceMechaFighters(CombatSpaceSystem combatSystem, bool invincible)
        {
            // 没有太空扇区 craftPool 或单位池时没有可处理对象。
            if (combatSystem?.spaceSector?.craftPool == null || combatSystem.units == null)
            {
                return;
            }

            CraftData[] craftPool = combatSystem.spaceSector.craftPool;
            UnitComponent[] units = combatSystem.units.buffer;
            int cursor = combatSystem.units.cursor;
            // 遍历有效 UnitComponent，再通过 craftId 找对应 CraftData。
            for (int i = 1; i < cursor; i++)
            {
                ref UnitComponent unit = ref units[i];
                if (unit.id != i || unit.craftId <= 0)
                {
                    continue;
                }

                ref CraftData craft = ref craftPool[unit.craftId];
                SetInvincible(ref craft, craftPool, combatSystem.spaceSector.skillSystem, invincible);
            }
        }

        // 地面新单位生成后立即检查是否是机甲战机；新生成单位不在启用时的全量扫描结果里。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CombatGroundSystem), "NewUnitComponent")]
        private static void CombatGroundSystemNewUnitComponentPostfix(CombatGroundSystem __instance, int craftId)
        {
            // craftId 必须落在工厂 craftPool 范围内，避免对象池越界。
            if (__instance?.factory?.craftPool == null || craftId <= 0 || craftId >= __instance.factory.craftPool.Length)
            {
                return;
            }

            ref CraftData craft = ref __instance.factory.craftPool[craftId];
            // MakeInvincible 内部会过滤非机甲战机。
            MakeInvincible(ref craft, __instance.factory.craftPool, __instance.factory.skillSystem);
        }

        // 太空新单位生成后同样补设无敌，覆盖战机在太空场景生成的路径。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CombatSpaceSystem), "NewUnitComponent")]
        private static void CombatSpaceSystemNewUnitComponentPostfix(CombatSpaceSystem __instance, int craftId)
        {
            // 太空单位使用 SpaceSector 的 craftPool，与地面工厂对象池不同。
            if (__instance?.spaceSector?.craftPool == null || craftId <= 0 || craftId >= __instance.spaceSector.craftPool.Length)
            {
                return;
            }

            ref CraftData craft = ref __instance.spaceSector.craftPool[craftId];
            MakeInvincible(ref craft, __instance.spaceSector.craftPool, __instance.spaceSector.skillSystem);
        }

        // 地面战斗系统每秒刷新一次无敌状态，防止原版逻辑或读档同步覆盖 craft.isInvincible。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CombatGroundSystem), "GameTick")]
        private static void CombatGroundSystemGameTickPostfix(CombatGroundSystem __instance, long tick)
        {
            // 60 tick 约等于一秒，足够及时又不会每帧扫描对象池。
            if (tick % 60 == 0)
            {
                RefreshGroundMechaFighters(__instance);
            }
        }

        // 太空战斗系统每秒刷新一次无敌状态，和地面路径保持一致。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CombatSpaceSystem), "GameTick")]
        private static void CombatSpaceSystemGameTickPostfix(CombatSpaceSystem __instance, long tick)
        {
            // 周期性维护可以覆盖新生成、切场景或游戏状态刷新带来的状态回退。
            if (tick % 60 == 0)
            {
                RefreshSpaceMechaFighters(__instance);
            }
        }

        // 本地索敌前临时扩大机甲模块感知范围，让战机能发现更远的地面目标。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CombatModuleComponent), "DiscoverLocalEnemy")]
        private static void CombatModuleComponentDiscoverLocalEnemyPrefix(CombatModuleComponent __instance, ref SensorRangeState __state)
        {
            BoostSensorRange(__instance, ref __state);
        }

        // 本地索敌后恢复感知范围，避免永久改变 CombatModuleComponent 的共享状态。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CombatModuleComponent), "DiscoverLocalEnemy")]
        private static void CombatModuleComponentDiscoverLocalEnemyPostfix(CombatModuleComponent __instance, SensorRangeState __state)
        {
            RestoreSensorRange(__instance, __state);
        }

        // 太空索敌前临时扩大机甲模块感知范围，让战机能发现更远的太空目标。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CombatModuleComponent), "DiscoverSpaceEnemy")]
        private static void CombatModuleComponentDiscoverSpaceEnemyPrefix(CombatModuleComponent __instance, ref SensorRangeState __state)
        {
            BoostSensorRange(__instance, ref __state);
        }

        // 太空索敌后恢复感知范围，保证范围 boost 只作用于本次搜索。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CombatModuleComponent), "DiscoverSpaceEnemy")]
        private static void CombatModuleComponentDiscoverSpaceEnemyPostfix(CombatModuleComponent __instance, SensorRangeState __state)
        {
            RestoreSensorRange(__instance, __state);
        }

        // 地面舰队传感器逻辑前临时放大机甲舰队原型范围。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(FleetComponent), "SensorLogic_Ground")]
        private static void FleetComponentSensorLogicGroundPrefix(ref CraftData craft, PrefabDesc pdesc, ref FleetDescState __state)
        {
            BoostFleetDescForMechaFleet(ref craft, pdesc, ref __state);
        }

        // 用 Finalizer 而不是普通 Postfix，是为了原函数抛异常时也能恢复共享 PrefabDesc。
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(FleetComponent), "SensorLogic_Ground")]
        private static void FleetComponentSensorLogicGroundFinalizer(PrefabDesc pdesc, FleetDescState __state)
        {
            RestoreFleetDesc(__state);
        }

        // 太空舰队传感器逻辑前临时放大机甲舰队原型范围。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(FleetComponent), "SensorLogic_Space")]
        private static void FleetComponentSensorLogicSpacePrefix(ref CraftData craft, PrefabDesc pdesc, ref FleetDescState __state)
        {
            BoostFleetDescForMechaFleet(ref craft, pdesc, ref __state);
        }

        // 太空传感器结束后恢复原型范围，避免影响其他舰队或后续计算。
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(FleetComponent), "SensorLogic_Space")]
        private static void FleetComponentSensorLogicSpaceFinalizer(PrefabDesc pdesc, FleetDescState __state)
        {
            RestoreFleetDesc(__state);
        }

        // 地面舰队激活敌方单位前放大范围；这里需要从 FleetComponent.craftId 反查 CraftData。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(FleetComponent), "ActiveEnemyUnits_Ground")]
        private static void FleetComponentActiveEnemyUnitsGroundPrefix(FleetComponent __instance, PlanetFactory factory, PrefabDesc pdesc, ref FleetDescState __state)
        {
            __state = default(FleetDescState);
            // craftId 必须在地面工厂 craftPool 范围内，才能确认它是否属于机甲舰队。
            if (factory?.craftPool == null || __instance.craftId <= 0 || __instance.craftId >= factory.craftPool.Length)
            {
                return;
            }

            ref CraftData craft = ref factory.craftPool[__instance.craftId];
            BoostFleetDescForMechaFleet(ref craft, pdesc, ref __state);
        }

        // 地面激活逻辑结束后恢复舰队原型范围。
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(FleetComponent), "ActiveEnemyUnits_Ground")]
        private static void FleetComponentActiveEnemyUnitsGroundFinalizer(PrefabDesc pdesc, FleetDescState __state)
        {
            RestoreFleetDesc(__state);
        }

        // 太空舰队激活敌方单位前放大范围；太空路径使用 SpaceSector.craftPool。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(FleetComponent), "ActiveEnemyUnits_Space")]
        private static void FleetComponentActiveEnemyUnitsSpacePrefix(FleetComponent __instance, SpaceSector sector, PrefabDesc pdesc, ref FleetDescState __state)
        {
            __state = default(FleetDescState);
            // 太空 craftId 必须落在 sector craftPool 内。
            if (sector?.craftPool == null || __instance.craftId <= 0 || __instance.craftId >= sector.craftPool.Length)
            {
                return;
            }

            ref CraftData craft = ref sector.craftPool[__instance.craftId];
            BoostFleetDescForMechaFleet(ref craft, pdesc, ref __state);
        }

        // 太空激活逻辑结束后恢复舰队原型范围。
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(FleetComponent), "ActiveEnemyUnits_Space")]
        private static void FleetComponentActiveEnemyUnitsSpaceFinalizer(PrefabDesc pdesc, FleetDescState __state)
        {
            RestoreFleetDesc(__state);
        }

        // 地面单位 Tick 前放大所属机甲舰队范围，让单个战机的行为判断能看到更远目标。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnitComponent), "PostGameTick_Ground")]
        private static void UnitComponentPostGameTickGroundPrefix(PlanetFactory factory, ref CraftData craft, ref FleetDescState __state)
        {
            BoostFighterFleetDesc(ref craft, factory?.craftPool, ref __state);
        }

        // 地面单位 Tick 后恢复舰队范围。
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(UnitComponent), "PostGameTick_Ground")]
        private static void UnitComponentPostGameTickGroundFinalizer(FleetDescState __state)
        {
            RestoreFleetDesc(__state);
        }

        // 太空单位 Tick 前放大所属机甲舰队范围。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnitComponent), "PostGameTick_Space")]
        private static void UnitComponentPostGameTickSpacePrefix(SpaceSector sector, ref CraftData craft, ref FleetDescState __state)
        {
            BoostFighterFleetDesc(ref craft, sector?.craftPool, ref __state);
        }

        // 太空单位 Tick 后恢复舰队范围。
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(UnitComponent), "PostGameTick_Space")]
        private static void UnitComponentPostGameTickSpaceFinalizer(FleetDescState __state)
        {
            RestoreFleetDesc(__state);
        }

        // 地面激光攻击前同时放大伤害和范围，保证远距离发现后攻击行为也能成立。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_AttackLaser_Ground")]
        private static void UnitComponentAttackLaserGroundPrefix(PlanetFactory factory, ref CraftData craft, ref CombatUpgradeData combatUpgradeData, ref FighterBehaviorState __state)
        {
            BoostFighterBehavior(ref craft, factory?.craftPool, ref combatUpgradeData, ref __state);
        }

        // 地面激光攻击结束后恢复临时伤害和范围。
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_AttackLaser_Ground")]
        private static void UnitComponentAttackLaserGroundFinalizer(ref CombatUpgradeData combatUpgradeData, FighterBehaviorState __state)
        {
            RestoreFighterBehavior(ref combatUpgradeData, __state);
        }

        // 地面等离子攻击前放大战机行为参数。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_AttackPlasma_Ground")]
        private static void UnitComponentAttackPlasmaGroundPrefix(PlanetFactory factory, ref CraftData craft, ref CombatUpgradeData combatUpgradeData, ref FighterBehaviorState __state)
        {
            BoostFighterBehavior(ref craft, factory?.craftPool, ref combatUpgradeData, ref __state);
        }

        // 地面等离子攻击结束后恢复临时参数。
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_AttackPlasma_Ground")]
        private static void UnitComponentAttackPlasmaGroundFinalizer(ref CombatUpgradeData combatUpgradeData, FighterBehaviorState __state)
        {
            RestoreFighterBehavior(ref combatUpgradeData, __state);
        }

        // 地面护盾行为也走战机行为参数，放大后能覆盖更远距离的防御目标。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_DefenseShield_Ground")]
        private static void UnitComponentDefenseShieldGroundPrefix(PlanetFactory factory, ref CraftData craft, ref CombatUpgradeData combatUpgradeData, ref FighterBehaviorState __state)
        {
            BoostFighterBehavior(ref craft, factory?.craftPool, ref combatUpgradeData, ref __state);
        }

        // 地面护盾行为结束后恢复临时参数。
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_DefenseShield_Ground")]
        private static void UnitComponentDefenseShieldGroundFinalizer(ref CombatUpgradeData combatUpgradeData, FighterBehaviorState __state)
        {
            RestoreFighterBehavior(ref combatUpgradeData, __state);
        }

        // 大型太空激光攻击前放大战机伤害和范围。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_SAttackLaser_Large")]
        private static void UnitComponentSAttackLaserLargePrefix(SpaceSector sector, ref CraftData craft, ref CombatUpgradeData combatUpgradeData, ref FighterBehaviorState __state)
        {
            BoostFighterBehavior(ref craft, sector?.craftPool, ref combatUpgradeData, ref __state);
        }

        // 大型太空激光攻击结束后恢复临时参数。
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_SAttackLaser_Large")]
        private static void UnitComponentSAttackLaserLargeFinalizer(ref CombatUpgradeData combatUpgradeData, FighterBehaviorState __state)
        {
            RestoreFighterBehavior(ref combatUpgradeData, __state);
        }

        // 小型太空等离子攻击前放大战机伤害和范围。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_SAttackPlasma_Small")]
        private static void UnitComponentSAttackPlasmaSmallPrefix(SpaceSector sector, ref CraftData craft, ref CombatUpgradeData combatUpgradeData, ref FighterBehaviorState __state)
        {
            BoostFighterBehavior(ref craft, sector?.craftPool, ref combatUpgradeData, ref __state);
        }

        // 小型太空等离子攻击结束后恢复临时参数，确保共享数据不泄漏到其他单位。
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(UnitComponent), "RunBehavior_Engage_SAttackPlasma_Small")]
        private static void UnitComponentSAttackPlasmaSmallFinalizer(ref CombatUpgradeData combatUpgradeData, FighterBehaviorState __state)
        {
            RestoreFighterBehavior(ref combatUpgradeData, __state);
        }
    }
}
