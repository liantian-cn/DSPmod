using System;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UXAssist.Common;
using UXAssist.UI;

namespace HardFog
{
    // HardFog 的 BepInEx 入口类：注册配置、初始化各功能模块，并把控制按钮/开关注入 UXAssist 配置窗口。
    [BepInPlugin("me.liantian.plugin.HardFog", "HardFog", "0.0.26")]
    [BepInDependency(UXAssist.PluginInfo.PLUGIN_GUID)]
    public class HardFogWindow : BaseUnityPlugin
    {
        // UXAssist 的 I18N 和控件接口都使用字符串 key，集中定义可以减少按钮文本和配置项写错的风险。
        private const string WindowTitleKey = "hard_fog_control";
        private const string ClearCurrentPlanetKey = "clear_current_planet";
        private const string ClearCurrentStarKey = "clear_current_star";
        private const string FillGalaxyHivesKey = "fill_hive";
        private const string SuperThreatReducerHiveKey = "super-threat-reducer-hive-enabled";
        private const string SuperThreatReducerGroundKey = "super-threat-reducer-ground-enabled";
        private const string RelayLandingLogicKey = "relay-landing-logic-enabled";
        private const string RelayLandingMarkersOnlyKey = "relay-landing-markers-only";
        private const string RelayLandingFastLaunchKey = "relay-landing-fast-launch";
        private const string FasterResearchKey = "faster-research-enabled";
        private const string BuildAnywhereOnWaterKey = "build-anywhere-on-water-enabled";
        private const string VeinPlacementKey = "vein-placement-enabled";
        private const string OverpoweredMechaFightersKey = "overpowered-mecha-fighters-enabled";
        private const string ConstructLowLatitudeRuinsKey = "surface-ruins-construct-low-latitude";
        private const string ConstructMidLatitudeRuinsKey = "surface-ruins-construct-mid-latitude";
        private const string ConstructHighLatitudeRuinsKey = "surface-ruins-construct-high-latitude";
        private const string BuildGeothermalOnIdleRuinsKey = "surface-ruins-build-geothermal-on-idle-ruins";
        private const string GeothermalPowerLineKey = "geothermal-power-line-enabled";

        private static UIButton clearCurrentPlanetButton;
        private static UIButton clearCurrentStarButton;
        private static UIButton fillGalaxyHivesButton;
        private static UIButton constructLowLatitudeRuinsButton;
        private static UIButton constructMidLatitudeRuinsButton;
        private static UIButton constructHighLatitudeRuinsButton;
        private static UIButton buildGeothermalOnIdleRuinsButton = null;

        // 共享日志入口；按钮型模块和配置型模块都通过它写入同一个 HardFog 日志源。
        internal static ManualLogSource Log;

        // BepInEx 在插件加载时调用 Awake；这里完成配置绑定、模块初始化、翻译注册和 UI 创建事件挂接。
        public void Awake()
        {
            // 先注入日志引用，后续模块初始化失败或运行时操作都能留下统一日志。
            Log = Logger;
            DarkFogControl.Log = Logger;
            SurfaceRuinControl.Log = Logger;
            // 每个控制器保存自己的 ConfigEntry，并在配置变更时安装或卸载对应 Harmony Patch。
            SuperThreatReducerControl.Init(
                Config.Bind("DarkFog", "SuperThreatReducerHiveEnabled", false, "Enable Space Hive Suppression. Zeros space hive threat and skips assault scans."),
                Config.Bind("DarkFog", "SuperThreatReducerGroundEnabled", false, "Enable Ground Base Suppression. Skips ground base threat accumulation and assault scans."),
                Logger);
            RelayControl.Init(
                Config.Bind("DarkFog", "RelayLandingLogicEnabled", true, "Enable relay landing logic modification. Uses preset landing spots (markers + sphere-packed coordinates) instead of vanilla random sampling."),
                Config.Bind("DarkFog", "RelayLandingMarkersOnly", false, "When relay landing logic is enabled, restrict landing candidates to markers only (ignore RuinPositions coordinates)."),
                Config.Bind("DarkFog", "RelayLandingFastLaunch", false, "When relay landing logic is enabled, check relay demand every 120 hive ticks instead of 600."),
                Logger);
            FasterResearchControl.Init(Config.Bind("HardFog", "FasterResearchEnabled", false, "Enable research speed multiplier. Reduces tech hash needed to about 1/36."), Logger);
            BuildAnywhereOnWaterControl.Init(Config.Bind("HardFog", "BuildAnywhereOnWaterEnabled", true, "Enable ignoring missing ground support build failures, including water placement."), Logger);
            VeinPlacementControl.Init(Config.Bind("HardFog", "VeinPlacementEnabled", true, "Enable better vein placement for future planet vein generation."), Logger);
            OverpoweredMechaFightersControl.Init(Config.Bind("HardFog", "OverpoweredMechaFightersEnabled", false, "Enable stronger mecha fighters: 10x range, 10x damage, and invincibility."), Logger);
            GeothermalPowerLineControl.Init(Config.Bind("HardFog", "GeothermalPowerLineEnabled", true, "Extend geothermal power station connection distance to planet diameter."), Logger);

            I18N.Add(WindowTitleKey, "Dark Fog Control", "黑雾操控");
            I18N.Add(ClearCurrentPlanetKey, "Clear Dark Fog ground bases on current planet", "清理当前星球的地面黑雾基地");
            I18N.Add(ClearCurrentStarKey, "Clear space Dark Fog hives in current star system", "清理当前恒星的太空黑雾巢穴");
            I18N.Add(FillGalaxyHivesKey, "Fill the galaxy with Dark Fog hives", "为整个星系填满黑雾巢穴");
            I18N.Add(SuperThreatReducerHiveKey, "Space Hive Suppression", "太空巢穴降压");
            I18N.Add(SuperThreatReducerGroundKey, "Ground Base Suppression", "地面基地降压");
            I18N.Add(RelayLandingLogicKey, "Relay Landing Logic Modification", "中继站降落逻辑修改");
            I18N.Add(RelayLandingMarkersOnlyKey, "Relay stations only dispatch to markers", "中继站只发往信标");
            I18N.Add(RelayLandingFastLaunchKey, "Launch relay stations faster", "更快的发射中继站");
            I18N.Add(FasterResearchKey, "Research Speed Multiplier", "研究倍速器");
            I18N.Add(VeinPlacementKey, "Better vein placement", "更好的矿物位置");
            I18N.Add(OverpoweredMechaFightersKey, "Fighters fly farther", "战斗机更远的飞行距离");
            I18N.Add(ConstructLowLatitudeRuinsKey, "Construct low-latitude ruins", "构造低纬度废墟");
            I18N.Add(ConstructMidLatitudeRuinsKey, "Construct mid-latitude ruins", "构造中纬度废墟");
            I18N.Add(ConstructHighLatitudeRuinsKey, "Construct high-latitude ruins", "构造高纬度废墟");
            I18N.Add(BuildGeothermalOnIdleRuinsKey, "Build geothermal power stations on idle ruins", "在空闲废墟上建造地热发电站");
            I18N.Add(BuildAnywhereOnWaterKey, "Ignore ground support requirement", "无需地基支撑建造");
            I18N.Add(GeothermalPowerLineKey, "Geothermal power line extension", "地热发电站电力线扩展");
            I18N.Apply();

            // UXAssist 的配置窗口可能晚于插件 Awake 创建，所以用事件延迟注入 HardFog 的页签。
            MyConfigWindow.OnUICreated += CreateUI;
            Log.LogInfo("HardFog 初始化");
        }

        // 插件卸载或游戏关闭时释放事件和 Harmony Patch，避免下次加载时重复挂接或保留失效配置引用。
        public void OnDestroy()
        {
            MyConfigWindow.OnUICreated -= CreateUI;
            SuperThreatReducerControl.Uninit();
            RelayControl.Uninit();
            FasterResearchControl.Uninit();
            BuildAnywhereOnWaterControl.Uninit();
            VeinPlacementControl.Uninit();
            OverpoweredMechaFightersControl.Uninit();
            GeothermalPowerLineControl.Uninit();
        }

        // 创建 HardFog 的配置页；左侧放常驻开关，右侧放立即执行的存档/星球操作按钮。
        private static void CreateUI(MyConfigWindow wnd, RectTransform trans)
        {
            // 使用固定坐标是为了匹配 UXAssist 现有配置窗口布局，不额外引入新的布局系统。
            const float leftX = 10f;
            const float rightX = 400f;
            var y = 10f;

            // 先创建独立页签，所有 HardFog 控件都放在这个 tab 中，避免污染 UXAssist 的其他设置页。
            wnd.AddSplitter(trans, 10f);
            wnd.AddTabGroup(trans, WindowTitleKey, "tab-group-hard-fog");
            RectTransform tab = wnd.AddTab(trans, WindowTitleKey);

            // 这些复选框直接绑定 ConfigEntry；用户切换时控制器会收到 SettingChanged 并更新补丁状态。
            wnd.AddCheckBox(leftX, y, tab, SuperThreatReducerControl.EnabledConfigHive, SuperThreatReducerHiveKey, 16);
            y += 36f;
            wnd.AddCheckBox(leftX, y, tab, SuperThreatReducerControl.EnabledConfigGround, SuperThreatReducerGroundKey, 16);
            y += 36f;
            wnd.AddCheckBox(leftX, y, tab, RelayControl.LandingLogicEnabledConfig, RelayLandingLogicKey, 16);
            y += 36f;
            MyCheckBox markersOnlyCheckBox = wnd.AddCheckBox(leftX + 20f, y, tab, RelayControl.MarkersOnlyConfig, RelayLandingMarkersOnlyKey, 13);
            y += 36f;
            MyCheckBox fastLaunchCheckBox = wnd.AddCheckBox(leftX + 20f, y, tab, RelayControl.FastLaunchEnabledConfig, RelayLandingFastLaunchKey, 13);
            // 两个子选项依赖”中继站降落逻辑修改”，所以父选项关闭时禁用子选项 UI，避免用户误以为独立生效。
            RelayControl.LandingLogicEnabledConfig.SettingChanged += RelayOptionChanged;
            // 配置窗口销毁时解绑本地事件，避免窗口重建后一个配置变更触发多个旧闭包。
            wnd.OnFree += () => { RelayControl.LandingLogicEnabledConfig.SettingChanged -= RelayOptionChanged; };
            RelayOptionChanged(null, null);
            y += 36f;
            wnd.AddCheckBox(leftX, y, tab, FasterResearchControl.EnabledConfig, FasterResearchKey, 16);
            y += 36f;
            wnd.AddCheckBox(leftX, y, tab, BuildAnywhereOnWaterControl.EnabledConfig, BuildAnywhereOnWaterKey, 16);
            y += 36f;
            wnd.AddCheckBox(leftX, y, tab, VeinPlacementControl.EnabledConfig, VeinPlacementKey, 16);
            y += 36f;
            wnd.AddCheckBox(leftX, y, tab, OverpoweredMechaFightersControl.EnabledConfig, OverpoweredMechaFightersKey, 16);
            y += 36f;
            wnd.AddCheckBox(leftX, y, tab, GeothermalPowerLineControl.EnabledConfig, GeothermalPowerLineKey, 16);

            // 右侧按钮会立即修改当前存档/星系状态，所以每个按钮都通过 RunWithButtonDisabled 防止重复点击。
            y = 10f;
            clearCurrentPlanetButton = wnd.AddButton(rightX, y, 240, tab, ClearCurrentPlanetKey, 16, "button-clear-current-planet-dark-fog", OnClearCurrentPlanetClicked);
            y += 36f;
            clearCurrentStarButton = wnd.AddButton(rightX, y, 240, tab, ClearCurrentStarKey, 16, "button-clear-current-star-dark-fog", OnClearCurrentStarClicked);
            y += 36f;
            fillGalaxyHivesButton = wnd.AddButton(rightX, y, 240, tab, FillGalaxyHivesKey, 16, "button-fill-galaxy-dark-fog-hives", OnFillGalaxyHivesClicked);
            // y += 36f;
            // constructLowLatitudeRuinsButton = wnd.AddButton(rightX, y, 260, tab, ConstructLowLatitudeRuinsKey, 16, "button-surface-ruins-construct-low-latitude", OnConstructLowLatitudeRuinsClicked);
            // y += 36f;
            // constructMidLatitudeRuinsButton = wnd.AddButton(rightX, y, 260, tab, ConstructMidLatitudeRuinsKey, 16, "button-surface-ruins-construct-mid-latitude", OnConstructMidLatitudeRuinsClicked);
            // y += 36f;
            // constructHighLatitudeRuinsButton = wnd.AddButton(rightX, y, 260, tab, ConstructHighLatitudeRuinsKey, 16, "button-surface-ruins-construct-high-latitude", OnConstructHighLatitudeRuinsClicked);
            // Ghost UI hook: keep the geothermal helper code, but do not expose the button.
            // y += 36f;
            // buildGeothermalOnIdleRuinsButton = wnd.AddButton(rightX, y, 340, tab, BuildGeothermalOnIdleRuinsKey, 16, "button-surface-ruins-build-geothermal-on-idle-ruins", OnBuildGeothermalOnIdleRuinsClicked);

            void RelayOptionChanged(object sender, EventArgs args)
            {
                // 只控制 UI 可用性，不改子选项的配置值；这样重新启用父选项后能保留用户偏好。
                bool enabled = RelayControl.LandingLogicEnabledConfig.Value;
                markersOnlyCheckBox.SetEnable(enabled);
                fastLaunchCheckBox.SetEnable(enabled);
            }
        }

        // 清理当前星球地面暗雾；按钮逻辑和业务逻辑分开，便于 DarkFogControl 独立维护复杂清理流程。
        private static void OnClearCurrentPlanetClicked()
        {
            RunWithButtonDisabled(clearCurrentPlanetButton, DarkFogControl.ClearCurrentPlanetDarkFog);
        }

        // 清理当前恒星系太空暗雾巢穴；按钮禁用包装可以避免同一帧重复执行破坏对象池状态。
        private static void OnClearCurrentStarClicked()
        {
            RunWithButtonDisabled(clearCurrentStarButton, DarkFogControl.ClearCurrentStarSpaceDarkFog);
        }

        // 向全银河补满暗雾巢穴；这是存档级操作，所以保持为显式按钮而不是自动执行。
        private static void OnFillGalaxyHivesClicked()
        {
            RunWithButtonDisabled(fillGalaxyHivesButton, DarkFogControl.FillGalaxyWithDarkFogHives);
        }

        // 在当前星球构建低纬度废墟，用于测试或快速布置地面废墟玩法。
        private static void OnConstructLowLatitudeRuinsClicked()
        {
            RunWithButtonDisabled(constructLowLatitudeRuinsButton, SurfaceRuinControl.ConstructLowLatitudeRuinsOnCurrentPlanet);
        }

        // 在当前星球构建中纬度废墟，和低/高纬度按钮共用 SurfaceRuinControl 的安全构造逻辑。
        private static void OnConstructMidLatitudeRuinsClicked()
        {
            RunWithButtonDisabled(constructMidLatitudeRuinsButton, SurfaceRuinControl.ConstructMidLatitudeRuinsOnCurrentPlanet);
        }

        // 在当前星球构建高纬度废墟，用于覆盖不同纬度带的废墟生成场景。
        private static void OnConstructHighLatitudeRuinsClicked()
        {
            RunWithButtonDisabled(constructHighLatitudeRuinsButton, SurfaceRuinControl.ConstructHighLatitudeRuinsOnCurrentPlanet);
        }

        // 保留的地热站辅助入口；按钮目前不展示，但函数保留方便以后恢复 UI 时不用重写调用链。
        private static void OnBuildGeothermalOnIdleRuinsClicked()
        {
            RunWithButtonDisabled(buildGeothermalOnIdleRuinsButton, SurfaceRuinControl.BuildGeothermalOnIdleRuinsCurrentPlanet);
        }

        // 执行按钮动作期间临时禁用按钮；即使 action 抛异常，finally 也会恢复按钮状态。
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
