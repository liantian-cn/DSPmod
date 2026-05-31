$ErrorActionPreference = "Stop"

$moduleDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionPath = Join-Path $moduleDir "HardFog.slnx"
$veinPlacementPath = Join-Path $moduleDir "VeinPlacementControl.cs"
$waterBuildPath = Join-Path $moduleDir "BuildAnywhereOnWaterControl.cs"
$hardFogWindowPath = Join-Path $moduleDir "HardFogWindow.cs"
$projectPath = Join-Path $moduleDir "HardFog.csproj"
$manifestPath = Join-Path $moduleDir "Package\manifest.json"
$readmePath = Join-Path $moduleDir "Package\README.md"
$veinPlacementSource = Get-Content -LiteralPath $veinPlacementPath -Raw
$waterBuildSource = Get-Content -LiteralPath $waterBuildPath -Raw
$hardFogWindowSource = Get-Content -LiteralPath $hardFogWindowPath -Raw
$projectSource = Get-Content -LiteralPath $projectPath -Raw
$manifestSource = Get-Content -LiteralPath $manifestPath -Raw
$readmeSource = Get-Content -LiteralPath $readmePath -Raw

function Assert-SourceContains([string]$needle, [string]$message) {
    if (-not $veinPlacementSource.Contains($needle)) {
        throw $message
    }
}

function Assert-TextContains([string]$source, [string]$needle, [string]$message) {
    if (-not $source.Contains($needle)) {
        throw $message
    }
}

function Assert-TextNotContains([string]$source, [string]$needle, [string]$message) {
    if ($source.Contains($needle)) {
        throw $message
    }
}

function Assert-SourceNotContains([string]$needle, [string]$message) {
    if ($veinPlacementSource.Contains($needle)) {
        throw $message
    }
}

Assert-SourceContains "private const int GroupPlacementAttempts = 24;" "Vein placement should try each placement phase 24 times."
Assert-SourceContains "private const int LongitudeExpansionPasses = 100;" "Vein placement should run 100 expanded longitude fallback passes."
Assert-SourceContains "private const float LongitudeExpansionStepDegrees = 30f;" "Vein placement should expand longitude by 30 degrees per fallback pass."
Assert-SourceContains "EnsureBirthPointDirection(planet);" "Vein placement should ensure PlanetData.birthPoint exists before placing groups."
Assert-SourceContains "float centerLongitude = LongitudeFromDirection(birthDirection);" "Vein placement target window should be centered on birthPoint longitude."
Assert-SourceContains "placedCenters.Add(birthDirection);" "The first vein group should be placed at birthPoint."
Assert-SourceContains "lastSuccessfulCenter" "Failed group placement should keep later groups anchored to the last successful center."
Assert-SourceContains "GetOriginalGroupCenter" "Failed group placement should preserve and block against the original group center."
Assert-SourceContains "List<VeinGroupWork> groups = InterleaveGroupsByType(CollectGroups(planet));" "Vein groups should be interleaved by vein type before placement."
Assert-SourceContains "Dictionary<EVeinType, List<VeinGroupWork>> byType" "Vein group interleaving should bucket groups by vein type."
Assert-SourceContains "for (int round = 0; round < largestTypeGroupCount; round++)" "Vein group interleaving should place one group from each type per round."
Assert-SourceContains "if (round < typeGroups[i].Count)" "Vein group interleaving should skip vein types that do not have a group for the current round."
Assert-SourceNotContains "groups.Sort(" "Vein groups should not be block-sorted by vein type."
Assert-SourceContains "private static bool IsWaterAllowedVeinGroup(VeinGroupWork group)" "Vein placement should centralize water-allowed vein type checks."
Assert-SourceContains "group.Type == EVeinType.Oil || group.Type == EVeinType.Bamboo" "Only oil and spiniform stalagmite vein groups should be allowed to use water candidates."
Assert-SourceContains "private static bool IsValidTerrainCandidate(PlanetData planet, VeinGroupWork group, Vector3 candidate)" "Vein placement should validate candidate terrain before accepting a group center."
Assert-SourceContains "planet.data.QueryHeight(candidate) >= planet.radius" "Non-water vein groups should reject candidate centers below the planet water radius."
Assert-SourceContains "IsValidTerrainCandidate(planet, group, candidate)" "Vein placement should skip water candidates before accepting a group center."
Assert-SourceNotContains "initialLongitude" "Vein placement should not choose a random target longitude."

Assert-TextContains $projectSource 'Compile Include="BuildAnywhereOnWaterControl.cs"' "Build-anywhere-on-water control should be compiled into HardFog."
Assert-TextContains $hardFogWindowSource '[BepInPlugin("me.liantian.plugin.HardFog", "HardFog", "0.0.16")]' "HardFog plugin version should be 0.0.16."
Assert-TextContains $hardFogWindowSource 'BuildAnywhereOnWaterControl.Init(Config.Bind("HardFog", "BuildAnywhereOnWaterEnabled", true' "Water-build feature should default on in HardFog config."
Assert-TextContains $hardFogWindowSource 'BuildAnywhereOnWaterControl.Uninit();' "Water-build feature should be uninitialized with other controls."
Assert-TextContains $hardFogWindowSource 'wnd.AddCheckBox(x, y, tab, BuildAnywhereOnWaterControl.EnabledConfig' "Water-build feature should be exposed in the HardFog UI."
Assert-TextContains $manifestSource '"version_number": "0.0.16"' "Package manifest version should match the plugin version."
Assert-TextContains $readmeSource 'Ignore ground support requirement' "Package README should document the ground-support override feature."

Assert-TextContains $waterBuildSource '[HarmonyPatch(typeof(BuildTool_Click), "CheckBuildConditions")]' "Water-build control should patch manual build condition checks."
Assert-TextContains $waterBuildSource '[HarmonyPatch(typeof(BuildTool_BlueprintPaste), "CheckBuildConditions")]' "Water-build control should patch blueprint paste condition checks."
Assert-TextContains $waterBuildSource 'private static void BuildToolClickCheckBuildConditionsPostfix(BuildTool_Click __instance, ref bool __result)' "Manual build patch should be a postfix that can update the build result."
Assert-TextContains $waterBuildSource 'private static void BuildToolBlueprintPasteCheckBuildConditionsPostfix(BuildTool_BlueprintPaste __instance, ref bool __result)' "Blueprint build patch should be a postfix that can update the build result."
Assert-TextContains $waterBuildSource 'bp.condition == EBuildCondition.NeedGround' "Water-build control should specifically clear NeedGround conditions."
Assert-TextContains $waterBuildSource 'bp.condition = EBuildCondition.Ok;' "Water-build control should treat NeedGround as buildable."
Assert-TextContains $waterBuildSource 'RemoveClearedBlueprintErrors(__instance' "Water-build control should remove stale blueprint errors after clearing NeedGround."
Assert-TextContains $waterBuildSource 'RemoveConditionErrorBuildings(tool, EBuildCondition.NeedGround)' "Water-build control should remove raw NeedGround blueprint error tips."
Assert-TextContains $waterBuildSource 'tool._tmpErrorTipsCursor' "Water-build control should keep the blueprint error tip cursor consistent."
Assert-TextContains $waterBuildSource 'BuildPreview.GetConditionText(EBuildCondition.Ok)' "Manual build cursor text should be refreshed after clearing NeedGround."
Assert-TextContains $waterBuildSource 'RefreshManualCursor(__instance, __result)' "Manual build cursor should be refreshed after recomputing the result."
Assert-TextContains $waterBuildSource 'bp.condition == EBuildCondition.ConnWithErrorBuilding' "Blueprint connection errors caused by cleared NeedGround previews should be rechecked."
Assert-TextContains $waterBuildSource 'IsBlueprintAllowedCondition' "Blueprint result recomputation should preserve original allowed conditions."
Assert-TextNotContains $waterBuildSource 'desc.allowBuildInWater = true;' "Water-build control should no longer mutate PrefabDesc water support flags."
Assert-TextNotContains $waterBuildSource 'ThemeProto[] themes = LDB.themes.dataArray;' "Water-build control should no longer depend on theme water type data."
Assert-TextNotContains $waterBuildSource '[HarmonyPatch(typeof(VFPreload), "InvokeOnLoadWorkEnded")]' "Water-build control should no longer patch preload for PrefabDesc overrides."

$buildOutput = dotnet build $solutionPath -t:Rebuild 2>&1
$buildOutput | Write-Host
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build HardFog.slnx -t:Rebuild failed."
}

Write-Host "HardFog compile check passed."
