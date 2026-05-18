# SmartRelayDispatch

SmartRelayDispatch 是一个 Dyson Sphere Program 的 BepInEx / Harmony mod，用于只修改暗雾 Hive 派出 Relay（中继站）时的概率判定。

## 行为说明

原版 `EnemyDFHiveSystem.DetermineRelayDemand()` 在没有 marker / 全息信标目标时，会根据 `relayNeutralizedCounter` 做随机概率判断；有有效 marker 目标时，原版已经会强制通过这段概率判断。

本 mod 使用 Harmony transpiler 只替换 `DetermineRelayDemand()` 中的这段随机概率门：

- 没有被选中的 marker / 全息信标目标：派出概率为 0%；
- 有被选中的 marker / 全息信标目标：派出概率为 100%。

除这段概率门以外，Relay 和 Hive 的其他逻辑都保持原版，包括需求判定频率、目标选择、每次最多派出 1 个 idle Relay、星系 Relay 上限、物质需求、飞行逻辑、落点搜索、landing checks、建造、战斗以及资源消耗。

## GUI / config

本 mod 没有 GUI，没有配置项。启用 mod 即启用上面的概率修改。

## 部署

构建后将 `SmartRelayDispatch.dll` 放入游戏的 BepInEx 插件目录：

```text
Dyson Sphere Program/BepInEx/plugins/
```
