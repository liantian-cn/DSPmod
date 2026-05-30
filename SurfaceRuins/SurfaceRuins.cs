using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UXAssist.Common;
using UXAssist.UI;

namespace SurfaceRuins
{
    [BepInPlugin(PluginGuid, "Surface Ruins", PluginVersion)]
    [BepInDependency(UXAssist.PluginInfo.PLUGIN_GUID)]
    public class SurfaceRuins : BaseUnityPlugin
    {
        private const string PluginGuid = "me.liantian.plugin.SurfaceRuins";
        private const string PluginVersion = "0.0.1";
        private const short BasePitRuinModelIndex = 406;
        private const int Level30BasePitLifeTime = -31;
        private const float DuplicateRuinRadius = 50f;
        private const float SurfaceOffset = 0.2f;
        private const float LowLatitudeMax = 28.5f;
        private const float MidLatitudeMax = 46.5f;

        internal static ManualLogSource Log;

        private static readonly Vector3[] LowLatitudeRuinPositions =
        {
            new Vector3(7.252994f, 21.688333f, 198.889543f),
            new Vector3(-18.871118f, -48.381667f, 193.347188f),
            new Vector3(49.789273f, -21.688333f, 192.693239f),
            new Vector3(-61.758180f, -5.005000f, 190.370474f),
            new Vector3(-33.887310f, 65.065000f, 186.274625f),
            new Vector3(73.246545f, 48.381667f, 179.928314f),
            new Vector3(21.143705f, -91.758333f, 176.673122f),
            new Vector3(30.137751f, 91.758333f, 175.362950f),
            new Vector3(-99.348881f, 38.371667f, 169.521252f),
            new Vector3(85.620971f, -65.065000f, 168.865731f),
            new Vector3(-80.298004f, -75.075000f, 167.320097f),
            new Vector3(113.502074f, 5.005000f, 164.840132f),
            new Vector3(-121.797735f, -31.698333f, 155.693826f),
            new Vector3(124.695415f, 75.075000f, 137.458495f),
            new Vector3(-124.101170f, 81.748333f, 134.149728f),
            new Vector3(143.582535f, -38.371667f, 134.133183f),
            new Vector3(-154.071396f, 11.678333f, 127.301460f),
            new Vector3(161.153652f, 31.698333f, 114.476006f),
            new Vector3(-162.570266f, -58.391667f, 101.199614f),
            new Vector3(157.215083f, -81.748333f, 93.170100f),
            new Vector3(-171.247828f, 55.055000f, 87.881558f),
            new Vector3(183.986069f, -11.678333f, 78.056281f),
            new Vector3(-188.181705f, -15.015000f, 66.650098f),
            new Vector3(184.685192f, 58.391667f, 50.614554f),
            new Vector3(-177.364112f, -85.085000f, 37.182720f),
            new Vector3(189.202646f, -55.055000f, 35.374367f),
            new Vector3(-196.659478f, 28.361667f, 24.509292f),
            new Vector3(199.375695f, 15.015000f, 10.194213f),
            new Vector3(-195.797869f, -41.708333f, -1.910381f),
            new Vector3(180.600965f, 85.085000f, -14.962421f),
            new Vector3(-186.107880f, 71.738333f, -17.248436f),
            new Vector3(195.478871f, -28.361667f, -32.613905f),
            new Vector3(-195.095011f, 1.668333f, -44.890906f),
            new Vector3(187.115847f, 41.708333f, -57.689815f),
            new Vector3(-176.097246f, -68.401667f, -66.264710f),
            new Vector3(173.452790f, -71.738333f, -69.626009f),
            new Vector3(-176.273098f, 45.045000f, -83.539111f),
            new Vector3(174.180361f, -1.668333f, -98.683629f),
            new Vector3(-166.546816f, -25.025000f, -108.240230f),
            new Vector3(-142.008003f, 88.421667f, -109.978979f),
            new Vector3(149.874415f, 68.401667f, -113.749337f),
            new Vector3(-133.596525f, -95.095000f, -114.843151f),
            new Vector3(145.114775f, -45.045000f, -130.356013f),
            new Vector3(-139.931481f, 18.351667f, -141.994496f),
            new Vector3(104.730694f, -88.421667f, -145.921659f),
            new Vector3(95.281091f, 95.095000f, -148.183989f),
            new Vector3(128.745762f, 25.025000f, -151.255804f),
            new Vector3(-116.171664f, -51.718333f, -154.626642f),
            new Vector3(-101.432569f, 61.728333f, -161.186497f),
            new Vector3(93.606819f, -18.351667f, -176.014260f),
            new Vector3(-53.412284f, -78.411667f, -176.291742f),
            new Vector3(67.230615f, 51.718333f, -181.343041f),
            new Vector3(-83.008664f, -8.341667f, -181.989061f),
            new Vector3(51.232601f, -61.728333f, -183.425389f),
            new Vector3(0.898605f, 78.411667f, -184.203266f),
            new Vector3(-41.383839f, 35.035000f, -192.717323f),
            new Vector3(-15.315983f, -35.035000f, -196.514655f),
            new Vector3(27.639634f, 8.341667f, -198.107312f)
        };

        private static readonly Vector3[] MidLatitudeRuinPositions =
        {
            new Vector3(-36.958391f, -118.451667f, 157.109261f),
            new Vector3(-64.968982f, 108.441667f, 155.240060f),
            new Vector3(-6.822990f, 135.135000f, 147.553443f),
            new Vector3(80.243888f, 118.451667f, 140.036285f),
            new Vector3(48.634698f, -135.135000f, 139.474865f),
            new Vector3(106.557147f, -108.441667f, 130.253673f),
            new Vector3(-123.385916f, -101.768333f, 120.412466f),
            new Vector3(-75.702787f, -145.145000f, 115.247807f),
            new Vector3(-128.284623f, 125.125000f, 89.257100f),
            new Vector3(105.435556f, 145.145000f, 88.861198f),
            new Vector3(152.610475f, 101.768333f, 80.207788f),
            new Vector3(-140.013143f, -128.461667f, 63.039352f),
            new Vector3(148.417341f, -125.125000f, 48.949640f),
            new Vector3(-168.572708f, 98.431667f, 44.435225f),
            new Vector3(152.178830f, 128.461667f, 20.475446f),
            new Vector3(-141.196118f, 141.808333f, 5.838903f),
            new Vector3(174.243971f, -98.431667f, -5.503233f),
            new Vector3(-164.382076f, -111.778333f, -23.752417f),
            new Vector3(136.994028f, -141.808333f, -34.685342f),
            new Vector3(-156.045658f, 115.115000f, -49.782821f),
            new Vector3(-127.325236f, -138.471667f, -68.512203f),
            new Vector3(150.774363f, 111.778333f, -69.661580f),
            new Vector3(135.358224f, -115.115000f, -92.231924f),
            new Vector3(102.488099f, 138.471667f, -101.989348f),
            new Vector3(-96.552209f, 131.798333f, -115.701816f),
            new Vector3(59.531305f, -131.798333f, -138.438662f),
            new Vector3(-77.650758f, -121.788333f, -138.629007f),
            new Vector3(34.874507f, 121.788333f, -155.020678f),
            new Vector3(-57.669109f, 105.105000f, -160.334815f),
            new Vector3(9.530848f, -105.105000f, -170.123902f)
        };

        private static readonly Vector3[] HighLatitudeRuinPositions =
        {
            new Vector3(-1.724847f, -161.828333f, 117.850140f),
            new Vector3(35.274423f, 161.828333f, 112.460418f),
            new Vector3(-75.056558f, 151.818333f, 106.760230f),
            new Vector3(-23.527692f, 178.511667f, 87.521841f),
            new Vector3(102.394777f, -151.818333f, 80.910712f),
            new Vector3(47.518873f, -178.511667f, 77.172414f),
            new Vector3(-24.036525f, -188.521667f, 62.943361f),
            new Vector3(-82.425464f, -171.838333f, 61.299837f),
            new Vector3(40.994609f, 188.521667f, 53.470209f),
            new Vector3(-99.928673f, 168.501667f, 41.249104f),
            new Vector3(96.488130f, 171.838333f, 35.237310f),
            new Vector3(-32.802159f, 195.195000f, 30.049465f),
            new Vector3(40.011718f, -195.195000f, 19.442592f),
            new Vector3(107.543698f, -168.501667f, 11.026390f),
            new Vector3(-126.182578f, -155.155000f, 9.215368f),
            new Vector3(-24.719946f, -198.531667f, 7.358099f),
            new Vector3(25.791807f, 198.531667f, 0.000000f),
            new Vector3(-74.906578f, 185.185000f, -13.249919f),
            new Vector3(-80.535495f, -181.848333f, -22.918501f),
            new Vector3(123.567689f, 155.155000f, -27.166010f),
            new Vector3(68.013547f, -185.185000f, -34.069241f),
            new Vector3(70.650204f, 181.848333f, -44.941877f),
            new Vector3(-21.044364f, -191.858333f, -53.174756f),
            new Vector3(4.999666f, 191.858333f, -56.968617f),
            new Vector3(-105.826443f, 158.491667f, -61.328587f),
            new Vector3(-75.195618f, -165.165000f, -84.535092f),
            new Vector3(-44.671758f, 175.175000f, -86.012752f),
            new Vector3(83.932160f, -158.491667f, -88.970918f),
            new Vector3(18.276853f, -175.175000f, -95.182540f),
            new Vector3(47.953755f, 165.165000f, -102.474388f),
            new Vector3(-21.452843f, -148.481667f, -132.563231f),
            new Vector3(-17.257428f, 148.481667f, -133.174381f)
        };

        private const string LowLatitudeConstructButtonKey = "surface-ruins-construct-low-latitude";
        private const string MidLatitudeConstructButtonKey = "surface-ruins-construct-mid-latitude";
        private const string HighLatitudeConstructButtonKey = "surface-ruins-construct-high-latitude";

        public void Awake()
        {
            Log = Logger;
            I18N.Add("surface-ruins-menu", "Surface Ruins", "星表废墟");
            I18N.Add(LowLatitudeConstructButtonKey, "Construct low-latitude ruins", "构造低纬度废墟");
            I18N.Add(MidLatitudeConstructButtonKey, "Construct mid-latitude ruins", "构造中纬度废墟");
            I18N.Add(HighLatitudeConstructButtonKey, "Construct high-latitude ruins", "构造高纬度废墟");
            I18N.Add("surface-ruins-build-geothermal-on-idle-ruins", "Build geothermal power stations on idle ruins", "在空闲废墟上建造地热发电站");
            I18N.Apply();
            MyConfigWindow.OnUICreated += CreateUI;
            Log.LogInfo($"Surface Ruins {PluginVersion} initialized");
        }

        public void OnDestroy()
        {
            MyConfigWindow.OnUICreated -= CreateUI;
        }

        private static void CreateUI(MyConfigWindow wnd, RectTransform trans)
        {
            wnd.AddSplitter(trans, 10f);
            wnd.AddTabGroup(trans, "星表废墟", "tab-group-surface-ruins");
            RectTransform tab = wnd.AddTab(trans, "星表废墟");
            wnd.AddButton(10f, 10f, 260, tab, LowLatitudeConstructButtonKey, 16, "button-surface-ruins-construct-low-latitude", ConstructLowLatitudeRuinsOnCurrentPlanet);
            wnd.AddButton(10f, 46f, 260, tab, MidLatitudeConstructButtonKey, 16, "button-surface-ruins-construct-mid-latitude", ConstructMidLatitudeRuinsOnCurrentPlanet);
            wnd.AddButton(10f, 82f, 260, tab, HighLatitudeConstructButtonKey, 16, "button-surface-ruins-construct-high-latitude", ConstructHighLatitudeRuinsOnCurrentPlanet);
            wnd.AddButton(10f, 118f, 340, tab, "surface-ruins-build-geothermal-on-idle-ruins", 16, "button-surface-ruins-build-geothermal-on-idle-ruins", BuildGeothermalOnIdleRuinsCurrentPlanet);
        }

        private enum LatitudeBand
        {
            Low,
            Mid,
            High
        }

        private static void ConstructLowLatitudeRuinsOnCurrentPlanet()
        {
            ConstructRuinsOnCurrentPlanet(LatitudeBand.Low, "low-latitude");
        }

        private static void ConstructMidLatitudeRuinsOnCurrentPlanet()
        {
            ConstructRuinsOnCurrentPlanet(LatitudeBand.Mid, "mid-latitude");
        }

        private static void ConstructHighLatitudeRuinsOnCurrentPlanet()
        {
            ConstructRuinsOnCurrentPlanet(LatitudeBand.High, "high-latitude");
        }

        private static void ConstructRuinsOnCurrentPlanet(LatitudeBand targetBand, string bandName)
        {
            PlanetData planet = GameMain.localPlanet;
            if (planet == null)
            {
                Report("Surface Ruins: no current planet.");
                return;
            }

            PlanetFactory factory = planet.factory;
            if (factory == null || factory.ruinPool == null)
            {
                Report($"Surface Ruins: planet {planet.displayName ?? planet.name} has no loaded factory.");
                return;
            }

            int created = 0;
            int skipped = 0;
            Vector3[] ruinPositions = GetAllRuinPositions();
            for (int i = 0; i < ruinPositions.Length; i++)
            {
                Vector3 templatePos = ruinPositions[i];
                if (GetLatitudeBand(templatePos) != targetBand)
                {
                    continue;
                }

                Vector3 pos = SnapToSurface(planet, templatePos);
                if (HasRuinWithin(factory, pos, DuplicateRuinRadius))
                {
                    skipped++;
                    continue;
                }

                RuinData ruinData = default(RuinData);
                ruinData.modelIndex = BasePitRuinModelIndex;
                ruinData.lifeTime = Level30BasePitLifeTime;
                ruinData.pos = pos;
                ruinData.rot = Maths.SphericalRotation(pos, DeterministicYaw(i));
                factory.AddRuinDataWithComponent(ruinData);
                created++;
            }

            Report($"Surface Ruins: created {created}, skipped {skipped}, total {CountRuinPositions(targetBand)} {bandName} ruins on {planet.displayName ?? planet.name}.");
        }

        private static Vector3[] GetAllRuinPositions()
        {
            Vector3[] positions = new Vector3[LowLatitudeRuinPositions.Length + MidLatitudeRuinPositions.Length + HighLatitudeRuinPositions.Length];
            int offset = 0;
            for (int i = 0; i < LowLatitudeRuinPositions.Length; i++)
            {
                positions[offset + i] = LowLatitudeRuinPositions[i];
            }

            offset += LowLatitudeRuinPositions.Length;
            for (int i = 0; i < MidLatitudeRuinPositions.Length; i++)
            {
                positions[offset + i] = MidLatitudeRuinPositions[i];
            }

            offset += MidLatitudeRuinPositions.Length;
            for (int i = 0; i < HighLatitudeRuinPositions.Length; i++)
            {
                positions[offset + i] = HighLatitudeRuinPositions[i];
            }

            return positions;
        }

        private static int CountRuinPositions(LatitudeBand targetBand)
        {
            int count = 0;
            Vector3[] positions = GetAllRuinPositions();
            for (int i = 0; i < positions.Length; i++)
            {
                if (GetLatitudeBand(positions[i]) == targetBand)
                {
                    count++;
                }
            }

            return count;
        }

        private static LatitudeBand GetLatitudeBand(Vector3 position)
        {
            Vector3 normalized = position.normalized;
            float latitude = Mathf.Asin(Mathf.Clamp(normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            float absLatitude = Mathf.Abs(latitude);
            if (absLatitude < LowLatitudeMax)
            {
                return LatitudeBand.Low;
            }

            if (absLatitude < MidLatitudeMax)
            {
                return LatitudeBand.Mid;
            }

            return LatitudeBand.High;
        }

        private static void BuildGeothermalOnIdleRuinsCurrentPlanet()
        {
            PlanetData planet = GameMain.localPlanet;
            if (planet == null)
            {
                Report("Surface Ruins: no current planet.");
                return;
            }

            PlanetFactory factory = planet.factory;
            if (factory == null || factory.ruinPool == null || factory.powerSystem == null)
            {
                Report($"Surface Ruins: planet {planet.displayName ?? planet.name} has no loaded factory or power system.");
                return;
            }

            ItemProto geothermalItem = FindGeothermalPowerItem();
            if (geothermalItem == null || geothermalItem.prefabDesc == null || !geothermalItem.prefabDesc.geothermal)
            {
                Report("Surface Ruins: geothermal power item not found.");
                return;
            }

            int built = 0;
            int skippedOccupied = 0;
            int skippedBase = 0;
            int skippedFailed = 0;
            RuinData[] ruinPool = factory.ruinPool;
            int ruinCursor = factory.ruinCursor;
            for (int i = 1; i < ruinCursor; i++)
            {
                if (ruinPool[i].id != i || ruinPool[i].modelIndex != BasePitRuinModelIndex)
                {
                    continue;
                }

                if (HasGeothermalOnBaseRuin(factory, i))
                {
                    skippedOccupied++;
                    continue;
                }

                if (HasBaseOnRuin(factory, i))
                {
                    skippedBase++;
                    continue;
                }

                int entityId = BuildGeothermalEntity(factory, geothermalItem, i, ruinPool[i].pos);
                if (entityId > 0)
                {
                    built++;
                    continue;
                }

                skippedFailed++;
            }

            Report($"Surface Ruins: built geothermal {built}, occupied {skippedOccupied}, bound bases {skippedBase}, failed {skippedFailed} on {planet.displayName ?? planet.name}.");
        }

        private static ItemProto FindGeothermalPowerItem()
        {
            if (LDB.items?.dataArray == null)
            {
                return null;
            }

            ItemProto[] items = LDB.items.dataArray;
            for (int i = 0; i < items.Length; i++)
            {
                ItemProto item = items[i];
                if (item?.prefabDesc == null || !item.CanBuild || !item.IsEntity)
                {
                    continue;
                }

                if (item.prefabDesc.isPowerGen && item.prefabDesc.geothermal)
                {
                    return item;
                }
            }

            return null;
        }

        private static bool HasGeothermalOnBaseRuin(PlanetFactory factory, int baseRuinId)
        {
            if (factory == null || factory.powerSystem == null || baseRuinId <= 0)
            {
                return false;
            }

            PowerGeneratorComponent[] genPool = factory.powerSystem.genPool;
            int genCursor = factory.powerSystem.genCursor;
            for (int i = 1; i < genCursor; i++)
            {
                ref PowerGeneratorComponent gen = ref genPool[i];
                if (gen.id == i && gen.geothermal && gen.baseRuinId == baseRuinId)
                {
                    return true;
                }
            }

            PrebuildData[] prebuildPool = factory.prebuildPool;
            int prebuildCursor = factory.prebuildCursor;
            for (int i = 1; i < prebuildCursor; i++)
            {
                ref PrebuildData prebuild = ref prebuildPool[i];
                if (prebuild.id != i || prebuild.paramCount <= 0 || prebuild.parameters == null || prebuild.parameters[0] != baseRuinId)
                {
                    continue;
                }

                ItemProto item = LDB.items.Select(prebuild.protoId);
                if (item?.prefabDesc != null && item.prefabDesc.geothermal)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasBaseOnRuin(PlanetFactory factory, int baseRuinId)
        {
            if (factory?.enemySystem?.bases == null || baseRuinId <= 0)
            {
                return false;
            }

            var bases = factory.enemySystem.bases;
            for (int i = 1; i < bases.cursor; i++)
            {
                var baseComponent = bases.buffer[i];
                if (baseComponent != null && baseComponent.id == i && baseComponent.ruinId == baseRuinId)
                {
                    return true;
                }
            }

            return false;
        }

        private static int BuildGeothermalEntity(PlanetFactory factory, ItemProto geothermalItem, int baseRuinId, Vector3 ruinPos)
        {
            if (factory?.planet == null || geothermalItem?.prefabDesc == null || baseRuinId <= 0)
            {
                return 0;
            }

            Vector3 buildPos = SnapGeothermalBuildPosition(factory.planet, ruinPos);
            Quaternion buildRot = Maths.SphericalRotation(buildPos, 0f);

            PrebuildData prebuild = default(PrebuildData);
            prebuild.isDestroyed = false;
            prebuild.protoId = (short)geothermalItem.ID;
            prebuild.modelIndex = (short)geothermalItem.ModelIndex;
            prebuild.pos = buildPos;
            prebuild.pos2 = buildPos;
            prebuild.rot = buildRot;
            prebuild.rot2 = buildRot;
            prebuild.InitParametersArray(1);
            prebuild.parameters[0] = baseRuinId;

            int prebuildId = factory.AddPrebuildDataWithComponents(prebuild);
            if (prebuildId <= 0)
            {
                return 0;
            }

            EntityData entity = default(EntityData);
            entity.protoId = prebuild.protoId;
            entity.modelIndex = prebuild.modelIndex;
            entity.pos = prebuild.pos;
            entity.rot = prebuild.rot;
            entity.alt = entity.pos.magnitude;
            entity.tilt = prebuild.tilt;
            entity.localized = factory.planet == GameMain.localPlanet && factory.planet.factoryLoaded;

            int entityId = factory.AddEntityDataWithComponents(entity, prebuildId);
            if (entityId <= 0)
            {
                factory.RemovePrebuildWithComponents(prebuildId);
                return 0;
            }

            GameMain.mainPlayer?.controller?.actionBuild?.NotifyBuilt(-prebuildId, entityId);
            factory.RemovePrebuildWithComponents(prebuildId);
            GameMain.history?.MarkItemBuilt(prebuild.protoId);
            if (factory.entityPool[entityId].beltId > 0)
            {
                factory.OnBeltBuilt(entityId);
            }
            if (factory.entityPool[entityId].inserterId > 0)
            {
                factory.OnInserterBuilt(entityId);
            }
            if (geothermalItem.prefabDesc.addonType != EAddonType.None)
            {
                factory.OnAddonBuilt(entityId);
            }
            factory.OnBuildEntity(entityId, prebuildId);
            if (!PlanetFactory.batchBuild)
            {
                factory.OnSinglyBuildEntity(entityId, prebuildId);
            }
            GameMain.gameScenario?.NotifyOnBuild(factory.planet.id, factory.entityPool[entityId].protoId, entityId);
            return entityId;
        }

        private static Vector3 SnapGeothermalBuildPosition(PlanetData planet, Vector3 ruinPos)
        {
            if (planet.aux != null)
            {
                return planet.aux.Snap(ruinPos, onTerrain: true);
            }

            return ruinPos.normalized * (planet.realRadius + SurfaceOffset);
        }

        private static Vector3 SnapToSurface(PlanetData planet, Vector3 templatePos)
        {
            float radius = planet.realRadius + SurfaceOffset;
            Vector3 pos = templatePos.normalized * radius;
            if (planet.aux != null)
            {
                pos = planet.aux.Snap(pos, onTerrain: true);
            }

            return pos;
        }

        private static bool HasRuinWithin(PlanetFactory factory, Vector3 pos, float radius)
        {
            float radiusSqr = radius * radius;
            RuinData[] ruinPool = factory.ruinPool;
            int ruinCursor = factory.ruinCursor;
            for (int i = 1; i < ruinCursor; i++)
            {
                if (ruinPool[i].id != i)
                {
                    continue;
                }

                if ((ruinPool[i].pos - pos).sqrMagnitude < radiusSqr)
                {
                    return true;
                }
            }

            return false;
        }

        private static float DeterministicYaw(int index)
        {
            return (index * 137.50777f) % 360f;
        }

        private static void Report(string message)
        {
            Log?.LogInfo(message);
            UIRealtimeTip.Popup(message, sound: false);
        }
    }
}

