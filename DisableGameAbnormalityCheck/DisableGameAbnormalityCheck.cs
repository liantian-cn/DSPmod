using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using BepInEx.Configuration;


namespace DisableGameAbnormalityCheck
{
    [BepInPlugin("me.liantian.plugin.PuttyVein", "PuttyVein", "1.0.1")]
    [BepInProcess("DSPGAME.exe")]
    public class DisableGameAbnormalityCheck : BaseUnityPlugin
    {



		public new static ManualLogSource Logger;
		public static RectTransform pppButton = null;

		public void Awake()
		{

			DisableGameAbnormalityCheck.Logger = base.Logger;
			Harmony.CreateAndPatchAll(typeof(DisableGameAbnormalityCheck));
			DisableGameAbnormalityCheck.Logger.LogInfo("DisableGameAbnormalityCheck 初始化");
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(GameAbnormalityCheck), "isGameNormal")]
		public static bool isGameNormalPatch(ref bool __result)
		{
			__result = true;
			return false;
        }


		[HarmonyPrefix]
		[HarmonyPatch(typeof(GameAbnormalityCheck), "IsFunctionNormal")]
		public static bool IsFunctionNormalPatch(ref bool __result)
		{
			__result = true;
			return false;
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(MilkyWayWebClient), "SendUploadRecordRequest")]
		public static bool SendUploadRecordRequestPatch(GameData gameData)
		{

			return false;
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(MilkyWayWebClient), "SendReportRequest")]
		public static bool SendReportRequestPatch()
		{

			return false;
		}

	}
}
