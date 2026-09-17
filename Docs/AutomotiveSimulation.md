# Automotive commercial simulation and 3D asset guide

## Contract 3 editing requirement

Camera practice requires opening the viewfinder with LMB first, then five accumulated seconds of actual Ctrl + WASD movement with the viewfinder open before the next Boss dialogue.

The Soft Light starts at 4300K; the player uses Z (lower) / K (higher) to reach 3200K during the placement lesson and immediately starts Boss-guided camera pickup, blank SD-card collection, and Ctrl + WASD rehearsal. Rehearsal requires at least one metre of actual movement with the camera equipped. It runs before the Almanac/contract introduction and marks the camera lesson complete to avoid repeating it at the car setup. Existing careers that skipped this introduction retain the later setup fallback.

Use exactly three separate Level 3 recordings on three SD cards: back, side and overall car views. Budget 450 B-Coins for three blank cards. Record and trim each view to about 7 seconds. The editor supplies a 2-second Terrari intro and 2-second outro. The required continuous sequence is intro + three different recordings + outro, totaling 25 seconds. The grader requires different source filenames, full supplied cards, valid trim ranges, no gaps/overlaps, and Soft Light metadata on the recorded takes. Duplicate or split copies of one recording do not satisfy the contract. Hold Ctrl while moving with the camera for precision movement; the movement itself does not change the score.

## Current release: beginner curriculum

Level 3 uses the warm Soft Light and does not introduce the LED strip. Its guided boss lesson starts after the car, backdrop and deployed Soft Light are ready. The lesson teaches Ctrl precision movement with the camera, how to begin and settle a moving shot, the three required SD-card views, and the 25-second editor structure. The Soft Light practice target is 3200K so the lesson target and score agree.

| Level | Equipment taught and used |
|---|---|
| 1 — Flower | Camera, SD card, one panel light; guided purchase, pickup, controls and recording |
| 2 — Goke | Reuse equipment, upgrade camera, three panel lights; key/fill/back placement and thirds practice |
| 3 — Car | Soft Light practice, then LED-strip rim lighting: power, color, tilt, placement and camera inspection |
| 4 — Coffee | Reuse camera and Soft Light; guided Actor setup, performance, coverage and continuity |
| 5 — Campaign | Combine familiar camera, media, lighting and editing skills with contract and Almanac guidance |

Strip controls: E pickup, left-click power, C white/cyan/warm, R tilt, Q turn, G deploy. The remaining prototype feature descriptions below are implementation notes for future use, not currently available gameplay.

## Implemented in level 3
From level 3 onward, the equipment shop has a LIGHTING & GRIP page. The player can buy the tools in any later production where they are useful, collect them from delivery, carry them in the normal hotbar, and deploy them on the stage. They are optional tools, not compulsory new scoring requirements. The shop includes an OVERHEAD LIGHT BANK (1,800 B-Coins), BLACK FLAG + STAND (350 B-Coins), DIFFUSION FRAME (600 B-Coins), BOUNCE BOARD (250 B-Coins), TRACK DOLLY (1,600 B-Coins), LIGHT STRIP (900 B-Coins), and EXPOSURE MONITOR (500 B-Coins). The level 3 boss introduces the page and explains the tools.

Carry a tool with E, deploy it with G, turn equipment with Q/E, and use left-click while holding a light. The camera can mount/release from a nearby dolly with J and start/stop its move with K. In the camera viewfinder, M toggles the purchased exposure histogram. Equipment ownership is saved with the existing delivery system. Wall color editing does not recolor these tools.

The bank has a ground-supported gantry and four downward, shadow-casting spotlights. The opaque flag can block direct light from shadow-casting sources. These are procedural placeholders ready to replace with authored meshes.

Limitations: four spots approximate a large source; this is not physically accurate area lighting or dynamic studio reflections. The flag does not simulate full global-illumination negative fill. Rigging loads, electrical circuits, photometric exposure and polarizers are not simulated. The new tools do not award points simply for purchasing them.

## Prioritized assets to model
Suggested dimensions below are game modeling targets, not manufacturer specifications. Use one Unity unit per metre, +Y up and a ground-level root pivot. Separate adjustable joints, cloth, metal, light faces and stands.

| Priority | Asset | Suggested size / important parts |
|---|---|---|
| 1 | Overhead light bank and supported gantry | Bank 3.6 x 2.2 m, gantry height 3.3 m; white diffusion face, black housing, crossbar, uprights, feet and ballast |
| 1 | Black flag/floppy and C-stand | 1.2 m square cloth; frame, grip head, adjustable riser, staggered legs and sandbag |
| 2 | Diffusion frame | 2.4 m square; translucent cloth, frame, stands and ballast; requires a separate lamp behind it |
| 2 | White/black bounce board | 1.2 x 2.4 m; reversible surfaces and supported base |
| 2 | LED panel or COB light with softbox | Separate emitter, yoke, controls, stand and power cable; panel body around 0.6 x 0.35 m |
| 2 | Camera dolly and track | Platform about 0.8 x 1.1 m and modular 3 m track sections; wheels, leveling feet and tripod mount |
| 3 | Camera package | Tripod, fluid head, lens, matte box, monitor, battery, media and focus controls |
| 3 | Video village cart | Around 1 x 0.6 x 1.5 m; monitor, playback equipment and cable storage |
| 3 | Grip/electrical kit | Sandbags, clamps, safety cables, cable ramps, distribution boxes and storage carts |
| 3 | Cyclorama and curtains | Curved seamless wall-to-floor transition; black curtains for spill/reflection control |

## Photo references
- [Overhead Chimera bank above a car](https://www.adorama.com/cm8982.html)
![Overhead bank](https://www.adorama.com/images/Large/cm8982.jpg)
- [Black floppy flag on a stand](https://thegripstore.com/products/40x40-floppy-top-hinge)
![Flag and stand](https://thegripstore.com/cdn/shop/files/floppy_40x40_solid_top-hinge_169025T.png?v=1774903479&width=1946)
- [ARRI: Subaru's in-house studio](https://www.arri.com/news-en/arri-skypanels-light-subaru-s-new-in-house-multi-purpose-studio-)
- [Matthews lighting control equipment](https://www.msegrip.com/collections/lighting-control)
These are visual references; build your own meshes rather than treating the photographs as redistributable game textures.

## A more realistic playable workflow
1. Receive a brief: audience, message, duration, aspect ratio, required product details and deliverables.
2. Plan a shot list and budget: hero angle, badge, wheel, interior and controlled camera move; allocate crew time, rentals and contingency.
3. Prepare the set: clean the car, position the background, block camera/vehicle movement, secure stands and keep cables out of paths.
4. Shape lighting by looking at the car's reflections. Large bright surfaces create broad highlights; flags control spill. Light strips provide accents rather than replacing a main light.
5. Set and check exposure, white balance, focus and unwanted reflections. Add waveform/false-color tools for meaningful feedback.
6. Record several takes and coverage, with continuity checks and take notes. Camera moves should have deliberate start/end framing.
7. Transfer and verify media, select takes, edit to the brief, add appropriate sound/music and branding, then color-correct consistently.
8. Review client notes and export the requested format; calculate actual spending and profit.

The next technical priorities should be believable car reflections and exposure monitoring. More decorative props alone will not make lighting decisions realistic. After that, add a controllable dolly, diffusion/bounce behavior, shot-list coverage and rental/crew-time costs. Keep these staged so the player learns one technique at a time.

## Camera precision movement
Hold either Ctrl key with a camera equipped to move at 1.25 m/s with eased acceleration/deceleration and gentler mouse/stick look. Ctrl overrides sprint. Release movement before Ctrl for a smooth stop. This applies to the single-player Rigidbody controller and respects existing menu/tutorial movement locks; zoom and pedestal recording locks are unchanged. It does not change grading or the first tutorial stationary-shot requirement.

## Panel / Soft Light height and haze
While holding a FilmLightItem, Page Up / Page Down extends its head by 0-1.5 metres above the original stand height. A telescopic riser connects the head; deployment and re-pickup retain the extension. H toggles a subtle local haze volume, following power, tint and output. The Resources/StudioLightHaze shader ray-marches a cone with camera-depth clipping; it is a visual approximation, not shadowed volumetric scattering. Transparent blockers and occlusion between source and objects are not simulated. The effect does not add grading points. The separate LED-strip prop is unchanged.

The haze is now setup-view only: per-camera density is zero for film-camera hierarchies, recorder cameras, render textures and reflections. Height raises the physical panel and emitter together, including while carried; the held-model anchor no longer cancels extension. Actual light placement still affects illumination; only the haze guide is excluded from footage.

Stand revision: panel and Soft Lights start at +0.50 m extension, applied after measuring the authored Light/Stick hierarchy. Q raises and E lowers the stand within +0.00 to +1.50 m; the right-side HEIGHT readout shows this extension live. Pickup and placement preserve the current adjustment. Height stretches the authored stand about its measured bottom, with no added pole. Setup haze follows light power automatically; there is no H toggle.

Shared light practice now teaches stand height before tilt: Key +0.50m, Fill +0.25m, Back +0.80m. These are demonstration extensions. Goke practice uses increased stage-bounded spacing; its camera station remains between the front-light stations. Key/Fill/Back roles and intensity guidance remain consistent.
