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
| **Chop** (red) | E | Chop raw food on a board. The chef keeps chopping until done or until you walk away |
| **Swap** (green) | Q | Switch to the other chef |
| Pause button | Esc | Pause |

## Kitchens

| # | Kitchen | Menu | What it teaches |
| --- | --- | --- | --- |
| 1 | Salad Days | Green Salad, Garden Salad | Crates, boards, plates, serving |
| 2 | Soup Kitchen | Tomato Soup, Onion Soup, Garden Salad | Pots, cook timers, burning |
| 3 | The Divide | Burger, Deluxe Burger, Garden Salad | Split kitchen: pass food over the counter and swap chefs |
| 4 | Dinner Rush | Everything | Juggling it all |

Each served order pays its menu price plus a tip of up to 10 coins, scaled by how much time was left. A missed order costs 10 coins. Each kitchen has three star thresholds, and one star unlocks the next kitchen. Best scores are saved in `localStorage`.

## Tweaking

Everything you'd want to tune is at the top of the `<script>`:

- `LEVELS`: ASCII kitchen maps (legend in the comment above them), menu, round length, star thresholds, order pacing and plate count.
- `RECIPES`: ingredients, price and ticket time for each dish.
- `CHOP_TIME`, `POT_COOK`, `POT_BURN`, `PAN_COOK`, `PAN_BURN`, `SPEED`, `TIP_MAX`, `MISS_PENALTY`.

In portrait, kitchens that are wider than they are tall get rotated so they fill the screen.
