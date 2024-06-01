using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using BepInEx.Configuration;
using System.IO;
using Steamworks;
using System.Collections.Generic;
using UnityEngine.UI;
using UXAssist.UI;
using UXAssist.Common;
using UXAssist;
using System.Linq;


namespace HardFog
{
    [BepInPlugin("me.liantian.plugin.HardFog", "HardFog", "0.0.1")]
    [BepInProcess("DSPGAME.exe")]
    public class HardFog : BaseUnityPlugin
    {
        private static ConfigEntry<bool> MoreFrequentRelays;
        //private static ConfigEntry<bool> HiveMoreEnergy;
        //private static ConfigEntry<bool> HiveLessEnergy;
        //private static ConfigEntry<bool> BaseMoreEnergy;
        //private static ConfigEntry<bool> BaseLessEnergy;
        private static ConfigEntry<bool> DysonSphereDebuffImmunity;
        private static ConfigEntry<bool> CoreIsInvincible;
        private static ConfigEntry<bool> SuperRayReception;

        public new static ManualLogSource Logger;

        // UXAssist面板
        private static RectTransform _windowTrans;


        public void Awake()
        {
            HardFog.MoreFrequentRelays = base.Config.Bind<bool>("FogCheats", "MoreFrequentRelays", false, "巢穴更频繁的发送的中继站");
            //HardFog.HiveMoreEnergy = base.Config.Bind<bool>("FogCheats", "HiveMoreEnergy", false, "黑雾巢穴获得更多的能量");
            //HardFog.HiveLessEnergy = base.Config.Bind<bool>("FogCheats", "HiveLessEnergy", false, "黑雾巢穴获得更少的能量");
            //HardFog.BaseMoreEnergy = base.Config.Bind<bool>("FogCheats", "BaseMoreEnergy", false, "行星基地获得更多的能量");
            //HardFog.BaseLessEnergy = base.Config.Bind<bool>("FogCheats", "BaseLessEnergy", false, "行星基地获得更少的能量");
            HardFog.DysonSphereDebuffImmunity = base.Config.Bind<bool>("Cheats", "DysonSphereDebuffImmunity", false, "黑雾无法偷电，因为不偷也很强大了。");
            HardFog.CoreIsInvincible = base.Config.Bind<bool>("QOL", "CoreIsInvincible", true, "不攻击巢穴核心。");
            HardFog.SuperRayReception = base.Config.Bind<bool>("Cheats", "SuperRayReception", false, "射线接收器满速接收。");
            HardFog.Logger = base.Logger;
            Harmony.CreateAndPatchAll(typeof(HardFog));


            I18N.Add("Clear the enemies on the current planet", "Clear the enemies on the current planet", "清理当前星球的黑雾");
            I18N.Add("Clear the space enemies of the current star", "Clear the space enemies of the current star", "清理当前恒星的太空黑雾");
            I18N.Add("Fill the galaxy with Hive", "Fill the galaxy with Hive", "为所有星系填满黑雾巢穴");
            I18N.Add("Reset the Hive in the current star", "Reset the Hive in the current star", "为当前恒星重置黑雾巢穴");
            I18N.Apply();

            MyConfigWindow.OnUICreated += CreateUI;
            HardFog.Logger.LogInfo("HardFog 初始化");


        }



        private static void CreateUI(MyConfigWindow wnd, RectTransform trans)
        {
            _windowTrans = trans;
            // General tab
            var x = 0f;
            var y = 10f;
            wnd.AddSplitter(trans, 10f);
            wnd.AddTabGroup(trans, "Hard fog", "tab-group-hard-fog");
            var tab1 = wnd.AddTab(_windowTrans, "General");
            wnd.AddButton(x, y, 200, tab1, "Clear the enemies on the current planet", 16, "button-clear-planet", HardFog.ClearPlanetEnemies);
            y += 36f;
            wnd.AddButton(x, y, 200, tab1, "Clear the space enemies of the current star", 16, "button-clear-star", HardFog.ClearStarEnemies);
            y += 36f;
            wnd.AddButton(x, y, 200, tab1, "Fill the galaxy with Hive", 16, "button-fill-hive", HardFog.StarsFillHive);
            y += 36f;
            wnd.AddButton(x, y, 200, tab1, "Reset the Hive in the current star", 16, "button-reset-hive", HardFog.StarResetHive);
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(EnemyDFHiveSystem), "GameTickLogic")]
        public static void EnemyDFHiveSystemPreGameTickLogic(EnemyDFHiveSystem __instance, long gameTick)
        {
            int num = (int)(gameTick % 6000L);

            // 中继器更加频繁
            if ((num == 0) && (HardFog.MoreFrequentRelays.Value))
            {
                __instance.relayNeutralizedCounter = 0;
                //HardFog.Logger.LogInfo("清空relayNeutralizedCounter");
            }

        }

        // 中继器更加频繁
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DFRelayComponent), "RelaySailLogic")]
        public static void DFRelayComponentRelaySailLogic(DFRelayComponent __instance, ref bool keyFrame)
        {
            if (HardFog.MoreFrequentRelays.Value)
            {
                keyFrame = true;
            }

        }



        //// 增加黑雾巢穴能量
        //[HarmonyPrefix]
        //[HarmonyPatch(typeof(DFSCoreComponent), "LogicTick")]
        //public static void DFSCoreComponentLogicTick(DFSCoreComponent __instance, EnemyDFHiveSystem hive)
        //{
        //    if ((HardFog.HiveMoreEnergy.Value) && (hive.evolve.waveTicks > 0) && (hive.starData.id != hive.galaxy.birthStarId))
        //    {
        //        ref EnemyBuilderComponent ptr = ref hive.builders.buffer[__instance.builderId];
        //        ptr.matter += (int)(((double)ptr.maxMatter * 2 - (double)ptr.matter) * 0.05);
        //        ptr.energy += (int)(((double)ptr.maxEnergy * 2 - (double)ptr.energy) * 0.05);

        //    }
        //    else if (HardFog.HiveLessEnergy.Value)
        //    {
        //        ref EnemyBuilderComponent ptr = ref hive.builders.buffer[__instance.builderId];
        //        ptr.matter -= (int)(((double)ptr.matter - (double)ptr.minMatter) * 0.05);
        //        ptr.energy -= (int)(((double)ptr.energy - (double)ptr.minEnergy) * 0.05); ;
        //    }

        //    ref EnemyData ptr4 = ref hive.sector.enemyPool[__instance.enemyId];
        //    if (HardFog.CoreIsInvincible.Value)
        //    {

        //        ptr4.isInvincible = true;
        //    }
        //    else
        //    {
        //        ptr4.isInvincible = false;
        //    }


        //}

        //// 增加基地核心的能量
        //[HarmonyPrefix]
        //[HarmonyPatch(typeof(DFGBaseComponent), "LogicTick")]
        //public static void DFGBaseComponentLogicTick(DFGBaseComponent __instance, ref EnemyBuilderComponent builder)
        //{
        //    if ((HardFog.BaseMoreEnergy.Value && (__instance.turboTicks > 0)))
        //    {
        //        builder.matter += (int)(((double)builder.maxMatter - (double)builder.matter) * 0.05);
        //        builder.energy += (int)(((double)builder.maxEnergy - (double)builder.energy) * 0.05);
        //    }
        //    else if (HardFog.BaseLessEnergy.Value)
        //    {
        //        builder.matter -= (int)(((double)builder.matter - (double)builder.minMatter) * 0.05);
        //        builder.energy -= (int)(((double)builder.matter - (double)builder.minEnergy) * 0.05);
        //    }

        //}


        // 去掉电力偷取
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DysonSphere), "energyDFHivesDebuffCoef", MethodType.Getter)]
        public static bool EnergyDFHivesDebuffCoef_Patch(ref double __result)
        {
            if (HardFog.DysonSphereDebuffImmunity.Value)
            {
                __result = 1.0;
                return false;

            }
            return true;
        }


        public static void ClearPlanetEnemies()
        {
            var player = GameMain.mainPlayer;
            if (player == null) return;
            var planet = GameMain.localPlanet;

            HardFog.Logger.LogInfo("planet.name: " + planet.name);

            int planetastroid = planet.astroId;
            HardFog.Logger.LogInfo("astroid" + planetastroid);

            PlanetFactory planetFactory = GameMain.data.GetOrCreateFactory(planet);
            CombatStat[] combatStatsbuffer = planetFactory.skillSystem.combatStats.buffer;

            ObjectPool<DFGBaseComponent> bases = planetFactory.enemySystem.bases;

            for (int i = 1; i < bases.cursor; i++)
            {
                DFGBaseComponent dfgbaseComponent = bases.buffer[i];
                EnemyFormation[] forms = dfgbaseComponent.forms;
                for (int j = 0; j < forms.Length; j++)
                {
                    if (forms[j].unitCount > 0)
                    {
                        HardFog.Logger.LogInfo("clear DFGBaseComponent:" + dfgbaseComponent.id + "  -> form count ： " + forms[j].unitCount);
                        forms[j].Clear();
                    }

                }
            }


            for (var i = planetFactory.enemyCursor - 1; i > 0; i--)
            {
                ref var enemyData = ref planetFactory.enemyPool[i];
                //if (enemydata.id != i) continue;
                if (enemyData.id != i) continue;

                HardFog.Logger.LogInfo("KillEnemyFinally -> enemyData.id： " + enemyData.id);

                var combatStatId = enemyData.combatStatId;
                planetFactory.skillSystem.OnRemovingSkillTarget(combatStatId, combatStatsbuffer[combatStatId].originAstroId, ETargetType.CombatStat);
                planetFactory.skillSystem.combatStats.Remove(combatStatId);
                planetFactory.KillEnemyFinally(player, i, ref CombatStat.empty);

            }

            HardFog.Logger.LogInfo("------------------------------------");
            HardFog.Logger.LogInfo("planetFactory.craftPool.Length " + planetFactory.craftPool.Length);

            SpaceSector spaceSector = GameMain.spaceSector;

            for (var i = spaceSector.enemyCursor - 1; i > 0; i--)
            {
                ref var enemyData = ref spaceSector.enemyPool[i];
                //if (enemydata.id != i) continue;
                if (enemyData.id != i) continue;

                if (enemyData.astroId != planet.id) continue;
                HardFog.Logger.LogInfo("KillEnemyFinal -> enemyData.id： " + enemyData.id);
                spaceSector.KillEnemyFinal(enemyData.id, ref CombatStat.empty);

            }

        }


        public static void ClearStarEnemies()
        {
            var player = GameMain.mainPlayer;
            if (player == null) return;
            var planet = GameMain.localPlanet;
            var star = GameMain.localStar;
            SpaceSector spaceSector = GameMain.spaceSector;
            // 待杀掉的ID
            List<int> needKillEnemyIds = new List<int>();
            // 杀掉飞行中的中继站
            for (var i = spaceSector.enemyCursor - 1; i > 0; i--)
            {
                ref var enemyData = ref spaceSector.enemyPool[i];
                //if (enemydata.id != i) continue;
                if (enemyData.id != i) continue;
                if (enemyData.isInvincible) continue;
                if (enemyData.dfSCoreId > 0) continue;
                if (enemyData.astroId != star.astroId) continue;

                HardFog.Logger.LogInfo("active replys->" + enemyData.id);
                needKillEnemyIds.Add(enemyData.id);
                //spaceSector.KillEnemyFinal(enemyData.id, ref CombatStat.empty);
            }

            EnemyDFHiveSystem enemyDFHiveSystem = spaceSector.dfHives[star.index];

            while (enemyDFHiveSystem != null)
            {
                if (enemyDFHiveSystem.isAlive)
                {
                    HardFog.Logger.LogInfo("enemyDFHiveSystem.hiveAstroId " + enemyDFHiveSystem.hiveAstroId);
                    HardFog.Logger.LogInfo("enemyDFHiveSystem.rootEnemyId " + enemyDFHiveSystem.rootEnemyId);

                    // 
                    for (var i = enemyDFHiveSystem.units.cursor - 1; i > 0; i--)
                    {

                        int enemyId = enemyDFHiveSystem.units.buffer[i].enemyId;
                        if (enemyId > 0)
                        {
                            ref var enemyData = ref spaceSector.enemyPool[enemyId];
                            if ((!enemyData.isInvincible) && (enemyData.dfSCoreId <= 0))
                            {
                                HardFog.Logger.LogInfo("units->" + enemyId);
                                needKillEnemyIds.Add(enemyId);
                                //spaceSector.KillEnemyFinal(enemyId, ref CombatStat.empty);
                            }

                        }
                    }
                    // 
                    for (var i = enemyDFHiveSystem.tinders.cursor - 1; i > 0; i--)
                    {
                        int enemyId = enemyDFHiveSystem.tinders.buffer[i].enemyId;
                        if (enemyId > 0)
                        {
                            ref var enemyData = ref spaceSector.enemyPool[enemyId];
                            if ((!enemyData.isInvincible) && (enemyData.dfSCoreId <= 0))
                            {
                                HardFog.Logger.LogInfo("tinders->" + enemyId);
                                needKillEnemyIds.Add(enemyId);
                                //spaceSector.KillEnemyFinal(enemyId, ref CombatStat.empty);
                            }
                        }
                    }

                    for (var i = enemyDFHiveSystem.idleRelayCount - 1; i > 0; i--)
                    {
                        DFRelayComponent dfrelayComponent = enemyDFHiveSystem.relays.buffer[enemyDFHiveSystem.idleRelayIds[i]];
                        int enemyId = dfrelayComponent.enemyId;
                        if (enemyId > 0)
                        {
                            ref var enemyData = ref spaceSector.enemyPool[enemyId];
                            if ((!enemyData.isInvincible) && (enemyData.dfSCoreId <= 0))
                            {
                                HardFog.Logger.LogInfo("idle relays->" + enemyId);
                                needKillEnemyIds.Add(enemyId);
                                //spaceSector.KillEnemyFinal(enemyId, ref CombatStat.empty);
                            }
                        }
                    }

                    //
                    for (var i = enemyDFHiveSystem.turrets.cursor - 1; i > 0; i--)
                    {
                        int enemyId = enemyDFHiveSystem.turrets.buffer[i].enemyId;

                        if (enemyId > 0)
                        {
                            ref var enemyData = ref spaceSector.enemyPool[enemyId];
                            if ((!enemyData.isInvincible) && (enemyData.dfSCoreId <= 0))
                            {
                                HardFog.Logger.LogInfo("turrets->" + enemyId);
                                needKillEnemyIds.Add(enemyId);
                                //spaceSector.KillEnemyFinal(enemyId, ref CombatStat.empty);
                            }
                        }
                    }
                    // 
                    for (var i = enemyDFHiveSystem.gammas.cursor - 1; i > 0; i--)
                    {
                        int enemyId = enemyDFHiveSystem.gammas.buffer[i].enemyId;
                        if (enemyId > 0)
                        {
                            ref var enemyData = ref spaceSector.enemyPool[enemyId];
                            if ((!enemyData.isInvincible) && (enemyData.dfSCoreId <= 0))
                            {
                                HardFog.Logger.LogInfo("gammas->" + enemyId);
                                needKillEnemyIds.Add(enemyId);
                                //spaceSector.KillEnemyFinal(enemyId, ref CombatStat.empty);
                            }
                        }
                    }
                    // 
                    for (var i = enemyDFHiveSystem.replicators.cursor - 1; i > 0; i--)
                    {
                        int enemyId = enemyDFHiveSystem.replicators.buffer[i].enemyId;
                        if (enemyId > 0)
                        {
                            ref var enemyData = ref spaceSector.enemyPool[enemyId];
                            if ((!enemyData.isInvincible) && (enemyData.dfSCoreId <= 0))
                            {
                                HardFog.Logger.LogInfo("replicators->" + enemyId);
                                needKillEnemyIds.Add(enemyId);
                                //spaceSector.KillEnemyFinal(enemyId, ref CombatStat.empty);
                            }
                        }
                    }
                    // 
                    for (var i = enemyDFHiveSystem.connectors.cursor - 1; i > 0; i--)
                    {
                        int enemyId = enemyDFHiveSystem.connectors.buffer[i].enemyId;
                        if (enemyId > 0)
                        {
                            ref var enemyData = ref spaceSector.enemyPool[enemyId];
                            if ((!enemyData.isInvincible) && (enemyData.dfSCoreId <= 0))
                            {
                                HardFog.Logger.LogInfo("connectors->" + enemyId);
                                needKillEnemyIds.Add(enemyId);
                                //spaceSector.KillEnemyFinal(enemyId, ref CombatStat.empty);
                            }
                        }
                    }
                    // 
                    for (var i = enemyDFHiveSystem.nodes.cursor - 1; i > 0; i--)
                    {

                        int enemyId = enemyDFHiveSystem.nodes.buffer[i].enemyId;
                        if (enemyId > 0)
                        {
                            ref var enemyData = ref spaceSector.enemyPool[enemyId];
                            if ((!enemyData.isInvincible) && (enemyData.dfSCoreId <= 0))
                            {
                                HardFog.Logger.LogInfo("nodes->" + enemyId);
                                needKillEnemyIds.Add(enemyId);
                                //spaceSector.KillEnemyFinal(enemyId, ref CombatStat.empty);
                            }
                        }
                    }

                    for (var i = enemyDFHiveSystem.builders.cursor - 1; i > 0; i--)
                    {
                        int enemyId = enemyDFHiveSystem.builders.buffer[i].enemyId;
                        if (enemyId > 0)
                        {
                            ref var enemyData = ref spaceSector.enemyPool[enemyId];
                            if ((!enemyData.isInvincible) && (enemyData.dfSCoreId <= 0))
                            {
                                HardFog.Logger.LogInfo("builders->" + enemyId);
                                needKillEnemyIds.Add(enemyId);
                                //spaceSector.KillEnemyFinal(enemyId, ref CombatStat.empty);
                            }
                        }

                    }

                    // 清空舰队
                    EnemyFormation[] forms = enemyDFHiveSystem.forms;
                    for (int j = 0; j < forms.Length; j++)
                    {
                        if (forms[j].unitCount > 0)
                        {
                            HardFog.Logger.LogInfo("clear form " + forms[j].unitCount);
                            forms[j].Clear();
                        }

                    }

                    // 清空核心能量
                    for (var i = enemyDFHiveSystem.cores.cursor - 1; i > 0; i--)
                    {

                        ref EnemyBuilderComponent ptr = ref enemyDFHiveSystem.builders.buffer[enemyDFHiveSystem.cores.buffer[i].builderId];
                        ptr.matter = 0;
                        ptr.energy = 0;


                    }




                }
                // 下一个巢穴
                enemyDFHiveSystem = enemyDFHiveSystem.nextSibling;
            }

            List<int> distinctNeedKillEnemyIds = needKillEnemyIds.Distinct().ToList();
            foreach (int enemyId in distinctNeedKillEnemyIds)
            {
                try
                {
                    spaceSector.KillEnemyFinal(enemyId, ref CombatStat.empty);
                }
                catch (Exception ex)
                {
                    HardFog.Logger.LogInfo("error to kill " + enemyId);
                }

            }

        }
        public static void StarsFillHive()
        {
            var player = GameMain.mainPlayer;
            if (player == null) return;
            var galaxy = GameMain.galaxy;
            SpaceSector spaceSector = GameMain.spaceSector;

            for (int i = 0; i < galaxy.starCount; i++)
            {
                StarData star = galaxy.stars[i];

                int maxHiveCount = star.maxHiveCount;
                if (maxHiveCount > 8)
                {
                    maxHiveCount = 8;
                }

                EnemyDFHiveSystem enemyDFHiveSystem = spaceSector.dfHives[star.index];
                int curHiveCount = 0;
                while (enemyDFHiveSystem != null)
                {
                    curHiveCount++;
                    enemyDFHiveSystem = enemyDFHiveSystem.nextSibling;
                }

                for (int j = 0; j < (maxHiveCount - curHiveCount); j++)

                {
                    EnemyDFHiveSystem hive = spaceSector.TryCreateNewHive(star);

                    if (hive != null)
                    {
                        HardFog.Logger.LogInfo(star.displayName + " Add 1 hive");
                        hive.isEmpty = false;
                        hive.matterStatComplete = false;
                        float num = Mathf.Pow(1f - hive.starData.safetyFactor, 1.4f);
                        int num2 = RandomTable.Integer(ref hive.rtseed, 600);
                        float initialGrowth = hive.history.combatSettings.initialGrowth;
                        hive.ticks = 600 + (int)(num * 72000f) + num2;
                        hive.ticks = (int)((float)hive.ticks * initialGrowth);
                        hive.ticks = hive.InitialGrowthToTicks(hive.ticks);

                        if (hive.ticks < 4)
                        {
                            hive.ticks = 4;
                        }
                        if (initialGrowth > 1f)
                        {
                            float num3 = initialGrowth - 1f;
                            hive.evolve.rankBase = (int)((float)hive.evolve.rankBase * initialGrowth + 0.5f);
                            hive.evolve.rankBase = hive.evolve.rankBase + (int)(num3 * 40f + 0.5f);
                            if (hive.evolve.rankBase > 127)
                            {
                                hive.evolve.rankBase = 127;
                            }
                        }
                        else if (initialGrowth < 1f)
                        {
                            hive.evolve.rankBase = (int)((float)hive.evolve.rankBase * Mathf.Lerp(initialGrowth, 1f, 0.5f) + 0.5f);
                        }
                        hive.isCarrierRealized = (hive.starData.id == hive.galaxy.birthStarId);
                        hive.tindersInTransit = 0;

                    }
                }


            }
        }

        public static void StarResetHive()
        {
            var player = GameMain.mainPlayer;
            if (player == null) return;
            var planet = GameMain.localPlanet;
            var star = GameMain.localStar;
            SpaceSector spaceSector = GameMain.spaceSector;


            EnemyDFHiveSystem enemyDFHiveSystem = spaceSector.dfHives[star.index];

            while (enemyDFHiveSystem != null)
            {
                enemyDFHiveSystem.SetForNewGame();
                enemyDFHiveSystem = enemyDFHiveSystem.nextSibling;
            }
        }

        // 星球
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DysonSphere), "energyRespCoef", MethodType.Getter)]
        public static bool energyRespCoef_Patch(ref float __result)
        {
            if (HardFog.SuperRayReception.Value)
            {
                __result = 1f;
                return false;
            }
            return true;
        }

    }
}

