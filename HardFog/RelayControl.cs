using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HardFog
{
    // 中继站控制器：加快巢穴检查中继需求的频率，并可限制中继站只飞向玩家放置的信标。
    [HarmonyPatch]
    internal static class RelayControl
    {
        // 独立 Harmony ID 让中继相关补丁可以整体启停，不影响其他 HardFog 功能。
        private const string PatchGuid = "me.liantian.plugin.HardFog.RelayControl";
        // 原版约每 600 个巢穴 tick 检查一次中继需求；这里替换为更短间隔来提高发射频率。
        private const int VanillaRelayDemandInterval = 600;
        private const int FasterRelayDemandInterval = 120;

        // FasterRelayLaunch 是主开关；SmartRelayDispatch 只在主开关开启时才有实际效果。
        internal static ConfigEntry<bool> FasterRelayLaunchEnabledConfig { get; private set; }
        internal static ConfigEntry<bool> SmartRelayDispatchEnabledConfig { get; private set; }

        private static ManualLogSource Log;
        private static Harmony harmony;
        private static EventHandler fasterRelayLaunchChangedHandler;

        // 初始化两个中继配置；只监听主开关，因为子开关不需要决定 Harmony 是否安装。
        internal static void Init(
            ConfigEntry<bool> fasterRelayLaunchEnabledConfig,
            ConfigEntry<bool> smartRelayDispatchEnabledConfig,
            ManualLogSource log)
        {
            // 防御性解绑旧主开关，避免重复初始化后 SetActive 被多次调用。
            if (FasterRelayLaunchEnabledConfig != null && fasterRelayLaunchChangedHandler != null)
            {
                FasterRelayLaunchEnabledConfig.SettingChanged -= fasterRelayLaunchChangedHandler;
            }

            // 保存配置引用；补丁运行时会读取 SmartRelayDispatch 的当前值决定是否只派往信标。
            Log = log;
            FasterRelayLaunchEnabledConfig = fasterRelayLaunchEnabledConfig;
            SmartRelayDispatchEnabledConfig = smartRelayDispatchEnabledConfig;
            fasterRelayLaunchChangedHandler = OnFasterRelayLaunchChanged;
            FasterRelayLaunchEnabledConfig.SettingChanged += fasterRelayLaunchChangedHandler;
            SetActive(FasterRelayLaunchEnabledConfig.Value);
        }

        // 卸载时解绑主开关并关闭补丁，避免静态事件在插件销毁后继续回调。
        internal static void Uninit()
        {
            // 先解绑事件，再清空配置引用，防止旧 ConfigEntry 继续引用本类。
            if (FasterRelayLaunchEnabledConfig != null && fasterRelayLaunchChangedHandler != null)
            {
                FasterRelayLaunchEnabledConfig.SettingChanged -= fasterRelayLaunchChangedHandler;
            }

            SetActive(false);
            fasterRelayLaunchChangedHandler = null;
            FasterRelayLaunchEnabledConfig = null;
            SmartRelayDispatchEnabledConfig = null;
            Log = null;
        }

        // 主开关变更入口；子开关只是改变派遣策略，不需要重新 Patch。
        private static void OnFasterRelayLaunchChanged(object sender, EventArgs args)
        {
            SetActive(FasterRelayLaunchEnabledConfig != null && FasterRelayLaunchEnabledConfig.Value);
        }

        // 根据主开关安装或卸载全部中继补丁。
        private static void SetActive(bool active)
        {
            if (active)
            {
                // 已经安装过就不重复安装，避免 Prefix 和 Transpiler 叠加。
                if (harmony != null)
                {
                    return;
                }

                // 安装后会替换原版需求判定和部分常量，让巢穴更频繁地尝试派中继。
                harmony = Harmony.CreateAndPatchAll(typeof(RelayControl), PatchGuid);
                Log?.LogInfo("RelayControl enabled");
                return;
            }

            // 未安装时关闭没有副作用，直接返回。
            if (harmony == null)
            {
                return;
            }

            // 卸载后原版 DetermineRelayDemand 和检查间隔恢复，已飞出的中继不会被强制改变。
            harmony.UnpatchSelf();
            harmony = null;
            Log?.LogInfo("RelayControl disabled");
        }

        // 完全替代原版中继需求判定：每次只允许派出一个空闲中继，避免短时间批量发射。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(EnemyDFHiveSystem), "DetermineRelayDemand")]
        private static bool EnemyDFHiveSystemDetermineRelayDemandPrefix(EnemyDFHiveSystem __instance)
        {
            DispatchOneIdleRelayIfAllowed(__instance);
            // 返回 false 表示不执行原函数，因为本模块已经自己决定是否派遣。
            return false;
        }

        // 虚拟巢穴的物质统计里包含“多久检查一次中继需求”的常量，用 Transpiler 改成更短间隔。
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(EnemyDFHiveSystem), "UpdateMatterStatisticsVirtual")]
        private static IEnumerable<CodeInstruction> EnemyDFHiveSystemUpdateMatterStatisticsVirtualTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceRelayDemandInterval(instructions);
        }

        // 实体化巢穴也有同样的检查间隔常量，必须同步替换，否则不同加载状态下发射频率不一致。
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(EnemyDFHiveSystem), "UpdateMatterStatisticsRealized")]
        private static IEnumerable<CodeInstruction> EnemyDFHiveSystemUpdateMatterStatisticsRealizedTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceRelayDemandInterval(instructions);
        }

        // 拦截中继站搜索目标流程：普通模式限制到非气态行星，信标模式只允许飞向有效信标。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DFRelayComponent), "SearchTargetPlaceProcess")]
        private static bool DFRelayComponentSearchTargetPlaceProcessPrefix(DFRelayComponent __instance, ref bool __result)
        {
            // 实例为空时让原版处理，避免把异常状态吞掉。
            if (__instance == null)
            {
                return true;
            }

            // 信标模式下，中继若已经有有效目标信标，就继续原版搜索；信标失效则取消本次派遣。
            if (IsMarkerOnlyEnabled())
            {
                if (IsRelayMarkerStillValid(__instance))
                {
                    return true;
                }

                CancelDockDispatch(__instance);
                __result = false;
                // 返回 false 阻止原版自动找普通落点，保证“只飞向信标”的语义。
                return false;
            }

            // 已经有目标信标或正在搜索目标时不干预，避免覆盖原版/其他逻辑已经设置好的目标。
            if (__instance.dstMarkerAstroId > 0 ||
                __instance.dstMarkerId > 0 ||
                __instance.searchAstroId != 0)
            {
                return true;
            }

            EnemyDFHiveSystem hive = __instance.hive;
            StarData starData = hive?.starData;
            // 没有巢穴或恒星数据时无法安全随机行星，交回原版处理。
            if (starData == null)
            {
                return true;
            }

            // 中继站不能落在气态巨星上，所以随机池只计算非气态行星。
            int nonGasPlanetCount = CountNonGasPlanets(starData);
            if (nonGasPlanetCount <= 0)
            {
                return true;
            }

            // 使用巢穴自己的随机种子，保持与游戏暗雾系统一致的可复现随机流。
            int pick = RandomTable.Integer(ref hive.rtseed, nonGasPlanetCount);
            PlanetData planet = PickNonGasPlanet(starData, pick);
            if (planet == null)
            {
                return true;
            }

            // 只指定搜索星球，不直接放置基地；后续仍由原版搜索流程挑具体落点。
            __instance.searchAstroId = planet.astroId;
            __instance.searchBaseId = 0;
            __instance.searchChance = 5;
            __instance.searchLPos = Vector3.zero;
            __instance.searchEntityCursor = 0;
            __result = false;
            // 返回 false 表示本轮搜索已经设置好目标星球，不让原版再覆盖成别的随机策略。
            return false;
        }

        // 把 IL 中的原版中继需求间隔常量替换为更短值；比重写整段函数更不容易受游戏更新影响。
        private static IEnumerable<CodeInstruction> ReplaceRelayDemandInterval(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            int replacements = 0;

            // 查找所有加载 600 的指令并改成加载 120，兼容不同 ldc.i4 编码形式。
            for (int i = 0; i < codes.Count; i++)
            {
                if (!LoadsInt(codes[i], VanillaRelayDemandInterval))
                {
                    continue;
                }

                codes[i].opcode = OpCodes.Ldc_I4;
                codes[i].operand = FasterRelayDemandInterval;
                replacements++;
            }

            if (replacements == 0)
            {
                // 如果游戏更新后常量不再存在，补丁可能失效，日志能提醒维护者检查 IL。
                Log?.LogWarning("Failed to patch relay demand interval.");
            }

            return codes;
        }

        // 判断一条 IL 指令是否在加载指定整数；Transpiler 需要兼容 C# 编译器使用的多种短指令。
        private static bool LoadsInt(CodeInstruction code, int value)
        {
            if (code.opcode == OpCodes.Ldc_I4 && code.operand is int intValue)
            {
                return intValue == value;
            }

            if (code.opcode == OpCodes.Ldc_I4_S && code.operand is sbyte sbyteValue)
            {
                return sbyteValue == value;
            }

            if (code.opcode == OpCodes.Ldc_I4_0)
            {
                return value == 0;
            }

            if (code.opcode == OpCodes.Ldc_I4_1)
            {
                return value == 1;
            }

            if (code.opcode == OpCodes.Ldc_I4_2)
            {
                return value == 2;
            }

            if (code.opcode == OpCodes.Ldc_I4_3)
            {
                return value == 3;
            }

            if (code.opcode == OpCodes.Ldc_I4_4)
            {
                return value == 4;
            }

            if (code.opcode == OpCodes.Ldc_I4_5)
            {
                return value == 5;
            }

            if (code.opcode == OpCodes.Ldc_I4_6)
            {
                return value == 6;
            }

            if (code.opcode == OpCodes.Ldc_I4_7)
            {
                return value == 7;
            }

            if (code.opcode == OpCodes.Ldc_I4_8)
            {
                return value == 8;
            }

            return false;
        }

        // 在一个巢穴中挑一个空闲中继发射；限制一次只派一个，避免短时间刷出过多地面基地。
        private static void DispatchOneIdleRelayIfAllowed(EnemyDFHiveSystem hive)
        {
            // 没有巢穴、中继池或空闲中继时直接退出。
            if (hive == null || hive.relays == null || hive.idleRelayCount <= 0)
            {
                return;
            }

            // 如果已经有中继在路上，就不再派新的，控制发射节奏并减少重复目标。
            if (HasOutgoingRelay(hive))
            {
                return;
            }

            // 信标模式下必须先找到可用信标，再让中继飞向它。
            if (IsMarkerOnlyEnabled())
            {
                DispatchOneIdleRelayToMarkerIfAllowed(hive);
                return;
            }

            // 普通加速模式按空闲列表顺序尝试派出第一个可用中继。
            for (int i = 0; i < hive.idleRelayCount; i++)
            {
                int relayId = hive.idleRelayIds[i];
                // 空闲列表可能有空槽或旧数据，必须校验 id。
                if (relayId <= 0)
                {
                    continue;
                }

                DFRelayComponent relay = hive.relays.buffer[relayId];
                // 对象池条目 id 必须匹配索引，否则说明槽位已被释放或复用。
                if (relay == null || relay.id != relayId)
                {
                    continue;
                }

                // TryDispatchFromHive 会设置中继出发状态；成功后立即停止，保持“一次一个”。
                if (relay.TryDispatchFromHive())
                {
                    return;
                }
            }
        }

        // 信标模式下派出一个空闲中继，并把它绑定到随机选中的可用信标。
        private static void DispatchOneIdleRelayToMarkerIfAllowed(EnemyDFHiveSystem hive)
        {
            PlanetFactory markerFactory;
            int markerId;
            // 没有可用信标时不派遣，这样中继不会自动寻找普通落点。
            if (!TryPickAvailableRelayMarker(hive, out markerFactory, out markerId))
            {
                return;
            }

            // 找到信标后，再从空闲中继列表里挑一个实际能起飞的中继。
            for (int i = 0; i < hive.idleRelayCount; i++)
            {
                int relayId = hive.idleRelayIds[i];
                // 跳过无效空闲 id。
                if (relayId <= 0)
                {
                    continue;
                }

                DFRelayComponent relay = hive.relays.buffer[relayId];
                // 对象池校验，避免对已释放或错位的中继组件写状态。
                if (relay == null || relay.id != relayId)
                {
                    continue;
                }

                // 起飞失败说明这个中继虽然在空闲列表里，但当前状态不能派遣，继续尝试下一个。
                if (!relay.TryDispatchFromHive())
                {
                    continue;
                }

                // 起飞后写入目标信标；若信标在这之间失效，立刻取消派遣防止中继飞向无效位置。
                relay.SetTargetedMarker(markerFactory, markerId);
                if (!IsRelayMarkerStillValid(relay))
                {
                    CancelDockDispatch(relay);
                }

                return;
            }
        }

        // 从本恒星系所有可用信标中随机挑一个；用 reservoir 风格抽样避免额外保存候选列表。
        private static bool TryPickAvailableRelayMarker(EnemyDFHiveSystem hive, out PlanetFactory factory, out int markerId)
        {
            factory = null;
            markerId = 0;

            // 先计数，后面按 remaining 做均匀抽样；没有候选就不派遣。
            int candidateCount = CountAvailableRelayMarkers(hive);
            if (candidateCount <= 0)
            {
                return false;
            }

            int remaining = candidateCount;
            StarData starData = hive.starData;
            // 只扫描同一恒星系的行星工厂，避免中继站跨恒星系寻找信标。
            for (int i = 0; i < starData.planetCount; i++)
            {
                PlanetData planet = starData.planets[i];
                PlanetFactory planetFactory = GetMarkerPlanetFactory(hive, planet);
                // 没有加载工厂或不适合放中继的行星不能提供候选信标。
                if (planetFactory == null)
                {
                    continue;
                }

                ObjectPool<MarkerComponent> markers = planetFactory.digitalSystem.markers;
                // Marker 对象池从 1 开始使用，0 通常是空哨兵。
                for (int j = 1; j < markers.cursor; j++)
                {
                    MarkerComponent marker = markers[j];
                    // 只考虑仍存在、开启吸引中继、且没有被其他中继锁定的信标。
                    if (!IsAvailableRelayMarker(hive, planetFactory, marker, j))
                    {
                        continue;
                    }

                    // 按剩余候选数抽样，确保每个可用信标有相同概率被选中。
                    float chance = marker.power / (float)remaining;
                    if (UnityEngine.Random.value < chance)
                    {
                        factory = planetFactory;
                        markerId = j;
                        return true;
                    }

                    // 当前候选没有被选中，剩余候选数减一。
                    remaining--;
                }
            }

            return false;
        }

        // 统计当前恒星系内能吸引中继且尚未被锁定的信标数量。
        private static int CountAvailableRelayMarkers(EnemyDFHiveSystem hive)
        {
            int count = 0;
            StarData starData = hive.starData;
            // 中继只能派到同一恒星系的非气态行星工厂，所以逐星球过滤。
            for (int i = 0; i < starData.planetCount; i++)
            {
                PlanetData planet = starData.planets[i];
                PlanetFactory planetFactory = GetMarkerPlanetFactory(hive, planet);
                if (planetFactory == null)
                {
                    continue;
                }

                ObjectPool<MarkerComponent> markers = planetFactory.digitalSystem.markers;
                // 逐个检查对象池中的有效信标。
                for (int j = 1; j < markers.cursor; j++)
                {
                    if (IsAvailableRelayMarker(hive, planetFactory, markers[j], j))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        // 获取可作为信标来源的行星工厂；过滤气态行星、跨恒星系、未加载工厂和无数字系统的情况。
        private static PlanetFactory GetMarkerPlanetFactory(EnemyDFHiveSystem hive, PlanetData planet)
        {
            if (hive == null ||
                planet == null ||
                planet.type == EPlanetType.Gas ||
                planet.star != hive.starData ||
                planet.factory == null ||
                planet.factory.entityCount == 0 ||
                planet.factory.digitalSystem == null)
            {
                return null;
            }

            return planet.factory;
        }

        // 判断一个信标是否能被当前巢穴派出的中继使用。
        private static bool IsAvailableRelayMarker(
            EnemyDFHiveSystem hive,
            PlanetFactory factory,
            MarkerComponent marker,
            int markerId)
        {
            // id 匹配保证对象池槽位有效；attractsDFRelay 是玩家开启“吸引中继”的信标标志。
            return marker != null &&
                marker.id == markerId &&
                marker.attractsDFRelay &&
                !IsMarkerAlreadyTargeted(hive, factory.planet.astroId, markerId);
        }

        // 检查本恒星系所有兄弟巢穴的中继，避免多个中继同时锁定同一个信标。
        private static bool IsMarkerAlreadyTargeted(EnemyDFHiveSystem hive, int astroId, int markerId)
        {
            // firstSibling/nextSibling 构成同恒星系巢穴链表，需要全链扫描才能避免跨巢穴重复。
            for (EnemyDFHiveSystem sibling = hive.firstSibling; sibling != null; sibling = sibling.nextSibling)
            {
                DFRelayComponent[] buffer = sibling.relays.buffer;
                int cursor = sibling.relays.cursor;
                // 中继对象池同样从 1 开始遍历，校验 id 确认槽位有效。
                for (int i = 1; i < cursor; i++)
                {
                    DFRelayComponent relay = buffer[i];
                    if (relay != null &&
                        relay.id == i &&
                        relay.direction > 0 &&
                        relay.dstMarkerAstroId == astroId &&
                        relay.dstMarkerId == markerId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // 判断巢穴是否已经有任意中继处于飞行/外派状态；用来限制每次只保持一个外派中继。
        private static bool HasOutgoingRelay(EnemyDFHiveSystem hive)
        {
            DFRelayComponent[] buffer = hive.relays.buffer;
            int cursor = hive.relays.cursor;
            // direction > 0 表示中继已经离开巢穴或正在执行外派流程。
            for (int i = 1; i < cursor; i++)
            {
                DFRelayComponent relay = buffer[i];
                if (relay != null && relay.id == i && relay.direction > 0)
                {
                    return true;
                }
            }

            return false;
        }

        // 校验中继当前绑定的目标信标是否仍然存在且位置一致；信标模式依赖它防止自动转普通落点。
        private static bool IsRelayMarkerStillValid(DFRelayComponent relay)
        {
            // 没有目标 astro/id 或没有所属巢穴，就无法确认信标有效。
            if (relay.dstMarkerAstroId <= 0 || relay.dstMarkerId <= 0 || relay.hive == null)
            {
                return false;
            }

            PlanetFactory factory = relay.hive.sector.galaxy.astrosFactory[relay.dstMarkerAstroId];
            // 目标必须是同恒星系、非气态、已加载数字系统的工厂，并且 markerId 在对象池范围内。
            if (factory == null ||
                factory.planet.type == EPlanetType.Gas ||
                factory.planet.star != relay.hive.starData ||
                factory.digitalSystem == null ||
                relay.dstMarkerId >= factory.digitalSystem.markers.cursor)
            {
                return false;
            }

            MarkerComponent marker = factory.digitalSystem.markers[relay.dstMarkerId];
            // 信标槽位必须仍然是同一个 marker，并且仍开启吸引中继。
            if (marker == null || marker.id != relay.dstMarkerId || !marker.attractsDFRelay)
            {
                return false;
            }

            // 目标位置不能在星球内部；这是防止信标位置缓存异常导致中继落点无效。
            if (relay.dstMarkerLPos.sqrMagnitude < (factory.planet.realRadius - 2f) * (factory.planet.realRadius - 2f))
            {
                return false;
            }

            // 信标必须仍绑定到有效实体，否则它可能已经被拆除或对象池复用。
            if (marker.entityId <= 0 || marker.entityId >= factory.entityPool.Length)
            {
                return false;
            }

            // 目标本地坐标要和实体当前位置足够接近，确保中继飞向的是最新信标而不是旧缓存。
            return (factory.entityPool[marker.entityId].pos - relay.dstMarkerLPos).sqrMagnitude <= 0.25f;
        }

        // 取消中继本次离巢派遣，清空目标、基地和搜索状态，让它回到可重新派遣的干净状态。
        private static void CancelDockDispatch(DFRelayComponent relay)
        {
            // 清空落点目标，防止后续逻辑继续朝无效星球或信标移动。
            relay.targetAstroId = 0;
            relay.targetLPos = Vector3.zero;
            relay.targetYaw = 0f;
            // 清空即将建立的地面基地信息，避免残留 baseId/baseState 让游戏认为它正在建站。
            relay.baseState = 0;
            relay.baseId = 0;
            relay.baseTicks = 0;
            relay.baseEvolve = default(EvolveData);
            relay.baseRespawnCD = 0;
            // direction 和速度归零表示中继不再处于外派运动状态。
            relay.direction = 0;
            relay.param0 = 0f;
            relay.uSpeed = 0f;
            // 同时清掉搜索和信标状态，否则下一次派遣可能继承旧目标。
            relay.ResetSearchStates();
            relay.ResetTargetedMarker();
        }

        // 判断当前是否处于“更快发射 + 只飞向信标”的组合模式。
        private static bool IsMarkerOnlyEnabled()
        {
            return FasterRelayLaunchEnabledConfig != null &&
                FasterRelayLaunchEnabledConfig.Value &&
                SmartRelayDispatchEnabledConfig != null &&
                SmartRelayDispatchEnabledConfig.Value;
        }

        // 统计恒星系内非气态行星数量，供普通模式随机选择中继搜索目标。
        private static int CountNonGasPlanets(StarData starData)
        {
            int count = 0;
            // 气态行星不能建设地面暗雾基地，所以不计入候选。
            for (int i = 0; i < starData.planetCount; i++)
            {
                PlanetData planet = starData.planets[i];
                if (planet != null && planet.type != EPlanetType.Gas)
                {
                    count++;
                }
            }

            return count;
        }

        // 按非气态行星序号返回目标星球；pick 来自 CountNonGasPlanets 范围内的随机数。
        private static PlanetData PickNonGasPlanet(StarData starData, int pick)
        {
            // 只在非气态行星上递减 pick，保证随机分布和计数函数一致。
            for (int i = 0; i < starData.planetCount; i++)
            {
                PlanetData planet = starData.planets[i];
                if (planet == null || planet.type == EPlanetType.Gas)
                {
                    continue;
                }

                if (pick == 0)
                {
                    return planet;
                }

                pick--;
            }

            return null;
        }
    }
}
