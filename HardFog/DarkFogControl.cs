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

            ReturnRelaysTargetingPlanet(planet);

            for (int i = 1; i < bases.cursor; i++)
            {
                DFGBaseComponent baseComponent = bases.buffer[i];
                if (baseComponent == null)
                {
                    continue;
                }

                ClearGroundBaseFormations(baseComponent);
            }

            for (var i = planetFactory.enemyCursor - 1; i > 0; i--)
            {
                if (planetFactory.enemyPool[i].id != i)
                {
                    continue;
                }

                try
                {
                    ClearGroundEnemy(planetFactory, combatStatsBuffer, i);
                }
                catch (Exception)
                {
                    LogInfo("error to clear enemy " + i);
                }
            }

            RemoveResidualGroundBaseRecords(planetFactory, bases);

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

        private static void ClearGroundEnemy(PlanetFactory planetFactory, CombatStat[] combatStatsBuffer, int enemyId)
        {
            ref EnemyData enemyData = ref planetFactory.enemyPool[enemyId];
            int combatStatId = enemyData.combatStatId;
            if (combatStatId > 0)
            {
                planetFactory.skillSystem.OnRemovingSkillTarget(combatStatId, combatStatsBuffer[combatStatId].originAstroId, ETargetType.CombatStat);
                planetFactory.skillSystem.combatStats.Remove(combatStatId);
                enemyData.combatStatId = 0;
            }

            if (enemyData.dynamic)
            {
                LogInfo("RemoveEnemyWithComponents -> enemyData.id: " + enemyData.id);
                planetFactory.RemoveEnemyWithComponents(enemyId);
                return;
            }

            LogInfo("KillEnemyFinally -> enemyData.id: " + enemyData.id);
            planetFactory.KillEnemyFinally(enemyId, ref CombatStat.empty);
        }

        private static void RemoveResidualGroundBaseRecords(PlanetFactory planetFactory, ObjectPool<DFGBaseComponent> bases)
        {
            if (planetFactory == null || bases == null || bases.buffer == null)
            {
                return;
            }

            List<int> baseIds = new List<int>();
            for (int i = 1; i < bases.cursor; i++)
            {
                DFGBaseComponent baseComponent = bases.buffer[i];
                if (baseComponent != null && baseComponent.id == i)
                {
                    baseIds.Add(i);
                }
            }

            foreach (int baseId in baseIds)
            {
                if (baseId <= 0 || baseId >= bases.cursor)
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
                    RemoveResidualGroundBaseRecord(planetFactory, baseComponent);
                }
                catch (Exception)
                {
                    LogInfo("error to remove DFGBaseComponent " + baseId);
                }
            }
        }

        private static void RemoveResidualGroundBaseRecord(PlanetFactory planetFactory, DFGBaseComponent baseComponent)
        {
            int baseId = baseComponent.id;
            int enemyId = baseComponent.enemyId;
            LogInfo("remove DFGBaseComponent -> base.id: " + baseId + ", enemyId: " + enemyId + ", ruinId: " + baseComponent.ruinId);

            ClearRelaysTargetingGroundBase(planetFactory.planet, baseId);

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

        private static void ClearRelaysTargetingGroundBase(PlanetData planet, int baseId)
        {
            if (planet == null || planet.star == null || GameMain.spaceSector == null)
            {
                return;
            }

            EnemyDFHiveSystem hive = GameMain.spaceSector.dfHives[planet.star.index];
            while (hive != null)
            {
                ClearRelaysTargetingGroundBase(hive, planet.astroId, baseId);
                hive = hive.nextSibling;
            }
        }

        private static void ClearRelaysTargetingGroundBase(EnemyDFHiveSystem hive, int planetAstroId, int baseId)
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

                LogInfo("clear relay ground base reference -> relay.id: " + relay.id + ", baseId: " + baseId);
                relay.ClearBaseReferences();
                relay.baseId = 0;
                relay.baseState = 0;
                relay.baseTicks = 0;
                relay.baseRespawnCD = 0;
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
                if (relay == null || relay.id != i || relay.targetAstroId != planetAstroId)
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
                return false;
            }

            LogInfo("return relay -> relay.id: " + relay.id + ", enemyId: " + relay.enemyId);

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
