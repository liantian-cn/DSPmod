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
            CombatStat[] combatStatsBuffer = planetFactory.skillSystem.combatStats.buffer;
            ObjectPool<DFGBaseComponent> bases = planetFactory.enemySystem.bases;
            List<int> baseIds = CollectGroundBaseIds(bases);

            foreach (int baseId in baseIds)
            {
                ClearGroundUnitsForBase(planetFactory, combatStatsBuffer, baseId);
            }

            foreach (int baseId in baseIds)
            {
                ClearGroundBuildingsForBase(planetFactory, combatStatsBuffer, baseId);
            }

            foreach (int baseId in baseIds)
            {
                ReturnRelaysTargetingGroundBase(planet, baseId);
                RemoveResidualGroundBaseRecord(planetFactory, bases, baseId);
            }

            ReturnRelaysTargetingPlanet(planet);
            ClearGroundEnemiesLikeUxAssist(planet, planetFactory, combatStatsBuffer);
            ClearGroundWreckage(planet);
            ClearDarkFogAssaultTips();

            SpaceSector spaceSector = GameMain.spaceSector;

            for (var i = spaceSector.enemyCursor - 1; i > 0; i--)
            {
                ref EnemyData enemyData = ref spaceSector.enemyPool[i];
                if (enemyData.id != i)
                {
                    continue;
                }

                if (enemyData.astroId != planet.id)
                {
                    continue;
                }

                if (enemyData.dfRelayId > 0)
                {
                    LogInfo("skip relay -> enemyData.id: " + enemyData.id);
                    continue;
                }

                LogInfo("KillEnemyFinal -> enemyData.id: " + enemyData.id);
                spaceSector.KillEnemyFinal(enemyData.id, ref CombatStat.empty);
            }
        }

        private static List<int> CollectGroundBaseIds(ObjectPool<DFGBaseComponent> bases)
        {
            List<int> baseIds = new List<int>();
            if (bases == null || bases.buffer == null)
            {
                return baseIds;
            }

            for (int i = 1; i < bases.cursor; i++)
            {
                DFGBaseComponent baseComponent = bases.buffer[i];
                if (baseComponent != null && baseComponent.id == i)
                {
                    baseIds.Add(i);
                }
            }

            return baseIds;
        }

        private static void ClearGroundUnitsForBase(PlanetFactory planetFactory, CombatStat[] combatStatsBuffer, int baseId)
        {
            foreach (int enemyId in CollectBaseEnemyIds(planetFactory, baseId, dynamicOnly: true, coreLast: false))
            {
                ClearGroundEnemyFinally(planetFactory, combatStatsBuffer, enemyId);
            }

            DFGBaseComponent baseComponent = GetGroundBase(planetFactory, baseId);
            if (baseComponent != null)
            {
                ClearGroundBaseFormations(baseComponent);
            }
        }

        private static void ClearGroundBuildingsForBase(PlanetFactory planetFactory, CombatStat[] combatStatsBuffer, int baseId)
        {
            foreach (int enemyId in CollectBaseEnemyIds(planetFactory, baseId, dynamicOnly: false, coreLast: true))
            {
                ClearGroundEnemyFinally(planetFactory, combatStatsBuffer, enemyId);
            }
        }

        private static List<int> CollectBaseEnemyIds(PlanetFactory planetFactory, int baseId, bool dynamicOnly, bool coreLast)
        {
            List<int> enemyIds = new List<int>();
            int baseCoreEnemyId = 0;
            for (int i = 1; i < planetFactory.enemyCursor; i++)
            {
                ref EnemyData enemyData = ref planetFactory.enemyPool[i];
                if (enemyData.id != i || enemyData.owner != baseId || enemyData.dynamic != dynamicOnly)
                {
                    continue;
                }

                if (coreLast && enemyData.dfGBaseId == baseId)
                {
                    baseCoreEnemyId = i;
                    continue;
                }

                enemyIds.Add(i);
            }

            if (baseCoreEnemyId > 0)
            {
                enemyIds.Add(baseCoreEnemyId);
            }

            return enemyIds;
        }

        private static void ClearGroundEnemyFinally(PlanetFactory planetFactory, CombatStat[] combatStatsBuffer, int enemyId)
        {
            if (enemyId <= 0 || enemyId >= planetFactory.enemyCursor || planetFactory.enemyPool[enemyId].id != enemyId)
            {
                return;
            }

            try
            {
                RemoveGroundEnemyCombatStat(planetFactory, combatStatsBuffer, enemyId);
                LogInfo("KillEnemyFinally -> enemyData.id: " + enemyId);
                planetFactory.KillEnemyFinally(enemyId, ref CombatStat.empty);
            }
            catch (Exception)
            {
                LogInfo("error to kill enemy " + enemyId);
            }
        }

        private static void ClearGroundEnemiesLikeUxAssist(PlanetData planet, PlanetFactory planetFactory, CombatStat[] combatStatsBuffer)
        {
            if (planetFactory.enemyPool != null)
            {
                for (int i = planetFactory.enemyCursor - 1; i > 0; i--)
                {
                    if (planetFactory.enemyPool[i].id != i)
                    {
                        continue;
                    }

                    try
                    {
                        RemoveGroundEnemyCombatStat(planetFactory, combatStatsBuffer, i);
                        LogInfo("UXAssist-style KillEnemyFinally -> enemyData.id: " + i);
                        planetFactory.KillEnemyFinally(i, ref CombatStat.empty);
                    }
                    catch (Exception)
                    {
                        LogInfo("error to UXAssist-style kill enemy " + i);
                    }

                    if (i >= planetFactory.enemyCursor || planetFactory.enemyPool[i].id != i)
                    {
                        continue;
                    }

                    try
                    {
                        RemoveGroundEnemyCombatStat(planetFactory, combatStatsBuffer, i);
                        LogInfo("UXAssist-style RemoveEnemyWithComponents -> enemyData.id: " + i);
                        planetFactory.RemoveEnemyWithComponents(i);
                    }
                    catch (Exception)
                    {
                        LogInfo("error to UXAssist-style remove enemy " + i);
                        ClearGroundEnemySlotFallback(planetFactory, i);
                    }
                }
            }

            ClearGroundEnemyAnimationData(planetFactory);
            ResetGroundEnemyComponentPools(planetFactory);
            ClearLocalGroundEnemyRenderers(planet, planetFactory);
        }

        private static void ClearGroundEnemyAnimationData(PlanetFactory planetFactory)
        {
            if (planetFactory.enemyAnimPool == null)
            {
                return;
            }

            Array.Clear(planetFactory.enemyAnimPool, 0, Math.Min(planetFactory.enemyCursor, planetFactory.enemyAnimPool.Length));
        }

        private static void ResetGroundEnemyComponentPools(PlanetFactory planetFactory)
        {
            EnemyDFGroundSystem enemySystem = planetFactory.enemySystem;
            if (enemySystem == null)
            {
                return;
            }

            ResetDataPool(ref enemySystem.builders, "enemy builders");
            ResetObjectPool(ref enemySystem.bases, "DFG bases");
            ResetDataPool(ref enemySystem.connectors, "DFG connectors");
            ResetDataPool(ref enemySystem.replicators, "DFG replicators");
            ResetDataPool(ref enemySystem.turrets, "DFG turrets");
            ResetDataPool(ref enemySystem.shields, "DFG shields");
            ResetDataPool(ref enemySystem.units, "enemy units");
            enemySystem.truckSegments = null;
        }

        private static void ResetDataPool<T>(ref DataPool<T> pool, string name)
            where T : struct, IPoolElement
        {
            try
            {
                if (pool == null)
                {
                    pool = new DataPool<T>();
                }

                pool.Reset();
            }
            catch (Exception)
            {
                LogInfo("error to reset " + name);
                pool = new DataPool<T>();
                pool.Reset();
            }
        }

        private static void ResetObjectPool<T>(ref ObjectPool<T> pool, string name)
            where T : class, IPoolElement, new()
        {
            try
            {
                if (pool == null)
                {
                    pool = new ObjectPool<T>();
                }

                pool.Reset();
            }
            catch (Exception)
            {
                LogInfo("error to reset " + name);
                pool = new ObjectPool<T>();
                pool.Reset();
            }
        }

        private static void ClearLocalGroundEnemyRenderers(PlanetData planet, PlanetFactory planetFactory)
        {
            FactoryModel factoryModel = planet?.factoryModel;
            if (factoryModel == null)
            {
                return;
            }

            try
            {
                if (factoryModel.dfGroundRenderer != null)
                {
                    factoryModel.dfGroundRenderer.enemySystem = planetFactory.enemySystem;
                    factoryModel.dfGroundRenderer.truckSegments = null;
                    if (factoryModel.dfGroundRenderer.builderArr != null)
                    {
                        Array.Clear(factoryModel.dfGroundRenderer.builderArr, 0, factoryModel.dfGroundRenderer.builderArr.Length);
                    }
                }
            }
            catch (Exception)
            {
                LogInfo("error to clear ground enemy renderer");
            }

            try
            {
                if (factoryModel.dfFormRenderers != null)
                {
                    foreach (EnemyFormationRenderer renderer in factoryModel.dfFormRenderers)
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
            }
            catch (Exception)
            {
                LogInfo("error to clear ground enemy formation renderers");
            }
        }

        private static void RemoveGroundEnemyCombatStat(PlanetFactory planetFactory, CombatStat[] combatStatsBuffer, int enemyId)
        {
            if (combatStatsBuffer == null)
            {
                return;
            }

            ref EnemyData enemyData = ref planetFactory.enemyPool[enemyId];
            int combatStatId = enemyData.combatStatId;
            if (combatStatId > 0 && combatStatId < combatStatsBuffer.Length && combatStatsBuffer[combatStatId].id == combatStatId)
            {
                planetFactory.skillSystem.OnRemovingSkillTarget(combatStatId, combatStatsBuffer[combatStatId].originAstroId, ETargetType.CombatStat);
                planetFactory.skillSystem.combatStats.Remove(combatStatId);
                enemyData.combatStatId = 0;
            }
        }

        private static void ClearGroundEnemySlotFallback(PlanetFactory planetFactory, int enemyId)
        {
            if (enemyId <= 0 || enemyId >= planetFactory.enemyCursor || planetFactory.enemyPool[enemyId].id != enemyId)
            {
                return;
            }

            try
            {
                ref EnemyData enemyData = ref planetFactory.enemyPool[enemyId];

                if (enemyData.modelId != 0 && GameMain.gpuiManager.activeFactory == planetFactory)
                {
                    GameMain.gpuiManager.RemoveModel(enemyData.modelIndex, enemyData.modelId);
                }

                if (enemyData.mmblockId != 0 && planetFactory.blockContainer != null)
                {
                    planetFactory.blockContainer.RemoveMiniBlock(enemyData.mmblockId);
                }

                if (enemyData.colliderId != 0 && planetFactory.planet.physics != null)
                {
                    planetFactory.planet.physics.RemoveLinkedColliderData(enemyData.colliderId);
                }

                if (enemyData.audioId != 0 && planetFactory.planet.audio != null)
                {
                    planetFactory.planet.audio.RemoveAudioData(enemyData.audioId);
                    planetFactory.planet.audio.NotifyObjectRemove(EObjectType.Enemy, enemyId);
                }

                if (enemyData.hashAddress != 0)
                {
                    if (enemyData.dynamic)
                    {
                        planetFactory.hashSystemDynamic.RemoveObjectFromBucket(enemyData.hashAddress);
                    }
                    else
                    {
                        planetFactory.hashSystemStatic.RemoveObjectFromBucket(enemyData.hashAddress);
                    }
                }

                ClearGroundEnemyLogicComponentsFallback(planetFactory, ref enemyData);
                planetFactory.skillSystem.OnRemovingSkillTarget(enemyId, planetFactory.planet.astroId, ETargetType.Enemy);
                enemyData.SetEmpty();
                planetFactory.enemyAnimPool[enemyId] = default(AnimData);

                if (planetFactory.planet.physics != null)
                {
                    planetFactory.planet.physics.NotifyObjectRemove(EObjectType.Enemy, enemyId);
                }
            }
            catch (Exception)
            {
                LogInfo("error to fallback-clear enemy " + enemyId);
            }
        }

        private static void ClearGroundEnemyLogicComponentsFallback(PlanetFactory planetFactory, ref EnemyData enemyData)
        {
            try
            {
                if (enemyData.dfGBaseId != 0)
                {
                    planetFactory.platformSystem?.RemoveStateArea((uint)(0x1000000uL | (ulong)enemyData.dfGBaseId));
                    planetFactory.enemySystem.RemoveDFGBaseComponent(enemyData.dfGBaseId);
                    enemyData.dfGBaseId = 0;
                }

                if (enemyData.dfGConnectorId != 0)
                {
                    planetFactory.enemySystem.RemoveDFGConnectorComponent(enemyData.dfGConnectorId);
                    enemyData.dfGConnectorId = 0;
                }

                if (enemyData.dfGReplicatorId != 0)
                {
                    planetFactory.enemySystem.RemoveDFGReplicatorComponent(enemyData.dfGReplicatorId);
                    enemyData.dfGReplicatorId = 0;
                }

                if (enemyData.dfGTurretId != 0)
                {
                    planetFactory.enemySystem.RemoveDFGTurretComponent(enemyData.dfGTurretId);
                    enemyData.dfGTurretId = 0;
                }

                if (enemyData.dfGShieldId != 0)
                {
                    planetFactory.enemySystem.RemoveDFGShieldComponent(enemyData.dfGShieldId);
                    enemyData.dfGShieldId = 0;
                }

                if (enemyData.unitId != 0)
                {
                    planetFactory.enemySystem.RemoveEnemyUnitComponent(enemyData.unitId);
                    enemyData.unitId = 0;
                }

                if (enemyData.builderId != 0)
                {
                    planetFactory.enemySystem.RemoveEnemyBuilderComponent(enemyData.builderId);
                    enemyData.builderId = 0;
                }
            }
            catch (Exception)
            {
                LogInfo("error to fallback-clear enemy logic components");
            }
        }

        private static void RemoveResidualGroundBaseRecord(PlanetFactory planetFactory, ObjectPool<DFGBaseComponent> bases, int baseId)
        {
            if (baseId <= 0 || bases == null || bases.buffer == null || baseId >= bases.cursor)
            {
                return;
            }

            DFGBaseComponent baseComponent = bases.buffer[baseId];
            if (baseComponent == null || baseComponent.id != baseId)
            {
                return;
            }

            try
            {
                int enemyId = baseComponent.enemyId;
                LogInfo("remove DFGBaseComponent -> base.id: " + baseId + ", enemyId: " + enemyId + ", ruinId: " + baseComponent.ruinId);

                if (enemyId > 0 && enemyId < planetFactory.enemyCursor && planetFactory.enemyPool[enemyId].id == enemyId)
                {
                    planetFactory.RemoveEnemyWithComponents(enemyId);
                    return;
                }

                if (planetFactory.platformSystem != null)
                {
                    planetFactory.platformSystem.RemoveStateArea((uint)(0x1000000uL | (ulong)baseId));
                }

                planetFactory.enemySystem.RemoveDFGBaseComponent(baseId);
            }
            catch (Exception)
            {
                LogInfo("error to remove DFGBaseComponent " + baseId);
            }
        }

        private static DFGBaseComponent GetGroundBase(PlanetFactory planetFactory, int baseId)
        {
            ObjectPool<DFGBaseComponent> bases = planetFactory.enemySystem.bases;
            if (baseId <= 0 || bases == null || bases.buffer == null || baseId >= bases.cursor)
            {
                return null;
            }

            DFGBaseComponent baseComponent = bases.buffer[baseId];
            if (baseComponent != null && baseComponent.id == baseId)
            {
                return baseComponent;
            }

            return null;
        }

        private static void ReturnRelaysTargetingGroundBase(PlanetData planet, int baseId)
        {
            if (planet == null || planet.star == null || GameMain.spaceSector == null)
            {
                return;
            }

            EnemyDFHiveSystem hive = GameMain.spaceSector.dfHives[planet.star.index];
            while (hive != null)
            {
                ReturnRelaysTargetingGroundBase(hive, planet.astroId, baseId);
                hive = hive.nextSibling;
            }
        }

        private static void ReturnRelaysTargetingGroundBase(EnemyDFHiveSystem hive, int planetAstroId, int baseId)
        {
            if (hive == null || hive.relays == null)
            {
                return;
            }

            for (int i = hive.relays.cursor - 1; i > 0; i--)
            {
                DFRelayComponent relay = hive.relays.buffer[i];
                if (relay == null || relay.id != i || relay.targetAstroId != planetAstroId || relay.baseId != baseId)
                {
                    continue;
                }

                try
                {
                    if (ReturnRelay(relay))
                    {
                        hive.relayNeutralizedCounter++;
                    }
                }
                catch (Exception)
                {
                    LogInfo("error to return relay " + relay.id);
                }
            }
        }

        private static void ReturnRelaysTargetingPlanet(PlanetData planet)
        {
            if (planet == null || planet.star == null || GameMain.spaceSector == null)
            {
                return;
            }

            EnemyDFHiveSystem hive = GameMain.spaceSector.dfHives[planet.star.index];
            while (hive != null)
            {
                ReturnRelaysTargetingPlanet(hive, planet.astroId);
                hive = hive.nextSibling;
            }
        }

        private static void ReturnRelaysTargetingPlanet(EnemyDFHiveSystem hive, int planetAstroId)
        {
            if (hive == null || hive.relays == null)
            {
                return;
            }

            for (int i = hive.relays.cursor - 1; i > 0; i--)
            {
                DFRelayComponent relay = hive.relays.buffer[i];
                if (relay == null || relay.id != i || (relay.targetAstroId != planetAstroId && relay.searchAstroId != planetAstroId))
                {
                    continue;
                }

                try
                {
                    if (ReturnRelay(relay))
                    {
                        hive.relayNeutralizedCounter++;
                    }
                }
                catch (Exception)
                {
                    LogInfo("error to return relay " + relay.id);
                }
            }
        }

        private static bool ReturnRelay(DFRelayComponent relay)
        {
            if (relay.direction < 0)
            {
                relay.searchAstroId = 0;
                relay.ResetSearchStates();
                relay.ResetTargetedMarker();
                return false;
            }

            LogInfo("return relay -> relay.id: " + relay.id + ", enemyId: " + relay.enemyId);
            relay.searchAstroId = 0;

            if (relay.stage == 2)
            {
                relay.LeaveBase();
                return true;
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

        private static void ClearGroundBaseFormations(DFGBaseComponent baseComponent)
        {
            EnemyFormation[] formations = baseComponent.forms;
            for (int i = 0; i < formations.Length; i++)
            {
                if (formations[i].unitCount <= 0)
                {
                    continue;
                }

                try
                {
                    LogInfo("clear DFGBaseComponent:" + baseComponent.id + " -> form count: " + formations[i].unitCount);
                    formations[i].Clear();
                }
                catch (Exception)
                {
                    LogInfo("error to clear form " + i);
                }
            }
        }

        private static void ClearGroundWreckage(PlanetData planet)
        {
            WreckageContainer wreckageContainer = planet?.factoryModel?.wreckageContainer;
            if (wreckageContainer == null || wreckageContainer.fragments == null)
            {
                return;
            }

            for (int i = wreckageContainer.cursor - 1; i > 0; i--)
            {
                try
                {
                    wreckageContainer.RemoveFragment(i);
                }
                catch (Exception)
                {
                    LogInfo("error to remove wreckage fragment " + i);
                }
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
