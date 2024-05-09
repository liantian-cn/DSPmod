using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using BepInEx.Configuration;

namespace DSPmod
{
    [BepInPlugin("me.liantian.plugin.HardFog", "HardFog", "0.0.1")]
    [BepInProcess("DSPGAME.exe")]
    public class HardFog : BaseUnityPlugin
    {
        private static ConfigEntry<bool> MoreFrequentRelays;
        private static ConfigEntry<bool> MoreEnergy;
        private static ConfigEntry<bool> DysonSphereDebuffImmunity;
        private static ConfigEntry<bool> CoreIsInvincible;
        public new static ManualLogSource Logger;


        public void Awake()
        {
            HardFog.MoreFrequentRelays = base.Config.Bind<bool>("", "MoreFrequentRelays", true, "巢穴更频繁的发送的中继站");
            HardFog.MoreEnergy = base.Config.Bind<bool>("", "MoreEnergy", true, "黑雾获得更多的能量");
            HardFog.DysonSphereDebuffImmunity = base.Config.Bind<bool>("", "DysonSphereDebuffImmunity", true, "黑雾无法偷电，因为不偷也很强大了。");
            HardFog.CoreIsInvincible = base.Config.Bind<bool>("", "CoreIsInvincible", true, "不攻击巢穴核心。");
            HardFog.Logger = base.Logger;
            Harmony.CreateAndPatchAll(typeof(HardFog));
            HardFog.Logger.LogInfo("HardFog 初始化");
        }



        [HarmonyPrefix]
        [HarmonyPatch(typeof(EnemyDFHiveSystem), "GameTickLogic")]
        public static void EnemyDFHiveSystemPreGameTickLogic(EnemyDFHiveSystem __instance, long gameTick)
        {
            int num = (int)(gameTick % 60L);

            // 提高发射中继站几率
            if ((num == 0) && (HardFog.MoreFrequentRelays.Value) && (__instance.starData.id != __instance.gameData.galaxy.birthStarId))
            {
                __instance.relayNeutralizedCounter = 0;
                //HardFog.Logger.LogInfo("清空relayNeutralizedCounter");
            }

            // 更多能量
            //if (num == 0 && HardFog.MoreEnergy.Value)
            //{
            //    for (int j = 1; j < __instance.builders.cursor; j++)
            //    {
            //        __instance.builders.buffer[j].matter += (int)(((double)__instance.builders.buffer[j].maxMatter - (double)__instance.builders.buffer[j].matter) * 0.01);
            //        __instance.builders.buffer[j].energy += (int)(((double)__instance.builders.buffer[j].maxEnergy - (double)__instance.builders.buffer[j].energy) * 0.01);
            //        //HardFog.Logger.LogInfo("补充能量");

            //    }

            //}

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



        // 增加核心能量
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DFSCoreComponent), "LogicTick")]
        public static void DFSCoreComponentLogicTick(DFSCoreComponent __instance, EnemyDFHiveSystem hive)
        {
            if (HardFog.MoreEnergy.Value && (hive.starData.id != hive.gameData.galaxy.birthStarId))
            {
                ref EnemyBuilderComponent ptr = ref hive.builders.buffer[__instance.builderId];
                ptr.matter += (int)(((double)ptr.maxMatter - (double)ptr.matter) * 0.01);
                ptr.energy += (int)(((double)ptr.maxEnergy - (double)ptr.energy) * 0.01);


            }

            ref EnemyData ptr4 = ref hive.sector.enemyPool[__instance.enemyId];
            if (HardFog.CoreIsInvincible.Value) {
                
                ptr4.isInvincible = true;
            } else
            {
                ptr4.isInvincible = false;
            }


        }

        // 增加核心能量
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DFGBaseComponent), "LogicTick")]
        public static void DFGBaseComponentLogicTick(DFGBaseComponent __instance, ref EnemyBuilderComponent builder)
        {
            if (HardFog.MoreEnergy.Value && (__instance.groundSystem.planet.star.id != __instance.groundSystem.gameData.galaxy.birthStarId))
            {
                builder.matter += (int)(((double)builder.maxMatter - (double)builder.matter) * 0.01);
                builder.energy += (int)(((double)builder.maxEnergy - (double)builder.energy) * 0.01);


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
        // 去掉电力偷取
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DysonSphere), "energyRespCoef", MethodType.Getter)]
        public static bool energyRespCoef_Patch(ref float __result)
        {
            if (HardFog.DysonSphereDebuffImmunity.Value)
            {
                __result = 1f;
                return false;


            }
            return true;
        }



    }
}

