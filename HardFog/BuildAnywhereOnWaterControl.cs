using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace HardFog
{
    [HarmonyPatch]
    internal static class BuildAnywhereOnWaterControl
    {
        private const string PatchGuid = "me.liantian.plugin.HardFog.BuildAnywhereOnWater";

        internal static ConfigEntry<bool> EnabledConfig { get; private set; }

        private static readonly Dictionary<PrefabDesc, OriginalBuildCondition> OriginalStates = new Dictionary<PrefabDesc, OriginalBuildCondition>();
        private static ManualLogSource Log;
        private static Harmony harmony;
        private static EventHandler settingChangedHandler;

        internal static void Init(ConfigEntry<bool> enabledConfig, ManualLogSource log)
        {
            if (EnabledConfig != null && settingChangedHandler != null)
            {
                EnabledConfig.SettingChanged -= settingChangedHandler;
            }

            Log = log;
            EnabledConfig = enabledConfig;
            settingChangedHandler = OnSettingChanged;
            EnabledConfig.SettingChanged += settingChangedHandler;
            SetActive(EnabledConfig.Value);
        }

        internal static void Uninit()
        {
            if (EnabledConfig != null && settingChangedHandler != null)
            {
                EnabledConfig.SettingChanged -= settingChangedHandler;
            }

            SetActive(false);
            settingChangedHandler = null;
            EnabledConfig = null;
            Log = null;
        }

        private static void OnSettingChanged(object sender, EventArgs args)
        {
            SetActive(EnabledConfig != null && EnabledConfig.Value);
        }

        private static void SetActive(bool active)
        {
            if (active)
            {
                if (harmony == null)
                {
                    harmony = Harmony.CreateAndPatchAll(typeof(BuildAnywhereOnWaterControl), PatchGuid);
                    Log?.LogInfo("BuildAnywhereOnWater enabled");
                }

                ApplyOverrides();
                return;
            }

            RestoreOriginals();
            if (harmony == null)
            {
                return;
            }

            harmony.UnpatchSelf();
            harmony = null;
            Log?.LogInfo("BuildAnywhereOnWater disabled");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(VFPreload), "InvokeOnLoadWorkEnded")]
        private static void VFPreloadInvokeOnLoadWorkEndedPostfix()
        {
            if (EnabledConfig != null && EnabledConfig.Value)
            {
                ApplyOverrides();
            }
        }

        private static void ApplyOverrides()
        {
            if (LDB.items?.dataArray == null || LDB.themes?.dataArray == null)
            {
                return;
            }

            int[] waterTypes = CollectThemeWaterTypes();
            if (waterTypes.Length == 0)
            {
                return;
            }

            int newlyChanged = 0;
            ItemProto[] items = LDB.items.dataArray;
            for (int i = 0; i < items.Length; i++)
            {
                ItemProto item = items[i];
                PrefabDesc desc = item?.prefabDesc;
                if (!ShouldOverride(item, desc))
                {
                    continue;
                }

                if (!OriginalStates.ContainsKey(desc))
                {
                    OriginalStates.Add(desc, new OriginalBuildCondition(desc));
                    newlyChanged++;
                }

                desc.allowBuildInWater = true;
                desc.needBuildInWaterTech = false;
                desc.waterTypes = waterTypes;
            }

            if (newlyChanged > 0)
            {
                Log?.LogInfo("BuildAnywhereOnWater patched " + newlyChanged + " prefab descriptions");
            }
        }

        private static int[] CollectThemeWaterTypes()
        {
            ThemeProto[] themes = LDB.themes.dataArray;
            List<int> waterTypes = new List<int>();
            for (int i = 0; i < themes.Length; i++)
            {
                ThemeProto theme = themes[i];
                if (theme != null && theme.WaterItemId > 0 && !waterTypes.Contains(theme.WaterItemId))
                {
                    waterTypes.Add(theme.WaterItemId);
                }
            }

            waterTypes.Sort();
            return waterTypes.ToArray();
        }

        private static bool ShouldOverride(ItemProto item, PrefabDesc desc)
        {
            if (item == null || desc == null || !item.CanBuild || !item.IsEntity)
            {
                return false;
            }

            if (desc.landPoints == null || desc.landPoints.Length == 0)
            {
                return false;
            }

            if (desc.waterPoints != null && desc.waterPoints.Length > 0)
            {
                return false;
            }

            if (desc.minerType != EMinerType.None || desc.veinMiner || desc.oilMiner)
            {
                return false;
            }

            if (desc.geothermal || desc.isInserter)
            {
                return false;
            }

            if (desc.addonType != EAddonType.None && !desc.isBelt && !desc.isTurret)
            {
                return false;
            }

            return true;
        }

        private static void RestoreOriginals()
        {
            if (OriginalStates.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<PrefabDesc, OriginalBuildCondition> pair in OriginalStates)
            {
                if (pair.Key != null)
                {
                    pair.Value.Restore(pair.Key);
                }
            }

            Log?.LogInfo("BuildAnywhereOnWater restored " + OriginalStates.Count + " prefab descriptions");
            OriginalStates.Clear();
        }

        private struct OriginalBuildCondition
        {
            private readonly bool allowBuildInWater;
            private readonly bool needBuildInWaterTech;
            private readonly int[] waterTypes;

            internal OriginalBuildCondition(PrefabDesc desc)
            {
                allowBuildInWater = desc.allowBuildInWater;
                needBuildInWaterTech = desc.needBuildInWaterTech;
                waterTypes = desc.waterTypes;
            }

            internal void Restore(PrefabDesc desc)
            {
                desc.allowBuildInWater = allowBuildInWater;
                desc.needBuildInWaterTech = needBuildInWaterTech;
                desc.waterTypes = waterTypes;
            }
        }
    }
}
