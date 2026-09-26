# Role-based multiplayer studio

## Start

Use the existing Main Menu Multiplayer create/join flow. Share the room code.
Two to four players claim Director, Camera, AV Technician and Editor. Each role has
one owner; smaller crews can claim several roles. Everyone locks their roles,
readies up, then the
host starts. The six Boss pages are shared: the first player's Space press advances
the page for everyone. Simultaneous presses for the same page advance only once;
the final page starts production without waiting for separate acknowledgments.
The Boss has no Close/Skip/Space button. Space remains the keyboard advance control.
E uses the physical shop/tablet/computer. P opens Almanac, Tab the contract,
Escape pause/options. RoleSelectionUI.Lobby reuses MultiStudio's HelpDesk UI artwork
for the room code, roster, role selection, Help and Settings tabs. Lock/unlock resets
readiness; the host can unlock a crew member before starting. The shared room snapshot
stores the lock state and the host validates it. During production, players can claim
vacant roles if a crew member leaves, but cannot drop an assigned role.
Gameplay uses authored UI.
The tablet keeps its authored view and element-card art, without an extra Close
button; Escape returns to the studio. In the shop, click an equipment card to
select a quantity, then Confirm purchases. There are no Add to Cart buttons.
Leave Room returns to Main Menu. Use the same updated build and a fresh room.

## Controls and brief

- Director: use the tablet to place the set, actors, chair and products. Buy a
  megaphone (B900), collect it and select its hotbar slot. Enter calls ACTION.
  Click to select an actor. Z/X/C cue performances;
  B saves a start mark, T repositions with a transparent preview (Q/E turns,
  LMB places), N saves the end, K walks, J returns, O stops/releases products.
  With an actor selected, click a chair, coffee machine or coffee product to cue
  the interaction. H clears selection. Tables cannot be held.
- AV Technician: buy lights (B1200) or an audio monitor (B750), collect and place
  them. Select a light: Z/X Kelvin, C/V intensity, F power, T placement, Q/E turns.
  Audio monitor uses F power, Z/X gain and displays the local output peak.
- Camera: buy camera (B4000) and SD cards (B150), then collect delivery.
  1–5 selects a slot, G drops. Hold camera, C inserts a blank SD, LMB viewfinder,
  V cycles Wide/Medium/Close; wheel adjusts field of view.
  Director calls ACTION; R starts/stops a take. Record at least five
  seconds of each size. Takes automatically stop at 30 seconds.
- Camera/Editor: hold a recorded SD and press E at the computer to deposit it.
- Editor: press E at the computer and open the recordings folder. Select a take to download/preview.
  Open Editor to use the copied Editor scene layout,
  play/scrub, set IN/OUT, and add it to the shared cut list. UP changes its order;
  REMOVE removes the cut without deleting the source. Play the assembled cut list
  and submit once it contains five seconds each of Wide, Medium and Close footage.
  All roles can read the Almanac; only Editor can open the computer/editor.
  Branding title and brightness/contrast/saturation are room state. The Editor
  preview applies the grade and title. There is no separate editing-kit purchase.

MultiplayerRecording reuses TruePixelRecorder without modifying it: silent
320x180, 6 fps JPEG tapes, 30 seconds maximum. The Camera client holds the source
tapes; other crew download them on demand via bounded, paced Photon chunks.
Keep that client connected until the Editor has downloaded the required footage.
These are low-bandwidth rushes, not full-resolution audio/video export. Full
singleplayer final video export/audio recording remain outside this adapter.
Composition contributes 45 points, lighting 25, performance 20 and set presence
10. The team grade averages the source scores in the submitted cut list.
The room has an independent B20,000 budget, 32 placed-object limit and 12-take log.
Movement uses the existing Photon player; recording is from its first-person view.
MultiplayerAuthoredUI uses copies of the SingleStudio and Editor canvas trees.
MultiplayerAuthoredUIBuilder generates Resources/CrewUI on first Editor refresh;
Crew-On-Set > Refresh Multiplayer UI Copies refreshes them after authored UI edits.
It copies views, bindings and sanitized equipment models without rewriting source
scenes or shared asset transforms. Runtime adapters replace career callbacks.
The copy also records SingleStudio's configured character model, height, animation
controller and tablet camera framing. Multiplayer instantiates that character for
every peer and drives the same locomotion parameters from replicated movement.
The local model casts shadows without obscuring its own camera.

## Isolation and ownership

- Only MultiStudio's serialized career/tutorial/shop/menu behaviour components
  were removed. Studio geometry and the original singleplayer scene are retained.
- PUNSpawner starts the multiplayer services, hides legacy UI and scene player,
  and spawns the existing Photon Player prefab. The controller type now matches
  MultiplayerPlayerController.cs, retaining its existing script GUID/fields.
- Room JSON `COS.Crew.V1` stores roles, the shared briefing page, purchased kits,
  actor cues, lighting, shots and cut lists. Commands use Photon event 171;
  targeted rejection messages use 172. Footage request/chunk events are 180/181.
  The master checks role permissions and budget before changing room state.
- Local deterministic replicas use the existing ProductModels catalog read-only.
  No Photon components were added to singleplayer furniture or prefabs.
- ActorBot has additive opt-in crew creation/motion/preview helpers. Normal actors
  keep their existing creation, walking, tutorial and interaction behaviour.
- Late joiners rebuild objects from the room snapshot. Photon master changes
  retain the latest snapshot; departed players release their roles. When the last
  player leaves, the session is discarded. There is no multiplayer career save.
- This is cooperative client-host authority, not cheat-proof server authority.
- MultiplayerAuthoredUI and its Stations/Editor partial files own authored views;
  MultiplayerRoomActions validates deliveries, pickup, SD insertion and roles.
  MultiplayerWorkbench is retained for compatibility but is no longer started.
  No singleplayer UI managers, ProductModels transforms or saved budgets change.
- Existing global audio/display preferences are accessible through pause/options.

## Verification / handoff

Editor and actual Windows standalone-player reference script compilations were
run. No full Unity player build, live UI render, or two-client Photon session was
run. Test with an editor and a build using the same Photon application/version:
join, claim/ready/start, verify role restrictions, placement/cues on both clients,
record/submit, join late and leave as host. Open a singleplayer save afterward to
confirm normal tutorials, money and backdrop behaviour. Script compilation does
not establish runtime networking or visual correctness.
