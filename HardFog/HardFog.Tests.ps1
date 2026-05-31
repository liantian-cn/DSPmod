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
Assert-TextContains $hardFogWindowSource '[BepInPlugin("me.liantian.plugin.HardFog", "HardFog", "0.0.15")]' "HardFog plugin version should be 0.0.15."
Assert-TextContains $hardFogWindowSource 'BuildAnywhereOnWaterControl.Init(Config.Bind("HardFog", "BuildAnywhereOnWaterEnabled", true' "Water-build feature should default on in HardFog config."
Assert-TextContains $hardFogWindowSource 'BuildAnywhereOnWaterControl.Uninit();' "Water-build feature should be uninitialized with other controls."
Assert-TextContains $hardFogWindowSource 'wnd.AddCheckBox(x, y, tab, BuildAnywhereOnWaterControl.EnabledConfig' "Water-build feature should be exposed in the HardFog UI."
Assert-TextContains $manifestSource '"version_number": "0.0.15"' "Package manifest version should match the plugin version."
Assert-TextContains $readmeSource 'Build anywhere on liquid oceans' "Package README should document the water-build feature."

Assert-TextContains $waterBuildSource '[HarmonyPatch(typeof(VFPreload), "InvokeOnLoadWorkEnded")]' "Water-build control should reapply prefab overrides after game preload completes."
Assert-TextContains $waterBuildSource 'ThemeProto[] themes = LDB.themes.dataArray;' "Water-build control should gather water types from theme data."
Assert-TextContains $waterBuildSource 'theme.WaterItemId > 0' "Water-build control should include every positive theme liquid item id."
Assert-TextContains $waterBuildSource 'desc.allowBuildInWater = true;' "Water-build control should make eligible prefabs buildable in water."
Assert-TextContains $waterBuildSource 'desc.needBuildInWaterTech = false;' "Water-build control should bypass per-building water tech requirements."
Assert-TextContains $waterBuildSource 'desc.waterTypes = waterTypes;' "Water-build control should assign all known liquid ocean item ids."
Assert-TextContains $waterBuildSource 'RestoreOriginals();' "Water-build control should restore original prefab values when disabled."
Assert-TextContains $waterBuildSource 'desc.waterPoints.Length > 0' "Water-build control should not alter water-resource-specific buildings."
Assert-TextContains $waterBuildSource 'desc.minerType != EMinerType.None' "Water-build control should not alter miners."
Assert-TextContains $waterBuildSource 'desc.veinMiner || desc.oilMiner' "Water-build control should not alter vein or oil miners."
Assert-TextContains $waterBuildSource 'desc.geothermal' "Water-build control should not alter geothermal placement rules."
Assert-TextContains $waterBuildSource 'desc.isInserter' "Water-build control should not alter inserter connection rules."
Assert-TextContains $waterBuildSource 'desc.addonType != EAddonType.None && !desc.isBelt && !desc.isTurret' "Water-build control should preserve non-belt addon placement rules."

$buildOutput = dotnet build $solutionPath -t:Rebuild 2>&1
$buildOutput | Write-Host
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build HardFog.slnx -t:Rebuild failed."
}

Write-Host "HardFog compile check passed."
