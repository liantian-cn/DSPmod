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

        internal static ManualLogSource Log;

        private static readonly Vector3[] LowLatitudeRuinPositions =
        {
            new Vector3(-173.010403f, -47.818899f, 88.661115f),
            new Vector3(173.010403f, -47.818899f, 88.661115f),
            new Vector3(173.010403f, 47.818899f, 88.661115f),
            new Vector3(-173.010403f, 47.818899f, 88.661115f),
            new Vector3(-117.674608f, -137.775830f, 85.150146f),
            new Vector3(117.674608f, -137.775830f, 85.150146f),
            new Vector3(117.674608f, 137.775830f, 85.150146f),
            new Vector3(-117.674608f, 137.775830f, 85.150146f),
            new Vector3(-161.965202f, -100.100000f, 61.865202f),
            new Vector3(161.965202f, -100.100000f, 61.865202f),
            new Vector3(161.965202f, 100.100000f, 61.865202f),
            new Vector3(-161.965202f, 100.100000f, 61.865202f),
            new Vector3(191.275597f, 0.000000f, 59.107410f),
            new Vector3(-191.275597f, 0.000000f, 59.107410f),
            new Vector3(-32.524462f, -190.401515f, 52.625684f),
            new Vector3(32.524462f, -190.401515f, 52.625684f),
            new Vector3(32.524462f, 190.401515f, 52.625684f),
            new Vector3(-32.524462f, 190.401515f, 52.625684f),
            new Vector3(-88.661115f, -173.010403f, 47.818899f),
            new Vector3(88.661115f, -173.010403f, 47.818899f),
            new Vector3(88.661115f, 173.010403f, 47.818899f),
            new Vector3(-88.661115f, 173.010403f, 47.818899f),
            new Vector3(-190.401515f, -52.625684f, 32.524462f),
            new Vector3(190.401515f, -52.625684f, 32.524462f),
            new Vector3(190.401515f, 52.625684f, 32.524462f),
            new Vector3(-190.401515f, 52.625684f, 32.524462f),
            new Vector3(-136.480014f, -143.456698f, 29.553705f),
            new Vector3(136.480014f, -143.456698f, 29.553705f),
            new Vector3(136.480014f, 143.456698f, 29.553705f),
            new Vector3(-136.480014f, 143.456698f, 29.553705f),
            new Vector3(-170.300292f, -105.251369f, 0.000000f),
            new Vector3(-105.251369f, -170.300292f, 0.000000f),
            new Vector3(-59.107410f, -191.275597f, 0.000000f),
            new Vector3(0.000000f, -200.200000f, 0.000000f),
            new Vector3(59.107410f, -191.275597f, 0.000000f),
            new Vector3(105.251369f, -170.300292f, 0.000000f),
            new Vector3(170.300292f, -105.251369f, 0.000000f),
            new Vector3(200.200000f, 0.000000f, 0.000000f),
            new Vector3(170.300292f, 105.251369f, 0.000000f),
            new Vector3(105.251369f, 170.300292f, 0.000000f),
            new Vector3(59.107410f, 191.275597f, 0.000000f),
            new Vector3(0.000000f, 200.200000f, 0.000000f),
            new Vector3(-59.107410f, 191.275597f, 0.000000f),
            new Vector3(-105.251369f, 170.300292f, 0.000000f),
            new Vector3(-170.300292f, 105.251369f, 0.000000f),
            new Vector3(-200.200000f, 0.000000f, 0.000000f),
            new Vector3(-136.480014f, -143.456698f, -29.553705f),
            new Vector3(136.480014f, -143.456698f, -29.553705f),
            new Vector3(136.480014f, 143.456698f, -29.553705f),
            new Vector3(-136.480014f, 143.456698f, -29.553705f),
            new Vector3(-190.401515f, -52.625684f, -32.524462f),
            new Vector3(190.401515f, -52.625684f, -32.524462f),
            new Vector3(190.401515f, 52.625684f, -32.524462f),
            new Vector3(-190.401515f, 52.625684f, -32.524462f),
            new Vector3(-88.661115f, -173.010403f, -47.818899f),
            new Vector3(88.661115f, -173.010403f, -47.818899f),
            new Vector3(88.661115f, 173.010403f, -47.818899f),
            new Vector3(-88.661115f, 173.010403f, -47.818899f),
            new Vector3(-32.524462f, -190.401515f, -52.625684f),
            new Vector3(32.524462f, -190.401515f, -52.625684f),
            new Vector3(32.524462f, 190.401515f, -52.625684f),
            new Vector3(-32.524462f, 190.401515f, -52.625684f),
            new Vector3(191.275597f, 0.000000f, -59.107410f),
            new Vector3(-191.275597f, 0.000000f, -59.107410f),
            new Vector3(-161.965202f, -100.100000f, -61.865202f),
            new Vector3(161.965202f, -100.100000f, -61.865202f),
            new Vector3(161.965202f, 100.100000f, -61.865202f),
            new Vector3(-161.965202f, 100.100000f, -61.865202f),
            new Vector3(-117.674608f, -137.775830f, -85.150146f),
            new Vector3(117.674608f, -137.775830f, -85.150146f),
            new Vector3(117.674608f, 137.775830f, -85.150146f),
            new Vector3(-117.674608f, 137.775830f, -85.150146f),
            new Vector3(-173.010403f, -47.818899f, -88.661115f),
            new Vector3(173.010403f, -47.818899f, -88.661115f),
            new Vector3(173.010403f, 47.818899f, -88.661115f),
            new Vector3(-173.010403f, 47.818899f, -88.661115f)
        };

        private static readonly Vector3[] MidLatitudeRuinPositions =
        {
            new Vector3(-29.553705f, -136.480014f, 143.456698f),
            new Vector3(29.553705f, -136.480014f, 143.456698f),
            new Vector3(29.553705f, 136.480014f, 143.456698f),
            new Vector3(-29.553705f, 136.480014f, 143.456698f),
            new Vector3(-85.150146f, -117.674608f, 137.775830f),
            new Vector3(85.150146f, -117.674608f, 137.775830f),
            new Vector3(85.150146f, 117.674608f, 137.775830f),
            new Vector3(-85.150146f, 117.674608f, 137.775830f),
            new Vector3(-143.456698f, -29.553705f, 136.480014f),
            new Vector3(143.456698f, -29.553705f, 136.480014f),
            new Vector3(143.456698f, 29.553705f, 136.480014f),
            new Vector3(-143.456698f, 29.553705f, 136.480014f),
            new Vector3(-137.775830f, -85.150146f, 117.674608f),
            new Vector3(137.775830f, -85.150146f, 117.674608f),
            new Vector3(137.775830f, 85.150146f, 117.674608f),
            new Vector3(-137.775830f, 85.150146f, 117.674608f),
            new Vector3(0.000000f, -170.300292f, 105.251369f),
            new Vector3(170.300292f, 0.000000f, 105.251369f),
            new Vector3(0.000000f, 170.300292f, 105.251369f),
            new Vector3(-170.300292f, 0.000000f, 105.251369f),
            new Vector3(-61.865202f, -161.965202f, 100.100000f),
            new Vector3(61.865202f, -161.965202f, 100.100000f),
            new Vector3(61.865202f, 161.965202f, 100.100000f),
            new Vector3(-61.865202f, 161.965202f, 100.100000f),
            new Vector3(-61.865202f, -161.965202f, -100.100000f),
            new Vector3(61.865202f, -161.965202f, -100.100000f),
            new Vector3(61.865202f, 161.965202f, -100.100000f),
            new Vector3(-61.865202f, 161.965202f, -100.100000f),
            new Vector3(0.000000f, -170.300292f, -105.251369f),
            new Vector3(170.300292f, 0.000000f, -105.251369f),
            new Vector3(0.000000f, 170.300292f, -105.251369f),
            new Vector3(-170.300292f, 0.000000f, -105.251369f),
            new Vector3(-137.775830f, -85.150146f, -117.674608f),
            new Vector3(137.775830f, -85.150146f, -117.674608f),
            new Vector3(137.775830f, 85.150146f, -117.674608f),
            new Vector3(-137.775830f, 85.150146f, -117.674608f),
            new Vector3(-143.456698f, -29.553705f, -136.480014f),
            new Vector3(143.456698f, -29.553705f, -136.480014f),
            new Vector3(143.456698f, 29.553705f, -136.480014f),
            new Vector3(-143.456698f, 29.553705f, -136.480014f),
            new Vector3(-85.150146f, -117.674608f, -137.775830f),
            new Vector3(85.150146f, -117.674608f, -137.775830f),
            new Vector3(85.150146f, 117.674608f, -137.775830f),
            new Vector3(-85.150146f, 117.674608f, -137.775830f),
            new Vector3(-29.553705f, -136.480014f, -143.456698f),
            new Vector3(29.553705f, -136.480014f, -143.456698f),
            new Vector3(29.553705f, 136.480014f, -143.456698f),
            new Vector3(-29.553705f, 136.480014f, -143.456698f)
        };

        private static readonly Vector3[] HighLatitudeRuinPositions =
        {
            new Vector3(0.000000f, 0.000000f, 200.200000f),
            new Vector3(0.000000f, -59.107410f, 191.275597f),
            new Vector3(0.000000f, 59.107410f, 191.275597f),
            new Vector3(-52.625684f, -32.524462f, 190.401515f),
            new Vector3(52.625684f, -32.524462f, 190.401515f),
            new Vector3(52.625684f, 32.524462f, 190.401515f),
            new Vector3(-52.625684f, 32.524462f, 190.401515f),
            new Vector3(-47.818899f, -88.661115f, 173.010403f),
            new Vector3(47.818899f, -88.661115f, 173.010403f),
            new Vector3(47.818899f, 88.661115f, 173.010403f),
            new Vector3(-47.818899f, 88.661115f, 173.010403f),
            new Vector3(0.000000f, -105.251369f, 170.300292f),
            new Vector3(105.251369f, 0.000000f, 170.300292f),
            new Vector3(0.000000f, 105.251369f, 170.300292f),
            new Vector3(-105.251369f, 0.000000f, 170.300292f),
            new Vector3(-100.100000f, -61.865202f, 161.965202f),
            new Vector3(100.100000f, -61.865202f, 161.965202f),
            new Vector3(100.100000f, 61.865202f, 161.965202f),
            new Vector3(-100.100000f, 61.865202f, 161.965202f),
            new Vector3(-100.100000f, -61.865202f, -161.965202f),
            new Vector3(100.100000f, -61.865202f, -161.965202f),
            new Vector3(100.100000f, 61.865202f, -161.965202f),
            new Vector3(-100.100000f, 61.865202f, -161.965202f),
            new Vector3(0.000000f, -105.251369f, -170.300292f),
            new Vector3(105.251369f, 0.000000f, -170.300292f),
            new Vector3(0.000000f, 105.251369f, -170.300292f),
            new Vector3(-105.251369f, 0.000000f, -170.300292f),
            new Vector3(-47.818899f, -88.661115f, -173.010403f),
            new Vector3(47.818899f, -88.661115f, -173.010403f),
            new Vector3(47.818899f, 88.661115f, -173.010403f),
            new Vector3(-47.818899f, 88.661115f, -173.010403f),
            new Vector3(-52.625684f, -32.524462f, -190.401515f),
            new Vector3(52.625684f, -32.524462f, -190.401515f),
            new Vector3(52.625684f, 32.524462f, -190.401515f),
            new Vector3(-52.625684f, 32.524462f, -190.401515f),
            new Vector3(0.000000f, -59.107410f, -191.275597f),
            new Vector3(0.000000f, 59.107410f, -191.275597f),
            new Vector3(0.000000f, 0.000000f, -200.200000f)
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

        private static void ConstructLowLatitudeRuinsOnCurrentPlanet()
        {
            ConstructRuinsOnCurrentPlanet(LowLatitudeRuinPositions, "low-latitude");
        }

        private static void ConstructMidLatitudeRuinsOnCurrentPlanet()
        {
            ConstructRuinsOnCurrentPlanet(MidLatitudeRuinPositions, "mid-latitude");
        }

        private static void ConstructHighLatitudeRuinsOnCurrentPlanet()
        {
            ConstructRuinsOnCurrentPlanet(HighLatitudeRuinPositions, "high-latitude");
        }

        private static void ConstructRuinsOnCurrentPlanet(Vector3[] ruinPositions, string bandName)
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
            for (int i = 0; i < ruinPositions.Length; i++)
            {
                Vector3 pos = SnapToSurface(planet, ruinPositions[i]);
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

            Report($"Surface Ruins: created {created}, skipped {skipped}, total {ruinPositions.Length} {bandName} ruins on {planet.displayName ?? planet.name}.");
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
