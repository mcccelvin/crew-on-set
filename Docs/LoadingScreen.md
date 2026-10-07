# Loading screen circle close

`Assets/Script/LoadingScreenController.cs` retains its public scene-loading overloads and persistent overlay. The supplied logo, gameplay tips, original background, progress bar, scene routes and Photon synchronization are unchanged. A centered 0.7-second blue iris close covers the loading screen, followed by a 0.45-second smooth fade revealing the ready destination. The outside uses the loading bar's exact cyan-blue (`#00EBEB`), with one shared color constant for both; the transparent opening shows the actual loading screen, not green chroma-key imagery.

## Timing and ownership

- Local: render the loading overlay, begin Unity async loading with activation held, wait for 0.9 readiness and the original 0.65-second minimum, complete the displayed progress bar, hold for the original 0.12 seconds, close the circle over 0.7 seconds, present a fully blue frame, then allow destination activation. Keep the blue cover until the operation completes and the destination has an initialization/render frame; hide the old loading backdrop, fade blue to transparent over 0.45 seconds with SmoothStep easing, then remove the overlay.
- Multiplayer: retain `PhotonNetwork.LoadLevel` and Photon-owned scene activation. Once the synchronized arrival is reported, complete the bar, close the same circle and fade the full blue cover to reveal the arrived scene. The destination may already be loaded behind the cover; no Photon activation/scene queue is held by this feature. Arrival callbacks during close/reveal cannot start another reveal.
- Progress, circle motion and reveal fading use unscaled time. Loading artwork keeps alpha 1 during the circle close; only after full closure is the loading backdrop hidden and the blue overlay faded. There is no automatic circle-opening animation, new sound or video download.

`Show` resets the circle, restores the loading backdrop and full opacity before enabling the overlay. `Finish` cancels pending coroutines and clears the loading/network/close/reveal state, circle and overlay, including disconnect/room-leave and failed-start cleanup. Subsequent loads reuse the same original overlay with an open circle. The old logo/tips never fade back over the destination.

## Rendering

`BlueLoadingIris.cs` is a `MaskableGraphic` using Unity UI's default material, with no extra shader/video/bitmap assets. Its circular opening uses local canvas units, preserving equal horizontal/vertical radius across aspect ratios. A 256-segment ring plus exact corner spokes covers the rectangle without corner holes. A 1.25-pixel alpha edge smooths the boundary. Zero closure has no blue mesh; full closure uses a completely opaque full-screen quad. Stretch anchors automatically track screen size changes.

The overlay retains sorting order 32760. The existing transparent full-screen input shield remains above the iris and blocks UI clicks until loading finishes. The iris itself does not intercept raycasts.

## Verification (2026-10-06)

The changed scripts compile against the actual project's Editor and standalone references. An isolated Unity 2022.3.62f3 fixture using the real loading controller, iris and loading-logo asset passed 32 native render assertions at 1280x720, 1024x768 and 1920x720, including the transparent opening, blue coverage, circular aspect and completely opaque final frame. Native Play Mode checks passed two real asynchronous scene loads with activation held until full blue, paused-time animation, repeated-load reset and disconnect/failed-start cleanup. Photon arrival was simulated; an actual multiplayer session and full game build were not tested. Contact sheets and reports are in the chat workspace's `loading-circle-*` files.

2026-10-08 color-only update: the wipe now shares the progress fill's unchanged `#00EBEB` color. Editor/standalone-reference compilation and 33 native render assertions passed, including exact circle/fill color equality; the updated contact sheet was inspected. Timing and scene-loading behavior were not changed.

2026-10-08 reveal-fade update: actual Editor/standalone-reference compilation passed. The isolated native fixture passed 40 render assertions (including full/intermediate/transparent reveal poses without loading-art ghosting) and two real async scene-load tests covering opaque activation, monotonic unscaled fade, destination readiness and repeat restoration. Disconnect/failed-start cleanup and simulated Photon arrival also passed. The native reveal contact sheet was inspected; no live multiplayer session or full game build was run.
