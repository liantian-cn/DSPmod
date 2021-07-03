using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using HarmonyLib;
using BepInEx.Logging;
using BepInEx.Configuration;

namespace SuperPower
{
	[BepInPlugin("me.liantian.plugin.SuperPower", "SuperPower", "1.0")]
	public class SuperPower : BaseUnityPlugin
	{

		private static ConfigEntry<long> SolarPanelRatio;
		private static ConfigEntry<long> WindTurbineRatio;
		private static ConfigEntry<long> ThermalPowerRatio;
		private static ConfigEntry<long> FusionPowerRatio;
		private static ConfigEntry<long> RayReceiverRatio;
		private static ConfigEntry<long> ArtificialStarRatio;
		void Start()
		{
			SuperPower.Logger = base.Logger;
			SolarPanelRatio = Config.Bind<long>("config", "SolarPanelRatio", 100, "太阳能板倍数");
			WindTurbineRatio = Config.Bind<long>("config", "WindTurbineRatio", 100, "风力发电倍数");
			ThermalPowerRatio = Config.Bind<long>("config", "ThermalPowerRatio", 10, "火力发电倍数");
			FusionPowerRatio = Config.Bind<long>("config", "FusionPowerRatio", 10, "核电倍数");
			RayReceiverRatio = Config.Bind<long>("config", "RayReceiverRatio", 10, "射线接收器倍数");
			ArtificialStarRatio = Config.Bind<long>("config", "ArtificialStarRatio", 10, "小太阳倍数");
			Harmony.CreateAndPatchAll(typeof(SuperPower));
		}



		[HarmonyPrefix]
		[HarmonyPatch(typeof(PowerSystem), "NewGeneratorComponent")]
		private static bool NewGeneratorComponent_Prefix(ref long __state, ref int __result, int entityId, PrefabDesc desc)
		{

			if (desc.photovoltaic)
			{
				//SuperPower.Logger.LogInfo("photovoltaic");       //太阳能电池板
				//SuperPower.Logger.LogInfo(desc.photovoltaic);
				__state = SolarPanelRatio.Value;
			}
			else if (desc.windForcedPower)
            {
				//SuperPower.Logger.LogInfo("windForcedPower");    //风电
				//SuperPower.Logger.LogInfo(desc.windForcedPower);
				__state = WindTurbineRatio.Value;
			}
			else if (desc.gammaRayReceiver)
            {
				//SuperPower.Logger.LogInfo("gammaRayReceiver");   //射线接收器
				//SuperPower.Logger.LogInfo(desc.gammaRayReceiver);
				__state = RayReceiverRatio.Value;

			}
			else if (desc.modelIndex == 118)
            {
				//SuperPower.Logger.LogInfo("modelIndex");      //  118核电  54火电  56 小太阳
				//SuperPower.Logger.LogInfo(desc.modelIndex);
				__state = FusionPowerRatio.Value;
			}
			else if (desc.modelIndex == 54)
			{
				//SuperPower.Logger.LogInfo("modelIndex");      //  118核电  54火电  56 小太阳
				//SuperPower.Logger.LogInfo(desc.modelIndex);
				__state = ThermalPowerRatio.Value;
			}
			else if (desc.modelIndex == 56)
			{
				//SuperPower.Logger.LogInfo("modelIndex");      //  118核电  54火电  56 小太阳
				//SuperPower.Logger.LogInfo(desc.modelIndex);
				__state = ArtificialStarRatio.Value;
			}
			else
            {
				__state = (long)1;
			}
			return true;

		}



		[HarmonyPostfix]
		[HarmonyPatch(typeof(PowerSystem), "NewGeneratorComponent")]
		private static void NewGeneratorComponent_Postfix(PowerSystem __instance, int __result, ref long __state)
		{
			//SuperPower.Logger.LogInfo(__state);
			PowerGeneratorComponent[] genPool = __instance.genPool;
			//SuperPower.Logger.LogInfo(genPool[__result].genEnergyPerTick);
			genPool[__result].genEnergyPerTick = genPool[__result].genEnergyPerTick * __state;
			//SuperPower.Logger.LogInfo(genPool[__result].genEnergyPerTick);
		}

		public new static ManualLogSource Logger;


	}
}
