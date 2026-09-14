# Project map

Read the row relevant to the task, then search its entry points. Paths below are relative to the project root. Most first-party gameplay code is still in `Assets/Script`; this map groups responsibilities without moving serialized assets.

| Area | Start here | Related code / detail |
| --- | --- | --- |
| Save slots and accounts | `GameSaveManager.cs`, `GameSaveMenu.cs` | `GameSaveRepository.cs`, `GameSavePrefs.cs`, `LegacyGameSave.cs`, `SaveLoadPanelHost.cs`; `Docs/SaveGames.md` |
| Login / registration | `Assets/UI/Scripts/AccountManager.cs` | Login assigns identity through `GameSaveManager.SetAccount`; account sign-in button is repurposed by `SaveLoadPanelHost` |
| Menu routes / multiplayer | `SceneController.cs`, `Multiplayer/GameSetupMenu.cs` | `Multiplayer/LobbyManager.cs`; keep Photon host/join separate from single-player save slots |
| Settings / pause | `PauseManager.cs`, `SharedOptionsPanel.cs` | `MainMenuOptionsHost.cs` attaches the same view in the main menu; `GameOptions.cs` applies global settings |
| Campaign / rewards | `CampaignLevelManager.cs`, `CampaignProgression.cs`, `CareerManager.cs` | `ProductionEconomy.cs`, `ContractGrader.cs`; `Docs/ProductionEconomy.md` |
| First commercial tutorial | `TutorialManager.cs` | `TutorialUIManager.cs` owns boss dialogue, hints and interaction restrictions |
| Goke / level 2 | `GokeLevelManager.cs` | `GokeCommercialCards.cs`; shared editor tutorial also participates |
| Terrari / level 3 | `Level3Manager.cs`, `LamborminiEditLesson.cs` | `LamborminiBrief.cs`, `LamborminiShowroom.cs`, `LamborminiVehicleVisual.cs`; `Docs/AutomotiveSimulation.md` |
| Level 4 / actors | `CampaignLevelManager.cs`, `ActorBot.cs` | `Docs/Level4Tutorial.md`, `Docs/ActorBots.md` |
| Stage / director tablet | `DirectorTerminal.cs`, `UIDragProp.cs` | `StageInterior.cs`, `ImportedProductVisual.cs`; `Docs/DirectorTablet.md`, `Docs/StageInteriors.md`, `Docs/ProductModels.md` |
| Equipment shop / delivery | `ShopManager.cs`, `ShopTerminal.cs`, `EquipmentInteractor.cs` | `ProductionKit.cs`, `ProductionKitShop.cs`, `ProductionEconomy.cs` |
| Lighting / grip | `FilmLightItem.cs`, `StageLightStrip.cs` | `ProductionLightModifiers.cs`, `ProductionExposureMonitor.cs`, `AutomotiveGrip.cs` |
| Camera / SD / recordings | `FilmCameraItem.cs`, `ComputerStation.cs`, `ComputerUIManager.cs` | Follow recording and inventory callers before changing media transfer |
| Editing / playback | `EditorManager.cs`, `TruePixelPlayer.cs`, `CommercialCompiler.cs` | `EditorTutorialManager.cs`, `DraggableClip.cs`, `ClipInspector.cs`, `ProjectDataManager.cs` |
| Branding / color | `BrandingBinManager.cs`, `DraggableOverlay.cs`, `BrandingClip.cs` | `ColorGradingManager.cs`, `ContractGrader.cs`; timing must agree with playback and grading |
| Contract / Almanac UI | `ContractUIManager.cs`, `AlmanacManager.cs`, `AlmanacBook.cs` | `ExportUIArt.cs`, `Assets/Resources/ExportUIArt.asset`; `Docs/UIExportIntegration.md` |
| Player controls / testing cheats | `InputManager.cs`, `DevTutorialBypass.cs` | Also inspect `Assets/Player`; tutorials own movement restrictions |

Unless a full path is given, filenames above are under `Assets/Script`.

## Runtime boundaries

- `GameSaveManager` persists across scenes, selects the active career and coordinates local/cloud saves. `GameSaveRepository` handles stored records; `GameSavePrefs` routes career preferences. UI consumes these services.
- `GameSaveMenu` draws the PSD-based save grid and dialogs. `SaveLoadPanelHost` connects the authored LOAD tab and existing account button. `GameSetupMenu` handles Single/Multi creation.
- `PauseManager` owns pausing and cursor restoration. `SharedOptionsPanel` owns settings drafts and UI. `GameOptions` owns applying settings; `MainMenuOptionsHost` owns the main-menu entry point.
- Tutorial/level managers orchestrate steps; equipment and editor components perform actions; grading checks the result. When changing a rule, check its tutorial instructions and grader together.
- `ExportUIArt` resolves catalog keys to sprites. Artwork lives in `Assets/UI/UI-EXPORT`, including `PSD Pieces`. Preserve authored art rather than adding a second UI on top.

## Scene flow and serialization

The main scenes live in `Assets/Scenes`: `Main Menu`, `Account`, `Login`, `CutScene`, `SingleStudio`, `MultiStudio`, `Editor`, and `ReviewScene`. Confirm enabled build order in `ProjectSettings/EditorBuildSettings.asset` before editing integer scene routes.

Unity scenes and prefabs reference scripts by `.meta` GUID. Public methods can be called through serialized UnityEvents even when C# has no callers. Some secondary components are added at runtime by type; do not assume every component appears in scene YAML.

## Refactoring strategy

Keep scene-facing components and saved data stable. Extract one responsibility at a time, retain original APIs, and validate that feature before continuing. The settings/save UI cleanup removed unreachable legacy builders and separated existing classes into matching files; it did not change scene assets, contracts, economy, saved data or SDKs. Larger tutorial managers remain intact and should be split only alongside focused tests for their step transitions.

No exact token saving is guaranteed: the primary benefit is locating and reading fewer relevant files. Do not paste this whole map into every task.
