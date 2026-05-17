# FasterResearch

FasterResearch 是一个 Dyson Sphere Program 的 BepInEx / Harmony mod，通过降低科技总 hash 需求来加速研究进度。

## 逻辑说明

原版中，每个科技通过 `TechProto.GetHashNeeded(level)` 计算当前等级需要上传的总 hash。`GameHistoryData` 会把这个值保存到 `TechState.hashNeeded`，实验室和机甲研究不断增加 `hashUploaded`，直到达到 `hashNeeded` 后解锁或进入下一等级。

本 mod patch `TechProto.GetHashNeeded` 的 Postfix：

- 将原版返回的总 hash 需求除以倍率常量，向上取整；
- 新游戏和读档后同步未解锁科技的 `TechState.hashNeeded`；
- 保留已有 `hashUploaded`，但如果已有进度超过新的需求，会压到 `hashNeeded - 1`，避免读档时出现超界状态。

因此总研究需求变为原来的 1/N。在同样 hash 上传速度下，完成时间约为原来的 1/N。

### 倍率常量

```csharp
private const int Multiplier = 24;
```

修改此值并重新编译即可调整倍率。向上取整确保小需求不会被截为 0。

## 为什么改总 hash 需求？

只修改 `LabComponent.matrixPoints` 主要降低每 hash 的矩阵消耗；供料充足时，实验室每帧上传的 hash 仍可能被原版 `research_speed` 限制，体感不会稳定达到倍率。

直接降低 `hashNeeded` 更接近“科技消耗减少”：实验室、机甲研究、科技树 UI、读档重算等都会走同一条需求计算链路，不需要改实验室 tick 或矩阵消耗逻辑。

## GUI / 配置

本 mod 没有 GUI。倍率通过修改源码中的 `Multiplier` 常量后重新编译来调整。

## 部署

构建后将 `FasterResearch.dll` 放入游戏的 BepInEx 插件目录：

```text
Dyson Sphere Program/BepInEx/plugins/
```
