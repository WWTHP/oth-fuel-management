# Project: FuelManagement

Project layer for `WWTHP/oth-fuel-management`. Game layer: [Over the Hill](../games/over-the-hill/README.md).

Every car has a fuel tank. Driving drains it, any **house** (a `Cabin` point of interest) refills
it, and **side jerry cans** (cosmetic) add range. Design goal: a trip from a house to a question
mark and back is relaxed; distant question marks need planning. When the tank runs dry the car
goes into **limp mode**, driving the way the game drives a car at maximum damage, until it
reaches a house.

Current version: `Mod.Version` in `FuelManagement/Mod.cs`. Menu key **F10**. Settings in
`MelonPreferences.cfg [FuelManagement]`. **No network messages**: each player's game handles
only their own car, so there's nothing to sync and vanilla peers are unaffected.

## Layout

| File | What |
| --- | --- |
| `Mod.cs` | lifecycle wiring |
| `Settings.cs` | all settings (range, tanks, refuel, limp) + the saved fuel level |
| `Car.cs` | finds the local car (`VehicleInput` with input authority), its `Rigidbody`, `CarDamageSystem`, and mounted `VehicleUpgradeObject` parts → extra-tank count |
| `Houses.cs` | scans `PointOfInterest`s: `Cabin`s are houses, everything else counts as a question mark; calibrates the range |
| `Fuel.cs` | per-frame loop: burn by distance + climb, refuel near a house, low-fuel warnings, limp on/off, save |
| `Limp.cs` | limp mode via the game's own `DamageEffects` curves |
| `Hud.cs` | gauge (level, km left, nearest house), messages, F10 menu |
| `Core/FuelTank.cs` | tank level, burn/refuel maths |
| `Core/RangeCalibration.cs` | range from the house/question-mark layout |
| `Core/Trip.cs` | per-frame distance/climb with teleport guard; extra-tank keyword matching |

## Design

### Consumption
Fuel is measured in **metres of flat-road range**. Each frame burns
`(distance + climb × ClimbCost) / range` of a tank. Downhill is free. Frame steps over 30 m
(respawn, fast travel) are ignored.

### Range (AutoRange, default on)
For every question mark, take the straight-line distance to its nearest house. The
`AutoPercentile` (0.75) of those is a typical trip leg:

```
range = leg × 2 (there and back) × AutoWinding (1.5) × AutoComfort (1.6) × RangeScale
```

So a typical round trip uses ~60% of a tank. A question mark twice as far from any house as the
typical one needs ~125%: out of reach without jerry cans or a stop at another house on the way.
Clamped to 2–60 km. Before the map is measured (or with AutoRange off), `FixedRangeKm` (8 km)
applies. The log prints the distance stats (`p50/p75/p95/max`) and the resulting range, so
tuning can start from real numbers.

### Extra tanks
Mounted parts whose `VehiclePartSO` name contains a `TankKeywords` keyword (`jerry,fuel,tank`)
each add `TankBonus` (+40%), up to `MaxExtraTanks` (2). The game's assets contain
`Side_Jerry Cans` / `Side_Jerrycan`. **Unconfirmed in game** which name the mounted part
reports; the log lists every mounted part on change (`Mounted parts: [...]`).

### Refuel
Within `RefuelRadius` (40 m) of a house, moving under 4 m/s, the tank fills in `RefuelSeconds` (6 s).

### Limp mode
`CarDamageSystem` debuffs a damaged car by scaling `originalSpeedLimit` / `originalMaxTorque`
with its `DamageEffects` curves (`maxSpeedMultiplier`, `torqueMultiplier`). With an empty tank
the mod evaluates those curves at their worst end and writes the result into the vehicle's
`speedControl.speedLimit` (limiter on) and the engine torque field that matches
`originalMaxTorque` (`peakRpmTorque` or `torqueCapLimit`), every `LateUpdate` so it wins over the
game's own debuff update. The real damage value is never touched, so repairs, the health UI and
the multiplayer damage state are unaffected. On refuel the captured settings are restored and
`ApplyDriveabilityDebuffs()` re-applies real damage. Fallback multipliers
(`LimpFallbackSpeed` 0.35, `LimpFallbackTorque` 0.4) apply if the curves can't be read.
`accelerationMultiplier` isn't reproduced: what the game applies it to isn't visible from the stubs.

## Riskiest assumptions (check in the first in-game run)

1. Writing VPP `speedControl` / `engine` settings at runtime takes effect (the game's own debuffs
   do the same, but it's unconfirmed). Log: `Limp mode ON. speed limit ... → ...`.
2. Houses = `Cabin`s, question marks = all other POI types. Log: `POIs [Cabin=..., Trail=..., ...]`.
3. The jerry can part's name matches a keyword. Log: `Mounted parts: [...] → N extra tank part(s)`.
4. POIs stream in: the calibration grows as more are found, so the range can change early in a
   session (fuel is stored as a fraction, so the level doesn't jump).

## In-game test plan

1. Single player, start near a house: gauge shows 100% and `house 0.0 km`. Log has the POI line
   with the auto range.
2. Drive to a nearby question mark and back: expect roughly 40–70% used. Refuels at the house
   (`Refuelling at the house…`, then `Tank full.`).
3. Mount side jerry cans: gauge shows `+1 can` and more km.
4. Menu (F10) → Range scale 0.3, drive until empty: `Out of fuel` message, car crawls (LIMP).
   Crawl to a house: refuels, limp off, speed back to normal (and still damaged if it was).
5. Multiplayer client: own gauge works; nothing changes for other players.

## Changelog

| Version | Date | Change |
| --- | --- | --- |
| 0.1.0 | 2026-11-15 | First version: tank, consumption, house refuel, jerry can bonus, auto range, limp mode, gauge, F10 menu. **Needs in-game testing.** |
