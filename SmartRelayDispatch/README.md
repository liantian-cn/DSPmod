# SmartRelayDispatch

SmartRelayDispatch 是一个 Dyson Sphere Program 的 BepInEx / Harmony mod，用于限制暗雾 Hive 派出 Relay（中继站），使其仅在全息信标选中目标时派出。

## 逻辑说明

原版 `EnemyDFHiveSystem` 会在 Hive 的物质统计窗口结束时调用 `DetermineRelayDemand()`。这个窗口是 600 个 Hive ticks，实际约等于 10 分钟一次。

本 mod 不修改原版物质统计逻辑，也不修改 Relay 飞行、落点搜索、能量 / 物质消耗、建造、战斗或其他 Hive 逻辑。它只在 `KeyTickLogic` 后额外检查一次：

- Hive 存活；
- `matterStatComplete` 已经为 true；
- 当前 `ticks` 能被 60 整除；
- 当前 `ticks` 不能被 600 整除，避免和原版 10 分钟检查重复。

满足条件时，额外调用一次 `DetermineRelayDemand()`。因此 Relay 派出需求判定从原版约 10 分钟一次，变为约 1 分钟一次。

## 派出概率

原版在没有吸引 Relay 的 marker / 全息信标目标时，会根据 `relayNeutralizedCounter` 做随机概率判断；有目标时会强制通过概率判断。

本 mod 用 Harmony transpiler 只替换这段随机概率判断：

- 没有被选中的吸引 Relay marker / 全息信标目标：派出概率为 0%；
- 有被选中的吸引 Relay marker / 全息信标目标：派出概率为 100%。

目标选择、每次最多派出 1 个 idle Relay、星系 Relay 上限、物质需求和空闲 Relay 条件仍沿用原版逻辑。

## 全息信标落点检查

当 Relay 已经被全息信标吸引并派出时，本 mod 保留原版同一信标重复派遣保护，但忽略后续会让该 Relay 折返的 landing checks。

## GUI / 配置

本 mod 没有 GUI，没有配置项。启用 mod 即启用全部逻辑。

## 部署

构建后将 `SmartRelayDispatch.dll` 放入游戏的 BepInEx 插件目录：

```text
Dyson Sphere Program/BepInEx/plugins/
```
