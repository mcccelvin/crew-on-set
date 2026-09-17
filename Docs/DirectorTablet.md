# Director tablet stage and color controls

Prop selection and right-click deletion resolve the outer Props Rigidbody, including tablet wrappers around prefabs with nested bodies (such as the Goke cube). Deletion clears selection and drag references before destroying the whole prop; selection outlines skip destroyed renderers.

Default ADD WALL uses `Assets/Studio/GreenScreenBackdrop.prefab`, derived from `Assets/Product/GreenScreen.fbx`, in SingleStudio and MultiStudio, including guided wall practice. It uses 0.95 scale and mesh colliders so the interior stays open. The existing Screen color-selection path is retained. Placement aligns the screen floor to the stage in either vertical direction.

Both studios use `Assets/Product/STUDIOP.fbx`. Existing station components remain attached to their original objects and are positioned on the model's director, editor, sound and kiosk tables. `Assets/Editor/StudioAssetMigration.cs` owns the migration; original scene backups and inspection reports are in `Logs/StudioRedesign`. The former studio renderers and colliders are disabled while existing gameplay references and lesson markers are retained.

Level 3 uses the same **ADD WALL** action as the other levels. It spawns the supplied green-screen asset for 500 B-Coins. There is no automatic or tablet-generated showroom preset. The player chooses the backdrop color, places the car, and arranges the lighting to follow the contract. **CLEAR STAGE** removes the wall and props. The contract, tutorial, and handbook describe this manual setup.

The RGB readouts beside the sliders are now editable integer fields. Enter **0–255** and press Enter or click elsewhere to apply. Values outside this range are clamped. The HEX field accepts six digits with an optional `#`, plus three-digit shorthand (for example, `#F60`). Invalid input restores the previous valid color. Inputs and sliders synchronize, and live refresh does not overwrite a field while it is being edited. Editing a field suspends the tablet's object movement/selection shortcuts.

Color editing follows the selected target and existing tutorial permissions. Vehicle paint changes only the car body. Imported packaged-product colors remain protected.

## Verification

Editor and player source compilation passed with Unity 2022.3.62f3. Earlier input checks used the actual DirectorUI hierarchy and font/texture assets from SingleStudio, covering HEX/RGB/slider synchronization, clamping and invalid input, 0�1 and 0�255 slider configurations, and focused text preservation. The input rendering was inspected for spacing and overlap. Full gameplay progression was not replayed.

## Backdrop paint isolation

The wall-both FBX combines the vertical backdrop and horizontal floor in one Screen mesh. `Assets/Studio/WallBackdropAndFloor.asset` preserves its vertices and triangles but separates them into two material slots: backdrop (0) and horizontal surfaces (1). The prefab uses this derived mesh; the original FBX stays unchanged. Paint updates the materials on Screen/backdrop renderers, recoloring both Screen material slots while leaving separate stands, pipes, clips, and frame untouched. Re-exporting the source FBX requires rebuilding this derived mesh to match any changed geometry.

Backdrop placement now raises the Screen floor to 0.025 world units above the supporting stage renderer. This corrects the old spawn anchor hiding the floor inside the raised platform, preserves horizontal placement/rotation, and applies to guided practice too. The floor mesh collider rises with it so props rest on the visible surface.

## Light-strip props

The Level 3 Terrari contract does not add a light strip to the Elements bank. It uses the warm Level 3 Soft Light and the existing camera. Hold **Ctrl** while moving the equipped camera with WASD and the mouse for smooth precision movement.

After raising the backdrop floor, each tripod is grounded independently. A downward ray finds the room floor or raised stage beneath that stand, ignoring the backdrop and movable props. The stand is stretched vertically around its top attachment so the feet touch the support while the rail connection stays in place.

Light strips now have three real-time, soft-shadowed point lights distributed along the bar. The sources follow tilt and share the RGB/HEX color. Select a strip and press F to cycle MEDIUM > HIGH > OFF > LOW. Range is 5 units per emitter. The diffuser does not cast shadows onto its own sources. The Level 3 contract introduction and Almanac teach rim lighting and warm/cool contrast; these are optional creative techniques, with no additional grading requirements.

