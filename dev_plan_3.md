# Dev Plan — Market Game
**Unity 6 · URP · First Person · co-developed by Claude Code + Codex**

This is the single source of truth for *what to build and in what order*, plus the live progress
checkboxes. Contracts (how to write code, how the two agents collaborate) live elsewhere:
`AGENTS.md` (coding/architecture rules, shared by both agents), `CLAUDE.md` (Claude's review/publish
role), `COLLAB.md` (branch-per-task + PR process). Don't duplicate progress anywhere but this file.

> **Agents: don't read this file whole (36 KB ≈ 10k tokens).** Current state = the `## Progress`
> section at the bottom; task details = the section of the block you're working on. Grep by step id.

---

## Philosophy

Top rule: **the game runs and is playable at every point in time.** No "two weeks of systems, then
test". Every step below:

- adds exactly one thing;
- ends with a concrete "press Play and confirm X works" check;
- builds on the previous step.

If a step can't be verified in a couple of minutes, it's too big — split it further. Blocks are in
implementation order. Don't jump ahead: specializations are pointless until the core loop is fun.

**No hidden market coefficients.** No drought ×1.8, no random demand multipliers, no invisible
world-factor effects. The player understands the economy directly:

- base buy/sell prices live in `ItemSO`;
- the player sets the sell price on the stall;
- NPCs buy by budget, category preference, and concrete requests;
- seasons change *availability* and visuals, never price via magic multipliers;
- depth comes from assortment, production, orders, reputation and progression — not hidden formulas.

### Do
- Every day should tell a small story: who came, what they bought, which order appeared, what unlocked.
- Every system must be explainable to the player in one sentence.
- Progress must be visible in the world: new items, places, NPCs, decor, stations.
- Automation unlocks *after* the player has learned the manual action. A worker removes chores; it
  doesn't play the game for the player from day one.
- Customer attraction must be physical and visible: a sign, a display window, flyers, a discount ad,
  a nice-looking stall, a rare item on show.
- NPCs are content, not just NavMesh agents.
- Orders / NPC requests are the main source of "events".
- Prices are fixed and readable; the player chooses the sell price.

### Don't
- Hidden market coefficients.
- Drought/rain/festival as a numeric price modifier.
- Rumors that invisibly change demand.
- A rival that secretly siphons a % of traffic.
- Complex loans/penalties before there's proper UI.
- Automation before the manual loop is enjoyable.
- Invisible "+20% customers" with no in-world cause. If traffic rose, the player must see why.
- Co-op before a complete solo game.

---

## Direction & recommended order (fun-first)

The foundation (Blocks 0/A/B) is done: you can walk the market, buy from a supplier, place goods on
a stall, NPCs buy, time/season/daylight run, saving works — all via debug keys. The remaining work is
ordered to make the **manual loop fun before adding deep systems**:

1. **Block C — UI**: replace Debug.Log with real screens. Highest priority; everything else is hard
   to test while the loop lives in the console.
2. **Block D — Day rhythm, orders, visible progression & attraction**: the day becomes a short
   session (prep → open → sell → evening summary), NPCs gain personalities, a Wishboard replaces
   "world events", and growth shows up as staff/signs/decor — never as hidden buffs.
3. **Blocks E–I — Specializations**: farm, fishing, animals, crafting, social/town. Only once the
   core loop is enjoyable. Gated heavily by available art (see legend).
4. **Block J — Co-op**, **Block K — Polish & release**: last.

### Minimal fun-slice (the near-term target)
Morning: buy apples → place them on the stall via UI → open the shop → 3 different NPCs arrive with
lines → one haggles, one buys, one leaves a request → if you over-bought, run a discount ad to clear
stock → evening summary → after 3 completed orders a new item/decor unlocks → the player sees a goal
like "hire a cashier" or "put up a sign". If this is fun, add farm/fishing/animals/co-op. If it
isn't, no big system will save it.

---

## Asset availability legend

Per project decision, plan steps are tagged by whether the art already exists in the repo. The artist
won't be making new models/animations for a while, so steps needing new art are stubbed or deferred,
not blocked.

- **`[assets: ready]`** — models/animations exist in the project; build the real thing now.
- **`[assets: stub]`** — no dedicated model; ship with a primitive or an existing model as a
  placeholder (e.g. a cylinder for a crop, a Barn for a workshop). Logic is real; art is swapped later.
- **`[assets: backlog]`** — needs new art that won't exist soon; keep the step here for design
  completeness but implement late / behind a flag, or prototype with the nearest stub.

What we actually have (Assets/):
- **Items/food** — Kenney Food Kit (~200 FBX): vegetables, fruit, meat, dairy, eggs, baked goods,
  cooked dishes (bread/loaf/baguette/pie/pizza/cake/donut/sandwich/burger…), barrels/bottles/cartons.
- **Farm buildings** — Quaternius: Barn/BigBarn/SmallBarn/OpenBarn, ChickenCoop, Silo, Windmill,
  WaterTower, Well, Fence.
- **Animals** — Quaternius: Cow, Pig, Sheep, Horse, Llama, Pug, Zebra. (No chicken model; coop exists.)
- **Fish (as items)** — Quaternius: Fish1-3, Shark, Dolphin, Whale, Manta ray.
- **NPC rig + animations** — UAL Standard rig + Mixamo (idle/walk/talk).
- **Decoration** — Textured Stylized Trees.

Not in the repo (→ stub/backlog): crop growth stages, greenhouse, beehive, flowers/bouquets, fishing
spot / rod / boat / shipyard, dedicated crafting-station buildings, market decor / display window /
signboard / cash register, chicken, cat.

---

# BLOCK 0 — Project foundation ✅
*Skeleton you can drop features into. Boots through Bootstrap, builds, has conventions.*

- ✅ 0.1 Folder architecture & namespaces (`Assets/_Project/…`, `Market.<Subsystem>`, asmdefs)
- ✅ 0.2 Asset filtering (packs in place; unused/dupes in `_ArchiveAssets/` outside `Assets/`)
- ✅ 0.3 Scenes & Bootstrap (Bootstrap → MainMenu → Market via `SceneLoader`)
- ✅ 0.4 ServiceLocator + EventBus (type-safe events, no statics, no `FindObjectOfType`)
- ✅ 0.5 Main menu (New Game / Continue / Settings / Quit)
- ✅ 0.6 First build (Player Settings, Build Settings, Build & Run green)

> **Checkpoint 0:** skeleton exists, build is green, there's an entry point.

---

# BLOCK A — Playable skeleton ✅
*A tiny but complete game: walk, buy, place, NPC buys, you earn.*

- ✅ A1 Walking (`FirstPersonController`, `HeadBob`, new Input System)
- ✅ A2 Look & prompt (`IInteractable`, `InteractionSystem` raycast, `InteractionPromptUI`)
- ✅ A3 Money & HUD (`MoneySystem`, `MoneyHUD`)
- ✅ A4 Items & inventory (`ItemSO`, `ItemCategory`, `Inventory`)
- ✅ A5 Supplier (debug) (`SupplierShop`, `DebugSupplierBuy`)
- ✅ A6 Stall (debug) (`MarketStall`, `StallSlot`, `DebugStallPlace`)
- ✅ A7 NavMesh + `NavMeshObstacle` (carve) on the player
- ✅ A8 First NPC & first sale (`NPCVisitor` state machine) — **first full loop**
- ✅ A9 Visitor flow (`NPCSpawner`, `NPCTypeSO`)
- ✅ A10 Saving (`SaveSystem`, `SaveData`, JSON in `persistentDataPath`)

> **Checkpoint A:** a real mini-game exists. Now add depth.

---

# BLOCK B — Stable market, no hidden coefficients ✅
*Predictable, inspectable market. Liveliness comes from time/season/traffic, not multipliers.*

- ✅ B1 Game time (`TimeSystem`, `OnHourChanged`/`OnDayChanged`, pause on menus)
- ✅ B2 Daylight by time (`DaylightSystem` — sun/moon, ambient, skybox exposure)
- ✅ B3 NPC traffic by hour (density curve: morning low, midday peak, night ~0)
- ✅ B4 Seasons (`SeasonManager` — 4 seasons, sky tint, supplier availability)
- ✅ B5 Single fixed-price read point (`PriceCalculator` = buy `BaseBuyPrice`, sell `BaseSellPrice`)
- ✅ B6 Simplify the legacy pricing code (remove/freeze `IPriceModifier`/`PriceContext`; keep
  `GetBuyPrice` / `GetSuggestedSellPrice`)
- ✅ B7 Full-loop auto-debug (`MarketAutoDebugger`: F9 loop, F10 one cycle, snapshots in `game.log`)
- ✅ B8 NPC purchase rules without hidden demand (concrete refusal reasons in logs)
- ✅ B9 Multi-stall prep (mark `NPCSpawner.targetStall` / `GameSaver.marketStall` as temporary
  single-stall API; planned `MarketStallRegistry`)
- ✅ B10 Seasonal supplier assortment without price change (out-of-season goods shown muted/unbuyable)

> **Checkpoint B:** the market is stable and explainable. Next must be UI, or the project stalls in
> Debug.Log.

---

# BLOCK C — UX & player-facing surface
*Turn console output into real screens so a stranger can play. Highest priority of the remaining work.*

### C1. Cursor / UI-mode service `[assets: ready]`
**Do:** one place that switches "game ↔ menu": lock/unlock cursor, show/hide cursor, suppress
player input while a panel is open. Every panel uses it.
**Sub-steps:** `UIModeService` (or extend an existing coordinator) · enter/exit calls from each panel
· guard against double-Esc breaking Play Mode.
**Check:** open any panel → mouse frees; close → FPS control returns; input doesn't leak through.

### C2. InventoryUI `[assets: ready]`
**Do:** Tab opens a slot grid (icon, name, count); hover shows description. Subscribed to
`Inventory.OnChanged`; reflects model, owns no gameplay state.
**Sub-steps:** grid + slot prefab · hover tooltip · open/close via UIModeService · update only on change.
**Check:** buy apple → slot appears; remove → slot disappears.

### C3. ShopUI — supplier ✅
Supplier interaction opens a list (price, seasonal availability, Buy), mouse unlocked, purchase
updates UI. Replaces `DebugSupplierBuy`. **Done.**

### C4. StallUI — stall `[assets: ready]` ✅
**Do:** stall screen — slots, take item from inventory, price input, "Place" and "Remove".
**Sub-steps:** slot list bound to `MarketStall` · drag-or-click item from inventory · price field with
validation (≥ 0; warn below buy price) · place spawns `worldPrefab`, remove returns to inventory.
**Check:** place apple at 25 via UI → it appears in 3D and an NPC can buy it for 25. **Done.**

### C5. PauseMenu `[assets: ready]`
**Do:** Esc → Resume / Save / Settings / Main Menu; `Time.timeScale = 0`.
**Sub-steps:** pause stack via UIModeService · Save calls `GameSaver` · safe return to MainMenu.
**Check:** Esc halts the game, Save works from pause, double-Esc doesn't break Play Mode. **Done.**

### C6. Settings menu `[assets: ready]`
**Do:** `SettingsSO`/PlayerPrefs — mouse sensitivity, invert-Y, volumes (Master/Music/SFX), key
rebinding via Input System.
**Check:** change a setting → applies immediately and persists across sessions.

### C7. AudioMixer & base audio `[assets: stub]` *(deferred → merged into K2)*
**Do:** `AudioMixer` (Master/Music/SFX/Ambient), `AudioService` for one-shots, player footsteps and
market ambience. Wire the SettingsService volume values (C6) to the mixer groups.
(Sound files are stubs/free placeholders until audio pass in K.)
**Check:** footsteps on walk, ambient on the market, settings volumes work.
**→ Skipped for now; implement together with K2 Sound design when audio assets exist.**

### C8. NPC animated model `[assets: partial]`
**Do:** replace the capsule NPC visual with the UAL humanoid model and drive Idle/Walk/Talk from
`NPCVisitor` + `NavMeshAgent`. Visual variety/pool is deferred until more humanoid models/outfits exist.
**Check:** NPCs spawn as the animated UAL model: Walk while moving, Talk while browsing, Idle when still. **Done.**

### C9. Interaction prompt & cursor polish `[assets: ready]`
**Do:** prompt shows the correct device key (KB/Gamepad) via Input System; cursor hidden in game,
visible in menus.
**Check:** prompt key matches the active device; cursor toggles correctly. **Done.**

> **Checkpoint C:** looks like a game, not a prototype. Playable by a stranger.

---

# BLOCK D — Day rhythm, orders, visible progression & attraction
*The "fun layer". A day becomes a short session; NPCs become characters; orders replace world events;
growth shows up physically (staff/signs/decor) — never as hidden buffs.*

### D0. MarketStallRegistry `[assets: ready]`
**Do:** retire the temporary single-stall API from B9 *before* new systems build on it:
`MarketStallRegistry` owns all stalls in the scene; `NPCSpawner.targetStall` and
`GameSaver.marketStall` iterate the registry instead of holding one reference. D11 (props) and
D12 (Stocker) depend on this.
**Check:** two stalls in the scene — NPCs visit both, save/load restores both.

### D1. DayPhaseSystem `[assets: ready]`
**Do:** phases Morning Prep → Market Open → Evening Summary → Night/Next Day; HUD shows the phase.
**Check:** the day advances through phases; HUD reflects it.

### D2. Open / Close stall `[assets: stub]`
**Do:** player manually opens the shop in the morning and closes it in the evening; NPCs only come
while open. (Open/Closed sign uses a stub prop until art exists — see D11.)
**Check:** closed → no NPC purchases; open → traffic flows.

### D3. Evening Summary `[assets: ready]`
**Do:** end-of-day screen: revenue, expenses, profit, items sold, orders done, best-selling item.
**Check:** close the day → a clear report.

### D4. Daily Goals v1 `[assets: ready]`
**Do:** 1–3 simple goals (sell N, earn X, complete an order); small reward/sound/checkmark on
completion.
**Check:** goal met → visible feedback.

### D5. Sleep / Next Day `[assets: stub]`
**Do:** advance to next day via a "End Day" button/bed; time resets to morning, day +1, season
persists. (Bed prop is a stub.)
**Check:** end day → morning of day+1, season state intact.

### D6. NPC personalities `[assets: ready]`
**Do:** `NPCPersonalitySO` — name/role, lines, budget, favorite categories, patience, haggle chance.
At least 5 archetypes: regular, thrifty haggler, rich collector, cook/innkeeper, child/odd buyer.
**Check:** different NPCs show different lines and budgets.

### D7. Dialogue bubble & haggling `[assets: ready]`
**Do:** TMP bubble over the NPC (greeting / price reaction / buy / refuse). Simple haggling: if price
is slightly above budget, NPC may propose its own price; player accepts/declines. No demand formulas.
**Sub-steps:** world-space bubble · patience timer (empty stall/slow player → leaves with a line) ·
accept/decline haggle flow.
**Check:** NPC says "I'll take it for 18", player chooses; empty stall → NPC grumbles and leaves.

### D8. Wishboard / Orders `[assets: stub]`
**Do:** replace world-events with visible NPC requests. `OrderSO` (who, `ItemSO`, count, deadline,
fixed reward, text) → `OrderInstance` + `OrderSystem` (active/done/expired) → board in scene + UI list
→ turn-in from inventory → daily generation (2–4 by available content). (Board model is a stub.)
**Sub-steps:** D8a `OrderSO` data · D8b runtime order lifecycle · D8c Wishboard UI · D8d turn-in flow ·
D8e daily generation · D8f orders as unlock trigger (N done → new item/supplier/NPC).
**Check:** "Cook needs 3 apples by evening" appears in the morning, is turned in for the reward, and
expires after the deadline.

### D9. ReputationSystem `[assets: ready]`
**Do:** single scale; up from completed orders/fair deals, down from failures. Gates access to
orders/suppliers/dialogue — never changes prices directly.
**Check:** complete order → rep up; fail → rep down; a gated order opens at the required rep.

### D10. MarketRating + UnlockSystem `[assets: stub]`
**Do:** `MarketRating` (1–5★) from assortment, completed orders, stable stock; one `UnlockSystem`
list (items, NPCs, recipes, stalls, decor, stations). Rating opens concrete content, no hidden
traffic buff.
**Check:** reach 2★ → a new supplier/decor unlocks and is actually usable; the player sees the cause.

### D11. Physical market props `[assets: stub]`
**Do:** make the stall feel hands-on: crate item (`ItemSO`+count, pick up/place), storage shelf,
restock from crate/shelf (not just abstract Inventory), price-tag prop on a slot, Open/Closed sign or
bell. (All use primitives/Food-Kit barrels/cartons as stubs until dedicated props exist.)
**Sub-steps:** D11a crate · D11b storage shelf · D11c restock-from-crate · D11d price-tag prop ·
D11e open sign/bell.
**Check:** carry a crate of apples to the stall; restock a slot from storage; price visible in world.

### D12. Staff / automation `[assets: ready]`
**Do:** automation as a reward, using existing NPC models. `StaffSO` (role, daily wage, work speed,
hire requirements, lines); HiringBoard UI; Cashier (completes sales while player does other work);
Stocker (refills slots from storage); Cleaner (light, visible mess after busy days). Staff work only
in Market Open, paid in Evening Summary; no money → warning and risk of leaving. Workers cost wages
and have speed/quirks — they never play the whole game for the player.
**Sub-steps:** D12a `StaffSO` · D12b HiringBoard UI · D12c Cashier · D12d Stocker · D12e Cleaner ·
D12f scheduling/wages · D12g balance (player still chooses assortment/prices/orders/growth).
**Check:** hire a cashier → NPCs buy without manual confirmation; wage shows in Evening Summary.

### D13. Visible customer attraction `[assets: backlog]`
**Do:** physical, explainable attraction — never a hidden multiplier. `AttractionObjectSO` (sign,
display window, banner, lantern, decor, demo stand); a signboard/display "item of the day" the player
picks (draws NPCs interested in that category); a manual Promo Day (fixed cost → more visitors for one
Market Open, item price unchanged); a Discount Ad (player picks an item + sale price + pays for
flyers; that day more category-interested NPCs arrive to clear overstock; one item at a time, ends at
day's end, warn when below buy price). (Signs/display/decor need new art → backlog; prototype with
stubs.)
**Sub-steps:** D13a `AttractionObjectSO` · D13b signboard/display "item of the day" · D13c Promo Day ·
D13d Discount Ad · D13e discount rules/guardrails.
**Check:** set apples as item-of-the-day → more food-oriented NPCs (visible in summary/logs); run a
discount on surplus apples → they sell faster at lower per-unit profit, summary shows the ad cost.

### D14. Decorations v1 `[assets: backlog]`
**Do:** 5–10 simple market decorations — purely visual / for rating, no hidden buffs. (Needs art →
backlog; trees and existing props can stub a few.)
**Check:** place decor → market looks richer; rating counts presence.

### D15. Rent / Loans (optional pressure) `[assets: ready]`
**Do:** only after UI is solid. `RentSystem` (fixed per-stall charge each season, 1-day warning);
`LoanSystem` (loan for big unlocks, daily interest, default = lose the object). Keep it simple and
visible.
**Check:** season end → rent charged with prior warning; take/repay a loan cleanly.

> **Checkpoint D:** the day has rhythm and stakes, NPCs are characters, orders drive goals, and growth
> is visible in the world. This is the full fun-slice.
> Ship with a `SaveData.version` bump + migration of old saves + an EditMode migration test.

---

# BLOCK E — Farm (first big vertical slice)
*Give the player their own goods instead of endless resale. Harvest items exist; growth-stage art does not.*

### E1. CropPlot + CropSO `[assets: stub]`
**Do:** `CropPlot` (Empty/Planted/Growing/Ready), `CropSO` (seed, growth time, yield, plant seasons);
buy seeds from supplier (seed `ItemSO`); debug instant-grow. Harvest items use Food-Kit models
(carrot/corn/pumpkin/tomato/…); the growing plot itself is a stub (primitive scaled by progress).
**Check:** buy seed → plant → speed up → harvest a carrot into inventory.

### E2. Crop visual stages `[assets: backlog]`
**Do:** sprout / young / ready meshes per crop, switched by timer progress. Needs new art → backlog;
until then scale/material-swap a stub.
**Check:** the plot visibly grows from sprout to harvest.

### E3. Crop seasonality `[assets: ready]`
**Do:** `CropSO.AvailableSeasons`; out-of-season can't be planted on a normal plot (greenhouse lifts
this later). No ×0.3 slowdown, no random death — just allow/deny with a clear reason.
**Check:** can't plant a summer crop in winter (UI/log explains); plants fine in season.

### E4. Harvest quality (optional) `[assets: ready]`
**Do:** if needed, quality is separate fixed-price `ItemSO` variants (`Carrot`, `Carrot_Good`,
`Carrot_Prize`) — no hidden price multiply. First pass may ship only the normal harvest.
**Check:** harvest yields a clear item with a fixed price.

### E5. Cost of production `[assets: ready]`
**Do:** tie `CropSO` to economy: seed price + grow time → unit cost; base sell ~2–3× cost so growing
beats resale.
**Check:** margin on grown goods > resale; farming is worthwhile.

### E6. Greenhouse `[assets: stub]`
**Do:** `Greenhouse` structure; plots inside ignore season limits; debug-unlocked for now. Use a Barn
(BigBarn/OpenBarn) as a stub building until greenhouse art exists.
**Check:** a winter crop grows inside.

### E7. Beehive `[assets: stub]`
**Do:** `Beehive` yields honey + wax on a timer; needs flowers nearby (see E8). Honey item is ready;
the hive is a stub (barrel/Well) until art exists.
**Check:** place hive → time passes → honey + wax in inventory.

### E8. Flowers & bouquets `[assets: backlog]`
**Do:** flower crops + `RecipeSO` bouquets (3 flowers → bouquet priced above the parts). No flower
models in the repo → backlog; prototype with colored stubs.
**Check:** assemble a bouquet that sells above its parts.

### E9. Farm tutorial `[assets: ready]`
**Do:** scripted hint chain (buy seed → find plot → plant → wait/harvest → place on stall), steps in
`TutorialStepSO`.
**Check:** a new player completes the chain with no outside explanation.

> **Checkpoint E:** full vertical slice — the "playable prototype" you show off.
> Ship with a `SaveData.version` bump + migration of old saves + an EditMode migration test.

---

# BLOCK F — Fishing
*Same template as Block E. Fish items exist; fishing-spot/rod/boat/shipyard art does not.*

### F1. FishingSpot & catching `[assets: stub]`
**Do:** `FishingSpot` interactable; hold E → timer with chance → fish in inventory; basic rod as a
tool `ItemSO`. Water + spot are stubs (a plane + primitive); fish items are ready.
**Check:** fish at the water; sometimes a fish drops.

### F2. Fish types & rarity `[assets: ready]`
**Do:** `FishSO` (chance, price, min rod). Map to the Fish Pack models (Fish1-3/Shark/Dolphin/…).
**Check:** different fish appear; rare ones cost more.

### F3. Spot depletion & recovery `[assets: ready]`
**Do:** spot capacity; chance drops after N catches; recovers over a day/week.
**Check:** active fishing depletes the spot; fish return after recovery.

### F4. Smoking & drying `[assets: stub]`
**Do:** `SmokingStation` turns raw fish into a separate fixed-price `SmokedFishSO` on a timer. Station
is a stub.
**Check:** load fish → wait → smoked fish at a higher price.

### F5. Aquariums as goods `[assets: backlog]`
**Do:** `AquariumStallItem` — live fish as premium decor; needs a "tank" from crafting. Tank art →
backlog.
**Check:** an aquarium on the stall sells rarely but expensively.

### F6. Shipyard & boats `[assets: backlog]`
**Do:** `Shipyard` consumes boards + metal → a sellable boat. Boat/shipyard art → backlog.
**Check:** build a boat → sell for a large sum.

### F7. Ferry crossing `[assets: backlog]`
**Do:** a running ferry gives passive income and brings "other-district" NPCs (rare supplier stock).
Ferry art → backlog.
**Check:** ferry running → daily income; new supplier items appear.

> **Checkpoint F:** one extra specialization fully playable.
> Ship with a `SaveData.version` bump + migration of old saves + an EditMode migration test.

---

# BLOCK G — Animal husbandry
*Animal + coop models exist (no chicken model). Pets partly exist (Pug; no cat).*

### G1. Chickens & eggs `[assets: stub]`
**Do:** `ChickenCoop` (model ready) collects eggs on a timer; feed bought from supplier; egg item
ready. Chicken creature is a stub (no chicken model) — coop can abstract the birds for now.
**Check:** feed → collect eggs → sell.

### G2. Cows, pigs, sheep `[assets: ready]`
**Do:** each animal (Cow/Pig/Sheep models ready) yields its resource (milk/meat/wool) on its own cycle.
**Check:** all three branches produce distinct raw materials.

### G3. Pets & "market happiness" (visible) `[assets: stub]`
**Do:** `PetSO` (Pug ready as dog; cat → backlog; Pig as mini-pig). A pet is a *visible* object that
unlocks NPC lines, cosmetic reactions, themed orders/achievements — **no invisible traffic buff**.
**Check:** add a dog → NPCs occasionally react; a themed order/achievement opens.

### G4. Horses — rental & delivery `[assets: ready]`
**Do:** `Horse` (model ready) can be rented (passive income) or sent to fetch supplier goods (faster,
risky).
**Check:** send the horse → after a timer it returns with goods or empty.

### G5. Horse racing (mini-game) `[assets: ready]`
**Do:** a simple once-a-season event (pick a winner or ride), using the Horse model.
**Check:** race runs; the bet pays out or burns.

> **Checkpoint G:** animal branch fully playable.
> Ship with a `SaveData.version` bump + migration of old saves + an EditMode migration test.

---

# BLOCK H — Crafting & kitchen
*Crafted results exist as Food-Kit models; dedicated station buildings do not (use Barn stubs).*

### H1. CraftingStation base `[assets: stub]`
**Do:** `ICraftingStation` — takes a recipe + ingredients from inventory → timer → result. Station
is a stub building (Barn/SmallBarn) until art exists.
**Check:** craft a simple item from a recipe.

### H2. Bakery `[assets: ready]`
**Do:** `Bakery` consumes grain + eggs → bread/pie/buns. Results map to Food-Kit
bread/loaf/baguette/pie models. (Building stubbed; products ready.)
**Check:** bake bread → sell at a margin (it's a separate `ItemSO`).

### H3. Brewery `[assets: stub]`
**Do:** `Brewery` — grain + honey → beer/mead; long timer, premium price. Drink props stubbed
(bottle/barrel from Food Kit).
**Check:** start a brew → product after a cycle.

### H4. Smithy — tools for other branches `[assets: backlog]`
**Do:** `Smithy` makes improved rods (F), boards (shipyard), garden tools (E). Tool models → backlog;
stub with primitives.
**Check:** craft a rod → fisher unlocks new fish.

### H5. Apothecary `[assets: backlog]`
**Do:** `Apothecary` — herbs + animal products → potions/cosmetics (premium). Potion/herb art →
backlog.
**Check:** brew a potion → sell expensively.

### H6. Tailor `[assets: backlog]`
**Do:** `Tailor` — wool + leather → clothing for seasonal orders/new buyer types (fixed price, no
hidden bonus). Clothing art → backlog.
**Check:** make winter clothing → turn it into a winter order or sell as a fixed-price item.

### H7. Recipes as finds `[assets: ready]`
**Do:** `RecipeBookSO`; recipes aren't given upfront — bought from traveling traders or dropped as
rewards. (Data/UI only.)
**Check:** unlock a recipe → it appears in the station's craft menu.

### H8. Unique recipe & rare item `[assets: ready]`
**Do:** rare recipes yield a unique high fixed-price item with its own orders — no temporary ×2
monopoly. (Reuses existing Food-Kit models.)
**Check:** unlock a unique recipe → make a new item that shows up in orders/achievements.

> **Checkpoint H:** all four specializations playable and linked by materials.
> Ship with a `SaveData.version` bump + migration of old saves + an EditMode migration test.

---

# BLOCK I — Social systems & town
*Relationships, contracts, consequences — still no hidden price/demand changes. Mostly data/UI/logic.*

### I1. NPC dialogue & relationships `[assets: ready]`
**Do:** `NPCRelationSystem` — key NPCs have a relationship that opens lines, orders, rewards. No
hidden price effect.
**Check:** complete the baker's orders a few times → a new line/order/recipe opens.

### I2. Supplier contracts `[assets: ready]`
**Do:** long-term contracts reserve a fixed quantity at a fixed price/prepay — clearer than a dynamic
market.
**Check:** contract for apples → N apples available daily; skipping breaks trust.

### I3. Town licenses `[assets: ready]`
**Do:** `LicenseSystem` — permits to sell categories / build objects / run stations; bought or
unlocked via rating/orders.
**Check:** no fish license → no fish stall; buy it → allowed.

### I4. Reputation in action `[assets: ready]`
**Do:** `ReputationSystem` gates rare recipes, supplier trust, contracts, dialogue, order limits.
Never changes prices directly.
**Check:** high rep → more trust/content; low rep → refusals and closed branches.

> **Checkpoint I:** the player builds relationships with the town, not just sells goods.
> Ship with a `SaveData.version` bump + migration of old saves + an EditMode migration test.

---

# BLOCK J — Co-op
*Last. Solo must be fully working first.*

### J1. Netcode bootstrap `[assets: ready]`
**Do:** add Netcode for GameObjects; Host/Client starter from the menu.
**Check:** a second client connects to the host.

### J2. Second player in scene `[assets: ready]`
**Do:** `NetworkPlayer` — sync position/rotation/animations; see the other player in third person.
**Check:** two clients → you see the other player walking nearby.

### J3. Shared market state `[assets: ready]`
**Do:** server-authoritative time, orders, stalls, storage, rating; wallets personal or shared
(decide at design time).
**Check:** one sells/completes an order → both see the market/progress update.

### J4. Role split / WorkerNPC for solo `[assets: ready]`
**Do:** `WorkerNPC` + `WorkerSystem` cover a specialization at a basic tier for a wage; endgame
(greenhouse/shipyard/unique recipes) stays human-only. In co-op, split roles (stall / farm / fishing).
**Check:** solo hire a fisher worker → basic fish supply; greenhouse stays locked to them.

### J5. Goal scaling `[assets: ready]`
**Do:** final target and rating thresholds scale with the number of human players.
**Check:** solo target is clearly lower than four-player.

> **Checkpoint J:** playable co-op for up to 4.

---

# BLOCK K — Polish & release
*When it all works — finish and ship. Most art-replacement here waits on the artist.*

### K1. Replace placeholder assets `[assets: backlog]`
**Do:** final models/materials (single style), URP fix-ups, LOD groups. Waits on the artist; swap
stubs as art lands.
**Check:** the scene looks coherent; no pink materials; FPS holds.

### K2. Sound design + AudioMixer `[assets: backlog]` *(includes C7)*
**Do:** `AudioMixer` (Master/Music/SFX/Ambient) + `AudioService` for one-shots; wire
SettingsService volume values (C6) to mixer groups; player footsteps, market ambience,
NPC purchase reactions, UI/order/build sounds, achievement jingles.
**Check:** every significant action has a sound; mixer groups work; settings volumes apply.

### K3. First-session tutorial `[assets: ready]`
**Do:** 8–10 hint chain: buy → place → sell → take a board order → complete it → unlock something.
Steps in `TutorialStepSO`.
**Check:** a new player gets through the first ~20–30 min without docs.

### K4. Balance pass `[assets: ready]`
**Do:** prices/timers/wages table, 5–10 playthroughs, tune. Target: a concrete sum in a concrete
number of seasons.
**Check:** playtests reach the goal without exploits or boredom.

### K5. Performance pass `[assets: ready]`
**Do:** NPC pooling, LOD, crowd profiling, GPU instancing for repeated meshes. Target stable 60 FPS.
**Check:** profiler shows < 16 ms/frame at peak.

### K6. UX pass `[assets: ready]`
**Do:** review all screens — consistent style/fonts/contrast, minimal localization (RU/EN via
`LocalizationSO`).
**Check:** a "new player" walks every screen without questions.

### K7. Build pipeline & release `[assets: ready]`
**Do:** Build & Run for target platforms, icons, splash, store page, SemVer. (Versioning already in
place: `VERSION` + `CHANGELOG.md` + `vX.Y.Z` tags.)
**Check:** the build downloads, installs, runs.

### K8. Playtest & patch cycle `[assets: ready]`
**Do:** 3–5 external testers → bug reports → fixes → one minor post-release patch.
**Check:** critical bugs fixed; patch shipped.

> **Checkpoint K:** the game is released.

---

## Key risks (keep in mind)

- **Don't run ahead through blocks.** Resist starting fishing before the market loop is fun — it
  leads to piles of disconnected systems. Hold the order.
- **Don't bring back hidden coefficients.** Want a "lively market"? Add visible content first: new
  orders, suppliers, NPCs, seasonal availability, lines and goals. No droughts, demand ×1.5, rumor ×2.
- **UI (Block C) outranks deep economy.** While shop/inventory/stall live in Debug.Log, deep systems
  are untestable. Stabilize C first.
- **First-person crowds.** Wide aisles (3–4 m), `NavMeshObstacle` on the player (A7), NPCs yield —
  or the player gets stuck in the crowd.
- **Scale.** Reference capsule 1.8 m; measure everything against it from A1. First person is merciless
  about bad scale.
- **Saving gets complex fast.** Built in A10; bump `SaveData.Version` with each big block and migrate.
  Don't stack five blocks without migration.
- **Build after every block.** Run Build & Run at each block checkpoint; build-only bugs (missing
  shaders, AOT) accumulate silently.
- **Co-op is last.** Netcode on top of a finished solo game is fine; designing everything for network
  from scratch is slow and costly.
- **Art is gated.** New models/animations won't arrive soon. Build logic against stubs (`[assets:
  stub]`) and keep `[assets: backlog]` steps behind flags rather than blocking on art.

---

## Progress

### Block 0 — Foundation
- [x] 0.1 Folder architecture & namespaces
- [x] 0.2 Asset filtering
- [x] 0.3 Scenes & Bootstrap
- [x] 0.4 ServiceLocator + EventBus
- [x] 0.5 Main menu
- [x] 0.6 First build

### Block A — Playable skeleton
- [x] A1 Walking · [x] A2 Look & prompt · [x] A3 Money & HUD · [x] A4 Items & inventory
- [x] A5 Supplier (debug) · [x] A6 Stall (debug) · [x] A7 NavMesh + obstacle
- [x] A8 First NPC & sale · [x] A9 Visitor flow · [x] A10 Saving

### Block B — Stable market, no hidden coefficients
- [x] B1 Game time · [x] B2 Daylight · [x] B3 Traffic by hour · [x] B4 Seasons
- [x] B5 Single price point · [x] B6 Simplify pricing · [x] B7 Auto-debug
- [x] B8 NPC purchase rules · [x] B9 Multi-stall prep · [x] B10 Seasonal assortment

### Block C — UX & player-facing surface
- [x] C1 Cursor/UI-mode service
- [x] C2 InventoryUI
- [x] C3 ShopUI (supplier)
- [x] C4 StallUI
- [x] C5 PauseMenu
- [x] C6 Settings menu
- [ ] C7 AudioMixer & base audio *(deferred → K2)*
- [x] C8 NPC animated model *(visual variety deferred until more assets exist)*
- [x] C9 Interaction prompt & cursor polish

### Block D — Day rhythm, orders, progression & attraction
- [ ] D0 MarketStallRegistry
- [ ] D1 DayPhaseSystem
- [ ] D2 Open/Close stall
- [ ] D3 Evening Summary
- [ ] D4 Daily Goals v1
- [ ] D5 Sleep / Next Day
- [ ] D6 NPC personalities
- [ ] D7 Dialogue bubble & haggling
- [ ] D8 Wishboard / Orders (D8a–D8f)
- [ ] D9 ReputationSystem
- [ ] D10 MarketRating + UnlockSystem
- [ ] D11 Physical market props (D11a–D11e)
- [ ] D12 Staff / automation (D12a–D12g)
- [ ] D13 Visible customer attraction (D13a–D13e)
- [ ] D14 Decorations v1
- [ ] D15 Rent / Loans (optional)

### Block E — Farm
- [ ] E1 CropPlot + CropSO · [ ] E2 Visual stages · [ ] E3 Seasonality · [ ] E4 Quality
- [ ] E5 Cost of production · [ ] E6 Greenhouse · [ ] E7 Beehive · [ ] E8 Flowers & bouquets
- [ ] E9 Farm tutorial

### Block F — Fishing
- [ ] F1 FishingSpot · [ ] F2 Fish types · [ ] F3 Depletion/recovery · [ ] F4 Smoking/drying
- [ ] F5 Aquariums · [ ] F6 Shipyard & boats · [ ] F7 Ferry

### Block G — Animal husbandry
- [ ] G1 Chickens & eggs · [ ] G2 Cows/pigs/sheep · [ ] G3 Pets · [ ] G4 Horses · [ ] G5 Racing

### Block H — Crafting & kitchen
- [ ] H1 CraftingStation base · [ ] H2 Bakery · [ ] H3 Brewery · [ ] H4 Smithy
- [ ] H5 Apothecary · [ ] H6 Tailor · [ ] H7 Recipes as finds · [ ] H8 Unique recipe

### Block I — Social systems & town
- [ ] I1 NPC dialogue & relationships · [ ] I2 Supplier contracts · [ ] I3 Town licenses
- [ ] I4 Reputation in action

### Block J — Co-op
- [ ] J1 Netcode bootstrap · [ ] J2 Second player · [ ] J3 Shared market state
- [ ] J4 Role split / WorkerNPC · [ ] J5 Goal scaling

### Block K — Polish & release
- [ ] K1 Replace placeholders · [ ] K2 Sound design · [ ] K3 Tutorial · [ ] K4 Balance
- [ ] K5 Performance · [ ] K6 UX pass · [ ] K7 Build pipeline · [ ] K8 Playtest & patch
