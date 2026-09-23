# Order Up!

A top-down cooking game for phones, in the spirit of Overcooked. You run a kitchen with two chefs. Chop, cook, plate and serve dishes before each order ticket runs out.

It's one self-contained HTML file with no build step and no assets. Everything is drawn on a canvas, and the sound effects are synthesised in code.

## Play

- **On a phone:** host the folder anywhere static (GitHub Pages, Netlify, `npx serve OrderUp`) and open it. Use "Add to Home Screen" to play fullscreen.
- **Locally:** open `OrderUp/index.html` in a browser.

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

Each served order pays its menu price plus a tip of up to 10 coins, scaled by how much time was left. A missed order costs 10 coins. Each kitchen has three star thresholds. Within a stop, one star unlocks the next kitchen. The next stop opens once you've collected enough stars in the current one (3 for Route 66). Best scores are saved in `localStorage` by kitchen index, so add new kitchens at the end of `LEVELS`.

## Kitchen systems

- **Griddle:** put eggs, bacon or batter straight on it. They cook and then start to burn, with a flashing warning and beeps first.
- **Coffee machine:** tap Grab to brew a mug.
- **Dishwashing** (kitchens with `dishes: true`): served plates come back dirty through the hatch. Carry the stack to the sink and wash the plates there, and clean ones appear on the plate rack.
- **Fire** (kitchens with `fire: true`): burnt food sets its counter alight. Fire spreads to a neighbouring counter every few seconds, and you can't use a counter while it's burning. Items on it aren't destroyed. Pick up the extinguisher and hold Spray while facing the flames.

## Tweaking

Everything you'd want to tune is at the top of the `<script>`:

- `LEVELS`: ASCII kitchen maps (legend in the comment above them), menu, round length, star thresholds, order pacing and plate count.
- `RECIPES`: ingredients, price and ticket time for each dish.
- `STOPS`: road trip stops and how many stars open each one.
- `CHOP_TIME`, `POT_COOK`, `POT_BURN`, `PAN_COOK`, `PAN_BURN`, `GRIDDLE`, `GRIDDLE_BURN`, `BREW_TIME`, `WASH_TIME`, `FIRE_SPREAD`, `SPEED`, `TIP_MAX`, `MISS_PENALTY`.

In portrait, kitchens that are wider than they are tall get rotated so they fill the screen.
