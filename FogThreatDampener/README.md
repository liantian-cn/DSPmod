# FogThreatDampener

FogThreatDampener 是一个 Dyson Sphere Program 的 BepInEx / Harmony mod，用于缓慢降低太空黑雾巢穴的常态威胁值。

## 逻辑说明

本 mod patch `EnemyDFHiveSystem.KeyTickLogic()`。每个太空 Hive 存活时，它的 `ticks` 约每秒增加 1。

当满足以下条件时，本 mod 会把当前太空巢穴威胁值乘以 `0.99`，即降低 1%：

- Hive 存活；
- 当前 `ticks` 能被 30 整除，也就是每 30 Hive ticks 触发一次；
- Hive 不在太空突袭中；
- Hive 不在威胁满值后的集结状态；
- 当前威胁值大于 0 且小于最大威胁值。

## 主动威胁

本 mod 只修改 `evolve.threat`，不修改 `evolve.threatshr`。攻击巢穴、攻击中继站、地面基地受击等主动行为产生的延迟威胁仍由原版逻辑写入 `threatshr`，再由 `SpaceSector` 汇总到威胁值中。

因此它主要降低戴森球等常态来源积累出来的威胁，不会直接清空主动攻击带来的威胁缓冲，也不会中断已经开始的太空突袭或集结。

## GUI / config

本 mod 没有 GUI，没有 config。启用 mod 即启用全部逻辑。

## 部署

构建后将 `FogThreatDampener.dll` 放入游戏的 BepInEx 插件目录：

```text
Dyson Sphere Program/BepInEx/plugins/
```
