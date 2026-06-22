<div align="center">

# 🧱 Walk Track

### A private step tracker that turns your walking into a cosy little town.

*A privacy-first, offline **step tracker** built with .NET MAUI — wrapped in a gentle town-building game that motivates you to move. Walking is the product; the town is the reward. No account, no cloud, no tracking.*

</div>

---

## What is it?

**Walk Track** rewards the steps you already take. Your phone's step counter earns **Bricks**
(1 Brick per 10 steps), and you spend Bricks to place and upgrade buildings on a living isometric
town. Watch townsfolk move in, keep a daily streak alive for bonus rewards, and come home each
evening to a town that grew while you walked.

Its whole identity is being the **calm, private, no-pressure** walking companion — everything stays
on your phone.

## Highlights

- 🚶 **Walk → Bricks → build** — steps become currency, even while the app is closed
- 🏘️ **22 buildings** to place, upgrade (L1–L3), move and sell across a tiered unlock ladder
- 🌅 **A living world** — real-clock day/night cycle with warm lit windows and chimney smoke at night, four seasons, passing weather, festivals
- 🧑‍🤝‍🧑 **Townsfolk with character** — varied bodies, hair, skin tones, hats and props; pets and rare visitors (a wandering cat, a travelling trader)
- 🌳 **Animated environment** — swaying trees and rippling fountains/ponds
- 🎯 **23 goals & daily streaks** with Brick rewards, plus achievements and insights
- 🔔 **Gentle local reminders** — near-goal, streak-at-risk, go-for-a-walk and goal-reached nudges
- 💾 **Backup & restore** — export your town to a file you control, restore on a new phone
- 📤 **Share your town** — render a card of your town + walking stats to the system share sheet
- 🔒 **100% private** — see below

## 🔒 Privacy by design

Walk Track has **no internet access**, so your data physically cannot leave your device.

- **No account, no cloud, no analytics, no ads, no tracking.**
- **No location / GPS.** Steps come only from the phone's on-device motion sensor.
- **One runtime permission:** Physical activity (`ACTIVITY_RECOGNITION`) to read your step count.
- The `AndroidManifest.xml` deliberately omits `INTERNET` and `ACCESS_NETWORK_STATE`.
- All progress lives in a local SQLite database in the app's private storage; uninstalling removes it.

📄 Full policy: [`docs/privacy.html`](docs/privacy.html) (also published via GitHub Pages for the Play Store listing).

## Tech stack

- **.NET MAUI 10** / **C# 13**, Android-first (iOS kept compilable)
- **MVVM** with the CommunityToolkit.Mvvm
- **SkiaSharp** (`SKCanvasView`) for the isometric town rendering, day/night, weather and townsfolk
- **SQLite** (`sqlite-net`) for on-device persistence
- Android **foreground service** (type `health`) for background step counting

## Project structure

```
Walk-Base/
├─ WalkBase/                  # the .NET MAUI app
│  ├─ Models/                 # data records (PlayerState, PlacedBuilding, Building, …)
│  ├─ Services/               # step reconciliation, currency, catalog, happiness, backup, share, …
│  ├─ ViewModels/             # MVVM view-models
│  ├─ Views/                  # XAML pages (Town, Build, History, Insights, Settings, Privacy, …)
│  ├─ Rendering/              # SkiaSharp isometric renderer + townsfolk simulation
│  ├─ Platforms/Android/      # step sensor, foreground service, widget
│  └─ Resources/              # sprites, icons, fonts, splash
├─ WalkBase.Tests/            # xUnit unit tests (step reconciliation, quests, happiness, …)
└─ docs/                      # hosted privacy policy (GitHub Pages)
```

## Build & run

Requires the **.NET 10 SDK** with the `maui` workload, the **Android SDK** (API 24+), and a **JDK**.

```bash
# from Walk-Base/WalkBase
dotnet build -f net10.0-android -c Debug -t:Run
```

> 💡 Use a **physical device** to exercise step counting — the standard Android emulator usually has
> no `TYPE_STEP_COUNTER` sensor and will show the "no step sensor" screen.

### Tests

```bash
# from Walk-Base/WalkBase.Tests
dotnet test
```

### Release build

See [`WalkBase/RELEASE.md`](WalkBase/RELEASE.md) for the signed Play Store AAB, store-listing copy,
Data Safety answers, the foreground-service declaration, and the pre-launch checklist.

## Credits

Building artwork is composited from the **Kenney "Sketch Town"** pack ([kenney.nl](https://kenney.nl)),
released under **Creative Commons Zero (CC0)** — free for commercial use, attribution not required but
gratefully given. See [`WalkBase/SPRITES.md`](WalkBase/SPRITES.md) for the art pipeline and license notes.

## License

Application code © 2026 Kirk — all rights reserved.
Building art © Kenney, used under CC0 1.0.
