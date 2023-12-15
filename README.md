﻿## Overview

This is a mod to add procedural asteroids to the rings of planets.

Initially it was only for Bylen in the Paradiso system, but other planets
should now be able to be configured to have asteroid rings.

Asteroids are generated within about 50km of players and grids, and will
persist after the mod is removed. Voxel cleanup will likely delete
asteroids beyond a certain distance, but this mod should re-generate them
in the same place after a reload or after sufficient player or grid
movement.

## Configuration

Rings are configured using xml files in the mod storage directory in
the world save directory. The settings from these files are also stored
in the world Sandbox.sbc file, with the future idea of being able to
configure these settings from within the game.

Default settings are stored in `ringDefaults.xml`, and individual planet
ring settings are stored in xml files named by the planet's `StorageName` -
e.g. for `Bylen-12345d120000.vx2` will be stored in
`Bylen-12345d120000.xml`.

Default settings exist for the following planets:
* Bylen (from e.g. Paradise)
* Bylen as it is in Ares at War
* Demus as defined in the Perdiso mod

These are disabled by default, and need to be enabled by editing and
renaming the `.xml.example` configuration file(s) that are written in
the mod storage directory.

### Configuration settings

```xml
<?xml version="1.0" encoding="utf-16"?>
<RingConfig xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <PlanetName>Bylen-396150125d120000</PlanetName>
  <ModId>1669459989.sbm</ModId>
  <Vanilla>false</Vanilla>
  <RingOuterRadius>1100000</RingOuterRadius>
  <RingInnerRadius>650000</RingInnerRadius>
  <RingHeight>3500</RingHeight>
  <SectorSize>10000</SectorSize>
  <MaxAsteroidsPerSector>500</MaxAsteroidsPerSector>
  <RingLongitudeAscendingNode>-2.85</RingLongitudeAscendingNode>
  <RingInclination>22.42</RingInclination>
  <MinAsteroidSize>128</MinAsteroidSize>
  <MaxAsteroidSize>2048</MaxAsteroidSize>
  <EntityMovementThreshold>512</EntityMovementThreshold>
  <SizeExponent>2</SizeExponent>
  <ExclusionZoneSize xsi:nil="true" />
  <ExclusionZoneSizeMult xsi:nil="true" />
  <RingZones>
    <RingZone>
      <InnerRadius>880000</InnerRadius>
      <OuterRadius>920000</OuterRadius>
      <RingHeight xsi:nil="true" />
      <InnerRingHeight xsi:nil="true" />
      <OuterRingHeight xsi:nil="true" />
      <MaxAsteroidsPerSector>10</MaxAsteroidsPerSector>
      <MinAsteroidSize xsi:nil="true" />
      <MaxAsteroidSize xsi:nil="true" />
      <TaperEdges xsi:nil="true" />
    </RingZone>
  </RingZones>
  <TaperRingEdge xsi:nil="true" />
  <Enabled>true</Enabled>
  <EarlyLog>false</EarlyLog>
  <LogDebug xsi:nil="true" />
</RingConfig>
```

| Option name | Global Default | Bylen Default | Description |
|-------------|----------------|---------------|-------------|
| PlanetName  | -              | -             | Planet storage name (from config file basename) |
| ModId       | -              | -             | Used to anchor the config to a specific mod (i.e. not apply if the planet comes from a different mod) |
| Vanilla     | -              | false         | Set to true if putting rings around a base-game planet |
| RingOuterRadius | -          | 1100000       | Ring outer radius in metres |
| RingInnerRadius | -          | 650000        | Ring inner radius in metres |
| RingHeight      | -          | 3500          | Distance between ring plane and upper / lower limit of ring |
| SectorSize      | -          | 10000         | Ring sector size in metres |
| MaxAsteroidsPerSector | -    | 50            | Maximum asteroids per sector |
| RingLongitudeAscendingNode | - | -2.85       | Longitude where the ring crosses the planet's equator going northwards (ascending node) |
| RingInclination | -          | 22.42         | Inclination of ring to planet's equator |
| MinAsteroidSize | 128        | -             | Minimum asteroid size in metres |
| MaxAsteroidSize | 2048       | -             | Maximum asteroid size in metres |
| EntityMovementThreshold | 512 | -            | Distance any grid or player needs to move before new sectors are considered for population with asteroids |
| ExclusionZoneSize | 64       | -             | Minimum space around asteroid in metres to exclude other asteroids |
| ExclusionZoneSizeMult | 1.5  | -             | Minimum space around asteroid as a multiple of its size to exclude other asteroids |
| TaperRingEdge | true         | -             | Taper inner and outer edges of ring |
| SizeExponent  | 2.0          | -             | Size weighting exponent. Values larger than 1 prefer smaller sizes, while values smaller than 1 prefer larger sizes |
| Enabled       | -            | -             | Set to true to enable the ring |
| LogDebug      | -            | -             | Used for logging; log debugging information into a file per planet in local storage directory (by default in `AppData\Roaming\SpaceEngineers\Storage\{ModId}_{ClassName}`) |
| EarlyLog      | -            | -             | Used for logging; start logging before planet is ring enable check |
| RingZones     | -            | 1 zone        | Array of zero or more RingZone elements |

RingZone:
| OptionName   | Bylen zone 1 Default | Description |
|--------------|----------------------|-------------|
| InnerRadius  | 880000               | Inner radius of ring zone |
| OuterRadius  | 920000               | Outer radius of ring zone |
| RingHeight   | -                    | Override ring height for this zone |
| InnerRingHeight | -                 | Override ring height for inner edge of this zone |
| OuterRingHeight | -                 | Override ring height for outer edge of this zone |
| MaxAsteroidsPerSector | 10          | Override maximum asteroids per sector in this zone |
| MinAsteroidSize | -                 | Override minimum asteroid size for this zone |
| MaxAsteroidSize | -                 | Override maximum asteroid size for this zone |
| TaperEdges      | -                 | True to taper inner and outer edges toward the normal ring height |

## Future work

I intend to add the ability to define and enable rings using chat commands in the future.