# UI-EXPORT integration

The LED strip now clones the normal panel-light equipment card on both shop canvases. It uses the existing illustration temporarily, displays the 900 B-Coin price, and adds the strip through the normal cart. The separate Lighting & Grip overlay is no longer built. Tutorial and Almanac wording point to the LIGHT STRIP card. The existing Level 3 unlock still applies.

`Assets/Resources/ExportUIArt.asset` references the original textures under UI/UI-EXPORT; textures are not duplicated in the game project. `ExportUIArt` creates and caches runtime sprites, so these original default-texture imports also work in builds.

Connected artwork:
- Almanac: paper surfaces, tab/header art, close icon; existing live entries and controls retained.
- Contract: folder and paper surfaces; accept/decline button backgrounds; dark text on paper.
- Options: exported background/frame, slider knob, green/red button backgrounds; existing sensitivity/fullscreen/save-on-close/reset behavior retained.
- Level 1 overlays: exported ECCENTRIC CENTERPIECE banner and text-only FLORA & FORM HOME wordmark, preserving their existing sequence and timing.

The folder also contains flattened mockups, duplicated layers, sample contract text, and a reference branded `ref (DELETE)`. These are not rendered over live controls. Extra product variants and Terrari artwork are cataloged for later content; they do not replace a different level's client or unlock reserved equipment. No new 3D equipment models were found in this PNG export folder.

## Shared settings

Main-menu Options and pause-menu Options instantiate `SharedOptionsPanel` from `Assets/Script/SharedOptionsPanel.cs`. `MainMenuOptionsHost.cs` wires the inactive main-menu entry point on scene load; `PauseManager.cs` owns the pause entry point; `GameOptions.cs` applies global settings. Both views use the supplied OPTIONS.psd frame, tabs, slider and action-button artwork and the same global Options.* preferences. SAVE applies and persists all draft settings; RESET stages defaults; X/Escape cancels. Closing in-game settings preserves the pause state.

General: logarithmic mouse sensitivity and controls reference. Audio: master volume through AudioListener. Graphics: fullscreen, Unity quality level and frame-rate cap. Separate music/SFX/voice buses and keyboard rebinding are not presented as working controls. Fullscreen applies in standalone builds.

## PSD contract and Almanac design

The supplied Almanac.psd, CONTRACT.psd and CONTRACT2.psd artwork is extracted into `Assets/UI/UI-EXPORT/PSD Pieces`. The original cover, parchment, metallic rings, five colored ribbons, closed folder, open folder/paper stack and taped photo frame are used directly. Photoshop sample copy and transparency checkerboards are not included. The runtime catalog references these textures for builds.

All five contract introductions use a three-folder selector with arrows and SELECT. Only the current job can be selected. SELECT opens the scrollable brief; closing its red X resumes the original acceptance callback exactly once, allowing the boss/tutorial to continue without overlapping the brief. Subsequent TAB reviews remain available. Contract requirements and economy values remain live game data, not the PSD's sample 60,000 budget.

The Almanac uses EQUIPMENTS and TECHNIQUES tabs, illustrated category ribbons and pagination of unlocked beginner instructions. Director record and milestones remain accessible at the foot of the left page. Artwork is from the PSD; entry text is dynamic and other products use available artwork or a name placeholder in the photo frame.

Validation: editor/player compilation and isolated Unity checks for texture loading, strip-card duplication prevention, price/cart listener, contract lock protection, brief scrolling and exactly-once acceptance. 1920x1080 renders of the Almanac, selector and brief are reviewed separately from the live studio scene.
