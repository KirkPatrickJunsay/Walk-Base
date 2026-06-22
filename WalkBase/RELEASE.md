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

**Title (≤30 chars):** `Walk Track — Step Tracker` *(include a keyword like "Step Tracker"/"Pedometer" for discovery)*
**Short description (≤80):** Private step tracker that grows a cosy town as you walk. Offline, no account.
**Full description:**
> Walk Track is a private step tracker that makes walking fun. It counts your daily steps in the background and turns them into a cosy little town that grows the more you walk — a gentle nudge to move more, with no pressure and no data harvesting.
>
> Track your walking:
> • Daily steps, distance and calories — counted on-device, even when the app is closed
> • Daily step goals, streaks and milestones to keep you moving
> • A clear day-by-day history and simple activity insights
> • Gentle reminders: near-goal, streak-at-risk and time-for-a-walk nudges
>
> Stay motivated:
> • Every 10 steps earns a Brick — spend Bricks to build and grow your own little town
> • Houses, farms, markets, gardens and more, in a living world with day/night skies, seasons, weather and townsfolk
> • Back up your town to a file you control, or share a snapshot of it
>
> 100% private: no account, no cloud, no location, no ads, no tracking. The app has no internet permission, so your steps and data never leave your phone.
>
> Health & safety: Walk Track is a fun step-tracking app, not a medical or fitness-advice tool. Step counts and estimates may be inaccurate and aren't intended to diagnose, treat or prevent any condition. Your activity choices are your own responsibility — consult a healthcare professional before changing your exercise habits, and stay aware of your surroundings while walking.

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
