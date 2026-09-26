# Project map

Camera viewfinder: `CameraHUDController`, `FilmCameraItem.HUD.cs` and `Editor/CameraHUDBuilder` own the dynamic CAM FX3 display and inset live image. `Resources/CameraHUDArt.asset` holds the exported PSD artwork references. See `Docs/CameraViewfinder.md` for single-player and multiplayer bindings and prefab authoring.

Camera holding: `FilmCameraItem.Holding.cs` positions the physical camera from player aim and exposes adjustable grip offsets. `PlayerController.LateUpdate` applies `PlayerCameraPose` arm solving after locomotion/look and before the recording lens update. The procedural pose uses the existing humanoid bones; it needs no replacement animation clips or controller. Mounted cameras and stationary interactions skip the handheld pose.

Read the row relevant to the task, then search its entry points. Paths below are relative to the project root. Most first-party gameplay code is still in `Assets/Script`; this map groups responsibilities without moving serialized assets.

| Area | Start here | Related code / detail |
| --- | --- | --- |
| Save slots and accounts | `GameSaveManager.cs`, `GameSaveMenu.cs` | `GameSaveRepository.cs`, `GameSavePrefs.cs`, `LegacyGameSave.cs`, `SaveLoadPanelHost.cs`; `Docs/SaveGames.md` |
| Login / registration | `Assets/UI/Scripts/AccountManager.cs` | Login assigns identity through `GameSaveManager.SetAccount`; account sign-in button is repurposed by `SaveLoadPanelHost` |
| Menu routes / multiplayer | `SceneController.cs`, `Multiplayer/GameSetupMenu.cs` | `Multiplayer/LobbyManager.cs`; keep Photon host/join separate from single-player save slots |
| Role-based crew studio | `Multiplayer/MultiplayerRoleManager.cs`, `Multiplayer/MultiplayerContractManager.cs` | `MultiplayerAuthoredUI` (Stations/Editor partials) connects copied singleplayer views to physical stations; `MultiplayerRoomActions` validates role commands. `Editor/MultiplayerAuthoredUIBuilder` generates `Resources/CrewUI` views/models, `MultiplayerUIReferences` stores bindings. `NetworkStudioFactory`/`NetworkStageObject` replicate equipment, actors and sets; `MultiplayerRecording` transfers rushes. See `Docs/MultiplayerRoles.md`. |
| Settings / pause | `PauseManager.cs`, `SharedOptionsPanel.cs` | `MainMenuOptionsHost.cs` attaches the same view in the main menu; `GameOptions.cs` applies global settings |
| Campaign / rewards | `CampaignLevelManager.cs`, `CampaignProgression.cs`, `CareerManager.cs` | `ProductionEconomy.cs`, `ContractGrader.cs`; `Docs/ProductionEconomy.md` |
| First commercial tutorial | `TutorialManager.cs` | `TutorialUIManager.cs` owns boss dialogue, hints and interaction restrictions |
| Goke / level 2 | `GokeLevelManager.cs` | `GokeCommercialCards.cs`; shared editor tutorial also participates |
| Terrari / level 3 | `Level3Manager.cs`, `LamborminiEditLesson.cs` | `LamborminiBrief.cs`, `LamborminiShowroom.cs`, `LamborminiVehicleVisual.cs`; `Docs/AutomotiveSimulation.md` |
| Level 4 / actors | `CampaignLevelManager.cs`, `ActorBot.cs` | `Docs/Level4Tutorial.md`, `Docs/ActorBots.md` |
| Stage / director tablet | `DirectorTerminal.cs`, `UIDragProp.cs` | `StageInterior.cs`, `ImportedProductVisual.cs`; `Docs/DirectorTablet.md`, `Docs/StageInteriors.md`, `Docs/ProductModels.md` |
| Equipment shop / delivery | `ShopManager.cs`, `ShopTerminal.cs`, `EquipmentInteractor.cs`, `ActorMegaphoneItem.cs` | `ProductionKit.cs`, `ProductionKitShop.cs`, `ProductionEconomy.cs` |
| Lighting / grip | `FilmLightItem.cs`, `StageLightStrip.cs`, `StudioLightHaze.cs` | `ProductionLightModifiers.cs`, `ProductionExposureMonitor.cs`, `AutomotiveGrip.cs` |
| Camera / SD / recordings | `FilmCameraItem.cs`, `FilmCameraItem.Settings.cs`, `CameraFeatureUnlocks.cs`, `ComputerStation.cs`, `ComputerUIManager.cs` | One career camera: L1 autofocus, L2 manual focus/thirds, L3 white balance, L4 exposure. F2 settings, arrows adjust, brackets pull focus. Settings use GameSavePrefs; camera-only URP overrides also affect recorded footage. Legacy camera IDs remain compatible; shop restores the base camera instead of selling upgrades. Follow recording and inventory callers before changing media transfer |
| Editing / playback | `EditorManager.cs`, `TruePixelPlayer.cs`, `CommercialCompiler.cs` | `EditorTutorialManager.cs`, `DraggableClip.cs`, `ClipInspector.cs`, `ProjectDataManager.cs` |
| Branding / color | `BrandingBinManager.cs`, `DraggableOverlay.cs`, `BrandingClip.cs` | `ColorGradingManager.cs`, `ContractGrader.cs`; timing must agree with playback and grading |
| Gameplay HUD | `CareerManager.ConfigureGameplayHUD`, `HotbarUIManager` | Almanac PSD book icon above balance; P shortcut; dark slots with selected outline. Artwork key: `almanacHud`. |
| Contract / Almanac UI | `ContractUIManager.cs`, `AlmanacManager.cs`, `AlmanacBook.cs` | `ExportUIArt.cs`, `Assets/Resources/ExportUIArt.asset`; `Docs/UIExportIntegration.md` |
| Player controls / testing cheats | `InputManager.cs`, `DevTutorialBypass.cs` | Also inspect `Assets/Player`; tutorials own movement restrictions |

Unless a full path is given, filenames above are under `Assets/Script`.

## Runtime boundaries

- `GameSaveManager` persists across scenes, selects the active career and coordinates local/cloud saves. `GameSaveRepository` handles stored records; `GameSavePrefs` routes career preferences. UI consumes these services.
- `GameSaveMenu` draws the PSD-based save grid and dialogs. `SaveLoadPanelHost` connects the authored LOAD tab and existing account button. `GameSetupMenu` handles Single/Multi creation.
- `PauseManager` owns pausing and cursor restoration. `SharedOptionsPanel` owns settings drafts and UI. `GameOptions` owns applying settings; `MainMenuOptionsHost` owns the main-menu entry point.
- Tutorial/level managers orchestrate steps; equipment and editor components perform actions; grading checks the result. When changing a rule, check its tutorial instructions and grader together.
- `ExportUIArt` resolves catalog keys to sprites. Artwork lives in `Assets/UI/UI-EXPORT`, including `PSD Pieces`. Preserve authored art rather than adding a second UI on top.

## Authored UI

Settings audio controls are upgraded in-place by `SharedOptionsPanel` for existing authored layouts. `GameOptions` owns global SFX/music preferences and maps the three visible quality presets to existing Unity tier indices. SAVE applies all drafts; closing discards them. Gameplay, coffee actions, and commercial playback multiply their base volumes by the matching category setting. Multiplayer pause opens the same options panel.

Gameplay audio: `GameplayAudioManager` installs automatically and owns UI/feedback cues, boss voice and contract background music. `GameplaySoundLibraryBuilder` generates `Resources/GameplaySounds.asset` from the original sound folders on editor reload and before builds. Recording and movement trigger cues directly; `CoffeeActionAudio` owns cancellable spatial machine sequences for players and actors. Master volume still comes from `GameOptions`. Imported sounds without a corresponding gameplay action are retained in the catalog for future hooks; this does not add audio capture to recorded commercials.

Static UI is saved in the scenes and equipment prefab rather than rebuilt on Play. `Assets/Editor/AuthoredUIMigration.cs` owns the one-time migration (`Crew-On-Set/UI/Save UI into Scene Hierarchy`) and backs up scenes under `Logs/AuthoredUI`. Root screen UI is grouped under `UI`; world-space screens and internal lookup paths remain in place. UI controllers serialize layout references and bind runtime listeners independently. `SettingsLayout`, `PlayerEditTools`, `AlmanacGuidePlayer`, and `CircularSliderKnob` have matching script files so Unity can serialize their components.

Clip cards, timeline ticks, save cards, achievements, and prop-bank entries are data-driven and still populate dynamically. Do not globally disable Instantiate or remove UI controllers. Existing builders remain as compatibility fallbacks for unmigrated scenes. Keep the scene/prefab references when editing layouts; repeat migration only when deliberately adding missing layouts.

## Scene flow and serialization

The main scenes live in `Assets/Scenes`: `Main Menu`, `Account`, `Login`, `CutScene`, `SingleStudio`, `MultiStudio`, `Editor`, and `ReviewScene`. Confirm enabled build order in `ProjectSettings/EditorBuildSettings.asset` before editing integer scene routes.

Unity scenes and prefabs reference scripts by `.meta` GUID. Public methods can be called through serialized UnityEvents even when C# has no callers. Some secondary components are added at runtime by type; do not assume every component appears in scene YAML.

## Refactoring strategy

Keep scene-facing components and saved data stable. Extract one responsibility at a time, retain original APIs, and validate that feature before continuing. The settings/save UI cleanup removed unreachable legacy builders and separated existing classes into matching files; it did not change scene assets, contracts, economy, saved data or SDKs. Larger tutorial managers remain intact and should be split only alongside focused tests for their step transitions.

No exact token saving is guaranteed: the primary benefit is locating and reading fewer relevant files. Do not paste this whole map into every task.

Sound editing: use Crew-On-Set > Edit Gameplay Sounds, or select Assets/Resources/GameplaySounds.asset. GameplaySoundLibraryEditor displays searchable named actions with editable clip variations. Display names may change freely; action keys preserve existing gameplay hooks. Empty Clips mute that action. Add Sound creates a custom entry; a new gameplay event must call GameplayAudioManager.Play with its key. Extra entries are imported assets with no built-in action mapping. The builder initializes named entries once and thereafter updates only the hidden discovery cache, preserving custom assignments through editor reloads and builds.

