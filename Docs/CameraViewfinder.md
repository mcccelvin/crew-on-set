# CAM FX3 viewfinder

`CameraHUDController` displays the recording camera through a 16:9 RenderTexture inside a black surround. Its resource prefab is `Assets/Resources/CameraHUD.prefab`, authored on the next editor refresh by `Assets/Editor/CameraHUDBuilder.cs`. Use **Crew-On-Set > UI > Rebuild CAM FX3 Viewfinder** to rebuild it. The builder preserves an existing prefab by default, so Inspector layout changes survive reloads. The runtime layout is a fallback until the prefab is authored.

`Assets/Resources/CameraHUDArt.asset` references the user's exported `Assets/UI/CAM_FX3` focus and exposure artwork. Changing readouts are TMP labels rather than baked text from the PSD. The original PSD and PNG exports are unchanged.

`FilmCameraItem.HUD.cs` supplies real camera settings and recording state. `FilmCameraItem.Settings.cs` supplies the F2 menu. Manual focus unlocks in L2, camera WB in L3, exposure in L4. The UI shows the recorder's actual dimensions and frame rate; it does not invent battery drain or claim the artwork's 60p specification.

`MultiplayerAuthoredUI` uses the same layout with Photon recording time and actual SD state. Multiplayer currently records 320x180 at 6 fps and does not implement the single-player exposure controls, so those readouts remain hidden.

Existing scene `Cam Pov` artwork is suppressed while the new view is active. Thirds grid and subject tracking move into the inset view. TruePixelRecorder already restores a previous camera target after capture, so UI/borders never enter the tape. Closing the view restores the original camera target, viewport and aspect ratio.
