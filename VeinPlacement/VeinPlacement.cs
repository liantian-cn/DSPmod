using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace VeinPlacement
{
    [BepInPlugin("me.liantian.plugin.VeinPlacement", "VeinPlacement", "0.0.1")]
    public class VeinPlacement : BaseUnityPlugin
    {
        private const string PluginGuid = "me.liantian.plugin.VeinPlacement";
        private const int ModSeedSalt = 0x56_50_30_31;
        private const float TargetLatitudeDegrees = 40f;
        private const float TargetLongitudeDegrees = 90f;
        private const float NormalGroupSpacing = 196f;
        private const float OilGroupSpacing = 100f;
        private const float InnerVeinMinDistanceSqr = 0.6375f;
        private const float InnerVeinMaxRadiusSqr = 15f;
        private const int GroupPlacementAttempts = 12;
        private const int GroupPlacementRelaxPasses = 32;
        private const int LocalShapePasses = 20;
        private const int LocalFallbackAttempts = 8192;

        internal static ManualLogSource Log;

        private static readonly AccessTools.FieldRef<PlanetAlgorithm, PlanetData> PlanetRef =
            AccessTools.FieldRefAccess<PlanetAlgorithm, PlanetData>("planet");

        private Harmony harmony;

        public void Awake()
        {
            Log = Logger;
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(VeinPlacement).Assembly);
            Log.LogInfo("VeinPlacement 0.0.1 initialized");
        }

        public void OnDestroy()
        {
            harmony?.UnpatchSelf();
            harmony = null;
        }

        private static void ApplyPlacement(PlanetAlgorithm algorithm)
        {
            PlanetData planet = GetPlanet(algorithm);
            if (planet == null || planet.type == EPlanetType.Gas || planet.data == null || planet.data.veinPool == null || planet.data.veinCursor <= 1)
            {
                return;
            }

            List<VeinGroupWork> groups = CollectGroups(planet);
            if (groups.Count == 0)
            {
                return;
            }

            DotNet35Random rng = CreateRandom(planet);
            float initialLongitude = (float)(rng.NextDouble() * Math.PI * 2.0);
            List<Vector3> centers = PlaceGroupCenters(planet, groups, initialLongitude, rng);
            if (centers.Count != groups.Count)
            {
                Log?.LogWarning($"Failed to place all vein groups on planet {planet.displayName ?? planet.name}");
                return;
            }

            PlanetRawData data = planet.data;
            for (int i = 0; i < groups.Count; i++)
            {
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
                }

                group.VeinIds.Add(i);
            }

            List<VeinGroupWork> groups = new List<VeinGroupWork>(byGroup.Values);
            groups.Sort((a, b) =>
            {
                int typeCompare = a.Type.CompareTo(b.Type);
                if (typeCompare != 0)
                {
                    return typeCompare;
                }

                return a.GroupIndex.CompareTo(b.GroupIndex);
            });
            return groups;
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

        private static List<Vector3> PlaceGroupCenters(PlanetData planet, List<VeinGroupWork> groups, float initialLongitude, DotNet35Random rng)
        {
            List<Vector3> placed = new List<Vector3>(groups.Count);
            Vector3 anchor = RandomDirectionInTargetWindow(initialLongitude, rng).normalized;

            for (int i = 0; i < groups.Count; i++)
            {
                Vector3 center;
                if (!TryPlaceOneGroupCenter(planet, groups[i], initialLongitude, rng, placed, anchor, out center))
                {
                    center = FindAnyCenter(planet, groups[i], initialLongitude, rng, placed);
                }

                placed.Add(center);
                anchor = center;
            }

            return new List<Vector3>(placed);
        }

        private static bool TryPlaceOneGroupCenter(PlanetData planet, VeinGroupWork group, float initialLongitude, DotNet35Random rng, List<Vector3> placed, Vector3 anchor, out Vector3 center)
        {
            float num = 2.1f / planet.radius;
            float spacing = group.Type == EVeinType.Oil ? OilGroupSpacing : NormalGroupSpacing;
            float minDistanceSqr = num * num * spacing;

            for (int attempt = 0; attempt < GroupPlacementAttempts; attempt++)
            {
                Vector3 candidate = RandomDirectionBetween(anchor, rng, minDistanceSqr, 3f, initialLongitude);

                bool tooClose = false;
                for (int i = 0; i < placed.Count; i++)
                {
                    if ((placed[i] - candidate).sqrMagnitude < minDistanceSqr)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    center = candidate.normalized;
                    return true;
                }
            }

            for (int attempt = 0; attempt < GroupPlacementAttempts; attempt++)
            {
                Vector3 candidate = RandomDirectionInTargetWindow(initialLongitude, rng);
                if (IsValidCenter(candidate, placed, minDistanceSqr))
                {
                    center = candidate.normalized;
                    return true;
                }
            }

            float spacingScale = 1f;
            for (int relax = 0; relax < GroupPlacementRelaxPasses; relax++)
            {
                float relaxedMinDistanceSqr = minDistanceSqr * spacingScale;
                for (int attempt = 0; attempt < GroupPlacementAttempts; attempt++)
                {
                    Vector3 candidate = RandomDirectionBetween(anchor, rng, relaxedMinDistanceSqr, 3f, initialLongitude);
                    if (IsValidCenter(candidate, placed, relaxedMinDistanceSqr))
                    {
                        center = candidate.normalized;
                        return true;
                    }
                }

                spacingScale *= 0.85f;
            }

            center = Vector3.up;
            return false;
        }

        private static Vector3 FindAnyCenter(PlanetData planet, VeinGroupWork group, float centerLongitude, DotNet35Random rng, List<Vector3> placed)
        {
            float num = 2.1f / planet.radius;
            float spacing = group.Type == EVeinType.Oil ? OilGroupSpacing : NormalGroupSpacing;
            float minDistanceSqr = num * num * spacing;

            for (int attempt = 0; attempt < GroupPlacementAttempts; attempt++)
            {
                Vector3 candidate = RandomDirectionInTargetWindow(centerLongitude, rng);
                if (IsValidCenter(candidate, placed, minDistanceSqr))
                {
                    return candidate.normalized;
                }
            }

            float spacingScale = 1f;
            for (int relax = 0; relax < GroupPlacementRelaxPasses; relax++)
            {
                float relaxedMinDistanceSqr = minDistanceSqr * spacingScale;
                for (int attempt = 0; attempt < GroupPlacementAttempts; attempt++)
                {
                    Vector3 candidate = RandomDirectionInTargetWindow(centerLongitude, rng);
                    if (IsValidCenter(candidate, placed, relaxedMinDistanceSqr))
                    {
                        return candidate.normalized;
                    }
                }

                spacingScale *= 0.85f;
            }

            return RandomDirectionInTargetWindow(centerLongitude, rng).normalized;
        }

        private static Vector3 RandomDirectionInTargetWindow(float centerLongitude, DotNet35Random rng)
        {
            float lat = Deg2Rad((float)((rng.NextDouble() * 2.0 - 1.0) * TargetLatitudeDegrees));
            float lon = centerLongitude + Deg2Rad((float)((rng.NextDouble() * 2.0 - 1.0) * TargetLongitudeDegrees));
            return DirectionFromLatLon(lat, lon);
        }

        private static Vector3 RandomDirectionBetween(Vector3 anchor, DotNet35Random rng, float minDistanceSqr, float maxDistanceScale, float centerLongitude)
        {
            Vector3 candidate;
            for (int attempt = 0; attempt < GroupPlacementAttempts; attempt++)
            {
                candidate = RandomDirectionInTargetWindow(centerLongitude, rng);
                float distanceSqr = (candidate - anchor).sqrMagnitude;
                if (distanceSqr > minDistanceSqr && distanceSqr <= minDistanceSqr * maxDistanceScale)
                {
                    return candidate.normalized;
                }
            }

            return RandomDirectionInTargetWindow(centerLongitude, rng).normalized;
        }

        private static bool IsValidCenter(Vector3 candidate, List<Vector3> placed, float minDistanceSqr)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                if ((placed[i] - candidate).sqrMagnitude < minDistanceSqr)
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector3 DirectionFromLatLon(float latitude, float longitude)
        {
            float cosLat = Mathf.Cos(latitude);
            return new Vector3(Mathf.Cos(longitude) * cosLat, Mathf.Sin(latitude), Mathf.Sin(longitude) * cosLat).normalized;
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

        [HarmonyPatch(typeof(PlanetAlgorithm), "GenerateVeins")]
        private static class PlanetAlgorithmGenerateVeinsPatch
        {
            private static void Postfix(PlanetAlgorithm __instance)
            {
                ApplyPlacement(__instance);
            }
        }

        [HarmonyPatch(typeof(PlanetAlgorithm0), "GenerateVeins")]
        private static class PlanetAlgorithm0GenerateVeinsPatch
        {
            private static void Postfix(PlanetAlgorithm __instance)
            {
                ApplyPlacement(__instance);
            }
        }

        [HarmonyPatch(typeof(PlanetAlgorithm7), "GenerateVeins")]
        private static class PlanetAlgorithm7GenerateVeinsPatch
        {
            private static void Postfix(PlanetAlgorithm __instance)
            {
                ApplyPlacement(__instance);
            }
        }

        [HarmonyPatch(typeof(PlanetAlgorithm11), "GenerateVeins")]
        private static class PlanetAlgorithm11GenerateVeinsPatch
        {
            private static void Postfix(PlanetAlgorithm __instance)
            {
                ApplyPlacement(__instance);
            }
        }

        [HarmonyPatch(typeof(PlanetAlgorithm12), "GenerateVeins")]
        private static class PlanetAlgorithm12GenerateVeinsPatch
        {
            private static void Postfix(PlanetAlgorithm __instance)
            {
                ApplyPlacement(__instance);
            }
        }

        [HarmonyPatch(typeof(PlanetAlgorithm13), "GenerateVeins")]
        private static class PlanetAlgorithm13GenerateVeinsPatch
        {
            private static void Postfix(PlanetAlgorithm __instance)
            {
                ApplyPlacement(__instance);
            }
        }
    }
}
