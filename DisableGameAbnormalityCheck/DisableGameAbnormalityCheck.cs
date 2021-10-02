using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using BepInEx.Configuration;
using System.Reflection;


namespace DisableGameAbnormalityCheck
{
    [BepInPlugin("me.liantian.plugin.DisableGameAbnormalityCheck", "DisableGameAbnormalityCheck", "1.0.1")]
    [BepInProcess("DSPGAME.exe")]
    public class DisableGameAbnormalityCheck : BaseUnityPlugin
    {



		public static RectTransform pppButton = null;

		public void Awake()
		{
			Logger.LogInfo("DisableGameAbnormalityCheckis 初始化");
			Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(GameAbnormalityCheck), "isGameNormal")]
		public static bool CheckGameNormalPatch(ref bool __result)
		{
			__result = true;
			return false;
        }


		[HarmonyPrefix]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.AfterTick))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.BeforeTick))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckMajorClause))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.NotifyAbnormalityChecked))]
		[HarmonyPatch(typeof(MilkyWayWebClient), nameof(MilkyWayWebClient.SendReportRequest))]
		[HarmonyPatch(typeof(MilkyWayWebClient), nameof(MilkyWayWebClient.SendUploadRecordRequest))]
		public static bool Prefix()
		{
			return false;
		}

	}
}
