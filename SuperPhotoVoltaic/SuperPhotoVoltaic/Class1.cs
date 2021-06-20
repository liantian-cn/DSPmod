
using BepInEx;
using HarmonyLib;
using BepInEx.Logging;


namespace SuperPhotoVoltaic
{
	[BepInPlugin("org.bepinex.plugins.TestMod", "SuperPhotoVoltaic", "0.0.1")]
	public class SuperPhotoVoltaic : BaseUnityPlugin
	{
		void Start()
		{
			Harmony.CreateAndPatchAll(typeof(SuperPhotoVoltaic));
			SuperPhotoVoltaic.Logger = base.Logger;
		}

		#region 超级风电
		[HarmonyPostfix]
		[HarmonyPatch(typeof(PowerSystem), "NewGeneratorComponent")]
		private static void NewGeneratorComponent(PowerSystem __instance, int __result)
		{
			PowerGeneratorComponent[] genPool = __instance.genPool;
			if (genPool[__result].photovoltaic)
			{
				genPool[__result].genEnergyPerTick = genPool[__result].genEnergyPerTick * (long)1000;
			}
			// genPool[__result].genEnergyPerTick = genPool[__result].genEnergyPerTick * (long)10000;
			SuperPhotoVoltaic.Logger.LogInfo(genPool[__result].entityId);
			//SuperPhotoVoltaic.Logger.LogInfo(genPool[__result].photovoltaic);
		}
		#endregion

		public new static ManualLogSource Logger;



	}
}
