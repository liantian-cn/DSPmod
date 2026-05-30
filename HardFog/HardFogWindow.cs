using System;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UXAssist.Common;
using UXAssist.UI;

namespace HardFog
{
    [BepInPlugin("me.liantian.plugin.HardFog", "HardFog", "0.0.7")]
    [BepInDependency(UXAssist.PluginInfo.PLUGIN_GUID)]
    public class HardFogWindow : BaseUnityPlugin
    {
        private const string WindowTitleKey = "hard_fog_control";
        private const string ClearCurrentPlanetKey = "clear_current_planet";
        private const string ClearCurrentStarKey = "clear_current_star";
        private const string FillGalaxyHivesKey = "fill_hive";

        private static UIButton clearCurrentPlanetButton;
        private static UIButton clearCurrentStarButton;
        private static UIButton fillGalaxyHivesButton;

        internal static ManualLogSource Log;

        public void Awake()
        {
            Log = Logger;
            DarkFogControl.Log = Logger;

            I18N.Add(WindowTitleKey, "Dark Fog Control", "黑雾操控");
            I18N.Add(ClearCurrentPlanetKey, "Clear Dark Fog on current planet", "清理当前星球的黑雾");
            I18N.Add(ClearCurrentStarKey, "Clear space Dark Fog in current star system", "清理当前恒星的太空黑雾");
            I18N.Add(FillGalaxyHivesKey, "Fill the galaxy with Dark Fog hives", "为整个星系填满黑雾巢穴");
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
