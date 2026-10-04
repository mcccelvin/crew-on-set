# Player profile, achievements and statistics

The main-menu Account screen and the in-game Profile HUD button (**I**) open the same profile view. It uses the original curtain/stage backdrop, girl character preview, paper panels, title signs and three icon tabs: **Profile**, **Stats** and **Shop**. Achievements open from the Stats footer and return to Stats without closing the profile. Back/Close return to the originating menu or gameplay using the existing input-state handling. All pages share the screen-scaling canvas (1920×1080 reference, Expand). Stats and achievements use bundled Kalam/Allura pen fonts, opaque paper cards and visible scrollbars. The portrait snapshots the bundled stage idle pose, corrects imported rig/skin scaling, and uses scale-independent lighting so the mesh is neither clipped nor a black silhouette.

## Shared account details and shop

Name and Bio are editable on the Profile paper; SAVE preserves them and the account ID remains read-only. `AccountProfileData` caches identity in global, per-account `SaveSystem.Profile.v1.<accountId>` preferences, separate from career checkpoints and budget retries. Authenticated saves update the existing PlayFab title display name and private `ProfileBio` user data. Guest/offline edits remain local with an explicit pending status; a late reply from an old account or an earlier read cannot overwrite a newly saved edit. Account switching discards the previous account's draft. No live backend deployment or payment changes are introduced here.

`AccountProfileMenuHost` intercepts the existing Account scene/main-menu Account Profile entry, disabling its old display canvas while the shared view is open. It does not require changing existing menu button events. Main-menu Stats show the most recently saved non-deleted career, with SWITCH CAREER for other saved careers. This is a read-only view: opening or switching this page does not activate a career or alter its preferences. In-game Stats use the currently active career.

The Shop paper has Hat, Shirt, Pants, Shoes and Frames tabs. Only profile frames are supported by the current server catalog protocol. Clothing tabs explain that no supported items are available, rather than inventing items or charging coins. Frames use the existing authoritative C-Coin wallet, purchase/equip rules and sync status. DEFAULT LOOK removes the equipped frame; it does not change ownership. See `Docs/CCoins.md` for the undeployed backend/payment requirements.

## Ownership and persistence

- `AlmanacProfile.cs` owns the original profile/portrait. `AlmanacProfileProgress.cs` upgrades existing authored profiles without duplicating panels, binds the Stats tab, refreshes stats and achievement cards, and preserves the existing serialized/public profile entry points. `AlmanacProfileAccount.cs` shares account inputs, menu career selection and the shop paper between both entry points; `ProfileSkillsChart` draws the live native UI radar chart.
- `CareerProfileProgress.cs` stores fixed-size lifetime statistics in career-scoped `Profile.Career.v1` through `GameSavePrefs`. It never caches a different account/career's record. Existing checkpoint/cloud transport carries this value with the selected save; no new wallet or payment APIs are used.
- `PlayerAnalytics.Begin` initializes tracking before a new attempt. Actual saved camera takes pass duration from `FilmCameraItem.EjectUsedSDCard`. Actual successful transactions update B-Coin income/spending (developer funds excluded). `PlayerAnalytics.Complete` records the final grades inside its existing exactly-once completion guard. Previewing or reopening feedback does not award progress.
- Counts include saved takes/duration, graded attempts, passed/failed attempts, five distinct completed clients, best/average overall score, best letter grade, income/spending, per-client best scores and the latest five submissions. Best numeric score and passing status are separate: an F with 99/100 remains failed. Lifetime totals do not shrink when the analytics history trims to its 50-result limit.
- Ordinary contract/budget retries retain lifetime stats and achievement keys while restoring contract budget/purchases. A new career starts empty; F12 resets only its selected career. `LegacyGameSave` imports/resets the profile key and achievement keys for direct Editor testing too.
- Room preference writes remain separate from the solo career. This feature does not introduce persistent multiplayer/account-wide achievements or paid rewards.

The radar chart replaces the artwork's static example values. Director uses the retained submissions' average pre-production score, Camera normalizes its /70 grade to /100, Lights normalizes /30 to /100, and Editor uses post-production. Sound is explicitly ungraded and displays a dash rather than a fabricated score. The chart averages the retained analytics history (up to 50 submissions); lifetime counters above it remain fixed-size career totals. Empty or older saves do not invent missing grades.

## Achievements

16 built-in achievements cover saved recordings (1, 10, 25), first successful commercial, each of the five clients, all five clients, S grade, post-production ≥95, all three departments ≥90, a pass after a failed attempt at the same brief, an unassisted fully tracked first-attempt pass, and a fully tracked unassisted pass retaining ≥1,000 B-Coins without rejected purchases or untracked balance changes. Quality milestones require a passing final letter grade, including mandatory contract requirements.

Progress mirrors the existing `AchivProg_career_*` / `AchivDone_career_*` keys. Each achievement unlocks once, with a non-blocking notification; it does not grant B-Coins, C-Coins or cosmetic items. Existing custom serialized achievements and `AddAchievementProgress` remain supported; arbitrary increments cannot unlock the built-in gameplay achievements.

## Older saves

First access imports completion flags, stored per-client best scores and retained analytics results. It does not invent missing attempts, grades or recording durations. The Stats tab explains partial totals; a `+` after recorded duration means earlier take durations were not saved. First-attempt awards are withheld for clients whose earlier attempts may be missing, but new fully tracked clients can still earn them. Legacy unlocks are restored silently.

## Verification

Compile against actual Unity Editor and actual standalone-player references. In an isolated Unity project, exercise unchanged progress/analytics/preferences/profile partials using native JsonUtility, UI, TMP and bundled fonts, with unrelated scene, save-checkpoint, PlayFab and notification services replaced by memory fixtures. Test failed-high-score gates, repeated completion, retries, independent saves, reload/reset, room isolation, legacy/corrupt data, money/assistance gates, lifetime counters beyond 50 results, account-specific identity, pending edits and stale cloud callbacks. Check both profile entry points and read-only menu career switching. Render Profile/Stats/Achievements/Shop at 1920×1080, 1280×1024 and 2560×1080, inspect text visibility and reuse of authored UI. This is not a full game build or a live PlayFab sync test.

Include the original script `.meta` files in the isolated project and validate their GUIDs as exactly 32 hexadecimal characters before compiling. A source-only compiler check cannot detect Unity skipping a script because its asset metadata is invalid.
