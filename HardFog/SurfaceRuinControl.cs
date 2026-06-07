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
        // HardFogWindow 注入日志；这里是按钮型模块，不需要单独 Init。
        internal static ManualLogSource Log;

        // UI 按钮入口：在当前星球按低纬度坐标创建废墟。
        internal static void ConstructLowLatitudeRuinsOnCurrentPlanet()
        {
            ConstructRuinsOnCurrentPlanet(RuinPositions.LatitudeBand.Low, "low-latitude");
        }

        // UI 按钮入口：在当前星球按中纬度坐标创建废墟。
        internal static void ConstructMidLatitudeRuinsOnCurrentPlanet()
        {
            ConstructRuinsOnCurrentPlanet(RuinPositions.LatitudeBand.Mid, "mid-latitude");
        }

        // UI 按钮入口：在当前星球按高纬度坐标创建废墟。
        internal static void ConstructHighLatitudeRuinsOnCurrentPlanet()
        {
            ConstructRuinsOnCurrentPlanet(RuinPositions.LatitudeBand.High, "high-latitude");
        }

        // 按指定纬度带创建废墟；核心逻辑会吸附到地表、跳过近距离重复点，并写入工厂废墟池。
        private static void ConstructRuinsOnCurrentPlanet(RuinPositions.LatitudeBand targetBand, string bandName)
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
            Vector3[] ruinPositions = RuinPositions.All;
            // 遍历完整坐标表后按纬度带筛选，保证三个按钮使用同一套坐标来源和分类规则。
            for (int i = 0; i < ruinPositions.Length; i++)
            {
                Vector3 templatePos = ruinPositions[i];
                if (RuinPositions.GetLatitudeBand(templatePos) != targetBand)
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

            Report($"Surface Ruins: created {created}, skipped {skipped}, total {RuinPositions.CountRuinPositions(targetBand)} {bandName} ruins on {planet.displayName ?? planet.name}.");
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

