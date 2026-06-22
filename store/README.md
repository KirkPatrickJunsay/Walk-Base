# Play Store assets

Generated graphics for the Google Play listing. Drop these straight into the Play Console.

| File | Size | Play requirement | Use |
|------|------|------------------|-----|
| `icon_512.png` | 512 × 512 | App icon, 512×512, 32-bit PNG | Store icon (matches the launcher icon) |
| `feature_graphic.png` | 1024 × 500 | Feature graphic, 1024×500 | Top banner on the listing |
| `screenshot_1_town.png` | 1080 × 1920 | Phone screenshot (2–8, ratio ≤ 2:1) | "Turn your steps into a cosy town" |
| `screenshot_2_insights.png` | 1080 × 1920 | " | "Track your steps, distance & calories" |
| `screenshot_3_build.png` | 1080 × 1920 | " | "Spend your steps on 22 buildings" |
| `screenshot_4_goals.png` | 1080 × 1920 | " | "Goals, streaks and milestones" |
| `screenshot_5_privacy.png` | 1080 × 1920 | " | "100% private — data never leaves your phone" |

Notes:
- **All phone screens were captured from a physical Galaxy S25 Ultra** (1440×3120), then composed
  onto a branded background with captions and scaled to 1080×1920 (16:9, within Play's 2:1 max ratio).
- The icon is the app's actual adaptive launcher icon, composited to a full 512×512 square (built from
  the icon SVG, not a device capture).
- The feature graphic is composed (text + layout) around the app's S25-rendered town/stats card and the icon.
- Recommended listing order: town hook → tracking proof → build → goals → privacy, matching the
  Health & Fitness positioning (a charming, private step tracker).

## Foreground service declaration video

`foreground_service_demo.mp4` — a 25s video for **Play Console → App content → Foreground service
permissions** (the `FOREGROUND_SERVICE_HEALTH` declaration "Video link" field). It demonstrates:
the step-tracking feature → the "Background tracking" setting (keeps counting when the app is closed)
→ the ongoing foreground-service notification shown on the home screen with the app closed → tracked
steps/distance/calories over time. Composed from real Galaxy S25 Ultra screenshots with captions.

**To use it:** upload to YouTube as **Unlisted** (or Google Drive with link sharing) and paste that
URL into the "Video link" field. Play needs a hosted URL, not a file upload.
