# Order Up!

A top-down cooking game for phones, in the spirit of Overcooked. You run a kitchen with two chefs. Chop, cook, plate and serve dishes before each order ticket runs out.

It's one self-contained HTML file with no build step and no image files. The pixel art is 16×16 sprites stored as text grids in the code, and the sound effects are synthesised.

## Play

- **On a phone:** host the folder anywhere static (GitHub Pages, Netlify, `npx serve OrderUp`) and open it. Use "Add to Home Screen" to play fullscreen.
- **Locally:** open `OrderUp/index.html` in a browser.

## Tutorial

New players see **New here? Start with the tutorial** on the title screen. It's a small, untimed practice kitchen that teaches one thing at a time: walking, reading a ticket (dish picture, ingredient icons, patience bar), grabbing, chopping, plating, serving, coins and tips, swapping chefs, a two-ingredient salad, the bin, and the kitchen symbols (progress bar, ready tick, burn warning, fire). A yellow arrow points at whatever to use next, and each step moves on when you do it. Afterwards, the button becomes **Replay the tutorial**. The steps live in `TUT_STEPS`.

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
| 8 | Drive-In Burgers | Curb Service | Burger, Cheeseburger, Fries | Deep fryer, patties on the griddle; **delivery tickets** start |
| 9 | Drive-In Burgers | Shake Shack | + Vanilla & Strawberry Shakes | Blender (served in the cup) |
| 10 | Drive-In Burgers | Friday Night Rush | + Burger & Fries | Split kitchen, everything at once; deliveries |
| 11 | Texas BBQ Smokehouse | Low & Slow | Smoked Ribs, Cornbread | Smoker (long cooks, so plan ahead) |
| 12 | Texas BBQ Smokehouse | Brisket Board | Brisket Plate, Ribs, Mac & Cheese | Slicing smoked brisket; pot recipes |
| 13 | Texas BBQ Smokehouse | Pitmaster Showdown | + Pitmaster Platter, Cornbread | Three smokers, split kitchen |
| 14 | Tex-Mex Border Town | Taco Truck | Tacos, Nachos (built to order) | Build-your-own tickets; tortillas on the griddle; deliveries |
| 15 | Tex-Mex Border Town | Burrito Bar | Burritos, Tacos | Rice and bean pots, guacamole |
| 16 | Tex-Mex Border Town | Fiesta Night | Tacos, Burritos, Nachos | Split kitchen, everything built to order |
| 17 | New Orleans Cajun | Bayou Kitchen | Gumbo, Beignets | Ordered pots; frying dough |
| 18 | New Orleans Cajun | Jazz Brunch | + Jambalaya | Two ordered stews sharing sausage |
| 19 | New Orleans Cajun | Mardi Gras | Gumbo, Jambalaya, Beignets | Split kitchen, three pots |
| 20 | New York Deli & Pizza | Corner Slice | Cheese Slice, Pepperoni Slice | Oven; stretch, top, bake, slice; pizza delivery |
| 21 | New York Deli & Pizza | Deli Counter | Pastrami on Rye, Bagel & Schmear, Cheese Slice | Slicing pastrami; toasting bagels |
| 22 | New York Deli & Pizza | Midtown Lunch Rush | Everything | Split kitchen, three ovens; deliveries |
| 23 | New England Seafood Shack | Harbor Shack | Clam Chowder, Fish & Chips | Shucking clams, filleting and frying fish |
| 24 | New England Seafood Shack | Lobster Pound | Lobster Roll, Clam Chowder | Live lobsters that wander off and escape |
| 25 | New England Seafood Shack | Nor'easter | Everything | Split kitchen: pass lobsters across fast |
| 26 | California Coast | Venice Beach | Fish Tacos, Avocado Toast (built to order) | Short tickets; fish and toast in seconds |
| 27 | California Coast | Smoothie Stand | Smoothie Bowl, Avocado Toast | Blender recipes; pour into a bowl, top with granola |
| 28 | California Coast | Sunset Rush | Everything | Split kitchen, fastest ticket rate; deliveries |
| 29 | Thanksgiving Finale | Family Dinner | Turkey Dinner, Pumpkin Pie | Long turkey roast; carving; mash pot |
| 30 | Thanksgiving Finale | Grandma's Kitchen | The Feast, Stuffing, Pumpkin Pie | Three-part feast plate |
| 31 | Thanksgiving Finale | The Big Feast | Everything Thanksgiving | 5-minute boss service; road trip complete |
| Bonus | The Judge's Table | The Judge's Table | Deluxe Burger, Tomato Soup, Fish & Chips, Strawberry Shake | A panel of three judges: three strikes and you're out |

### The Judge's Table (bonus boss)

Unlocked by earning a star on The Big Feast. A panel of three parody TV-chef judges inspects every plate, each calling out their specialty with their own pixel portrait and name on the speech bubble:

- **Yourdone Ramsey** (head judge): wrong dishes, walk-outs, the opening and the final verdict.
- **Richard Braise** (heat): overcooked and burnt food.
- **Nyesha Garnishton** (polish and temperature): cold food.
- Any of them may praise a perfect plate.

The names are deliberate sound-alike cooking puns on real TV chefs, and the portraits are generic, not likenesses. The Judge's Table intro card and this README say: *All judges are fictional parody characters, not affiliated with or endorsed by any real chef.* Parody lowers the risk but doesn't remove it, so get legal advice before an app store release (right of publicity). The names live in `JUDGES`.
 You get a **strike** for a wrong dish, **cold** food (a cooked part more than 15 seconds off the heat; a frost icon shows it), **overcooked** food (taken off during the burn warning), anything **burning**, or a customer who **walks out**. Three strikes ends the service on the spot with no stars. Tickets are 30% shorter than normal. It's tuned to be brutal, so tell me after you've played it if it's too easy or too hard.

**AI helper (solo only):** when you play the Judge's Table on your own, not in co-op, the intro card offers three random helpers drawn from the stations in that kitchen. You can hire **one helper per star you earned on The Big Feast, up to three** (so a 3-star Feast lets you take all three). Picking past your limit lets go of your earliest pick. Hired helpers join your two chefs, marked by a purple neckerchief and a badge. Each does exactly one job:

| Helper | Job |
| --- | --- |
| Prep Cook | Chops, slices and stretches anything left on a board |
| Fry Cook | Pulls food out of the fryer before it burns |
| Grill Cook | Takes food off the griddle before it burns |
| Pot Watcher | Moves finished pots and pans off the stove |
| Pitmaster | Pulls meat from the smoker |
| Baker | Takes pizzas, pies and roasts out of the oven |
| Dishwasher | Brings dirty plates to the sink and washes them |
| Barista | Keeps the coffee machines brewing |

Helpers find their way around with grid pathfinding and use the same Grab and Chop actions as you. The whole roster is in `HELPERS`, so any kitchen could offer them later.

**Testers:** add `#unlockall` to the end of the game's link to open every kitchen, including this one.

Each served order pays its menu price plus a tip of up to 10 coins, scaled by how much time was left. A missed order costs 10 coins. Each kitchen has three star thresholds. Within a stop, one star unlocks the next kitchen. The next stop opens once you've collected enough stars in the current one (3 for Route 66, 4 for Drive-In Burgers, 5 each for every stop from the Smokehouse on, including the Thanksgiving Finale). Best scores are saved in `localStorage` by kitchen index, so add new kitchens at the end of `LEVELS`.

## Difficulty

- **Rising curve:** each kitchen's ticket rate is set so that "pressure" (orders per minute × work per dish, counting long cooks) rises smoothly. It goes up about 1.2 per stop, and +5 from a stop's first kitchen to its last, so each stop opens gently while it teaches its twist and ends with a rush. Star targets are 35%, 55% and 75% of every possible coin at that pace, creeping up slightly at later stops. `node OrderUp/tools/tune.js` prints the table, and `--write` applies it after you change recipes or levels.
- **Co-op scaling:** each extra human player makes service 25% busier, adds one more ticket to the rail, and raises star targets by the same 25% (`COOP_STEP`). The intro card shows the adjusted targets. The Judge's Table strike rules are the same for everyone.
- **Saves** remember the stars you've earned, so a co-op run can't award solo stars and a future re-tune won't take stars away. Your "Best" score is from solo runs.

## Kitchen systems

- **Griddle:** put eggs, bacon, batter, cornmeal, tortillas or chopped meat (patties) straight on it. They cook and then start to burn, with a flashing warning and beeps first.
- **Coffee machine:** tap Grab to brew a mug.
- **Deep fryer:** chop potatoes, then drop them in. Fryers burn faster than griddles.
- **Smoker:** ribs (15s) and brisket (20s) go in whole. It's slow, but there's a long window before they burn, and smoked meat keeps once it's off the heat. Smoked brisket gets **sliced** on a board before plating.
- **Build-your-own orders** (Tex-Mex): a recipe with `extras` gets a random set of them on each ticket (`pick` = min/max), and each extra adds `extraReward`. The ticket's icon row shows the exact build, and the pass only accepts that exact combination.
- **Plate straight from a crate:** holding a plate at a crate of something that needs no prep (chips, cheese, buns) puts it right on the plate.
- **Ordered pots** (Cajun): gumbo goes in as onion, then sausage, then shrimp, and jambalaya as rice, then sausage, then tomato. The ticket icons show the order. Add something out of turn and the chef says what goes in next.
- **Carving and slicing:** brisket, pizza, pie and turkey come out of the heat and get sliced (or carved) on a board. Clams are shucked, fish is filleted and dough is stretched, all with the same button.
- **Tickets** spell out pot dishes step by step when they're the whole order. On multi-part plates (like the Feast) they show the finished side instead, so the icon row stays readable.
- **Pizza** (New York): stretch dough on a board, then add sauce, then cheese (and pepperoni). Toppings build up on the base (`TOPPINGS`), and adding one out of order tells you what is missing. Bake it in the **oven**, then slice it on a board.
- **Live lobsters** (New England): a raw lobster left on a counter hops to a free neighbouring counter every few seconds, and from an edge counter it can escape for good (`LOBSTER_HOP`). Take it straight to a pot.
- **Blender recipes** (`BLENDS`): ice cream makes a vanilla shake, ice cream + strawberry a strawberry shake, and banana + strawberry a smoothie. Tap Blend once the jar matches a recipe.
- **Build-your-own tickets only ask for toppings that kitchen has a crate for.**
- **Pots follow recipes** (`POT_RECIPES`): 3 tomatoes, 3 onions, 2 macaroni + 1 cheese, 2 rice, or 2 beans. A pot only takes ingredients that lead to something on the kitchen's menu.
- **Blender:** add a scoop of ice cream (and a strawberry for a strawberry shake), then tap **Blend**. Shakes are served in the cup, like coffee.
- **Dishwashing** (kitchens with `dishes: true`): served plates come back dirty through the hatch. Carry the stack to the sink and wash the plates there, and clean ones appear on the plate rack.
- **Delivery tickets** (kitchens with `delivery: <chance>`, a bagging station `A` and a pickup window `@`): some tickets show a paper bag. Plate the order as usual, tap Grab at the bagging station to bag it (the plate goes back to the dishes), then hand the bag off at the pickup window. Delivery tickets get 25% more time and pay 5 extra coins. When the ticket runs out, the driver pulls up on a scooter and waits 8 more seconds (`DRIVER_WAIT`). A bag handed over then earns no tip, and if the driver leaves it's a missed order. The bell won't take a delivery plate, and bags can be binned.
- **Fire** (kitchens with `fire: true`): burnt food sets its counter alight. Fire spreads to a neighbouring counter every few seconds, and you can't use a counter while it's burning. Items on it aren't destroyed. Pick up the extinguisher and hold Spray while facing the flames.

## Playtest feedback

When the game is opened as the published Claude artifact, every finished kitchen is logged for balancing: the kitchen, player count, score, stars, star targets, orders served and missed, tips, Judge strikes and hired helpers. The results screen asks **How did that feel?** (Too easy / Just right / Too hard), and the answer is saved with the run. No names are stored, only an anonymous per-tester id. Testers need "Can interact" access to the artifact for their runs to save. Outside the artifact (a local file, Netlify) nothing is logged and the question is hidden.

To read the results, export the `playtests` collection to `runs.json` and run:

```
node OrderUp/tools/playtest.js runs.json
```

It prints each kitchen's run count, pass rate, average score against the 1-star target, 3-star rate and ratings, with a hint to ease off or push. Apply changes in `tools/tune.js` (pace and targets) and re-run it with `--write`.

## Tweaking

Everything you'd want to tune is at the top of the `<script>`:

- `LEVELS`: ASCII kitchen maps (legend in the comment above them), menu, round length, star thresholds, order pacing and plate count.
- `RECIPES`: ingredients, price and ticket time for each dish.
- `SPR` / `PAL`: the pixel art. Each sprite is a grid of palette letters, where `.` is transparent and digits are colour slots filled in per use (for example chef colours and cooked or burnt states). Kitchen tiles are 16 px and food is 12 px.
- `STOPS`: road trip stops and how many stars open each one.
- `CHOP_TIME`, `POT_COOK`, `POT_BURN`, `PAN_COOK`, `PAN_BURN`, `HEAT` (what cooks on griddles and fryers, and how fast), `BLEND_TIME`, `BREW_TIME`, `WASH_TIME`, `FIRE_SPREAD`, `SPEED`, `TIP_MAX`, `MISS_PENALTY`.

In portrait, kitchens that are wider than they are tall get rotated so they fill the screen.
