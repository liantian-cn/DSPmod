# FasterResearch

FasterResearch 是一个 Dyson Sphere Program 的 BepInEx / Harmony mod，通过降低研究消耗的矩阵材料来等效加速研究进度。

## 逻辑说明

原版中，每个科技通过 `TechProto.ItemPoints[]` 定义了每种矩阵每个哈希单位的消耗点数。这些值在 `FactorySystem.GameTickLabResearchMode` 中被赋给静态数组 `LabComponent.matrixPoints[]`，随后在 `LabComponent.InternalUpdateResearch` 中用于计算每帧实际消耗。

本 mod 不修改原版研究速度、哈希累积、机甲手搓、Lab 生产矩阵等任何逻辑。它只在 `GameTickLabResearchMode` 后做一件事：

- 检测到当前研究科技切换时；
- 将 `LabComponent.matrixPoints[i]` 的每个非零值除以倍率常量，向上取整。

因此研究消耗的矩阵材料变为原来的 1/N，等效研究进度速度变为原来的 N 倍。

### 倍率常量

```csharp
private const int Multiplier = 24;
```

修改此值并重新编译即可调整倍率。向上取整确保小数值不会被截为 0（例如 `matrixPoints[0] = 24`，除以 24 后 = 1，而非 0）。

## 为什么用修改材料消耗代替修改研究速度？

`InternalUpdateResearch` 中存在基于材料库存的速率上限：

```csharp
research_speed = ((research_speed < (float)num) ? research_speed : ((float)num));
```

其中 `num` 取所有所需矩阵 `matrixServed[i] / matrixPoints[i]` 的最小值。直接扩大 `techSpeed` 会被此上限钳制，需要同步改造物流供料系统才能生效。

降低 `matrixPoints` 的效果是双向的：既等效加速了研究，又减轻了材料供应压力。

## GUI / 配置

本 mod 没有 GUI。倍率通过修改源码中的 `Multiplier` 常量后重新编译来调整。

## 部署

构建后将 `FasterResearch.dll` 放入游戏的 BepInEx 插件目录：

```text
Dyson Sphere Program/BepInEx/plugins/
```
