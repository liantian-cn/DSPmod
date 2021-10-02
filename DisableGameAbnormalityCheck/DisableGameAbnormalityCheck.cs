using BepInEx;
using HarmonyLib;



namespace DisableGameAbnormalityCheck
{
    [BepInPlugin("me.liantian.plugin.DisableGameAbnormalityCheck", "DisableGameAbnormalityCheck", "1.0.1")]
    [BepInProcess("DSPGAME.exe")]
    public class DisableGameAbnormalityCheck : BaseUnityPlugin
    {


		public void Awake()
		{
			Logger.LogInfo("DisableGameAbnormalityCheckis 初始化!");
			Harmony.CreateAndPatchAll(typeof(DisableGameAbnormalityCheck));

		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(GameAbnormalityCheck), "isGameNormal")]
		public static bool GameNormalPatch(ref bool __result)
		{
			__result = true;
			return false;
		}


		[HarmonyPrefix]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.NotifyAbnormalityChecked))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckItemProto))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckTechProto))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckRecipeProto))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckVeinProto))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckVegeProto))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckAllProtoSetInLDB))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckFreeModeConfig))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckProtoNormal))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckAllTechValueValid))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckTechUnlockConditions))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckTechUpgradeItemConsume))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckRecipeUnlocked))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckRecipeItemAndProduceValid))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckAllDysonSphereEnergyGenParam))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckLocalDysonSphereBuildValid))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckItemConsume))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckPowerGeneratorData))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckMinerData))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckTransportData))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.BeforeTick))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.AfterTick))]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.CheckMajorClause))]

		[HarmonyPatch(typeof(ProtoCheck), nameof(ProtoCheck.CheckProto))]
		[HarmonyPatch(typeof(ProtoCheck), nameof(ProtoCheck.CheckConfigParam))]

		[HarmonyPatch(typeof(TechCheck), nameof(TechCheck.CheckValueInfByVeinsUtil))]
		[HarmonyPatch(typeof(TechCheck), nameof(TechCheck.CheckValueInfByMechaFrame))]
		[HarmonyPatch(typeof(TechCheck), nameof(TechCheck.CheckValueInfByDroneEngine))]
		[HarmonyPatch(typeof(TechCheck), nameof(TechCheck.CheckValueInfByCommunicationControl))]
		[HarmonyPatch(typeof(TechCheck), nameof(TechCheck.CheckValueInfByMechaCore))]
		[HarmonyPatch(typeof(TechCheck), nameof(TechCheck.CheckTechUnlockConditions))]
		[HarmonyPatch(typeof(TechCheck), nameof(TechCheck.CheckTechUpgradeItemConsume))]
		[HarmonyPatch(typeof(TechCheck), nameof(TechCheck.InitAfterGameDataReady))]

		[HarmonyPatch(typeof(RecipeCheck), nameof(RecipeCheck.CheckRecipeUnlocked))]
		[HarmonyPatch(typeof(RecipeCheck), nameof(RecipeCheck.CheckRecipeItemAndProduceValid))]
		[HarmonyPatch(typeof(RecipeCheck), nameof(RecipeCheck.AfterTick))]

		[HarmonyPatch(typeof(StorageCheck), nameof(StorageCheck.StartCheckPlayerStorageGrid))]
		[HarmonyPatch(typeof(StorageCheck), nameof(StorageCheck.AfterTick))]

		[HarmonyPatch(typeof(DysonSphereCheck), nameof(DysonSphereCheck.CheckAllDysonSphereEnergyGenParam))]
		[HarmonyPatch(typeof(DysonSphereCheck), nameof(DysonSphereCheck.CheckLocalDysonSphereBuildValid))]
		[HarmonyPatch(typeof(DysonSphereCheck), nameof(DysonSphereCheck.AfterTick))]

		[HarmonyPatch(typeof(ProductionCheck), nameof(ProductionCheck.CheckAllConcernedItemConsume))]

		[HarmonyPatch(typeof(FactoryCheck), nameof(FactoryCheck.CheckPowerGeneratorData))]
		[HarmonyPatch(typeof(FactoryCheck), nameof(FactoryCheck.CheckMinerData))]
		[HarmonyPatch(typeof(FactoryCheck), nameof(FactoryCheck.StartCheckTransportData))]
		[HarmonyPatch(typeof(FactoryCheck), nameof(FactoryCheck.AfterTick))]
		[HarmonyPatch(typeof(FactoryCheck), nameof(FactoryCheck.BeforeTick))]


		[HarmonyPatch(typeof(MilkyWayWebClient), nameof(MilkyWayWebClient.SendLoginRequest))]
		[HarmonyPatch(typeof(MilkyWayWebClient), nameof(MilkyWayWebClient.SendUploadLoginRequest))]
		[HarmonyPatch(typeof(MilkyWayWebClient), nameof(MilkyWayWebClient.SendFullDataDownloadRequest))]
		[HarmonyPatch(typeof(MilkyWayWebClient), nameof(MilkyWayWebClient.SendDownloadFeedback))]
		[HarmonyPatch(typeof(MilkyWayWebClient), nameof(MilkyWayWebClient.SendUploadRecordRequest))]
		[HarmonyPatch(typeof(MilkyWayWebClient), nameof(MilkyWayWebClient.SendReportRequest))]
		[HarmonyPatch(typeof(PARTNER), nameof(PARTNER.UploadClusterGenerationToGalaxyServer))]
		public static bool Prefix()
		{
			return false;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(GameAbnormalityCheck), nameof(GameAbnormalityCheck.InitAfterGameDataReady))]
		public static void GameAbnormalityCheck_InitAfterGameDataReady_Postfix(GameAbnormalityCheck __instance)
		{
			__instance.gameData.history.onTechUnlocked -= __instance.CheckTechUnlockValid;
			__instance.gameData.mainPlayer.package.onStorageChange -= __instance.OnPlayerStorageChange;
		}


	}
}
