# Create a Level 30 Base Ruin at a Fixed Position

This note records how to create a Dark Fog ground base pit ruin directly on a planet surface without creating a real Dark Fog base or attracting relays.

## Core Idea

Create only a `RuinData` entry and add it through `PlanetFactory.AddRuinDataWithComponent`.

Do not create or keep any of these objects for a fake ruin:

- `DFGBaseComponent`
- `EnemyData`
- `DFRelayComponent`
- hive or relay bindings such as `relayId`, `hiveAstroId`, or `baseId`

Creating only `RuinData` gives the planet a visible/collidable base pit ruin. It does not enter the Dark Fog base lifecycle and does not attract enemies. A `modelIndex == 406` ruin also makes relays avoid landing nearby.

## Required Fields

Use these values for a level 30 base pit ruin:

```csharp
RuinData ruinData = default(RuinData);
ruinData.modelIndex = 406;
ruinData.lifeTime = -31;
ruinData.pos = localPos;
ruinData.rot = Maths.SphericalRotation(ruinData.pos, yaw);
int ruinId = factory.AddRuinDataWithComponent(ruinData);
```

Field meanings:

- `modelIndex = 406`: Dark Fog ground base pit ruin.
- `lifeTime = -31`: level 30 pit. Vanilla uses `lifeTime = -1 - level`.
- `pos`: planet-local surface position.
- `rot`: spherical rotation at the ruin position.

For normal buildable planets, `PlanetData.radius` is `200`, `scale` is `1`, and `realRadius` is `200`. Surface build positions are commonly around `realRadius + 0.2`, so `200.2`.

## Helper Example

```csharp
private static int CreateLevel30BaseRuin(PlanetFactory factory, Vector3 localPos, float yaw = 0f)
{
    if (factory == null || factory.planet == null)
    {
        return 0;
    }

    Vector3 pos = localPos;
    if (pos.sqrMagnitude <= 0.01f)
    {
        return 0;
    }

    if (factory.planet.aux != null)
    {
        pos = factory.planet.aux.Snap(pos, onTerrain: true);
    }
    else
    {
        pos = pos.normalized * (factory.planet.realRadius + 0.2f);
    }

    RuinData ruinData = default(RuinData);
    ruinData.modelIndex = 406;
    ruinData.lifeTime = -31;
    ruinData.pos = pos;
    ruinData.rot = Maths.SphericalRotation(pos, yaw);

    return factory.AddRuinDataWithComponent(ruinData);
}
```

## Geothermal Behavior

Geothermal strength for a base pit uses:

```csharp
int level = -factory.GetRuinData(baseRuinId).lifeTime - 1;
strength = 3f + level * 0.1f;
```

For `lifeTime = -31`, the level is `30`, so geothermal strength is:

```text
3.0 + 30 * 0.1 = 6.0
```

When building a geothermal power station on this ruin, pass the ruin id through the vanilla prebuild parameter path:

```csharp
prebuild.InitParametersArray(1);
prebuild.parameters[0] = baseRuinId;
```

The final `PowerGeneratorComponent.baseRuinId` is copied from this parameter when the entity is created.

## Spacing Rules

Relay landing and co-landing checks treat `modelIndex == 406` ruins as blocked nearby positions.

Relevant vanilla squared distances:

- Relay target vs relay target: `11664`, distance `108`.
- Relay target vs existing Dark Fog base core: `6400`, distance `80`.
- Relay target vs base pit ruin `modelIndex == 406`: `2704`, distance `52`.

For generated fake base pit ruins, use at least `52` local-space distance between pit centers. Prefer `55` or `60` to avoid edge cases from snapping and floating point rounding.

## Safety Checklist

- Only call `AddRuinDataWithComponent`.
- Use `modelIndex = 406` for a base pit.
- Use `lifeTime = -31` for level 30.
- Keep fake ruins at least `55` to `60` units apart.
- Do not call `CreateEnemyPlanetBase`.
- Do not allocate `DFGBaseComponent`.
- Do not bind a relay or hive to the ruin.
- Do not call `NotifyBaseKilled`; that path is for a real base core death.
