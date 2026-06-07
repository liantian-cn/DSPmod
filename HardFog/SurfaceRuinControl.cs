using BepInEx.Logging;
using UnityEngine;
using UXAssist.UI;

namespace HardFog
{
    // 地表废墟控制器：在当前星球按预设纬度带创建暗雾基地废墟，并保留一个把闲置废墟转成地热站的辅助功能。
    internal static class SurfaceRuinControl
    {
        // 406 是暗雾基地坑废墟模型；lifeTime 使用负数让废墟按高等级基地坑的长期状态存在。
        private const short BasePitRuinModelIndex = 406;
        private const int Level30BasePitLifeTime = -31;
        // 避免重复点击按钮时在同一区域堆叠废墟；0.2 的表面偏移让生成位置略高于地表。
        private const float DuplicateRuinRadius = 50f;
        private const float SurfaceOffset = 0.2f;
        // 按绝对纬度把坐标表分成低、中、高三档，UI 三个按钮分别使用这些边界筛选。
        private const float LowLatitudeMax = 28.5f;
        private const float MidLatitudeMax = 46.5f;

        // HardFogWindow 注入日志；这里是按钮型模块，不需要单独 Init。
        internal static ManualLogSource Log;

        // 低纬度候选点：坐标按标准半径模板给出，实际生成时会归一化并吸附到当前星球表面。
        private static readonly Vector3[] LowLatitudeRuinPositions =
        {
            new Vector3(11.598103f, -1.578621f, 199.857529f),
            new Vector3(-185.391524f, 1.658643f, 75.546488f),
            new Vector3(-198.351749f, -1.812110f, -27.080253f),
            new Vector3(172.888075f, 2.491250f, -100.913563f),
            new Vector3(197.217750f, 3.839144f, 34.211988f),
            new Vector3(-123.238574f, 5.794695f, 157.666469f),
            new Vector3(-101.052240f, -7.764880f, -172.650489f),
            new Vector3(62.122292f, 10.052953f, 190.052095f),
            new Vector3(-158.363070f, -12.312171f, -121.858887f),
            new Vector3(140.964330f, 14.731406f, -141.393364f),
            new Vector3(-48.379320f, -15.559359f, -193.642422f),
            new Vector3(161.502661f, -16.104509f, 117.207403f),
            new Vector3(-78.527619f, -17.154318f, 183.355345f),
            new Vector3(198.672916f, -17.515802f, -17.387038f),
            new Vector3(8.386353f, -19.746066f, -199.047235f),
            new Vector3(-184.090693f, 19.962110f, -76.106313f),
            new Vector3(-36.132626f, 20.313733f, 195.861751f),
            new Vector3(97.203970f, 21.108621f, -173.740767f),
            new Vector3(108.714643f, 21.139402f, 166.776173f),
            new Vector3(-197.070622f, -22.860905f, 26.843789f),
            new Vector3(190.908836f, 22.943318f, -55.744601f),
            new Vector3(180.176771f, 23.664264f, 84.002226f),
            new Vector3(44.741159f, 23.787829f, -193.681202f),
            new Vector3(67.033541f, -26.218206f, -186.813142f),
            new Vector3(-155.260340f, -26.302968f, 123.622088f),
            new Vector3(-159.490317f, 27.326220f, 117.881959f),
            new Vector3(124.976286f, -29.511194f, 153.590551f),
            new Vector3(-196.865241f, 30.468250f, 19.894784f),
            new Vector3(-14.271550f, 30.715747f, -197.314231f),
            new Vector3(117.312697f, -31.541974f, -159.131628f),
            new Vector3(147.777492f, 32.638154f, 131.059543f),
            new Vector3(183.470306f, -34.182861f, 72.458393f),
            new Vector3(-182.407838f, -35.672493f, -74.396869f),
            new Vector3(-70.616570f, 36.329126f, -183.775773f),
            new Vector3(184.715662f, -37.270373f, -67.609789f),
            new Vector3(-28.479162f, -38.753140f, 194.337777f),
            new Vector3(-119.919474f, 39.394394f, -155.394471f),
            new Vector3(-156.631339f, 40.653768f, -117.872537f),
            new Vector3(-81.807226f, 40.930395f, 178.079535f),
            new Vector3(79.180308f, -40.983027f, 179.250970f),
            new Vector3(195.597223f, 42.285599f, -5.804684f),
            new Vector3(154.425771f, -43.187264f, -119.864847f),
            new Vector3(-126.516077f, -46.164055f, -148.130356f),
            new Vector3(27.430205f, -50.658574f, 191.732451f),
            new Vector3(18.872435f, 50.926695f, 192.692353f),
            new Vector3(-114.125468f, -51.543115f, 156.200912f),
            new Vector3(-176.253751f, -52.210942f, 79.301152f),
            new Vector3(163.270330f, 54.636785f, -102.164872f),
            new Vector3(-189.871160f, 55.023478f, -31.644898f),
            new Vector3(-180.601032f, 55.485613f, 66.216719f),
            new Vector3(190.899852f, -55.878985f, 22.689769f),
            new Vector3(-190.861525f, -56.272185f, -22.030877f),
            new Vector3(-78.986761f, -60.678844f, -173.664071f),
            new Vector3(-121.264336f, 61.620063f, 146.894413f),
            new Vector3(184.685091f, 62.240929f, 45.798734f),
            new Vector3(70.963182f, 64.760933f, 175.642501f),
            new Vector3(124.415900f, 67.099534f, -141.768742f),
            new Vector3(-25.791934f, -67.209432f, -186.809284f),
            new Vector3(29.782177f, -67.922545f, -185.955881f),
            new Vector3(148.509291f, -67.924163f, 115.807334f),
            new Vector3(-152.883917f, -70.722270f, -108.189225f),
            new Vector3(22.646023f, 71.975982f, -185.436392f),
            new Vector3(76.472202f, 72.219469f, -170.341981f),
            new Vector3(-183.562151f, -73.952237f, 30.266210f),
            new Vector3(-64.213366f, -74.720941f, 174.279846f),
            new Vector3(85.749202f, -74.782918f, -164.725923f),
            new Vector3(116.548957f, 74.805218f, 144.570260f),
            new Vector3(-29.867572f, 74.822556f, 183.274530f),
            new Vector3(157.912629f, 74.906226f, 97.635542f),
            new Vector3(183.098914f, -75.511283f, -29.204002f),
            new Vector3(-167.139678f, 77.098305f, -78.741471f),
            new Vector3(176.979559f, 77.115253f, -53.023706f),
            new Vector3(-151.080601f, 78.654886f, 105.205042f),
            new Vector3(-37.712758f, 78.656979f, -180.196747f),
            new Vector3(-142.489475f, -79.655050f, 115.895912f),
            new Vector3(103.995982f, -82.980387f, 149.596562f),
            new Vector3(-181.153609f, 83.628969f, 16.419667f),
            new Vector3(160.656667f, -85.695525f, -83.221105f),
            new Vector3(167.575890f, -86.386359f, 67.348037f),
            new Vector3(-88.217718f, 86.759972f, -157.386091f),
            new Vector3(125.528247f, -87.763799f, -128.919412f),
            new Vector3(-130.954103f, 89.231017f, -122.347409f),
            new Vector3(-168.179415f, -90.522567f, -60.011574f),
            new Vector3(-8.113596f, -90.739354f, 178.271083f),
            new Vector3(51.215063f, -93.244797f, 169.595003f),
            new Vector3(-75.980180f, 93.981409f, 159.607478f),
            new Vector3(176.572714f, 94.351015f, 0.050617f)
        };

        // 中纬度候选点：和低/高纬表共用后续筛选逻辑，便于统一计数和去重。
        private static readonly Vector3[] MidLatitudeRuinPositions =
        {
            new Vector3(-108.123875f, -96.968438f, -137.791109f),
            new Vector3(25.902877f, 100.674274f, 171.095796f),
            new Vector3(141.666194f, 103.509212f, -96.418735f),
            new Vector3(-95.937767f, -104.535087f, 141.238806f),
            new Vector3(-157.515092f, -105.327855f, 64.614849f),
            new Vector3(-165.282099f, 107.857347f, -33.595542f),
            new Vector3(168.046659f, -107.932403f, 13.818713f),
            new Vector3(-168.306125f, -108.237807f, -6.137232f),
            new Vector3(-156.361332f, 108.438175f, 62.228097f),
            new Vector3(-57.946485f, -110.197511f, -156.776125f),
            new Vector3(157.365339f, 110.518925f, 55.693420f),
            new Vector3(47.441406f, -110.767700f, -159.874544f),
            new Vector3(-113.785396f, 113.381226f, 119.489001f),
            new Vector3(-1.807725f, 113.962065f, -164.588638f),
            new Vector3(75.949807f, 113.966881f, 146.024713f),
            new Vector3(-4.943917f, -114.188850f, -164.366980f),
            new Vector3(98.526514f, 114.657807f, -131.248441f),
            new Vector3(123.779156f, -115.184551f, 107.197387f),
            new Vector3(121.717737f, 115.962518f, 108.708450f),
            new Vector3(49.686901f, 118.107951f, -153.823808f),
            new Vector3(-129.183921f, -121.118017f, -93.391545f),
            new Vector3(153.111945f, -122.017823f, -41.814148f),
            new Vector3(-22.378325f, 123.364806f, 156.078106f),
            new Vector3(-42.290641f, -124.481475f, 150.983125f),
            new Vector3(149.387886f, 125.158105f, -45.812096f),
            new Vector3(-134.844687f, 125.377831f, -78.596118f),
            new Vector3(-121.072765f, -126.321081f, 97.285200f),
            new Vector3(86.603131f, -126.852737f, -128.406856f),
            new Vector3(-49.919493f, 127.099080f, -146.403238f),
            new Vector3(72.562503f, -129.022537f, 134.788383f),
            new Vector3(123.926304f, -129.695274f, -88.890084f),
            new Vector3(-151.297663f, 130.341072f, 14.151395f),
            new Vector3(139.857838f, -131.432624f, 56.967451f),
            new Vector3(-94.706788f, 131.936208f, -117.061955f),
            new Vector3(15.986042f, -133.052629f, 148.732930f),
            new Vector3(-140.672761f, -136.049744f, -42.209969f),
            new Vector3(145.443436f, 137.306477f, 8.554427f),
            new Vector3(-67.538026f, 138.233134f, 128.102520f),
            new Vector3(-139.953903f, -140.615231f, 26.838437f),
            new Vector3(-79.925535f, -142.615456f, -115.554232f),
            new Vector3(-117.253818f, 144.185720f, 74.445016f),
            new Vector3(28.545322f, 144.391899f, 135.706242f)
        };

        // 高纬度候选点：包含南北极附近位置，用来补全低纬按钮无法覆盖的区域。
        private static readonly Vector3[] HighLatitudeRuinPositions =
        {
            new Vector3(106.969556f, 145.989900f, -85.583311f),
            new Vector3(117.825926f, 148.032243f, 65.448806f),
            new Vector3(-73.041958f, -149.330421f, 111.558674f),
            new Vector3(132.809275f, -149.804901f, -0.477645f),
            new Vector3(30.395189f, -150.258024f, -128.758296f),
            new Vector3(-25.661842f, -151.738089f, -128.051014f),
            new Vector3(-126.276433f, 152.283651f, -30.724457f),
            new Vector3(76.675554f, 152.386601f, 104.781789f),
            new Vector3(4.496557f, 152.534061f, -129.588507f),
            new Vector3(89.728534f, -154.535821f, 90.263559f),
            new Vector3(58.075591f, 154.996575f, -112.620280f),
            new Vector3(-102.944368f, -160.399601f, 61.273690f),
            new Vector3(-96.850740f, -162.178123f, -66.319157f),
            new Vector3(106.351812f, -162.544538f, -48.462413f),
            new Vector3(-18.005185f, -163.035109f, 114.784173f),
            new Vector3(-20.314518f, 163.264167f, 114.070908f),
            new Vector3(110.264658f, 163.498523f, -34.496061f),
            new Vector3(70.457376f, -163.729369f, -91.150929f),
            new Vector3(-112.093797f, 164.135701f, 23.968573f),
            new Vector3(-89.351696f, 164.136328f, -71.802368f),
            new Vector3(-44.330153f, 164.535668f, -105.085163f),
            new Vector3(37.182648f, -165.607366f, 106.168221f),
            new Vector3(103.644000f, -166.370061f, 40.730381f),
            new Vector3(-110.617252f, -166.509238f, -10.887481f),
            new Vector3(-69.446601f, 167.985169f, 83.893937f),
            new Vector3(105.135662f, 169.374900f, 18.403147f),
            new Vector3(-44.436065f, -177.124686f, -82.050727f),
            new Vector3(27.207053f, 177.218183f, 89.070375f),
            new Vector3(63.618630f, 179.148017f, -62.759047f),
            new Vector3(11.113996f, -179.497282f, -87.961610f),
            new Vector3(68.534053f, 179.855253f, 55.092755f),
            new Vector3(-49.888532f, -180.272664f, 71.364844f),
            new Vector3(11.002197f, 181.013302f, -84.812594f),
            new Vector3(-79.315120f, 182.739437f, -19.885921f),
            new Vector3(81.246248f, -182.930919f, -3.919971f),
            new Vector3(-74.757067f, -184.266004f, 23.183201f),
            new Vector3(55.324586f, -184.626500f, 54.150583f),
            new Vector3(-65.662780f, 186.041548f, 34.014434f),
            new Vector3(-60.847689f, -188.350883f, -30.025717f),
            new Vector3(48.728831f, -188.386857f, -47.073698f),
            new Vector3(-36.873142f, 188.857925f, -55.254822f),
            new Vector3(-19.785994f, 189.090742f, 62.715595f),
            new Vector3(3.207821f, -189.126259f, 65.582072f),
            new Vector3(63.699857f, 189.651172f, -7.402768f),
            new Vector3(-6.878857f, -196.468221f, -37.854450f),
            new Vector3(22.552972f, 196.830625f, 28.794246f),
            new Vector3(16.407601f, 196.842242f, -32.618438f),
            new Vector3(29.249061f, -197.919717f, 7.233123f),
            new Vector3(-23.787590f, -198.044148f, 17.108648f),
            new Vector3(-26.044186f, 198.497490f, 0.697714f)
        };
        // 预设废墟坐标所属纬度带，按钮点击后按这个枚举筛选目标点。
        private enum LatitudeBand
        {
            Low,
            Mid,
            High
        }

        // UI 按钮入口：在当前星球按低纬度坐标创建废墟。
        internal static void ConstructLowLatitudeRuinsOnCurrentPlanet()
        {
            ConstructRuinsOnCurrentPlanet(LatitudeBand.Low, "low-latitude");
        }

        // UI 按钮入口：在当前星球按中纬度坐标创建废墟。
        internal static void ConstructMidLatitudeRuinsOnCurrentPlanet()
        {
            ConstructRuinsOnCurrentPlanet(LatitudeBand.Mid, "mid-latitude");
        }

        // UI 按钮入口：在当前星球按高纬度坐标创建废墟。
        internal static void ConstructHighLatitudeRuinsOnCurrentPlanet()
        {
            ConstructRuinsOnCurrentPlanet(LatitudeBand.High, "high-latitude");
        }

        // 按指定纬度带创建废墟；核心逻辑会吸附到地表、跳过近距离重复点，并写入工厂废墟池。
        private static void ConstructRuinsOnCurrentPlanet(LatitudeBand targetBand, string bandName)
        {
            // 只能对当前已进入的星球操作；没有当前星球时没有工厂和地表可写。
            PlanetData planet = GameMain.localPlanet;
            if (planet == null)
            {
                Report("Surface Ruins: no current planet.");
                return;
            }

            PlanetFactory factory = planet.factory;
            // 废墟数据属于 PlanetFactory；工厂未加载时不能直接写 ruinPool。
            if (factory == null || factory.ruinPool == null)
            {
                Report($"Surface Ruins: planet {planet.displayName ?? planet.name} has no loaded factory.");
                return;
            }

            int created = 0;
            int skipped = 0;
            Vector3[] ruinPositions = GetAllRuinPositions();
            // 遍历完整坐标表后按纬度带筛选，保证三个按钮使用同一套坐标来源和分类规则。
            for (int i = 0; i < ruinPositions.Length; i++)
            {
                Vector3 templatePos = ruinPositions[i];
                if (GetLatitudeBand(templatePos) != targetBand)
                {
                    continue;
                }

                // 模板坐标只表示方向；生成时根据当前星球半径和地形吸附出实际位置。
                Vector3 pos = SnapToSurface(planet, templatePos);
                if (HasRuinWithin(factory, pos, DuplicateRuinRadius))
                {
                    // 近距离已有废墟时跳过，防止重复点击按钮造成重叠废墟或对象池膨胀。
                    skipped++;
                    continue;
                }

                // 构造 RuinData 后交给工厂 API 创建组件，让游戏同时维护数据池和组件状态。
                RuinData ruinData = default(RuinData);
                ruinData.modelIndex = BasePitRuinModelIndex;
                ruinData.lifeTime = Level30BasePitLifeTime;
                ruinData.pos = pos;
                // 用确定性 yaw 分散朝向，不依赖运行时随机数，重复生成同一序号能得到相同朝向。
                ruinData.rot = Maths.SphericalRotation(pos, DeterministicYaw(i));
                factory.AddRuinDataWithComponent(ruinData);
                created++;
            }

            Report($"Surface Ruins: created {created}, skipped {skipped}, total {CountRuinPositions(targetBand)} {bandName} ruins on {planet.displayName ?? planet.name}.");
        }

        // 合并三张坐标表；后续按纬度带筛选，这样分类边界只维护在 GetLatitudeBand 中。
        private static Vector3[] GetAllRuinPositions()
        {
            Vector3[] positions = new Vector3[LowLatitudeRuinPositions.Length + MidLatitudeRuinPositions.Length + HighLatitudeRuinPositions.Length];
            int offset = 0;
            // 复制低纬坐标到结果数组开头。
            for (int i = 0; i < LowLatitudeRuinPositions.Length; i++)
            {
                positions[offset + i] = LowLatitudeRuinPositions[i];
            }

            offset += LowLatitudeRuinPositions.Length;
            // 接着复制中纬坐标，offset 避免覆盖前一段。
            for (int i = 0; i < MidLatitudeRuinPositions.Length; i++)
            {
                positions[offset + i] = MidLatitudeRuinPositions[i];
            }

            offset += MidLatitudeRuinPositions.Length;
            // 最后复制高纬坐标，得到完整候选点列表。
            for (int i = 0; i < HighLatitudeRuinPositions.Length; i++)
            {
                positions[offset + i] = HighLatitudeRuinPositions[i];
            }

            return positions;
        }

        // 统计指定纬度带有多少模板坐标，用于报告里显示本按钮理论目标数量。
        private static int CountRuinPositions(LatitudeBand targetBand)
        {
            int count = 0;
            Vector3[] positions = GetAllRuinPositions();
            // 用同一个 GetLatitudeBand 分类，避免报告数量和实际创建筛选规则不一致。
            for (int i = 0; i < positions.Length; i++)
            {
                if (GetLatitudeBand(positions[i]) == targetBand)
                {
                    count++;
                }
            }

            return count;
        }

        // 根据坐标方向计算绝对纬度，并映射到低/中/高纬带。
        private static LatitudeBand GetLatitudeBand(Vector3 position)
        {
            // 归一化后 y 分量就是纬度正弦；Clamp 用来防止浮点误差超出 Asin 输入范围。
            Vector3 normalized = position.normalized;
            float latitude = Mathf.Asin(Mathf.Clamp(normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            float absLatitude = Mathf.Abs(latitude);
            // 使用绝对纬度让南北半球对称地归入同一纬度带。
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

        // 保留的辅助入口：在当前星球所有闲置基地坑废墟上建地热站。
        internal static void BuildGeothermalOnIdleRuinsCurrentPlanet()
        {
            // 必须在当前星球执行，因为地热站建造需要当前已加载工厂和地表坐标。
            PlanetData planet = GameMain.localPlanet;
            if (planet == null)
            {
                Report("Surface Ruins: no current planet.");
                return;
            }

            PlanetFactory factory = planet.factory;
            // 地热站需要同时写工厂、废墟池和电力系统，缺任何一个都不能安全建造。
            if (factory == null || factory.ruinPool == null || factory.powerSystem == null)
            {
                Report($"Surface Ruins: planet {planet.displayName ?? planet.name} has no loaded factory or power system.");
                return;
            }

            ItemProto geothermalItem = FindGeothermalPowerItem();
            // 通过物品原型动态查找地热站，避免硬编码物品 ID 受游戏版本影响。
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
            // 只处理当前工厂对象池里的有效基地坑废墟。
            for (int i = 1; i < ruinCursor; i++)
            {
                if (ruinPool[i].id != i || ruinPool[i].modelIndex != BasePitRuinModelIndex)
                {
                    continue;
                }

                // 已经有地热站或地热预建绑定到这个废墟时不能重复建造。
                if (HasGeothermalOnBaseRuin(factory, i))
                {
                    skippedOccupied++;
                    continue;
                }

                // 如果废墟仍绑定暗雾基地，先跳过，避免在活跃基地坑上强行建站。
                if (HasBaseOnRuin(factory, i))
                {
                    skippedBase++;
                    continue;
                }

                // 使用和玩家建造类似的预建转实体流程，让电网、历史和场景回调都能正常触发。
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

        // 在物品表中查找可建造的地热发电机原型。
        private static ItemProto FindGeothermalPowerItem()
        {
            // LDB 未初始化时不能查物品表，通常发生在主菜单或加载早期。
            if (LDB.items?.dataArray == null)
            {
                return null;
            }

            ItemProto[] items = LDB.items.dataArray;
            // 遍历而不是硬编码 ID，是为了兼容不同版本或物品表顺序变化。
            for (int i = 0; i < items.Length; i++)
            {
                ItemProto item = items[i];
                // 只考虑真正可建造且会生成实体的物品。
                if (item?.prefabDesc == null || !item.CanBuild || !item.IsEntity)
                {
                    continue;
                }

                // 地热站同时是发电机并带 geothermal 标记。
                if (item.prefabDesc.isPowerGen && item.prefabDesc.geothermal)
                {
                    return item;
                }
            }

            return null;
        }

        // 检查指定废墟是否已经被地热站或地热预建占用。
        private static bool HasGeothermalOnBaseRuin(PlanetFactory factory, int baseRuinId)
        {
            // 没有电力系统或无效废墟 id 时不可能存在可确认的地热占用。
            if (factory == null || factory.powerSystem == null || baseRuinId <= 0)
            {
                return false;
            }

            PowerGeneratorComponent[] genPool = factory.powerSystem.genPool;
            int genCursor = factory.powerSystem.genCursor;
            // 已完成建筑的地热组件会记录 baseRuinId，可直接检查电力发电机池。
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
            // 还未建成的地热预建也要算占用，否则可能重复放置预建。
            for (int i = 1; i < prebuildCursor; i++)
            {
                ref PrebuildData prebuild = ref prebuildPool[i];
                // parameters[0] 记录地热站绑定的 baseRuinId，先过滤无参数或不匹配的预建。
                if (prebuild.id != i || prebuild.paramCount <= 0 || prebuild.parameters == null || prebuild.parameters[0] != baseRuinId)
                {
                    continue;
                }

                ItemProto item = LDB.items.Select(prebuild.protoId);
                // 只有地热原型的预建才算占用这个废墟。
                if (item?.prefabDesc != null && item.prefabDesc.geothermal)
                {
                    return true;
                }
            }

            return false;
        }

        // 检查废墟是否仍被地面暗雾基地占用，活跃基地不能直接放地热站。
        private static bool HasBaseOnRuin(PlanetFactory factory, int baseRuinId)
        {
            // 没有敌人系统或无效废墟 id 时不认为被基地占用。
            if (factory?.enemySystem?.bases == null || baseRuinId <= 0)
            {
                return false;
            }

            var bases = factory.enemySystem.bases;
            // 扫描基地组件池，ruinId 匹配说明这个坑还被暗雾基地绑定。
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

        // 用预建转实体流程在指定基地坑废墟上生成地热站，尽量复用游戏原有建造回调。
        private static int BuildGeothermalEntity(PlanetFactory factory, ItemProto geothermalItem, int baseRuinId, Vector3 ruinPos)
        {
            // 基础数据缺失时不能构造预建，否则后续工厂 API 会读空引用。
            if (factory?.planet == null || geothermalItem?.prefabDesc == null || baseRuinId <= 0)
            {
                return 0;
            }

            // 地热站位置需要吸附到废墟所在星球地表，旋转使用球面朝向。
            Vector3 buildPos = SnapGeothermalBuildPosition(factory.planet, ruinPos);
            Quaternion buildRot = Maths.SphericalRotation(buildPos, 0f);

            // 先创建 PrebuildData，是因为工厂 AddEntityDataWithComponents 需要预建 id 来建立完整组件。
            PrebuildData prebuild = default(PrebuildData);
            prebuild.isDestroyed = false;
            prebuild.protoId = (short)geothermalItem.ID;
            prebuild.modelIndex = (short)geothermalItem.ModelIndex;
            prebuild.pos = buildPos;
            prebuild.pos2 = buildPos;
            prebuild.rot = buildRot;
            prebuild.rot2 = buildRot;
            prebuild.InitParametersArray(1);
            // 地热站通过第一个参数绑定基地坑废墟 id，和游戏原版地热站数据结构保持一致。
            prebuild.parameters[0] = baseRuinId;

            int prebuildId = factory.AddPrebuildDataWithComponents(prebuild);
            // 预建失败说明工厂无法接受这次建造，直接返回失败。
            if (prebuildId <= 0)
            {
                return 0;
            }

            // 再由预建数据生成实体数据；实体的 localized 影响本地渲染/模型创建。
            EntityData entity = default(EntityData);
            entity.protoId = prebuild.protoId;
            entity.modelIndex = prebuild.modelIndex;
            entity.pos = prebuild.pos;
            entity.rot = prebuild.rot;
            entity.alt = entity.pos.magnitude;
            entity.tilt = prebuild.tilt;
            entity.localized = factory.planet == GameMain.localPlanet && factory.planet.factoryLoaded;

            int entityId = factory.AddEntityDataWithComponents(entity, prebuildId);
            // 实体生成失败时要移除预建，避免工厂里留下不可完成的幽灵预建。
            if (entityId <= 0)
            {
                factory.RemovePrebuildWithComponents(prebuildId);
                return 0;
            }

            // 通知玩家建造控制器和历史系统，让统计、成就/场景和 UI 刷新走原版路径。
            GameMain.mainPlayer?.controller?.actionBuild?.NotifyBuilt(-prebuildId, entityId);
            // 实体已生成后删除预建，完成“预建转实体”。
            factory.RemovePrebuildWithComponents(prebuildId);
            GameMain.history?.MarkItemBuilt(prebuild.protoId);
            // 按实体组件类型触发工厂回调，保持传送带、分拣器、插件和普通建筑状态一致。
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
            // 通用建造回调和单体建造回调会更新电力、网格、统计等系统。
            factory.OnBuildEntity(entityId, prebuildId);
            if (!PlanetFactory.batchBuild)
            {
                factory.OnSinglyBuildEntity(entityId, prebuildId);
            }
            // 场景系统也需要知道建筑已生成，避免任务或场景条件漏判。
            GameMain.gameScenario?.NotifyOnBuild(factory.planet.id, factory.entityPool[entityId].protoId, entityId);
            return entityId;
        }

        // 计算地热站实际放置位置；优先使用星球辅助吸附，以匹配地形高度和网格。
        private static Vector3 SnapGeothermalBuildPosition(PlanetData planet, Vector3 ruinPos)
        {
            // planet.aux.Snap 会把位置投到当前星球地表，比简单半径投影更贴合地形。
            if (planet.aux != null)
            {
                return planet.aux.Snap(ruinPos, onTerrain: true);
            }

            // 没有 aux 时退回到球面半径投影，并加一点偏移避免嵌入地表。
            return ruinPos.normalized * (planet.realRadius + SurfaceOffset);
        }

        // 把模板坐标转换为当前星球表面坐标；用于废墟生成。
        private static Vector3 SnapToSurface(PlanetData planet, Vector3 templatePos)
        {
            // 模板坐标只表示方向，先按当前星球真实半径投影。
            float radius = planet.realRadius + SurfaceOffset;
            Vector3 pos = templatePos.normalized * radius;
            // 如果有 aux，就进一步吸附到真实地形，避免废墟悬空或埋入地面。
            if (planet.aux != null)
            {
                pos = planet.aux.Snap(pos, onTerrain: true);
            }

            return pos;
        }

        // 检查目标位置附近是否已有废墟，用于防止重复创建重叠废墟。
        private static bool HasRuinWithin(PlanetFactory factory, Vector3 pos, float radius)
        {
            float radiusSqr = radius * radius;
            RuinData[] ruinPool = factory.ruinPool;
            int ruinCursor = factory.ruinCursor;
            // 只遍历有效 ruinPool 槽位，id 不匹配说明该槽为空或已被回收。
            for (int i = 1; i < ruinCursor; i++)
            {
                if (ruinPool[i].id != i)
                {
                    continue;
                }

                // 使用平方距离避免每次开方，半径判断结果相同。
                if ((ruinPool[i].pos - pos).sqrMagnitude < radiusSqr)
                {
                    return true;
                }
            }

            return false;
        }

        // 为每个模板点生成稳定的朝向角；黄金角能让相邻废墟朝向分散得比较均匀。
        private static float DeterministicYaw(int index)
        {
            return (index * 137.50777f) % 360f;
        }

        // 同时写日志和屏幕提示，按钮操作完成后玩家能马上看到结果。
        private static void Report(string message)
        {
            Log?.LogInfo(message);
            UIRealtimeTip.Popup(message, sound: false);
        }
    }
}

