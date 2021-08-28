using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using HarmonyLib;
using BepInEx.Logging;
using BepInEx.Configuration;

namespace me.liantian.plugin.RefreshStationConfig
{


    [BepInPlugin("me.liantian.plugin.RefreshStationConfig", "RefreshStationConfig", "1.0")]
    public class RefreshStationConfig : BaseUnityPlugin
    {

        void Start()
        {
            RefreshStationConfig.Logger = base.Logger;
            me.liantian.plugin.RefreshStationConfig.Config.Init(base.Config);
            new Harmony("me.liantian.plugin.RefreshStationConfig").PatchAll(typeof(GameSavePatch));
        }
        public new static ManualLogSource Logger;

    }


    [HarmonyPatch(typeof(GameSave))]
    public class GameSavePatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("LoadCurrentGame")]
        public static void LoadCurrentGamePatch(bool __result)
        {
            if (!__result)
            {
                return;
            }
            RefreshStationConfig.Logger.LogInfo("RefreshStationConfig patching stations...");
            StarData[] stars = GameMain.galaxy.stars;
            for (int i = 0; i < stars.Length; i++)
            {
                foreach (PlanetData planetData in stars[i].planets)
                {
                    PlanetTransport planetTransport;
                    if (planetData == null)
                    {
                        planetTransport = null;
                    }
                    else
                    {
                        PlanetFactory factory = planetData.factory;
                        planetTransport = ((factory != null) ? factory.transport : null);
                    }
                    StationComponent[] station_list = (planetTransport != null) ? planetTransport.stationPool : null;
                    if (station_list != null && station_list.Length != 0)
                    {
                        foreach (StationComponent stationComponent in station_list)
                        {
                            if (stationComponent != null && !stationComponent.isCollector)
                            {
                                Extensions.SetMinWarpRange(stationComponent);
                                Extensions.SetMaxStellarRange(stationComponent);
                             
                            }
                        }
                    } // if (station_list != null && station_list.Length != 0)
                } //foreach (PlanetData planetData in stars[i].planets)

            }// for (int i = 0; i < stars.Length; i++)
        } // LoadCurrentGamePatch

    } //public class PlanetTransportPatch
    public static class Config
    {

        internal static void Init(ConfigFile config)
        {

            MaxStellarRange = config.Bind<int>("config", "MaxStellarRange", 20, new ConfigDescription("数值：星际船最远距离", new AcceptableValueList<int>(new int[] { 1, 2, 3, 4, 5, 7, 8, 9, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40, 42, 44, 46, 48, 50, 52, 54, 56, 58, 60, 10000 }), new object[] { new { } }));
            ChangeMaxStellarRange = config.Bind<bool>("config", "ChangeMaxStellarRange", false, "修改：星际船最远距离");

            MinWarpRange = config.Bind<double>("config", "MinWarpRange", 60.0, new ConfigDescription("数值：曲率距离", new AcceptableValueList<double>(new double[] { 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0, 11.0, 12.0, 14.0, 16.0, 18.0, 20.0, 60.0 }), new object[] { new { } }));
            ChangeMinWarpRange = config.Bind<bool>("config", "ChangeMinWarpRange", false, "修改：曲率距离");
        } //internal static void Init(ConfigFile config)

        public static ConfigEntry<int> MaxStellarRange;
        public static ConfigEntry<bool> ChangeMaxStellarRange;
        public static ConfigEntry<double> MinWarpRange;
        public static ConfigEntry<bool> ChangeMinWarpRange;


    } //public static class Config

    public static class Extensions
    {

        public static void SetMinWarpRange(this StationComponent component)
        {

            if (!component.isStellar)
            {
                return;
            }
            RefreshStationConfig.Logger.LogInfo("站点ID：" + component.pcId.ToString());
            RefreshStationConfig.Logger.LogInfo(component.pcId.ToString() + "现曲率距离：" + component.warpEnableDist.ToString());

            if (!Config.ChangeMinWarpRange.Value)
            {
                return;
            }
            component.warpEnableDist = AU(Config.MinWarpRange.Value);
        }

        public static void SetMaxStellarRange(this StationComponent component)
        {

            if (!component.isStellar)
            {
                return;
            }
            RefreshStationConfig.Logger.LogInfo("站点ID：" + component.pcId.ToString());
            RefreshStationConfig.Logger.LogInfo(component.pcId.ToString() + "现最远航程：" + component.tripRangeShips.ToString());
            if (!Config.ChangeMaxStellarRange.Value)
            {
                return;
            }
            component.tripRangeShips = LY((double)Config.MaxStellarRange.Value);
        }

        private static double AU(double num)
        {
            return num * 40000.0;
        }

        private static double LY(double num)
        {
            return num * 2400000.0;
        }
    }//public static class Extensions

}
