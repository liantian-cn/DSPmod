# FasterResearch

FasterResearch 是一个 Dyson Sphere Program 的 BepInEx / Harmony mod，通过提高实验室研究速度并降低每 hash 的矩阵消耗来加速研究进度。

## 逻辑说明

原版中，每个科技通过 `TechProto.ItemPoints[]` 定义了每种矩阵每个哈希单位的消耗点数。这些值在 `FactorySystem.GameTickLabResearchMode` 中被赋给静态数组 `LabComponent.matrixPoints[]`，随后在 `LabComponent.InternalUpdateResearch` 中用于计算每帧实际消耗。

本 mod patch `LabComponent.InternalUpdateResearch` 的 Prefix，在实验室每次实际研究前做两件事：

- 将当前 `LabComponent.matrixPoints[i]` 与 `TechProto.ItemPoints[i]` 原始值逐一比对；
- 若匹配（说明尚未被除过），除以倍率常量，向上取整。
- 将本次调用的 `research_speed` 参数乘以倍率常量。

用原始值比对而非 techId 追踪，是为了正确处理多工厂场景：`LabComponent.matrixPoints` 是静态数组，每个工厂的 `GameTickLabResearchMode` 都可能重新赋值为原始 ItemPoints（`FactorySystem.researchTechId` 不参与序列化，读档后各工厂依次触发重赋值），最后写者覆盖前值。比对原始值可确保每次重赋值后都执行一次除法，不会被跳过。

因此实验室研究速度变为原来的 N 倍，同时每 hash 的矩阵消耗变为原来的 1/N。

### 倍率常量

```csharp
private const int Multiplier = 24;
```

修改此值并重新编译即可调整倍率。向上取整确保小数值不会被截为 0（例如 `matrixPoints[0] = 24`，除以 24 后 = 1，而非 0）。

## 为什么同时修改研究速度和材料消耗？

`InternalUpdateResearch` 中存在基于材料库存的速率上限：

```csharp
research_speed = ((research_speed < (float)num) ? research_speed : ((float)num));
```

其中 `num` 取所有所需矩阵 `matrixServed[i] / matrixPoints[i]` 的最小值。只降低 `matrixPoints` 会提高材料可支持的上限，但供料充足时仍会被原始 `research_speed` 限制，看起来不会变快。

所以需要同时降低 `matrixPoints` 并放大本次 `research_speed`：前者避免材料上限钳制，后者真正提高每帧上传的 hash。

## GUI / 配置

本 mod 没有 GUI。倍率通过修改源码中的 `Multiplier` 常量后重新编译来调整。

## 部署

构建后将 `FasterResearch.dll` 放入游戏的 BepInEx 插件目录：

```text
Dyson Sphere Program/BepInEx/plugins/
```
