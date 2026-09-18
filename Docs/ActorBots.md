# Actor bots

Level 4 and 5 Actor cards now hire the existing rigged Character/Models/Armature model. Its idle animation supplies breathing; humanoid muscle animation adds repeating Wave (greeting) and Action (product presentation) performances. Neutral, Wave and Action retain their existing grading identifiers.

Default tiers use ProductionEconomy.ActorBase (750) multiplied by 1, 3 and 6:

| Actor | Tier | Default hire fee | Delivery |
|---|---|---|---|
| A | Rookie | 750 B-Coins | Smaller gestures, slower rhythm with pauses |
| B | Trained | 2,250 B-Coins | Smoother timing, clearer gestures |
| C | Expert | 4,500 B-Coins | Fluid, more expressive gestures and attention |

The same pricing function drives bank labels and actual charges. All tiers can satisfy the contract; higher prices buy visual polish rather than an automatic grading bonus.

Use POSE ACTOR to cycle the performance, T to reposition and R to turn in 15-degree steps. Bots remain on their mark unless a walk is configured. Select the actor, place START and press B; move with T to END and press N. K rehearses the straight walk, J resets to START, and H clears the route. Recordings restart the route automatically. The actor stops at END or an obstacle; route planning is manual, without automatic pathfinding. The Director Megaphone is a reusable Level 4 shop item: aim at an Actor and press LMB to select them, then use Z/X/C for Neutral/Wave/Action, the arrow keys to nudge, R to turn and B/N/K/J/H for walk marks and rehearsal without opening the tablet. Action turns their attention toward the campaign product. Starting a camera recording resets the performance clock, so takes begin consistently. Pausing freezes the animation. Materials and animation graphs are released when actors are removed.

The model is normalized to about 1.85 units tall with its feet on the stage. If the humanoid model cannot load, the existing cube actor remains the fallback. No player controller, navigation or multiplayer components are attached to the NPC.

Validation: isolated Unity Play Mode checks and rendered previews cover all three humanoid tiers, grounded scale, animated gestures, stable marks/continuity, take reset, actual hire charges and insufficient funds. Editor/player compilation is also checked. A complete manual production play-through remains unverified.


Megaphone pickup: purchasing activates the existing director-table prop. Restores count ActorMegaphoneItem instances, including inactive hotbar slots. Picking it up selects its slot automatically. Contract 4 explains buying, E pickup, hotbar selection, LMB targeting and actor commands in Boss dialogue before offering the contract; hands-on practice follows set setup.

Contract 4 uses ProductModels.coffeeActors (Assets/Product/coffee_actors.fbx), selecting one character per tier with nearest-body clothing and original materials. The FBX has no skeleton: movement, turning, walk marks and take reset work, but walking slides and Wave/Action cannot animate limbs. Other contracts retain humanoid actors. CoffeeInterior.fbx is already wired through ProductModels.coffeeInterior.

Coffee actor clothing: static meshes spanning multiple characters are partitioned by triangle proximity to fixed body centres, retaining material slots. The FBX requires Read/Write enabled. Generated meshes are destroyed with ActorBot. Static source actors still lack a humanoid rig for limb gestures.
