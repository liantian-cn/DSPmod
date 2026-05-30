using System;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UXAssist.Common;
using UXAssist.UI;

namespace HardFog
{
    [BepInPlugin("me.liantian.plugin.HardFog", "HardFog", "0.0.8")]
    [BepInDependency(UXAssist.PluginInfo.PLUGIN_GUID)]
    public class HardFogWindow : BaseUnityPlugin
    {
        private const string WindowTitleKey = "hard_fog_control";
        private const string ClearCurrentPlanetKey = "clear_current_planet";
        private const string ClearCurrentStarKey = "clear_current_star";
        private const string FillGalaxyHivesKey = "fill_hive";
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

            I18N.Add(WindowTitleKey, "Dark Fog Control", "黑雾操控");
            I18N.Add(ClearCurrentPlanetKey, "Clear Dark Fog on current planet", "清理当前星球的黑雾");
            I18N.Add(ClearCurrentStarKey, "Clear space Dark Fog in current star system", "清理当前恒星的太空黑雾");
            I18N.Add(FillGalaxyHivesKey, "Fill the galaxy with Dark Fog hives", "为整个星系填满黑雾巢穴");
            I18N.Add(ConstructLowLatitudeRuinsKey, "Construct low-latitude ruins", "构造低纬度废墟");
            I18N.Add(ConstructMidLatitudeRuinsKey, "Construct mid-latitude ruins", "构造中纬度废墟");
            I18N.Add(ConstructHighLatitudeRuinsKey, "Construct high-latitude ruins", "构造高纬度废墟");
            I18N.Add(BuildGeothermalOnIdleRuinsKey, "Build geothermal power stations on idle ruins", "在空闲废墟上建造地热发电站");
            I18N.Apply();

            MyConfigWindow.OnUICreated += CreateUI;
            Log.LogInfo("HardFog 初始化");
        }

        public void OnDestroy()
        {
            MyConfigWindow.OnUICreated -= CreateUI;
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
