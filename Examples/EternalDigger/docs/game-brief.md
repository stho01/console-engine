# EternalDigger: game brief

*A self-contained description of the game as it currently plays, written for design conversations. No code or tech,
only what a player and a designer need. Everything here is implemented and working unless it says otherwise.*

## The pitch

You are a miner with a shovel and a headlamp. Under your little house there is a mine that goes down forever. Dig
down, gather copper, silver and gold, haul it back to the surface, sell it, and buy ladders, because ladders are the
only way back up. The deeper you go, the richer the ore and the harder the rock, and the more ladders it costs to get
home.

The look is chunky retro pixel art made of text characters, with lots of small reactive effects: flying debris,
dust, sparkling ore, drifting cave mist, cascading coins.

## How it looks

The game is drawn with text characters in 16 colours, like an old terminal game made pretty. The screen is 30 tiles
wide and about 23 tiles tall; the whole mine is exactly one screen wide, and the view scrolls only up and down,
following the miner.

The start screen, without colours:

```text
 ETERNAL DIGGER   GOLD 0g   LADDERS 8   DEPTH 0m   BAG ◆0 ◆0 ◆0 = 0g
 ←→↓ walk/dig  ↑ climb  ↑+←→ jump  SPACE ladder  E use  hold E+arrow dynamite  TAB inventory  hold R rescue  ESC quit
        ▄▄▀▄▀▄▀▄▀▄▀▄▀▄▀▄▀▄▀▄▀▄▀▄
         ─── EternalDigger ─────
            ▀             ▀     ▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄                                    MINE
          ┌┬┐     ▄▄▄▄    ┌┬┐      ╟─╢ ╟─╢                                         ╔════╗
         ─└┴┘─────║║║║────└┴┘───   ╟─╢ ╟─╢                                         ║    ║
                  ║║║•                                                             ║    ║
         ─────────║║║║────────── ▀▀▀▀▀▀▀▀▀▀▀▀▀▀              ☻•                    ║    ║
                  ║║║║              LADDERS                 ▐█/                    ║    ║
▀▀▀▓▓▓▀▀▀▀▀▀▓▀▀▓▓▀▓▓▓▀▓▀▓▀▀▀▓▓▓▀▀▓▀▀▀▀▀▀▀▀▓▀▓▓▀▀▀▀▓▀▓▀▀▀▀▀▀▀▀▓▀▀▓▀▀▀▀▀▓▀▀▓▀▀▀▀▓▀▓▀▀▀╟──╢▀▀▓▀▀▀▓▀▀▀▓▀▓▀▓▀▓▀▀▓▀▀▀
·▒·▒░ ░·▒ ·░▒░ ·▒░▒▒··░▒▒▒░▒░▒░▒░·░░░▒·▒··▒░·▒·▒▒░ ░ ░ ░ ▒▒▒▒░▒▒▒·░░▒· ▒·░   ░░░▒░▒▒╟──╢░▒ ░░░ ░ ▒· ▒░·▒░▒░
                                                                                ░░ ░╟──╢░▒▒▒
                                                                                    ░░░░
```

- **The surface** is sunny: a blue-to-cyan sky, a sun, slowly drifting clouds, green hills on the horizon.
- **The house** (left) has a red shingle roof, a smoking chimney, plank walls with windows, a door, and a wooden sign
  reading "EternalDigger". Attached is a **ladder shop** with a red-and-white striped awning, ladders on display
  and a "LADDERS" board.
- **The mine entrance** (right) is a wooden frame with a "MINE" plank over a shaft with ladders going down.
- **The miner** is 4 characters wide and 2 tall: backpack, smiley head, a glowing headlamp, blue overalls, animated
  legs, and a shovel that swings when digging.
- **Underground is dark.** You only see what daylight (near the surface) or your headlamp lights.

## The core loop

1. **Dig.** Walk into rock to dig it. Every tile takes a number of shovel hits.
2. **Gather.** Breaking an ore tile drops one piece of ore into your bag.
3. **Survive the way back.** Digging down leaves vertical holes you can't climb out of on your own. Place ladders
   as you go.
4. **Deliver and buy.** Back at the house, sell the bag at the door, buy ladders and more at the shop counter.
5. **Go deeper.** More ladders let you reach richer ore.

There is currently **no win condition, no failure state and no danger**. Progress is saved automatically. It's an endless sandbox with
one tension: depth versus ladders.

## The world

- The **surface** is a strip of unbreakable grass. The only way down is the **mine shaft**, which starts two tiles
  deep with ladders already in it.
- The mine is **30 tiles wide** with unbreakable bedrock walls on both sides, and **endless** downwards. Depth is
  shown in metres: 1 tile = 1 m.
- Each mine is generated from a **seed** number, so the same seed always gives the same mine. Normally every launch
  gives a new mine.

### Materials

| Tile | First appears | Shovel hits | Sells for |
|---|---|---|---|
| Dirt | top | 2 | — |
| Stone | top (rare), most of the rock by 40 m | 4 | — |
| Copper | 2 m | 4 | 15 gold |
| Silver | 12 m | 6 | 40 gold |
| Granite | 40 m | 7 | — |
| Gold | 30 m | 8 | 100 gold |
| Caves (open air) | 6 m | — | — |
| Obsidian (the wall) | 110 m, 3–5 tiles thick | the shovel can't break it | — |
| Slate, quartzite (act 2 rock) | below the wall | 6 / 8 | — |
| Gem | below the wall | 9 | 250 gold |
| Fossil `§` (rare find) | 15 m to the wall, about 1 tile in 700 | 5 | 300 gold |
| Relic `¥` (rare find) | below the wall, about 1 tile in 1000 | 7 | 750 gold |

Everything gets **one hit tougher every 25 m**, so gold at 100 m takes 12 hits.

What a player meets, roughly:

| Depth | Feels like |
|---|---|
| 0–10 m | Almost all dirt (fast digging), copper here and there |
| 10–30 m | Stone creeps in, silver shows up below ~15 m |
| 30–50 m | Half stone, first gold |
| 50–80 m | Dirt is gone, all stone and granite, gold becomes common |
| 80–110 m | About a third granite; ore levels off (copper and silver ~7%, gold ~6% of tiles) |
| 110 m | The obsidian wall: only dynamite gets through. Gems below it glint as a hint from about 95–100 m |
| below the wall | Act 2: blue slate and cyan quartzite with magenta gems; none of the act 1 rock or ore |

Ore comes in **veins** (small clusters with gaps), not single scattered tiles. Caves are rare small pockets.

## The miner and controls

| Key | Action |
|---|---|
| Left / Right (hold) | Walk one tile at a time. If the tile is rock, dig it instead |
| Down (hold) | Dig the tile below, or step down onto it if it's open |
| Up (hold) | Climb up, **only while standing on a ladder**. If the tile above is rock, dig it. A long climb speeds up after 4 tiles |
| Up + Left/Right | Jump two tiles sideways, over a one-tile hole (both tiles must be open) |
| Space | Place a ladder in the tile you're standing in |
| E | At the door: sell everything in the bag. At the shop counter: buy the item chosen with Up/Down (ladders, the blasting kit, then dynamite). At the lift sign: 10 m of lift. On a lift landing: call the car or get in. At the deep camp (100 m, 1500g at the shop): sell the bag at 70% or buy ladders, oil and dynamite at a markup |
| E (hold 0.5 s) | Underground: plant a lit stick of dynamite in the rock you face, or aim it with an arrow held at the same time (2 s fuse, 3 × 3 blast). Also on the lift's bottom landing, where a quick tap of E is the lift |
| Up / Down in the lift | Ride to the top / the bottom |
| R (hold 1.5 s) | Rescue: teleported back to the surface, **but your bag of ore is lost** (gold and ladders kept) |
| Tab | Inventory screen: everything carried, the bag's worth and weight, upgrades, finds. Pauses the game |
| Esc | Save and quit |

### Movement rules

- The miner moves **one tile per step**, sliding smoothly: walking takes about 0.13 s per tile, climbing 0.17 s (holding Up speeds it up to 0.06 s after 4 tiles, so 100 m takes under 8 s),
  falling 0.06 s per tile (speeding up). No input is taken mid-step.
- **Gravity:** you stay put if you stand on solid ground, on a ladder, or on top of a ladder. Otherwise you fall,
  tile by tile, until something holds you. **There is no fall damage.**
- **Digging** is one hit every 0.16 s while holding a direction. The tile shows growing cracks as it takes damage.

### Ladders

- You start with **8 ladders**. A ladder costs **10 gold**.
- A ladder can only be placed underground, in an open tile that doesn't have one yet. `SPACE` in a tile that has a
  ladder picks it back up for reuse (the miner falls if nothing else holds him).
- To climb you must be standing **in** a ladder, so a straight shaft down to depth *d* needs *d* − 1 ladders to
  climb out. The 8 starting ladders get you back from 9 m.
- Sideways tunnels need no ladders.
- The natural rhythm: place a ladder, dig the tile below, step down, repeat.

### Getting stuck

If you dig yourself into a pit with no ladders left, the only way out is the **rescue** (hold R), which costs you
everything in your bag. The HUD blinks "NO LADDERS LEFT! Hold R to be rescued (the bag is lost)" when you're
underground with zero ladders.

## The economy

- You start with **0 gold**.
- Sell at the door: copper 15g, silver 40g, gold 100g, gem 250g each, the whole bag at once. The bag has no size limit, but a heavy one (over 20 weight: gold and gems 2, finds 3, others 1) slows the climb up.
- Buy at the shop counter: Up/Down choose the item, E buys one. Ladders are 10g each.
- The lift: 200g for the first 10 m, 100g more for each next 10 m. It stops above the wall unless the shaft is blasted.
- The blasting kit (2500g once) and dynamite (100g a stick).

## Light and finding ore

This is the most "felt" system in the game.

- **The headlamp** casts a cone of light in the direction you last moved or dug: about 80° wide, reaching up to 8
  tiles, bright close up and fading with distance.
- **Rock blocks the light.** You see into open tunnels and caves, and the first wall the beam hits, but not into
  solid rock. Tiles right around you (1–2 tiles) are always visible.
- **Daylight** reaches a few metres down the shaft and the tunnels right under the surface.
- **Memory:** tiles you have seen stay faintly visible as grey outlines, without their colours. Unexplored darkness
  is pitch black.
- **Glints:** since you can't see through rock, ore reveals itself by **twinkling**. Ore within about 9 tiles sparkles
  now and then, even through rock and in total darkness. Gold twinkles most, then silver, then copper; ore in the dark
  twinkles more. Gold glints yellow, silver cyan, copper dark yellow. Hunting glints is the real "prospecting"
  mechanic.

## Feedback and juice

Every action has visible feedback:

| Event | Effect |
|---|---|
| Each shovel hit | Chunks of dirt, stone or metal fly out of the rock towards you and fall, landing on the tunnel floor. Ore adds sparks |
| Breaking a tile | A burst of chunks, a puff of dust, a small screen shake (bigger for ore, which also sprays glittering sparks) |
| Walking, climbing | Little dust puffs from the feet |
| Landing after a fall | Dust to both sides, more for longer falls; long falls shake the screen |
| Caves you've lit | Slow drifting mist |
| Selling | A burst of spinning gold coins that rain down onto the grass (more coins for bigger sales), a ring of stars, and a floating "+150g" |
| Buying a ladder | A ring of stars and a floating "-10g" |
| Can't do it (empty bag, no money, no ladder) | A small dust puff |
| Standing at the door or shop | A small bobbing pixel-art prompt: "DELIVER [E]", or at the counter a panel with the item, what you have and its price (red when you can't afford it) |

**The HUD** (top two rows) shows the title, gold, ladders, depth, and the bag: copper, silver and gold counts with
coloured icons, plus what the bag is worth. The second row shows the controls, a rescue progress bar while R is held,
or the no-ladders warning.

## The original vision vs. now

The game was built from a short design document. Everything in it is implemented: the surface with the house and
sign, the shop, the shaft, the endless mine with tougher and richer layers, copper/silver/gold, ladders bought and
placed, the headlamp cone, glints, mist, debris, dust, coins, floating numbers, the HUD and the prompts.

Added beyond the original document: the rescue (so you can never get permanently stuck), remembered tiles, caves,
granite, toughness growing with depth, screen shake.

## What isn't there (yet)

- No goal, ending, or progression beyond "go deeper and earn more".
- No danger: no fall damage, no hazards, no enemies, no stamina, no time pressure. The lamp burns oil underground:
  low oil shrinks and flickers the light, empty leaves only a dim glow (it never kills). Refill at the counter, or carry oil flasks (60g, up to 5) that pour themselves in when the lamp runs low.
- Upgrades at the counter: shovel (digs faster), pickaxe (fewer hits per tile), lamp (reaches further), bag (carries more) and detector (glints from
  further), three tiers each.
- No hard bag limit; a heavy bag slows the climb up.
- Ladders are permanent once placed; besides the lift and dynamite there are no other tools (no ropes, supports).
- One save slot only; no score, no records.
- No sound.
- One miner, single player.

## Design constraints worth knowing

These shape what ideas fit, without getting technical:

- **Text-mode visuals:** everything is made of characters (letters, symbols, box and block shapes) in 16 fixed
  colours. Particles and effects are single characters. Detailed sprites are tiny (the miner is 4 × 2 characters).
- **Grid world:** the mine is a grid of square tiles; the miner occupies one tile and moves tile by tile.
- **One screen wide:** the mine is exactly as wide as the screen (30 tiles); there is no sideways scrolling, only
  vertical.
- **Keyboard only**, a handful of keys. Holding keys works for walking and digging.
- **Effects are cheap:** hundreds of particles at once are fine, so "juicy" ideas are welcome.

## Open questions to explore

Some tensions the current design leaves open:

- What makes going deeper *risky* rather than just slower? (hazards, lamp fuel, stamina, cave-ins, water, gas…)
- Should there be something to spend gold on besides ladders? (upgrades, tools, a bigger bag, a better lamp…)
- Should the bag have a limit, to force more trips?
- Is the rescue too generous, or too harsh?
- Is there a goal: a depth record, a final treasure, a story, a "bottom"?
- Could the world have more variety: biomes by depth, rare finds, ruins, underground features?
- How could the surface evolve as you earn money (house upgrades, new shops, NPCs)?
