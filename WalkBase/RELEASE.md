# Walk Track — Play Store Release Guide

App id: `com.codesandchips.walktrack` · Version: `1.0` (versionCode `1`) — set in `WalkBase.csproj`
(`ApplicationDisplayVersion` / `ApplicationVersion`). Bump both for each store update.

## 1. Build a signed release AAB

Do **not** commit the keystore or passwords. Generate a keystore once and keep it safe:

```bash
keytool -genkeypair -v -keystore walktrack.keystore -alias walktrack \
  -keyalg RSA -keysize 2048 -validity 10000
```

Then build the signed App Bundle (pass secrets at build time / via CI env vars):

```bash
dotnet publish WalkBase.csproj -f net10.0-android -c Release \
  -p:AndroidPackageFormat=aab \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore=$KEYSTORE_PATH \
  -p:AndroidSigningKeyAlias=walktrack \
  -p:AndroidSigningKeyPass=$KEY_PASS \
  -p:AndroidSigningStorePass=$STORE_PASS
```

Output: `bin/Release/net10.0-android/publish/com.codesandchips.walktrack-Signed.aab`.
Upload that to the Play Console. (Recommended: enroll in Play App Signing.)

## 2. Store listing copy

**Positioning:** publish as an **App** (not a Game). **Category: Health & Fitness.**
Walking is the product; the town is the motivator. Lead the listing with step tracking, then the
cosy reward loop. (Category can be changed later if needed — see §9.)

**Title:** `Walk Track`
*(The title has no keyword, so the short/full description and the Play keyword fields carry discovery —
lead with "step tracker", "step counter", "pedometer", "walking".)*

**Short description (≤80, this is 76):** Private step tracker that grows a cosy town as you walk — offline, no account.

**Full description:**
> Walk Track is a private step tracker that makes walking fun. It counts your daily steps in the background and turns them into a cosy little town that grows the more you walk — a gentle, no-pressure nudge to move more, with no accounts and no data harvesting.
>
> Walking is the point. The town is your reward.
>
> 🚶 TRACK YOUR WALKING
> • Counts your daily steps automatically, even when the app is closed
> • See your walking distance and estimated calories at a glance
> • Set a daily step goal and build streaks to stay consistent
> • A clear day-by-day history and simple activity insights
> • Gentle reminders — near your goal, when a streak's at risk, or just time for a walk
>
> 🏘️ GROW YOUR TOWN
> • Every 10 steps earns a Brick — spend Bricks to place and upgrade buildings
> • Houses, farms, markets, gardens, a chapel, a fountain and more
> • A living world: day and night skies, four seasons, weather, festivals, townsfolk and pets
> • Watch your town come alive as your step count climbs
>
> 🔒 PRIVATE BY DESIGN
> Walk Track has no internet access at all, so your data physically cannot leave your phone.
> • No account, no sign-up
> • No cloud, no servers
> • No location or GPS
> • No ads, no analytics, no tracking
> Your steps, your town and your stats stay on your device. Back them up to a file you control, or delete everything just by uninstalling.
>
> 📤 MAKE IT YOURS
> • Name your town
> • Share a snapshot of your town and walking stats whenever you like
> • Light on battery and storage; works completely offline
>
> Whether you want a simple step counter, a friendly pedometer, or just a reason to take a few more steps each day, Walk Track turns your walking into something to look forward to.
>
> Health & safety: Walk Track is a fun step-tracking app, not a medical or fitness-advice tool. Step counts, distance and calories are estimates from your phone's sensor and may be inaccurate, and aren't intended to diagnose, treat or prevent any condition. Decisions about your activity are your own responsibility — consider talking to a qualified healthcare professional before starting or changing an exercise routine, and stay aware of your surroundings while you walk.
>
> Requires a device with a step-counter sensor.

## 3. Privacy policy URL (required)

Play requires a publicly reachable privacy-policy URL. It is **already hosted** via GitHub Pages:

> **https://kirkpatrickjunsay.github.io/Walk-Base/privacy.html**

Paste that URL into **Play Console → App content → Privacy policy** *and* the store listing.
The page source is [`docs/privacy.html`](../docs/privacy.html) (edit + push to update it; the markdown
copy is [`PRIVACY_POLICY.md`](PRIVACY_POLICY.md)). The same text is also shown in-app at
*Settings → About → Privacy* (no network needed).

## 4. Data safety form (Play Console → App content → Data safety)

Answer the questionnaire exactly as follows — the app collects and shares **nothing**:

- **Does your app collect or share any of the required user data types?** → **No.**
  (The app has no `INTERNET` permission, so no data can be transmitted off-device.)
- **Is all of the user data encrypted in transit?** → N/A (no data leaves the device).
- **Do you provide a way for users to request that their data is deleted?** → Yes —
  uninstalling the app deletes all data; users can also self-export a backup file.
- **Location:** Not collected, not used. No GPS.
- **Health & fitness (step count):** Read **on-device only** for gameplay; **not**
  "collected" in Play's sense because it is never sent off the device or shared.

> Note: "collect" in the Data Safety form means *transmitted off the device*. On-device-only
> processing (our step count, town save, height/weight) is **not** collection — declare **No**.

## 5. Foreground service declaration (Play Console → App content → Foreground service)

Required because the app uses a `health`-typed foreground service for background step
counting (targetSdk 34+). The manifest is already correct:
`<service android:foregroundServiceType="health">` plus `FOREGROUND_SERVICE` and
`FOREGROUND_SERVICE_HEALTH` permissions.

- **Which foreground service types does your app use?** → **Health.**
- **Justification (paste in the form):**
  > Walk Track counts the user's steps to award in-game currency. With "background
  > tracking" enabled, a Health foreground service keeps reading the device step-counter
  > sensor while the app is not in the foreground, so steps taken with the phone pocketed
  > are still counted. It shows an ongoing notification and performs no networking.
- **Provide a short screen recording** showing: enabling background tracking in Settings,
  backgrounding the app, walking, and the count having advanced on return (plus the
  ongoing notification). Record on the physical device with the real sensor.

## 6. Permissions rationale (for review notes)

The app requests a single data permission, **`ACTIVITY_RECOGNITION`**, and shows an
in-app rationale *before* the system prompt: the first-run onboarding ends on a
"Private & permission" slide explaining why step access is needed, and only then does
"Enable step tracking" trigger the OS dialog. Other permissions (`POST_NOTIFICATIONS`,
`FOREGROUND_SERVICE`/`_HEALTH`, `VIBRATE`) are non-data and support background counting
and haptics. No `INTERNET` / `ACCESS_NETWORK_STATE`.

## 7. Content rating

Everyone / PEGI 3 — no objectionable content, no user-generated content, no ads, no purchases.

## 8. Content rating

Everyone / PEGI 3 — no objectionable content, no user-generated content, no ads, no purchases.

## 9. Pre-launch checklist

- [ ] Bump `ApplicationDisplayVersion` + `ApplicationVersion` for the build.
- [ ] Confirm `AndroidManifest.xml` still omits `INTERNET` / `ACCESS_NETWORK_STATE`.
- [ ] App icon renders crisply (`Resources/AppIcon/appicon.svg`).
- [ ] Capture phone screenshots: Town (day + night), Build tab, a festival, Goals, Insights.
- [ ] Feature graphic (1024×500) + adaptive icon.
- [ ] **Host [`PRIVACY_POLICY.md`](PRIVACY_POLICY.md) and set its URL** in Play Console + listing.
- [ ] **Complete the Data Safety form** ("No" to data collection — see §4).
- [ ] **Submit the Foreground service declaration** (Health) with the justification + screen recording (§5).
- [ ] Verify the first-run onboarding shows the permission rationale and the OS prompt fires on "Enable step tracking" (test on a device with a real step sensor).
- [ ] Test the signed AAB on a physical device (real step sensor) before rollout.
