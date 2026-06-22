# Play Store assets

Generated graphics for the Google Play listing. Drop these straight into the Play Console.

| File | Size | Play requirement | Use |
|------|------|------------------|-----|
| `icon_512.png` | 512 × 512 | App icon, 512×512, 32-bit PNG | Store icon (matches the launcher icon) |
| `feature_graphic.png` | 1024 × 500 | Feature graphic, 1024×500 | Top banner on the listing |
| `screenshot_1_town.png` | 1080 × 1920 | Phone screenshot (2–8, ratio ≤ 2:1) | "Turn your daily steps into a town" |
| `screenshot_2_insights.png` | 1080 × 1920 | " | "Track your steps, distance & calories" |
| `screenshot_3_build.png` | 1080 × 1920 | " | "Spend your steps on 22 buildings" |
| `screenshot_4_goals.png` | 1080 × 1920 | " | "Goals, streaks and milestones" |
| `screenshot_5_night.png` | 1080 × 1920 | " | "A cosy world that grows as you walk" |

Notes:
- Screenshots are 1080×1920 (16:9), comfortably within Play's 2:1 max side ratio, captured from a
  physical Galaxy S25 Ultra with real step data, then composed onto a branded background with captions.
- The icon is the app's actual adaptive launcher icon, composited to a full 512×512 square.
- Source/compositing recipes: see the project history. To regenerate, recapture the phone screens and
  re-run the compositing (PIL) with updated captions.
- Recommended screenshot order in the listing: tracking value first (1 → 2), then the build/goal hooks
  (3 → 4), then atmosphere (5), matching the Health & Fitness positioning.
