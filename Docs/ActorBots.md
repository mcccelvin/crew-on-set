# Actor bots

Level 4 and 5 Actor cards now hire the existing rigged Character/Models/Armature model. Its idle animation supplies breathing; humanoid muscle animation adds repeating Wave (greeting) and Action (product presentation) performances. Neutral, Wave and Action retain their existing grading identifiers.

Default tiers use ProductionEconomy.ActorBase (750) multiplied by 1, 3 and 6:

| Actor | Tier | Default hire fee | Delivery |
|---|---|---|---|
| A | Rookie | 750 B-Coins | Smaller gestures, slower rhythm with pauses |
| B | Trained | 2,250 B-Coins | Smoother timing, clearer gestures |
| C | Expert | 4,500 B-Coins | Fluid, more expressive gestures and attention |

The same pricing function drives bank labels and actual charges. All tiers can satisfy the contract; higher prices buy visual polish rather than an automatic grading bonus.

Use POSE ACTOR to cycle the performance, T to reposition and R to turn in 15-degree steps. Bots remain on their mark unless a walk is configured. Select the actor, place START and press B; move with T to END and press N. K rehearses the straight walk, J resets to START, and H clears the route. Recordings restart the route automatically. The actor stops at END or an obstacle; route planning is manual, without automatic pathfinding. Action turns their attention toward the campaign product. Starting a camera recording resets the performance clock, so takes begin consistently. Pausing freezes the animation. Materials and animation graphs are released when actors are removed.

The model is normalized to about 1.85 units tall with its feet on the stage. If the humanoid model cannot load, the existing cube actor remains the fallback. No player controller, navigation or multiplayer components are attached to the NPC.

Validation: isolated Unity Play Mode checks and rendered previews cover all three humanoid tiers, grounded scale, animated gestures, stable marks/continuity, take reset, actual hire charges and insufficient funds. Editor/player compilation is also checked. A complete manual production play-through remains unverified.

