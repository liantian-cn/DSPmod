using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HardFog
{
    [HarmonyPatch]
    internal static class VeinPlacementControl
    {
        private const string PatchGuid = "me.liantian.plugin.HardFog.VeinPlacement";
        private const int ModSeedSalt = 0x56_50_30_31;
        private const float TargetLatitudeDegrees = 40f;
        private const float TargetLongitudeDegrees = 80f;
        private const float NormalGroupSpacing = 196f;
        private const float OilGroupSpacing = 100f;
        private const float InnerVeinMinDistanceSqr = 0.5f;
        private const float InnerVeinMaxRadiusSqr = 13f;
        private const int GroupPlacementAttempts = 24;
        private const int LongitudeExpansionPasses = 100;
        private const float LongitudeExpansionStepDegrees = 30f;
        private const int LocalShapePasses = 20;
        private const int LocalFallbackAttempts = 8192;

        internal static ConfigEntry<bool> EnabledConfig { get; private set; }

        private static ManualLogSource Log;
        private static Harmony harmony;
        private static EventHandler settingChangedHandler;

        private static readonly AccessTools.FieldRef<PlanetAlgorithm, PlanetData> PlanetRef =
            AccessTools.FieldRefAccess<PlanetAlgorithm, PlanetData>("planet");

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

                harmony = Harmony.CreateAndPatchAll(typeof(VeinPlacementControl), PatchGuid);
                Log?.LogInfo("VeinPlacement enabled");
                return;
            }

            if (harmony == null)
            {
                return;
            }

            harmony.UnpatchSelf();
            harmony = null;
            Log?.LogInfo("VeinPlacement disabled");
        }

        private static void ApplyPlacement(PlanetAlgorithm algorithm)
        {
            PlanetData planet = GetPlanet(algorithm);
            if (planet == null || planet.type == EPlanetType.Gas || planet.data == null || planet.data.veinPool == null || planet.data.veinCursor <= 1)
            {
                return;
            }

            List<VeinGroupWork> groups = InterleaveGroupsByType(CollectGroups(planet));
            if (groups.Count == 0)
            {
                return;
            }

            DotNet35Random rng = CreateRandom(planet);
            Vector3 birthDirection = EnsureBirthPointDirection(planet);
            if (birthDirection.sqrMagnitude < 0.5f)
            {
                Log?.LogWarning($"Unable to read birth point on planet {planet.displayName ?? planet.name}");
                return;
            }

            List<Vector3> centers = PlaceGroupCenters(planet, groups, birthDirection, rng);
            PlanetRawData data = planet.data;
            for (int i = 0; i < groups.Count; i++)
            {
                if (centers[i].sqrMagnitude < 0.5f)
                {
                    continue;
                }

                RegenerateGroupVeins(planet, data, groups[i], centers[i], rng);
            }

            planet.SummarizeVeinGroups();
        }

        private static PlanetData GetPlanet(PlanetAlgorithm algorithm)
        {
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
                Log?.LogWarning($"Unable to read PlanetAlgorithm.planet: {ex.Message}");
                return null;
            }
        }

        private static DotNet35Random CreateRandom(PlanetData planet)
        {
            unchecked
            {
                int seed = planet.seed;
                seed = (seed * 397) ^ planet.id;
                seed = (seed * 397) ^ planet.algoId;
                seed ^= ModSeedSalt;
                return new DotNet35Random(seed);
            }
        }

        private static List<VeinGroupWork> CollectGroups(PlanetData planet)
        {
            Dictionary<short, VeinGroupWork> byGroup = new Dictionary<short, VeinGroupWork>();
            List<VeinGroupWork> groups = new List<VeinGroupWork>();
            VeinData[] veinPool = planet.data.veinPool;
            int veinCursor = planet.data.veinCursor;

            for (int i = 1; i < veinCursor; i++)
            {
                if (veinPool[i].id != i || veinPool[i].type == EVeinType.None)
                {
                    continue;
                }

                short groupIndex = veinPool[i].groupIndex;
                if (groupIndex <= 0)
                {
                    continue;
                }

                VeinGroupWork group;
                if (!byGroup.TryGetValue(groupIndex, out group))
                {
                    group = new VeinGroupWork(groupIndex, veinPool[i].type);
                    byGroup[groupIndex] = group;
                    groups.Add(group);
                }

                group.VeinIds.Add(i);
            }

            return groups;
        }

        private static List<VeinGroupWork> InterleaveGroupsByType(List<VeinGroupWork> groups)
        {
            Dictionary<EVeinType, List<VeinGroupWork>> byType = new Dictionary<EVeinType, List<VeinGroupWork>>();
            List<List<VeinGroupWork>> typeGroups = new List<List<VeinGroupWork>>();
            int largestTypeGroupCount = 0;

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
                if (typeGroup.Count > largestTypeGroupCount)
                {
                    largestTypeGroupCount = typeGroup.Count;
                }
            }

            List<VeinGroupWork> interleaved = new List<VeinGroupWork>(groups.Count);
            for (int round = 0; round < largestTypeGroupCount; round++)
            {
                for (int i = 0; i < typeGroups.Count; i++)
                {
                    if (round < typeGroups[i].Count)
                    {
                        interleaved.Add(typeGroups[i][round]);
                    }
                }
            }

            return interleaved;
        }

        private static void Shuffle<T>(IList<T> items, DotNet35Random rng)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T temp = items[i];
                items[i] = items[j];
                items[j] = temp;
            }
        }

        private static Vector3 EnsureBirthPointDirection(PlanetData planet)
        {
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

            if (planet.birthPoint.sqrMagnitude < 1E-08f)
            {
                return Vector3.zero;
            }

            return planet.birthPoint.normalized;
        }

        private static List<Vector3> PlaceGroupCenters(PlanetData planet, List<VeinGroupWork> groups, Vector3 birthDirection, DotNet35Random rng)
        {
            List<Vector3> centers = new List<Vector3>(groups.Count);
            List<Vector3> placedCenters = new List<Vector3>(groups.Count);
            if (groups.Count == 0)
            {
                return centers;
            }

            float centerLongitude = LongitudeFromDirection(birthDirection);
            centers.Add(birthDirection);
            placedCenters.Add(birthDirection);
            Vector3 lastSuccessfulCenter = birthDirection;

            for (int i = 1; i < groups.Count; i++)
            {
                Vector3 center;
                if (TryPlaceGroupCenter(planet, groups[i], lastSuccessfulCenter, centerLongitude, rng, placedCenters, out center))
                {
                    centers.Add(center);
                    placedCenters.Add(center);
                    lastSuccessfulCenter = center;
                    continue;
                }

                centers.Add(Vector3.zero);
                Vector3 originalCenter = GetOriginalGroupCenter(planet, groups[i]);
                if (originalCenter.sqrMagnitude > 0.5f)
                {
                    placedCenters.Add(originalCenter);
                }
            }

            return centers;
        }

        private static bool TryPlaceGroupCenter(PlanetData planet, VeinGroupWork group, Vector3 anchor, float centerLongitude, DotNet35Random rng, List<Vector3> placedCenters, out Vector3 center)
        {
            float minDistanceSqr = GetMinDistanceSqr(planet, group);
            float minDistance = Mathf.Sqrt(minDistanceSqr);

            for (int band = 1; band <= 3; band++)
            {
                if (TryPlaceOnDistanceBand(planet, group, anchor, minDistance * band, minDistance * (band + 1), centerLongitude, TargetLongitudeDegrees, rng, placedCenters, minDistanceSqr, out center))
                {
                    return true;
                }
            }

            if (TryPlaceRandomInWindow(planet, group, centerLongitude, TargetLongitudeDegrees, rng, placedCenters, minDistanceSqr, out center))
            {
                return true;
            }

            for (int pass = 1; pass <= LongitudeExpansionPasses; pass++)
            {
                float longitudeHalfWidth = TargetLongitudeDegrees + LongitudeExpansionStepDegrees * pass;
                if (TryPlaceRandomInWindow(planet, group, centerLongitude, longitudeHalfWidth, rng, placedCenters, minDistanceSqr, out center))
                {
                    return true;
                }
            }

            center = Vector3.zero;
            return false;
        }

        private static bool TryPlaceOnDistanceBand(PlanetData planet, VeinGroupWork group, Vector3 anchor, float minDistance, float maxDistance, float centerLongitude, float longitudeHalfWidthDegrees, DotNet35Random rng, List<Vector3> placedCenters, float minDistanceSqr, out Vector3 center)
        {
            for (int attempt = 0; attempt < GroupPlacementAttempts; attempt++)
            {
                float distance = minDistance + (float)rng.NextDouble() * (maxDistance - minDistance);
                float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
                Vector3 candidate = DirectionAtChordDistance(anchor, distance, angle);
                if (IsValidCandidate(planet, group, candidate, centerLongitude, longitudeHalfWidthDegrees, placedCenters, minDistanceSqr))
                {
                    center = candidate;
                    return true;
                }
            }

            center = Vector3.zero;
            return false;
        }

        private static bool TryPlaceRandomInWindow(PlanetData planet, VeinGroupWork group, float centerLongitude, float longitudeHalfWidthDegrees, DotNet35Random rng, List<Vector3> placedCenters, float minDistanceSqr, out Vector3 center)
        {
            for (int attempt = 0; attempt < GroupPlacementAttempts; attempt++)
            {
                Vector3 candidate = RandomDirectionInTargetWindow(centerLongitude, longitudeHalfWidthDegrees, rng);
                if (IsValidCenter(planet, group, candidate, placedCenters, minDistanceSqr))
                {
                    center = candidate.normalized;
                    return true;
                }
            }

            center = Vector3.zero;
            return false;
        }

        private static Vector3 RandomDirectionInTargetWindow(float centerLongitude, float longitudeHalfWidthDegrees, DotNet35Random rng)
        {
            float lat = Deg2Rad((float)((rng.NextDouble() * 2.0 - 1.0) * TargetLatitudeDegrees));
            float lon = centerLongitude + Deg2Rad((float)((rng.NextDouble() * 2.0 - 1.0) * longitudeHalfWidthDegrees));
            return DirectionFromLatLon(lat, lon);
        }

        private static bool IsValidCandidate(PlanetData planet, VeinGroupWork group, Vector3 candidate, float centerLongitude, float longitudeHalfWidthDegrees, List<Vector3> placedCenters, float minDistanceSqr)
        {
            if (!IsInTargetWindow(candidate, centerLongitude, longitudeHalfWidthDegrees))
            {
                return false;
            }

            return IsValidCenter(planet, group, candidate, placedCenters, minDistanceSqr);
        }

        private static bool IsInTargetWindow(Vector3 candidate, float centerLongitude, float longitudeHalfWidthDegrees)
        {
            Vector3 direction = candidate.normalized;
            if (Mathf.Abs(LatitudeFromDirection(direction)) > Deg2Rad(TargetLatitudeDegrees))
            {
                return false;
            }

            return AbsLongitudeDelta(LongitudeFromDirection(direction), centerLongitude) <= Deg2Rad(longitudeHalfWidthDegrees);
        }

        private static bool IsValidCenter(PlanetData planet, VeinGroupWork group, Vector3 candidate, List<Vector3> placedCenters, float minDistanceSqr)
        {
            if (!IsValidTerrainCandidate(planet, group, candidate))
            {
                return false;
            }

            for (int i = 0; i < placedCenters.Count; i++)
            {
                if ((placedCenters[i] - candidate).sqrMagnitude < minDistanceSqr)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidTerrainCandidate(PlanetData planet, VeinGroupWork group, Vector3 candidate)
        {
            if (IsWaterAllowedVeinGroup(group))
            {
                return true;
            }

            return planet.data.QueryHeight(candidate) >= planet.radius;
        }

        private static bool IsWaterAllowedVeinGroup(VeinGroupWork group)
        {
            return group.Type == EVeinType.Oil || group.Type == EVeinType.Bamboo;
        }

        private static float GetMinDistanceSqr(PlanetData planet, VeinGroupWork group)
        {
            float scale = 2.1f / planet.radius;
            float spacing = group.Type == EVeinType.Oil ? OilGroupSpacing : NormalGroupSpacing;
            return scale * scale * spacing;
        }

        private static Vector3 DirectionAtChordDistance(Vector3 anchor, float chordDistance, float angle)
        {
            Vector3 normalizedAnchor = anchor.normalized;
            Vector3 tangentA = Vector3.Cross(normalizedAnchor, Vector3.up);
            if (tangentA.sqrMagnitude < 1E-08f)
            {
                tangentA = Vector3.Cross(normalizedAnchor, Vector3.right);
            }

            tangentA.Normalize();
            Vector3 tangentB = Vector3.Cross(normalizedAnchor, tangentA).normalized;
            Vector3 tangentDirection = Mathf.Cos(angle) * tangentA + Mathf.Sin(angle) * tangentB;
            float theta = 2f * Mathf.Asin(Mathf.Clamp(chordDistance * 0.5f, 0f, 0.999999f));
            return (Mathf.Cos(theta) * normalizedAnchor + Mathf.Sin(theta) * tangentDirection).normalized;
        }

        private static Vector3 GetOriginalGroupCenter(PlanetData planet, VeinGroupWork group)
        {
            Vector3 center = Vector3.zero;
            VeinData[] veinPool = planet.data.veinPool;
            for (int i = 0; i < group.VeinIds.Count; i++)
            {
                Vector3 pos = veinPool[group.VeinIds[i]].pos;
                if (pos.sqrMagnitude > 1E-08f)
                {
                    center += pos.normalized;
                }
            }

            if (center.sqrMagnitude < 1E-08f)
            {
                return Vector3.zero;
            }

            return center.normalized;
        }

        private static Vector3 DirectionFromLatLon(float latitude, float longitude)
        {
            float cosLat = Mathf.Cos(latitude);
            return new Vector3(Mathf.Cos(longitude) * cosLat, Mathf.Sin(latitude), Mathf.Sin(longitude) * cosLat).normalized;
        }

        private static float LongitudeFromDirection(Vector3 direction)
        {
            return Mathf.Atan2(direction.z, direction.x);
        }

        private static float LatitudeFromDirection(Vector3 direction)
        {
            return Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f));
        }

        private static float AbsLongitudeDelta(float longitude, float centerLongitude)
        {
            float delta = longitude - centerLongitude;
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

        private static float Deg2Rad(float degrees)
        {
            return degrees * Mathf.PI / 180f;
        }

        private static void RegenerateGroupVeins(PlanetData planet, PlanetRawData data, VeinGroupWork group, Vector3 center, DotNet35Random rng)
        {
            List<Vector2> offsets = GenerateLocalOffsets(group, rng);
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, center);
            Vector3 right = rotation * Vector3.right;
            Vector3 forward = rotation * Vector3.forward;
            float scale = 2.1f / planet.radius;
            VeinData[] veinPool = data.veinPool;

            for (int i = 0; i < group.VeinIds.Count; i++)
            {
                int veinId = group.VeinIds[i];
                VeinData vein = veinPool[veinId];
                Vector3 offset = (offsets[i].x * right + offsets[i].y * forward) * scale;
                Vector3 pos = center + offset;

                if (vein.type == EVeinType.Oil && planet.aux != null)
                {
                    pos = planet.aux.RawSnap(pos);
                }

                float height = data.QueryHeight(pos);
                data.EraseVegetableAtPoint(pos);
                vein.pos = pos.normalized * height;
                veinPool[veinId] = vein;
            }
        }

        private static List<Vector2> GenerateLocalOffsets(VeinGroupWork group, DotNet35Random rng)
        {
            int count = group.VeinIds.Count;
            List<Vector2> offsets = new List<Vector2>(count);
            offsets.Add(Vector2.zero);

            int pass = 0;
            while (pass++ < LocalShapePasses && offsets.Count < count)
            {
                int currentCount = offsets.Count;
                for (int i = 0; i < currentCount && offsets.Count < count; i++)
                {
                    if (offsets[i].sqrMagnitude > InnerVeinMaxRadiusSqr)
                    {
                        continue;
                    }

                    double angle = rng.NextDouble() * Math.PI * 2.0;
                    Vector2 direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                    direction += offsets[i] * 0.2f;
                    direction.Normalize();
                    Vector2 candidate = offsets[i] + direction;
                    if (candidate.sqrMagnitude > InnerVeinMaxRadiusSqr)
                    {
                        continue;
                    }

                    if (IsValidLocalOffset(candidate, offsets))
                    {
                        offsets.Add(candidate);
                    }
                }
            }

            while (offsets.Count < count)
            {
                if (!TryAddFallbackOffset(offsets, rng))
                {
                    Log?.LogWarning($"Unable to keep local vein spacing for group {group.GroupIndex}; preserving vein count with relaxed fallback");
                    offsets.Add(RandomFallbackOffset(rng));
                }
            }

            return offsets;
        }

        private static bool TryAddFallbackOffset(List<Vector2> offsets, DotNet35Random rng)
        {
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

        private static bool IsValidLocalOffset(Vector2 candidate, List<Vector2> offsets)
        {
            if (candidate.sqrMagnitude > InnerVeinMaxRadiusSqr)
            {
                return false;
            }

            for (int i = 0; i < offsets.Count; i++)
            {
                if ((offsets[i] - candidate).sqrMagnitude < InnerVeinMinDistanceSqr)
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector2 RandomFallbackOffset(DotNet35Random rng)
        {
            float radius = Mathf.Sqrt((float)rng.NextDouble() * InnerVeinMaxRadiusSqr);
            float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        private sealed class VeinGroupWork
        {
            public readonly short GroupIndex;
            public readonly EVeinType Type;
            public readonly List<int> VeinIds = new List<int>();

            public VeinGroupWork(short groupIndex, EVeinType type)
            {
                GroupIndex = groupIndex;
                Type = type;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlanetAlgorithm), "GenerateVeins")]
        private static void PlanetAlgorithmGenerateVeinsPostfix(PlanetAlgorithm __instance)
        {
            ApplyPlacement(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlanetAlgorithm0), "GenerateVeins")]
        private static void PlanetAlgorithm0GenerateVeinsPostfix(PlanetAlgorithm __instance)
        {
            ApplyPlacement(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlanetAlgorithm7), "GenerateVeins")]
        private static void PlanetAlgorithm7GenerateVeinsPostfix(PlanetAlgorithm __instance)
        {
            ApplyPlacement(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlanetAlgorithm11), "GenerateVeins")]
        private static void PlanetAlgorithm11GenerateVeinsPostfix(PlanetAlgorithm __instance)
        {
            ApplyPlacement(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlanetAlgorithm12), "GenerateVeins")]
        private static void PlanetAlgorithm12GenerateVeinsPostfix(PlanetAlgorithm __instance)
        {
            ApplyPlacement(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlanetAlgorithm13), "GenerateVeins")]
        private static void PlanetAlgorithm13GenerateVeinsPostfix(PlanetAlgorithm __instance)
        {
            ApplyPlacement(__instance);
        }
    }
}
