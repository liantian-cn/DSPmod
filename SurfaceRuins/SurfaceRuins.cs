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

        private static readonly Vector3[] RuinPositions =
        {
            new Vector3(45.044817f, 0.000000f, 195.066667f),
            new Vector3(-40.547176f, 37.144535f, 192.500000f),
            new Vector3(5.532998f, -63.045657f, 189.933333f),
            new Vector3(42.910136f, 55.968674f, 187.366667f),
            new Vector3(-75.822938f, -13.412010f, 184.800000f),
            new Vector3(69.940404f, -44.490360f, 182.233333f),
            new Vector3(-22.927527f, 85.289257f, 179.666667f),
            new Vector3(-43.029145f, -82.850001f, 177.100000f),
            new Vector3(92.121302f, 33.642552f, 174.533333f),
            new Vector3(-94.751081f, 39.111869f, 171.966667f),
            new Vector3(45.221964f, -96.636711f, 169.400000f),
            new Vector3(33.120163f, 105.592299f, 166.833333f),
            new Vector3(-99.014728f, -57.381059f, 164.266667f),
            new Vector3(115.285602f, -25.345216f, 161.700000f),
            new Vector3(-69.864905f, 99.375637f, 159.133333f),
            new Vector3(-16.033952f, -123.732903f, 156.566667f),
            new Vector3(97.815435f, 82.438952f, 154.000000f),
            new Vector3(-130.838493f, 5.410582f, 151.433333f),
            new Vector3(94.884583f, -94.422833f, 148.866667f),
            new Vector3(-6.312590f, 136.515571f, 146.300000f),
            new Vector3(-89.288213f, -106.997121f, 143.733333f),
            new Vector3(140.690017f, 18.929643f, 141.166667f),
            new Vector3(-118.585140f, 82.508452f, 138.600000f),
            new Vector3(32.238348f, -143.302691f, 136.033333f),
            new Vector3(74.189464f, 129.470507f, 133.466667f),
            new Vector3(-144.310882f, -46.039107f, 130.900000f),
            new Vector3(139.488822f, -64.447374f, 128.333333f),
            new Vector3(-60.134867f, 143.689190f, 125.766667f),
            new Vector3(-53.408799f, -148.490068f, 123.200000f),
            new Vector3(141.430002f, 74.331645f, 120.633333f),
            new Vector3(-156.339351f, 41.210551f, 118.066667f),
            new Vector3(88.439440f, -137.543649f, 115.500000f),
            new Vector3(27.998912f, 162.917658f, 112.933333f),
            new Vector3(-132.058305f, -102.273374f, 110.366667f),
            new Vector3(168.122556f, -13.928610f, 107.800000f),
            new Vector3(-115.654979f, 125.019644f, 105.233333f),
            new Vector3(0.838434f, -171.868824f, 102.666667f),
            new Vector3(116.490299f, 128.413552f, 100.100000f),
            new Vector3(-174.088960f, -16.134526f, 97.533333f),
            new Vector3(140.387522f, -106.549124f, 94.966667f),
            new Vector3(-31.787746f, 174.733566f, 92.400000f),
            new Vector3(-95.290289f, -151.425801f, 89.833333f),
            new Vector3(173.771583f, 47.623586f, 87.266667f),
            new Vector3(-161.389071f, 82.822206f, 84.700000f),
            new Vector3(63.466640f, -171.190365f, 82.133333f),
            new Vector3(69.266323f, 170.150998f, 79.566667f),
            new Vector3(-166.995602f, -79.142332f, 77.000000f),
            new Vector3(177.599336f, -54.755773f, 74.433333f),
            new Vector3(-94.478649f, 161.211064f, 71.866667f),
            new Vector3(-39.433925f, -183.636912f, 69.300000f),
            new Vector3(153.878183f, 109.307854f, 66.733333f),
            new Vector3(-188.182476f, 23.452820f, 64.166667f),
            new Vector3(123.468172f, -145.055474f, 61.600000f),
            new Vector3(6.971533f, 191.171398f, 59.033333f),
            new Vector3(-134.817786f, -136.805409f, 56.466667f),
            new Vector3(192.556214f, 9.845529f, 53.900000f),
            new Vector3(-149.174507f, 123.255407f, 51.333333f),
            new Vector3(26.830384f, -192.307001f, 48.766667f),
            new Vector3(110.473012f, 160.440997f, 46.200000f),
            new Vector3(-190.411582f, -43.813260f, 43.633333f),
            new Vector3(170.482354f, -96.588487f, 41.066667f),
            new Vector3(-60.624392f, 186.875555f, 38.500000f),
            new Vector3(-81.731608f, -179.189229f, 35.933333f),
            new Vector3(181.722154f, 77.095813f, 33.366667f),
            new Vector3(-186.466544f, 66.042623f, 30.800000f),
            new Vector3(93.063126f, -174.991924f, 28.233333f),
            new Vector3(49.670726f, 192.234443f, 25.666667f),
            new Vector3(-166.742234f, -108.367234f, 23.100000f),
            new Vector3(196.429095f, -32.772441f, 20.533333f),
            new Vector3(-122.856010f, 157.046616f, 17.966667f),
            new Vector3(-15.509945f, -199.003321f, 15.400000f),
            new Vector3(145.993947f, 136.385897f, 12.833333f),
            new Vector3(-199.927063f, -1.950669f, 10.266667f),
            new Vector3(148.823419f, -133.687472f, 7.700000f),
            new Vector3(-19.441163f, 199.187675f, 5.133333f),
            new Vector3(-120.243687f, -160.046580f, 2.566667f),
            new Vector3(196.790038f, 36.792947f, 0.000000f),
            new Vector3(-169.946160f, 105.791091f, -2.566667f),
            new Vector3(53.838873f, -192.756490f, -5.133333f),
            new Vector3(90.468807f, 178.426862f, -7.700000f),
            new Vector3(-187.126593f, -70.415011f, -10.266667f),
            new Vector3(185.408331f, -74.425105f, -12.833333f),
            new Vector3(-86.362395f, 179.956708f, -15.400000f),
            new Vector3(-57.815830f, -190.826017f, -17.966667f),
            new Vector3(171.319411f, 101.528724f, -20.533333f),
            new Vector3(-194.631873f, 40.802744f, -23.100000f),
            new Vector3(115.769981f, -161.302739f, -25.666667f),
            new Vector3(23.551818f, 196.794895f, -28.233333f),
            new Vector3(-150.009278f, -128.951993f, -30.800000f),
            new Vector3(197.301481f, -6.231470f, -33.366667f),
            new Vector3(-140.951869f, 137.555102f, -35.933333f),
            new Vector3(10.989223f, -196.155619f, -38.500000f),
            new Vector3(124.068574f, 151.659348f, -41.066667f),
            new Vector3(-193.378900f, -27.942319f, -43.633333f),
            new Vector3(160.978006f, -109.689022f, -46.200000f),
            new Vector3(-44.462804f, 189.010347f, -48.766667f),
            new Vector3(-94.565298f, -168.826341f, -51.333333f),
            new Vector3(183.106075f, 60.390358f, -53.900000f),
            new Vector3(-175.138700f, 78.854241f, -56.466667f),
            new Vector3(75.571077f, -175.738778f, -59.033333f),
            new Vector3(62.719055f, 179.866062f, -61.600000f),
            new Vector3(-166.997049f, -89.859136f, -64.166667f),
            new Vector3(182.976648f, -46.327622f, -66.733333f),
            new Vector3(-103.118386f, 156.984548f, -69.300000f),
            new Vector3(-29.850764f, -184.456375f, -71.866667f),
            new Vector3(145.819012f, 115.223846f, -74.433333f),
            new Vector3(-184.309131f, 13.460468f, -77.000000f),
            new Vector3(126.063101f, -133.631135f, -79.566667f),
            new Vector3(-2.671891f, 182.556886f, -82.133333f),
            new Vector3(-120.563325f, -135.537577f, -84.700000f),
            new Vector3(179.239624f, 18.377330f, -87.266667f),
            new Vector3(-143.563687f, 106.768348f, -89.833333f),
            new Vector3(33.491011f, -174.415115f, -92.400000f),
            new Vector3(92.407883f, 150.073833f, -94.966667f),
            new Vector3(-168.158523f, -47.853945f, -97.533333f),
            new Vector3(155.017261f, -77.651006f, -100.100000f),
            new Vector3(-61.314635f, 160.561861f, -102.666667f),
            new Vector3(-62.672622f, -158.360753f, -105.233333f),
            new Vector3(151.733304f, 73.730620f, -107.800000f),
            new Vector3(-160.089154f, 47.651879f, -110.366667f),
            new Vector3(84.969925f, -141.796383f, -112.933333f),
            new Vector3(32.770577f, 160.205740f, -115.500000f),
            new Vector3(-130.889064f, -94.912355f, -118.066667f),
            new Vector3(158.732402f, -18.211632f, -120.633333f),
            new Vector3(-103.450644f, 119.162764f, -123.200000f),
            new Vector3(-4.157611f, -155.709665f, -125.766667f),
            new Vector3(106.781309f, 110.491392f, -128.333333f),
            new Vector3(-151.196545f, -9.210586f, -130.900000f),
            new Vector3(115.955764f, -93.919910f, -133.466667f),
            new Vector3(-21.714831f, 145.270225f, -136.033333f),
            new Vector3(-80.764197f, -119.779900f, -138.600000f),
            new Vector3(138.025587f, 33.180559f, -141.166667f),
            new Vector3(-121.914916f, 67.509422f, -143.733333f),
            new Vector3(43.437086f, -129.574571f, -146.300000f),
            new Vector3(54.359952f, 122.326412f, -148.866667f),
            new Vector3(-120.045387f, -52.317212f, -151.433333f),
            new Vector3(120.993217f, -41.529283f, -154.000000f),
            new Vector3(-59.655705f, 109.581548f, -156.566667f),
            new Vector3(-29.240921f, -117.905007f, -159.133333f),
            new Vector3(98.340698f, 65.285964f, -161.700000f),
            new Vector3(-113.058048f, 17.730767f, -164.266667f),
            new Vector3(69.033503f, -86.493088f, -166.833333f),
            new Vector3(7.252210f, 106.447571f, -169.400000f),
            new Vector3(-74.219392f, -70.703517f, -171.966667f),
            new Vector3(98.053525f, 1.913580f, -174.533333f),
            new Vector3(-70.056303f, 61.706924f, -177.100000f),
            new Vector3(9.436368f, -87.811638f, -179.666667f),
            new Vector3(49.141443f, 66.754557f, -182.233333f),
            new Vector3(-75.546809f, -14.888910f, -184.800000f),
            new Vector3(60.233074f, -36.684452f, -187.366667f),
            new Vector3(-17.619240f, 60.785946f, -189.933333f),
            new Vector3(-24.387810f, -49.285137f, -192.500000f),
            new Vector3(42.002000f, 16.274752f, -195.066667f)
        };

        public void Awake()
        {
            Log = Logger;
            I18N.Add("surface-ruins-menu", "Surface Ruins", "星表废墟");
            I18N.Add("surface-ruins-construct-current-planet", "Construct ruins on current planet", "在当前星球构造废墟");
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
            wnd.AddButton(10f, 10f, 260, tab, "surface-ruins-construct-current-planet", 16, "button-surface-ruins-construct-current-planet", ConstructRuinsOnCurrentPlanet);
            wnd.AddButton(10f, 46f, 340, tab, "surface-ruins-build-geothermal-on-idle-ruins", 16, "button-surface-ruins-build-geothermal-on-idle-ruins", BuildGeothermalOnIdleRuinsCurrentPlanet);
        }

        private static void ConstructRuinsOnCurrentPlanet()
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
            for (int i = 0; i < RuinPositions.Length; i++)
            {
                Vector3 pos = SnapToSurface(planet, RuinPositions[i]);
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

            Report($"Surface Ruins: created {created}, skipped {skipped}, total {RuinPositions.Length} on {planet.displayName ?? planet.name}.");
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
