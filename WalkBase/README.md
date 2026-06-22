# WalkBase

A privacy-first gamified walking app (.NET MAUI 10). Your phone's step counter earns
**Bricks**; spend Bricks to place and upgrade buildings on a small 2D isometric base.

> **Core loop:** Walk → earn Bricks → build/upgrade → unlock the next building → walk more.

## Privacy (hard requirements — see spec §3)

- **No location**, **no network**, **no account**. All data is local (SQLite in the app sandbox).
- The only runtime permission is **Activity Recognition** (required by the step sensor on Android 10+).
- The `AndroidManifest.xml` deliberately does **not** declare `INTERNET` or `ACCESS_NETWORK_STATE`.

## Build & run (Android first)

```bash
# from the WalkBase/ folder
dotnet build -f net10.0-android -c Debug

# deploy to a connected device (recommended — emulators usually lack the step sensor)
dotnet build -f net10.0-android -c Debug -t:Run
```

Requires the .NET 10 SDK with the `maui` workload, the Android SDK (API 24+), and a JDK.
A **physical device** is needed to exercise step counting: the standard Android emulator
typically has no `TYPE_STEP_COUNTER` sensor, so the app will show the "no sensor" screen.

### iOS

iOS is kept compilable but device testing is deferred (spec §1). On this machine the
`net10.0-ios` build currently fails because the installed .NET-for-iOS SDK (26.5) requires
**Xcode 26.5** (Xcode 26.3 is installed). Update Xcode to build the iOS target.

## Art assets (optional — the app runs without them)

Building/tile sprites are loaded from `Resources/Raw/Sprites/` by filename. Until you add
them, the renderer draws colored isometric placeholders, so the full loop is playable with
zero assets. To use real art:

1. Download the **Kenney "Isometric Tiles"** packs (CC0) from <https://kenney.nl/assets> (spec §9).
2. Export/rename PNGs to the filenames referenced in `Services/BuildingCatalog.cs`
   (e.g. `town_hall_l1.png`, `house_l2.png`, …) and drop them into `Resources/Raw/Sprites/`.
   They're already globbed as `MauiAsset`, so they load automatically.

## Project layout

| Area | Files |
|---|---|
| Models | `Models/PlayerState.cs`, `PlacedBuilding.cs`, `Building.cs` |
| Services | `StepService` (+ platform `StepSensor`), `CurrencyService`, `GameDataService`, `BuildingCatalog`, `SelectionState` |
| Rendering | `Rendering/IsoMath.cs`, `IsometricRenderer.cs`, `SpriteCache.cs` |
| ViewModels | `BaseViewModel`, `BuildViewModel`, `StatsViewModel` |
| Views | `BasePage` (Skia canvas), `BuildPage`, `StatsPage` — 3 Shell tabs |

## Tests

Pure game logic (step delta/reboot reconciliation, currency conversion, catalog integrity)
is unit-tested in the sibling `../WalkBase.Tests` project:

```bash
cd ../WalkBase.Tests && dotnet test
```

## Tuning

- Conversion rate: `CurrencyService.STEPS_PER_BRICK` (default 10).
- Grid size: `GameDataService.GridSize` (default 8×8).
- Building costs / unlocks: `Services/BuildingCatalog.cs`.
- Tile dimensions: `Rendering/IsoMath.cs` (`TileWidth`/`TileHeight`).
