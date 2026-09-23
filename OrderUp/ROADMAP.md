# Order Up! Design roadmap

Decisions from the design interview, 23 Sep 2026. The current game (4 kitchens, two swappable chefs, chop / pot / pan / plate / serve) is the baseline.

## Direction

| Area | Decision |
| --- | --- |
| Shape | Level campaign of handcrafted kitchens, stars unlock the next (Overcooked-style) |
| Players | Solo with two swappable chefs, plus online play with friends: co-op **and** versus |
| Chef moves | Keep it simple: no dash or throw |
| Hazards | Fire (Overcooked-style, spreading) and dishwashing |
| Rewards | Chef outfits, bonus levels, daily challenge, light story and characters |
| Release | Free on iOS and Android |
| Monetisation | Occasional skippable ads **between levels only**, never during play. A one-time Remove Ads purchase turns them off |
| Tech | Keep iterating in the web version (fast to test with friends), then port to Unity for the store release |
| Art | Pixel art |

## Campaign: food truck road trip across the USA

Each stop brings in one new appliance or mechanic.

| # | Stop | Dishes | New mechanic |
| --- | --- | --- | --- |
| 0 | Hometown Diner (tutorial) | Salads, soup (current content) | Chop, plate, serve |
| 1 | Route 66 Breakfast | Pancakes, eggs & bacon, coffee | Griddle, coffee machine, **dishwashing starts** |
| 2 | Drive-In Burgers | Burgers, fries, milkshakes | Deep fryer (fire risk), blender |
| 3 | Texas BBQ Smokehouse | Brisket, ribs, cornbread, mac & cheese | Smoker with long cook times, so you plan ahead |
| 4 | Tex-Mex Border Town | Tacos, burritos, nachos | Build-your-own orders with many combinations |
| 5 | New Orleans Cajun | Gumbo, jambalaya, beignets | Pots that take several ingredients, added in order |
| 6 | New York Deli & Pizza | NY slice, pastrami on rye, bagels | Oven, dough stretching |
| 7 | New England Seafood Shack | Clam chowder, lobster rolls, fish & chips | Live lobsters that escape |
| 8 | California Coast | Fish tacos, avocado toast, smoothie bowls | Fast, fresh orders with only short cook steps |
| 9 | Thanksgiving Finale | Turkey, stuffing, mash, pumpkin pie | Everything at once (boss level) |

Every stop gets 3–5 kitchens plus bonus kitchens unlocked by stars.

## Feature specs

### Fire (Overcooked-style)
- Cookware that burns catches fire instead of only going black.
- Fire spreads to neighbouring counters every few seconds. You can't use a burning counter, but items on it aren't destroyed. Destroying plates could leave a kitchen with no plates at all.
- A fire extinguisher sits on a counter in each kitchen. Pick it up, face the fire and hold **Chop** to spray. The Grab/Chop buttons stay the same, so no new button is needed.
- From Drive-In onwards, an unattended fryer is the main source of fires.

### Dishwashing (from Route 66)
- Served plates come back **dirty** at a return hatch instead of reappearing clean.
- Put dirty plates in the sink and hold Chop to wash them. Clean plates stack on a drying rack.
- Kitchen 0 keeps clean plate returns so the tutorial stays simple.

### Delivery orders (in-level ghost kitchen)
- Some tickets are marked **Delivery**, with their own countdown for when the driver arrives.
- Plate the dish as usual, then put it in a bag at the bagging station and leave it at the pickup window.
- The driver waits a few seconds. A late bag means a lower tip, and a missed pickup counts as a missed order.
- **Later:** a driving mini-game where you do the delivery yourself.

### Online play
- **Co-op:** a room code or link, 2–4 players, and each player controls one chef in the same kitchen.
- **Versus:** two mirrored kitchens racing for score.
- Web prototype needs: a small realtime server (for example a hosted WebSocket service), host-authoritative game state, and inputs sent from clients. The published Claude artifact can't open sockets, so the prototype has to be hosted somewhere else, like Netlify plus a small server.

### Rewards
- **Chef outfits:** hats, colours and animal chefs, unlocked with stars.
- **Bonus levels:** unlocked by star totals in each stop.
- **Daily challenge:** a new kitchen and menu from a fixed daily seed, with a score to beat.
- **Story:** short scenes between stops on the road trip (the food truck and its crew).

## Next build (web)
1. ~~**Stop 1: Route 66 Breakfast.** Griddle, coffee machine, dishwashing, 3 kitchens.~~ Done.
2. ~~**Fire.** Spreading fire and the extinguisher.~~ Done. It's on in every kitchen that cooks (kitchens 2–7).
3. ~~**Pixel art restyle.**~~ Done. The Kenney packs in the repo (Tiny Town / Tiny Dungeon) have no food, kitchen equipment or chefs, so the game uses its own 16×16 sprites in the same style. They're text grids in the `SPR` table, with Pixelify Sans for the text.
4. ~~**Online co-op prototype.**~~ Done. It's peer-to-peer (PeerJS/WebRTC) with 4-letter room codes, a host-authoritative simulation, guest-side movement prediction and 2 players. Next steps: 3–4 players (needs more spawn points per kitchen), a TURN relay for strict networks, and versus mode.

## Done since
- Guided tutorial level (`TUT_STEPS`): 14 steps covering controls, tickets and kitchen symbols.
- Bonus boss: The Judge's Table (strikes, cold/overcooked checks, Chef Brutus Blaze).
- Tester unlock: `#unlockall` on the link.
- AI helper chefs (solo, Judge's Table only): hire from 3 random offers, one per star on The Big Feast, up to 3 (`helperHires`); one job per station (`HELPERS`).

## Difficulty (done)
- Smooth rising curve across all 31 kitchens (tools/tune.js), and co-op scaling (+25% pace and star targets per extra player).

## Later
- ~~Stop 2: Drive-In Burgers~~ Done: 3 kitchens with the deep fryer, blender, griddle patties and cheese.
- ~~Stop 3: Texas BBQ Smokehouse~~ Done: 3 kitchens with the smoker, brisket slicing, recipe-driven pots (mac & cheese) and griddle cornbread.
- ~~Stop 4: Tex-Mex Border Town~~ Done: 3 kitchens with build-your-own tickets (tacos, burritos, nachos), rice and bean pots, and guacamole.
- ~~Stop 5: New Orleans Cajun~~ Done: ordered pots (gumbo, jambalaya) and fried beignets.
- ~~Stop 6: New York Deli & Pizza~~ Done: the oven, dough stretching, layered pizza toppings, pastrami on rye and toasted bagels.
- ~~Stop 7: New England Seafood Shack~~ Done: live lobsters that wander and escape, clam chowder, and fish & chips.
- ~~Stop 8: California Coast~~ Done: fast, short tickets, grilled fish tacos, avocado toast and smoothie bowls.
- ~~Stop 9: Thanksgiving Finale~~ Done: turkey (roast and carve), stuffing, mash, pumpkin pie and the Feast plate. The Big Feast is the 5-minute boss service. **The road trip campaign is complete: 31 kitchens across 10 stops.**
- Polish: with 5 tickets on a portrait phone, 5-ingredient builds show their icons at the smallest size. Could use a two-row icon layout or a tap-to-zoom ticket.
- Versus mode.
- Driving mini-game.
- Unity port: iOS and Android builds, between-level ads with the Remove Ads purchase, and online play through Netcode or Photon.
