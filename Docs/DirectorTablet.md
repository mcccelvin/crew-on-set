# Director tablet stage and color controls

Default ADD WALL uses `Assets/Studio/wall-both.prefab` in SingleStudio and MultiStudio, including guided wall practice. The prefab retains the previous wall's 0.95 scale and uses a non-convex mesh collider on the screen panels so the stage interior stays open. Selecting a panel selects the complete wall for color changes. Spawn, three-panel collision, open front, color, selection, clear, and practice cleanup passed an isolated Unity Play-mode check.

Level 3 uses the same **ADD WALL** action as the other levels. It spawns the supplied wall-both asset for 500 B-Coins. There is no automatic or tablet-generated showroom preset. The player chooses the backdrop color, places the car, and arranges the lighting to follow the contract. **CLEAR STAGE** removes the wall and props. The contract, tutorial, and handbook describe this manual setup.

The RGB readouts beside the sliders are now editable integer fields. Enter **0–255** and press Enter or click elsewhere to apply. Values outside this range are clamped. The HEX field accepts six digits with an optional `#`, plus three-digit shorthand (for example, `#F60`). Invalid input restores the previous valid color. Inputs and sliders synchronize, and live refresh does not overwrite a field while it is being edited. Editing a field suspends the tablet's object movement/selection shortcuts.

Color editing follows the selected target and existing tutorial permissions. Vehicle paint changes only the car body. Imported packaged-product colors remain protected.

## Verification

Editor and player source compilation passed with Unity 2022.3.62f3. Earlier input checks used the actual DirectorUI hierarchy and font/texture assets from SingleStudio, covering HEX/RGB/slider synchronization, clamping and invalid input, 0�1 and 0�255 slider configurations, and focused text preservation. The input rendering was inspected for spacing and overlap. Full gameplay progression was not replayed.

## Backdrop paint isolation

The wall-both FBX combines the vertical backdrop and horizontal floor in one Screen mesh. `Assets/Studio/WallBackdropAndFloor.asset` preserves its vertices and triangles but separates them into two material slots: backdrop (0) and horizontal surfaces (1). The prefab uses this derived mesh; the original FBX stays unchanged. Paint updates the materials on Screen/backdrop renderers, recoloring both Screen material slots while leaving separate stands, pipes, clips, and frame untouched. Re-exporting the source FBX requires rebuilding this derived mesh to match any changed geometry.

Backdrop placement now raises the Screen floor to 0.025 world units above the supporting stage renderer. This corrects the old spawn anchor hiding the floor inside the raised platform, preserves horizontal placement/rotation, and applies to guided practice too. The floor mesh collider rises with it so props rest on the visible surface.

## Light-strip props

From Level 3 onward, the Elements bank includes **LIGHT STRIP** (900 B-Coins; car hire is 2,500 B-Coins). Click to place, then select and use **T** to reposition, **R** to cycle upright/diagonal/horizontal, and **Q/E** to turn in 15-degree steps. These keys also work during placement. RGB/HEX changes the diffuser color and emission while preserving the base and stem. Right-click removes a strip; CLEAR STAGE clears purchased strips. The strip casts real accent light and does not count as a car, product, or required Level 3 Soft Light.

After raising the backdrop floor, each tripod is grounded independently. A downward ray finds the room floor or raised stage beneath that stand, ignoring the backdrop and movable props. The stand is stretched vertically around its top attachment so the feet touch the support while the rail connection stays in place.

Light strips now have three real-time, soft-shadowed point lights distributed along the bar. The sources follow tilt and share the RGB/HEX color. Select a strip and press F to cycle MEDIUM > HIGH > OFF > LOW. Range is 5 units per emitter. The diffuser does not cast shadows onto its own sources. The Level 3 contract introduction and Almanac teach rim lighting and warm/cool contrast; these are optional creative techniques, with no additional grading requirements.

