# Product models

`Assets/Resources/ProductModels.asset` references the user's original FBX files under `Assets/Product/drive-download-20260911T074145Z-1-001`.

| Level | Product | Model | Placement size |
| --- | --- | --- | --- |
| 1 | Artisan vase | FlowerBase.fbx (currently empty; existing Flower prefab remains in use) | Existing prefab |
| 2 | Goke | Coke.fbx | 1 unit tall |
| 3 | Lambormini | Car.fbx | Roof = 70% of standing player height |
| 4 | Kape Kultura | Artisan Coffee Brand.fbx | 1.3 units tall |
| 5 | Haraya | Perfume.fbx | 1.1 units tall |

Level 5's vehicle option uses the same imported car. Actor and practice-cube props are unchanged.

The catalog normalizes the actual rendered bounds, centers the product horizontally, grounds its bottom, and adds one selection collider. Camera and contract markers retain their existing meanings. Imported cameras, lights, animation, and mesh colliders do not take over gameplay. Car body paint starts orange for the Lambormini brief; its other materials retain their imported appearance. Packaged product color controls are disabled to preserve their authored materials and labels.

`FlowerBase.fbx` currently imports with zero renderers and no mesh. Re-export it with the vase/flower mesh included and replace the file while retaining its `.meta`; the catalog reference will then use the model automatically. Until then, Level 1 uses its existing visible vase.

Restart Play mode and place fresh products to see the replacements. Existing objects in an already running scene are not migrated.

## Verification

Unity 2022.3.62f3 Editor and player compilation passed. An isolated Unity Play-mode regression loaded the actual serialized catalog and original FBX assets and exercised the real Director spawning methods for levels 2–5. Checks passed for mesh identity, normalized bounds, ground contact, subject/contract markers, material preservation, car material isolation, dragging with a destroyed cached collider, disabled-collider preservation, dropping, raycast selection, repositioning, and clearing. The empty Level 1 export correctly returned to the existing prefab path. Rendered previews of all four products were inspected.

The repositioning regression sets the same selected-object drag state directly; it does not simulate the T key or replace an end-to-end manual playthrough.

The car now derives its roof height from the active player CapsuleCollider (including world scale), with 1.94 units as the no-player fallback. At the current character scale the roof is 1.358 units high, preserving the FBX proportions. The catalog length value is only a fallback display setting; vehicle runtime scale uses height.

The existing Level 1 Flower prefab now uses `ColorfulFlower.asset` and dedicated green, coral, gold, pink, outline, and teal-vase materials. The derived mesh preserves the imported geometry and separates its color regions into material slots. Director spawning no longer overwrites this prefab with white. The original FBX and camera subject marker are retained.
