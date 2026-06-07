$ErrorActionPreference = "Stop"

$moduleDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionPath = Join-Path $moduleDir "HardFog.slnx"
$veinPlacementPath = Join-Path $moduleDir "VeinPlacementControl.cs"
$waterBuildPath = Join-Path $moduleDir "BuildAnywhereOnWaterControl.cs"
$pumpAnywherePath = Join-Path $moduleDir "PumpAnywhere.cs"
$hardFogWindowPath = Join-Path $moduleDir "HardFogWindow.cs"
$darkFogControlPath = Join-Path $moduleDir "DarkFogControl.cs"
$fasterRelayLaunchPath = Join-Path $moduleDir "FasterRelayLaunchControl.cs"
$projectPath = Join-Path $moduleDir "HardFog.csproj"
$manifestPath = Join-Path $moduleDir "Package\manifest.json"
$readmePath = Join-Path $moduleDir "Package\README.md"
$packageChangelogPath = Join-Path $moduleDir "Package\CHANGELOG.md"
$rootChangelogPath = Join-Path $moduleDir "..\CHANGELOG.md"
$veinPlacementSource = Get-Content -Encoding UTF8 -LiteralPath $veinPlacementPath -Raw
$waterBuildSource = Get-Content -Encoding UTF8 -LiteralPath $waterBuildPath -Raw
$pumpAnywhereSource = Get-Content -Encoding UTF8 -LiteralPath $pumpAnywherePath -Raw
$hardFogWindowSource = Get-Content -Encoding UTF8 -LiteralPath $hardFogWindowPath -Raw
$darkFogControlSource = Get-Content -Encoding UTF8 -LiteralPath $darkFogControlPath -Raw
$fasterRelayLaunchSource = Get-Content -Encoding UTF8 -LiteralPath $fasterRelayLaunchPath -Raw
$projectSource = Get-Content -Encoding UTF8 -LiteralPath $projectPath -Raw
$manifestSource = Get-Content -Encoding UTF8 -LiteralPath $manifestPath -Raw
$readmeSource = Get-Content -Encoding UTF8 -LiteralPath $readmePath -Raw
$packageChangelogSource = Get-Content -Encoding UTF8 -LiteralPath $packageChangelogPath -Raw
$rootChangelogSource = Get-Content -Encoding UTF8 -LiteralPath $rootChangelogPath -Raw
$projectExecutableSource = $projectSource -replace '<!--[\s\S]*?-->', ''
$hardFogWindowExecutableSource = ($hardFogWindowSource -split "`r?`n" | Where-Object { -not $_.TrimStart().StartsWith("//") }) -join "`n"

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
Assert-TextContains $pumpAnywhereSource 'DEPRECATED' "Pump-anywhere ghost code should be marked deprecated."
Assert-TextContains $pumpAnywhereSource 'Kept for reference only' "Pump-anywhere ghost code should be kept only as reference code."
Assert-TextNotContains $projectExecutableSource 'Compile Include="PumpAnywhere.cs"' "Pump-anywhere control should not be actively compiled into HardFog."
Assert-TextContains $projectSource 'Compile Include="FasterRelayLaunchControl.cs"' "Faster relay launch control should be compiled into HardFog."
Assert-TextContains $hardFogWindowSource '[BepInPlugin("me.liantian.plugin.HardFog", "HardFog", "0.0.22")]' "HardFog plugin version should be 0.0.22."
Assert-TextContains $hardFogWindowSource 'BuildAnywhereOnWaterControl.Init(Config.Bind("HardFog", "BuildAnywhereOnWaterEnabled", true' "Water-build feature should default on in HardFog config."
Assert-TextContains $hardFogWindowSource 'BuildAnywhereOnWaterControl.Uninit();' "Water-build feature should be uninitialized with other controls."
Assert-TextContains $hardFogWindowSource 'wnd.AddCheckBox(leftX, y, tab, BuildAnywhereOnWaterControl.EnabledConfig' "Water-build feature should be exposed in the HardFog UI."
Assert-TextNotContains $hardFogWindowExecutableSource 'button-surface-ruins-build-geothermal-on-idle-ruins' "Geothermal-on-idle-ruins button should be hidden from the HardFog UI."
Assert-TextContains $hardFogWindowSource '// buildGeothermalOnIdleRuinsButton = wnd.AddButton' "Geothermal-on-idle-ruins UI hook should remain as ghost code."
Assert-TextContains $hardFogWindowSource 'SurfaceRuinControl.BuildGeothermalOnIdleRuinsCurrentPlanet' "Geothermal-on-idle-ruins implementation should remain reachable from ghost handler code."
Assert-TextNotContains $hardFogWindowExecutableSource 'PumpAnywhere.Init(' "Pump-anywhere feature should not be initialized by HardFog."
Assert-TextNotContains $hardFogWindowExecutableSource 'PumpAnywhere.Uninit();' "Pump-anywhere feature should not be uninitialized by HardFog."
Assert-TextNotContains $hardFogWindowSource 'wnd.AddCheckBox(x, y, tab, PumpAnywhere.EnabledConfig' "Pump-anywhere feature should be hidden from the HardFog UI."
Assert-TextNotContains $hardFogWindowSource 'PumpAnywhereKey' "Pump-anywhere UI label key should not be registered."
Assert-TextNotContains $hardFogWindowSource 'I18N.Add(PumpAnywhereKey' "Pump-anywhere UI label should not be registered."
Assert-TextContains $hardFogWindowSource 'FasterRelayLaunchControl.Init(Config.Bind("DarkFog", "FasterRelayLaunchEnabled", false' "Faster relay launch feature should default off in HardFog config."
Assert-TextContains $hardFogWindowSource 'FasterRelayLaunchControl.Uninit();' "Faster relay launch feature should be uninitialized with other controls."
Assert-TextContains $hardFogWindowSource 'wnd.AddCheckBox(leftX, y, tab, FasterRelayLaunchControl.EnabledConfig' "Faster relay launch feature should be exposed in the HardFog UI."
Assert-TextContains $hardFogWindowSource 'I18N.Add(FasterRelayLaunchKey, "Launch relay stations faster"' "Faster relay launch UI label should be registered."
Assert-TextContains $manifestSource '"version_number": "0.0.22"' "Package manifest version should match the plugin version."
Assert-TextContains $readmeSource 'Launch relay stations faster' "Package README should document the faster relay launch feature."
Assert-TextContains $packageChangelogSource '## 0.0.22' "Package changelog should include version 0.0.22."
Assert-TextContains $rootChangelogSource '## 0.0.22' "Root changelog should include version 0.0.22."

Assert-TextContains $darkFogControlSource 'ReturnRelaysTargetingPlanet(planet);' "Planet cleanup should return relays before clearing ground bases."
Assert-TextContains $darkFogControlSource 'if (enemyData.dfRelayId > 0)' "Planet cleanup should recognize relay enemies in the space enemy pass."
Assert-TextContains $darkFogControlSource 'skip relay -> enemyData.id' "Planet cleanup should skip killing relay enemies."
Assert-TextContains $darkFogControlSource 'private static void ClearGroundEnemy(PlanetFactory planetFactory, CombatStat[] combatStatsBuffer, int enemyId)' "Planet cleanup should centralize ground enemy removal."
Assert-TextContains $darkFogControlSource 'if (enemyData.dynamic)' "Dynamic ground units should be distinguished from base buildings during cleanup."
Assert-TextContains $darkFogControlSource 'planetFactory.RemoveEnemyWithComponents(enemyId);' "Dynamic ground units should be hard-removed instead of using kill animation cleanup."
Assert-TextContains $darkFogControlSource 'private static bool ReturnRelay(DFRelayComponent relay)' "Planet cleanup should centralize relay return state changes."
Assert-TextContains $darkFogControlSource 'relay.LeaveBase();' "Landed relays should use the game relay leave-base behavior."
Assert-TextContains $darkFogControlSource 'relay.direction = -1;' "Targeted relays should be put into return direction."
Assert-TextContains $darkFogControlSource 'RemoveResidualGroundBaseRecords(planetFactory, bases);' "Planet cleanup should remove residual ground base records after killing ground enemies."
Assert-TextContains $darkFogControlSource 'private static void RemoveResidualGroundBaseRecords(PlanetFactory planetFactory, ObjectPool<DFGBaseComponent> bases)' "Planet cleanup should centralize residual ground base record removal."
Assert-TextContains $darkFogControlSource 'ClearRelaysTargetingGroundBase(planetFactory.planet, baseId);' "Ground base record removal should clear relay references before removing the base record."
Assert-TextContains $darkFogControlSource 'planetFactory.RemoveEnemyWithComponents(enemyId);' "Residual ground base records should be removed through enemy component cleanup when their core enemy remains."
Assert-TextNotContains $darkFogControlSource 'RemoveRuinWithComponet' "Planet cleanup should not delete surface ruins when removing residual ground base records."

Assert-TextContains $fasterRelayLaunchSource 'private const int VanillaRelayDemandInterval = 600;' "Faster relay launch control should document the vanilla relay demand interval."
Assert-TextContains $fasterRelayLaunchSource 'private const int FasterRelayDemandInterval = 120;' "Faster relay launch control should use a 120 hive tick relay demand interval."
Assert-TextContains $fasterRelayLaunchSource '[HarmonyPatch(typeof(EnemyDFHiveSystem), "DetermineRelayDemand")]' "Faster relay launch control should patch relay demand."
Assert-TextContains $fasterRelayLaunchSource 'DispatchOneIdleRelayIfAllowed(__instance);' "Faster relay launch control should replace demand logic with direct idle relay dispatch."
Assert-TextContains $fasterRelayLaunchSource 'relay.direction > 0' "Faster relay launch control should avoid dispatching when a relay is already outbound."
Assert-TextContains $fasterRelayLaunchSource '[HarmonyPatch(typeof(DFRelayComponent), "SearchTargetPlaceProcess")]' "Faster relay launch control should patch target planet selection."
Assert-TextContains $fasterRelayLaunchSource 'CountNonGasPlanets(starData)' "Faster relay launch control should count non-gas target planets."
Assert-TextContains $fasterRelayLaunchSource 'RandomTable.Integer(ref hive.rtseed, nonGasPlanetCount)' "Faster relay launch control should pick a non-gas planet uniformly."
Assert-TextNotContains $fasterRelayLaunchSource 'ArriveBase' "Faster relay launch control should not alter landing arrival logic."
Assert-TextNotContains $fasterRelayLaunchSource 'CheckLandCondition' "Faster relay launch control should not alter landing checks."

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

Assert-TextContains $pumpAnywhereSource '[HarmonyPatch(typeof(BuildTool_Click), "CheckBuildConditions")]' "Pump-anywhere control should patch manual build condition checks."
Assert-TextContains $pumpAnywhereSource '[HarmonyPatch(typeof(BuildTool_BlueprintPaste), "CheckBuildConditions")]' "Pump-anywhere control should patch blueprint paste condition checks."
Assert-TextContains $pumpAnywhereSource 'private static void BuildToolClickCheckBuildConditionsPostfix(BuildTool_Click __instance, ref bool __result)' "Pump-anywhere manual patch should be a postfix that can update the build result."
Assert-TextContains $pumpAnywhereSource 'private static void BuildToolBlueprintPasteCheckBuildConditionsPostfix(BuildTool_BlueprintPaste __instance, ref bool __result)' "Pump-anywhere blueprint patch should be a postfix that can update the build result."
Assert-TextContains $pumpAnywhereSource 'bp.condition != EBuildCondition.NeedWater' "Pump-anywhere control should specifically target NeedWater conditions."
Assert-TextContains $pumpAnywhereSource 'bp.condition = EBuildCondition.Ok;' "Pump-anywhere control should treat allowed NeedWater as buildable."
Assert-TextContains $pumpAnywhereSource 'RemoveConditionErrorBuildings(tool, EBuildCondition.NeedWater)' "Pump-anywhere control should remove raw NeedWater blueprint error tips."
Assert-TextContains $pumpAnywhereSource 'tool._tmpErrorTipsCursor' "Pump-anywhere control should keep the blueprint error tip cursor consistent."
Assert-TextContains $pumpAnywhereSource 'BuildPreview.GetConditionText(EBuildCondition.Ok)' "Pump-anywhere manual build cursor should be refreshed after clearing NeedWater."
Assert-TextContains $pumpAnywhereSource 'waterTypes[i] == waterItemId' "Pump-anywhere control should require the planet water type to match the building."
Assert-TextNotContains $pumpAnywhereSource 'NeedGeothermalResource = EBuildCondition.Ok' "Pump-anywhere control should not clear geothermal resource requirements."
Assert-TextNotContains $pumpAnywhereSource 'condition == EBuildCondition.NeedGeothermalResource' "Pump-anywhere control should not target geothermal resource failures."

$buildOutput = dotnet build $solutionPath -t:Rebuild 2>&1
$buildOutput | Write-Host
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build HardFog.slnx -t:Rebuild failed."
}

Write-Host "HardFog compile check passed."
