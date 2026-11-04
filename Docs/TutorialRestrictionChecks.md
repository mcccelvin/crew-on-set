# Tutorial action regression checks

The equipment gate runs before player-initiated pickup, slot changes, drop, use,
card insertion/ejection, recording, camera adjustments and terminal interaction.
Script-driven lesson cleanup is deliberately not gated. Training-room/free-play
and the existing developer tutorial bypass are not restricted.

## Play-mode acceptance checks (not yet executed)

- Contract 1: during PickUpCamera, attempt to collect the blank SD card or a light.
  Neither enters the hotbar; a focus reminder appears. Collect the camera, advance
  the boss dialogue, then collect the card during PickUpSDCard.
- While carrying a required light/camera to its marker, press G, change to an
  empty/unrelated hotbar slot, enter a shop, and attempt recording. Inventory,
  position of held equipment, and recording state must remain unchanged.
- Contract 1: complete power, intensity and tilt steps in order; unrelated light
  controls are blocked. Drop is permitted only during DropLight.
- Contracts 2/3: collect the requested lights; power/intensity/height/tilt (plus
  temperature/diffusion for Contract 3) must follow the lesson order. G before
  the placement step or away from its marker must not detach the light.
- Contract 2: camera pickup, SD pickup and card insertion are separate steps.
  Open viewfinder, open settings, enable grid, close settings, and complete both
  framing checks. Leaving the viewfinder after completion must still work.
- Contract 3 camera practice: collect camera then card, open viewfinder, compare
  white balance, close settings, and perform the movement rehearsal. Recording
  and dropping are blocked. The practice must remain completable.
- Contract 4: unrelated pickups/drop/terminals are blocked during megaphone
  practice; existing per-action megaphone and tablet permissions still apply.
- Editor: wrong buttons retain the existing input gate; attempts during boss
  explanations show a reminder without advancing the lesson.
- During dialogue, E/G/C/R show a reminder. Space/dialogue advancement, pause,
  settings, and exit remain usable. Repeated blocked input must not stack dialogs.
- After tutorials, repeat normal pickup, drop, recording and shop interactions;
  no tutorial equipment restriction should remain. Also check training room.

Compilation is necessary but does not establish that these runtime checks pass.
