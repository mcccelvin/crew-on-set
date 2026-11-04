# Role-based multiplayer studio

Crew voice is available in the waiting lobby, role selection and studio. Hold
LEFT ALT to talk by default, or change mode/key, input device, mic gain and voice
volume in Audio Options. Lobby and pause microphone icons show availability,
mute and speaking; click your own to mute/unmute or another player's to mute
them locally. Options retain preview/Save/cancel behavior. See `Docs/VoiceChat.md`.

## Start and shared contracts

Use the Main Menu Multiplayer create/join flow and share the room code. Joining
stays over Main Menu: two to four players ready up, then the host presses START
to open synchronized role selection. Roles are not selected while joining.
Players then claim Director, Camera, AV Technician and Editor. Each role has one
owner; smaller crews can claim several roles. Lock roles, ready up, then the host
starts the contract. Changing the selected contract clears readiness. The host
can select Contracts 2–5; the default is Contract 2. Contract 1 remains solo-only.
Hosting and joining require the current save owner's completed solo tutorial
(`FlowerContractGraded`), checked from saved careers and the active solo career.
Selecting a higher level or a room contract does not satisfy this requirement.

Each player reads the six Boss introduction pages independently. Space advances
only that player's page; finishing immediately releases that player into the
studio while teammates continue reading. Page progress survives host migration and
resets for the next contract or retry. Tab opens the contract: its specifications come from the same
ContractUIManager.MissionBrief builder as singleplayer. There is no separate
five-seconds-per-shot multiplayer checklist. Review the selected contract's
pre-production, production and post-production requirements, especially the
coffee interior, packaging/cup and performance footage for Contract 4.

A fresh room has no carried equipment, so it receives B9,000 plus the selected
contract's advance. Progressing
from one contract to the next retains purchased equipment and the unspent team
balance, adding only the next advance. Completion bonuses use ProductionEconomy
and the campaign's grade multipliers; a contract pays only once. Offline career
money, purchases, progress and save slots are not used.

Multiplayer gameplay loads SingleStudio, the same authored scene used offline.
Its early multiplayer bootstrap installs room-owned players/controllers and keeps
the solo tutorial, player and UI inactive. Role selection has its own 3D stage,
teammate roster, Help and Settings. The host can unlock a member before starting.
During production, players can claim vacant roles after a crew member leaves,
but cannot drop an assigned role. Use the same updated build and a fresh room on
all clients. Leave Room returns to Main Menu.

## Stations and controls

The equipment shop uses ShopTerminal's native solo card builders, layout, icons,
stock captions, prices and Add to Cart/Confirm/Cancel flow. Its mounted display
is the actual SingleStudio world-space canvas under Equipment, not a missing
reference inside the UI-only copy. The solo career terminal stays disabled;
room commands own purchases and delivery at the shared delivery marker.
The shared delivery marker points to the truck's rear pickup shelf inside the
existing garage through the automatic doors; tutorial guidance and solo delivery use it too.
Equipment stays stationary on that shelf until collected, and the garage is
included in room drop validation. The host still owns purchase positions and cost.
Native contract unlocks apply to both UI and host validation, with unique cameras and
megaphones, repeatable lights/SD cards, and no synthetic Audio Monitor card.
Only acknowledged purchases clear the buyer's cart. Per-member receipts prevent
duplicate charges from a repeated request. Failed purchases use GameFeedback.
The shop-side drawer cabinet stores shared equipment through E. STORE clears the
sender's hotbar slot; TAKE reserves a free slot and equips the same room object.
The host checks cabinet proximity, role, recording state and item ownership for
each request. Loaded SD cards must be ejected before storing a camera; recorded
cards retain their take identity. Stored lights are off and world replicas hidden.
Room-only floating status/instructions no longer cover the normal studio HUD.

Character looking follows the camera up/down and left/right through the shared PlayerLookPose chest/neck/head layer. MultiplayerPlayerController is observed alongside PhotonTransformView in the Player prefab and replicates look pitch/world yaw. Other peers interpolate those angles onto their animated customized character; local movement follows view direction while body yaw eases toward it. The pose is restored before animation each frame to prevent accumulated bone rotation.

Gaze correction uses the avatar's bind-pose head axes and corrects the animated
face toward the requested viewing direction, including at zero pitch. It does
not retain an idle clip's downward tilt. The multiplayer camera anchor is
measured once from humanoid eyes or the rig's skinned eye mesh after the neutral
look correction. It stays stable during animation and ignores hair/outfit bounds,
so the first-person view and the visible character share the same eye level.

Spawned players wear their own equipped customization, replicated from the owning player's public cosmetic IDs. Each peer applies those IDs to the same animated skeleton; remote characters never use the viewer's local outfit. The Tab contract view uses one authored open folder with a clipped, wheel-scrollable requirements page and the current contract's product artwork. Legacy selection folders, locked labels and SELECT controls remain hidden behind this room-owned view.

The room contract reuses the qualificationsPanel/briefScroll/briefBody hierarchy copied from SingleStudio. ContractUIManager and the room adapter share ContractBriefPresentation for the full-canvas 1920x1080 artwork, 530x740 page, canvas scaling, font, margins, dimming and clipping. Both default to the same MissionBrief text builder: client title, pitch, delivery summary, production budget, numbered BUILD THE SET / GET THE SHOT / CUT & BRAND sections and pass bonus. Contract 4 uses the supplied Kape Kultura coffee-folder artwork. The room does not prepend a different team-grade header or long department/budget guide. Its live crew balance and role/host-gated Retry/Next/Revise controls are in a scrolling document footer, rather than over the cover or outside the folder. Balance/permission updates preserve the reading position. Room views continue to omit solo signing and account-currency promises; Tab closes/reopens the brief, matching solo. Button captions use live, outlined text.

- Director: E opens the physical tablet. Place the selected contract's set,
  products, vehicle and actors. R rotates a placement by 45 degrees; Q/E makes
  fine adjustments. Actors and walk marks must stay inside the stage. Blocked
  routes use ActorBot's existing capsule/route checks. Buy and hold the megaphone
  (B900); Enter calls ACTION. LMB selects an actor or cues a chair, machine or
  product interaction. Z/X/C cue performances; B/N save walk marks, T previews a
  new position, K rehearses, J returns, O stops/releases and H clears selection.
- AV Technician: buy panel lights (B1,200), Better Lights (B4,500) or an audio
  monitor (B750). Collect delivery and select a hotbar slot. Light controls:
  Z/X Kelvin, C/V intensity, F power, T placement and Q/E turns. Audio monitor:
  F power, Z/X gain, with local output-peak display.
- Camera: buy camera (B4,000) and SD cards (B150), collect delivery, select a
  hotbar slot with 1-5 and insert/remove an SD with C. Recording leaves the card
  in the camera until its owner presses C again. LMB opens the viewfinder,
  V cycles Wide/Medium/Close and wheel adjusts field of view. R starts/stops a
  take. Contracts 4 and 5 require the Director's ACTION cue. Takes stop at 30
  seconds; use the selected brief's shot/duration requirements.
- Camera/Editor: hold a recorded SD and press E at the computer to deposit it.
- Editor: E opens the computer. Select any uploaded recording to download or
  preview; selecting another while downloading queues the last selection. Open
  Editor loads the functional singleplayer editor with room-only footage. Drag,
  arrange, trim and split clips; use supplied intro/outro clips where applicable,
  branding, overlays, music, transitions and color grading. Export, review and
  submit using the existing editor controls. Back to Computer retains this
  client's in-progress edit. Closing stops local playback/music.
- Everyone can read Almanac (P), the contract (Tab) and pause/options (Escape).
  Only the Editor can enter the computer/editor. Only the host can restart or
  advance a contract.

Escape opens the native PauseManager.pauseMenuCanvas through an explicit copied
binding. Older copies resolve only a Pause canvas containing Panel/Resume and
Panel/Option(s), never the computer playback button also named Pause. The native
shutter frame and Resume/Options/Exit artwork are retained. A left-side roster
uses the same shutter texture, teal accents, outlined white type and equal crew
rows, showing live usernames, roles, HOST/YOU labels, room code and empty slots.
Available-role actions retain the existing ability to fill departed crew roles.
The menu is local: room time and teammates continue. Resume/Escape restores the
previous station or briefing; Options Escape first returns to Pause. Local
editor playback is paused without losing the edit, and an editor download cannot
activate over the pause menu. MultiplayerAuthoredUI.Pause owns this behavior;
the legacy MultiplayerPauseManager defers Escape when that adapter is ready.
The copy builder includes PauseManager's reference and preserves SettingsLayout
and CircularSliderKnob so the scene-authored options view can be reused.

The shared review includes department grades and the Editor's campaign feedback.
REVISE EDIT is Editor-only. NEXT CONTRACT is host-only after passing; it clears
old takes/sets, retains equipment, and starts the next brief. RETRY CONTRACT
opens a Boss confirmation with RETRY CONTRACT and KEEP WORKING buttons. Confirming
restores that contract's starting team budget/equipment, clears its recordings
and cuts, and restarts the introduction. It does not overwrite an offline save.

## Functional editor copies

MultiplayerAuthoredUIBuilder copies SingleStudio's authored station views and
sanitized equipment models without editing source scenes or asset transforms.
Its EditorGameplay copy additionally retains the actual EditorManager,
ContractGrader, CommercialCompiler, timeline, trim, branding, color, playback and
editing controllers, with their serialized references and UnityEvents remapped
across copied roots. Career/tutorial/pause scene controllers and duplicate
cameras/listeners/EventSystems are removed from this copy. The legacy sanitized
Editor view remains available for compatibility.

Resources/CrewUI copies refresh automatically in edit mode and before builds.
Use **Crew-On-Set > Refresh Multiplayer UI Copies** after stopping Play Mode if
the functional copy is missing. A build check refuses packaging without an
EditorGameplay copy containing EditorManager. Entering Play Mode or rebuilding
is still necessary: changes do not update an already-running room/build.

The Studio copy records its configured character model, height, animation
controller and tablet camera framing. Multiplayer uses that character for peers
and drives locomotion from replicated movement. The local model casts shadows
without obscuring its camera. ProductModels remains a read-only catalog.

## Recording and authority

MultiplayerRecording reuses TruePixelRecorder for silent 320x180, 6 fps JPEG
rushes, with a 30-second maximum, 12-take log and paced bounded Photon transfers.
The Editor receives repeats of those frames on the original editor's 24 fps
clock, so playback and trims retain their duration. This does **not** create
24 unique captured frames per second or full-resolution video. Session tapes
live in a unique CrewSessions subfolder, outside the offline recordings index.

Keep the Camera client connected until the Editor downloads the footage. An
announced downloaded copy can subsequently serve another Editor if the original
owner leaves. At least one connected client must retain a complete copy; footage
is not stored on a server. Master migration retains metadata, not missing files.

The actual ContractGrader checks post-production and campaign-specific gates.
The host validates the submitting role, contract, uploaded source IDs, trim
ranges and finite report scores. It recomputes room setup, weighted source
camera/lighting scores, final rank and payout. Production evidence is sampled
throughout the take (visibility, soft light, three-point roles, coffee action
and screen direction), rather than using only the final frame. Composition and
lighting scoring still use the multiplayer studio's approximations, not every
FilmCameraItem metric. The host trusts the Editor's detailed post-production
report: this is cooperative client-host authority, not cheat-proof server-side
timeline/branding validation.

## Isolation and ownership

- PUNSpawner starts multiplayer services, hides legacy career UI/player, and
  spawns the existing Photon Player prefab. Singleplayer scenes are not replaced.
- GameSavePrefs.BeginRoomSession redirects career-key operations to ephemeral
  room values while retaining the selected offline save. GameSaveManager skips
  room checkpoints. Account identity and Options remain global. Leaving restores
  ordinary career-key routing. Room EditorManager uses RoomFootage, not
  ProjectDataManager.compiledFootage; grading skips CrossSceneData and analytics
  capture for the offline career.
- Room JSON COS.Crew.V1 contains roles, selected contract, briefing, budget,
  purchased objects, checkpoint, sampled footage evidence, submitted cuts and
  review. Commands use event 171, rejection notices 172, snapshot requests 173;
  footage request/chunk events are 180/181. The master checks permissions and
  funds before changing state.
- MultiplayerAuthoredUI and its Stations/Editor partials own station views.
  MultiplayerRoomActions validates shop/delivery, pickup, SD and role operations.
  NetworkStudioFactory/NetworkStageObject create deterministic replicas.
  MultiplayerWorkbench remains for compatibility, not normal startup.
- ActorBot has additive crew helpers; ordinary singleplayer actors retain their
  existing walking/tutorial/interaction flow. No network components were added
  to singleplayer furniture or prefabs.
- Late joiners rebuild objects from the snapshot. Departed members release their
  roles. The session is discarded after the last player leaves; there is no
  multiplayer career save. Source IDs remain monotonic across retries.

## Remaining parity gaps

This is a shared-contract/functional-editor integration, not yet a verified
one-to-one copy of every singleplayer interaction. The full singleplayer tutorial,
scroll-to-bottom acceptance/signing flow, complete props/world interaction
catalog (including player chair sitting), full-resolution/audio capture and
final-file sharing are not ported. Editor drafts, overlays and chosen music are
local to the active Editor; only submitted source cuts/grades are shared. Closing
and reopening preserves that client's draft, but changing Editor owner does not
transfer a complete editable project. Crew-wide playback of the finished edit
is not implemented. Two-client gameplay/visual validation is still required.

## Verification / handoff

Focused standalone logic checks cover room/offline preference isolation, role
coverage, setup rules for all five contracts, shared prices, checkpoint retries,
grade thresholds and 6-to-24 fps duration/index mapping. Those checks use Unity
stubs; they are not a runtime scene/network test. Actual Editor and Windows
standalone-reference runtime script compilations and the Editor-only copy-builder
compilation were run. A source-coordinate WPF lobby layout preview was rendered
and inspected for overlapping selector/readiness/status controls and sample text;
it does not verify Unity/TMP, the authored background or click wiring.

No full Unity build, live UI render or two-client Photon session was run. Test
two updated clients in a fresh room: cover roles, choose/ready/start, verify
restricted stations/commands, delivery and held-light controls, blocked actor
routes, record/deposit multiple takes, switch downloads, edit/trim/music/color,
review/pay once, revise/retry/advance, late join, and leave/migrate the host.
Verify Contracts 2 and 4 do not acquire an unintended three-point requirement.
Keep a downloaded-copy holder connected and test Camera departure. Open an
offline save afterward to confirm its budget, tutorials and recordings are
unchanged. Script compilation does not establish runtime or visual correctness.
