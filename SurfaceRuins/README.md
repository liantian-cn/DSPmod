# Surface Ruins

中文名：`星表废墟`

Surface Ruins 是一个 Dyson Sphere Program 的 BepInEx / UXAssist mod，用于在当前星球表面批量创建黑雾地面基地基坑废墟。

## 行为说明

本 mod 在 UXAssist 配置窗口里添加一个菜单：

- 菜单名：`星表废墟`
- 按钮名：`在当前星球构造废墟`

点击按钮后，mod 会在当前本地星球上尝试创建 153 个 30 级黑雾基坑废墟。

这些点来自以下命令的输出，并已经内置在 `SurfaceRuins.cs` 中：

```powershell
python estimate_sphere_ruin_capacity.py --emit-points 52.5 --starts 2 --iterations 100 --radius 200.2
```

生成点使用 52.5 的最小间距，半径使用 200.2。运行时会根据当前星球的 `realRadius + 0.2` 重新缩放，并用 `planet.aux.Snap(pos, onTerrain: true)` 吸附到星球表面。

## 废墟类型

每个新建废墟使用：

```csharp
modelIndex = 406;
lifeTime = -31;
```

含义：

- `modelIndex = 406`：黑雾地面基地基坑废墟。
- `lifeTime = -31`：30 级基坑废墟，原版等级公式是 `level = -lifeTime - 1`。
- 30 级基坑对应地热强度为 `3.0 + 30 * 0.1 = 6.0`。

创建入口只使用：

```csharp
factory.AddRuinDataWithComponent(ruinData);
```

## 去重逻辑

为了避免重复点击按钮后在同一位置堆叠废墟，mod 在每个目标点创建前会扫描当前星球已有 `RuinData`。

如果目标点 50 本地距离范围内已经存在任意 active 废墟，则跳过该目标点。

这意味着：

- 第一次点击通常会创建全部目标点。
- 后续重复点击只会补齐缺失点。
- 如果星球上已有其他废墟靠近目标点，也会跳过，避免重叠。

## 不会做的事

本 mod 创建的是“假黑雾基坑废墟”，不是一个真实黑雾地面基地。

它不会创建或绑定：

- `DFGBaseComponent`
- `EnemyData`
- `DFRelayComponent`
- relay / hive / base 绑定关系

它也不会调用：

- `CreateEnemyPlanetBase`
- `NotifyBaseKilled`

因此这些废墟不会进入黑雾基地生命周期，也不会主动吸引敌人过来。

## UI / config

本 mod 依赖 UXAssist：

```csharp
[BepInDependency(UXAssist.PluginInfo.PLUGIN_GUID)]
```

当前没有配置项。启用 mod 后，通过 UXAssist 菜单中的按钮手动执行生成。

## 相关文件

- `SurfaceRuins.cs`：插件主体、UXAssist UI、废墟创建逻辑、内置坐标。
- `CreateLevel30BaseRuin.md`：创建 30 级基坑废墟的研究笔记。
- `estimate_sphere_ruin_capacity.py`：球面点位估算脚本。
- `SurfaceRuins.Tests.ps1`：静态检查脚本。

## 构建和验证

从仓库根目录运行静态检查：

```powershell
powershell -ExecutionPolicy Bypass -File .\SurfaceRuins\SurfaceRuins.Tests.ps1
```

从仓库根目录构建：

```powershell
dotnet build SurfaceRuins\SurfaceRuins.slnx
```

构建后核心产物是：

```text
SurfaceRuins/bin/Debug/SurfaceRuins.dll
```

部署到游戏时，将 DLL 放入 BepInEx 插件目录：

```text
Dyson Sphere Program/BepInEx/plugins/
```
