using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;

namespace HardFog
{
    internal static class DarkFogControl
    {
        internal static ManualLogSource Log;

        internal static void ClearCurrentPlanetDarkFog()
        {
            SaveCurrentGame();

            if (GameMain.mainPlayer == null)
            {
                return;
            }

            PlanetData planet = GameMain.localPlanet;
            if (planet == null)
            {
                return;
            }

            ClearPlanetDarkFog(planet);
        }

        private static void ClearPlanetDarkFog(PlanetData planet)
        {
            LogInfo("planet.name: " + planet.name);
            LogInfo("astroId: " + planet.astroId);

            PlanetFactory planetFactory = GameMain.data.GetOrCreateFactory(planet);
            if (planetFactory?.enemySystem?.bases == null)
            {
                return;
            }

            ObjectPool<DFGBaseComponent> bases = planetFactory.enemySystem.bases;
            List<int> baseIds = CollectGroundBaseIdsDescending(bases);
            if (baseIds.Count <= 0)
            {
                ClearDarkFogAssaultTips();
                return;
            }

            HashSet<int> baseIdSet = new HashSet<int>(baseIds);
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

        private struct EnemyModelRef
        {
            public int ModelIndex;

            public int ModelId;
        }

        private static List<int> CollectGroundBaseIdsDescending(ObjectPool<DFGBaseComponent> bases)
        {
            List<int> baseIds = new List<int>();
            if (bases?.buffer == null)
            {
                return baseIds;
            }

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

        private static void ClearGroundUnits(PlanetFactory planetFactory, HashSet<int> baseIds, List<EnemyModelRef> removedModels)
        {
            foreach (int enemyId in CollectGroundUnitEnemyIdsDescending(planetFactory, baseIds))
            {
                KillGroundEnemyFinally(planetFactory, enemyId, "unit", removedModels);
            }
        }

        private static void ClearGroundNonCoreBuildings(PlanetFactory planetFactory, List<int> baseIds, List<EnemyModelRef> removedModels)
        {
            const int maxPasses = 8;
            for (int pass = 0; pass < maxPasses; pass++)
            {
                int killedCount = 0;
                foreach (int baseId in baseIds)
                {
                    foreach (int enemyId in CollectGroundNonCoreBuildingEnemyIdsDescending(planetFactory, baseId))
                    {
                        if (KillGroundEnemyFinally(planetFactory, enemyId, "building", removedModels))
                        {
                            killedCount++;
                        }
                    }
                }

                ExecuteDeferredGroundEnemyChanges(planetFactory);
                if (killedCount == 0)
                {
                    return;
                }
            }
        }

        private static void ClearGroundBaseCores(PlanetFactory planetFactory, List<int> baseIds, List<EnemyModelRef> removedModels)
        {
            foreach (int baseId in baseIds)
            {
                DFGBaseComponent baseComponent = GetGroundBase(planetFactory, baseId);
                int enemyId = baseComponent?.enemyId ?? 0;
                if (IsGroundCoreEnemy(planetFactory, enemyId, baseId))
                {
                    KillGroundEnemyFinally(planetFactory, enemyId, "core", removedModels);
                }
            }
        }

        private static List<int> CollectGroundUnitEnemyIdsDescending(PlanetFactory planetFactory, HashSet<int> baseIds)
        {
            List<int> enemyIds = new List<int>();
            HashSet<int> seenEnemyIds = new HashSet<int>();
            DataPool<EnemyUnitComponent> units = planetFactory?.enemySystem?.units;
            if (planetFactory?.enemyPool == null || baseIds == null || baseIds.Count <= 0)
            {
                return enemyIds;
            }

            if (units?.buffer != null)
            {
                for (int i = units.cursor - 1; i > 0; i--)
                {
                    ref EnemyUnitComponent unit = ref units.buffer[i];
                    if (unit.id == i && baseIds.Contains(unit.baseId) && IsGroundUnitEnemy(planetFactory, unit.enemyId, unit.baseId))
                    {
                        enemyIds.Add(unit.enemyId);
                        seenEnemyIds.Add(unit.enemyId);
                    }
                }
            }

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

        private static List<int> CollectGroundNonCoreBuildingEnemyIdsDescending(PlanetFactory planetFactory, int baseId)
        {
            List<int> enemyIds = new List<int>();
            HashSet<int> seenEnemyIds = new HashSet<int>();
            DFGBaseComponent baseComponent = GetGroundBase(planetFactory, baseId);
            GrowthPattern_DFGround.Builder[] pbuilders = baseComponent?.pbuilders;

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

            if (planetFactory?.enemyPool == null)
            {
                return enemyIds;
            }

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

        private static bool IsGroundUnitEnemy(PlanetFactory planetFactory, int enemyId, int baseId)
        {
            if (!IsLiveGroundEnemy(planetFactory, enemyId))
            {
                return false;
            }

            ref EnemyData enemyData = ref planetFactory.enemyPool[enemyId];
            return enemyData.dynamic && enemyData.owner == baseId;
        }

        private static bool IsGroundNonCoreBuildingEnemy(PlanetFactory planetFactory, int enemyId, int baseId)
        {
            if (!IsLiveGroundEnemy(planetFactory, enemyId))
            {
                return false;
            }

            ref EnemyData enemyData = ref planetFactory.enemyPool[enemyId];
            return !enemyData.dynamic && enemyData.owner == baseId && enemyData.dfGBaseId != baseId;
        }

        private static bool IsGroundCoreEnemy(PlanetFactory planetFactory, int enemyId, int baseId)
        {
            if (!IsLiveGroundEnemy(planetFactory, enemyId))
            {
                return false;
            }

            ref EnemyData enemyData = ref planetFactory.enemyPool[enemyId];
            return !enemyData.dynamic && enemyData.dfGBaseId == baseId;
        }

        private static bool KillGroundEnemyFinally(PlanetFactory planetFactory, int enemyId, string kind, List<EnemyModelRef> removedModels)
        {
            if (!IsLiveGroundEnemy(planetFactory, enemyId))
            {
                return false;
            }

            try
            {
                RememberEnemyModel(planetFactory, enemyId, removedModels);
                LogInfo("KillEnemyFinally " + kind + " -> enemyData.id: " + enemyId);
                planetFactory.KillEnemyFinally(enemyId, ref CombatStat.empty);
                return true;
            }
            catch (Exception ex)
            {
                LogInfo("error to kill ground " + kind + " " + enemyId + ": " + ex.Message);
                return false;
            }
        }

        private static bool IsLiveGroundEnemy(PlanetFactory planetFactory, int enemyId)
        {
            return planetFactory?.enemyPool != null
                && enemyId > 0
                && enemyId < planetFactory.enemyCursor
                && enemyId < planetFactory.enemyPool.Length
                && planetFactory.enemyPool[enemyId].id == enemyId
                && !planetFactory.enemyPool[enemyId].isSpace;
        }

        private static void RememberEnemyModel(PlanetFactory planetFactory, int enemyId, List<EnemyModelRef> removedModels)
        {
            if (removedModels == null || !IsLiveGroundEnemy(planetFactory, enemyId))
            {
                return;
            }

            ref EnemyData enemyData = ref planetFactory.enemyPool[enemyId];
            if (enemyData.modelId <= 0)
            {
                return;
            }

            removedModels.Add(new EnemyModelRef
            {
                ModelIndex = enemyData.modelIndex,
                ModelId = enemyData.modelId
            });
        }

        private static void ClearGroundBaseFormations(PlanetFactory planetFactory, List<int> baseIds)
        {
            foreach (int baseId in baseIds)
            {
                DFGBaseComponent baseComponent = GetGroundBase(planetFactory, baseId);
                if (baseComponent == null || baseComponent.forms == null)
                {
                    continue;
                }

                for (int i = baseComponent.forms.Length - 1; i >= 0; i--)
                {
                    EnemyFormation formation = baseComponent.forms[i];
                    if (formation == null || formation.unitCount <= 0)
                    {
                        continue;
                    }

                    try
                    {
                        LogInfo("clear DFGBaseComponent:" + baseComponent.id + " -> form count: " + formation.unitCount);
                        formation.Clear();
                    }
                    catch (Exception ex)
                    {
                        LogInfo("error to clear form " + i + " for base " + baseId + ": " + ex.Message);
                    }
                }
            }
        }

        private static DFGBaseComponent GetGroundBase(PlanetFactory planetFactory, int baseId)
        {
            ObjectPool<DFGBaseComponent> bases = planetFactory?.enemySystem?.bases;
            if (baseId <= 0 || bases?.buffer == null || baseId >= bases.cursor)
            {
                return null;
            }

            DFGBaseComponent baseComponent = bases.buffer[baseId];
            return baseComponent != null && baseComponent.id == baseId ? baseComponent : null;
        }

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

        private static void ReturnRelaysForGroundBases(PlanetData planet, HashSet<int> baseIds)
        {
            if (planet?.star == null || GameMain.spaceSector == null || baseIds == null || baseIds.Count <= 0)
            {
                return;
            }

            EnemyDFHiveSystem hive = GameMain.spaceSector.dfHives[planet.star.index];
            while (hive != null)
            {
                ReturnRelaysForGroundBases(hive, planet.astroId, baseIds);
                hive = hive.nextSibling;
            }
        }

        private static void ReturnRelaysForGroundBases(EnemyDFHiveSystem hive, int planetAstroId, HashSet<int> baseIds)
        {
            if (hive?.relays?.buffer == null)
            {
                return;
            }

            for (int i = hive.relays.cursor - 1; i > 0; i--)
            {
                DFRelayComponent relay = hive.relays.buffer[i];
                if (relay == null || relay.id != i)
                {
                    continue;
                }

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
                        relay.ResetSearchStates();
                        relay.ResetTargetedMarker();
                    }

                    if (targetsClearedBase && ReturnRelay(relay))
                    {
                        hive.relayNeutralizedCounter++;
                    }
                }
                catch (Exception ex)
                {
                    LogInfo("error to return relay " + relay.id + ": " + ex.Message);
                }
            }
        }

        private static bool ReturnRelay(DFRelayComponent relay)
        {
            LogInfo("return relay -> relay.id: " + relay.id + ", enemyId: " + relay.enemyId);

            if (relay.stage == 2)
            {
                relay.LeaveBase();
                return true;
            }

            if (relay.direction < 0)
            {
                relay.ResetSearchStates();
                relay.ResetTargetedMarker();
                return false;
            }

            relay.ClearBaseReferences();
            relay.targetAstroId = 0;
            relay.targetLPos = UnityEngine.Vector3.zero;
            relay.targetYaw = 0f;
            relay.baseState = 0;
            relay.baseId = 0;
            relay.baseTicks = 0;
            relay.baseEvolve = default(EvolveData);
            relay.baseRespawnCD = 0;
            relay.direction = -1;
            relay.param0 = 0f;
            relay.RemoveAllCarriers();
            relay.ResetSearchStates();
            relay.ResetTargetedMarker();
            return true;
        }

        private static void UnlinkGroundBaseRuins(PlanetFactory planetFactory, ObjectPool<DFGBaseComponent> bases, List<int> baseIds)
        {
            foreach (int baseId in baseIds)
            {
                if (baseId <= 0 || bases?.buffer == null || baseId >= bases.cursor)
                {
                    continue;
                }

                DFGBaseComponent baseComponent = bases.buffer[baseId];
                if (baseComponent == null || baseComponent.id != baseId)
                {
                    continue;
                }

                try
                {
                    int enemyId = baseComponent.enemyId;
                    int ruinId = baseComponent.ruinId;
                    LogInfo("unlink DFGBaseComponent -> base.id: " + baseId + ", enemyId: " + enemyId + ", ruinId: " + ruinId);

                    baseComponent.relayId = 0;
                    baseComponent.relayEnemyId = 0;
                    baseComponent.hiveAstroId = 0;

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

        private static void RemoveGroundBaseComponentOnly(PlanetFactory planetFactory, int baseId)
        {
            if (planetFactory?.enemySystem == null || baseId <= 0)
            {
                return;
            }

            if (planetFactory.platformSystem != null)
            {
                planetFactory.platformSystem.RemoveStateArea((uint)(0x1000000uL | (ulong)baseId));
            }

            planetFactory.enemySystem.RemoveDFGBaseComponent(baseId);
        }

        private static void ClearLocalDarkFogRenderResidue(
            PlanetData planet,
            PlanetFactory planetFactory,
            List<int> baseIds,
            List<EnemyModelRef> removedModels)
        {
            if (planet?.factoryModel == null || planetFactory == null || planet != GameMain.localPlanet || !planet.factoryLoaded)
            {
                return;
            }

            RemoveRememberedEnemyModels(planetFactory, removedModels);
            ClearLocalGroundRenderer(planet.factoryModel, planetFactory, baseIds);
            ClearLocalFormationRenderers(planet.factoryModel);
        }

        private static void RemoveRememberedEnemyModels(PlanetFactory planetFactory, List<EnemyModelRef> removedModels)
        {
            if (removedModels == null || removedModels.Count <= 0 || GameMain.gpuiManager?.activeFactory != planetFactory)
            {
                return;
            }

            HashSet<int> removedModelIds = new HashSet<int>();
            foreach (EnemyModelRef modelRef in removedModels)
            {
                if (modelRef.ModelId <= 0 || !removedModelIds.Add(modelRef.ModelId))
                {
                    continue;
                }

                try
                {
                    GameMain.gpuiManager.RemoveModel(modelRef.ModelIndex, modelRef.ModelId);
                }
                catch (Exception ex)
                {
                    LogInfo("error to remove remembered enemy model " + modelRef.ModelId + ": " + ex.Message);
                }
            }
        }

        private static void ClearLocalGroundRenderer(FactoryModel factoryModel, PlanetFactory planetFactory, List<int> baseIds)
        {
            EnemyDFGroundRenderer renderer = factoryModel.dfGroundRenderer;
            if (renderer == null)
            {
                return;
            }

            try
            {
                renderer.enemySystem = planetFactory.enemySystem;

                if (renderer.builderArr != null && baseIds != null)
                {
                    foreach (int baseId in baseIds)
                    {
                        int start = baseId * 80;
                        int end = Math.Min(start + 80, renderer.builderArr.Length);
                        for (int i = start; i < end; i++)
                        {
                            renderer.builderArr[i].instId = 0;
                        }
                    }
                }

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

        private static void ClearLocalFormationRenderers(FactoryModel factoryModel)
        {
            EnemyFormationRenderer[] renderers = factoryModel.dfFormRenderers;
            if (renderers == null)
            {
                return;
            }

            foreach (EnemyFormationRenderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.unitCount = 0;
                renderer.stateCursor = 0;
                renderer.groupCursor = 0;
            }
        }

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

        internal static void ClearCurrentStarSpaceDarkFog()
        {
            SaveCurrentGame();

            if (GameMain.mainPlayer == null)
            {
                return;
            }

            StarData star = GameMain.localStar;
            if (star == null)
            {
                return;
            }

            ClearStarSpaceDarkFog(star);
        }

        private static void ClearStarSpaceDarkFog(StarData star)
        {
            SpaceSector spaceSector = GameMain.spaceSector;
            List<int> enemyIdsToKill = new List<int>();

            for (var i = spaceSector.enemyCursor - 1; i > 0; i--)
            {
                ref EnemyData enemyData = ref spaceSector.enemyPool[i];
                if (enemyData.id != i || enemyData.isInvincible || enemyData.dfSCoreId > 0 || enemyData.astroId != star.astroId)
                {
                    continue;
                }

                LogInfo("active relays -> " + enemyData.id);
                enemyIdsToKill.Add(enemyData.id);
            }

            EnemyDFHiveSystem hive = spaceSector.dfHives[star.index];

            while (hive != null)
            {
                if (hive.isAlive)
                {
                    LogInfo("hiveAstroId " + hive.hiveAstroId);
                    LogInfo("rootEnemyId " + hive.rootEnemyId);

                    CollectKillableEnemyIds(spaceSector, enemyIdsToKill, hive.units, component => component.enemyId);
                    CollectKillableEnemyIds(spaceSector, enemyIdsToKill, hive.tinders, component => component.enemyId);
                    CollectIdleRelayEnemyIds(spaceSector, enemyIdsToKill, hive);
                    CollectKillableEnemyIds(spaceSector, enemyIdsToKill, hive.turrets, component => component.enemyId);
                    CollectKillableEnemyIds(spaceSector, enemyIdsToKill, hive.gammas, component => component.enemyId);
                    CollectKillableEnemyIds(spaceSector, enemyIdsToKill, hive.replicators, component => component.enemyId);
                    CollectKillableEnemyIds(spaceSector, enemyIdsToKill, hive.connectors, component => component.enemyId);
                    CollectKillableEnemyIds(spaceSector, enemyIdsToKill, hive.nodes, component => component.enemyId);
                    CollectKillableEnemyIds(spaceSector, enemyIdsToKill, hive.builders, component => component.enemyId);

                    ClearHiveFormations(hive);
                    DrainHiveCoreBuilders(hive);
                }

                hive = hive.nextSibling;
            }

            foreach (int enemyId in enemyIdsToKill.Distinct())
            {
                try
                {
                    spaceSector.KillEnemyFinal(enemyId, ref CombatStat.empty);
                }
                catch (Exception)
                {
                    LogInfo("error to kill " + enemyId);
                }
            }

            MakeHiveCoresInvincible(star);
        }

        private static void CollectKillableEnemyIds<T>(SpaceSector spaceSector, List<int> enemyIds, DataPool<T> pool, Func<T, int> getEnemyId)
            where T : struct, IPoolElement
        {
            for (var i = pool.cursor - 1; i > 0; i--)
            {
                int enemyId = getEnemyId(pool.buffer[i]);
                if (CanKillSpaceEnemy(spaceSector, enemyId))
                {
                    enemyIds.Add(enemyId);
                }
            }
        }

        private static void CollectIdleRelayEnemyIds(SpaceSector spaceSector, List<int> enemyIds, EnemyDFHiveSystem hive)
        {
            for (var i = hive.idleRelayCount - 1; i > 0; i--)
            {
                DFRelayComponent relay = hive.relays.buffer[hive.idleRelayIds[i]];
                if (CanKillSpaceEnemy(spaceSector, relay.enemyId))
                {
                    enemyIds.Add(relay.enemyId);
                }
            }
        }

        private static bool CanKillSpaceEnemy(SpaceSector spaceSector, int enemyId)
        {
            if (enemyId <= 0)
            {
                return false;
            }

            ref EnemyData enemyData = ref spaceSector.enemyPool[enemyId];
            return !enemyData.isInvincible && enemyData.dfSCoreId <= 0;
        }

        private static void ClearHiveFormations(EnemyDFHiveSystem hive)
        {
            EnemyFormation[] formations = hive.forms;
            for (int i = 0; i < formations.Length; i++)
            {
                if (formations[i].unitCount <= 0)
                {
                    continue;
                }

                LogInfo("clear form " + formations[i].unitCount);
                formations[i].Clear();
            }
        }

        private static void DrainHiveCoreBuilders(EnemyDFHiveSystem hive)
        {
            for (var i = hive.cores.cursor - 1; i > 0; i--)
            {
                ref EnemyBuilderComponent builder = ref hive.builders.buffer[hive.cores.buffer[i].builderId];
                builder.matter = 0;
                builder.energy = 0;
            }
        }

        internal static void FillGalaxyWithDarkFogHives()
        {
            SaveCurrentGame();

            if (GameMain.mainPlayer == null)
            {
                return;
            }

            GalaxyData galaxy = GameMain.galaxy;
            SpaceSector spaceSector = GameMain.spaceSector;

            for (int i = 0; i < galaxy.starCount; i++)
            {
                StarData star = galaxy.stars[i];
                int targetHiveCount = Math.Min(star.maxHiveCount, 8);
                int currentHiveCount = CountHives(spaceSector.dfHives[star.index]);

                for (int j = 0; j < targetHiveCount - currentHiveCount; j++)
                {
                    EnemyDFHiveSystem hive = spaceSector.TryCreateNewHive(star);
                    if (hive == null)
                    {
                        continue;
                    }

                    LogInfo(star.displayName + " Add 1 hive");
                    hive.SetForNewGame();
                }
            }
        }

        private static int CountHives(EnemyDFHiveSystem hive)
        {
            int count = 0;
            while (hive != null)
            {
                count++;
                hive = hive.nextSibling;
            }

            return count;
        }

        private static void MakeHiveCoresInvincible(StarData star)
        {
            SpaceSector spaceSector = GameMain.spaceSector;
            EnemyDFHiveSystem hive = spaceSector.dfHives[star.index];

            while (hive != null)
            {
                for (var i = hive.cores.cursor - 1; i > 0; i--)
                {
                    ref EnemyData enemyData = ref spaceSector.enemyPool[hive.cores.buffer[i].enemyId];
                    enemyData.isInvincible = true;
                }

                hive = hive.nextSibling;
            }
        }

        private static void SaveCurrentGame()
        {
            DateTime now = DateTime.Now;
            string currentTimeString = now.ToString("yyyy-MM-dd HH:mm:ss");
            string seedString = GameMain.data.gameDesc.galaxySeed.ToString("00000000");
            string saveName = string.Format("[{0}] {1}", seedString, currentTimeString);
            GameSave.SaveCurrentGame(saveName);
        }

        private static void LogInfo(string message)
        {
            Log?.LogInfo(message);
        }
    }
}
