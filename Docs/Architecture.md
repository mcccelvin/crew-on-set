# Project map

Tutorial navigation: `ProductionGuideLine` routes the existing tutorial/practice LineRenderers along grounded, collision-clear links around walls and props. A bounded local search refreshes while the player/target moves, with no unsafe straight-line fallback. Its depth-tested blue/amber filmstrip replaces the green beam. Elevated interaction targets have camera-facing framing marks on their actual collider/mesh bounds and a collision-checked rising cue from the standing point; ground destinations retain floor framing. A non-interactive direction HUD fades in when the next route segment is outside the horizontal field of view, guiding turns toward the walkable route rather than through obstacles. Its rounded cream paper, brown outline/text and gold arrow badge match the game's shop/HUD palette. It hides when the line is hidden, paused, or mouse controls are released. Tutorial managers retain target selection and completion/input ownership. The original serialized line references remain intact.

Computer guidance resolves the real `ComputerStation` before legacy named/eject-point targets. `TutorialGlowTarget` supplies combined live mesh bounds from the monitor, tower, keyboard and mouse, including its extra-part bindings, so raised furniture does not leave the marker beside the desk. Perspective-projected bounds fit the framing corners to the visible workstation rather than a fixed-size square; existing guide colors/glows remain unchanged. `HotbarUIManager` restyles the shared interaction prompt, including COMPUTER TERMINAL, as cream paper with brown ink/outline, a gold key badge/rule and a soft shadow. Titles/actions have separate padded rows, long actions expand the height, and entrances fade without zoom. Both generated and serialized/baked prompt references receive the style; interaction callbacks and keys are unchanged.

The serialized guide LineRenderer is now only the enable/disable control. A dedicated child ribbon owns floor-facing rotation and mesh output, preventing guide styling from rotating the tutorial manager and its product/light/camera placement-marker children.

Tutorial and guided-practice owners pass the player's `GameplayCamera` to the guide. Direction bearings and interaction framing therefore use the player's view even if it is untagged or another camera has the MainCamera tag. Starting a tutorial task restores manual look control without rotating the camera toward its target; stationary equipment exercises retain their movement restrictions.

Standing practice points use `PracticePointLock`: walk within 0.22 m of the marker's XZ center at floor height before completing arrival. The player controller aligns the body to exact center, clears horizontal momentum and temporarily freezes only XZ position; height/gravity and camera orientation remain unchanged. Resuming movement, releasing guided practice or disabling the player removes only the constraints added by this lock. Tutorial light/camera arrivals and guided-practice stations share the same center test and physics alignment; developer skips may explicitly bypass walking.

Goke's camera/light practice circles use the first tutorial's green disc, including its 0.12 m visual clearance. Their walk-to root resolves the actual raised practice-backdrop floor through its own upward-facing colliders, preserving target X/Z and excluding unrelated furniture/equipment. This keeps the camera circle visible above the grey floor instead of embedding it at the underlying stage height. Existing caption, centre-radius check, lighting targets and lesson callbacks remain unchanged.

The first tutorial's zoom and camera-height practice track one input in each direction while the viewfinder is active, then advance after 0.15 s with those controls released. There is no minimum rehearsal duration; each new practice step resets its direction history.

During the computer's ClickEditorApp step, the studio task stack docks at the canvas's top-right with safe-edge padding, away from the desktop application icons. The next task restores its normal placement; the shared measured heights, fixed gaps and non-blocking graphics remain unchanged.

Contract-folder opening: `ContractUIManager` passes the selected carousel card's displayed rect/art to `ContractFolderMotion` before hiding the offer. The cover starts at that exact pose, moves and uniformly zooms into place over 0.42 s, then unfolds around its now-fixed spine over 0.75 s. Pages and live agreement content reveal during unfolding, with no bounce, overshoot or second zoom. TAB/reference reopening retains the in-place hinge animation. Temporary artwork uses the full sprite canvas, retaining transparent margins so the animation and settled Image match without a size snap; interruptions restore exact position, scale, opacity and input. `UITransition.ShowImmediately` keeps generic entrance motion from competing. Original artwork, scrolling, signing, acceptance and tutorial callbacks retain ownership.

Notifications and tutorial task cards: `CrewPaperStyle` supplies the rounded cream paper, brown ink/outline, gold accents and soft shadow shared by `GameFeedback` and `TutorialUIManager`. Notices retain their success/caution meaning in the small accent/badge instead of changing the entire panel to green/red. Its `HintStack` measures wrapped text and compacts active studio cards from the original top-left with a fixed 12-unit gap; studio/editor cards share 12-unit vertical padding and unscaled text. Studio width/left-edge placement stay inside the parent canvas when the aspect ratio changes. Prefab X stretch is baked into studio card width, and task entrances fade without moving/scaling the stack. Completion changes ink/checkmarks without a size pulse. Editor/studio screen placement, checklist completion and input ownership stay in the existing tutorial manager. Cards do not block gameplay clicks. `GameSaveManager` keeps resume purchases/lesson state with the matching production budget; everyday profile/sync saves retain lifetime evidence without advancing that checkpoint. See `Docs/SaveGames.md`.

Shared studio dressing: `StudioSetDressing` installs `ProductModels.decorators` on every SingleStudio/MultiStudio load, independently of campaign level. It reuses saved prop placements, adds missing solid per-mesh colliders, and places missing props near clear wall-side floor positions. `Editor/StudioSetDressingSetup` assigns the six Kineme models. The same runtime setup raises all computer-desk mesh/outline parts to 1.05 m, retaining floor contact and moving the computer interaction/card-release anchor with the desktop.

Studio deliveries: `StudioSetDressing` installs `StudioDeliveryBay` after decoration.
It pairs the authored door_left/door_right meshes nearest the delivery marker,
opens them automatically for approaching players on either side, and uses a
short clear-delay before closing. The truck parks inside the existing garage,
identified by the authored Floor.026 surface on the far side of those doors.
Its floor bounds and doorway height determine parking clearance and truck scale.
The exterior opening between authored studio.043/studio.047 wall ends sets the
truck's orientation: cab outside, cargo body and pickup shelf inside the garage.
Delivery slots, showcase camera and work light follow the rotated truck.
Truck scale targets 1.8 times the base model, limited by opening width, actual
garage header clearance and the interior aisle. The hydraulic pickup shelf stays
at one metre above the authored floor independently of vehicle scale.
`StudioDeliveryBay.Access` builds one connected, walkable mesh from the garage
floor to the one-metre pickup landing and onward into the raised cargo floor.
The 3.4m approach and upper bridge leave no jump-sized step; the upper lip sits
15mm above the cargo floor to prevent coplanar flicker. Split side pickup decks
and adjacent yellow noses do not cover the central ramp or overlap each other.
Delivery slots stay beside the walkway; SD cards share the same slot counter as
other solo orders. Ramp clearance is included when fitting the parked truck.
The original floor and walls remain intact: no overlapping ground slabs, outdoor
courtyard or runtime wall cutting. The older Passage helper is no longer invoked.
`StudioDeliveryBay.Truck` owns the truck, liftgate and work light.

Equipment grounding uses `EquipmentModelVisuals.TryGetEquipmentBounds`: only active visible model meshes contribute, excluding canvases, text, haze/beam volumes and disabled replacement meshes. Shop upgrades configure their final model before delivery settlement. Returned practice lights use truck delivery slots and the shelf height directly rather than a downward ray that can catch the truck roof. Stand height measurement uses the same physical-mesh filter.

`ShopTerminal.EnsureUnlockedBetterLights` registers the advanced light's prefab/catalog entry at Contract 3+ (or training), both at shop startup and refresh/open. This is independent of tutorial-manager initialization, including skip and developer entry. It reuses the existing panel prefab plus the advanced model when no dedicated prefab is assigned; stock, prices and repeated-light purchases retain their normal behavior.

`StudioDeliveryBay.Guide` supplies bidirectional garage waypoints through both sides of the sliding doors. Delivery routes finish on the ramp's flat pickup landing; routes to indoor actors/stations descend the truck deck first, cross back into the studio, then continue to the actual target. `ProductionGuideLine` joins collision-checked floor routes for those waypoints, retaining the existing filmstrip and square target corners. Crossing guidance holds the doors open in either direction so a new indoor tutorial target does not lose the route after collection. Successful solo or multiplayer checkout requests a collection guide, which yields to tutorial-owned routes and disappears when the items are collected or a modal/pause releases gameplay controls. Pending multiplayer deliveries have a short replication grace period.

`StudioDeliveryBay.Exterior` adds an outdoor ground plane, paved loading yard, distant workshops and trees beyond the existing garage opening. It leaves the authored floor/walls intact and places a collision-only boundary across that opening: players can enter the garage/truck, but cannot walk into the scenery. Scenery has no colliders or dynamic shadows; its ground and paving have separated heights to avoid z-fighting. The same bay installation serves solo and multiplayer.
The existing Deliver transform moves to the actual scaled rear shelf, preserving
references held by lessons/shop/guides. Solo and room checkout, owned-equipment
restoration and practice-light returns share shelf positions and stable pickup
placement. Tutorial text and delivery showcase cameras follow the new location.
Room delivery positions remain host-owned; every client builds the same bay and
can open its doors when any nearby player approaches. Drop validation also
accepts the garage bounds. Purchases and prices retain their existing economy ownership.

Camera viewfinder: `CameraHUDController`, `FilmCameraItem.HUD.cs` and `Editor/CameraHUDBuilder` own the dynamic CAM FX3 display and inset live image. `Resources/CameraHUDArt.asset` holds the exported PSD artwork references. See `Docs/CameraViewfinder.md` for single-player and multiplayer bindings and prefab authoring.

Multiplayer voice: `CrewVoiceChat` owns room-only microphone capture and per-peer
playback. `CrewVoiceCodec`/`CrewVoiceBuffer` isolate packet validation and audio
thread buffering. `CrewVoiceBadge` is shared by lobby/role/pause crew cards;
`SharedOptionsPanel.Voice` extends the native Audio tab with scrollable controls
and the normal preview/Save/cancel lifecycle. See `Docs/VoiceChat.md`.

Equipment storage: `StudioSetDressing` replaces the shop-side kiosk table mesh with
`EquipmentStorageCabinet`, a steel drawer cabinet matching the truck palette.
Its drawers follow the original table's long edge, the back clears the wall-side
table edge by 8cm, and its plinth rests on the floor. There is no front lettering.
E opens a cream paper inventory with the existing blue action and red X artwork;
hotbar and stored gear have separate scrollable columns. Store/Take transfer the
same equipment instances, preserving settings and recorded SD cards without charges
or duplicate gear. Camera storage requires a closed viewfinder and an ejected card.
Tutorial action gates and local input ownership remain in effect. Equipment.Cabinet.v1
is career-scoped and follows the existing production checkpoint; restore reuses
owned-equipment instances (only saved physical SD cards require rehydration).
Multiplayer uses host-validated storeEquipment/retrieveEquipment commands and the
existing room object's stored action; contents are shared, role-checked and hidden
from world rendering, collision and active lighting. It never writes career data.

Camera holding: `FilmCameraItem.Holding.cs` positions the physical camera from player aim and exposes adjustable grip offsets. `PlayerController.LateUpdate` applies `PlayerCameraPose` arm solving after locomotion/look and before the recording lens update. The procedural pose uses the existing humanoid bones; it needs no replacement animation clips or controller. Mounted cameras and stationary interactions skip the handheld pose.

Character look: `PlayerLookPose` adds camera-relative aiming to the humanoid chest, neck and head after locomotion, with animation rotations restored before the next evaluation. Solo applies it before the handheld arm solve. MultiplayerPlayerController sends pitch and world yaw through its observed Photon stream, smooths remote aim, and lets the body ease toward horizontal view direction so the head leads a turn. Equipped meshes use the same bones, and lower-body locomotion remains independent.

Contract document presentation: `ContractBriefPresentation` supplies the same full-canvas artwork dimensions, reading-page geometry, canvas scale, typography and mask for ContractUIManager and MultiplayerAuthoredUI.Contract. Both use `ContractUIManager.MissionBrief` for the default numbered brief, including Contract 4's coffee-folder design. Multiplayer reuses the copied singleplayer qualifications hierarchy; its balance/actions sit in the scrolling footer, with no solo acceptance/signature widgets or account-currency promises.

Gameplay appearance: `PlayerController` subscribes to the confirmed account outfit from `CCoinService` and applies the shared `CharacterCosmeticRig` to its existing character visual. MultiplayerPlayerController installs `MultiplayerCharacterAppearance` on spawned players; CrewPlayerIdentity's owner-scoped Photon cosmetic properties drive remote outfits, including late arrivals and equipment updates. Both paths retain locomotion, collision and camera ownership. `FilmCameraItem.RefreshPlayerAppearanceVisibility` keeps changed outfit meshes hidden correctly in the solo viewfinder. See `Docs/PlayerProfile.md`.

First-person wall clipping: `PlayerController` caps the gameplay view's near plane at 0.025m and sphere-sweeps from the collision capsule's eye-level centre to the authored camera anchor. The probe encloses near-plane corners and leaves wall clearance, ignores triggers/player/held children, and pulls only the camera back. Scripted views with looking disabled, inactive gameplay cameras and stationary/seated actions retain their ownership; recording lenses and authored anchors are unchanged. Player Rigidbody locomotion uses continuous speculative collision detection for thin walls, retaining compatibility with kinematic chair interactions. Runtime initialization applies in Editor and standalone without rebaking a scene.

Read the row relevant to the task, then search its entry points. Paths below are relative to the project root. Most first-party gameplay code is still in `Assets/Script`; this map groups responsibilities without moving serialized assets.

| Area | Start here | Related code / detail |
| --- | --- | --- |
| Persistent career and accounts | `GameSaveManager.cs`, `GameSaveManager.SingleCareer.cs`, `GameSaveMenu.cs` | One account-scoped primary career with isolated contract replay. Private `CrewCareerProfile_v1` publishes and verifies the single-career model/primary checkpoint; old checkpoint payloads and backup files remain compatible. `GameSaveRepository.cs`, `GameSavePrefs.cs`, `LegacyGameSave.cs`, `SaveLoadPanelHost.cs`; `Docs/SaveGames.md` |
| Login / registration | `Assets/UI/Scripts/AccountManager.cs`, `LoginScreenPresentation.cs` | Login assigns identity through `GameSaveManager.SetAccount`; account sign-in button is repurposed by `SaveLoadPanelHost`. Login/recovery keep the original authored artwork/layout with readable text only. SIGN UP opens the website through `OnWebsiteSignUpPressed`, not the old registration panel. |
| Menu routes / multiplayer | `SceneController.cs`, `Multiplayer/GameSetupMenu.cs` | `Multiplayer/LobbyManager.cs`; keep Photon host/join separate from single-player save slots |
| Role-based crew studio | `Multiplayer/MultiplayerRoleManager.cs`, `Multiplayer/MultiplayerContractManager.cs`, `Multiplayer/SingleStudioMultiplayerBootstrap.cs` | Both modes load SingleStudio. Its early bootstrap replaces solo controllers/player/UI with room systems when Photon is in a room; the authored environment stays intact. Boss pages and release into gameplay are per player. Room contracts, shared money/checkpoints/reviews and role validation use `MultiplayerRoomActions`. `MultiplayerAuthoredUI` connects room station views and the functional `EditorGameplay` copy (`EditorManager.RoomFootage` + campaign `ContractGrader`); `GameSavePrefs` isolates room keys from offline careers. `Editor/MultiplayerAuthoredUIBuilder` generates/remaps/build-checks `Resources/CrewUI`; `MultiplayerUIReferences` stores bindings. `NetworkStudioFactory`/`NetworkStageObject` replicate equipment, actors and sets; `MultiplayerRecording` transfers/caches rushes and maps their 6 fps clock to the editor's 24 fps clock. See `Docs/MultiplayerRoles.md`. |
| Settings / pause | `PauseManager.cs`, `SharedOptionsPanel.cs` | `MainMenuOptionsHost.cs` attaches the same view in the main menu; `GameOptions.cs` applies global settings |
| Campaign / rewards | `CampaignLevelManager.cs`, `CampaignProgression.cs`, `CareerManager.cs` | `ProductionEconomy.cs`, `ContractGrader.cs`; `Docs/ProductionEconomy.md` |
| Account C-Coins / cosmetics | `CCoinService.cs`, `CCoinShopUI.cs`, `CCoinPackShopUI.cs`, `CCoinPackArt.cs`, `CCoinProfileWidget.cs` | `CCoinModels.cs`, `CCoinSettings.cs`; `GameSaveManager` binds login and schedules reconnect/focus refresh across account services. `CCoinService.Update` ticks profile/appearance retries and periodic authoritative wallet refresh. TOP UP opens the fixed approved website shop; no manual sync/default-look controls. Successful submissions queue rewards via `GradeManager`. Pack display offers are not client-authorized charges. Separate from B-Coins and career rollbacks. Server wallet/payment deployment is required; see `Docs/CCoins.md`. |
| First commercial tutorial | `TutorialManager.cs` | `TutorialUIManager.cs` owns boss dialogue, hints and interaction restrictions |
| Goke / level 2 | `GokeLevelManager.cs` | `GokeCommercialCards.cs`; shared editor tutorial also participates |
| Terrari / level 3 | `Level3Manager.cs`, `LamborminiEditLesson.cs` | `LamborminiBrief.cs`, `LamborminiShowroom.cs`, `LamborminiVehicleVisual.cs`; `Docs/AutomotiveSimulation.md` |
| Level 4 / actors | `CampaignLevelManager.cs`, `ActorBot.cs` | `Docs/Level4Tutorial.md`, `Docs/ActorBots.md` |
| Stage / director tablet | `DirectorTerminal.cs`, `UIDragProp.cs` | `StageInterior.cs`, `ImportedProductVisual.cs`; `Docs/DirectorTablet.md`, `Docs/StageInteriors.md`, `Docs/ProductModels.md` |
| Equipment shop / delivery | `ShopManager.cs`, `ShopTerminal.cs`, `EquipmentInteractor.cs`, `ActorMegaphoneItem.cs` | `ProductionKit.cs`, `ProductionKitShop.cs`, `ProductionEconomy.cs` |
| Lighting / grip | `FilmLightItem.cs`, `StageLightStrip.cs`, `StudioLightHaze.cs` | `ProductionLightModifiers.cs`, `ProductionExposureMonitor.cs`, `AutomotiveGrip.cs` |
| Camera / SD / recordings | `FilmCameraItem.cs`, `FilmCameraItem.Settings.cs`, `CameraFeatureUnlocks.cs`, `SDCardItem.cs`, `SDCardStorage.cs`, `TruePixelRecorder.cs`, `ComputerStation.cs`, `ComputerUIManager.cs`, `ComputerUIManager.Files.cs`, `ClipUIItem.cs`, `ComputerWindowMotion.cs` | One career camera: L1 autofocus, L2 manual focus/thirds, L3 white balance, L4 exposure. Reusable single-player SD cards hold multiple takes totaling 60s; C inserts/ejects the same physical card. Computer ingestion keeps per-take evidence; rename/delete validates registered media, updates references and frees capacity. Tape sidecars preserve local metadata, not unfinished-career/cloud media saves. Computer panels use continuous window motion and bounded filenames. F2 settings, arrows adjust, brackets pull focus. Legacy camera IDs and single-take cards remain compatible. Multiplayer recording retains its separate existing workflow |
| Editing / playback | `EditorManager.cs`, `TruePixelPlayer.cs`, `CommercialCompiler.cs` | `VideoFrameCompositor.cs` embeds placed branding into playback/review pixels; `EditorTutorialManager` supports Back/revise/re-export. `DraggableClip.cs`, `ClipInspector.cs`, `ProjectDataManager.cs`; `Docs/EditorReview.md` |
| Branding / color | `BrandingBinManager.cs`, `DraggableOverlay.cs`, `BrandingClip.cs` | `ColorGradingManager.cs`, `ContractGrader.cs`; timing must agree with playback and grading |
| Client results / feedback papers | `FinalGradePanelUI.cs`, `FeedbackPaperUI.cs`, `FeedbackReport.cs` | `GradeManager` still owns exactly-once career rewards/progression; `PlayerAnalytics` supplies spending. Five animated PSD papers with Boss coaching, scrolling, Next/Back and a saved-result stamp. `MultiplayerAuthoredUI` uses the same view with room budget and host-only advance/retry. See `Docs/FeedbackReview.md`. |
| Gameplay HUD | `CareerManager.ConfigureGameplayHUD`, `HotbarUIManager` | Almanac icon at top left (P); profile icon above coins at top right (I). Dark slots with selected outline. Artwork keys: `almanacHud`, `profileIcon`. |
| Contract / Almanac UI | `ContractUIManager.cs`, `AlmanacManager.cs`, `AlmanacBook.cs`, `AlmanacFieldNotes.cs`, `AlmanacProfile.cs` | `ExportUIArt.cs`, `Assets/Resources/ExportUIArt.asset`; `Docs/UIExportIntegration.md` |
| Shared account profile / career statistics / achievements | `AccountProfileData.cs`, `AccountAppearanceData.cs`, `AccountProfileMenuHost.cs`, `AlmanacProfileAccount.cs`, `AlmanacProfileShop.cs`, `ProfileShopGrid.cs`, `CareerProfileProgress.cs`, `AlmanacProfileProgress.cs`, `ProfileSkillsChart.cs` | Main-menu Account and in-game Profile share Name/Bio/ID and original Profile/Stats/Shop artwork. Shop cosmetics use boxed cards in the game's cream/brown/blue palette inside the original stage, retaining existing preview/equip/purchase callbacks. Identity and equipped appearance preferences are per-account; private `character_appearance` sync is separate from wallet ownership and excludes try-on/test equipment. Manual profile sync controls are retired. Saved takes, transactions and final results feed career-scoped totals via `PlayerAnalytics`. Menu career viewing is read-only. Native radar values use retained grades; Sound is explicitly ungraded. Existing achievements and C-Coin frame catalog remain supported. See `Docs/PlayerProfile.md`. |
| Player controls / testing cheats | `InputManager.cs`, `DevCommandsPanel.cs`, `DevTutorialBypass.cs` | F12 button panel replaces old cheat keys; Editor/Development builds only. Test C-Coins are session-only in `CCoinService`. See `Docs/DeveloperCommands.md`. Also inspect `Assets/Player`; tutorials own movement restrictions |

Unless a full path is given, filenames above are under `Assets/Script`.

## Runtime boundaries

- `PlayerAnalytics` stores categorized transactions and up to 50 contract attempts through career-scoped `GameSavePrefs`. Career/shop/tablet events record spending; camera ejection counts takes; `ContractGrader` captures camera/light scores and `GradeManager` finalizes results once. Results show budget coaching without changing technical grades. Almanac Techniques includes YOUR CAREER ANALYTICS (10 latest attempts). Existing mid-contract saves are marked partially tracked; legacy import/reset includes the analytics key. Shared multiplayer room budgets are outside this single-player career report.

- `GameSaveManager` persists across scenes, selects the active career and coordinates local/cloud saves. `SaveCheckpoint` preserves the matching resume budget, ownership and lesson/contract state during an unfinished attempt, while retaining lifetime evidence and reward identity. `SaveBudgetCheckpoint` advances that complete production state at grants/acceptance/completion/retry/reset boundaries. `GameSaveRepository` handles stored records; `GameSavePrefs` routes career preferences. UI consumes these services.
- `GameSaveMenu` draws the PSD-based save grid and dialogs. `SaveLoadPanelHost` connects the authored LOAD tab and existing account button. `GameSetupMenu` handles Single/Multi creation.
- `PauseManager` owns pausing and cursor restoration. `SharedOptionsPanel` owns settings drafts and UI. `GameOptions` owns applying settings; `MainMenuOptionsHost` owns the main-menu entry point.
- `ContractUIManager` owns the open contract's modal presentation (authored and runtime canvas order 200, above the gameplay HUD and below Pause/Options). Resume reapplies that presentation immediately and after the focus-restoration frame without reopening the offer, resetting the scroll/signature, or recapturing movement permissions. While open, its LateUpdate retains the free cursor and gameplay lock; editor reference briefs leave the editor's player ownership unchanged.
- Tutorial/level managers orchestrate steps; equipment and editor components perform actions; grading checks the result. `ContractTutorialChoice` asks PLAY/SKIP through the Boss for each solo contract attempt; its in-memory policy survives editor visits and clears on retries/new games. Skip affects tutorial guidance, not completion evidence or rewards. See `Docs/TutorialChoice.md`. When changing a rule, check its tutorial instructions and grader together.
- `ExportUIArt` resolves catalog keys to sprites. Artwork lives in `Assets/UI/UI-EXPORT`, including `PSD Pieces`. Preserve authored art rather than adding a second UI on top.

## Authored UI

Settings audio controls are upgraded in-place by `SharedOptionsPanel` for existing authored layouts. `GameOptions` owns global SFX/music preferences and maps the three visible quality presets to existing Unity tier indices. SAVE applies all drafts; closing discards them. Gameplay, coffee actions, and commercial playback multiply their base volumes by the matching category setting. Multiplayer pause opens the same options panel.

Gameplay audio: `GameplayAudioManager` installs automatically and owns UI/feedback cues, boss voice and contract background music. `GameplaySoundLibraryBuilder` generates `Resources/GameplaySounds.asset` from the original sound folders on editor reload and before builds. Recording and movement trigger cues directly; `CoffeeActionAudio` owns cancellable spatial machine sequences for players and actors. Master volume still comes from `GameOptions`. Imported sounds without a corresponding gameplay action are retained in the catalog for future hooks; this does not add audio capture to recorded commercials.

Static UI is saved in the scenes and equipment prefab rather than rebuilt on Play. `Assets/Editor/AuthoredUIMigration.cs` owns the one-time migration (`Crew-On-Set/UI/Save UI into Scene Hierarchy`) and backs up scenes under `Logs/AuthoredUI`. Root screen UI is grouped under `UI`; world-space screens and internal lookup paths remain in place. UI controllers serialize layout references and bind runtime listeners independently. `SettingsLayout`, `PlayerEditTools`, `AlmanacGuidePlayer`, and `CircularSliderKnob` have matching script files so Unity can serialize their components.

Clip cards, timeline ticks, save cards, achievements, and prop-bank entries are data-driven and still populate dynamically. Do not globally disable Instantiate or remove UI controllers. Existing builders remain as compatibility fallbacks for unmigrated scenes. Keep the scene/prefab references when editing layouts; repeat migration only when deliberately adding missing layouts.

## Scene flow and serialization

`LoadingScreenController` owns the persistent loading overlay and asynchronous scene transitions. Scene entry points retain its `LoadScene` overloads; multiplayer hosts retain `LoadNetworkScene` and Photon synchronization, while joining clients call `ShowNetworkLoading`. The current overlay uses `Resources/LoadingLogo`, the original logo, gameplay tips and a smoothed progress bar. `BlueLoadingIris` closes a centered transparent circle over that screen using the loading bar's cyan-blue after progress completes. Local activation waits until it is fully blue; synchronized Photon activation remains Photon-owned. Once the destination is ready, the original loading backdrop is hidden and the full blue cover fades out over 0.45 seconds to reveal it. See `Docs/LoadingScreen.md`. The existing loading-frame atlas/export tool remains unchanged.

The main scenes live in `Assets/Scenes`: `Main Menu`, `Account`, `Login`, `CutScene`, `SingleStudio`, `MultiStudio`, `Editor`, and `ReviewScene`. Confirm enabled build order in `ProjectSettings/EditorBuildSettings.asset` before editing integer scene routes.

Unity scenes and prefabs reference scripts by `.meta` GUID. Public methods can be called through serialized UnityEvents even when C# has no callers. Some secondary components are added at runtime by type; do not assume every component appears in scene YAML.

## Refactoring strategy

Keep scene-facing components and saved data stable. Extract one responsibility at a time, retain original APIs, and validate that feature before continuing. The settings/save UI cleanup removed unreachable legacy builders and separated existing classes into matching files; it did not change scene assets, contracts, economy, saved data or SDKs. Larger tutorial managers remain intact and should be split only alongside focused tests for their step transitions.

No exact token saving is guaranteed: the primary benefit is locating and reading fewer relevant files. Do not paste this whole map into every task.

Sound editing: use Crew-On-Set > Edit Gameplay Sounds, or select Assets/Resources/GameplaySounds.asset. GameplaySoundLibraryEditor displays searchable named actions with editable clip variations. Display names may change freely; action keys preserve existing gameplay hooks. Empty Clips mute that action. Add Sound creates a custom entry; a new gameplay event must call GameplayAudioManager.Play with its key. Extra entries are imported assets with no built-in action mapping. The builder initializes named entries once and thereafter updates only the hidden discovery cache, preserving custom assignments through editor reloads and builds.

