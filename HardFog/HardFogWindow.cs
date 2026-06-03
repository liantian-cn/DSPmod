using System;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UXAssist.Common;
using UXAssist.UI;

namespace HardFog
{
    [BepInPlugin("me.liantian.plugin.HardFog", "HardFog", "0.0.17")]
    [BepInDependency(UXAssist.PluginInfo.PLUGIN_GUID)]
    public class HardFogWindow : BaseUnityPlugin
    {
        private const string WindowTitleKey = "hard_fog_control";
        private const string ClearCurrentPlanetKey = "clear_current_planet";
        private const string ClearCurrentStarKey = "clear_current_star";
        private const string FillGalaxyHivesKey = "fill_hive";
        private const string FogThreatDampenerKey = "fog-threat-dampener-enabled";
        private const string SmartRelayDispatchKey = "smart-relay-dispatch-enabled";
        private const string FasterResearchKey = "faster-research-enabled";
        private const string BuildAnywhereOnWaterKey = "build-anywhere-on-water-enabled";
        private const string PumpAnywhereKey = "pump-anywhere-enabled";
        private const string VeinPlacementKey = "vein-placement-enabled";
        private const string OverpoweredMechaFightersKey = "overpowered-mecha-fighters-enabled";
        private const string ConstructLowLatitudeRuinsKey = "surface-ruins-construct-low-latitude";
        private const string ConstructMidLatitudeRuinsKey = "surface-ruins-construct-mid-latitude";
        private const string ConstructHighLatitudeRuinsKey = "surface-ruins-construct-high-latitude";
        private const string BuildGeothermalOnIdleRuinsKey = "surface-ruins-build-geothermal-on-idle-ruins";

        private static UIButton clearCurrentPlanetButton;
        private static UIButton clearCurrentStarButton;
        private static UIButton fillGalaxyHivesButton;
        private static UIButton constructLowLatitudeRuinsButton;
        private static UIButton constructMidLatitudeRuinsButton;
        private static UIButton constructHighLatitudeRuinsButton;
        private static UIButton buildGeothermalOnIdleRuinsButton;

        internal static ManualLogSource Log;

        public void Awake()
        {
            Log = Logger;
            DarkFogControl.Log = Logger;
            SurfaceRuinControl.Log = Logger;
            FogThreatDampenerControl.Init(Config.Bind("DarkFog", "FogThreatDampenerEnabled", false, "Enable Dark Fog Pressure Reducer. Reduces non-active hive threat by 1% every 30 hive ticks."), Logger);
            SmartRelayDispatchControl.Init(Config.Bind("DarkFog", "SmartRelayDispatchEnabled", false, "Enable relay station dispatch only to markers."), Logger);
            FasterResearchControl.Init(Config.Bind("HardFog", "FasterResearchEnabled", false, "Enable research speed multiplier. Reduces tech hash needed to about 1/36."), Logger);
            BuildAnywhereOnWaterControl.Init(Config.Bind("HardFog", "BuildAnywhereOnWaterEnabled", true, "Enable ignoring missing ground support build failures, including water placement."), Logger);
            PumpAnywhere.Init(Config.Bind("HardFog", "PumpAnywhereEnabled", false, "Enable placing water pumps anywhere on planets with matching water type."), Logger);
            VeinPlacementControl.Init(Config.Bind("HardFog", "VeinPlacementEnabled", true, "Enable better vein placement for future planet vein generation."), Logger);
            OverpoweredMechaFightersControl.Init(Config.Bind("HardFog", "OverpoweredMechaFightersEnabled", false, "Enable stronger mecha fighters: 10x range, 10x damage, and invincibility."), Logger);

            I18N.Add(WindowTitleKey, "Dark Fog Control", "黑雾操控");
            I18N.Add(ClearCurrentPlanetKey, "Clear Dark Fog on current planet", "清理当前星球的黑雾");
            I18N.Add(ClearCurrentStarKey, "Clear space Dark Fog in current star system", "清理当前恒星的太空黑雾");
            I18N.Add(FillGalaxyHivesKey, "Fill the galaxy with Dark Fog hives", "为整个星系填满黑雾巢穴");
            I18N.Add(FogThreatDampenerKey, "Dark Fog Pressure Reducer", "黑雾降压器");
            I18N.Add(SmartRelayDispatchKey, "Relay stations only dispatch to markers", "中继站只发往信标");
            I18N.Add(FasterResearchKey, "Research Speed Multiplier", "研究倍速器");
            I18N.Add(VeinPlacementKey, "Better vein placement", "更好的矿物位置");
            I18N.Add(OverpoweredMechaFightersKey, "Fighters fly farther", "战斗机更远的飞行距离");
            I18N.Add(ConstructLowLatitudeRuinsKey, "Construct low-latitude ruins", "构造低纬度废墟");
            I18N.Add(ConstructMidLatitudeRuinsKey, "Construct mid-latitude ruins", "构造中纬度废墟");
            I18N.Add(ConstructHighLatitudeRuinsKey, "Construct high-latitude ruins", "构造高纬度废墟");
            I18N.Add(BuildGeothermalOnIdleRuinsKey, "Build geothermal power stations on idle ruins", "在空闲废墟上建造地热发电站");
            I18N.Add(BuildAnywhereOnWaterKey, "Ignore ground support requirement", "无需地基支撑建造");
            I18N.Add(PumpAnywhereKey, "Pump anywhere", "平地抽水");
            I18N.Apply();

            MyConfigWindow.OnUICreated += CreateUI;
            Log.LogInfo("HardFog 初始化");
        }

        public void OnDestroy()
        {
            MyConfigWindow.OnUICreated -= CreateUI;
            FogThreatDampenerControl.Uninit();
            SmartRelayDispatchControl.Uninit();
            FasterResearchControl.Uninit();
            BuildAnywhereOnWaterControl.Uninit();
            PumpAnywhere.Uninit();
            VeinPlacementControl.Uninit();
            OverpoweredMechaFightersControl.Uninit();
        }

        private static void CreateUI(MyConfigWindow wnd, RectTransform trans)
        {
            const float x = 10f;
            var y = 10f;

            wnd.AddSplitter(trans, 10f);
            wnd.AddTabGroup(trans, WindowTitleKey, "tab-group-hard-fog");
            RectTransform tab = wnd.AddTab(trans, WindowTitleKey);

            clearCurrentPlanetButton = wnd.AddButton(x, y, 240, tab, ClearCurrentPlanetKey, 16, "button-clear-current-planet-dark-fog", OnClearCurrentPlanetClicked);
            y += 36f;
            clearCurrentStarButton = wnd.AddButton(x, y, 240, tab, ClearCurrentStarKey, 16, "button-clear-current-star-dark-fog", OnClearCurrentStarClicked);
            y += 36f;
            fillGalaxyHivesButton = wnd.AddButton(x, y, 240, tab, FillGalaxyHivesKey, 16, "button-fill-galaxy-dark-fog-hives", OnFillGalaxyHivesClicked);
            y += 36f;
            wnd.AddCheckBox(x, y, tab, FogThreatDampenerControl.EnabledConfig, FogThreatDampenerKey, 16);
            y += 36f;
            wnd.AddCheckBox(x, y, tab, SmartRelayDispatchControl.EnabledConfig, SmartRelayDispatchKey, 16);
            y += 36f;
            wnd.AddCheckBox(x, y, tab, FasterResearchControl.EnabledConfig, FasterResearchKey, 16);
            y += 36f;
            wnd.AddCheckBox(x, y, tab, BuildAnywhereOnWaterControl.EnabledConfig, BuildAnywhereOnWaterKey, 16);
            y += 36f;
            wnd.AddCheckBox(x, y, tab, PumpAnywhere.EnabledConfig, PumpAnywhereKey, 16);
            y += 36f;
            wnd.AddCheckBox(x, y, tab, VeinPlacementControl.EnabledConfig, VeinPlacementKey, 16);
            y += 36f;
            wnd.AddCheckBox(x, y, tab, OverpoweredMechaFightersControl.EnabledConfig, OverpoweredMechaFightersKey, 16);
            y += 36f;
            constructLowLatitudeRuinsButton = wnd.AddButton(x, y, 260, tab, ConstructLowLatitudeRuinsKey, 16, "button-surface-ruins-construct-low-latitude", OnConstructLowLatitudeRuinsClicked);
            y += 36f;
            constructMidLatitudeRuinsButton = wnd.AddButton(x, y, 260, tab, ConstructMidLatitudeRuinsKey, 16, "button-surface-ruins-construct-mid-latitude", OnConstructMidLatitudeRuinsClicked);
            y += 36f;
            constructHighLatitudeRuinsButton = wnd.AddButton(x, y, 260, tab, ConstructHighLatitudeRuinsKey, 16, "button-surface-ruins-construct-high-latitude", OnConstructHighLatitudeRuinsClicked);
            y += 36f;
            buildGeothermalOnIdleRuinsButton = wnd.AddButton(x, y, 340, tab, BuildGeothermalOnIdleRuinsKey, 16, "button-surface-ruins-build-geothermal-on-idle-ruins", OnBuildGeothermalOnIdleRuinsClicked);
        }

        private static void OnClearCurrentPlanetClicked()
        {
            RunWithButtonDisabled(clearCurrentPlanetButton, DarkFogControl.ClearCurrentPlanetDarkFog);
        }

        private static void OnClearCurrentStarClicked()
        {
            RunWithButtonDisabled(clearCurrentStarButton, DarkFogControl.ClearCurrentStarSpaceDarkFog);
        }

        private static void OnFillGalaxyHivesClicked()
        {
            RunWithButtonDisabled(fillGalaxyHivesButton, DarkFogControl.FillGalaxyWithDarkFogHives);
        }

        private static void OnConstructLowLatitudeRuinsClicked()
        {
            RunWithButtonDisabled(constructLowLatitudeRuinsButton, SurfaceRuinControl.ConstructLowLatitudeRuinsOnCurrentPlanet);
        }

        private static void OnConstructMidLatitudeRuinsClicked()
        {
            RunWithButtonDisabled(constructMidLatitudeRuinsButton, SurfaceRuinControl.ConstructMidLatitudeRuinsOnCurrentPlanet);
        }

        private static void OnConstructHighLatitudeRuinsClicked()
        {
            RunWithButtonDisabled(constructHighLatitudeRuinsButton, SurfaceRuinControl.ConstructHighLatitudeRuinsOnCurrentPlanet);
        }

        private static void OnBuildGeothermalOnIdleRuinsClicked()
        {
            RunWithButtonDisabled(buildGeothermalOnIdleRuinsButton, SurfaceRuinControl.BuildGeothermalOnIdleRuinsCurrentPlanet);
        }

        private static void RunWithButtonDisabled(UIButton button, Action action)
        {
            if (button != null)
            {
                button.enabled = false;
            }

            try
            {
                action();
            }
            finally
            {
                if (button != null)
                {
                    button.enabled = true;
                }
            }
        }
    }
}
