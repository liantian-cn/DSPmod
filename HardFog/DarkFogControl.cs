using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;

namespace HardFog
{
    // 暗雾即时操作控制器：提供按钮触发的地面基地清理、太空巢穴清理和全银河补巢穴功能。
    internal static class DarkFogControl
    {
        // HardFogWindow 注入日志；本类没有配置开关，只在按钮操作时使用。
        internal static ManualLogSource Log;

        // UI 按钮入口：保存当前游戏后清理当前星球的地面暗雾基地。
        internal static void ClearCurrentPlanetDarkFog()
        {
            // 这是会大规模删除敌人/基地的存档操作，先自动备份当前游戏。
            SaveCurrentGame();

            // 没有玩家说明不在有效游戏场景，不能安全操作当前星球。
            if (GameMain.mainPlayer == null)
            {
                return;
            }

            PlanetData planet = GameMain.localPlanet;
            // 当前不在星球表面时没有可清理的本地地面基地。
            if (planet == null)
            {
                return;
            }

            ClearPlanetDarkFog(planet);
        }

        // 清理指定星球上的所有地面暗雾对象，并同步处理中继、废墟、渲染和提示残留。
        private static void ClearPlanetDarkFog(PlanetData planet)
        {
            LogInfo("planet.name: " + planet.name);
            LogInfo("astroId: " + planet.astroId);

            // 确保星球工厂存在；敌人系统和基地对象池是后续所有删除操作的入口。
            PlanetFactory planetFactory = GameMain.data.GetOrCreateFactory(planet);
            if (planetFactory?.enemySystem?.bases == null)
            {
                return;
            }

            ObjectPool<DFGBaseComponent> bases = planetFactory.enemySystem.bases;
            // 倒序收集 baseId，后续删除对象池元素时更不容易受到 cursor/索引变化影响。
            List<int> baseIds = CollectGroundBaseIdsDescending(bases);
            if (baseIds.Count <= 0)
            {
                // 没有基地也清一下袭击提示，避免 UI 保留旧红点。
                ClearDarkFogAssaultTips();
                return;
            }

            HashSet<int> baseIdSet = new HashSet<int>(baseIds);
            // 先杀单位，再清阵型、建筑、核心，最后解绑基地组件和渲染；这个顺序减少对象池悬挂引用。
            List<EnemyModelRef> removedModels = new List<EnemyModelRef>();
            ClearGroundUnits(planetFactory, baseIdSet, removedModels);
            ClearGroundBaseFormations(planetFactory, baseIds);
            ClearGroundNonCoreBuildings(planetFactory, baseIds, removedModels);
            ClearGroundBaseCores(planetFactory, baseIds, removedModels);
            ReturnRelaysForGroundBases(planet, baseIdSet);
            UnlinkGroundBaseRuins(planetFactory, bases, baseIds);
            ClearLocalDarkFogRenderResidue(planet, planetFactory, baseIds, removedModels);
            RefreshGroundDefenseSearch(planetFactory);
            ClearDarkFogAssaultTips();
        }

        // 记录被删除敌人的 GPUI 模型引用；KillEnemyFinally 不总能立即清干净本地渲染缓存。
        private struct EnemyModelRef
        {
            public int ModelIndex;

            public int ModelId;
        }

        // 倒序收集当前工厂中所有有效地面基地 id。
        private static List<int> CollectGroundBaseIdsDescending(ObjectPool<DFGBaseComponent> bases)
        {
            List<int> baseIds = new List<int>();
            // 没有对象池时返回空列表，让调用方按“无基地”处理。
            if (bases?.buffer == null)
            {
                return baseIds;
            }

            // DFGBaseComponent 对象池从 1 开始，id 必须匹配索引才是有效槽位。
            for (int i = bases.cursor - 1; i > 0; i--)
            {
                DFGBaseComponent baseComponent = bases.buffer[i];
                if (baseComponent != null && baseComponent.id == i)
                {
                    baseIds.Add(i);
                }
            }

            return baseIds;
        }

        // 清理所有属于目标基地的移动单位。
        private static void ClearGroundUnits(PlanetFactory planetFactory, HashSet<int> baseIds, List<EnemyModelRef> removedModels)
        {
            // 收集阶段和删除阶段分开，避免遍历对象池时同时删除造成索引状态变化。
            foreach (int enemyId in CollectGroundUnitEnemyIdsDescending(planetFactory, baseIds))
            {
                KillGroundEnemyFinally(planetFactory, enemyId, "unit", removedModels);
            }
        }

        // 清理目标基地的非核心建筑；多轮执行是为了处理删除后才暴露出的构建器/依赖状态。
        private static void ClearGroundNonCoreBuildings(PlanetFactory planetFactory, List<int> baseIds, List<EnemyModelRef> removedModels)
        {
            const int maxPasses = 8;
            // 某些暗雾建筑删除会延迟更新 enemySystem，所以用多轮加 deferred change 逐步清干净。
            for (int pass = 0; pass < maxPasses; pass++)
            {
                int killedCount = 0;
                foreach (int baseId in baseIds)
                {
                    // 每一轮重新收集，避免使用上一轮已经失效的 enemyId。
                    foreach (int enemyId in CollectGroundNonCoreBuildingEnemyIdsDescending(planetFactory, baseId))
                    {
                        if (KillGroundEnemyFinally(planetFactory, enemyId, "building", removedModels))
                        {
                            killedCount++;
                        }
                    }
                }

                // 执行游戏自己的延迟敌人变更队列，让对象池状态在下一轮收集前稳定下来。
                ExecuteDeferredGroundEnemyChanges(planetFactory);
                if (killedCount == 0)
                {
                    // 本轮没有删除任何建筑，说明非核心建筑已经清完。
                    return;
                }
            }
        }

        // 清理每个地面基地的核心敌人；核心最后删，避免先删核心导致建筑/阵型引用异常。
        private static void ClearGroundBaseCores(PlanetFactory planetFactory, List<int> baseIds, List<EnemyModelRef> removedModels)
        {
            foreach (int baseId in baseIds)
            {
                DFGBaseComponent baseComponent = GetGroundBase(planetFactory, baseId);
                int enemyId = baseComponent?.enemyId ?? 0;
                // 只有确认 enemyId 是该基地核心才调用 KillEnemyFinally。
                if (IsGroundCoreEnemy(planetFactory, enemyId, baseId))
                {
                    KillGroundEnemyFinally(planetFactory, enemyId, "core", removedModels);
                }
            }
        }

        // 收集目标基地的地面移动单位 enemyId，并补充 enemyPool 中可能未在 units 池里登记的动态敌人。
        private static List<int> CollectGroundUnitEnemyIdsDescending(PlanetFactory planetFactory, HashSet<int> baseIds)
        {
            List<int> enemyIds = new List<int>();
            HashSet<int> seenEnemyIds = new HashSet<int>();
            DataPool<EnemyUnitComponent> units = planetFactory?.enemySystem?.units;
            // 缺少 enemyPool 或没有目标基地时没有可杀敌人。
            if (planetFactory?.enemyPool == null || baseIds == null || baseIds.Count <= 0)
            {
                return enemyIds;
            }

            // 优先从 EnemyUnitComponent 池收集，因为它明确记录单位所属 baseId。
            if (units?.buffer != null)
            {
                for (int i = units.cursor - 1; i > 0; i--)
                {
                    ref EnemyUnitComponent unit = ref units.buffer[i];
                    // id 匹配说明单位槽有效；再确认 enemyData 仍是该基地的动态地面单位。
                    if (unit.id == i && baseIds.Contains(unit.baseId) && IsGroundUnitEnemy(planetFactory, unit.enemyId, unit.baseId))
                    {
                        enemyIds.Add(unit.enemyId);
                        seenEnemyIds.Add(unit.enemyId);
                    }
                }
            }

            // 再从 enemyPool 兜底收集动态地面敌人，覆盖 units 池缺登记或状态不同步的情况。
            for (int i = planetFactory.enemyCursor - 1; i > 0; i--)
            {
                ref EnemyData enemyData = ref planetFactory.enemyPool[i];
                if (enemyData.id == i && enemyData.dynamic && !enemyData.isSpace && baseIds.Contains(enemyData.owner) && !seenEnemyIds.Contains(i))
                {
                    enemyIds.Add(i);
                }
            }

            return enemyIds;
        }

        // 收集指定基地的非核心建筑 enemyId，并补充 enemyPool 中未被 pbuilders 覆盖的建筑。
        private static List<int> CollectGroundNonCoreBuildingEnemyIdsDescending(PlanetFactory planetFactory, int baseId)
        {
            List<int> enemyIds = new List<int>();
            HashSet<int> seenEnemyIds = new HashSet<int>();
            DFGBaseComponent baseComponent = GetGroundBase(planetFactory, baseId);
            GrowthPattern_DFGround.Builder[] pbuilders = baseComponent?.pbuilders;

            // pbuilders 里记录基地建筑槽；从高索引倒序删除能减少数组状态变化影响。
            if (pbuilders != null)
            {
                for (int i = pbuilders.Length - 1; i > 1; i--)
                {
                    int enemyId = pbuilders[i].instId;
                    if (IsGroundNonCoreBuildingEnemy(planetFactory, enemyId, baseId))
                    {
                        enemyIds.Add(enemyId);
                        seenEnemyIds.Add(enemyId);
                    }
                }
            }

            // 没有 enemyPool 时只能返回 pbuilders 收集结果。
            if (planetFactory?.enemyPool == null)
            {
                return enemyIds;
            }

            // 兜底扫描静态地面敌人；dfGBaseId != baseId 排除基地核心。
            for (int i = planetFactory.enemyCursor - 1; i > 0; i--)
            {
                ref EnemyData enemyData = ref planetFactory.enemyPool[i];
                if (enemyData.id == i
                    && !enemyData.dynamic
                    && !enemyData.isSpace
                    && enemyData.owner == baseId
                    && enemyData.dfGBaseId != baseId
                    && !seenEnemyIds.Contains(i))
                {
                    enemyIds.Add(i);
                }
            }

            return enemyIds;
        }

        // 判断 enemyId 是否是指定基地的动态地面单位。
        private static bool IsGroundUnitEnemy(PlanetFactory planetFactory, int enemyId, int baseId)
        {
            // 先做通用有效性检查，避免 ref 访问 enemyPool 越界或读到已释放槽位。
            if (!IsLiveGroundEnemy(planetFactory, enemyId))
            {
                return false;
            }

            ref EnemyData enemyData = ref planetFactory.enemyPool[enemyId];
            // dynamic 表示移动单位，owner 对应所属地面基地。
            return enemyData.dynamic && enemyData.owner == baseId;
        }

        // 判断 enemyId 是否是指定基地的非核心静态建筑。
        private static bool IsGroundNonCoreBuildingEnemy(PlanetFactory planetFactory, int enemyId, int baseId)
        {
            if (!IsLiveGroundEnemy(planetFactory, enemyId))
            {
                return false;
            }

            ref EnemyData enemyData = ref planetFactory.enemyPool[enemyId];
            // 非动态是建筑；dfGBaseId == baseId 的是基地核心，所以这里排除核心。
            return !enemyData.dynamic && enemyData.owner == baseId && enemyData.dfGBaseId != baseId;
        }

        // 判断 enemyId 是否是指定基地的核心建筑。
        private static bool IsGroundCoreEnemy(PlanetFactory planetFactory, int enemyId, int baseId)
        {
            if (!IsLiveGroundEnemy(planetFactory, enemyId))
            {
                return false;
            }

            ref EnemyData enemyData = ref planetFactory.enemyPool[enemyId];
            // 核心是静态敌人，并用 dfGBaseId 绑定回自己的基地 id。
            return !enemyData.dynamic && enemyData.dfGBaseId == baseId;
        }

        // 调用游戏最终删除敌人的 API，并记录模型信息供本地渲染残留清理。
        private static bool KillGroundEnemyFinally(PlanetFactory planetFactory, int enemyId, string kind, List<EnemyModelRef> removedModels)
        {
            // 删除前再次确认敌人仍然有效，因为前一轮删除可能已经清掉同一个 id。
            if (!IsLiveGroundEnemy(planetFactory, enemyId))
            {
                return false;
            }

            try
            {
                // KillEnemyFinally 后 enemyData 可能清空，所以先记住 GPUI 模型 id。
                RememberEnemyModel(planetFactory, enemyId, removedModels);
                LogInfo("KillEnemyFinally " + kind + " -> enemyData.id: " + enemyId);
                // 使用 CombatStat.empty 表示不是由真实战斗造成的击杀，只要求游戏执行最终移除流程。
                planetFactory.KillEnemyFinally(enemyId, ref CombatStat.empty);
                return true;
            }
            catch (Exception ex)
            {
                // 单个敌人删除失败不应该中断整个清理按钮，记录日志后继续处理其他对象。
                LogInfo("error to kill ground " + kind + " " + enemyId + ": " + ex.Message);
                return false;
            }
        }

        // 通用地面敌人有效性检查，保护所有 enemyPool ref 访问。
        private static bool IsLiveGroundEnemy(PlanetFactory planetFactory, int enemyId)
        {
            return planetFactory?.enemyPool != null
                && enemyId > 0
                && enemyId < planetFactory.enemyCursor
                && enemyId < planetFactory.enemyPool.Length
                && planetFactory.enemyPool[enemyId].id == enemyId
                && !planetFactory.enemyPool[enemyId].isSpace;
        }

        // 保存敌人的模型索引和模型 id，后续可以从本地 GPUI 渲染器中主动移除。
        private static void RememberEnemyModel(PlanetFactory planetFactory, int enemyId, List<EnemyModelRef> removedModels)
        {
            // 没有收集列表或敌人已经无效时不记录。
            if (removedModels == null || !IsLiveGroundEnemy(planetFactory, enemyId))
            {
                return;
            }

            ref EnemyData enemyData = ref planetFactory.enemyPool[enemyId];
            // modelId <= 0 表示没有本地模型或模型尚未创建。
            if (enemyData.modelId <= 0)
            {
                return;
            }

            // 记录值类型快照，避免后续 enemyData 被清空后丢失模型引用。
            removedModels.Add(new EnemyModelRef
            {
                ModelIndex = enemyData.modelIndex,
                ModelId = enemyData.modelId
            });
        }

        // 清空目标基地的阵型对象；阵型里可能还持有已经被删除的单位引用。
        private static void ClearGroundBaseFormations(PlanetFactory planetFactory, List<int> baseIds)
        {
            foreach (int baseId in baseIds)
            {
                DFGBaseComponent baseComponent = GetGroundBase(planetFactory, baseId);
                // 基地或阵型数组不存在时跳过。
                if (baseComponent == null || baseComponent.forms == null)
                {
                    continue;
                }

                // 倒序清理阵型，和其他对象池删除顺序保持一致。
                for (int i = baseComponent.forms.Length - 1; i >= 0; i--)
                {
                    EnemyFormation formation = baseComponent.forms[i];
                    // 空阵型无需处理。
                    if (formation == null || formation.unitCount <= 0)
                    {
                        continue;
                    }

                    try
                    {
                        LogInfo("clear DFGBaseComponent:" + baseComponent.id + " -> form count: " + formation.unitCount);
                        // Clear 会重置阵型内部单位列表，避免后续 AI/渲染访问已删除单位。
                        formation.Clear();
                    }
                    catch (Exception ex)
                    {
                        LogInfo("error to clear form " + i + " for base " + baseId + ": " + ex.Message);
                    }
                }
            }
        }

        // 按 id 从基地对象池取有效 DFGBaseComponent。
        private static DFGBaseComponent GetGroundBase(PlanetFactory planetFactory, int baseId)
        {
            ObjectPool<DFGBaseComponent> bases = planetFactory?.enemySystem?.bases;
            // 对象池范围检查，避免 baseId 越界。
            if (baseId <= 0 || bases?.buffer == null || baseId >= bases.cursor)
            {
                return null;
            }

            DFGBaseComponent baseComponent = bases.buffer[baseId];
            // id 必须和索引匹配才算当前有效组件。
            return baseComponent != null && baseComponent.id == baseId ? baseComponent : null;
        }

        // 执行敌人系统延迟变更队列，让前面 KillEnemyFinally 排入的修改落地。
        private static void ExecuteDeferredGroundEnemyChanges(PlanetFactory planetFactory)
        {
            try
            {
                planetFactory?.enemySystem?.ExecuteDeferredEnemyChange();
            }
            catch (Exception ex)
            {
                LogInfo("error to execute deferred ground enemy changes: " + ex.Message);
            }
        }

        // 把正在服务被清理基地的太空中继站送回或重置，避免清掉地面基地后中继仍引用旧 baseId。
        private static void ReturnRelaysForGroundBases(PlanetData planet, HashSet<int> baseIds)
        {
            // 没有恒星、太空扇区或目标基地集合时没有可返回的中继。
            if (planet?.star == null || GameMain.spaceSector == null || baseIds == null || baseIds.Count <= 0)
            {
                return;
            }

            EnemyDFHiveSystem hive = GameMain.spaceSector.dfHives[planet.star.index];
            // 同一恒星可能有多个兄弟巢穴，全部扫描才能清掉所有相关中继。
            while (hive != null)
            {
                ReturnRelaysForGroundBases(hive, planet.astroId, baseIds);
                hive = hive.nextSibling;
            }
        }

        // 扫描单个巢穴的中继池，处理目标或搜索状态指向被清理星球/基地的中继。
        private static void ReturnRelaysForGroundBases(EnemyDFHiveSystem hive, int planetAstroId, HashSet<int> baseIds)
        {
            // 没有中继对象池时不能处理。
            if (hive?.relays?.buffer == null)
            {
                return;
            }

            // 倒序扫描中继池，避免状态变更后影响尚未检查的槽位。
            for (int i = hive.relays.cursor - 1; i > 0; i--)
            {
                DFRelayComponent relay = hive.relays.buffer[i];
                // 对象池 id 校验，跳过空槽或旧对象。
                if (relay == null || relay.id != i)
                {
                    continue;
                }

                // 中继可能已经锁定被清理的基地，也可能只是在这个星球搜索新落点。
                bool targetsClearedBase = relay.targetAstroId == planetAstroId && baseIds.Contains(relay.baseId);
                bool searchesPlanet = relay.searchAstroId == planetAstroId;
                if (!targetsClearedBase && !searchesPlanet)
                {
                    continue;
                }

                try
                {
                    if (searchesPlanet)
                    {
                        // 只是在搜索这个星球时，清掉搜索和信标状态即可。
                        relay.ResetSearchStates();
                        relay.ResetTargetedMarker();
                    }

                    if (targetsClearedBase && ReturnRelay(relay))
                    {
                        // 返回成功后增加中和计数，和原版 LeaveBase/被摧毁统计保持接近。
                        hive.relayNeutralizedCounter++;
                    }
                }
                catch (Exception ex)
                {
                    LogInfo("error to return relay " + relay.id + ": " + ex.Message);
                }
            }
        }

        // 让一个中继脱离被清理基地并返回巢穴；返回 true 表示确实执行了中和/返回动作。
        private static bool ReturnRelay(DFRelayComponent relay)
        {
            LogInfo("return relay -> relay.id: " + relay.id + ", enemyId: " + relay.enemyId);

            // stage == 2 表示中继已停靠在地面基地，使用原版 LeaveBase 让它按游戏规则离开。
            if (relay.stage == 2)
            {
                relay.LeaveBase();
                return true;
            }

            // direction < 0 通常表示已经在返回途中，只清搜索状态，不重复计入中和。
            if (relay.direction < 0)
            {
                relay.ResetSearchStates();
                relay.ResetTargetedMarker();
                return false;
            }

            // 其他外派状态手动清空基地引用，并把 direction 设为返回方向。
            relay.ClearBaseReferences();
            relay.targetAstroId = 0;
            relay.targetLPos = UnityEngine.Vector3.zero;
            relay.targetYaw = 0f;
            // 清掉地面基地相关字段，避免下次 tick 继续访问已删除 baseId。
            relay.baseState = 0;
            relay.baseId = 0;
            relay.baseTicks = 0;
            relay.baseEvolve = default(EvolveData);
            relay.baseRespawnCD = 0;
            // direction = -1 表示回巢，速度和参数归零让后续状态机重新计算。
            relay.direction = -1;
            relay.param0 = 0f;
            // 载具和搜索/信标状态都要清掉，否则中继可能保留旧任务。
            relay.RemoveAllCarriers();
            relay.ResetSearchStates();
            relay.ResetTargetedMarker();
            return true;
        }

        // 解绑并移除地面基地组件；在所有敌人和中继处理后执行，避免留下基地/废墟互相引用。
        private static void UnlinkGroundBaseRuins(PlanetFactory planetFactory, ObjectPool<DFGBaseComponent> bases, List<int> baseIds)
        {
            foreach (int baseId in baseIds)
            {
                // 跳过越界或无效基地 id。
                if (baseId <= 0 || bases?.buffer == null || baseId >= bases.cursor)
                {
                    continue;
                }

                DFGBaseComponent baseComponent = bases.buffer[baseId];
                // id 不匹配说明槽位已被释放或复用。
                if (baseComponent == null || baseComponent.id != baseId)
                {
                    continue;
                }

                try
                {
                    int enemyId = baseComponent.enemyId;
                    int ruinId = baseComponent.ruinId;
                    LogInfo("unlink DFGBaseComponent -> base.id: " + baseId + ", enemyId: " + enemyId + ", ruinId: " + ruinId);

                    // 先清中继/巢穴引用，避免 RemoveEnemyWithComponents 过程中仍试图通过基地找回中继。
                    baseComponent.relayId = 0;
                    baseComponent.relayEnemyId = 0;
                    baseComponent.hiveAstroId = 0;

                    // 如果核心敌人仍存在，使用工厂 API 移除它和相关组件；否则只移除基地组件本身。
                    if (IsLiveGroundEnemy(planetFactory, enemyId))
                    {
                        planetFactory.RemoveEnemyWithComponents(enemyId);
                    }
                    else
                    {
                        RemoveGroundBaseComponentOnly(planetFactory, baseId);
                    }
                }
                catch (Exception ex)
                {
                    LogInfo("error to unlink DFGBaseComponent " + baseId + ": " + ex.Message);
                }
            }
        }

        // 只移除 DFGBaseComponent 和平台状态区域；用于核心敌人已经不存在的情况。
        private static void RemoveGroundBaseComponentOnly(PlanetFactory planetFactory, int baseId)
        {
            // 没有 enemySystem 或 baseId 无效时没有可移除组件。
            if (planetFactory?.enemySystem == null || baseId <= 0)
            {
                return;
            }

            // 平台系统里用 baseId 编码状态区域，不移除会留下暗雾基地占地区域。
            if (planetFactory.platformSystem != null)
            {
                planetFactory.platformSystem.RemoveStateArea((uint)(0x1000000uL | (ulong)baseId));
            }

            // 移除敌人系统中的地面基地组件，让 bases 对象池不再保留该基地。
            planetFactory.enemySystem.RemoveDFGBaseComponent(baseId);
        }

        // 清理当前本地星球的渲染残留；非本地或未加载星球只需要数据层删除。
        private static void ClearLocalDarkFogRenderResidue(
            PlanetData planet,
            PlanetFactory planetFactory,
            List<int> baseIds,
            List<EnemyModelRef> removedModels)
        {
            // 只有当前玩家所在且已加载的星球才存在需要立即清理的本地模型/渲染器。
            if (planet?.factoryModel == null || planetFactory == null || planet != GameMain.localPlanet || !planet.factoryLoaded)
            {
                return;
            }

            // 先移除具体敌人模型，再清理地面渲染器和阵型渲染器缓存。
            RemoveRememberedEnemyModels(planetFactory, removedModels);
            ClearLocalGroundRenderer(planet.factoryModel, planetFactory, baseIds);
            ClearLocalFormationRenderers(planet.factoryModel);
        }

        // 主动从 GPUI 移除已经删除的敌人模型，防止屏幕上短时间残留暗雾模型。
        private static void RemoveRememberedEnemyModels(PlanetFactory planetFactory, List<EnemyModelRef> removedModels)
        {
            // 只有当前激活工厂的模型需要手动移除；非激活工厂没有可见 GPUI 残留。
            if (removedModels == null || removedModels.Count <= 0 || GameMain.gpuiManager?.activeFactory != planetFactory)
            {
                return;
            }

            HashSet<int> removedModelIds = new HashSet<int>();
            foreach (EnemyModelRef modelRef in removedModels)
            {
                // 去重 modelId，避免同一个模型被重复 RemoveModel。
                if (modelRef.ModelId <= 0 || !removedModelIds.Add(modelRef.ModelId))
                {
                    continue;
                }

                try
                {
                    // GPUI 需要 modelIndex + modelId 才能定位并移除实例。
                    GameMain.gpuiManager.RemoveModel(modelRef.ModelIndex, modelRef.ModelId);
                }
                catch (Exception ex)
                {
                    LogInfo("error to remove remembered enemy model " + modelRef.ModelId + ": " + ex.Message);
                }
            }
        }

        // 清理地面暗雾渲染器中按基地索引缓存的 builder/truck 数据。
        private static void ClearLocalGroundRenderer(FactoryModel factoryModel, PlanetFactory planetFactory, List<int> baseIds)
        {
            EnemyDFGroundRenderer renderer = factoryModel.dfGroundRenderer;
            // 没有地面暗雾渲染器时不需要处理。
            if (renderer == null)
            {
                return;
            }

            try
            {
                // 确保渲染器引用当前 enemySystem，避免后续渲染 tick 使用旧引用。
                renderer.enemySystem = planetFactory.enemySystem;

                if (renderer.builderArr != null && baseIds != null)
                {
                    foreach (int baseId in baseIds)
                    {
                        // builderArr 按 baseId * 80 分段存储该基地 builder 渲染状态。
                        int start = baseId * 80;
                        int end = Math.Min(start + 80, renderer.builderArr.Length);
                        for (int i = start; i < end; i++)
                        {
                            // instId 清零能让渲染器不再尝试绘制已删除的建筑 builder。
                            renderer.builderArr[i].instId = 0;
                        }
                    }
                }

                // 运输车段和索引是临时渲染缓存，整体清零防止残留路径/车辆显示。
                if (renderer.truckSegments != null)
                {
                    Array.Clear(renderer.truckSegments, 0, renderer.truckSegments.Length);
                }

                if (renderer.truckBuilderIndices != null)
                {
                    Array.Clear(renderer.truckBuilderIndices, 0, renderer.truckBuilderIndices.Length);
                }
            }
            catch (Exception ex)
            {
                LogInfo("error to clear local ground renderer residue: " + ex.Message);
            }
        }

        // 清空所有阵型渲染器计数，避免已清除阵型仍在本地画出单位群。
        private static void ClearLocalFormationRenderers(FactoryModel factoryModel)
        {
            EnemyFormationRenderer[] renderers = factoryModel.dfFormRenderers;
            // 没有阵型渲染器时直接返回。
            if (renderers == null)
            {
                return;
            }

            foreach (EnemyFormationRenderer renderer in renderers)
            {
                // 某些槽位可能没有实例。
                if (renderer == null)
                {
                    continue;
                }

                // 清三个 cursor/计数，让渲染器下一帧认为没有单位、状态和组可画。
                renderer.unitCount = 0;
                renderer.stateCursor = 0;
                renderer.groupCursor = 0;
            }
        }

        // 刷新炮塔搜索缓存；暗雾被删除后，防御系统需要重新计算可攻击目标配对。
        private static void RefreshGroundDefenseSearch(PlanetFactory planetFactory)
        {
            try
            {
                planetFactory?.defenseSystem?.RefreshTurretSearchPair();
            }
            catch (Exception ex)
            {
                LogInfo("error to refresh turret search pair: " + ex.Message);
            }
        }

        // 清理 UI 上的暗雾袭击红点/提示，避免敌人已被删除但提示还停留。
        private static void ClearDarkFogAssaultTips()
        {
            try
            {
                UIRoot.instance?.uiGame?.dfAssaultTip?.ClearAllSpots();
            }
            catch (Exception)
            {
                LogInfo("error to clear dark fog assault tips");
            }
        }

        // UI 按钮入口：保存当前游戏后清理当前恒星系的太空暗雾单位和巢穴外层结构。
        internal static void ClearCurrentStarSpaceDarkFog()
        {
            // 这是大范围删除太空敌人的操作，先保存当前游戏方便回退。
            SaveCurrentGame();

            // 不在游戏场景时不能安全访问本地恒星。
            if (GameMain.mainPlayer == null)
            {
                return;
            }

            StarData star = GameMain.localStar;
            // 没有本地恒星时说明当前不在可操作星系上下文。
            if (star == null)
            {
                return;
            }

            ClearStarSpaceDarkFog(star);
        }

        // 清理指定恒星系的太空暗雾敌人；巢穴核心保留并设为无敌，避免破坏核心结构引用。
        private static void ClearStarSpaceDarkFog(StarData star)
        {
            SpaceSector spaceSector = GameMain.spaceSector;
            List<int> enemyIdsToKill = new List<int>();

            // 先从全局太空 enemyPool 收集当前恒星系可直接杀的敌人。
            for (var i = spaceSector.enemyCursor - 1; i > 0; i--)
            {
                ref EnemyData enemyData = ref spaceSector.enemyPool[i];
                // 跳过无效槽、无敌单位、巢穴核心和其他恒星系敌人。
                if (enemyData.id != i || enemyData.isInvincible || enemyData.dfSCoreId > 0 || enemyData.astroId != star.astroId)
                {
                    continue;
                }

                LogInfo("active relays -> " + enemyData.id);
                enemyIdsToKill.Add(enemyData.id);
            }

            EnemyDFHiveSystem hive = spaceSector.dfHives[star.index];

            // 同一恒星可能有多个兄弟巢穴，逐个收集内部对象池里的可杀敌人。
            while (hive != null)
            {
                if (hive.isAlive)
                {
                    LogInfo("hiveAstroId " + hive.hiveAstroId);
                    LogInfo("rootEnemyId " + hive.rootEnemyId);

                    // 各类 DataPool 分别存放单位、火种、炮塔、节点等结构，用统一 helper 收集 enemyId。
                    CollectKillableEnemyIds(spaceSector, enemyIdsToKill, hive.units, component => component.enemyId);
                    CollectKillableEnemyIds(spaceSector, enemyIdsToKill, hive.tinders, component => component.enemyId);
                    // 空闲中继不一定在通用单位池里，所以单独从 idleRelayIds 收集。
                    CollectIdleRelayEnemyIds(spaceSector, enemyIdsToKill, hive);
                    CollectKillableEnemyIds(spaceSector, enemyIdsToKill, hive.turrets, component => component.enemyId);
                    CollectKillableEnemyIds(spaceSector, enemyIdsToKill, hive.gammas, component => component.enemyId);
                    CollectKillableEnemyIds(spaceSector, enemyIdsToKill, hive.replicators, component => component.enemyId);
                    CollectKillableEnemyIds(spaceSector, enemyIdsToKill, hive.connectors, component => component.enemyId);
                    CollectKillableEnemyIds(spaceSector, enemyIdsToKill, hive.nodes, component => component.enemyId);
                    CollectKillableEnemyIds(spaceSector, enemyIdsToKill, hive.builders, component => component.enemyId);

                    // 清阵型和抽干核心 builder 资源，避免被杀单位/建筑马上被巢穴逻辑补回。
                    ClearHiveFormations(hive);
                    DrainHiveCoreBuilders(hive);
                }

                hive = hive.nextSibling;
            }

            // Distinct 去重后执行最终击杀；同一个 enemyId 可能从多个池或全局扫描收集到。
            foreach (int enemyId in enemyIdsToKill.Distinct())
            {
                try
                {
                    // KillEnemyFinal 走太空扇区自己的最终删除流程，清理组件和对象池引用。
                    spaceSector.KillEnemyFinal(enemyId, ref CombatStat.empty);
                }
                catch (Exception)
                {
                    LogInfo("error to kill " + enemyId);
                }
            }

            // 清理外层结构后把核心设为无敌，防止玩家或后续清理误删核心导致巢穴链表损坏。
            MakeHiveCoresInvincible(star);
        }

        // 从任意太空暗雾组件池中收集可杀 enemyId；泛型让多个池共用同一套校验逻辑。
        private static void CollectKillableEnemyIds<T>(SpaceSector spaceSector, List<int> enemyIds, DataPool<T> pool, Func<T, int> getEnemyId)
            where T : struct, IPoolElement
        {
            // 倒序遍历对象池，和删除流程的方向保持一致。
            for (var i = pool.cursor - 1; i > 0; i--)
            {
                int enemyId = getEnemyId(pool.buffer[i]);
                // 只收集非核心、非无敌的有效敌人，避免破坏巢穴核心引用。
                if (CanKillSpaceEnemy(spaceSector, enemyId))
                {
                    enemyIds.Add(enemyId);
                }
            }
        }

        // 收集巢穴空闲中继对应的 enemyId；这些中继可能没有从其他池被覆盖到。
        private static void CollectIdleRelayEnemyIds(SpaceSector spaceSector, List<int> enemyIds, EnemyDFHiveSystem hive)
        {
            // idleRelayIds 是有效中继 id 列表，倒序遍历和其他清理保持一致。
            for (var i = hive.idleRelayCount - 1; i > 0; i--)
            {
                DFRelayComponent relay = hive.relays.buffer[hive.idleRelayIds[i]];
                // 只杀非核心、非无敌的中继敌人。
                if (CanKillSpaceEnemy(spaceSector, relay.enemyId))
                {
                    enemyIds.Add(relay.enemyId);
                }
            }
        }

        // 判断一个太空 enemyId 是否允许被清理按钮击杀。
        private static bool CanKillSpaceEnemy(SpaceSector spaceSector, int enemyId)
        {
            // 非正 id 是空引用，不能访问 enemyPool。
            if (enemyId <= 0)
            {
                return false;
            }

            ref EnemyData enemyData = ref spaceSector.enemyPool[enemyId];
            // 保留无敌单位和巢穴核心，避免破坏关键结构。
            return !enemyData.isInvincible && enemyData.dfSCoreId <= 0;
        }

        // 清空巢穴阵型，避免已删除单位仍被阵型 AI/渲染引用。
        private static void ClearHiveFormations(EnemyDFHiveSystem hive)
        {
            EnemyFormation[] formations = hive.forms;
            // 逐个阵型清理；这里不删除数组，只重置阵型内容。
            for (int i = 0; i < formations.Length; i++)
            {
                if (formations[i].unitCount <= 0)
                {
                    continue;
                }

                LogInfo("clear form " + formations[i].unitCount);
                // Clear 会清掉单位列表和计数，后续 AI 不会再驱动已删除单位。
                formations[i].Clear();
            }
        }

        // 抽干巢穴核心 builder 的物质和能量，降低清理后马上重建外层结构的概率。
        private static void DrainHiveCoreBuilders(EnemyDFHiveSystem hive)
        {
            // core 里保存 builderId，真正的资源字段在 builders 池中。
            for (var i = hive.cores.cursor - 1; i > 0; i--)
            {
                ref EnemyBuilderComponent builder = ref hive.builders.buffer[hive.cores.buffer[i].builderId];
                // 物质和能量归零，让核心短期内没有资源继续建造。
                builder.matter = 0;
                builder.energy = 0;
            }
        }

        // UI 按钮入口：为全银河每个恒星补足暗雾巢穴数量。
        internal static void FillGalaxyWithDarkFogHives()
        {
            // 大范围创建巢穴前保存当前游戏，便于玩家回退。
            SaveCurrentGame();

            // 不在有效游戏场景时不能安全访问银河和太空扇区。
            if (GameMain.mainPlayer == null)
            {
                return;
            }

            GalaxyData galaxy = GameMain.galaxy;
            SpaceSector spaceSector = GameMain.spaceSector;

            // 遍历所有恒星，按 star.maxHiveCount 上限补巢穴，但最多补到 8 个，避免过量生成。
            for (int i = 0; i < galaxy.starCount; i++)
            {
                StarData star = galaxy.stars[i];
                int targetHiveCount = Math.Min(star.maxHiveCount, 8);
                int currentHiveCount = CountHives(spaceSector.dfHives[star.index]);

                // 只创建缺口数量的巢穴，已有巢穴不动。
                for (int j = 0; j < targetHiveCount - currentHiveCount; j++)
                {
                    EnemyDFHiveSystem hive = spaceSector.TryCreateNewHive(star);
                    // 创建失败可能是游戏内部条件不满足，跳过该次尝试。
                    if (hive == null)
                    {
                        continue;
                    }

                    LogInfo(star.displayName + " Add 1 hive");
                    // SetForNewGame 初始化巢穴内部池、核心和基础状态，让新巢穴能被游戏正常驱动。
                    hive.SetForNewGame();
                }
            }
        }

        // 统计一个恒星的兄弟巢穴链表长度。
        private static int CountHives(EnemyDFHiveSystem hive)
        {
            int count = 0;
            // dfHives[star.index] 是链表头，nextSibling 串起同恒星的其他巢穴。
            while (hive != null)
            {
                count++;
                hive = hive.nextSibling;
            }

            return count;
        }

        // 把指定恒星所有巢穴核心标记为无敌，确保清理太空外层后核心不会被误删。
        private static void MakeHiveCoresInvincible(StarData star)
        {
            SpaceSector spaceSector = GameMain.spaceSector;
            EnemyDFHiveSystem hive = spaceSector.dfHives[star.index];

            // 扫描同恒星所有巢穴的核心池。
            while (hive != null)
            {
                for (var i = hive.cores.cursor - 1; i > 0; i--)
                {
                    ref EnemyData enemyData = ref spaceSector.enemyPool[hive.cores.buffer[i].enemyId];
                    // 核心保留后设为无敌，避免下一次清理或战斗把它删掉。
                    enemyData.isInvincible = true;
                }

                hive = hive.nextSibling;
            }
        }

        // 给破坏性按钮操作创建一次带种子和时间戳的自动存档。
        private static void SaveCurrentGame()
        {
            DateTime now = DateTime.Now;
            string currentTimeString = now.ToString("yyyy-MM-dd HH:mm:ss");
            string seedString = GameMain.data.gameDesc.galaxySeed.ToString("00000000");
            // 存档名包含银河种子和当前时间，便于玩家在存档列表里识别操作前备份。
            string saveName = string.Format("[{0}] {1}", seedString, currentTimeString);
            GameSave.SaveCurrentGame(saveName);
        }

        // 日志包装，避免每个调用点都处理 Log 为空的情况。
        private static void LogInfo(string message)
        {
            Log?.LogInfo(message);
        }
    }
}
