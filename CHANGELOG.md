# Changelog

## 0.0.22

- Added the faster relay station launch option: relay demand checks run every 120 hive ticks and non-gas target planets receive dispatch chances evenly.
- Unified faster relay launch and marker-only relay dispatch into one relay control path.
- Marker-only relay dispatch is now a sub-option of faster relay launch and only applies when faster relay launch is enabled.

## 0.0.21

- Changed current-planet Dark Fog ground cleanup so relay stations are returned instead of destroyed.
- After clearing current-planet ground Dark Fog, residual ground base records are removed so base ruins become ownerless ruins instead of priority revival targets.
- Current-planet ground Dark Fog cleanup now processes bases in order: simulate killing ground units, simulate destroying base buildings, then return relay stations.
- Fixed current-planet ground Dark Fog cleanup leaving noninteractive residual enemy visuals by hard-removing remaining ground enemies and clearing local ground wreckage.
