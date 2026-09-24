# Role-based multiplayer studio

## Start

Use the existing Main Menu Multiplayer create/join flow. Share the room code.
Two to four players claim Director, Camera, Lighting and Set/Props. Each role has
one owner; smaller crews can claim several roles. Everyone readies up, then the
host starts. Tab/Escape opens the crew overlay. Leave Room returns to Main Menu.

## Controls and brief

- Director: buy an actor in the crew menu. Click to select. Z/X/C cue performances;
  B saves a start mark, T repositions with a transparent preview (Q/E turns,
  LMB places), N saves the end, K walks, J returns, O stops/releases products.
  With an actor selected, click a chair, coffee machine or coffee product to cue
  the interaction. H clears selection. Tables cannot be held.
- Set/Props: purchase a backdrop/interior, chair, table, coffee pack or cup from
  the crew menu. Aim at a horizontal surface and click to place. T moves selected
  objects, Q/E rotates during placement. Delete removes an unused object without
  refund. Changing a set requires actors to stop using its furniture first.
- Lighting: place lights; select one, Z/X changes Kelvin, C/V intensity, F power.
  Crew-menu sliders adjust yaw, tilt and diffusion.
- Camera: choose Wide/Medium/Close in the menu; wheel adjusts field of view.
  Director calls ACTION in the menu; R starts/stops a take. Record at least five
  seconds of each size. Takes automatically stop at 30 seconds. Director submits.

The first implementation stores **shot metrics, not video files or an editing
timeline**. Composition contributes 45 points, lighting 25, performance 20 and
set presence 10. The team grade averages the best qualifying take of each size.
The room has an independent B20,000 budget, 32 placed-object limit and 12-take log.
Movement uses the existing Photon player; recording is from its first-person view.
The initial crew overlay uses Unity IMGUI, independent of existing career PSD UI.

## Isolation and ownership

- Only MultiStudio's serialized career/tutorial/shop/menu behaviour components
  were removed. Studio geometry and the original singleplayer scene are retained.
- PUNSpawner starts the multiplayer services, hides legacy UI and scene player,
  and spawns the existing Photon Player prefab. The controller type now matches
  MultiplayerPlayerController.cs, retaining its existing script GUID/fields.
- Room JSON `COS.Crew.V1` stores roles, ready state, purchases, actor cues, lighting
  and shots. Commands use Photon event 171; targeted rejection messages use 172.
  The master checks role permissions and budget before changing room state.
- Local deterministic replicas use the existing ProductModels catalog read-only.
  No Photon components were added to singleplayer furniture or prefabs.
- ActorBot has additive opt-in crew creation/motion/preview helpers. Normal actors
  keep their existing creation, walking, tutorial and interaction behaviour.
- Late joiners rebuild objects from the room snapshot. Photon master changes
  retain the latest snapshot; departed players release their roles. When the last
  player leaves, the session is discarded. There is no multiplayer career save.
- This is cooperative client-host authority, not cheat-proof server authority.
- Existing global audio/display preferences remain shared. The old multiplayer
  pause overlay is not used by the crew scene; adjust settings from Main Menu.

## Verification / handoff

Editor and actual Windows standalone-player reference script compilations were
run. No full Unity player build, live UI render, or two-client Photon session was
run. Test with an editor and a build using the same Photon application/version:
join, claim/ready/start, verify role restrictions, placement/cues on both clients,
record/submit, join late and leave as host. Open a singleplayer save afterward to
confirm normal tutorials, money and backdrop behaviour. Script compilation does
not establish runtime networking or visual correctness.
