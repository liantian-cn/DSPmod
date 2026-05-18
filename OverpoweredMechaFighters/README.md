# OverpoweredMechaFighters

Makes mecha-owned combat fighters extremely strong.

## Behavior

- Affects only mecha fleet fighters, identified by `owner == -1`.
- Does not affect battle base fighters.
- Increases automatic enemy discovery range by 10x.
- Increases fighter active engagement area by 10x so dispatched fighters do not immediately retreat.
- Increases fighter damage by 10x during fighter attack behavior.
- Sets mecha-owned fighters to `isInvincible`, which is honored by the vanilla damage and incoming-damage paths.

The mod has no GUI or config.

## Implementation Notes

The plugin uses Harmony patches and does not persistently edit `GameHistoryData` combat damage ratios or prototype data. Damage and range multipliers are applied only around the relevant vanilla calls.
