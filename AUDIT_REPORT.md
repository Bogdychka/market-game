# Project Audit Report — 2026-07-05

Read-only audit: future-proofing (Block N / saves), performance, asset import settings, and
agent-docs correctness. No changes made. Effort: S < 1h, M = half-day, L = day+.
"Auto-fix safe" = no gameplay/design impact expected; save-format changes always need a
version bump + migration test per the standing policy.

---

## Critical

### C1. Contracts point to the wrong dev plan
- **Where:** `CLAUDE.md:9,41,56` · `AGENTS.md:6,19,190,287` — all say `dev_plan_3.md`; the live plan
  is `dev_plan_4_1.md` (v4, co-op-aware, Block N). Both files coexist at repo root.
- **Problem:** the standing instruction "tick dev_plan_3.md after a step / grep it by step id"
  sends every future session to the retired plan.
- **Consequence:** progress forks between two plans; v4-only content (N0 retrofit list) is invisible
  to an agent that obeys the contract literally.
- **Fix:** update all 7 references to `dev_plan_4_1.md`; move `dev_plan_3.md` to `_ArchiveAssets/docs/`
  (or delete — git history keeps it).
- **Effort:** S · **Auto-fix safe:** yes (docs only).

### C2. Planted crops are not saved (E1 not wired into persistence)
- **Where:** `GameSaver.cs:174-188` (`CollectSaveData` — no crops) · `SaveData.cs` (no crop DTO) ·
  `CropPlot.cs:32-33` (`_planted`, `_plantedAtMinutes` runtime-only).
- **Problem:** plant a seed -> save (F5 or autosave on exit) -> load: the plot is empty and the seed
  is consumed. Known bug, deferred; cost grows with every E-block step (E2 visuals, E4 quality,
  E10 soil state all hang off plot state).
- **Consequence:** player loses resources on every save/load; each new E feature deepens the
  unsaved-state hole; the N0-f "one unbroken migration chain" gets harder the later this lands.
- **Fix:** `CropPlotData { plotId, cropId, plantedAtMinutes }` in `SaveData`, plot registry or
  saver-serialized list, Collect/Apply pair, `SaveData.version = 5`, `SaveMigrationTests` case.
- **Effort:** M · **Auto-fix safe:** yes (additive save field; policy-compliant bump required).

---

## High

### H1. Save file is written in place — one crash corrupts the only save
- **Where:** `SaveSystem.cs:28-29` (`File.WriteAllText(SavePath, json)`), single `save.json`, no backup.
- **Problem:** a crash/power loss mid-write leaves a truncated JSON; `Load()` then returns null forever.
- **Consequence:** total progress loss from a single bad write; severity grows as saves accumulate value.
- **Fix:** write to `save.json.tmp`, then `File.Replace` (atomic) keeping `save.json.bak`; on load,
  fall back to `.bak` if parse fails.
- **Effort:** S · **Auto-fix safe:** yes.

### H2. NPC save identity = ScriptableObject asset name
- **Where:** `NPCSpawner.cs:133` (`npcTypeKey = visitor.Type.name`), `NPCSpawner.cs:414-426`
  (`FindType` by `name`/`TypeName`).
- **Problem:** renaming an `NPCTypeSO` asset silently orphans saved visitors (restore skipped with a
  warning). Items already solved this class of bug via `ItemDatabase.Resolve(id, name)`; NPC types
  have no stable id.
- **Consequence:** every future NPC personality (D6) multiplies the rename-fragile surface; breaks
  the "preserve old-save compatibility" rule invisibly.
- **Fix:** add `[SerializeField] private string id` to `NPCTypeSO` (same pattern as `ItemSO`),
  save id, resolve id-first name-fallback.
- **Effort:** S · **Auto-fix safe:** yes (fallback keeps old saves loading).

### H3. AGENTS.md still says netcode is "Block J only"
- **Where:** `AGENTS.md:57` (tech-stack table: "Netcode for GameObjects — Block J only").
- **Problem:** plan v4 moved network foundation to Block N, before specializations E-I.
- **Consequence:** an agent trusting AGENTS.md will write E-I systems against local-only state —
  exactly what Block N's "nothing after this line may be written against local-only state" forbids.
- **Fix:** change to "Block N (before specializations); see dev_plan_4_1.md".
- **Effort:** S · **Auto-fix safe:** yes.

### H4. 259 static-prop FBX import rigs and animation they never use
- **Where (meta audit):** kenney_food-kit 200/200, Textured Stylized Trees 45/45, Farm Buildings
  13/13, blender/wood_box 1/1 — all `importAnimation: 1`, `animationType: 2 (Generic)`.
  (Animated packs — Quaternius animals/fish, UAL, Mixamo — are correctly rigged; not affected.)
- **Problem:** every static prop imports an Avatar + Animator into its model prefab.
- **Consequence:** slower imports/builds, Animator components silently instantiated with item
  visuals (`StallSlot.Place` spawns `WorldPrefab`), wasted memory per placed item; adds up as
  E-blocks spawn many more props.
- **Fix:** one editor pass (or `AssetPostprocessor` preset per folder): Rig -> None,
  Import Animation off for the four folders above.
- **Effort:** M (touches 259 metas; mechanical) · **Auto-fix safe:** yes — verify nothing
  references clips from these files first (audit found none).

---

## Medium

### M1. Cartoon_Farm_Crops materials are built-in Standard shader (magenta under URP)
- **Where:** all 14 `Assets/Cartoon_Farm_Crops/Materials/*.mat` (`m_Shader: fileID 46,
  guid 0000...f000` = built-in Standard); 2 FBX also have `isReadable: 1`.
- **Consequence:** the moment E2 (visual growth stages) uses these prefabs, every crop renders
  magenta; readable meshes double their memory.
- **Fix:** batch-convert materials to URP/Lit (`_BaseColor`/`_BaseMap` per AGENTS.md), turn off
  Read/Write on the 2 FBX.
- **Effort:** S · **Auto-fix safe:** yes.

### M2. EventBus: one throwing handler starves all later subscribers
- **Where:** `EventBus.cs:37-49` — the whole multicast delegate is invoked inside a single try/catch.
- **Consequence:** a single buggy UI handler for `ItemSoldEvent` silently prevents
  `DailySummarySystem` (or any later subscriber) from seeing the sale; data corruption with no error
  at the failure site. Gets worse as D8/D9 add subscribers.
- **Fix:** iterate `handler.GetInvocationList()` and try/catch per handler.
- **Effort:** S · **Auto-fix safe:** yes.

### M3. NPCSpawner disable/enable cycle strands the visitor pool and the counter
- **Where:** `NPCSpawner.cs:362-378` — `OnDisable` unsubscribes and clears `_spawnedVisitors` but
  does not release visitors or reset `_activeCount`; `OnEnable` never recounts.
- **Consequence:** after a disable/enable cycle, `_activeCount` stays inflated (spawner permanently
  under-spawns) and live visitors bypass the pool (`OnDespawned` has no subscriber ->
  `Destroy(gameObject)` in `NPCVisitor.cs:398`).
- **Fix:** in `OnDisable`, release tracked visitors to the pool and zero `_activeCount`
  (reuse `ClearActiveVisitors`).
- **Effort:** S · **Auto-fix safe:** yes.

### M4. Interaction prompt goes stale while the target's text changes
- **Where:** `InteractionPromptUI.cs:59-84` — label refreshes only on `CurrentChanged` /
  controls-changed; `CropPlot.PromptText` (`CropPlot.cs:154-175`) changes with growth state.
- **Consequence:** stare at a growing plot and the prompt still says "Carrot growing" after it is
  Ready (harvest hint never appears). Any future timed interactable (smoker, oven) inherits the bug.
- **Fix:** small refresh timer (~0.25 s) while a target is set, or a dirty-flag on `IInteractable`.
- **Effort:** S · **Auto-fix safe:** yes.

### M5. Money is a float
- **Where:** `MoneySystem.cs:15` (`private float _amount`), `SaveData.money` float, prices float
  throughout.
- **Consequence:** cumulative drift over long sessions (`+= price` thousands of times); N3's shared
  host-authoritative wallet then syncs imprecise floats. Cheap to fix now, painful after D8 orders
  and N3 land on top.
- **Fix:** integer coins (long) end-to-end, or keep floats but round on every mutation; save
  migration for the field.
- **Effort:** M · **Auto-fix safe:** NO — touches save format, UI formatting, and price math;
  needs a deliberate step.

### M6. No physics layers: everything collides with everything, raycasts scan everything
- **Where:** `TagManager.asset` (no custom layers), `DynamicsManager.asset:20` (matrix all-on),
  `InteractionSystem.cs:20` (`layerMask = ~0`).
- **Consequence:** NPC capsules push each other and the player redundantly (NavMesh already
  avoids), interaction ray tests every collider; cost scales with NPC count (N0-e raises it) and
  E-I prop density.
- **Fix:** add `Player / NPC / Interactable / StaticProp` layers, disable NPC-NPC and NPC-StaticProp
  pairs in the matrix, narrow the interaction mask to `Interactable`.
- **Effort:** M (code S + scene/prefab pass) · **Auto-fix safe:** mostly — needs one Play-mode
  regression of NPC flow + interaction.

### M7. Plan self-description and progress are stale
- **Where:** `dev_plan_4_1.md:9` claims "36 KB ~= 10k tokens" — the file is 61 KB (~15k tokens);
  `AGENTS.md:19` repeats the stale size. Progress: `[ ] D3 Evening Summary` is unticked while the
  code is fully present (`DailySummarySystem.cs`, `EveningSummaryPanelRenderer.cs`, GameSaver/UI
  wiring, CHANGELOG entry).
- **Consequence:** token-discipline budgeting is wrong; a future session may re-implement D3.
- **Fix:** correct the size note in both files; verify D3 in Play mode once, then tick the checkbox.
- **Effort:** S · **Auto-fix safe:** docs yes; the D3 tick requires the Play-mode check first.

### M8. "Pause on menus" is a dead claim — TimeSystem.Pause has zero callers
- **Where:** `TimeSystem.cs:47-49` (`Pause/Resume/IsPaused` — no callers anywhere);
  `dev_plan_4_1.md:175` (B1 says "pause on menus"); the real pause is `Time.timeScale = 0` in
  `PauseMenuController.cs:86` (N0-b's known retrofit target). The clock also ticks while the
  MainMenu is open (`GameBootstrap.Update` -> `TickTime`), masked only by New-Game `Reset()` /
  Continue overwrite.
- **Consequence:** doc describes behavior the code does not have; dead API invites misuse.
- **Fix:** N0-a already plans to kill the rule — remove the dead `Pause/Resume` API there; until
  then correct the B1 line to "timeScale pause in PauseMenu only".
- **Effort:** S · **Auto-fix safe:** docs yes; API removal belongs to N0-a.

### M9. UI item icons: 2048px cap, no SpriteAtlas
- **Where:** icons come from `kenney_food-kit/Previews/*.png` (`maxTextureSize: 2048`,
  `spriteMode: 1`, no atlas anywhere in the project).
- **Consequence:** each icon is its own texture -> a full inventory/supplier list breaks batching
  (one draw call per distinct icon); 2048 cap wastes memory for ~64 px UI icons.
- **Fix:** `maxTextureSize: 256` on the Previews folder + one `SpriteAtlas` over used icons.
- **Effort:** S · **Auto-fix safe:** yes.

---

## Low

### L1. Per-browse closure allocation in NPC purchase check
- **Where:** `NPCVisitor.cs:290` — `Array.Exists(_preferredCategories, c => c == category)` captures
  `category` (new closure per candidate slot per browse).
- **Fix:** manual for-loop. **Effort:** S · safe.

### L2. CropPlot scales its visual every frame, even when fully grown
- **Where:** `CropPlot.cs:47-52` + `188-205` — `localScale` write per frame while planted.
- **Fix:** refresh on hour-change event (growth is time-derived) or early-out at progress >= 1.
- **Effort:** S · safe.

### L3. Mixamo_animations pack is unused
- **Where:** `Assets/Mixamo_animations/` — 4 clips, zero references from `_Project` (C8 uses UAL's
  own clips); `AGENTS.md` asset table still lists it as "NPC animations".
- **Fix:** move to `_ArchiveAssets/` per convention 0.2; update the AGENTS.md row.
- **Effort:** S · safe.

### L4. Stray folders at Assets root
- **Where:** `Assets/blender/wood_box.fbx` (personal WIP folder); `Assets/Standard Assets/` —
  ToonShading *textures only* (44 KB, no shader — likely a partial import).
- **Fix:** move wood_box under an art folder; verify ToonShading is unreferenced and archive it.
- **Effort:** S · safe after reference check.

### L5. DailySummary state is not saved
- **Where:** `DailySummarySystem` accumulators are runtime-only; save mid-day -> load ->
  evening report under-counts that day.
- **Fix:** fold into the C2 save-format step (or accept and document as design).
- **Effort:** S-M · additive.

### L6. FileLogger flushes synchronously on every log line
- **Where:** `FileLogger.cs:29` (`AutoFlush = true`); dev-only (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`).
- **Fix:** flush on interval or on error-severity only. **Effort:** S · safe.

### L7. Uncapped frame rate in builds
- **Where:** `QualitySettings.asset` `vSyncCount: 0` in all tiers; no `Application.targetFrameRate`.
- **Consequence:** GPU spins at max FPS in builds (heat/noise). Belongs to K5, noted here so it
  isn't lost. **Effort:** S · safe.

### L8. AGENTS.md architecture/debug lists lag the code by one block
- **Where:** `AGENTS.md` subsystem map lacks `DayPhaseSystem`, `MarketOpenSystem`,
  `MarketStallRegistry`, `DailySummarySystem`, `CropPlot/CropSO` (Farm); Debug tooling section
  lacks the D2/D5 debug cubes.
- **Fix:** one-line additions. **Effort:** S · safe.

---

## Netcode cross-check (N0-a..g vs what the audit found)

The N0 list correctly covers: time/pause (a,b — confirmed: `Time.timeScale` in PauseMenu, clock
ticked locally), money/inventory (c — confirmed the only two mutation points are
`NPCVisitor.cs:295` and `SupplierShop.cs:108`, both already event-publishing), stall slots (d),
traffic multipliers (e), saves (f), debugger (g).

**Gaps the N0 list misses — propose adding:**
1. **N0-d scope:** slot locking covers stalls only. `SupplierShop` stock decrement and
   `CropPlot.Interact` (plant/harvest) are the same race when two players interact simultaneously —
   the lock/authority pattern should name *all shared interactables*, not just stall slots.
2. **EventBus locality:** `ItemSoldEvent`/`MarketOpenChangedEvent` are process-local. N3 implies
   host->client replication of gameplay-relevant events; worth one explicit line so E-I systems
   don't subscribe to events that will only fire host-side.
3. **Save-order dependency:** C2 (crop persistence) must land *before* N0-f freezes the migration
   chain, otherwise crops become the first system whose save format is born inside the netcode
   retrofit.
4. **GameBootstrap.HandleEscape** returns to MainMenu by loading a scene — in co-op this unloads
   the shared world for the local player mid-session; N0-b covers the pause half, not the
   scene-exit half.

**Guardrail proposal for the contracts (docs, last commit):** add one rule to `AGENTS.md`
Persistence: "Any new system that owns state surviving a day boundary MUST ship its
Collect/Apply pair + SaveData field + migration test in the same step." That single rule would
have caught C2, L5, and future E-block gaps.

---

## Proposed fix order (one concern per commit)

| # | Item | Why this position |
|---|------|--------------------|
| 1 | H1 atomic save write | Protects every later save-format change; zero design impact |
| 2 | C2 crop persistence (+v5, migration test) | Blocks E2+; must precede N0-f |
| 3 | H2 NPCTypeSO stable id | Same commit family as save-format work |
| 4 | M2 EventBus per-handler isolation | Cheap, de-risks all D8/D9 subscriber growth |
| 5 | M3 spawner pool/counter on disable | Cheap correctness |
| 6 | M4 prompt refresh | Player-visible E1 bug |
| 7 | M1 crop materials -> URP (+readable off) | Unblocks E2 visuals |
| 8 | H4 static FBX import pass | Mechanical asset hygiene |
| 9 | M9 icon sizes + atlas | UI perf |
| 10 | M6 physics layers | Needs one Play-mode regression |
| 11 | L1, L2, L6 micro-perf | Batchable as one perf commit |
| 12 | L3, L4 archive pass | Asset moves |
| 13 | C1, H3, M7, M8, L8 + guardrail rule | All md updates, last, one commit |

Deferred by design: M5 (money as int) — schedule as its own step near N0-c; L7 — K5.

**Stopping here per instructions. Nothing was modified. Awaiting approval of specific items.**
