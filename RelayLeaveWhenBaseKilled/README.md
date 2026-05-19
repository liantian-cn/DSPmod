# RelayLeaveWhenBaseKilled

RelayLeaveWhenBaseKilled 是一个 Dyson Sphere Program 的 BepInEx / Harmony mod，用于让黑雾地面基地核心被打爆时，对应中继站立即离开基地。

## 行为说明

原版在 `EnemyDFGroundSystem.NotifyBaseKilled` 中会留下基地坑，并让中继站继续绑定该基地一段时间；中继站可能继续给基地供给物质和能量，尝试让基地从坑中恢复。

本 mod 使用 Harmony Postfix patch `NotifyBaseKilled`：

- 查找刚被摧毁的 `DFGBaseComponent`；
- 通过 `GetRelay()` 找到仍绑定该基地的中继站；
- 如果中继站已经降落并处于维护地面基地状态，就调用 `LeaveBase()` 让它立即离开；
- 同步增加 `relayNeutralizedCounter`，保持原版“中继站被压制”计数语义。
- 如果原版 `CheckBaseCanRemoved(baseId) == 0`，说明当前基地坑已经满足可处理条件，则在该基地废墟上自动新建一座地热发电站；
- 自动建地热站时复用原版地热发电站的预建造和实体组件创建路径，传入基地废墟 `baseRuinId`，并在已有地热站或地热预建造时跳过，避免重复建造；
- 如果地热发电站组件存在 `isInvincible` 字段，会尝试将其设为 `true`。当前游戏版本的 `PowerGeneratorComponent` 没有这个字段时会自动跳过。

本 mod 不调用 `RemoveBasePit`，不直接移除基地坑。It does not call `RemoveBasePit`. 地热发电站建在基地坑上、基地坑碰撞和地热强度等逻辑仍沿用原版。

## GUI / config

本 mod 没有 GUI，没有配置项。启用 mod 即启用全部逻辑。

## 部署

构建后将 `RelayLeaveWhenBaseKilled.dll` 放入游戏的 BepInEx 插件目录：

```text
Dyson Sphere Program/BepInEx/plugins/
```
