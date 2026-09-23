# Order Up!

A top-down cooking game for phones, in the spirit of Overcooked. You run a kitchen with two chefs. Chop, cook, plate and serve dishes before each order ticket runs out.

It's one self-contained HTML file with no build step and no image files. The pixel art is 16×16 sprites stored as text grids in the code, and the sound effects are synthesised.

## Play

- **On a phone:** host the folder anywhere static (GitHub Pages, Netlify, `npx serve OrderUp`) and open it. Use "Add to Home Screen" to play fullscreen.
- **Locally:** open `OrderUp/index.html` in a browser.

## Play with a friend (online co-op)

Two phones, one kitchen. On the title screen, tap **Play with a friend**. One player taps **Host a kitchen** and gets a 4-letter room code. The other types the code and taps **Join**. Each player runs one chef, and the host picks the kitchens.

- **Peer-to-peer:** the host's phone runs the game. The guest's phone sends its stick and button presses, and gets back about 15 snapshots of the kitchen a second. The guest's own chef is predicted locally, so movement feels instant.
- **Matchmaking:** [PeerJS](https://peerjs.com) (loaded from jsdelivr only when you go online) and its free public server introduce the two phones. After that, data goes directly between them over WebRTC.
- **Needs a real web host.** Online play doesn't work inside the Claude artifact preview, which blocks WebRTC. Deploy the `OrderUp` folder anywhere static (for example, drag it onto [Netlify Drop](https://app.netlify.com/drop)) and both players open that link.
- **Own PeerJS server:** add `?peerhost=your.server&peerport=443` to the URL. `peerpath` and `peersecure=0` are also accepted.
- **Limits of the prototype:** 2 players only. Some strict mobile or corporate networks block direct connections. That would need a TURN relay server, which isn't set up.

## Controls

| Touch | Keyboard | Action |
| --- | --- | --- |
| Drag on the left half | WASD / arrows | Walk. You act on the counter you're facing, which gets a white outline |
| **Grab** (yellow) | Space | Pick up, put down, plate, serve, bin. The label changes to show what it will do |
| **Chop** (red) | E | Chop raw food on a board, or wash dirty plates at the sink. The chef keeps going until done or until you walk away. While holding the extinguisher it becomes **Spray**: hold it down to spray |
| **Swap** (green) | Q | Switch to the other chef |
| Pause button | Esc | Pause |

## Kitchens

The campaign is a food truck road trip (see `ROADMAP.md`). Kitchens are grouped into stops.

| # | Stop | Kitchen | Menu | What it teaches |
| --- | --- | --- | --- | --- |
| 1 | Hometown Diner | Salad Days | Green Salad, Garden Salad | Crates, boards, plates, serving |
| 2 | Hometown Diner | Soup Kitchen | Tomato Soup, Onion Soup, Garden Salad | Pots, cook timers, fire |
| 3 | Hometown Diner | The Divide | Burger, Deluxe Burger, Garden Salad | Split kitchen: pass food over the counter and swap chefs |
| 4 | Hometown Diner | Dinner Rush | Everything above | Juggling it all |
| 5 | Route 66 Breakfast | Sunrise Griddle | Eggs & Bacon, Pancakes | Griddle, dishwashing |
| 6 | Route 66 Breakfast | Bottomless Coffee | + Coffee | Coffee machine (served in the mug, no plate) |
| 7 | Route 66 Breakfast | Truck Stop Rush | + Big Breakfast | Three-item plates, three griddles |
| 8 | Drive-In Burgers | Curb Service | Burger, Cheeseburger, Fries | Deep fryer, patties on the griddle |
| 9 | Drive-In Burgers | Shake Shack | + Vanilla & Strawberry Shakes | Blender (served in the cup) |
| 10 | Drive-In Burgers | Friday Night Rush | + Burger & Fries | Split kitchen, everything at once |
| 11 | Texas BBQ Smokehouse | Low & Slow | Smoked Ribs, Cornbread | Smoker (long cooks, so plan ahead) |
| 12 | Texas BBQ Smokehouse | Brisket Board | Brisket Plate, Ribs, Mac & Cheese | Slicing smoked brisket; pot recipes |
| 13 | Texas BBQ Smokehouse | Pitmaster Showdown | + Pitmaster Platter, Cornbread | Three smokers, split kitchen |

Each served order pays its menu price plus a tip of up to 10 coins, scaled by how much time was left. A missed order costs 10 coins. Each kitchen has three star thresholds. Within a stop, one star unlocks the next kitchen. The next stop opens once you've collected enough stars in the current one (3 for Route 66, 4 for Drive-In Burgers, 5 for the Smokehouse). Best scores are saved in `localStorage` by kitchen index, so add new kitchens at the end of `LEVELS`.

## Kitchen systems

- **Griddle:** put eggs, bacon, batter, cornmeal or chopped meat (patties) straight on it. They cook and then start to burn, with a flashing warning and beeps first.
- **Coffee machine:** tap Grab to brew a mug.
- **Deep fryer:** chop potatoes, then drop them in. Fryers burn faster than griddles.
- **Smoker:** ribs (15s) and brisket (20s) go in whole. It's slow, but there's a long window before they burn, and smoked meat keeps once it's off the heat. Smoked brisket gets **sliced** on a board before plating.
- **Pots follow recipes** (`POT_RECIPES`): 3 tomatoes, 3 onions, or 2 macaroni + 1 cheese. A pot only takes ingredients that lead to something on the kitchen's menu.
- **Blender:** add a scoop of ice cream (and a strawberry for a strawberry shake), then tap **Blend**. Shakes are served in the cup, like coffee.
- **Dishwashing** (kitchens with `dishes: true`): served plates come back dirty through the hatch. Carry the stack to the sink and wash the plates there, and clean ones appear on the plate rack.
- **Fire** (kitchens with `fire: true`): burnt food sets its counter alight. Fire spreads to a neighbouring counter every few seconds, and you can't use a counter while it's burning. Items on it aren't destroyed. Pick up the extinguisher and hold Spray while facing the flames.

## Tweaking

Everything you'd want to tune is at the top of the `<script>`:

- `LEVELS`: ASCII kitchen maps (legend in the comment above them), menu, round length, star thresholds, order pacing and plate count.
- `RECIPES`: ingredients, price and ticket time for each dish.
- `SPR` / `PAL`: the pixel art. Each sprite is a grid of palette letters, where `.` is transparent and digits are colour slots filled in per use (for example chef colours and cooked or burnt states). Kitchen tiles are 16 px and food is 12 px.
- `STOPS`: road trip stops and how many stars open each one.
- `CHOP_TIME`, `POT_COOK`, `POT_BURN`, `PAN_COOK`, `PAN_BURN`, `HEAT` (what cooks on griddles and fryers, and how fast), `BLEND_TIME`, `BREW_TIME`, `WASH_TIME`, `FIRE_SPREAD`, `SPEED`, `TIP_MAX`, `MISS_PENALTY`.

In portrait, kitchens that are wider than they are tall get rotated so they fill the screen.
