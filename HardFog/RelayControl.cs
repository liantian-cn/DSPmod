using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HardFog
{
    // 中继站控制器：修改降落选址逻辑，支持预设候选落点（信标 + RuinPositions），并可调节发射频率。
    [HarmonyPatch]
    internal static class RelayControl
    {
        // 独立 Harmony ID 让中继相关补丁可以整体启停，不影响其他 HardFog 功能。
        private const string PatchGuid = "me.liantian.plugin.HardFog.RelayControl";
        // 原版约每 600 个巢穴 tick 检查一次中继需求；更快发射时替换为更短间隔。
        private const int VanillaRelayDemandInterval = 600;
        private const int FasterRelayDemandInterval = 120;
        // 降落时摧毁附近建筑的半径，和原版信标降落逻辑一致。
        private const float EraseObstacleRadius = 20.1f;
        // 候选落点与已有基地/废墟的最小间距（米）。
        private const float MinClearanceMeters = 52f;

        // LandingLogicEnabled 是总开关；MarkersOnly 只在总开关开启时有实际效果。
        internal static ConfigEntry<bool> LandingLogicEnabledConfig { get; private set; }
        internal static ConfigEntry<bool> MarkersOnlyConfig { get; private set; }
        internal static ConfigEntry<bool> FastLaunchEnabledConfig { get; private set; }

        private static ManualLogSource Log;
        private static Harmony harmony;
        // Fisher-Yates 洗牌用的随机种子，每帧独立于巢穴的 rtseed。
        private static int _shuffleSeed = (int)(System.Diagnostics.Stopwatch.GetTimestamp() & 0x7FFFFFFF);
        private static EventHandler masterSwitchChangedHandler;

        // 缓存 EraseLandingObstacles 的 MethodInfo，避免每次降落都反射查找。
        private static readonly MethodInfo EraseLandingObstaclesMethod =
            AccessTools.Method(typeof(DFRelayComponent), "EraseLandingObstacles");

        // 初始化三个中继配置；只监听总开关，因为子开关不需要决定 Harmony 是否安装。
        internal static void Init(
            ConfigEntry<bool> landingLogicEnabledConfig,
            ConfigEntry<bool> markersOnlyConfig,
            ConfigEntry<bool> fastLaunchEnabledConfig,
            ManualLogSource log)
        {
            // 防御性解绑旧总开关，避免重复初始化后 SetActive 被多次调用。
            if (LandingLogicEnabledConfig != null && masterSwitchChangedHandler != null)
            {
                LandingLogicEnabledConfig.SettingChanged -= masterSwitchChangedHandler;
            }

            // 保存配置引用；补丁运行时会读取子开关的当前值决定行为。
            Log = log;
            LandingLogicEnabledConfig = landingLogicEnabledConfig;
            MarkersOnlyConfig = markersOnlyConfig;
            FastLaunchEnabledConfig = fastLaunchEnabledConfig;
            masterSwitchChangedHandler = OnMasterSwitchChanged;
            LandingLogicEnabledConfig.SettingChanged += masterSwitchChangedHandler;
            SetActive(LandingLogicEnabledConfig.Value);
        }

        // 卸载时解绑总开关并关闭补丁，避免静态事件在插件销毁后继续回调。
        internal static void Uninit()
        {
            if (LandingLogicEnabledConfig != null && masterSwitchChangedHandler != null)
            {
                LandingLogicEnabledConfig.SettingChanged -= masterSwitchChangedHandler;
            }

            SetActive(false);
            masterSwitchChangedHandler = null;
            LandingLogicEnabledConfig = null;
            MarkersOnlyConfig = null;
            FastLaunchEnabledConfig = null;
            Log = null;
        }

        // 总开关变更入口；子开关只是改变行为细节，不需要重新 Patch。
        private static void OnMasterSwitchChanged(object sender, EventArgs args)
        {
            SetActive(LandingLogicEnabledConfig != null && LandingLogicEnabledConfig.Value);
        }

        // 根据总开关安装或卸载全部中继补丁。
        private static void SetActive(bool active)
        {
            if (active)
            {
                if (harmony != null)
                {
                    return;
                }

                harmony = Harmony.CreateAndPatchAll(typeof(RelayControl), PatchGuid);
                Log?.LogInfo("RelayControl enabled");
                return;
            }

            if (harmony == null)
            {
                return;
            }

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
            return false;
        }

        // 虚拟巢穴的物质统计里包含"多久检查一次中继需求"的常量，仅在快速发射模式时替换。
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(EnemyDFHiveSystem), "UpdateMatterStatisticsVirtual")]
        private static IEnumerable<CodeInstruction> EnemyDFHiveSystemUpdateMatterStatisticsVirtualTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceRelayDemandInterval(instructions);
        }

        // 实体化巢穴也有同样的检查间隔常量，仅在快速发射模式时替换。
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(EnemyDFHiveSystem), "UpdateMatterStatisticsRealized")]
        private static IEnumerable<CodeInstruction> EnemyDFHiveSystemUpdateMatterStatisticsRealizedTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceRelayDemandInterval(instructions);
        }

        // 拦截中继站搜索目标流程：使用预设候选落点（信标 + RuinPositions）替代原版随机采样。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DFRelayComponent), "SearchTargetPlaceProcess")]
        private static bool DFRelayComponentSearchTargetPlaceProcessPrefix(DFRelayComponent __instance, ref bool __result)
        {
            if (__instance == null)
            {
                return true;
            }

            // 如果 relay 已经有目标信标，跳过本次（保留已有的信标绑定）。
            if (__instance.dstMarkerAstroId > 0 || __instance.dstMarkerId > 0)
            {
                return true;
            }

            // 如果已经在搜索某个星球的过程中（searchAstroId != 0），让它继续。
            if (__instance.searchAstroId != 0)
            {
                return true;
            }

            EnemyDFHiveSystem hive = __instance.hive;
            StarData starData = hive?.starData;
            if (starData == null)
            {
                return true;
            }

            // 统计非气态行星，从中随机挑一个作为目标。
            int nonGasPlanetCount = CountNonGasPlanets(starData);
            if (nonGasPlanetCount <= 0)
            {
                __result = false;
                return false;
            }

            int pick = RandomTable.Integer(ref hive.rtseed, nonGasPlanetCount);
            PlanetData planet = PickNonGasPlanet(starData, pick);
            if (planet == null)
            {
                __result = false;
                return false;
            }

            PlanetFactory factory = planet.factory;

            // 构建候选落点列表：先收集信标，再追加 RuinPositions（除非仅信标模式）。
            List<Vector3> candidates = new List<Vector3>();
            CollectMarkerCandidates(hive, planet, factory, candidates);
            if (!IsMarkersOnlyEnabled())
            {
                CollectRuinPositionCandidates(planet, factory, candidates);
            }

            // 按顺序遍历候选落点，找第一个满足 AT 场 + 52m 净空要求的。
            foreach (Vector3 candidate in candidates)
            {
                // AT 场检查（仅此一项保留原版逻辑，其余忽略）。
                if (factory != null && factory.planetATField != null && !factory.planetATField.TestRelayCondition(candidate))
                {
                    continue;
                }

                if (IsCandidateClear(hive, factory, planet.astroId, candidate))
                {
                    // 候选坐标是星球表面位置；searchLPos 需要抬高到轨道高度。
                    __instance.searchAstroId = planet.astroId;
                    __instance.searchBaseId = 0;
                    __instance.searchLPos = candidate.normalized * (planet.realRadius + 70f);
                    __instance.searchChance = 0;
                    __instance.searchEntityCursor = 0;
                    __result = true;
                    return false;
                }
            }

            // 所有候选都不满足条件，放弃本次派遣。
            __instance.ResetSearchStates();
            CancelDockDispatch(__instance);
            __result = false;
            return false;
        }

        // 收集目标星球上所有可用的吸引信标坐标。
        private static void CollectMarkerCandidates(
            EnemyDFHiveSystem hive,
            PlanetData planet,
            PlanetFactory factory,
            List<Vector3> candidates)
        {
            if (factory == null || factory.digitalSystem == null)
            {
                return;
            }

            ObjectPool<MarkerComponent> markers = factory.digitalSystem.markers;
            for (int i = 1; i < markers.cursor; i++)
            {
                MarkerComponent marker = markers[i];
                if (marker == null || marker.id != i || !marker.attractsDFRelay)
                {
                    continue;
                }

                if (marker.entityId <= 0 || marker.entityId >= factory.entityPool.Length)
                {
                    continue;
                }

                // 信标可能已被其他中继锁定，跳过已锁定的。
                if (IsMarkerAlreadyTargetedByOthers(hive, planet.astroId, i))
                {
                    continue;
                }

                Vector3 entityPos = factory.entityPool[marker.entityId].pos;
                candidates.Add(entityPos);
            }
        }

        // 收集 RuinPositions 全部候选方向，随机排序后投影到目标星球表面。
        // 如果目标星球有护盾（任意覆盖率），则跳过 RuinPositions，只使用信标。
        private static void CollectRuinPositionCandidates(PlanetData planet, PlanetFactory factory, List<Vector3> candidates)
        {
            if (HasAnyShieldCoverage(factory))
            {
                return;
            }

            float surfaceRadius = planet.realRadius + 0.2f;
            Vector3[] all = RuinPositions.All;
            // Fisher-Yates 洗牌，创建随机排列的索引数组。
            int[] indices = new int[all.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }

            for (int i = indices.Length - 1; i > 0; i--)
            {
                int j = RandomTable.Integer(ref _shuffleSeed, i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            for (int i = 0; i < indices.Length; i++)
            {
                Vector3 dir = all[indices[i]].normalized;
                candidates.Add(dir * surfaceRadius);
            }
        }

        private static bool HasAnyShieldCoverage(PlanetFactory factory)
        {
            return factory != null && factory.planetATField != null && !factory.planetATField.isEmpty;
        }

        // 检查候选落点周围 52m 是否有已有地面基地或废墟（model 406），以及是否有其他中继已锁定此位置。
        private static bool IsCandidateClear(
            EnemyDFHiveSystem hive,
            PlanetFactory factory,
            int astroId,
            Vector3 candidate)
        {
            float clearanceSq = MinClearanceMeters * MinClearanceMeters;

            // 检查其他中继（含兄弟巢穴）是否已锁定附近位置。
            if (IsCandidateTargetedByOtherRelay(hive, astroId, candidate, clearanceSq))
            {
                return false;
            }

            if (factory == null)
            {
                return true;
            }

            // 检查已有地面基地。
            EnemyDFGroundSystem enemySystem = factory.enemySystem;
            if (enemySystem != null)
            {
                DFGBaseComponent[] bases = enemySystem.bases.buffer;
                int baseCursor = enemySystem.bases.cursor;
                for (int i = 1; i < baseCursor; i++)
                {
                    if (bases[i] != null && bases[i].id == i)
                    {
                        int enemyId = bases[i].enemyId;
                        if (enemyId > 0 && enemyId < factory.enemyPool.Length)
                        {
                            Vector3 basePos = factory.enemyPool[enemyId].pos;
                            if ((basePos - candidate).sqrMagnitude < clearanceSq)
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            // 检查废墟（model 406）。
            RuinData[] ruinPool = factory.ruinPool;
            int ruinCursor = factory.ruinCursor;
            for (int i = 1; i < ruinCursor; i++)
            {
                if (ruinPool[i].id == i && ruinPool[i].modelIndex == 406)
                {
                    if ((ruinPool[i].pos - candidate).sqrMagnitude < clearanceSq)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // 检查兄弟巢穴的中继是否已经锁定了同一坐标（避免多个中继抢同一个候选）。
        // 注意：targetLPos 在 realRadius+70 轨道高度，candidate 在地表（realRadius+0.2），
        // 必须归一化到同一半径再比较，否则 ~70 的海拔差会让 52m 阈值永远无法触发。
        private static bool IsCandidateTargetedByOtherRelay(
            EnemyDFHiveSystem hive,
            int astroId,
            Vector3 candidate,
            float clearanceSq)
        {
            float candidateRadius = candidate.magnitude;

            for (EnemyDFHiveSystem sibling = hive.firstSibling; sibling != null; sibling = sibling.nextSibling)
            {
                DFRelayComponent[] buffer = sibling.relays.buffer;
                int cursor = sibling.relays.cursor;
                for (int i = 1; i < cursor; i++)
                {
                    DFRelayComponent relay = buffer[i];
                    if (relay == null || relay.id != i || relay.direction <= 0)
                    {
                        continue;
                    }

                    // targetLPos：RelaySailLogic 写入的轨道高度坐标。
                    if (relay.targetAstroId == astroId && relay.targetLPos.sqrMagnitude > 0.01f)
                    {
                        Vector3 surfacePos = relay.targetLPos.normalized * candidateRadius;
                        if ((surfacePos - candidate).sqrMagnitude < clearanceSq)
                        {
                            return true;
                        }
                    }

                    // searchLPos：我们的 Prefix 在 SearchTargetPlaceProcess 内写入，
                    // 此时 targetLPos 尚未被 RelaySailLogic 设置，同一帧内的后续中继需要用 searchLPos 判断。
                    if (relay.searchAstroId == astroId && relay.searchLPos.sqrMagnitude > 0.01f)
                    {
                        Vector3 surfacePos = relay.searchLPos.normalized * candidateRadius;
                        if ((surfacePos - candidate).sqrMagnitude < clearanceSq)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        // 检查信标是否已被兄弟巢穴的中继绑定。
        private static bool IsMarkerAlreadyTargetedByOthers(EnemyDFHiveSystem hive, int astroId, int markerId)
        {
            for (EnemyDFHiveSystem sibling = hive.firstSibling; sibling != null; sibling = sibling.nextSibling)
            {
                DFRelayComponent[] buffer = sibling.relays.buffer;
                int cursor = sibling.relays.cursor;
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

        // 航行中只保留 AT 场检查，忽略原版其他检查（实体扫描、建筑间距等）。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DFRelayComponent), "CheckLandCondition")]
        private static bool DFRelayComponentCheckLandConditionPrefix(
            DFRelayComponent __instance,
            PlanetFactory factory,
            Vector3 tarpos,
            ref bool __result)
        {
            // 仅检查 AT 场：有 AT 场且该场阻挡该位置时拒绝降落，其余一律放行。
            if (__instance.dstMarkerId == 0 &&
                factory != null &&
                factory.planetATField != null &&
                !factory.planetATField.TestRelayCondition(tarpos))
            {
                __result = false;
                return false;
            }

            __result = true;
            return false;
        }

        // 中继降落成功后摧毁落点附近的建筑，和信标降落逻辑一致。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(DFRelayComponent), "ArriveBase")]
        private static void DFRelayComponentArriveBasePostfix(DFRelayComponent __instance)
        {
            if (__instance == null || __instance.targetAstroId <= 0)
            {
                return;
            }

            // 只有降落成功（baseState == REALIZED_BASE）且 direction 已归零时才清理。
            if (__instance.baseState != 2 || __instance.direction != 0)
            {
                return;
            }

            PlanetFactory planetFactory = __instance.hive?.sector?.galaxy?.astrosFactory[__instance.targetAstroId];
            if (planetFactory == null)
            {
                return;
            }

            // 调用原版 EraseLandingObstacles 清理 20.1m 半径内的建筑和预建。
            if (EraseLandingObstaclesMethod != null)
            {
                try
                {
                    EraseLandingObstaclesMethod.Invoke(__instance, new object[] { planetFactory, __instance.targetLPos, EraseObstacleRadius });
                }
                catch (Exception ex)
                {
                    Log?.LogWarning($"EraseLandingObstacles failed: {ex.Message}");
                }
            }
        }

        // 把 IL 中的原版中继需求间隔常量替换为更短值；仅在快速发射模式启用时执行替换。
        private static IEnumerable<CodeInstruction> ReplaceRelayDemandInterval(IEnumerable<CodeInstruction> instructions)
        {
            // 非快速发射模式时不替换，保持原版 600。
            if (!IsFastLaunchEnabled())
            {
                return instructions;
            }

            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            int replacements = 0;

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
                Log?.LogWarning("Failed to patch relay demand interval.");
            }

            return codes;
        }

        // 判断一条 IL 指令是否在加载指定整数。
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
            if (hive == null || hive.relays == null || hive.idleRelayCount <= 0)
            {
                return;
            }

            if (HasOutgoingRelay(hive))
            {
                return;
            }

            for (int i = 0; i < hive.idleRelayCount; i++)
            {
                int relayId = hive.idleRelayIds[i];
                if (relayId <= 0)
                {
                    continue;
                }

                DFRelayComponent relay = hive.relays.buffer[relayId];
                if (relay == null || relay.id != relayId)
                {
                    continue;
                }

                if (relay.TryDispatchFromHive())
                {
                    return;
                }
            }
        }

        // 判断巢穴是否已经有任意中继处于飞行/外派状态。
        private static bool HasOutgoingRelay(EnemyDFHiveSystem hive)
        {
            DFRelayComponent[] buffer = hive.relays.buffer;
            int cursor = hive.relays.cursor;
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

        // 取消中继本次离巢派遣，清空目标、基地和搜索状态。
        private static void CancelDockDispatch(DFRelayComponent relay)
        {
            relay.targetAstroId = 0;
            relay.targetLPos = Vector3.zero;
            relay.targetYaw = 0f;
            relay.baseState = 0;
            relay.baseId = 0;
            relay.baseTicks = 0;
            relay.baseEvolve = default(EvolveData);
            relay.baseRespawnCD = 0;
            relay.direction = 0;
            relay.param0 = 0f;
            relay.uSpeed = 0f;
            relay.ResetSearchStates();
            relay.ResetTargetedMarker();
        }

        // 判断降落选址逻辑是否启用。
        private static bool IsLandingLogicEnabled()
        {
            return LandingLogicEnabledConfig != null && LandingLogicEnabledConfig.Value;
        }

        // 判断是否仅信标模式。
        private static bool IsMarkersOnlyEnabled()
        {
            return MarkersOnlyConfig != null && MarkersOnlyConfig.Value;
        }

        // 判断是否快速发射模式。
        private static bool IsFastLaunchEnabled()
        {
            return FastLaunchEnabledConfig != null && FastLaunchEnabledConfig.Value;
        }

        // 统计恒星系内非气态行星数量。
        private static int CountNonGasPlanets(StarData starData)
        {
            int count = 0;
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

        // 按非气态行星序号返回目标星球。
        private static PlanetData PickNonGasPlanet(StarData starData, int pick)
        {
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
