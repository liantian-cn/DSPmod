using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace HardFog
{
    [HarmonyPatch]
    internal static class PumpAnywhere
    {
        private const string PatchGuid = "me.liantian.plugin.HardFog.PumpAnywhere";

        internal static ConfigEntry<bool> EnabledConfig { get; private set; }

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
                if (harmony != null)
                {
                    return;
                }

                harmony = Harmony.CreateAndPatchAll(typeof(PumpAnywhere), PatchGuid);
                Log?.LogInfo("PumpAnywhere enabled");
                return;
            }

            if (harmony == null)
            {
                return;
            }

            harmony.UnpatchSelf();
            harmony = null;
            Log?.LogInfo("PumpAnywhere disabled");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildTool_Click), "CheckBuildConditions")]
        private static void BuildToolClickCheckBuildConditionsPostfix(BuildTool_Click __instance, ref bool __result)
        {
            if (__instance?.buildPreviews == null)
            {
                return;
            }

            if (!ClearNeedWaterConditions(__instance.buildPreviews, __instance.planet))
            {
                return;
            }

            __result = IsManualBuildAllowed(__instance.buildPreviews);
            RefreshManualCursor(__instance, __result);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildTool_BlueprintPaste), "CheckBuildConditions")]
        private static void BuildToolBlueprintPasteCheckBuildConditionsPostfix(BuildTool_BlueprintPaste __instance, ref bool __result)
        {
            if (__instance?.bpPool == null || __instance.bpCursor <= 0)
            {
                return;
            }

            if (!ClearNeedWaterConditions(__instance.bpPool, __instance.bpCursor, __instance.planet))
            {
                return;
            }

            RemoveClearedBlueprintErrors(__instance);
            __result = IsBlueprintBuildAllowed(__instance);
            if (__result && __instance.actionBuild?.model != null)
            {
                __instance.actionBuild.model.cursorState = 0;
            }
        }

        private static bool ClearNeedWaterConditions(IList<BuildPreview> previews, PlanetData planet)
        {
            bool changed = false;
            for (int i = 0; i < previews.Count; i++)
            {
                changed |= ClearNeedWaterCondition(previews[i], planet);
            }

            return changed;
        }

        private static bool ClearNeedWaterConditions(BuildPreview[] previews, int count, PlanetData planet)
        {
            bool changed = false;
            int limit = Math.Min(count, previews.Length);
            for (int i = 0; i < limit; i++)
            {
                changed |= ClearNeedWaterCondition(previews[i], planet);
            }

            return changed;
        }

        private static bool ClearNeedWaterCondition(BuildPreview bp, PlanetData planet)
        {
            if (bp == null || bp.condition != EBuildCondition.NeedWater || !IsPlanetWaterAllowed(bp, planet))
            {
                return false;
            }

            bp.condition = EBuildCondition.Ok;
            return true;
        }

        private static bool IsPlanetWaterAllowed(BuildPreview bp, PlanetData planet)
        {
            int waterItemId = planet?.waterItemId ?? 0;
            if (waterItemId == 0 || bp.desc?.waterTypes == null)
            {
                return false;
            }

            int[] waterTypes = bp.desc.waterTypes;
            for (int i = 0; i < waterTypes.Length; i++)
            {
                if (waterTypes[i] == waterItemId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsManualBuildAllowed(IList<BuildPreview> previews)
        {
            for (int i = 0; i < previews.Count; i++)
            {
                BuildPreview bp = previews[i];
                if (bp != null && bp.condition != EBuildCondition.Ok && bp.condition != EBuildCondition.NeedConn)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsBlueprintBuildAllowed(BuildTool_BlueprintPaste tool)
        {
            int limit = Math.Min(tool.bpCursor, tool.bpPool.Length);
            for (int i = 0; i < limit; i++)
            {
                BuildPreview bp = tool.bpPool[i];
                if (bp != null && bp.bpgpuiModelId > 0 && !IsBlueprintAllowedCondition(bp.condition))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsBlueprintAllowedCondition(EBuildCondition condition)
        {
            return condition == EBuildCondition.Ok ||
                condition == EBuildCondition.NeedConn ||
                condition == EBuildCondition.NotEnoughItem;
        }

        private static void RemoveClearedBlueprintErrors(BuildTool_BlueprintPaste tool)
        {
            RemoveCondition(tool._tmp_error_types, EBuildCondition.NeedWater);
            RemoveConditionErrorBuildings(tool, EBuildCondition.NeedWater);

            if (tool.rootGridErrors == null)
            {
                return;
            }

            List<uint> emptyKeys = new List<uint>();
            foreach (KeyValuePair<uint, HashSet<EBuildCondition>> entry in tool.rootGridErrors)
            {
                if (entry.Value == null)
                {
                    emptyKeys.Add(entry.Key);
                    continue;
                }

                entry.Value.Remove(EBuildCondition.NeedWater);
                if (entry.Value.Count == 0)
                {
                    emptyKeys.Add(entry.Key);
                }
            }

            for (int i = 0; i < emptyKeys.Count; i++)
            {
                tool.rootGridErrors.Remove(emptyKeys[i]);
            }
        }

        private static void RemoveCondition(List<EBuildCondition> conditions, EBuildCondition condition)
        {
            if (conditions == null)
            {
                return;
            }

            while (conditions.Remove(condition))
            {
            }
        }

        private static void RemoveConditionErrorBuildings(BuildTool_BlueprintPaste tool, EBuildCondition condition)
        {
            if (tool._tmpErrorBuildings == null || tool._tmpErrorTipsCursor <= 0)
            {
                return;
            }

            int writeIndex = 0;
            int limit = Math.Min(tool._tmpErrorTipsCursor, tool._tmpErrorBuildings.Length);
            for (int readIndex = 0; readIndex < limit; readIndex++)
            {
                ulong error = tool._tmpErrorBuildings[readIndex];
                if ((EBuildCondition)(error >> 48) == condition)
                {
                    continue;
                }

                if (writeIndex != readIndex)
                {
                    tool._tmpErrorBuildings[writeIndex] = error;
                }

                writeIndex++;
            }

            for (int i = writeIndex; i < limit; i++)
            {
                tool._tmpErrorBuildings[i] = 0UL;
            }

            tool._tmpErrorTipsCursor = writeIndex;
        }

        private static void RefreshManualCursor(BuildTool_Click tool, bool allowed)
        {
            if (tool.actionBuild?.model == null)
            {
                return;
            }

            if (allowed)
            {
                tool.actionBuild.model.cursorState = 0;
                tool.actionBuild.model.cursorText = BuildPreview.GetConditionText(EBuildCondition.Ok);
                return;
            }

            for (int i = 0; i < tool.buildPreviews.Count; i++)
            {
                BuildPreview bp = tool.buildPreviews[i];
                if (bp == null || bp.condition == EBuildCondition.Ok || bp.condition == EBuildCondition.NeedConn)
                {
                    continue;
                }

                tool.actionBuild.model.cursorState = -1;
                tool.actionBuild.model.cursorText = bp.conditionText;
                return;
            }
        }
    }
}
