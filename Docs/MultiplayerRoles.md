# Role-based multiplayer studio

## Start and shared contracts

Use the Main Menu Multiplayer create/join flow and share the room code. Two to
four players claim Director, Camera, AV Technician and Editor. Each role has one
owner; smaller crews can claim several roles. Lock roles, ready up, then the host
starts. Changing the selected contract clears readiness. The host can select any
of the five campaign contracts in the lobby; the default is Contract 1.

The six Boss introduction pages are shared. Space advances the current page for
everyone, with simultaneous presses advancing only once. The last page starts
production. Tab opens the contract: its specifications come from the same
ContractUIManager.DepartmentBrief as singleplayer. There is no separate
five-seconds-per-shot multiplayer checklist. Review the selected contract's
pre-production, production and post-production requirements, especially the
coffee interior, packaging/cup and performance footage for Contract 4.

Contract 1 starts with B9,000. A fresh room starting at a later contract has no
carried equipment, so it receives B9,000 plus that contract's advance. Progressing
from one contract to the next retains purchased equipment and the unspent team
balance, adding only the next advance. Completion bonuses use ProductionEconomy
and the campaign's grade multipliers; a contract pays only once. Offline career
money, purchases, progress and save slots are not used.

RoleSelectionUI.Lobby reuses MultiStudio's HelpDesk artwork for room code, roster,
role selection, Help and Settings. The host can unlock a member before starting.
During production, players can claim vacant roles after a crew member leaves,
but cannot drop an assigned role. Use the same updated build and a fresh room on
all clients. Leave Room returns to Main Menu.

## Stations and controls

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
  hotbar slot with 1-5 and insert a blank SD with C. LMB opens the viewfinder,
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
