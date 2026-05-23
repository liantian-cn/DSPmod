# Surface Ruins

中文名：`星表废墟`

`Surface Ruins` 是一个 Dyson Sphere Program 的 BepInEx / UXAssist 插件，用来在当前星球上快速生成黑雾基地坑废墟，或者直接在空闲废墟上补建地热发电站。

## 功能

插件会在 UXAssist 配置窗口里添加一个 `星表废墟` 分组，目前包含四个按钮：

- `构造低纬度废墟`
- `构造中纬度废墟`
- `构造高纬度废墟`
- `在空闲废墟上建造地热发电站`

前三个按钮分别在当前本地星球上生成三组 30 级黑雾基地坑废墟，按绝对纬度分组：

- 低纬度组：南北纬 28.5 度以内
- 中纬度组：南北纬 28.5 到 46.5 度之间
- 高纬度组：南北纬 46.5 度以上

第四个按钮会扫描当前星球上的 406 号基地坑废墟，只对“没有已建地热、没有地热预建、也没有真实黑雾基地绑定”的空闲废墟创建地热发电站。

## 原理

### 废墟生成

废墟不是通过真实黑雾基地系统生成的，而是直接调用：

```csharp
factory.AddRuinDataWithComponent(ruinData);
```

生成时使用这些关键字段：

```csharp
ruinData.modelIndex = 406;
ruinData.lifeTime = -31;
```

- `modelIndex = 406` 表示黑雾地面基地坑废墟
- `lifeTime = -31` 对应 30 级基地坑

生成位置会先按当前星球半径缩放，再通过 `planet.aux.Snap(pos, onTerrain: true)` 吸附到地表。

### 地热建造

地热按钮复用了游戏原版的预建造和实体创建流程：

1. 扫描 `LDB.items`，动态找到可建造的地热发电站物品
2. 扫描 `ruinPool`，只挑选 `modelIndex == 406` 的废墟
3. 排除已经有地热发电站、地热预建，或者仍绑定真实黑雾基地的废墟
4. 构造 `PrebuildData`，并把目标废墟 id 写入：

```csharp
prebuild.InitParametersArray(1);
prebuild.parameters[0] = baseRuinId;
```

5. 通过 `AddPrebuildDataWithComponents` 和 `AddEntityDataWithComponents` 完成实体创建
6. 清理临时 prebuild，并触发原版建造回调

这个流程的关键点是：**不检查背包、不检查地基、不检查沙土，也不走真实基地移除路径**。它只负责把地热站直接建在“空闲废墟”上。

## 空闲废墟定义

这里的“空闲废墟”指的是：

- 当前星球上的 406 号基地坑废墟
- 没有已建成的地热发电站
- 没有正在等待完成的地热预建
- 没有仍然绑定的真实黑雾基地

## 构建与验证

从仓库根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\SurfaceRuins\SurfaceRuins.Tests.ps1
python .\SurfaceRuins\generate_icosphere_ruin_positions.py --write .\SurfaceRuins\SurfaceRuins.cs
```

脚本会同时导出同名 CSV：

```text
SurfaceRuins\generate_icosphere_ruin_positions.csv
```

输出 DLL 位于：

```text
SurfaceRuins\bin\Debug\SurfaceRuins.dll
```

部署到游戏时，把 DLL 放到 `Dyson Sphere Program/BepInEx/plugins/`。
