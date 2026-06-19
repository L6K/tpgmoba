# AetherRift: Respawn, Titan, and Defense Plaza Review

Reviewed: 2026-06-20

Scope: `AetherRift_Map`, `BuildAetherRiftMap.cs`, and the respawn, fountain, bot, shop, and minion code. This is a design/code review only; no game code or scene assets were changed.

## Requirement under review

Each base needs three visibly and mechanically separate areas:

1. A small, safe respawn fountain at the rear.
2. A distinct Titan as the final destruction objective.
3. A broad, readable 3v3 defense plaza on the lane-facing side of the Titan.

## Findings

### [P1] The Titan's front-side combat space is only about 4 m deep when measured to its visible ring

`PlaceTitan` places the Titan at radial distance 48 (`x = +/-48`) and gives it a 5.5 m-radius floor ring. The inner edge of `JungleLaneWalls` is radial distance 38.5. That leaves `48 - 5.5 - 38.5 = 4.0 m` of visually usable depth in front of the Titan. Even when the non-colliding floor ring is ignored, the physical capsule leaves only about 8.25 m before the wall.

The new 20-degree opening at radius 39.8 is only about 13.8 m wide (chord length). It is an improvement over a solid wall, but it is a choke, not a broad 3v3 defensive plaza. The Titan itself dominates the only front-facing staging area.

References:
- `Assets/Editor/BuildAetherRiftMap.cs:420-421` -- Titan centres.
- `Assets/Editor/BuildAetherRiftMap.cs:2211-2228` -- Titan capsule and 5.5 m floor ring.
- `Assets/Editor/BuildAetherRiftMap.cs:2996-3013` -- wall radius and 170-190 / 350-370 degree openings.

Recommendation: establish a target clear area before modeling. A practical minimum is a 10-12 m deep, 22-25 m wide front plaza after excluding the Titan silhouette/ring. Achieve this by moving the Titan rearward inside the base, shrinking its floor ring if necessary, and widening the front opening to roughly 32-36 degrees. The exact visual shape can then be built around that collision-free footprint.

### [P1] The fountain is still mechanically and visually a large base zone, rather than a compact safe point

The fountain centre is at `x = +/-64` with a 10 m healing radius. The visible ring uses the same 10 m outer radius. On a 17 m-radius base platform, one fountain covers most of the rear half of the base. It also leaves only 0.5 m between its visual edge (`64 - 10 = 54`) and the Titan's 5.5 m visual ring (`48 + 5.5 = 53.5`) on Blue side; the same geometry mirrors on Red.

The Titan and fountain are technically distinct objects, but their presentation reads as adjacent zones with almost no neutral transition. This is why the previous large pillar made the space feel like one combined spawn/objective.

References:
- `Assets/Editor/BuildAetherRiftMap.cs:611, 746-750` -- player spawn and fountain centre.
- `Assets/Editor/BuildAetherRiftMap.cs:1481-1500` -- 9.2-10 m fountain ring.
- `Assets/_Project/Scripts/Combat/FountainRegen.cs:11-13, 26-30` -- default 10 m healing rule.
- `Assets/Editor/BuildAetherRiftMap.cs:2211-2228` -- Titan geometry used for the spacing calculation.

Recommendation: reduce the fountain's mechanical and visual radius together (for example 4-5 m), use it as a clearly marked rear pad, and reserve the middle of the base for the Titan and the front for combat. Do not merely shrink the VFX ring: the `FountainRegen` radius must match the visual boundary.

### [P1] The shop overlaps both the fountain and the Titan, defeating the intended role separation

The shop is centred at `x = -56` with a 14 m range. Its range is therefore `[-70, -42]` on the Blue base: it includes the fountain centre at -64, the Titan at -48, and the entire front transition. A player can open the shop while standing at the Titan, so the shop zone visually/mechanically labels the whole base rather than the safe rear area.

References:
- `Assets/Editor/BuildAetherRiftMap.cs:931-938` -- builder sets shop centre to `(-56, 0, 0)`.
- `Assets/_Project/Scripts/UI/ShopController.cs:21-23, 105-110` -- centre and fixed 14 m range.

Recommendation: place the shop on the compact fountain pad and use the same small radius (or a slightly smaller one). This makes "respawn/heal/buy" one safe rear interaction area without turning the Titan plaza into a shop zone.

### [P2] Bot spawn locations use the edge of the current large fountain and will conflict with a smaller fountain

The three Red bots use `(63, 9)`, `(63, -9)`, and `(63, 4)` while the common fountain centre is `(64, 0)`. The top and bot spawns are about 9.1 m from the fountain centre, so they only fit because the radius is 10 m. The player is at the centre. Reducing the fountain to the requested compact safe point without moving bot spawn positions will make them respawn outside the healing area.

References:
- `Assets/Editor/BuildAetherRiftMap.cs:611` -- player spawn.
- `Assets/Editor/BuildAetherRiftMap.cs:840-856` -- bot spawn coordinates.
- `Assets/Editor/BuildAetherRiftMap.cs:2392-2397` -- bots share a centre at `(+/-64, 0)`.
- `Assets/Editor/BuildAetherRiftMap.cs:2441` -- this coordinate is also each bot's respawn point.

Recommendation: introduce named, tightly clustered spawn pads inside the new fountain radius, with modest z offsets that keep character controllers from overlapping. The bot spawn positions, `EnemyChampionAI._respawnPos`, and `FountainRegen` centre/radius should be changed as one unit.

### [P2] The current bot route bypasses the intended defense-plaza flow

After respawning, lane bots target their first lane waypoint at `( +/-45.5, +/-8 )`. There are no intermediate points for "fountain exit -> behind Titan -> Titan-front plaza -> lane entrance." Their route passes along a side of the Titan, which is adequate for getting to lane but does not make the defense plaza a deliberate regroup/retreat space.

References:
- `Assets/Editor/BuildAetherRiftMap.cs:2447-2452` -- bot route assignment.
- `Assets/Editor/BuildAetherRiftMap.cs:2464-2500` -- first top/bottom lane points.
- `Assets/_Project/Scripts/Characters/EnemyChampionAI.cs:726-751, 1070-1075` -- respawn resets movement to waypoint zero and follows the supplied list.

Recommendation: add base-internal route points only after the physical layout is finalized. They should guide bots through the back fountain exit and Titan-front plaza, not cross the Titan silhouette or cut directly to an outer-lane point.

### [P2] Minion waves do not intentionally enter the Titan plaza or target the Titan

Lane waves end at `(+/-50, +/-8)`, while the defending Titan is at `(+/-48, 0)`. After the final waypoint, `MinionAI` simply stops following the route; it has no objective-specific continuation. Whether it happens to aggro a Titan then depends on the generic overlap scan rather than a deterministic base-siege route. This cannot guarantee a clear final-objective phase.

References:
- `Assets/Editor/BuildAetherRiftMap.cs:2730-2764` -- minion route endpoints.
- `Assets/Editor/BuildAetherRiftMap.cs:420-421` -- Titan positions.
- `Assets/_Project/Scripts/Minions/MinionAI.cs:220-235` -- movement ends once the waypoint list is exhausted.
- `Assets/_Project/Scripts/Minions/MinionAI.cs:152-154` -- targeting is generic `Physics.OverlapSphere` aggro.

Recommendation: once a lane's final defense condition is cleared, route minions to an explicit Titan-front staging point and then the Titan. This keeps the plaza meaningful and makes the win condition testable rather than accidental.

## What is already improved

The latest change did fix the prior forward-respawn problem: bots now respawn near `x = +/-63`, and their shared healing centre is at `x = +/-64`. The base-facing `JungleLaneWalls` now has an opening at 0/180 degrees. The issue is not that these changes are absent; it is that their dimensions still implement a large spawn zone and a narrow Titan choke, which is the inverse of the current requirement.

## Validation after a future layout change

1. Measure the collision-free Titan-front plaza with six champions present; no character should start inside the fountain/shop range.
2. Verify player and every bot respawn inside the small fountain, then can leave without clipping the Titan or each other.
3. Verify shop opens only inside the rear fountain pad.
4. Run a wave through a completed lane and confirm it enters the plaza and attacks the Titan reliably.
5. Check both bases as mirrored layouts, including top and bottom exits.
