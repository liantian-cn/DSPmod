using System;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using BepInEx.Configuration;
using UXAssist.UI;
using UXAssist.Common;
using System.Linq;
using System.Collections;
using UnityEngine.Playables;


namespace HardFog
{
    [BepInPlugin("me.liantian.plugin.HardFog", "HardFog", "0.0.6")]
    [BepInDependency(UXAssist.PluginInfo.PLUGIN_GUID)]
    public class HardFog : BaseUnityPlugin
    {
        private static ConfigEntry<bool> MoreFrequentRelaysEnable;
        private static ConfigEntry<bool> DysonSphereDebuffImmunityEnable;
        private static ConfigEntry<bool> SuperRayReceptionEnable;
        private static ConfigEntry<bool> DFSCoreSuperEnergyEnable;
        private static ConfigEntry<bool> DFGBaseSuperEnergyEnable;

        public new static ManualLogSource Logger;

        // UXAssist面板
        private static RectTransform _windowTrans;


        public void Awake()
        {
            MoreFrequentRelaysEnable = Config.Bind("FogCheats", "MoreFrequentRelays", false, "巢穴更频繁的发送的中继站");
            DysonSphereDebuffImmunityEnable = Config.Bind("Cheats", "DysonSphereDebuffImmunity", false, "黑雾无法偷电");
            SuperRayReceptionEnable = Config.Bind("Cheats", "SuperRayReception", false, "射线接收器满速接收");
            DFSCoreSuperEnergyEnable = Config.Bind("FogCheats", "DFSCoreSuperEnergy", false, "玩家所在星系黑雾巢穴满能量");
            DFGBaseSuperEnergyEnable = Config.Bind("FogCheats", "DFGBaseSuperEnergy", false, "黑雾地面基地满能量");

            HardFog.Logger = base.Logger;
            Harmony.CreateAndPatchAll(typeof(HardFog));


            I18N.Add("Clear the enemies on the current planet", "Clear the enemies on the current planet", "清理当前星球的黑雾");
            I18N.Add("Clear the space enemies of the current star", "Clear the space enemies of the current star", "清理当前恒星的太空黑雾");
            I18N.Add("Fill the galaxy with Hive", "Fill the galaxy with Hive", "为所有星系填满黑雾巢穴");
            I18N.Add("Reset the Hive in the current star", "Reset the Hive in the current star", "为当前恒星重置黑雾巢穴");
            I18N.Add("More Frequent Relays", "The HIVE is sending out Relays more frequently.", "黑雾巢穴更频繁的发送中继站");
            I18N.Add("Set HiveCore Invincible", "The HiveCore in the current star is invulnerable to attacks", "当前恒星的黑雾巢穴核心无法被攻击");
            I18N.Add("Set HiveCore Vulnerable", "The HiveCore in the current star is vulnerable to attacks.", "当前恒星的黑雾巢穴核心可以被攻击");
            I18N.Add("Fog cannot steal electricity", "Fog cannot steal electricity", "黑雾无法偷取电力");
            I18N.Add("the ray receiver receives at full power", "ray receiver receives at full power", "射线接收器满接收");
            I18N.Add("The Hive in the current star get full energy and matter", "The Hive in the current star get full energy and matter", "当前恒星的黑雾巢穴满能量/物质");
            I18N.Add("The Fogbase get full energy and matter", "The Fog core on base get full energy and matter", "黑雾地面核心获得满能量/物质");
            I18N.Apply();

            MyConfigWindow.OnUICreated += CreateUI;

            MoreFrequentRelaysEnable.SettingChanged += (_, _) => MoreFrequentRelaysPatch.Enable(MoreFrequentRelaysEnable.Value);
            DysonSphereDebuffImmunityEnable.SettingChanged += (_, _) => DysonSphereDebuffImmunityPatch.Enable(DysonSphereDebuffImmunityEnable.Value);
            SuperRayReceptionEnable.SettingChanged += (_, _) => SuperRayReceptionPatch.Enable(SuperRayReceptionEnable.Value);
            DFSCoreSuperEnergyEnable.SettingChanged += (_, _) => DFSCoreSuperEnergyPatch.Enable(DFSCoreSuperEnergyEnable.Value);
            DFGBaseSuperEnergyEnable.SettingChanged += (_, _) => DFGBaseSuperEnergyPatch.Enable(DFGBaseSuperEnergyEnable.Value);
            HardFog.Logger.LogInfo("HardFog 初始化");


        }



        private static void CreateUI(MyConfigWindow wnd, RectTransform trans)
        {
            _windowTrans = trans;
            // General tab
            var x = 10f;
            var y = 10f;
            wnd.AddSplitter(trans, 10f);
            wnd.AddTabGroup(trans, "Hard fog", "tab-group-hard-fog");
            var tab1 = wnd.AddTab(_windowTrans, "General");
            wnd.AddButton(x, y, 200, tab1, "Clear the enemies on the current planet", 16, "button-clear-planet", ClearCurrentPlanetEnemies);
            y += 36f;
            wnd.AddButton(x, y, 200, tab1, "Clear the space enemies of the current star", 16, "button-clear-star", ClearStarEnemies);
            y += 36f;
            wnd.AddButton(x, y, 200, tab1, "Fill the galaxy with Hive", 16, "button-fill-hive", StarsFillHive);
            y += 36f;
            wnd.AddButton(x, y, 200, tab1, "Reset the Hive in the current star", 16, "button-reset-hive", StarResetHive);
            y += 36f;
            MyCheckBox.CreateCheckBox(x, y, tab1, HardFog.MoreFrequentRelaysEnable, "More Frequent Relays");
            y += 36f;
            wnd.AddButton(x, y, 200, tab1, "Set HiveCore Invincible", 12, "button-set-core-invincible", SetCoreInvincible);
            y += 36f;
            wnd.AddButton(x, y, 200, tab1, "Set HiveCore Vulnerable", 12, "button-set-core-vulnerable", SetCoreVulnerable);
            y += 36f;
            MyCheckBox.CreateCheckBox(x, y, tab1, HardFog.DysonSphereDebuffImmunityEnable, "Fog cannot steal electricity");
            y += 36f;
            MyCheckBox.CreateCheckBox(x, y, tab1, HardFog.SuperRayReceptionEnable, "the ray receiver receives at full power");
            y += 36f;
            MyCheckBox.CreateCheckBox(x, y, tab1, HardFog.DFSCoreSuperEnergyEnable, "The Hive in the current star get full energy and matter");
            y += 36f;
            MyCheckBox.CreateCheckBox(x, y, tab1, HardFog.DFGBaseSuperEnergyEnable, "The Fogbase get full energy and matter");


        }


        private static class MoreFrequentRelaysPatch
        {
            private static Harmony _patch;

            public static void Enable(bool enable)
            {
                if (enable)
                {
                    _patch = Harmony.CreateAndPatchAll(typeof(MoreFrequentRelaysPatch));
                    return;
                }
                _patch?.UnpatchSelf();
                _patch = null;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(EnemyDFHiveSystem), "GameTickLogic")]
            public static void EnemyDFHiveSystemPreGameTickLogic(EnemyDFHiveSystem __instance, long gameTick)
            {
                int num = (int)(gameTick % 6000L);
                __instance.relayNeutralizedCounter = 0;
            }

            // 中继器更加频繁
            [HarmonyPrefix]
            [HarmonyPatch(typeof(DFRelayComponent), "RelaySailLogic")]
            public static void DFRelayComponentRelaySailLogic(DFRelayComponent __instance, ref bool keyFrame)
            {
                keyFrame = true;
            }


        }

        private static class DysonSphereDebuffImmunityPatch
        {
            private static Harmony _patch;

            public static void Enable(bool enable)
            {
                if (enable)
                {
                    _patch = Harmony.CreateAndPatchAll(typeof(DysonSphereDebuffImmunityPatch));
                    return;
                }
                _patch?.UnpatchSelf();
                _patch = null;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(DysonSphere), "energyDFHivesDebuffCoef", MethodType.Getter)]
            public static bool EnergyDFHivesDebuffCoef_Patch(ref double __result)
            {

                __result = 1.0;
                return false;

            }


        }


        private static class SuperRayReceptionPatch
        {
            private static Harmony _patch;

            public static void Enable(bool enable)
            {
                if (enable)
                {
                    _patch = Harmony.CreateAndPatchAll(typeof(SuperRayReceptionPatch));
                    return;
                }
                _patch?.UnpatchSelf();
                _patch = null;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(DysonSphere), "energyRespCoef", MethodType.Getter)]
            public static bool energyRespCoef_Patch(ref float __result)
            {

                __result = 1f;
                return false;

            }


        }

        private static class DFSCoreSuperEnergyPatch
        {
            private static Harmony _patch;

            public static void Enable(bool enable)
            {
                if (enable)
                {
                    _patch = Harmony.CreateAndPatchAll(typeof(DFSCoreSuperEnergyPatch));
                    return;
                }
                _patch?.UnpatchSelf();
                _patch = null;
            }

            // 增加黑雾巢穴能量
            [HarmonyPrefix]
            [HarmonyPatch(typeof(DFSCoreComponent), "LogicTick")]
            public static void DFSCoreComponentLogicTick(DFSCoreComponent __instance, EnemyDFHiveSystem hive)
            {
                if (hive.isLocal)
                {
                    ref EnemyBuilderComponent ptr = ref hive.builders.buffer[__instance.builderId];
                    ptr.matter = ptr.maxMatter;
                    ptr.energy = ptr.maxEnergy;
                }
            }
        }


        private static class DFGBaseSuperEnergyPatch
        {
            private static Harmony _patch;

            public static void Enable(bool enable)
            {
                if (enable)
                {
                    _patch = Harmony.CreateAndPatchAll(typeof(DFGBaseSuperEnergyPatch));
                    return;
                }
                _patch?.UnpatchSelf();
                _patch = null;
            }
            // 增加基地核心的能量
            [HarmonyPrefix]
            [HarmonyPatch(typeof(DFGBaseComponent), "LogicTick")]
            public static void DFGBaseComponentLogicTick(DFGBaseComponent __instance, ref EnemyBuilderComponent builder)
            {

                builder.matter = builder.maxMatter;
                builder.energy = builder.maxEnergy;

            }
        }




        private static void ClearCurrentPlanetEnemies()
        {
            var player = GameMain.mainPlayer;
            if (player == null) return;
            var planet = GameMain.localPlanet;
            ClearPlanetEnemies(planet);
        }



        private static void ClearPlanetEnemies(PlanetData planet)
        {
            var player = GameMain.mainPlayer;
            if (player == null) return;


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




        private static void ClearStarEnemies()
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
                                //HardFog.Logger.LogInfo("units->" + enemyId);
                                needKillEnemyIds.Add(enemyId);
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
                                //HardFog.Logger.LogInfo("tinders->" + enemyId);
                                needKillEnemyIds.Add(enemyId);
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
                                //HardFog.Logger.LogInfo("idle relays->" + enemyId);
                                needKillEnemyIds.Add(enemyId);
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
                                //HardFog.Logger.LogInfo("turrets->" + enemyId);
                                needKillEnemyIds.Add(enemyId);
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
                                //HardFog.Logger.LogInfo("gammas->" + enemyId);
                                needKillEnemyIds.Add(enemyId);
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
                                //HardFog.Logger.LogInfo("replicators->" + enemyId);
                                needKillEnemyIds.Add(enemyId);
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
                                //HardFog.Logger.LogInfo("connectors->" + enemyId);
                                needKillEnemyIds.Add(enemyId);
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
                                //HardFog.Logger.LogInfo("nodes->" + enemyId);
                                needKillEnemyIds.Add(enemyId);
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
                                //HardFog.Logger.LogInfo("builders->" + enemyId);
                                needKillEnemyIds.Add(enemyId);
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
        private static void StarsFillHive()
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

        private static void StarResetHive()
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

        private static void SetCoreInvincible()
        {
            var player = GameMain.mainPlayer;
            if (player == null) return;
            var planet = GameMain.localPlanet;
            var star = GameMain.localStar;
            SpaceSector spaceSector = GameMain.spaceSector;


            EnemyDFHiveSystem enemyDFHiveSystem = spaceSector.dfHives[star.index];

            while (enemyDFHiveSystem != null)
            {

                // 清空核心能量
                for (var i = enemyDFHiveSystem.cores.cursor - 1; i > 0; i--)
                {

                    ref EnemyData ptr = ref spaceSector.enemyPool[enemyDFHiveSystem.cores.buffer[i].enemyId];
                    ptr.isInvincible = true;
                }

                enemyDFHiveSystem = enemyDFHiveSystem.nextSibling;
            }
        }

        private static void SetCoreVulnerable()
        {
            var player = GameMain.mainPlayer;
            if (player == null) return;
            var planet = GameMain.localPlanet;
            var star = GameMain.localStar;
            SpaceSector spaceSector = GameMain.spaceSector;


            EnemyDFHiveSystem enemyDFHiveSystem = spaceSector.dfHives[star.index];

            while (enemyDFHiveSystem != null)
            {

                // 清空核心能量
                for (var i = enemyDFHiveSystem.cores.cursor - 1; i > 0; i--)
                {

                    ref EnemyData ptr = ref spaceSector.enemyPool[enemyDFHiveSystem.cores.buffer[i].enemyId];
                    ptr.isInvincible = false;
                }

                enemyDFHiveSystem = enemyDFHiveSystem.nextSibling;
            }
        }





    }
}

