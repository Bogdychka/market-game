# Dev Plan - Market Game (v4, co-op-aware)
**Unity 6 - URP - First Person - developed with Claude Code - designed for 1-2 players (arch up to 4)**

This is the single source of truth for *what to build and in what order*, plus the live progress
checkboxes. Contracts (how to write code, how to work) live elsewhere: `AGENTS.md`
(coding/architecture rules), `CLAUDE.md` (role, git process, response rules). Don't duplicate
progress anywhere but this file.

> **Agents: don't read this file whole (~61 KB ~ 15k tokens).** Current state = the `## Progress`
> section at the bottom; task details = the section of the block you're working on. Grep by step id.

---

## Philosophy

Top rule: **the game runs and is playable at every point in time.** No "two weeks of systems, then
test". Every step below:

- adds exactly one thing;
- ends with a concrete "press Play and confirm X works" check;
- builds on the previous step.

If a step can't be verified in a couple of minutes, it's too big - split it further. Blocks are in
implementation order. Don't jump ahead: specializations are pointless until the core loop is fun.

**No hidden market coefficients.** No drought x1.8, no random demand multipliers, no invisible
world-factor effects. The player understands the economy directly:

- base buy/sell prices live in `ItemSO`;
- the player sets the sell price on the stall;
- NPCs buy by budget, category preference, and concrete requests;
- seasons change *availability* and visuals, never price via magic multipliers;
- depth comes from assortment, production, orders, reputation and progression - not hidden formulas.

### Do
- Every day should tell a small story: who came, what they bought, which order appeared, what unlocked.
- Every system must be explainable to the player in one sentence.
- Progress must be visible in the world: new items, places, NPCs, decor, stations.
- **Every profession follows the same 4-layer skeleton:** manual work -> optimization (gear, recipes,
  processing) -> delegation (workers) -> scale (manager, second site, contracts). Uniqueness lives in
  the *content* of the layers, not the structure - one worker system, one contract system, reused
  everywhere. Better 4 professions finished to layer 4 than 8 stuck at layer 1.
- **Unmet demand is a carrot, never a stick.** An NPC who can't find cheese leaves a Demand Journal
  entry, not a reputation hit. Accumulated demand pays out as a visible sales spike when the player
  finally closes it.
- **Demand ceilings are visible, not hidden.** Each item has a readable weekly demand capacity shown
  in the journal ("tomatoes: demand ~40/wk, you sell 35"). Saturation is a fact the player can see
  coming, not a surprise. This does NOT violate the no-hidden-coefficients rule - it's an open number.
- Automation unlocks *after* the player has learned the manual action. A worker removes chores; it
  doesn't play the game for the player from day one. Workers deliver ~85% of the player's quality -
  a fair trade of margin for freed time, never a strict upgrade.
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
- Reputation loss for things the player couldn't have done. Rep falls ONLY from avoidable active
  failures (sold spoiled goods, broke an accepted contract). An unclaimed journal wish or an untaken
  board order never lowers anything.
- ~~Co-op before a complete solo game~~ **(REVISED, see Block N):** co-op *content* still comes late,
  but the *network foundation* moves to right after Block D. Blocks 0-D were built solo - accepted
  sunk cost; retrofitting them once is cheaper than retrofitting E-I later. Every system from Block N
  onward is written server-authoritative from day one. Solo remains a first-class mode: co-op is a
  modifier layer (demand x, cost x) on top of solo balance, never a separate balance table.
- Competitive mechanics between co-op partners (revenue leaderboards, "who earned more"). Personal
  *attribution* yes, competition no.

---

## Direction & recommended order (fun-first)

The foundation (Blocks 0/A/B) is done: you can walk the market, buy from a supplier, place goods on
a stall, NPCs buy, time/season/daylight run, saving works - all via debug keys. The remaining work is
ordered to make the **manual loop fun before adding deep systems**:

1. **Block C - UI**: replace Debug.Log with real screens. Highest priority; everything else is hard
   to test while the loop lives in the console.
2. **Block D - Day rhythm, orders/Demand Journal, visible progression & attraction**: the day becomes
   a short session (prep -> open -> sell -> evening summary), NPCs gain personalities, the Demand
   Journal replaces "world events", and growth shows up as staff/signs/decor - never as hidden buffs.
3. **Block N - Network foundation** *(moved up from old Block J)*: Netcode bootstrap, second player,
   server-authoritative shared state. Done BEFORE specializations so E-I are network-aware from
   birth. Co-op needn't be *fun* yet - it must be *possible* so nothing built after it assumes a
   single local player.
4. **Blocks E-I - Specializations**: farm, fishing, animals, crafting, social/town. Only once the
   core loop is enjoyable. Each block must reach layer 3-4 (workers/scale), not just layer 1.
   Gated heavily by available art (see legend).
5. **Block J - Co-op balance & experience**, **Block K - Polish & release**: last.

### Minimal fun-slice (the near-term target)
Morning: buy apples -> place them on the stall via UI -> open the shop -> 3 different NPCs arrive with
lines -> one haggles, one buys, one leaves without finding cheese (-> journal +1, no penalty) -> if you over-bought, run a discount ad to clear
stock -> evening summary -> after 3 completed orders a new item/decor unlocks -> the player sees a goal
like "hire a cashier" or "put up a sign". If this is fun, add farm/fishing/animals/co-op. If it
isn't, no big system will save it.

---

## Asset availability legend

Per project decision, plan steps are tagged by whether the art already exists in the repo. The artist
won't be making new models/animations for a while, so steps needing new art are stubbed or deferred,
not blocked.

- **`[assets: ready]`** - models/animations exist in the project; build the real thing now.
- **`[assets: stub]`** - no dedicated model; ship with a primitive or an existing model as a
  placeholder (e.g. a cylinder for a crop, a Barn for a workshop). Logic is real; art is swapped later.
- **`[assets: backlog]`** - needs new art that won't exist soon; keep the step here for design
  completeness but implement late / behind a flag, or prototype with the nearest stub.

What we actually have (Assets/):
- **Items/food** - Kenney Food Kit (~200 FBX): vegetables, fruit, meat, dairy, eggs, baked goods,
  cooked dishes (bread/loaf/baguette/pie/pizza/cake/donut/sandwich/burger...), barrels/bottles/cartons.
- **Farm buildings** - Quaternius: Barn/BigBarn/SmallBarn/OpenBarn, ChickenCoop, Silo, Windmill,
  WaterTower, Well, Fence.
- **Animals** - Quaternius: Cow, Pig, Sheep, Horse, Llama, Pug, Zebra. (No chicken model; coop exists.)
- **Fish (as items)** - Quaternius: Fish1-3, Shark, Dolphin, Whale, Manta ray.
- **NPC rig + animations** - UAL Standard rig + Mixamo (idle/walk/talk).
- **Decoration** - Textured Stylized Trees.

Not in the repo (-> stub/backlog): crop growth stages, greenhouse, beehive, flowers/bouquets, fishing
spot / rod / boat / shipyard, dedicated crafting-station buildings, market decor / display window /
signboard / cash register, chicken, cat.

---

# BLOCK 0 - Project foundation [done]
*Skeleton you can drop features into. Boots through Bootstrap, builds, has conventions.*

- [done] 0.1 Folder architecture & namespaces (`Assets/_Project/...`, `Market.<Subsystem>`, asmdefs)
- [done] 0.2 Asset filtering (packs in place; unused/dupes in `_ArchiveAssets/` outside `Assets/`)
- [done] 0.3 Scenes & Bootstrap (Bootstrap -> MainMenu -> Market via `SceneLoader`)
- [done] 0.4 ServiceLocator + EventBus (type-safe events, no statics, no `FindObjectOfType`)
- [done] 0.5 Main menu (New Game / Continue / Settings / Quit)
- [done] 0.6 First build (Player Settings, Build Settings, Build & Run green)

> **Checkpoint 0:** skeleton exists, build is green, there's an entry point.

---

# BLOCK A - Playable skeleton [done]
*A tiny but complete game: walk, buy, place, NPC buys, you earn.*

- [done] A1 Walking (`FirstPersonController`, `HeadBob`, new Input System)
- [done] A2 Look & prompt (`IInteractable`, `InteractionSystem` raycast, `InteractionPromptUI`)
- [done] A3 Money & HUD (`MoneySystem`, `MoneyHUD`) - *v4: wallet becomes SHARED + host-authoritative in
  N3 (N0-c); HUD unchanged*
- [done] A4 Items & inventory (`ItemSO`, `ItemCategory`, `Inventory`) - *v4 decision: inventories are
  PERSONAL per player; stalls/storage are shared (N3). `Inventory` gets an owner id (N0-c)*
- [done] A5 Supplier (debug) (`SupplierShop`, `DebugSupplierBuy`)
- [done] A6 Stall (debug) (`MarketStall`, `StallSlot`, `DebugStallPlace`)
- [done] A7 NavMesh + `NavMeshObstacle` (carve) on the player - *v4: same carve for every `NetworkPlayer`*
- [done] A8 First NPC & first sale (`NPCVisitor` state machine) - **first full loop**
- [done] A9 Visitor flow (`NPCSpawner`, `NPCTypeSO`) - *v4: spawner will consume B3's new open
  multipliers (rep, player count) - no rewrite, new inputs (N0-e)*
- [done] A10 Saving (`SaveSystem`, `SaveData`, JSON in `persistentDataPath`) - *v4: in co-op the save is
  HOST-owned; add per-player attribution blobs (N5); keep the migration chain unbroken (N0-f)*

> **Checkpoint A:** a real mini-game exists. Now add depth.

---

# BLOCK B - Stable market, no hidden coefficients [done]
*Predictable, inspectable market. Liveliness comes from time/season/traffic, not multipliers.*

- [done] B1 Game time (`TimeSystem`, `OnHourChanged`/`OnDayChanged`) - *pause is currently `Time.timeScale
  = 0` from `PauseMenuController`; the `TimeSystem.Pause/Resume` API exists but has no callers (remove
  or wire in N0-a). v4 REVISION REQUIRED (N0-a): menu pause is SOLO-only. In a networked session the
  clock is host-authoritative and never stops for a client's menu. Day length target ~13 real min*
- [done] B2 Daylight by time (`DaylightSystem` - sun/moon, ambient, skybox exposure)
- [done] B3 NPC traffic by hour (density curve: morning low, midday peak, night ~0) - *v4 EXTENSION
  (N0-e): curve gains two OPEN multipliers - reputation tier (D9) and human player count (J1,
  x~1.6-1.7 for 2P). Both shown in Evening Summary ("visitors: base 20 x rep 1.3 x duo 1.6") -
  still no hidden buffs, the multipliers are printed*
- [done] B4 Seasons (`SeasonManager` - 4 seasons, sky tint, supplier availability) - *v4 config: season
  length = 14 days; winter allows no open-plot planting BY DESIGN (enforced in E3, not here)*
- [done] B5 Single fixed-price read point (`PriceCalculator` = buy `BaseBuyPrice`, sell `BaseSellPrice`)
- [done] B6 Simplify the legacy pricing code (remove/freeze `IPriceModifier`/`PriceContext`; keep
  `GetBuyPrice` / `GetSuggestedSellPrice`)
- [done] B7 Full-loop auto-debug (`MarketAutoDebugger`: F9 loop, F10 one cycle, snapshots in `game.log`)
  - *v4: extend to a host+client smoke mode once N3 lands (N0-g) - the cheapest co-op regression net*
- [done] B8 NPC purchase rules without hidden demand (concrete refusal reasons in logs) - *v4: this
  refusal pipe is the Demand Journal's data source (D8g) - do NOT refactor it away*
- [done] B9 Multi-stall prep (mark `NPCSpawner.targetStall` / `GameSaver.marketStall` as temporary
  single-stall API; planned `MarketStallRegistry`) - *retired by D0 as planned*
- [done] B10 Seasonal supplier assortment without price change (out-of-season goods shown muted/unbuyable)

> **Checkpoint B:** the market is stable and explainable. Next must be UI, or the project stalls in
> Debug.Log.

---

# BLOCK C - UX & player-facing surface
*Turn console output into real screens so a stranger can play. Highest priority of the remaining work.*

### C1. Cursor / UI-mode service `[assets: ready]`
**Do:** one place that switches "game <-> menu": lock/unlock cursor, show/hide cursor, suppress
player input while a panel is open. Every panel uses it.
**Sub-steps:** `UIModeService` (or extend an existing coordinator) - enter/exit calls from each panel
 - guard against double-Esc breaking Play Mode.
**Check:** open any panel -> mouse frees; close -> FPS control returns; input doesn't leak through.

### C2. InventoryUI `[assets: ready]`
**Do:** Tab opens a slot grid (icon, name, count); hover shows description. Subscribed to
`Inventory.OnChanged`; reflects model, owns no gameplay state.
**Sub-steps:** grid + slot prefab - hover tooltip - open/close via UIModeService - update only on change.
**Check:** buy apple -> slot appears; remove -> slot disappears.

### C3. ShopUI - supplier [done]
Supplier interaction opens a list (price, seasonal availability, Buy), mouse unlocked, purchase
updates UI. Replaces `DebugSupplierBuy`. **Done.**

### C4. StallUI - stall `[assets: ready]` [done]
**Do:** stall screen - slots, take item from inventory, price input, "Place" and "Remove".
**Sub-steps:** slot list bound to `MarketStall` - drag-or-click item from inventory - price field with
validation (>= 0; warn below buy price) - place spawns `worldPrefab`, remove returns to inventory.
**Check:** place apple at 25 via UI -> it appears in 3D and an NPC can buy it for 25. **Done.**
*v4 note (N0-d): slots become server-authoritative in N3; when a player has a slot open for editing,
soft-lock it for the partner (small "in use" badge) - cheapest fix for concurrent edits.*

### C5. PauseMenu `[assets: ready]` [done] *(v4 REVISION REQUIRED - N0-b)*
**Do (v3, shipped):** Esc -> Resume / Save / Settings / Main Menu; `Time.timeScale = 0`.
**v4 revision:** `Time.timeScale = 0` is valid ONLY solo (host with 0 clients). In a networked
session Esc opens the same menu as a LOCAL overlay - the world keeps running (host clock, N3);
"Save" stays host-only; a client's "Main Menu" disconnects that client without stopping the host.
UIModeService (C1) keeps working unchanged - only the timeScale call becomes mode-aware.
**Check:** solo Esc halts the game as before; in co-op Esc frees the mouse but NPCs keep walking;
double-Esc still doesn't break Play Mode. **Done (v3) -> retrofit in N0.**

### C6. Settings menu `[assets: ready]`
**Do:** `SettingsSO`/PlayerPrefs - mouse sensitivity, invert-Y, volumes (Master/Music/SFX), key
rebinding via Input System.
**Check:** change a setting -> applies immediately and persists across sessions.

### C7. AudioMixer & base audio `[assets: stub]` *(deferred -> merged into K2)*
**Do:** `AudioMixer` (Master/Music/SFX/Ambient), `AudioService` for one-shots, player footsteps and
market ambience. Wire the SettingsService volume values (C6) to the mixer groups.
(Sound files are stubs/free placeholders until audio pass in K.)
**Check:** footsteps on walk, ambient on the market, settings volumes work.
**-> Skipped for now; implement together with K2 Sound design when audio assets exist.**

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

# BLOCK D - Day rhythm, orders, visible progression & attraction
*The "fun layer". A day becomes a short session; NPCs become characters; orders replace world events;
growth shows up physically (staff/signs/decor) - never as hidden buffs.*

### D0. MarketStallRegistry `[assets: ready]`
**Do:** retire the temporary single-stall API from B9 *before* new systems build on it:
`MarketStallRegistry` owns all stalls in the scene; `NPCSpawner.targetStall` and
`GameSaver.marketStall` iterate the registry instead of holding one reference. D11 (props) and
D12 (Stocker) depend on this.
**Check:** two stalls in the scene - NPCs visit both, save/load restores both.

**Status:** Done.

### D1. DayPhaseSystem `[assets: ready]`
**Do:** phases Morning Prep -> Market Open -> Evening Summary -> Night/Next Day; HUD shows the phase.
**The day ends by TIMER, not by sleep** (target ~13 real minutes/day, ~10 "working"). "End Day"
button (D5) is an optional early skip, never a requirement - in co-op nobody can hold the day
hostage, and Evening Summary arrives for everyone simultaneously.
**Check:** the day advances through phases on the clock; HUD reflects it; skipping is optional.
**Status:** Done to v3 spec (sleep-driven day end) -> **v4 retrofit N0-a:** convert to timer-driven
end with optional skip. Phases/HUD survive as-is; only the advance trigger changes.

### D2. Open / Close stall `[assets: stub]`
**Do:** player manually opens the shop in the morning and closes it in the evening; NPCs only come
while open. (Open/Closed sign uses a stub prop until art exists - see D11.)
**Check:** closed -> no NPC purchases; open -> traffic flows.
**Status:** Done. *v4 note: open/closed is one authoritative flag (N3); either partner may toggle it.*

### D3. Evening Summary `[assets: ready]`
**Do:** end-of-day screen: revenue, expenses, profit, items sold, orders done, best-selling item,
top unmet demand (teaser line from the Demand Journal - "cheese was asked for 3x today"). Design it
as a shared "evening planning" moment: in co-op both players see the same screen and the journal
side-by-side - this 2-minute ritual is where partners split tomorrow's tasks. The player should
always leave this screen with 1-2 unfinished intentions for tomorrow (the "one more day" hook).
**Check:** close the day -> a clear report; unmet-demand teaser visible.

### D4. Daily Goals v1 `[assets: ready]`
**Do:** 1-3 simple goals (sell N, earn X, complete an order); small reward/sound/checkmark on
completion.
**Check:** goal met -> visible feedback.

### D5. Sleep / Next Day `[assets: stub]` *(v4 REVISION REQUIRED - N0-a)*
**Do (v3, shipped):** advance to next day via a "End Day" button/bed; time resets to morning, day +1,
season persists. (Bed prop is a stub.)
**v4 revision:** the day now ends by TIMER (D1); "End Day"/bed is demoted to an OPTIONAL early skip.
Solo: skip freely. Co-op: skip fires only with both players' consent (a "ready to end day" toggle;
day ends when all humans are ready OR the timer runs out) - nobody holds the day hostage, nobody
fast-forwards the partner.
**Check:** ignore the bed -> the day still ends on the clock; solo skip works; in co-op one player's
skip request shows a waiting badge until the partner agrees. **Done (v3) -> retrofit in N0.**

### D6. NPC personalities `[assets: ready]`
**Do:** `NPCPersonalitySO` - name/role, lines, budget, favorite categories, patience, haggle chance.
At least 5 archetypes: regular, thrifty haggler, rich collector, cook/innkeeper, child/odd buyer.
**Check:** different NPCs show different lines and budgets.

### D7. Dialogue bubble & haggling `[assets: ready]`
**Do:** TMP bubble over the NPC (greeting / price reaction / buy / refuse). Simple haggling: if price
is slightly above budget, NPC may propose its own price; player accepts/declines. No demand formulas.
**Sub-steps:** world-space bubble - patience timer (empty stall/slow player -> leaves with a line) -
accept/decline haggle flow.
**Check:** NPC says "I'll take it for 18", player chooses; empty stall -> NPC grumbles and leaves.

### D8. Orders + Demand Journal `[assets: stub]`
Two connected halves. **Orders** = explicit quests with deadlines (unchanged from the old plan).
**Demand Journal** = the passive ledger of everything NPCs wanted and didn't get - the game's main
long-term carrot and the replacement for Old-Market-style "missing item" reputation punishment.

**Orders (as before):** `OrderSO` (who, `ItemSO`, count, deadline, fixed reward, text) ->
`OrderInstance` + `OrderSystem` (active/done/expired) -> board in scene + UI list -> turn-in from
inventory -> daily generation (2-4 by available content). (Board model is a stub.)
An order that expires *untaken* costs nothing. Only an *accepted-then-failed* order hurts rep (D9).

**Demand Journal (new):** when an NPC leaves without finding what they wanted (B8 already logs
concrete refusal reasons - reuse that pipe), the item is tallied: "cheese: asked 14x this week".
- No penalty, ever. The journal is intel + foregone profit made visible; greed motivates, not fear.
- **Accumulated demand -> payoff spike:** when the player first stocks a long-demanded item, the
  pent-up demand converts into a short visible rush (queued NPCs, thank-you lines, above-normal
  volume for a few days). Closing a gap must feel like a *peak*, not a return to zero.
- **Composition rule:** ~70% of journal entries must be closable now or in 1-2 steps, ~25% next
  progression tier, rare single entries = far endgame dream. Generator respects unlocked content.
- **Saturation display:** per-item weekly demand capacity vs. player's actual sales ("tomatoes:
  ~40/wk, you sell 35"). Capacity grows with market rating/traffic - so the mono-farmer discovers
  that the fastest way to sell MORE tomatoes is to close OTHER journal entries and raise traffic.
- Journal breathes with seasons (candles & spices spike before the winter fair - visible weeks
  ahead, so the player can prepare: planning over reaction).
- Recurring heavy demand from one NPC personality escalates into a personal contract offer (-> I2).
- No pushy notifications: the journal waits to be opened. The Evening Summary teaser (D3) is the
  only surfacing.
**Sub-steps:** D8a `OrderSO` data - D8b runtime order lifecycle - D8c board UI - D8d turn-in flow -
D8e daily generation - D8f orders as unlock trigger - **D8g refusal->journal tally pipe** -
**D8h Journal UI (item, times asked, weekly capacity vs sold)** - **D8i pent-up demand spike** -
**D8j 70/25/5 generation weights** - **D8k seasonal demand curves (visible)**.
**Check:** NPC fails to find cheese -> journal +1, rep unchanged; stock cheese after 14 asks -> rush
for 2-3 days, journal entry marked closed; tomato line shows 35/40 and the ceiling is readable.

### D9. ReputationSystem `[assets: ready]`
**Do:** single scale, **asymmetric by design**: UP from actions (completed orders, closed journal
demand, high-quality goods sold, fair haggles, events held); DOWN only from avoidable active
failures (sold spoiled goods, broke an *accepted* contract/order). Never down from absence: unmet
journal demand, untaken board orders, or missing assortment cost nothing - "didn't do it" means
"didn't grow", not "shrank". Gates access to orders/suppliers/dialogue - never changes prices
directly. Rep drives visitor traffic (B3 density scales with rep) -> which raises journal demand
capacity (D8h) -> the growth loop.
**Check:** complete order -> rep up; ignore a board order to expiry -> rep unchanged; sell spoiled ->
rep down; a gated order opens at the required rep; higher rep -> visibly more visitors.

### D10. MarketRating + UnlockSystem + Story frame `[assets: stub]`
**Do:** `MarketRating` (1-5star) from assortment, completed orders, stable stock; one `UnlockSystem`
list (items, NPCs, recipes, stalls, decor, stations). Rating opens concrete content, no hidden
traffic buff.
**Story frame ("former glory"):** one line of lore - the player inherits a run-down market and the
end goal is restoring its former glory. Milestones are phrased through BREADTH, not money: "5 stalls
operating", "60% of demand types covered", "the Grand Fair held". **The finale requires all
professions active** (solo: unlocked & staffed at basic tier; co-op: split between players) - the
fair needs produce AND baked goods AND fish AND crafts, so a mono-path is possible but visibly
capped by the story door. After the finale -> unrestricted freeplay.
**Visual revival is the reward:** each star / story milestone physically changes the market - empty
stalls fill, lights, banners, new NPC idlers, music. The world the player changed IS the progress
bar. (Art-heavy parts -> stub/backlog flags per prop, logic now.)
**Check:** reach 2star -> a new supplier/decor unlocks, a boarded-up stall visibly opens, and the player
sees the cause; milestone screen shows breadth conditions, not a money target.

### D11. Physical market props `[assets: stub]`
**Do:** make the stall feel hands-on: crate item (`ItemSO`+count, pick up/place), storage shelf,
restock from crate/shelf (not just abstract Inventory), price-tag prop on a slot, Open/Closed sign or
bell. (All use primitives/Food-Kit barrels/cartons as stubs until dedicated props exist.)
**Sub-steps:** D11a crate - D11b storage shelf - D11c restock-from-crate - D11d price-tag prop -
D11e open sign/bell.
**Check:** carry a crate of apples to the stall; restock a slot from storage; price visible in world.

### D12. Staff / automation `[assets: ready]`
**Do:** automation as a reward, using existing NPC models. `StaffSO` (role, daily wage, work speed,
hire requirements, lines); HiringBoard UI; Cashier (completes sales while player does other work);
Stocker (refills slots from storage); Cleaner (light, visible mess after busy days). Staff work only
in Market Open, paid in Evening Summary; no money -> warning and risk of leaving. Workers cost wages
and have speed/quirks - they never play the whole game for the player.
**Sub-steps:** D12a `StaffSO` - D12b HiringBoard UI - D12c Cashier - D12d Stocker - D12e Cleaner -
D12f scheduling/wages - D12g balance (player still chooses assortment/prices/orders/growth).
**Balance anchors:** staff output ~ 85% of the player's own quality/speed - good enough to free the
player, never a strict replacement. Wage ballpark 15-25/day (noticeable, not choking). Solo pacing
target: rising chores make the first hire *wanted* around day ~40; in co-op two pairs of hands push
that to ~day 55-60 - later, but it must still arrive, because demand scales with players (J-block).
If co-op pairs reach endgame having never hired anyone, demand scaling is too weak.
`StaffSO` is THE generic worker skeleton - later profession workers (farmhands E11, fisher J-era
`WorkerNPC`) are `StaffSO` instances with different stat names (speed/diligence), not new systems.
**Check:** hire a cashier -> NPCs buy without manual confirmation; wage shows in Evening Summary;
staffed work is measurably slightly worse than hands-on play.

### D13. Visible customer attraction `[assets: backlog]`
**Do:** physical, explainable attraction - never a hidden multiplier. `AttractionObjectSO` (sign,
display window, banner, lantern, decor, demo stand); a signboard/display "item of the day" the player
picks (draws NPCs interested in that category); a manual Promo Day (fixed cost -> more visitors for one
Market Open, item price unchanged); a Discount Ad (player picks an item + sale price + pays for
flyers; that day more category-interested NPCs arrive to clear overstock; one item at a time, ends at
day's end, warn when below buy price). (Signs/display/decor need new art -> backlog; prototype with
stubs.)
**Sub-steps:** D13a `AttractionObjectSO` - D13b signboard/display "item of the day" - D13c Promo Day -
D13d Discount Ad - D13e discount rules/guardrails.
**Check:** set apples as item-of-the-day -> more food-oriented NPCs (visible in summary/logs); run a
discount on surplus apples -> they sell faster at lower per-unit profit, summary shows the ad cost.

### D14. Decorations v1 `[assets: backlog]`
**Do:** 5-10 simple market decorations - purely visual / for rating, no hidden buffs. (Needs art ->
backlog; trees and existing props can stub a few.)
**Check:** place decor -> market looks richer; rating counts presence.

### D15. Rent / Loans (optional pressure) `[assets: ready]`
**Do:** only after UI is solid. `RentSystem` (fixed per-stall charge each season, 1-day warning);
`LoanSystem` (loan for big unlocks, daily interest, default = lose the object). Keep it simple and
visible.
**Check:** season end -> rent charged with prior warning; take/repay a loan cleanly.

> **Checkpoint D:** the day has rhythm and stakes, NPCs are characters, orders drive goals, and growth
> is visible in the world. This is the full fun-slice.
> Ship with a `SaveData.version` bump + migration of old saves + an EditMode migration test.

---

# BLOCK N - Network foundation *(moved up from old Block J - do BEFORE specializations)*
*Rationale: bolting netcode onto five finished specializations = rewriting five specializations.
Bolting it on after Block D = retrofitting one core once, then building E-I network-aware from birth.
Co-op does not need to be FUN here - it needs to be POSSIBLE, so no later system assumes a single
local player. Solo continues to run through the same host-authoritative path (host without clients).*

### N0. Retrofit audit of shipped (v3) systems `[assets: ready]` - DO FIRST in this block
*Everything below is already built and working, but to a pre-co-op / pre-journal spec. Fix in one
pass here, not lazily later - each item is small, and together they are the whole cost of the
"accepted sunk cost" decision. Nothing shipped gets deleted; five items get revised, four get
extended.*
- **N0-a Time & day-end (B1 + D1 + D5):** kill "pause on menus" as a global rule (solo-only);
  day ends by host-authoritative timer (~13 real min); bed/"End Day" demoted to optional skip
  (co-op: all-humans-ready consent).
- **N0-b PauseMenu (C5):** `timeScale = 0` only when solo; networked Esc = local overlay, world runs.
- **N0-c Money & Inventory (A3 + A4):** `MoneySystem` -> single shared host-authoritative wallet;
  `Inventory` gains an owner id - personal per player; stalls/storage shared.
- **N0-d StallUI (C4):** slots server-authoritative; soft-lock a slot while a player edits it.
- **N0-e Traffic (B3 + A9):** density curve x reputation tier (D9) x player count (J1) - both
  multipliers OPEN and printed in Evening Summary. Spawner consumes the product, no rewrite.
- **N0-f SaveSystem (A10):** host-owned save in co-op; per-player attribution blobs (N5); one
  unbroken migration chain from current saves.
- **N0-g AutoDebugger (B7):** host+client smoke mode - one F-key runs the full loop with a fake
  second client; becomes the co-op regression net for every later block.
**Check:** all v3 checkboxes still pass in solo AFTER the retrofit (regression), plus: client menu
doesn't stop host time; two wallets impossible; a v3 save loads and migrates.

### N1. Netcode bootstrap `[assets: ready]` *(was J1)*
**Do:** add Netcode for GameObjects; Host/Client starter from the menu; solo = host with 0 clients
(one code path for both modes).
**Check:** a second client connects to the host; solo still boots through the host path.

### N2. Second player in scene `[assets: ready]` *(was J2)*
**Do:** `NetworkPlayer` - sync position/rotation/animations; see the other player in third person.
**Check:** two clients -> you see the other player walking nearby.

### N3. Server-authoritative shared state `[assets: ready]` *(was J3, decision made)*
**Do:** host owns time/day-phase, money, inventory-on-stalls, orders, Demand Journal, reputation,
rating, saves. **Wallet decision: SHARED** (one market, one goal) - personal recognition comes from
attribution (N5), not personal money. Retrofit existing systems (Time, Money, Stall, OrderSystem)
to authoritative state + client RPCs; bump `SaveData.Version` + migration.
**Check:** one player sells / completes an order / closes a journal entry -> both see money, journal
and rating update; day phase ticks identically on both.

### N4. Asymmetric sessions rule `[assets: ready]`
**Do:** the world is playable whenever any owner is online. Another player's profession objects in
their absence can be *maintained* (water, harvest ready goods, sell stock) but not *developed*
(no building, upgrading, or skill progress on their behalf). Ownership tag on profession
buildings/plots; maintain-vs-develop permission check.
**Check:** partner offline -> I can water their crops and sell their bread, but the "upgrade"
button on their bakery is locked with a clear reason.

### N5. Personal attribution `[assets: ready]`
**Do:** per-player profile: profession mastery, personal journal-closes, quality records
("Masha's prize pumpkin", "Petya's cheese stall"). Pure recognition layer over the shared wallet -
people want visible contribution, not accounting.
**Check:** each player's profile shows their own deeds; Evening Summary names who did what.

> **Checkpoint N:** two players share one authoritative market; solo runs the same path. Nothing
> after this line may be written against local-only state.
> Ship with a `SaveData.version` bump + migration of old saves + an EditMode migration test.

---

# BLOCK E - Farm (first big vertical slice)
*Give the player their own goods instead of endless resale. Harvest items exist; growth-stage art does not.*

### E1. CropPlot + CropSO `[assets: stub]`
**Do:** `CropPlot` (Empty/Planted/Growing/Ready), `CropSO` (seed, growth time, yield, plant seasons);
buy seeds from supplier (seed `ItemSO`); debug instant-grow. Harvest items use Food-Kit models
(carrot/corn/pumpkin/tomato/...); the growing plot itself is a stub (primitive scaled by progress).
**Check:** buy seed -> plant -> speed up -> harvest a carrot into inventory.
**Status:** Done. *v4 note: the plot state machine WILL grow (care state for quality E4, fatigue
E10, ownership tag N4) - when extending, bump `SaveData.Version` and migrate planted plots rather
than resetting them.*

### E2. Crop visual stages `[assets: backlog]`
**Do:** sprout / young / ready meshes per crop, switched by timer progress. Needs new art -> backlog;
until then scale/material-swap a stub.
**Check:** the plot visibly grows from sprout to harvest.

### E3. Crop seasonality `[assets: ready]`
**Do:** `CropSO.AvailableSeasons`; out-of-season can't be planted on a normal plot (greenhouse lifts
this later). No x0.3 slowdown, no random death - just allow/deny with a clear reason.
**Check:** can't plant a summer crop in winter (UI/log explains); plants fine in season.

### E4. Harvest quality - NOT optional anymore `[assets: ready]`
**Do:** quality = the skill payoff of manual care and the whole point of layer 1. Separate
fixed-price `ItemSO` variants (`Carrot`, `Carrot_Good`, `Carrot_Prize`) - no hidden multiply.
Quality derives from care: missed watering/weeding drops the tier. Stars must be VISIBLE on the item
(collectible pride, not a tooltip number) - a prize pumpkin is a goal in itself and the Grand Fair
contest input (D10). Staff working the plot caps at Good tier (the 85% rule, D12) - Prize stays
human-only.
**Check:** perfect care -> Prize item with visible stars; skipped watering -> tier drops with a clear
reason in the plot tooltip; farmhand-tended plot never yields Prize.

### E5. Cost of production & crop roles `[assets: ready]`
**Do:** tie `CropSO` to economy: seed price + grow time -> unit cost; base sell ~2-3x cost so growing
beats resale. Balance every crop by **profit/day/plot = (sell - seed) / growth days**, and price
inconvenience: harder/longer/riskier crops earn MORE per day ("convenience costs margin").
**Season = 14 days; each season ships exactly 4 crop roles** (don't add crops without a role):
- *sprinter* - 2-3 d, cheap, unkillable (radish; teaches the loop, first-evening money);
- *anchor* - mid, reliable (salad/tomato/cabbage; tomato re-harvests every 3 d);
- *investment* - long, expensive, care-hungry, top profit/day (melon ~5.5/d, pumpkin ~7/d; the most
  expensive early-game mistake if neglected);
- *special* - breaks a rule mechanically (pea/hop = re-harvest + trellis build; sunflower = zero
  care for busy players; strawberry = perennial, bad year 1 / great year 2 - "past me was smart").
Hop exists for synergy: planted for journal demand / brewery contracts, not the price table. Winter
= no open planting BY DESIGN: the season where past decisions pay (preserves, greenhouse, honey).
**Check:** an in-editor balance sheet lists all crops with profit/day; roles cover each season;
resale never beats the season's anchor.

### E6. Greenhouse `[assets: stub]`
**Do:** `Greenhouse` structure; plots inside ignore season limits; debug-unlocked for now. Use a Barn
(BigBarn/OpenBarn) as a stub building until greenhouse art exists.
**Check:** a winter crop grows inside.

### E7. Beehive `[assets: stub]`
**Do:** `Beehive` yields honey + wax on a timer; needs flowers nearby (see E8). Honey item is ready;
the hive is a stub (barrel/Well) until art exists.
**Check:** place hive -> time passes -> honey + wax in inventory.

### E8. Flowers & bouquets `[assets: backlog]`
**Do:** flower crops + `RecipeSO` bouquets (3 flowers -> bouquet priced above the parts). No flower
models in the repo -> backlog; prototype with colored stubs.
**Check:** assemble a bouquet that sells above its parts.

### E9. Farm tutorial `[assets: ready]`
**Do:** scripted hint chain (buy seed -> find plot -> plant -> wait/harvest -> place on stall), steps in
`TutorialStepSO`.
**Check:** a new player completes the chain with no outside explanation.

### E10. Soil fatigue - ONE rule `[assets: ready]`
**Do:** a plot "tires" of repeating the same crop; rotate or fertilize. One rule, no pH/nutrients -
that single rule already creates season planning. Visible plot state + one-line explanation.
**Check:** third same-crop planting in a row -> reduced yield with a shown reason; rotation clears it.

### E11. Tool ladder ending in automation `[assets: stub]`
**Do:** watering can -> hose (more plots per minute) -> drip irrigation (plots along the pipe water
themselves). The top tool *removes* the chore, not speeds it - the built-in bridge from layer 1 to
layer 3. Pipe/hose props are stubs.
**Check:** drip-irrigated row needs no manual watering; upgrade is felt physically (morning round
shrinks).

### E12. Kitchen & preserving *(moved here from Block H - it's farm layer 2, not late crafting)*
`[assets: stub]`
**Do:** `Kitchen` station (Barn stub): tomato -> sauce (3 tomatoes + 1 day -> jar, `ItemSO` at a
fixed higher price). **Preserves don't spoil** - that's their entire point: summer overflow (when
tomatoes hit their demand ceiling, D8h) converts into winter stock. No seasonal price multiplier
needed: in winter fresh goods are simply unavailable and the journal visibly demands preserved food,
so volume (and the player's own pricing within NPC budgets) does the work - philosophy intact.
**Check:** overflow tomatoes -> jars in summer; in winter the journal shows preserved-food demand and
jars sell through; the tomato ceiling + kitchen unlock land in the same game-week (mid-summer).

### E13. Farmhands & farm manager (layers 3-4) `[assets: ready]`
**Do:** farmhands = `StaffSO` (D12) instances with speed/diligence stats; assign per task (water /
weed / harvest); diligence caps harvest quality (E4). Seasonal hires for harvest rush at premium
wage. Later: `FarmManager` - the player sets a season plan (which crops, which rows), the manager
allocates farmhands; player's job shifts from "water plot" to "plan season". Second field = new
plot cluster elsewhere (different soil -> different crop affinity, not a copy).
**Check:** farmhand waters assigned rows at 85% quality; manager executes a set season plan;
player's morning shrinks to inspection + planning.

> **Checkpoint E:** full vertical slice - the "playable prototype" you show off. The farm alone must
> demonstrate all 4 layers and stay fun for 3-4 hours; if it doesn't, no amount of extra professions
> will save the game - fix here before starting Block F.
> Ship with a `SaveData.version` bump + migration of old saves + an EditMode migration test.

---

# BLOCK F - Fishing
*Same template as Block E. Fish items exist; fishing-spot/rod/boat/shipyard art does not.*

### F1. FishingSpot & catching `[assets: stub]`
**Do:** `FishingSpot` interactable; hold E -> timer with chance -> fish in inventory; basic rod as a
tool `ItemSO`. Water + spot are stubs (a plane + primitive); fish items are ready.
**Check:** fish at the water; sometimes a fish drops.

### F2. Fish types & rarity `[assets: ready]`
**Do:** `FishSO` (chance, price, min rod). Map to the Fish Pack models (Fish1-3/Shark/Dolphin/...).
**Check:** different fish appear; rare ones cost more.

### F3. Spot depletion & recovery `[assets: ready]`
**Do:** spot capacity; chance drops after N catches; recovers over a day/week.
**Check:** active fishing depletes the spot; fish return after recovery.

### F4. Smoking & drying `[assets: stub]`
**Do:** `SmokingStation` turns raw fish into a separate fixed-price `SmokedFishSO` on a timer. Station
is a stub.
**Check:** load fish -> wait -> smoked fish at a higher price.

### F5. Aquariums as goods `[assets: backlog]`
**Do:** `AquariumStallItem` - live fish as premium decor; needs a "tank" from crafting. Tank art ->
backlog.
**Check:** an aquarium on the stall sells rarely but expensively.

### F6. Shipyard & boats `[assets: backlog]`
**Do:** `Shipyard` consumes boards + metal -> a sellable boat. Boat/shipyard art -> backlog.
**Check:** build a boat -> sell for a large sum.

### F7. Ferry crossing `[assets: backlog]`
**Do:** a running ferry gives passive income and brings "other-district" NPCs (rare supplier stock).
Ferry art -> backlog.
**Check:** ferry running -> daily income; new supplier items appear.

> **Checkpoint F:** one extra specialization fully playable.
> Ship with a `SaveData.version` bump + migration of old saves + an EditMode migration test.

---

# BLOCK G - Animal husbandry
*Animal + coop models exist (no chicken model). Pets partly exist (Pug; no cat).*

### G1. Chickens & eggs `[assets: stub]`
**Do:** `ChickenCoop` (model ready) collects eggs on a timer; feed bought from supplier; egg item
ready. Chicken creature is a stub (no chicken model) - coop can abstract the birds for now.
**Check:** feed -> collect eggs -> sell.

### G2. Cows, pigs, sheep `[assets: ready]`
**Do:** each animal (Cow/Pig/Sheep models ready) yields its resource (milk/meat/wool) on its own cycle.
**Check:** all three branches produce distinct raw materials.

### G3. Pets & "market happiness" (visible) `[assets: stub]`
**Do:** `PetSO` (Pug ready as dog; cat -> backlog; Pig as mini-pig). A pet is a *visible* object that
unlocks NPC lines, cosmetic reactions, themed orders/achievements - **no invisible traffic buff**.
**Check:** add a dog -> NPCs occasionally react; a themed order/achievement opens.

### G4. Horses - rental & delivery `[assets: ready]`
**Do:** `Horse` (model ready) can be rented (passive income) or sent to fetch supplier goods (faster,
risky).
**Check:** send the horse -> after a timer it returns with goods or empty.

### G5. Horse racing (mini-game) `[assets: ready]`
**Do:** a simple once-a-season event (pick a winner or ride), using the Horse model.
**Check:** race runs; the bet pays out or burns.

> **Checkpoint G:** animal branch fully playable.
> Ship with a `SaveData.version` bump + migration of old saves + an EditMode migration test.

---

# BLOCK H - Crafting stations
*Crafted results exist as Food-Kit models; dedicated station buildings do not (use Barn stubs).
Kitchen moved to E12 (farm layer 2). **Scope rule: better 4 professions at layer 4 than 8 at layer 1.**
Core here = Bakery + Brewery (both feed on farm goods -> cross-profession glue, and both map to
existing Food-Kit art). Smithy/Apothecary/Tailor are demoted to CUT CANDIDATES: keep the design
stubs, implement only if post-E playtests beg for more breadth - and only after every shipped
profession has its layer 3-4 done.*

### H1. CraftingStation base `[assets: stub]`
**Do:** `ICraftingStation` - takes a recipe + ingredients from inventory -> timer -> result. Station
is a stub building (Barn/SmallBarn) until art exists.
**Check:** craft a simple item from a recipe.

### H2. Bakery `[assets: ready]`
**Do:** `Bakery` consumes grain + eggs -> bread/pie/buns. Results map to Food-Kit
bread/loaf/baguette/pie models. (Building stubbed; products ready.)
**Check:** bake bread -> sell at a margin (it's a separate `ItemSO`).

### H3. Brewery `[assets: stub]`
**Do:** `Brewery` - grain + honey -> beer/mead; long timer, premium price. Drink props stubbed
(bottle/barrel from Food Kit).
**Check:** start a brew -> product after a cycle.

### H4. Smithy - tools for other branches `[assets: backlog]` **[CUT CANDIDATE]**
**Do:** `Smithy` makes improved rods (F), boards (shipyard), garden tools (E). Tool models -> backlog;
stub with primitives.
**Check:** craft a rod -> fisher unlocks new fish.

### H5. Apothecary `[assets: backlog]` **[CUT CANDIDATE]**
**Do:** `Apothecary` - herbs + animal products -> potions/cosmetics (premium). Potion/herb art ->
backlog.
**Check:** brew a potion -> sell expensively.

### H6. Tailor `[assets: backlog]` **[CUT CANDIDATE]**
**Do:** `Tailor` - wool + leather -> clothing for seasonal orders/new buyer types (fixed price, no
hidden bonus). Clothing art -> backlog.
**Check:** make winter clothing -> turn it into a winter order or sell as a fixed-price item.

### H7. Recipes as finds `[assets: ready]`
**Do:** `RecipeBookSO`; recipes aren't given upfront - bought from traveling traders or dropped as
rewards. (Data/UI only.)
**Check:** unlock a recipe -> it appears in the station's craft menu.

### H8. Unique recipe & rare item `[assets: ready]`
**Do:** rare recipes yield a unique high fixed-price item with its own orders - no temporary x2
monopoly. (Reuses existing Food-Kit models.)
**Check:** unlock a unique recipe -> make a new item that shows up in orders/achievements.

> **Checkpoint H:** all four specializations playable and linked by materials.
> Ship with a `SaveData.version` bump + migration of old saves + an EditMode migration test.

---

# BLOCK I - Social systems & town
*Relationships, contracts, consequences - still no hidden price/demand changes. Mostly data/UI/logic.*

### I1. NPC dialogue & relationships `[assets: ready]`
**Do:** `NPCRelationSystem` - key NPCs have a relationship that opens lines, orders, rewards. No
hidden price effect.
**Check:** complete the baker's orders a few times -> a new line/order/recipe opens.

### I2. Contracts - supply AND sales `[assets: ready]`
**Do:** two directions, one system.
*Supply* (as before): long-term supplier contracts reserve a fixed quantity at a fixed price/prepay.
*Sales* (new, the star): recurring heavy demand in the Journal (D8) gets PERSONIFIED - the innkeeper
walks up in person: "I need cheese every week - set up supply and we'll talk." Fixed weekly
quantity + fixed price vs. selling at the stall = a real portfolio decision (stability vs. margin).
The Journal of the early game *becomes* the contract system of the late game - one mechanic growing
with the player. Breaking an accepted contract lowers rep (D9); declining an offer costs nothing.
**Check:** cheese asked 15+x over 2 weeks -> innkeeper spawns a contract dialogue; accept -> weekly
pickup + pay; miss a week -> rep hit + trust cooldown; decline -> no penalty, offer may return later.

### I3. Town licenses `[assets: ready]`
**Do:** `LicenseSystem` - permits to sell categories / build objects / run stations; bought or
unlocked via rating/orders.
**Check:** no fish license -> no fish stall; buy it -> allowed.

### I4. Reputation in action `[assets: ready]`
**Do:** `ReputationSystem` gates rare recipes, supplier trust, contracts, dialogue, order limits.
Never changes prices directly.
**Check:** high rep -> more trust/content; low rep -> refusals and closed branches.

> **Checkpoint I:** the player builds relationships with the town, not just sells goods.
> Ship with a `SaveData.version` bump + migration of old saves + an EditMode migration test.

---

# BLOCK J - Co-op balance & experience
*Netcode itself lives in Block N and has been load-bearing since before the specializations. This
block makes co-op GOOD: solo balance is the base table; co-op is a modifier layer on top of it,
never a second table. Design & balance target: 2 players; architecture allows up to 4; playtest 1
and 2 as equal first-class modes.*

### J1. Demand & cost scaling `[assets: ready]`
**Do:** visitor flow and per-item demand capacity x~1.6-1.7 for the 2nd player (sub-linear ON
PURPOSE: two players produce 2x, so the per-item ceiling must arrive *earlier* per head - pushing
the pair into different professions instead of double tomatoes). Big build costs (greenhouse,
second stall, story milestones) x~1.4-1.5. All scaled numbers remain visible in the journal -
no hidden co-op coefficients.
**Check:** a duo hits the tomato ceiling a bit earlier by calendar than solo; journal shows the
scaled capacity openly; duo doesn't finish story content in half the solo time.

### J2. Role split / WorkerNPC parity `[assets: ready]`
**Do:** `WorkerNPC` (a `StaffSO` skin, D12) covers a specialization at basic tier for a wage;
endgame (greenhouse mastery / Prize quality / unique recipes) stays human-only. In co-op, natural
role split (stall / farm / bakery) - professions are each other's suppliers (bakery on partner's
flour beats bought flour), so cooperation pays through the existing economy, zero new mechanics.
**Check:** solo hires a fisher worker -> basic fish supply, Prize fish stays human; duo splitting
professions visibly out-earns duo doubling one profession.

### J3. Hire-point verification `[assets: ready]`
**Do:** playtest instrument for the D12 pacing anchor: solo first hire wanted ~day 40, duo ~day
55-60 - later but MANDATORY (friend != free worker forever; scaled demand must outgrow four hands).
**Check:** telemetry/playtests show duos hiring before endgame; if not -> J1 scaling up.

### J4. Journal as coordination board `[assets: ready]`
**Do:** journal entries can be "claimed" with a player color mark - partner sees and doesn't
duplicate. One flag on existing UI; the journal becomes the duo's planner.
**Check:** P1 claims cheese -> P2 sees the mark; Evening Summary planning screen shows both claims.

### J5. Goal scaling & pair sync `[assets: ready]`
**Do:** final target and rating thresholds scale with human player count (uses J1 multipliers).
Story milestones require BOTH players' professions (the Fair needs produce AND bread) so a
lagging partner is always needed, never a passenger; N4 asymmetric-session rule protects the
absent player's progress lane.
**Check:** solo target < duo target; a milestone blocks until both professions contribute; the
lagging player has a visible, wanted role in it.

> **Checkpoint J:** co-op for 2 feels designed, not tolerated; 4 connects and works. First co-op
> hour is the make-or-break playtest: if the duo doesn't hit a "we're a team" moment (first shared
> spike, first split-role day) in hour one, fix HERE before polish.

---

# BLOCK K - Polish & release
*When it all works - finish and ship. Most art-replacement here waits on the artist.*

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
**Do:** 8-10 hint chain: buy -> place -> sell -> take a board order -> complete it -> unlock something.
Steps in `TutorialStepSO`.
**Check:** a new player gets through the first ~20-30 min without docs.

### K4. Balance pass `[assets: ready]`
**Do:** prices/timers/wages table, 5-10 playthroughs, tune. Starting anchors (from design work -
expect them to bend in playtests, but keep the LOGIC): day ~ 13 real min; season = 14 days; crop
metric = profit/day/plot with "convenience costs margin"; item demand ceiling ~25-30/wk at start,
grows with rep; income arc ~40-60/day (phase 1) -> 150-250 (2) -> 500+ minus wages 15-25/worker (3) ->
thousands vs. 10-30k story builds (4) - the player never sits on a big pile with no visible next
purchase 1-2 sessions away. Manual chores share of the day: ~70% -> 45% -> 25% -> 10% across phases,
each freed minute refilled by a higher-level decision THE SAME DAY. Co-op modifiers per J1.
**Check:** playtests reach the goal without exploits or boredom; no phase has "empty minutes";
mono-farmer feels the first ceiling ~mid-season-2 (not earlier, not later).

### K5. Performance pass `[assets: ready]`
**Do:** NPC pooling, LOD, crowd profiling, GPU instancing for repeated meshes. Target stable 60 FPS.
**Check:** profiler shows < 16 ms/frame at peak.

#### K5a. Island render hot-path guardrails `[assets: ready]`
**Do:** remove the ocean opaque-color copy and double-sided pass; bound camera/Terrain/shadow cost;
serialize the optimized defaults in the builder; add read-only Project Health regression checks.
**Check:** Island uses instanced Terrain with one-sided shadows, depth-only front-face water, no
global URP opaque texture, and Unity compile/health plus focused EditMode tests pass.

#### K5b. Ambient occlusion pass (URP SSAO) `[assets: ready]`
**Context:** the SSAO renderer feature is already active on `PC_Renderer.asset` (Blue Noise,
Intensity 0.4, Radius 0.5, Samples Low, Bilateral blur, Source Depth) but its values were never
settled or recorded, and `Mobile_Renderer.asset` has no AO at all.
**Do:** finish the AO decision instead of leaving it half-tuned. Settle the PC values against the
stylized look (contact shadow under stall crates, crop plots, building bases - no dark halos around
NPCs against the sky, no self-shadow acne on Food-Kit props); pick Source Depth vs DepthNormals
deliberately (Depth avoids the DepthNormals prepass but reconstructs normals - check crop plot
edges); either add SSAO to `Mobile_Renderer` at Downsample+Low or record why mobile ships without
it; serialize the chosen values and note them in `CHANGELOG.md`.
**Decision (recorded here so it isn't re-litigated):** no third-party AO asset (Amplify Occlusion
et al.). URP 17.5 SSAO covers a stylized market/farm scene; URP has no GTAO, but the extra fidelity
a third-party pass buys is invisible on low-detail cartoon meshes. Revisit only if a profiler
capture shows built-in SSAO is the bottleneck *and* the look demonstrably suffers.
**Check:** AO visibly grounds props in the market and on the Island; no halos/acne at the K5a
guardrail settings; frame cost measured and stated (target <= ~0.5 ms at 1080p); `verify-unity.ps1`
health `ok`.

### K6. UX pass `[assets: ready]`
**Do:** review all screens - consistent style/fonts/contrast, minimal localization (RU/EN via
`LocalizationSO`).
**Check:** a "new player" walks every screen without questions.

### K7. Build pipeline & release `[assets: ready]`
**Do:** Build & Run for target platforms, icons, splash, store page, SemVer. (Versioning already in
place: `VERSION` + `CHANGELOG.md` + `vX.Y.Z` tags.)
**Check:** the build downloads, installs, runs.

### K8. Playtest & patch cycle `[assets: ready]`
**Do:** 3-5 external testers -> bug reports -> fixes -> one minor post-release patch.
**Check:** critical bugs fixed; patch shipped.

> **Checkpoint K:** the game is released.

---

## Project tooling

### T1. Walkable asset museum `[assets: ready]`
**Do:** Build a standalone development scene that presents imported art packs in labeled thematic
zones, keeps source assets unchanged, and supports direct first-person inspection.
**Check:** the scene opens directly, contains every supported model once, and runs with no console
errors.

### T2. MCP player agent `[assets: ready]`
**Do:** Add an MCP tool that drives the live first-person controller through collision-aware movement,
look, jump, and interaction commands, then returns a Game View PNG plus compact player telemetry.
**Check:** an unfocused Unity Editor can capture the HUD, execute movement and rotation in Play Mode,
and report the changed player pose with no console errors.

---

## Key risks (keep in mind)

- **Don't run ahead through blocks.** Resist starting fishing before the market loop is fun - it
  leads to piles of disconnected systems. Hold the order.
- **Don't bring back hidden coefficients.** Want a "lively market"? Add visible content first: new
  orders, suppliers, NPCs, seasonal availability, lines and goals. No droughts, demand x1.5, rumor x2.
- **UI (Block C) outranks deep economy.** While shop/inventory/stall live in Debug.Log, deep systems
  are untestable. Stabilize C first.
- **First-person crowds.** Wide aisles (3-4 m), `NavMeshObstacle` on the player (A7), NPCs yield -
  or the player gets stuck in the crowd.
- **Scale.** Reference capsule 1.8 m; measure everything against it from A1. First person is merciless
  about bad scale.
- **Saving gets complex fast.** Built in A10; bump `SaveData.Version` with each big block and migrate.
  Don't stack five blocks without migration.
- **Build after every block.** Run Build & Run at each block checkpoint; build-only bugs (missing
  shaders, AOT) accumulate silently.
- **Co-op cannot be bolted on at the end (REVISED).** "Netcode later" on top of five finished
  specializations means rewriting five specializations - the classic indie graveyard. Block N sits
  between D and E precisely so the one-time retrofit covers only the small finished core, and
  everything after is born server-authoritative. Corollary: co-op is +40-60% dev complexity overall
  (sync, reconnects, double-mode testing) - budget for it, don't discover it.
- **The layer-3 seam is where players quit.** The moment the farm stops needing the player's hands
  (staff hired) MUST coincide with the next tactile thing opening (second profession layer 1, big
  build). A 3-4 empty-day gap between "farmhand hired" and "something new to do with my hands" is a
  churn point - playtest this seam specifically, solo and duo.
- **The journal must stay a menu, not a debt list.** Enforce the 70/25/5 achievability weights
  (D8j); a journal full of unreachable wants recreates Old Market's anxiety without the penalty.
  And never let it push notifications - it waits to be opened.
- **Art is gated.** New models/animations won't arrive soon. Build logic against stubs (`[assets:
  stub]`) and keep `[assets: backlog]` steps behind flags rather than blocking on art.

---

## Progress

### Block 0 - Foundation
- [x] 0.1 Folder architecture & namespaces
- [x] 0.2 Asset filtering
- [x] 0.3 Scenes & Bootstrap
- [x] 0.4 ServiceLocator + EventBus
- [x] 0.5 Main menu
- [x] 0.6 First build

### Block A - Playable skeleton
- [x] A1 Walking
- [x] A2 Look & prompt
- [x] A3 Money & HUD *(->N0-c)*
- [x] A4 Items & inventory *(->N0-c)*
- [x] A5 Supplier (debug)
- [x] A6 Stall (debug)
- [x] A7 NavMesh + obstacle
- [x] A8 First NPC & sale
- [x] A9 Visitor flow *(->N0-e)*
- [x] A10 Saving *(->N0-f)*

### Block B - Stable market, no hidden coefficients
- [x] B1 Game time *(->N0-a)*
- [x] B2 Daylight
- [x] B3 Traffic by hour *(->N0-e)*
- [x] B4 Seasons *(14d cfg)*
- [x] B5 Single price point
- [x] B6 Simplify pricing
- [x] B7 Auto-debug
- [x] B8 NPC purchase rules
- [x] B9 Multi-stall prep
- [x] B10 Seasonal assortment

### Block C - UX & player-facing surface
- [x] C1 Cursor/UI-mode service
- [x] C2 InventoryUI
- [x] C3 ShopUI (supplier)
- [x] C4 StallUI *(->N0-d)*
- [x] C5 PauseMenu *(->N0-b)*
- [x] C6 Settings menu
- [ ] C7 AudioMixer & base audio *(deferred -> K2)*
- [x] C8 NPC animated model *(visual variety deferred until more assets exist)*
- [x] C9 Interaction prompt & cursor polish

### Block D - Day rhythm, orders, progression & attraction
- [x] D0 MarketStallRegistry
- [x] D1 DayPhaseSystem *(->N0-a)*
- [x] D2 Open/Close stall
- [x] D3 Evening Summary
- [ ] D4 Daily Goals v1
- [x] D5 Sleep / Next Day *(->N0-a)*
- [ ] D6 NPC personalities
- [ ] D7 Dialogue bubble & haggling
- [ ] D8 Orders + Demand Journal (D8a-D8k)
- [ ] D9 ReputationSystem
- [ ] D10 MarketRating + UnlockSystem
- [ ] D11 Physical market props (D11a-D11e)
- [ ] D12 Staff / automation (D12a-D12g)
- [ ] D13 Visible customer attraction (D13a-D13e)
- [ ] D14 Decorations v1
- [ ] D15 Rent / Loans (optional)

### Block N - Network foundation (moved up)
- [ ] N0 Retrofit audit of v3 systems (a time/day-end; b pause; c money/inventory; d stall lock; e traffic multipliers; f saves; g debugger)
- [ ] N1 Netcode bootstrap
- [ ] N2 Second player
- [ ] N3 Shared authoritative state (shared wallet)
- [ ] N4 Asymmetric sessions
- [ ] N5 Personal attribution

### Block E - Farm
- [x] E1 CropPlot + CropSO *(state machine will grow: E4/E10/N4)*
- [x] E2 Visual stages
- [ ] E3 Seasonality
- [ ] E4 Quality (stars, care)
- [ ] E5 Cost of production & crop roles
- [ ] E6 Greenhouse
- [ ] E7 Beehive
- [ ] E8 Flowers & bouquets
- [ ] E9 Farm tutorial
- [ ] E10 Soil fatigue
- [ ] E11 Tool ladder -> drip
- [ ] E12 Kitchen & preserving
- [ ] E13 Farmhands & manager

### Block F - Fishing
- [ ] F1 FishingSpot
- [ ] F2 Fish types
- [ ] F3 Depletion/recovery
- [ ] F4 Smoking/drying
- [ ] F5 Aquariums
- [ ] F6 Shipyard & boats
- [ ] F7 Ferry

### Block G - Animal husbandry
- [ ] G1 Chickens & eggs
- [ ] G2 Cows/pigs/sheep
- [ ] G3 Pets
- [ ] G4 Horses
- [ ] G5 Racing

### Block H - Crafting & kitchen
- [ ] H1 CraftingStation base
- [ ] H2 Bakery
- [ ] H3 Brewery
- [ ] H4 Smithy (cut?)
- [ ] H5 Apothecary (cut?)
- [ ] H6 Tailor (cut?)
- [ ] H7 Recipes as finds
- [ ] H8 Unique recipe

### Block I - Social systems & town
- [ ] I1 NPC dialogue & relationships
- [ ] I2 Contracts (supply + sales)
- [ ] I3 Town licenses
- [ ] I4 Reputation in action

### Block J - Co-op balance & experience
- [ ] J1 Demand & cost scaling
- [ ] J2 Role split / WorkerNPC parity
- [ ] J3 Hire-point check
- [ ] J4 Journal as coordination board
- [ ] J5 Goal scaling & pair sync

### Block K - Polish & release
- [ ] K1 Replace placeholders
- [ ] K2 Sound design
- [ ] K3 Tutorial
- [ ] K4 Balance
- [ ] K5 Performance
- [x] K5a Island render hot-path guardrails
- [ ] K5b Ambient occlusion (URP SSAO) *(no third-party AO asset - decided)*
- [ ] K6 UX pass
- [ ] K7 Build pipeline
- [ ] K8 Playtest & patch

### Project tooling
- [x] T1 Walkable asset museum
- [x] T2 MCP player agent
- [x] T3 Read-only project health scanner
- [x] T4 Local verification gate with compact MCP output
- [x] T5 Selected-model asset pipeline assistant
- [x] T6 Compact asset pipeline MCP output
- [x] T7 Stylized water prototype scene and legacy water cleanup
- [x] T8 Replace Island ocean with switchable Bitgem water
- [x] T9 Apply full Bitgem environment preset to Island
- [x] T10 Realistic water R5 local reflection and refraction integration
- [x] T11 Realistic water R6 temporal whitecaps and shoreline foam
- [x] T12 Realistic water R7 world-space caustic projection
- [x] T13 Realistic water R8 underwater surface and volume transition
- [x] T14 Realistic water R9 quality tiers, optimization, and promotion gate
- [x] T15 Realistic water optical-density material tuning
- [x] T16 WaterShaderLab complete shader-feature showcase pass
- [x] T17 Asset-based realistic caustic lookup and depth tuning
- [x] T18 Weather-driven realistic water modes from calm to storm
- [x] T19 Photon-traced caustic flipbook matching reference beach photography
- [x] T20 GrassLab layered meadow visual showcase pass
- [x] T21 Grass interaction bend and presentation polish
- [x] T22 Wave profile assets and procedural wave editor
- [x] T23 In-world water settings wall with crosshair interaction
