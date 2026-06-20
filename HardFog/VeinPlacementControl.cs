using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HardFog
{
    // 矿脉重排控制器：在星球矿脉生成完成后，把矿组重新布置到出生点附近的可控窗口内。
    [HarmonyPatch]
    internal static class VeinPlacementControl
    {
        // 独立 Harmony ID，关闭配置时只卸载矿脉放置补丁。
        private const string PatchGuid = "me.liantian.plugin.HardFog.VeinPlacement";
        // 给随机种子加固定盐，保证本 mod 的重排随机流和游戏原始矿脉随机流分离。
        private const int ModSeedSalt = 0x56_50_30_31;
        // 目标窗口限制矿组主要分布在出生点经度附近和中低纬度，减少玩家前期找矿成本。
        private const float TargetLatitudeDegrees = 40f;
        private const float TargetLongitudeDegrees = 60f;
        // 普通矿和油井使用不同组间距，因为油井原本数量和占地形态不同。
        private const float NormalGroupSpacing = 196f;
        private const float OilGroupSpacing = 100f;
        // 同组内矿点的局部间距和最大半径，用来保留矿脉“成团但不重叠”的形状。
        private const float InnerVeinMinDistanceSqr = 0.5f;
        private const float InnerVeinMaxRadiusSqr = 13f;
        // 中心点和局部形状尝试次数；失败时逐步放宽经度窗口或使用 fallback，保证不丢矿。
        private const int GroupPlacementAttempts = 24;
        private const int LongitudeExpansionPasses = 100;
        private const float LongitudeExpansionStepDegrees = 30f;
        private const int LocalShapePasses = 20;
        private const int LocalFallbackAttempts = 8192;

        // UI 绑定的配置开关；开关变化会安装或卸载矿脉生成 Postfix。
        internal static ConfigEntry<bool> EnabledConfig { get; private set; }

        private static ManualLogSource Log;
        private static Harmony harmony;
        private static EventHandler settingChangedHandler;

        // 记录需要铺设地基的位置，键为星球 ID，值为需要铺设地基的位置列表。
        // 矿脉生成发生在后台线程，工厂创建在主线程，需要跨线程传递数据。
        private static readonly Dictionary<int, List<Vector3>> PendingFoundationPositions = new Dictionary<int, List<Vector3>>();
        private static readonly object PendingFoundationLock = new object();

        // PlanetAlgorithm.planet 是非公开字段，用 FieldRef 读取，避免每次反射 GetValue 的开销。
        private static readonly AccessTools.FieldRef<PlanetAlgorithm, PlanetData> PlanetRef =
            AccessTools.FieldRefAccess<PlanetAlgorithm, PlanetData>("planet");

        // 初始化配置监听，并按当前配置激活或关闭矿脉重排补丁。
        internal static void Init(ConfigEntry<bool> enabledConfig, ManualLogSource log)
        {
            // 防止重复 Init 时旧事件处理器保留，导致配置变更重复 Patch。
            if (EnabledConfig != null && settingChangedHandler != null)
            {
                EnabledConfig.SettingChanged -= settingChangedHandler;
            }

            // 保存配置和日志引用，后续由 OnSettingChanged 统一驱动 SetActive。
            Log = log;
            EnabledConfig = enabledConfig;
            settingChangedHandler = OnSettingChanged;
            EnabledConfig.SettingChanged += settingChangedHandler;
            SetActive(EnabledConfig.Value);
        }

        // 卸载时解绑配置事件并卸载 Harmony，避免静态引用跨插件生命周期残留。
        internal static void Uninit()
        {
            // 先解绑事件，再清空配置引用，避免旧 ConfigEntry 继续回调。
            if (EnabledConfig != null && settingChangedHandler != null)
            {
                EnabledConfig.SettingChanged -= settingChangedHandler;
            }

            SetActive(false);
            settingChangedHandler = null;
            EnabledConfig = null;
            Log = null;
        }

        // 配置变更入口；将当前 bool 值转换成补丁安装状态。
        private static void OnSettingChanged(object sender, EventArgs args)
        {
            SetActive(EnabledConfig != null && EnabledConfig.Value);
        }

        // 安装或卸载所有 PlanetAlgorithm.GenerateVeins Postfix。
        private static void SetActive(bool active)
        {
            if (active)
            {
                // 已经安装时直接返回，避免同一颗星球生成时 ApplyPlacement 被重复执行。
                if (harmony != null)
                {
                    return;
                }

                // 多个 PlanetAlgorithm 子类各有 GenerateVeins，CreateAndPatchAll 会安装文件底部的所有 Postfix。
                harmony = Harmony.CreateAndPatchAll(typeof(VeinPlacementControl), PatchGuid);
                Log?.LogInfo("VeinPlacement enabled");
                return;
            }

            // 未安装时关闭没有需要恢复的状态。
            if (harmony == null)
            {
                return;
            }

            // 卸载后新生成星球回到原版矿脉放置；已生成星球不会被回滚。
            harmony.UnpatchSelf();
            harmony = null;
            Log?.LogInfo("VeinPlacement disabled");
        }

        // 统一的矿脉重排入口；在原版 GenerateVeins 完成后移动矿点并重新汇总矿组。
        private static void ApplyPlacement(PlanetAlgorithm algorithm)
        {
            PlanetData planet = GetPlanet(algorithm);
            // 气态行星、无原始数据或无矿脉时不能重排。
            if (planet == null || planet.type == EPlanetType.Gas || planet.data == null || planet.data.veinPool == null || planet.data.veinCursor <= 1)
            {
                return;
            }

            // 先按 groupIndex 收集矿组，再交错不同矿种，避免同类矿组连续放置导致空间竞争偏斜。
            List<VeinGroupWork> groups = InterleaveGroupsByType(CollectGroups(planet));
            if (groups.Count == 0)
            {
                return;
            }

            // 使用由星球参数派生的确定性随机数，保证同一星球同一 mod 版本生成结果可复现。
            DotNet35Random rng = CreateRandom(planet);
            Vector3 birthDirection = EnsureBirthPointDirection(planet);
            // 出生点方向是整个窗口的锚点，读不到就放弃，避免把矿组随机散到不可预期位置。
            if (birthDirection.sqrMagnitude < 0.5f)
            {
                Log?.LogWarning($"Unable to read birth point on planet {planet.displayName ?? planet.name}");
                return;
            }

            // 先为每个矿组找中心，再按中心重新生成该组内所有矿点的位置。
            List<Vector3> centers = PlaceGroupCenters(planet, groups, birthDirection, rng);
            PlanetRawData data = planet.data;
            for (int i = 0; i < groups.Count; i++)
            {
                // center 为 zero 表示该组没有找到安全新位置，保留原版矿点位置。
                if (centers[i].sqrMagnitude < 0.5f)
                {
                    continue;
                }

                RegenerateGroupVeins(planet, data, groups[i], centers[i], rng);
            }

            // 移动矿点后必须重新汇总矿组，否则矿脉计数、UI 和采矿逻辑可能仍用旧数据。
            planet.SummarizeVeinGroups();
        }

        // 从 PlanetAlgorithm 读取当前星球；算法子类共享基类私有字段，所以用 FieldRef。
        private static PlanetData GetPlanet(PlanetAlgorithm algorithm)
        {
            // 理论上 Postfix 会传入实例，但防御空值可避免 Harmony 异常时崩溃。
            if (algorithm == null)
            {
                return null;
            }

            try
            {
                return PlanetRef(algorithm);
            }
            catch (Exception ex)
            {
                // 游戏更新字段名或结构变化时会走这里，日志提示维护者检查反射入口。
                Log?.LogWarning($"Unable to read PlanetAlgorithm.planet: {ex.Message}");
                return null;
            }
        }

        // 为某颗星球创建确定性随机数；不同星球、算法和种子会得到不同重排结果。
        private static DotNet35Random CreateRandom(PlanetData planet)
        {
            unchecked
            {
                // unchecked 允许整数溢出自然回绕，用作轻量哈希混合。
                int seed = planet.seed;
                seed = (seed * 397) ^ planet.id;
                seed = (seed * 397) ^ planet.algoId;
                seed ^= ModSeedSalt;
                return new DotNet35Random(seed);
            }
        }

        // 从 veinPool 收集所有有效矿组，并记录每组包含的 veinId。
        private static List<VeinGroupWork> CollectGroups(PlanetData planet)
        {
            Dictionary<short, VeinGroupWork> byGroup = new Dictionary<short, VeinGroupWork>();
            List<VeinGroupWork> groups = new List<VeinGroupWork>();
            VeinData[] veinPool = planet.data.veinPool;
            int veinCursor = planet.data.veinCursor;

            // veinPool 从 1 开始使用，id 必须匹配索引才是有效矿点。
            for (int i = 1; i < veinCursor; i++)
            {
                if (veinPool[i].id != i || veinPool[i].type == EVeinType.None)
                {
                    continue;
                }

                short groupIndex = veinPool[i].groupIndex;
                // groupIndex <= 0 的矿点无法按组处理，保留原样。
                if (groupIndex <= 0)
                {
                    continue;
                }

                VeinGroupWork group;
                // 首次遇到某个 groupIndex 时创建工作对象，并保存该组矿种。
                if (!byGroup.TryGetValue(groupIndex, out group))
                {
                    group = new VeinGroupWork(groupIndex, veinPool[i].type);
                    byGroup[groupIndex] = group;
                    groups.Add(group);
                }

                // 只保存 veinId，后续重排时从原始 veinPool 读写实际 VeinData。
                group.VeinIds.Add(i);
            }

            return groups;
        }

        // 把不同矿种的矿组交错排列，降低某一种矿连续抢占出生点附近位置的概率。
        private static List<VeinGroupWork> InterleaveGroupsByType(List<VeinGroupWork> groups)
        {
            Dictionary<EVeinType, List<VeinGroupWork>> byType = new Dictionary<EVeinType, List<VeinGroupWork>>();
            List<List<VeinGroupWork>> typeGroups = new List<List<VeinGroupWork>>();
            int largestTypeGroupCount = 0;

            // 先按矿种分桶，同时记录最大桶长度用于轮询输出。
            for (int i = 0; i < groups.Count; i++)
            {
                VeinGroupWork group = groups[i];
                List<VeinGroupWork> typeGroup;
                if (!byType.TryGetValue(group.Type, out typeGroup))
                {
                    typeGroup = new List<VeinGroupWork>();
                    byType[group.Type] = typeGroup;
                    typeGroups.Add(typeGroup);
                }

                typeGroup.Add(group);
                // largestTypeGroupCount 决定后面需要轮询多少轮。
                if (typeGroup.Count > largestTypeGroupCount)
                {
                    largestTypeGroupCount = typeGroup.Count;
                }
            }

            List<VeinGroupWork> interleaved = new List<VeinGroupWork>(groups.Count);
            // 每轮从每个矿种桶取一个组，形成 typeA/typeB/typeC/typeA 的放置顺序。
            for (int round = 0; round < largestTypeGroupCount; round++)
            {
                for (int i = 0; i < typeGroups.Count; i++)
                {
                    // 某些矿种桶较短，轮到空位时跳过。
                    if (round < typeGroups[i].Count)
                    {
                        interleaved.Add(typeGroups[i][round]);
                    }
                }
            }

            return interleaved;
        }

        // Fisher-Yates 洗牌工具；当前文件保留给需要随机重排列表时使用。
        private static void Shuffle<T>(IList<T> items, DotNet35Random rng)
        {
            // 从尾到头交换，保证每个排列概率一致。
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T temp = items[i];
                items[i] = items[j];
                items[j] = temp;
            }
        }

        // 确保星球有出生点，并返回出生点方向；矿脉窗口以出生点经度为中心。
        private static Vector3 EnsureBirthPointDirection(PlanetData planet)
        {
            // 某些生成时机 birthPoint 还没算出来，主动触发一次生成。
            if (planet.birthPoint.sqrMagnitude < 1E-08f)
            {
                try
                {
                    planet.GenBirthPoints();
                }
                catch (Exception ex)
                {
                    Log?.LogWarning($"Unable to generate birth point on planet {planet.displayName ?? planet.name}: {ex.Message}");
                }
            }

            // 仍然没有出生点就返回 zero，让调用方放弃重排。
            if (planet.birthPoint.sqrMagnitude < 1E-08f)
            {
                return Vector3.zero;
            }

            // 后续算法在单位球面上计算，所以只需要方向。
            return planet.birthPoint.normalized;
        }

        // 为每个矿组选择新的中心方向；第一组锚定出生点，后续组围绕上一个成功中心逐步扩散。
        private static List<Vector3> PlaceGroupCenters(PlanetData planet, List<VeinGroupWork> groups, Vector3 birthDirection, DotNet35Random rng)
        {
            List<Vector3> centers = new List<Vector3>(groups.Count);
            List<Vector3> placedCenters = new List<Vector3>(groups.Count);
            // 没有矿组时直接返回空中心列表。
            if (groups.Count == 0)
            {
                return centers;
            }

            // 窗口经度固定为出生点经度，保证矿组总体围绕出生区展开。
            float centerLongitude = LongitudeFromDirection(birthDirection);
            // 第一组直接放在出生点方向，确保玩家出生附近至少有一个矿组。
            centers.Add(birthDirection);
            placedCenters.Add(birthDirection);
            Vector3 lastSuccessfulCenter = birthDirection;

            // 后续矿组以“上一个成功中心”为 anchor，形成逐步向外扩散的空间分布。
            for (int i = 1; i < groups.Count; i++)
            {
                Vector3 center;
                if (TryPlaceGroupCenter(planet, groups[i], lastSuccessfulCenter, centerLongitude, rng, placedCenters, out center))
                {
                    // 成功找到中心后加入已占用列表，后续矿组会避开它。
                    centers.Add(center);
                    placedCenters.Add(center);
                    lastSuccessfulCenter = center;
                    continue;
                }

                // 找不到合适位置时该组保留原版位置，centers 用 zero 标记。
                centers.Add(Vector3.zero);
                Vector3 originalCenter = GetOriginalGroupCenter(planet, groups[i]);
                if (originalCenter.sqrMagnitude > 0.5f)
                {
                    // 原位置也加入避让列表，避免后续新矿组压到这个保留原位的矿组上。
                    placedCenters.Add(originalCenter);
                }
            }

            return centers;
        }

        // 尝试为单个矿组选择中心：先按距离环带扩散，再在目标窗口随机，最后逐步放宽经度限制。
        private static bool TryPlaceGroupCenter(PlanetData planet, VeinGroupWork group, Vector3 anchor, float centerLongitude, DotNet35Random rng, List<Vector3> placedCenters, out Vector3 center)
        {
            float minDistanceSqr = GetMinDistanceSqr(planet, group);
            float minDistance = Mathf.Sqrt(minDistanceSqr);

            // 先在 anchor 周围 1-4 倍最小间距的环带尝试，让矿组分布有连续扩散感。
            for (int band = 1; band <= 3; band++)
            {
                if (TryPlaceOnDistanceBand(planet, group, anchor, minDistance * band, minDistance * (band + 1), centerLongitude, TargetLongitudeDegrees, rng, placedCenters, minDistanceSqr, out center))
                {
                    return true;
                }
            }

            // 环带失败后，在出生点经度窗口内随机找点。
            if (TryPlaceRandomInWindow(planet, group, centerLongitude, TargetLongitudeDegrees, rng, placedCenters, minDistanceSqr, out center))
            {
                return true;
            }

            // 如果窗口太挤，逐步扩大经度半宽；这样优先满足出生点附近，实在不行再放远。
            for (int pass = 1; pass <= LongitudeExpansionPasses; pass++)
            {
                float longitudeHalfWidth = TargetLongitudeDegrees + LongitudeExpansionStepDegrees * pass;
                if (TryPlaceRandomInWindow(planet, group, centerLongitude, longitudeHalfWidth, rng, placedCenters, minDistanceSqr, out center))
                {
                    return true;
                }
            }

            // 所有策略失败，调用方会保留该组原版位置。
            center = Vector3.zero;
            return false;
        }

        // 在 anchor 周围指定弦长范围内尝试随机中心点。
        private static bool TryPlaceOnDistanceBand(PlanetData planet, VeinGroupWork group, Vector3 anchor, float minDistance, float maxDistance, float centerLongitude, float longitudeHalfWidthDegrees, DotNet35Random rng, List<Vector3> placedCenters, float minDistanceSqr, out Vector3 center)
        {
            // 多次尝试不同角度和距离，直到找到满足窗口、地形和组间距的候选。
            for (int attempt = 0; attempt < GroupPlacementAttempts; attempt++)
            {
                float distance = minDistance + (float)rng.NextDouble() * (maxDistance - minDistance);
                float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
                Vector3 candidate = DirectionAtChordDistance(anchor, distance, angle);
                // 候选必须落在目标窗口内，并与所有已放置中心保持足够距离。
                if (IsValidCandidate(planet, group, candidate, centerLongitude, longitudeHalfWidthDegrees, placedCenters, minDistanceSqr))
                {
                    center = candidate;
                    return true;
                }
            }

            center = Vector3.zero;
            return false;
        }

        // 在纬度/经度窗口内随机尝试中心点，不依赖当前 anchor。
        private static bool TryPlaceRandomInWindow(PlanetData planet, VeinGroupWork group, float centerLongitude, float longitudeHalfWidthDegrees, DotNet35Random rng, List<Vector3> placedCenters, float minDistanceSqr, out Vector3 center)
        {
            // 固定尝试次数让生成时间可控，失败后交给外层扩窗或保留原位。
            for (int attempt = 0; attempt < GroupPlacementAttempts; attempt++)
            {
                Vector3 candidate = RandomDirectionInTargetWindow(centerLongitude, longitudeHalfWidthDegrees, rng);
                // 窗口随机点已经满足窗口约束，只需要检查地形和组间距。
                if (IsValidCenter(planet, group, candidate, placedCenters, minDistanceSqr))
                {
                    center = candidate.normalized;
                    return true;
                }
            }

            center = Vector3.zero;
            return false;
        }

        // 在目标纬度和经度窗口内生成一个单位球面方向。
        private static Vector3 RandomDirectionInTargetWindow(float centerLongitude, float longitudeHalfWidthDegrees, DotNet35Random rng)
        {
            // 纬度和经度都在对称区间内随机，centerLongitude 用来围绕出生点经度。
            float lat = Deg2Rad((float)((rng.NextDouble() * 2.0 - 1.0) * TargetLatitudeDegrees));
            float lon = centerLongitude + Deg2Rad((float)((rng.NextDouble() * 2.0 - 1.0) * longitudeHalfWidthDegrees));
            return DirectionFromLatLon(lat, lon);
        }

        // 检查环带候选是否在目标窗口内，并满足通用中心条件。
        private static bool IsValidCandidate(PlanetData planet, VeinGroupWork group, Vector3 candidate, float centerLongitude, float longitudeHalfWidthDegrees, List<Vector3> placedCenters, float minDistanceSqr)
        {
            // 环带点可能绕到窗口外，所以先检查纬度和经度范围。
            if (!IsInTargetWindow(candidate, centerLongitude, longitudeHalfWidthDegrees))
            {
                return false;
            }

            return IsValidCenter(planet, group, candidate, placedCenters, minDistanceSqr);
        }

        // 判断方向是否落在目标纬度上限和出生点经度窗口内。
        private static bool IsInTargetWindow(Vector3 candidate, float centerLongitude, float longitudeHalfWidthDegrees)
        {
            Vector3 direction = candidate.normalized;
            // 纬度超过上限会把矿组放到极区，前期寻找和铺设会变差。
            if (Mathf.Abs(LatitudeFromDirection(direction)) > Deg2Rad(TargetLatitudeDegrees))
            {
                return false;
            }

            // 经度差需要处理 -PI/PI 环绕，所以使用 AbsLongitudeDelta。
            return AbsLongitudeDelta(LongitudeFromDirection(direction), centerLongitude) <= Deg2Rad(longitudeHalfWidthDegrees);
        }

        // 检查中心点是否能放置该矿组：地形允许，并且离已放置中心足够远。
        private static bool IsValidCenter(PlanetData planet, VeinGroupWork group, Vector3 candidate, List<Vector3> placedCenters, float minDistanceSqr)
        {
            // 普通矿不能放到水面以下，油井/竹子等允许水面的组例外。
            if (!IsValidTerrainCandidate(planet, group, candidate))
            {
                return false;
            }

            // 与每个已放置中心比较弦长平方，避免矿组之间挤在一起。
            for (int i = 0; i < placedCenters.Count; i++)
            {
                if ((placedCenters[i] - candidate).sqrMagnitude < minDistanceSqr)
                {
                    return false;
                }
            }

            return true;
        }

        // 检查候选方向对应地形是否允许放该矿种。
        // 所有矿组都允许生成，包括海面区域（会自动铺设地基）。
        private static bool IsValidTerrainCandidate(PlanetData planet, VeinGroupWork group, Vector3 candidate)
        {
            // 所有矿组都允许生成，不再检查地形高度。
            // 海面区域的矿脉会在 RegenerateGroupVeins 中抬高到海面以上。
            return true;
        }

        // 判断矿组是否允许在水面或特殊地形上生成。
        private static bool IsWaterAllowedVeinGroup(VeinGroupWork group)
        {
            return group.Type == EVeinType.Oil || group.Type == EVeinType.Bamboo;
        }

        // 计算矿组之间的最小弦长平方；随星球半径缩放，保持不同半径星球上的视觉间距接近。
        private static float GetMinDistanceSqr(PlanetData planet, VeinGroupWork group)
        {
            float scale = 2.1f / planet.radius;
            float spacing = group.Type == EVeinType.Oil ? OilGroupSpacing : NormalGroupSpacing;
            return scale * scale * spacing;
        }

        // 从 anchor 沿随机切线方向走指定球面弦长，得到新的单位方向。
        private static Vector3 DirectionAtChordDistance(Vector3 anchor, float chordDistance, float angle)
        {
            Vector3 normalizedAnchor = anchor.normalized;
            Vector3 tangentA = Vector3.Cross(normalizedAnchor, Vector3.up);
            // anchor 接近 up/down 时和 up 叉乘会退化，改用 right 生成切线基。
            if (tangentA.sqrMagnitude < 1E-08f)
            {
                tangentA = Vector3.Cross(normalizedAnchor, Vector3.right);
            }

            tangentA.Normalize();
            Vector3 tangentB = Vector3.Cross(normalizedAnchor, tangentA).normalized;
            Vector3 tangentDirection = Mathf.Cos(angle) * tangentA + Mathf.Sin(angle) * tangentB;
            // chordDistance 是单位球弦长，换算成圆心角 theta 后在球面上旋转。
            float theta = 2f * Mathf.Asin(Mathf.Clamp(chordDistance * 0.5f, 0f, 0.999999f));
            return (Mathf.Cos(theta) * normalizedAnchor + Mathf.Sin(theta) * tangentDirection).normalized;
        }

        // 计算原版矿组中心方向；当新位置找不到时用它参与避让，防止后续矿组压到保留原位的组。
        private static Vector3 GetOriginalGroupCenter(PlanetData planet, VeinGroupWork group)
        {
            Vector3 center = Vector3.zero;
            VeinData[] veinPool = planet.data.veinPool;
            // 把同组每个矿点方向累加，得到近似中心方向。
            for (int i = 0; i < group.VeinIds.Count; i++)
            {
                Vector3 pos = veinPool[group.VeinIds[i]].pos;
                // 忽略无效位置，避免 zero 参与归一化。
                if (pos.sqrMagnitude > 1E-08f)
                {
                    center += pos.normalized;
                }
            }

            // 全部矿点都没有有效位置时返回 zero。
            if (center.sqrMagnitude < 1E-08f)
            {
                return Vector3.zero;
            }

            return center.normalized;
        }

        // 根据弧度纬度/经度生成单位球面方向。
        private static Vector3 DirectionFromLatLon(float latitude, float longitude)
        {
            float cosLat = Mathf.Cos(latitude);
            return new Vector3(Mathf.Cos(longitude) * cosLat, Mathf.Sin(latitude), Mathf.Sin(longitude) * cosLat).normalized;
        }

        // 从方向向量计算经度，范围为 -PI 到 PI。
        private static float LongitudeFromDirection(Vector3 direction)
        {
            return Mathf.Atan2(direction.z, direction.x);
        }

        // 从方向向量计算纬度；Clamp 防止浮点误差让 Asin 输入越界。
        private static float LatitudeFromDirection(Vector3 direction)
        {
            return Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f));
        }

        // 计算两个经度之间的最小绝对差，处理跨越 -PI/PI 的环绕情况。
        private static float AbsLongitudeDelta(float longitude, float centerLongitude)
        {
            float delta = longitude - centerLongitude;
            // 把差值折回 [-PI, PI]，这样 179 度和 -179 度会被认为只差 2 度。
            while (delta > Mathf.PI)
            {
                delta -= Mathf.PI * 2f;
            }

            while (delta < -Mathf.PI)
            {
                delta += Mathf.PI * 2f;
            }

            return Mathf.Abs(delta);
        }

        // 角度转弧度，避免在多个调用点重复写常量转换。
        private static float Deg2Rad(float degrees)
        {
            return degrees * Mathf.PI / 180f;
        }

        // 用新的中心方向和局部偏移重写一个矿组中所有矿点的位置，并铺设透明地基。
        private static void RegenerateGroupVeins(PlanetData planet, PlanetRawData data, VeinGroupWork group, Vector3 center, DotNet35Random rng)
        {
            List<Vector2> offsets = GenerateLocalOffsets(group, rng);
            // 构造以 center 为法线的局部平面，把二维偏移转换到球面附近的三维方向。
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, center);
            Vector3 right = rotation * Vector3.right;
            Vector3 forward = rotation * Vector3.forward;
            // 游戏矿脉局部坐标与星球半径相关，沿用 2.1 / radius 的缩放保持原版尺度接近。
            float scale = 2.1f / planet.radius;
            VeinData[] veinPool = data.veinPool;

            // 保持原有 veinId 和矿种/数量，只移动位置，避免改变资源总量。
            for (int i = 0; i < group.VeinIds.Count; i++)
            {
                int veinId = group.VeinIds[i];
                VeinData vein = veinPool[veinId];
                Vector3 offset = (offsets[i].x * right + offsets[i].y * forward) * scale;
                Vector3 pos = center + offset;

                // 油井需要 RawSnap 到星球原始地形点，避免油井位置和地形采样不匹配。
                if (vein.type == EVeinType.Oil && planet.aux != null)
                {
                    pos = planet.aux.RawSnap(pos);
                }

                // 用查询高度投回真实地表，并清掉该点附近植物，避免矿点和植被重叠。
                float height = data.QueryHeight(pos);
                data.EraseVegetableAtPoint(pos);

                // 如果查询高度低于星球半径（即在海面以下），将矿脉抬高到海面以上。
                // 海平面高度为 planet.realRadius，矿脉需要生成到海面以上 0.2 米处。
                if (height < planet.realRadius)
                {
                    height = planet.realRadius + 0.2f;
                }

                vein.pos = pos.normalized * height;
                // VeinData 是值类型，修改后必须写回 veinPool。
                veinPool[veinId] = vein;
            }

            // 在矿组中心和每个矿点下面铺设透明地基。
            PlaceTransparentFoundationForGroup(planet, group, center, veinPool);
        }

        // 为矿组中心和每个矿点记录需要铺设透明地基的位置。
        // 矿脉生成发生在后台线程，工厂创建在主线程，需要记录位置后在工厂创建后铺设。
        private static void PlaceTransparentFoundationForGroup(PlanetData planet, VeinGroupWork group, Vector3 center, VeinData[] veinPool)
        {
            // 记录需要铺设地基的位置。
            List<Vector3> positions = new List<Vector3>();

            // 记录矿组中心位置。
            if (center.sqrMagnitude > 1E-08f)
            {
                positions.Add(center);
            }

            // 记录每个矿点位置。
            for (int i = 0; i < group.VeinIds.Count; i++)
            {
                int veinId = group.VeinIds[i];
                VeinData vein = veinPool[veinId];
                if (vein.pos.sqrMagnitude > 1E-08f)
                {
                    positions.Add(vein.pos);
                }
            }

            // 将位置添加到待处理字典中（线程安全）。
            if (positions.Count > 0)
            {
                lock (PendingFoundationLock)
                {
                    List<Vector3> existingPositions;
                    if (!PendingFoundationPositions.TryGetValue(planet.id, out existingPositions))
                    {
                        existingPositions = new List<Vector3>();
                        PendingFoundationPositions[planet.id] = existingPositions;
                    }
                    existingPositions.AddRange(positions);
                }
            }
        }

        // 在指定位置铺设透明地基。
        private static void PlaceTransparentFoundationAtPosition(PlatformSystem platformSystem, Vector3 position, int type, int color, byte state)
        {
            // 将世界坐标转换为地基索引。
            int reformIndex = platformSystem.GetReformIndexForPosition(position);
            if (reformIndex < 0)
            {
                return;
            }

            // 设置地基类型、颜色和状态。
            platformSystem.SetReformType(reformIndex, type);
            platformSystem.SetReformColor(reformIndex, color);
            platformSystem.SetReformState(reformIndex, state);
        }

        // 生成同一矿组内部的二维偏移列表；既要成团，也要避免矿点互相重叠。
        private static List<Vector2> GenerateLocalOffsets(VeinGroupWork group, DotNet35Random rng)
        {
            int count = group.VeinIds.Count;
            List<Vector2> offsets = new List<Vector2>(count);
            // 第一个矿点放在组中心，保证矿组一定围绕中心存在。
            offsets.Add(Vector2.zero);

            int pass = 0;
            // 通过从现有偏移向外“生长”的方式生成自然簇状形态。
            while (pass++ < LocalShapePasses && offsets.Count < count)
            {
                int currentCount = offsets.Count;
                for (int i = 0; i < currentCount && offsets.Count < count; i++)
                {
                    // 已经超出最大半径的点不继续向外扩展。
                    if (offsets[i].sqrMagnitude > InnerVeinMaxRadiusSqr)
                    {
                        continue;
                    }

                    // 随机方向加一点当前偏移方向，让形状更偏向向外扩张，而不是完全随机云团。
                    double angle = rng.NextDouble() * Math.PI * 2.0;
                    Vector2 direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                    direction += offsets[i] * 0.2f;
                    direction.Normalize();
                    Vector2 candidate = offsets[i] + direction;
                    // 超出组内最大半径的候选丢弃。
                    if (candidate.sqrMagnitude > InnerVeinMaxRadiusSqr)
                    {
                        continue;
                    }

                    // 候选需要和已有矿点保持最小间距。
                    if (IsValidLocalOffset(candidate, offsets))
                    {
                        offsets.Add(candidate);
                    }
                }
            }

            // 生长失败时使用随机 fallback 补齐数量，确保不会因为形状约束丢矿。
            while (offsets.Count < count)
            {
                if (!TryAddFallbackOffset(offsets, rng))
                {
                    // 极端情况下放宽间距，保留矿点数量比严格间距更重要。
                    Log?.LogWarning($"Unable to keep local vein spacing for group {group.GroupIndex}; preserving vein count with relaxed fallback");
                    offsets.Add(RandomFallbackOffset(rng));
                }
            }

            return offsets;
        }

        // 随机尝试一个 fallback 偏移，仍尽量满足组内间距。
        private static bool TryAddFallbackOffset(List<Vector2> offsets, DotNet35Random rng)
        {
            // 尝试次数较大是因为大矿组在小半径内可能很拥挤，需要多次采样才能满足间距。
            for (int attempt = 0; attempt < LocalFallbackAttempts; attempt++)
            {
                Vector2 candidate = RandomFallbackOffset(rng);
                if (IsValidLocalOffset(candidate, offsets))
                {
                    offsets.Add(candidate);
                    return true;
                }
            }

            return false;
        }

        // 检查局部偏移是否在最大半径内，并且和已有偏移保持最小距离。
        private static bool IsValidLocalOffset(Vector2 candidate, List<Vector2> offsets)
        {
            // 超过最大半径会让单个矿点离组中心太远。
            if (candidate.sqrMagnitude > InnerVeinMaxRadiusSqr)
            {
                return false;
            }

            // 与每个已有偏移比较平方距离，避免矿点重叠。
            for (int i = 0; i < offsets.Count; i++)
            {
                if ((offsets[i] - candidate).sqrMagnitude < InnerVeinMinDistanceSqr)
                {
                    return false;
                }
            }

            return true;
        }

        // 在组内最大半径圆盘中均匀生成随机偏移。
        private static Vector2 RandomFallbackOffset(DotNet35Random rng)
        {
            // 对随机值开方可得到面积均匀的半径分布，而不是集中在圆心。
            float radius = Mathf.Sqrt((float)rng.NextDouble() * InnerVeinMaxRadiusSqr);
            float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        // 矿组重排工作对象：保存原 groupIndex、矿种和该组包含的 veinId 列表。
        private sealed class VeinGroupWork
        {
            public readonly short GroupIndex;
            public readonly EVeinType Type;
            public readonly List<int> VeinIds = new List<int>();

            // groupIndex 用于日志/回溯，type 用于水面规则和间距规则。
            public VeinGroupWork(short groupIndex, EVeinType type)
            {
                GroupIndex = groupIndex;
                Type = type;
            }
        }

        // 基类 PlanetAlgorithm 的矿脉生成后处理；部分算法直接走基类实现。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlanetAlgorithm), "GenerateVeins")]
        private static void PlanetAlgorithmGenerateVeinsPostfix(PlanetAlgorithm __instance)
        {
            ApplyPlacement(__instance);
        }

        // PlanetAlgorithm0 的矿脉生成后处理；不同星球算法可能各自声明 GenerateVeins，需要分别 Patch。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlanetAlgorithm0), "GenerateVeins")]
        private static void PlanetAlgorithm0GenerateVeinsPostfix(PlanetAlgorithm __instance)
        {
            ApplyPlacement(__instance);
        }

        // PlanetAlgorithm7 的矿脉生成后处理，统一转入 ApplyPlacement 保持行为一致。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlanetAlgorithm7), "GenerateVeins")]
        private static void PlanetAlgorithm7GenerateVeinsPostfix(PlanetAlgorithm __instance)
        {
            ApplyPlacement(__instance);
        }

        // PlanetAlgorithm11 的矿脉生成后处理。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlanetAlgorithm11), "GenerateVeins")]
        private static void PlanetAlgorithm11GenerateVeinsPostfix(PlanetAlgorithm __instance)
        {
            ApplyPlacement(__instance);
        }

        // PlanetAlgorithm12 的矿脉生成后处理。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlanetAlgorithm12), "GenerateVeins")]
        private static void PlanetAlgorithm12GenerateVeinsPostfix(PlanetAlgorithm __instance)
        {
            ApplyPlacement(__instance);
        }

        // PlanetAlgorithm13 的矿脉生成后处理。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlanetAlgorithm13), "GenerateVeins")]
        private static void PlanetAlgorithm13GenerateVeinsPostfix(PlanetAlgorithm __instance)
        {
            ApplyPlacement(__instance);
        }

        // PlanetFactory.Init 的后处理；在工厂创建后铺设待处理的透明地基。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlanetFactory), "Init")]
        private static void PlanetFactoryInitPostfix(PlanetFactory __instance)
        {
            if (__instance == null || __instance.planet == null)
            {
                return;
            }

            int planetId = __instance.planet.id;
            List<Vector3> positions = null;

            // 从待处理字典中获取该星球的地基位置（线程安全）。
            lock (PendingFoundationLock)
            {
                if (PendingFoundationPositions.TryGetValue(planetId, out positions))
                {
                    PendingFoundationPositions.Remove(planetId);
                }
            }

            // 如果没有待处理的位置，直接返回。
            if (positions == null || positions.Count == 0)
            {
                return;
            }

            // 铺设透明地基。
            PlaceTransparentFoundationAtPositions(__instance, positions);
        }

        // 在指定位置列表铺设透明地基。
        private static void PlaceTransparentFoundationAtPositions(PlanetFactory factory, List<Vector3> positions)
        {
            if (factory.platformSystem == null)
            {
                return;
            }

            PlatformSystem platformSystem = factory.platformSystem;

            // 确保地基数据已初始化。
            platformSystem.EnsureReformData();

            // 透明地基类型为 7（kUndecalReformType）。
            const int transparentFoundationType = 7;
            // 默认地基颜色为 0。
            const int defaultFoundationColor = 0;
            // 地基状态为 1 表示已铺设。
            const byte foundationState = 1;

            // 为每个位置铺设透明地基。
            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 position = positions[i];
                if (position.sqrMagnitude > 1E-08f)
                {
                    PlaceTransparentFoundationAtPosition(platformSystem, position, transparentFoundationType, defaultFoundationColor, foundationState);
                }
            }

            Log?.LogInfo($"Placed {positions.Count} transparent foundations on planet {factory.planet.displayName ?? factory.planet.name}");
        }
    }
}
