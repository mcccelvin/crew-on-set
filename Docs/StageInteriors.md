# Director Tablet interiors

Current flow: BUY SET saves ownership per career (OwnedInterior.0/1/2); USE SET places one owned set. CHOOSE SET remains available to switch for free. Preview/Cancel restores the previous active set. Clear Stage does not remove ownership. Coffee Interior replaces Living Room at 2,750 B and uses ProductModels.coffeeInterior; Cafe Corner stays procedural. The CoffeeInterior FBX still has no renderers, so Coffee Interior temporarily uses café furniture until the source mesh is re-exported. Earlier levels retain their original wall purchasing behavior.

From Level 4, CHOOSE SET opens a picker using the tablet's existing button art and font:

- Plain Backdrop: 500 B-Coins.
- Cafe Corner: 2,000 B-Coins, including a counter, coffee-machine decoration, stools and plant.
- Living Room: 2,750 B-Coins, including a sofa, side table, decorative lamp and plant.

The right-side picker previews each set on the actual stage, with the same paint and alignment as the purchase. Preview colliders are disabled and it does not count as a purchased wall. BUY SET charges once and commits the selection; changing previews, Cancel and closing the tablet discard it free. Only one set is purchased at a time. Cancel is free; insufficient funds leaves the picker open. CLEAR STAGE removes the set and props, allowing another selection. Earlier levels retain ADD WALL.

These are simple furnished studio sets built on wall-both, not imported room assets. Their front/centre stays open for filming. Furniture fits the backdrop footprint and retains its own colors when wall/floor paint changes. Furniture is not a graded product or actor; players still place the commercial product, hire an actor and arrange lighting. The Level 4 lesson and Almanac explain CHOOSE SET.

Validation: editor/player scripts compiled. Isolated Play Mode checked picker options/cancel, insufficient funds, exact fees, both furnished sets, backdrop fit, paint isolation, subject counts and Clear Stage. Rendered previews of both interiors were inspected. Full manual studio play-through remains unverified.



Cafe Corner now uses Assets/Product/CoffeeInterior.fbx through ProductModels.asset, preserving preview, purchase and clearing. Imported furniture has individual mesh colliders and no product/actor grading markers.
CoffeeInterior.fbx currently imports without renderers (4,124-byte export). Until re-exported with mesh geometry, Cafe Corner retains its existing furnished fallback. The asset reference is wired for the corrected FBX; preserve its .meta GUID.
