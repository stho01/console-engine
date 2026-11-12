# EternalDigger: feature handover

7 Nov 2026 · Robin Hovind

## Purpose and build order

Fix miner visibility and add saving first, then build the accelerating climb, the elevator and one dynamite wall at ~110 m; everything else hangs off those. Playtesting shows the game runs out at about 100 m and 3000 gold, for two reasons:

- **Nothing left to want.** Gold piles up with only ladders to buy, and below ~80 m ore density levels off while toughness keeps rising, so deeper is strictly worse.
- **The climb home is a chore.** At 100 m it is 15–20 s of holding Up with no decisions.

Tier 0 covers foundations the game needs regardless. The rest are ranked into three tiers. Tier 1 fixes both problems and tests the "second act" structure. Tier 2 makes each trip a decision. Tier 3 adds variety and long-term progression once the structure is proven.

| Tier | Feature | Fixes | Size |
| --- | --- | --- | --- |
| 0 | Saving | Progress now spans several sessions | Medium |
| 0 | Keep the miner visible | Miner hidden by particles while digging | Small |
| 1 | Accelerating climb | Chore climb | Tiny |
| 1 | Elevator | Chore climb, gold sink | Medium |
| 1 | Dynamite wall + first new layer | Nothing to want, flat depth | Large |
| 2 | Lamp oil | Trips have no turnaround point | Medium |
| 2 | Upgrade shop | Gold sink, toughness curve | Medium |
| 2 | New deep materials and rare finds | Depth doesn't pay | Small per item |
| 2 | Bag weight | No reason to go home | Small |
| 3 | Hazards: gas and darkness | No danger | Medium each |
| 3 | Deep camp (built alongside the elevator) | Chore climb, deep trading | Medium |
| 3 | Surface progression | No sense of history | Medium |
| 3 | Repeating acts | Endless mine has no structure | Large, content-driven |

## Engine constraints

Every feature here must work with these rules; each spec below says which characters and colours it uses.

- **Text only.** Everything is characters: letters, symbols, box drawing (`─│┌╔╟`), blocks (`▀▄█▓▒░`), and a few glyphs (`☻◆•`).
- **16 fixed colours.** No new colours can be added. A new layer or effect must read through palette choice and character choice alone.
- **Grid of square tiles,** 30 wide, one screen wide, vertical scroll only. The miner occupies one tile and moves tile by tile.
- **Tiny sprites.** The miner is 4 × 2 characters. Any new object (elevator car, NPC, shop) should stay in that size range; surface buildings can be larger.
- **Particles are single characters,** and hundreds at once are fine.
- **Keyboard only:** arrows, Space, E, R, Esc. New features should reuse these keys. Where a new key is unavoidable, it is called out below.
- **No sound.** All feedback is visual: colour, flicker, particles, shake, floating text.

## Controls

No new keys during play; new actions reuse E, Up and Down by context. Rows marked new or changed are what the coder adds.

| Key | Where | Action | Status |
| --- | --- | --- | --- |
| ← → (hold) | Anywhere | Walk, or dig if rock | Unchanged |
| ↓ (hold) | Anywhere | Dig below, or step down | Unchanged |
| ↑ (hold) | On a ladder | Climb; speeds up the longer it is held | Changed |
| ↑ / ↓ | In the elevator car | Ride up / down | New |
| ↑ / ↓ | At a shop counter (surface or deep camp) | Cycle items: ladders, oil, upgrades, blasting kit, dynamite | New |
| E | At the door or deep camp | Sell the bag | Unchanged / new at camp |
| E | At a shop counter | Buy the selected item | Changed |
| E | On the elevator column | Call the car / board | New |
| E (hold 0.5 s) | Underground, elsewhere | Plant and light a dynamite stick in the tile you face | New |
| Space | Underground | Place a ladder | Unchanged |
| R (hold 1.5 s) | Underground | Rescue, bag is lost | Unchanged |
| ↑ / ↓ + E | Title screen | CONTINUE / NEW MINE | New |
| Esc | Anywhere | Save and quit | Changed |

## Tier 0: foundations

### Saving

The game saves automatically; there is no save key.

- **When:** on every sale or purchase at the surface or the camp, and on Esc. Optionally every 60 s underground.
- **What:** seed, gold, ladders, oil, upgrades, dynamite stock, elevator depth, camp built, trophies and rare finds, depth record, acts unlocked, miner position and bag.
- **The mine:** store the seed plus a list of changed tiles (dug, blasted, ladder placed, remembered/seen) instead of the whole mine.
- **Title screen:** a small menu, `CONTINUE` / `NEW MINE`, picked with Up/Down and E. NEW MINE asks for confirmation if a save exists. One save slot to start.
- **Feedback:** a small dark-grey "SAVED" fading in the HUD's second row.

### Keep the miner visible

Today the debris, dust and sparks from digging are drawn over the miner and hide him. He must stay readable on every frame.

- **Draw order:** particles render under the miner sprite, or are skipped on his 4 × 2 cells.
- **Debris direction:** chunks still fly towards the miner, but land or fade before reaching his cells.
- **Density cap:** limit particles within 1 tile of the miner per frame; push the excess further out.
- **Contrast:** the face `☻` and the headlamp stay the brightest characters around him.
- **Acceptance test:** holding a dig key on the toughest tile, and during a dynamite blast, the miner's face and lamp are visible every frame.

## Tier 1: fix the 100 m wall-out

### 1. Accelerating climb

Holding Up on a ladder speeds up the climb, the same way falling already does.

- Start at the current 0.17 s per tile; after ~4 tiles of continuous climbing, ramp down towards ~0.06 s per tile. Releasing Up or stepping off a ladder resets the speed.
- Short climbs feel unchanged; a 100 m climb drops from ~17 s to roughly 7–8 s.
- **Visuals:** at top speed, trail 1–2 dust particles (`·` `'`) below the miner each tile, and swing the legs animation faster.

### 2. Elevator

A buyable lift in the mine shaft, built in segments, and the main gold sink for 1000–3000 gold.

- **Placement:** runs down the existing mine shaft column (or a dedicated column next to it). The first purchase builds 10 m; each further segment adds 10 m.
- **Cost:** rises with depth, e.g. 200g for the first segment, +100g per segment after. Bought at a new sign by the mine entrance with E.
- **Use:** stand on the elevator column and press E to call the car; press Up/Down to ride; it stops only at the top and bottom (optional: at each 10 m landing). Riding speed ~0.04 s per tile.
- **Deeper than the elevator:** you still need ladders below its bottom landing.
- **Visuals:** shaft walls `║` `║` in dark grey with `╪` cross-braces every 5 tiles; the car is a 4 × 2 cage like `╔══╗` / `╚══╝` in brown/yellow around the miner; a cable `│` above it. When riding, speed lines `¦` stream past the shaft walls.
- **Feedback:** "ELEVATOR +10m" floating text and a ring of stars on purchase; the HUD shows elevator depth next to ladders.

### 3. The wall and the first new layer

At about 110 m, a band of rock the shovel cannot break, and a new layer below it.

- **The wall:** a 3–5 tile thick band of **obsidian** across the full width. The shovel does no damage: a dull clank, sparks, and a floating "TOO HARD" message.
- **The key: dynamite.** A consumable. Unlock the blasting kit at the surface shop for ~2500g; sticks then cost ~100g each (also sold at the deep camp at a markup). Breaking through the first wall should cost roughly 3000g in total.
- **Using it:** hold E for 0.5 s underground to plant a stick in the tile you face (last move or dig direction). The fuse burns 2 s, so walk away. The blast clears a 3 × 3 area of any rock, obsidian included. A 4-tile wall needs about 4–6 sticks. Dynamite works anywhere in the mine, as a fast but costly way to dig.
- **Forgiving blasts:** the miner is never hurt; if inside the radius he is pushed one tile back with heavy shake. Ladders inside the radius are destroyed. Ore inside the radius goes straight into the bag.
- **Visuals:** a red stick `¡` stuck in the rock; the fuse spark `*` flickers yellow/red with `·` smoke. The blast: one frame of full-tile white/yellow `█` flash, then a ring of `▓▒░` debris, hundreds of particles, big shake, and dust lingering 1–2 s. The miner stays drawn on top (Tier 0).
- **Hinting:** from ~95 m, show occasional glints in a new colour below the wall, so the player sees something worth reaching before they find it.
- **Below the wall (act 2 layer):** new rock and a new ore only, for now (see Tier 2). It should feel like a new world from colour alone.
- **Rock visuals:** obsidian as `▓` in dark magenta or black-on-dark-grey with occasional `▪` highlights; act 2 rock in dark blue/dark cyan shades so the transition is obvious on screen.
- **Build one wall and one layer first** to test the "second act" feeling before building more (see Repeating acts in Tier 3).

## Tier 2: make every trip a decision

### 4. Lamp oil

The headlamp burns oil; running low shrinks the light. It never kills you.

- **Tank:** e.g. 100 units; burns 1 unit per ~3 s underground, nothing on the surface. Refill at the shop (e.g. 2g per unit, or "FILL LAMP [E]" for the whole tank).
- **Below 30%:** the cone shortens from 8 tiles towards 3, and flickers (random frames at reduced brightness).
- **Empty:** no cone, only the 1–2 tile close radius, dimmed. Remembered grey outlines and glints still show, so the player can navigate home by memory, or hold R.
- **HUD:** an oil gauge, e.g. `OIL ▮▮▮▮▮▯▯▯`, turning yellow then blinking red when low.
- **Visuals:** occasional single-character smoke `°` rising from the lamp when low; the lamp glyph dims to dark yellow.

### 5. Upgrade shop

Three or four upgrade lines, each with 3 tiers, sold at the existing shopfront (or a second counter beside it).

| Upgrade | Effect per tier | Suggested price (g) |
| --- | --- | --- |
| Shovel | −1 hit per tile (min 1), or −15% dig time | 300 / 900 / 2500 |
| Lamp | +2 tiles cone reach, −20% oil burn | 250 / 750 / 2000 |
| Bag | +capacity or −weight penalty (see Bag weight) | 200 / 600 / 1500 |
| Detector | +3 tiles glint radius, more frequent glints | 400 / 1200 / 3000 |

- **Controls:** at any counter, Up/Down cycles the items (ladders, oil, upgrades, blasting kit, dynamite) and E buys the selected one. A small pixel-art panel above the counter shows the item, tier pips `●●○` and price (red if unaffordable). This replaces today's single "BUY LADDERS [E]" prompt.
- **Visuals:** small sprite changes per upgrade where possible: a brighter shovel colour per tier, a brighter lamp colour at higher lamp tiers, and a red `¡` on the belt while carrying dynamite.

### 6. New deep materials and rare finds

Give depth a reason: new things to find, not just higher numbers.

- **New ore below the wall,** e.g. **crystal/gem** at 250g, glinting in a new colour (bright magenta or bright white) so it is recognisable instantly.
- **Rare finds:** single, hand-placed or very rare tiles: fossils, an old lantern, a previous miner's pickaxe. Each one either sells high or goes to the trophy shelf at the house (see Surface progression).
- **Visuals:** rare finds get their own glyph (e.g. `§` fossil, `¥` relic) and a distinct slow pulsing glint; breaking one triggers a bigger star burst and a floating name like "FOSSIL!".
- **HUD:** add the new ore to the bag line; rare finds as a separate `✦1` counter.

### 7. Bag weight

A heavy bag slows you down, so big hauls must be carried home.

- Each ore has a weight (copper 1, silver 1, gold 2, gems 2). Above a threshold (e.g. 20), climbing slows by a step per 10 extra weight; walking is unaffected.
- If oil is in, a heavy bag also burns oil faster while climbing.
- No hard cap, so the rescue (lose the bag) stays painful in proportion to greed.
- **HUD:** bag value turns yellow when heavy; the miner's backpack glyph swaps to a bulging version (e.g. `▓` instead of `▒`).

## Tier 3: variety and long-term progression

### 8. Hazards

Gas and darkness, for a moody but forgiving tone. Neither causes death; the worst outcome is lost light, and a rescue if the player gives up.

- **Gas pockets:** appear in caves as faint green shimmer particles (`~` `∙` in dark green). Bringing the lamp within 2 tiles makes them flare (`*` burst in yellow/red, screen shake) and blows out the lamp for a few seconds or drains some oil. A dynamite blast near gas makes a bigger flare but still does no harm.
- **Darkness:** builds on lamp oil. Deeper layers make light scarcer: act 2 rock absorbs light (cone reach −2 tiles), and rare "black damp" caves halve the cone and double oil burn while inside. Glints and remembered outlines always still show, so the way home is never fully lost. Visuals: black-damp caves have no mist, and their edges show as dark-grey `░` when lit.
- **Not planned for now:** cave-ins and water (harsher than the chosen tone). Fall damage stays out.

### 9. Deep camp

Built alongside the elevator: a trading post deep in the mine.

- A small structure (e.g. 4 × 3 characters: a tent `/▀\` with a lantern `¤`) at a fixed depth just above the first wall (e.g. 100 m), bought at the surface shop (e.g. ~1500g).
- Sells ore at ~70% of surface prices, and sells ladders, oil and dynamite at a markup. Same counter controls as the surface: Up/Down to pick, E to buy or sell.
- The elevator's bottom landing ends at the camp once it reaches that depth. The elevator is the fast trip home; the camp lets the player skip the trip.

### 10. Surface progression

The surface reflects how far you've come.

- **Trophy shelf:** rare finds appear in the house window or on a shelf next to the door, one glyph each.
- **Depth record sign** on the mine entrance: "RECORD 112m".
- **House upgrades** at gold milestones: second floor, flag on the roof, a lit window at night (if a day/night cycle is added).
- **New shops/NPCs** unlock after milestones, e.g. a tool shop that opens after the first wall is broken. NPCs at miner sprite size (4 × 2), one or two idle animation frames.

### 11. Repeating acts

Once one wall + layer works, repeat the pattern: each act ends with a wall, each wall needs a big purchase, each layer below has its own palette, ore and hazard.

| Act | Depth (approx) | Rock look | New ore | Hazard | Wall to enter |
| --- | --- | --- | --- | --- | --- |
| 1 (current) | 0–110 m | Brown dirt, grey stone, granite | Copper, silver, gold | None (lamp oil) | — |
| 2 | 110–250 m | Dark blue/cyan crystal rock | Gems | Gas, light-absorbing rock | Obsidian, dynamite |
| 3 (later) | 250–400 m | Red/dark red hot rock | TBD | Black damp darkness | TBD |
| 4 (later) | 400 m + | TBD | TBD | TBD | TBD |

Depths, ore and hazard pairings are suggestions to tune in playtesting.

## Asset list

Every asset is a character-and-colour design from the 16-colour palette; glyphs below are suggestions, not final.

| Asset | Feature | Size (chars) | Suggested glyphs / colours |
| --- | --- | --- | --- |
| Title menu: CONTINUE / NEW MINE | Saving | text | white, selected item highlighted |
| "SAVED" HUD text | Saving | text | dark grey, fades |
| Miner sprite readability pass | Keep the miner visible | 4 × 2 | face `☻` and lamp brightest |
| Climb dust trail | Accelerating climb | 1 | `·` `'` grey |
| Elevator shaft walls + braces | Elevator | 1 per tile | `║` `╪` dark grey |
| Elevator car + cable | Elevator | 4 × 2 | `╔══╗` `╚══╝` brown/yellow, `│` grey |
| Elevator sign at mine entrance | Elevator | ~6 × 2 | "LIFT" plank, brown/white |
| Speed lines | Elevator | 1 | `¦` grey |
| Obsidian wall tile | The wall | 1 | `▓` dark magenta / black, `▪` highlight |
| "TOO HARD" prompt | The wall | text | red, bobbing |
| Dynamite stick in rock | The wall | 1 | `¡` red |
| Fuse spark + smoke | The wall | 1 | `*` yellow/red flicker, `·` grey |
| Blast flash + debris ring | The wall | 3 × 3 + particles | `█` white/yellow, `▓▒░` |
| Dynamite on the belt | The wall | swap 1 char | `¡` red |
| Act 2 rock tiles (2–3 variants) | New layer | 1 | dark blue / dark cyan shades |
| Oil gauge | Lamp oil | HUD | `▮▯` yellow → red |
| Lamp smoke | Lamp oil | 1 | `°` grey |
| Counter panel with item, tier pips, price | Upgrade shop | ~12 × 3 | `●○`, red when unaffordable |
| Gem ore tile + glint | New materials | 1 | bright magenta or bright white |
| Rare find tiles | Rare finds | 1 each | `§` fossil, `¥` relic, slow pulse |
| Bulging backpack | Bag weight | swap 1 char | `▓` |
| Gas shimmer + flare | Hazards | 1 | `~` `∙` dark green; `*` yellow/red |
| Black damp cave edges | Hazards | 1 | `░` dark grey |
| Deep camp tent + counter | Deep camp | 4 × 3 | `/▀\`, `¤` lantern |
| Trophy shelf, record sign | Surface progression | varies | wood brown, white text |
| House upgrade states | Surface progression | house size | per milestone |
| NPC shopkeepers | Surface progression | 4 × 2, 2 frames | distinct from miner colours |
| Floating texts | All | text | "ELEVATOR +10m", "FOSSIL!", "TOO HARD" |

## Decisions

Decided on 7 Nov 2026:

- **Saving:** needed, added as Tier 0.
- **Elevator and deep camp:** both; the camp sits at the elevator's bottom landing.
- **Deep camp:** bought at the surface shop.
- **Wall tool:** dynamite, usable anywhere in the mine.
- **Ore in a blast:** goes into the bag.
- **Tone:** moody and forgiving; hazards are gas and darkness.
- **Below the first wall:** new rock and ore only, for now.
- **Shop controls:** Up/Down to pick, E to buy.

Left for later: what lies below the act 3 and act 4 walls, to be designed after act 2 has been playtested.

All prices and depths in this doc are starting points, to be tuned in playtesting.
