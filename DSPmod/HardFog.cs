using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using BepInEx.Configuration;
using System.IO;
using Steamworks;
using System.Collections.Generic;

namespace DSPmod
{
    [BepInPlugin("me.liantian.plugin.HardFog", "HardFog", "0.0.1")]
    [BepInProcess("DSPGAME.exe")]
    public class HardFog : BaseUnityPlugin
    {
        private static ConfigEntry<bool> MoreFrequentRelays;
        private static ConfigEntry<bool> HiveMoreEnergy;
        private static ConfigEntry<bool> HiveLessEnergy;
        private static ConfigEntry<bool> BaseMoreEnergy;
        private static ConfigEntry<bool> BaseLessEnergy;
        private static ConfigEntry<bool> DysonSphereDebuffImmunity;
        private static ConfigEntry<bool> CoreIsInvincible;
        private static ConfigEntry<bool> SuperRayReception;
        private static ConfigEntry<bool> StarsFillHive;
        public new static ManualLogSource Logger;


        public void Awake()
        {
            HardFog.MoreFrequentRelays = base.Config.Bind<bool>("FogCheats", "MoreFrequentRelays", false, "巢穴更频繁的发送的中继站");
            HardFog.HiveMoreEnergy = base.Config.Bind<bool>("FogCheats", "HiveMoreEnergy", false, "黑雾巢穴获得更多的能量");
            HardFog.HiveLessEnergy = base.Config.Bind<bool>("FogCheats", "HiveLessEnergy", false, "黑雾巢穴获得更少的能量");
            HardFog.BaseMoreEnergy = base.Config.Bind<bool>("FogCheats", "BaseMoreEnergy", false, "行星基地获得更多的能量");
            HardFog.BaseLessEnergy = base.Config.Bind<bool>("FogCheats", "BaseLessEnergy", false, "行星基地获得更少的能量");
            HardFog.DysonSphereDebuffImmunity = base.Config.Bind<bool>("Cheats", "DysonSphereDebuffImmunity", false, "黑雾无法偷电，因为不偷也很强大了。");
            HardFog.CoreIsInvincible = base.Config.Bind<bool>("QOL", "CoreIsInvincible", true, "不攻击巢穴核心。");
            HardFog.SuperRayReception = base.Config.Bind<bool>("Cheats", "SuperRayReception", false, "射线接收器满速接收。");
            HardFog.StarsFillHive = base.Config.Bind<bool>("FogCheats", "StarsFillHive", true, "星系填满巢穴。");
            HardFog.Logger = base.Logger;
            Harmony.CreateAndPatchAll(typeof(HardFog));
            HardFog.Logger.LogInfo("HardFog 初始化");
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



        // 增加黑雾巢穴能量
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DFSCoreComponent), "LogicTick")]
        public static void DFSCoreComponentLogicTick(DFSCoreComponent __instance, EnemyDFHiveSystem hive)
        {
            if ((HardFog.HiveMoreEnergy.Value) && (hive.evolve.waveTicks > 0) && (hive.starData.id != hive.galaxy.birthStarId))
            {
                ref EnemyBuilderComponent ptr = ref hive.builders.buffer[__instance.builderId];
                ptr.matter += (int)(((double)ptr.maxMatter * 2 - (double)ptr.matter) * 0.05);
                ptr.energy += (int)(((double)ptr.maxEnergy * 2 - (double)ptr.energy) * 0.05);

            }
            else if (HardFog.HiveLessEnergy.Value)
            {
                ref EnemyBuilderComponent ptr = ref hive.builders.buffer[__instance.builderId];
                ptr.matter -= (int)(((double)ptr.matter - (double)ptr.minMatter) * 0.005);
                ptr.energy -= (int)(((double)ptr.energy - (double)ptr.minEnergy) * 0.005); ;
            }

            ref EnemyData ptr4 = ref hive.sector.enemyPool[__instance.enemyId];
            if (HardFog.CoreIsInvincible.Value)
            {

                ptr4.isInvincible = true;
            }
            else
            {
                ptr4.isInvincible = false;
            }


        }

        // 增加基地核心的能量
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DFGBaseComponent), "LogicTick")]
        public static void DFGBaseComponentLogicTick(DFGBaseComponent __instance, ref EnemyBuilderComponent builder)
        {
            if ((HardFog.BaseMoreEnergy.Value && (__instance.turboTicks > 0) ))
            {
                builder.matter += (int)(((double)builder.maxMatter - (double)builder.matter) * 0.005);
                builder.energy += (int)(((double)builder.maxEnergy - (double)builder.energy) * 0.005);
            }
            else if (HardFog.BaseLessEnergy.Value)
            {
                builder.matter -= (int)(((double)builder.matter - (double)builder.minMatter) * 0.005);
                builder.energy-= (int)(((double)builder.matter - (double)builder.minEnergy) * 0.005);
            }

        }


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


        // 作弊锅
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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameData), nameof(GameData.Import))]
        private static void GameData_Import_Postfix(GameData __instance)
        {
            if (HardFog.StarsFillHive.Value && (!DSPGame.IsMenuDemo))
            {
                HardFog.Logger.LogInfo("读取了存档");
                HardFog.StarsFillHive.Value = false;
                for (int i = 0; i < __instance.galaxy.starCount; i++)
                {
                    StarData star = __instance.galaxy.stars[i];
                    
                    HardFog.Logger.LogInfo(star.name + ":" + star.maxHiveCount.ToString());
                    int num10086 = star.maxHiveCount;
                    if (num10086 > 8)
                    {
                        num10086 = 8;
                    }
                    for (int j = 0; j < num10086; j++)
                    {
                        EnemyDFHiveSystem hive = __instance.spaceSector.TryCreateNewHive(star);
                        if (hive != null)
                        {
                            //val3.SetForNewCreate();
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


                            HardFog.Logger.LogInfo("Add new hive");
                        }
                        //;
                    }


                }
            }
            
        }
    }
}

