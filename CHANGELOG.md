# 更新日志 / Changelog

## 0.0.24

- fix bug

## 0.0.23

- fix bug

## 0.0.22

- 新增更快中继站发射选项：中继站需求检查每120个巢穴tick运行一次，非气态目标行星均匀获得派遣机会。
- 将更快中继站发射与仅标记中继站派遣统一为同一个中继站控制路径。
- "仅标记中继站派遣"现为"更快中继站发射"的子选项，仅在启用更快中继站发射时生效。
- Added faster relay launch option: relay demand check runs every 120 hive ticks, and non-gas target planets get dispatch opportunities evenly.
- Unified faster relay launch and mark-only relay dispatch into a single relay control path.
- "Mark Only Relay Dispatch" is now a sub-option of "Faster Relay Launch", only effective when Faster Relay Launch is enabled.

## 0.0.21

- 修改当前星球黑雾地面清理：中继站改为归还而非摧毁。
- 清理当前星球地面黑雾后，移除残留的地面基地记录，使基地废墟变为无主废墟而非优先复活目标。
- 当前星球地面黑雾清理现在按顺序处理基地：先模拟击杀地面单位，再模拟摧毁基地建筑，最后归还中继站。
- 修复当前星球地面黑雾清理残留不可交互敌人视觉的问题：强制移除剩余地面敌人并清理本地地面残骸。
- Changed current planet Dark Fog ground cleanup: relays are now returned instead of destroyed.
- After clearing ground Dark Fog on current planet, removed residual ground base records so base ruins become unowned ruins instead of priority revival targets.
- Current planet ground Dark Fog cleanup now processes bases in order: simulate killing ground units, then simulate destroying base buildings, finally return relays.
- Fixed residual non-interactive enemy visuals after current planet ground Dark Fog cleanup: forcibly remove remaining ground enemies and clean up local ground debris.
