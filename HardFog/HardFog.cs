using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UXAssist.Common;
using UXAssist.UI;

namespace HardFog
{
    [BepInPlugin("me.liantian.plugin.HardFog", "HardFog", "0.0.7")]
    [BepInDependency(UXAssist.PluginInfo.PLUGIN_GUID)]
    public class HardFog : BaseUnityPlugin
    {
        private static UIButton buttonClearPlanet;
        private static UIButton buttonClearStar;
        private static UIButton buttonFillHive;

        public new static ManualLogSource Logger;

        public void Awake()
        {
            Logger = base.Logger;

            I18N.Add("clear_current_planet", "clear all enemies on current planet", "清理当前星球的黑雾");
            I18N.Add("clear_current_star", "clear all enemies on current star", "清理当前恒星的太空黑雾");
            I18N.Add("fill_hive", "Fill the galaxy with Hive", "为整个星系填满黑雾巢穴");
            I18N.Apply();

            MyConfigWindow.OnUICreated += CreateUI;
            Logger.LogInfo("HardFog 初始化");
        }

        private static void CreateUI(MyConfigWindow wnd, RectTransform trans)
        {
            var x = 10f;
            var y = 10f;

            wnd.AddSplitter(trans, 10f);
            wnd.AddTabGroup(trans, "Hard fog", "tab-group-hard-fog");
            var tab = wnd.AddTab(trans, "General");

            buttonClearPlanet = wnd.AddButton(x, y, 240, tab, "clear_current_planet", 16, "button-clear-planet", ClearCurrentPlanetEnemies);
            y += 36f;
            buttonClearStar = wnd.AddButton(x, y, 240, tab, "clear_current_star", 16, "button-clear-star", ClearCurrentStarEnemies);
            y += 36f;
            buttonFillHive = wnd.AddButton(x, y, 240, tab, "fill_hive", 16, "button-fill-hive", StarsFillHive);
        }

        private static void ClearCurrentPlanetEnemies()
        {
            if (buttonClearPlanet != null)
            {
                buttonClearPlanet.enabled = false;
            }

            try
            {
                SaveCurrentGame();

                if (GameMain.mainPlayer == null)
                {
                    return;
                }

                var planet = GameMain.localPlanet;
                if (planet == null)
                {
                    return;
                }

                ClearPlanetEnemies(planet);
            }
            finally
            {
                if (buttonClearPlanet != null)
                {
                    buttonClearPlanet.enabled = true;
                }
            }
        }

        private static void ClearPlanetEnemies(PlanetData planet)
        {
            Logger.LogInfo("planet.name: " + planet.name);
            Logger.LogInfo("astroId: " + planet.astroId);

            PlanetFactory planetFactory = GameMain.data.GetOrCreateFactory(planet);
            CombatStat[] combatStatsBuffer = planetFactory.skillSystem.combatStats.buffer;
            ObjectPool<DFGBaseComponent> bases = planetFactory.enemySystem.bases;

            for (int i = 1; i < bases.cursor; i++)
            {
                DFGBaseComponent dfgBaseComponent = bases.buffer[i];
                if (dfgBaseComponent == null)
                {
                    continue;
                }

                EnemyFormation[] forms = dfgBaseComponent.forms;
                for (int j = 0; j < forms.Length; j++)
                {
                    if (forms[j].unitCount <= 0)
                    {
                        continue;
                    }

                    try
                    {
                        Logger.LogInfo("clear DFGBaseComponent:" + dfgBaseComponent.id + " -> form count: " + forms[j].unitCount);
                        forms[j].Clear();
                    }
                    catch (Exception)
                    {
                        Logger.LogInfo("error to clear form " + j);
                    }
                }
            }

            for (var i = planetFactory.enemyCursor - 1; i > 0; i--)
            {
                ref var enemyData = ref planetFactory.enemyPool[i];
                if (enemyData.id != i)
                {
                    continue;
                }

                try
                {
                    Logger.LogInfo("KillEnemyFinally -> enemyData.id: " + enemyData.id);

                    var combatStatId = enemyData.combatStatId;
                    if (combatStatId > 0)
                    {
                        planetFactory.skillSystem.OnRemovingSkillTarget(combatStatId, combatStatsBuffer[combatStatId].originAstroId, ETargetType.CombatStat);
                        planetFactory.skillSystem.combatStats.Remove(combatStatId);
                    }

                    planetFactory.KillEnemyFinally(i, ref CombatStat.empty);
                }
                catch (Exception)
                {
                    Logger.LogInfo("error to kill enemy " + enemyData.id);
                }
            }

            SpaceSector spaceSector = GameMain.spaceSector;

            for (var i = spaceSector.enemyCursor - 1; i > 0; i--)
            {
                ref var enemyData = ref spaceSector.enemyPool[i];
                if (enemyData.id != i)
                {
                    continue;
                }

                if (enemyData.astroId != planet.id)
                {
                    continue;
                }

                Logger.LogInfo("KillEnemyFinal -> enemyData.id: " + enemyData.id);
                spaceSector.KillEnemyFinal(enemyData.id, ref CombatStat.empty);
            }
        }

        private static void ClearCurrentStarEnemies()
        {
            if (buttonClearStar != null)
            {
                buttonClearStar.enabled = false;
            }

            try
            {
                SaveCurrentGame();

                if (GameMain.mainPlayer == null)
                {
                    return;
                }

                var star = GameMain.localStar;
                if (star == null)
                {
                    return;
                }

                ClearStarEnemies(star);
            }
            finally
            {
                if (buttonClearStar != null)
                {
                    buttonClearStar.enabled = true;
                }
            }
        }

        private static void ClearStarEnemies(StarData star)
        {
            SpaceSector spaceSector = GameMain.spaceSector;
            List<int> needKillEnemyIds = new List<int>();

            for (var i = spaceSector.enemyCursor - 1; i > 0; i--)
            {
                ref var enemyData = ref spaceSector.enemyPool[i];
                if (enemyData.id != i || enemyData.isInvincible || enemyData.dfSCoreId > 0 || enemyData.astroId != star.astroId)
                {
                    continue;
                }

                Logger.LogInfo("active relays -> " + enemyData.id);
                needKillEnemyIds.Add(enemyData.id);
            }

            EnemyDFHiveSystem hive = spaceSector.dfHives[star.index];

            while (hive != null)
            {
                if (hive.isAlive)
                {
                    Logger.LogInfo("hiveAstroId " + hive.hiveAstroId);
                    Logger.LogInfo("rootEnemyId " + hive.rootEnemyId);

                    AddKillableEnemyId(spaceSector, needKillEnemyIds, hive.units, component => component.enemyId);
                    AddKillableEnemyId(spaceSector, needKillEnemyIds, hive.tinders, component => component.enemyId);
                    AddIdleRelayEnemyIds(spaceSector, needKillEnemyIds, hive);
                    AddKillableEnemyId(spaceSector, needKillEnemyIds, hive.turrets, component => component.enemyId);
                    AddKillableEnemyId(spaceSector, needKillEnemyIds, hive.gammas, component => component.enemyId);
                    AddKillableEnemyId(spaceSector, needKillEnemyIds, hive.replicators, component => component.enemyId);
                    AddKillableEnemyId(spaceSector, needKillEnemyIds, hive.connectors, component => component.enemyId);
                    AddKillableEnemyId(spaceSector, needKillEnemyIds, hive.nodes, component => component.enemyId);
                    AddKillableEnemyId(spaceSector, needKillEnemyIds, hive.builders, component => component.enemyId);

                    ClearHiveForms(hive);
                    EmptyHiveCoreEnergy(hive);
                }

                hive = hive.nextSibling;
            }

            foreach (int enemyId in needKillEnemyIds.Distinct())
            {
                try
                {
                    spaceSector.KillEnemyFinal(enemyId, ref CombatStat.empty);
                }
                catch (Exception)
                {
                    Logger.LogInfo("error to kill " + enemyId);
                }
            }

            SetCoreInvincible(star);
        }

        private static void AddKillableEnemyId<T>(SpaceSector spaceSector, List<int> enemyIds, DataPool<T> pool, Func<T, int> getEnemyId)
            where T : struct, IPoolElement
        {
            for (var i = pool.cursor - 1; i > 0; i--)
            {
                int enemyId = getEnemyId(pool.buffer[i]);
                if (CanKillEnemy(spaceSector, enemyId))
                {
                    enemyIds.Add(enemyId);
                }
            }
        }

        private static void AddIdleRelayEnemyIds(SpaceSector spaceSector, List<int> enemyIds, EnemyDFHiveSystem hive)
        {
            for (var i = hive.idleRelayCount - 1; i > 0; i--)
            {
                DFRelayComponent relay = hive.relays.buffer[hive.idleRelayIds[i]];
                if (CanKillEnemy(spaceSector, relay.enemyId))
                {
                    enemyIds.Add(relay.enemyId);
                }
            }
        }

        private static bool CanKillEnemy(SpaceSector spaceSector, int enemyId)
        {
            if (enemyId <= 0)
            {
                return false;
            }

            ref var enemyData = ref spaceSector.enemyPool[enemyId];
            return !enemyData.isInvincible && enemyData.dfSCoreId <= 0;
        }

        private static void ClearHiveForms(EnemyDFHiveSystem hive)
        {
            EnemyFormation[] forms = hive.forms;
            for (int i = 0; i < forms.Length; i++)
            {
                if (forms[i].unitCount <= 0)
                {
                    continue;
                }

                Logger.LogInfo("clear form " + forms[i].unitCount);
                forms[i].Clear();
            }
        }

        private static void EmptyHiveCoreEnergy(EnemyDFHiveSystem hive)
        {
            for (var i = hive.cores.cursor - 1; i > 0; i--)
            {
                ref EnemyBuilderComponent builder = ref hive.builders.buffer[hive.cores.buffer[i].builderId];
                builder.matter = 0;
                builder.energy = 0;
            }
        }

        private static void StarsFillHive()
        {
            if (buttonFillHive != null)
            {
                buttonFillHive.enabled = false;
            }

            try
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

                        Logger.LogInfo(star.displayName + " Add 1 hive");
                        hive.SetForNewGame();
                    }
                }
            }
            finally
            {
                if (buttonFillHive != null)
                {
                    buttonFillHive.enabled = true;
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

        private static void SetCoreInvincible(StarData star)
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
            string combinedString = string.Format("[{0}] {1}", seedString, currentTimeString);
            GameSave.SaveCurrentGame(combinedString);
        }
    }
}
