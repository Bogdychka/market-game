# Market Game — Project Context for Claude

---

## Tech Stack

| What | Version / Details |
|---|---|
| Unity | **6000.4.8f1** (Unity 6) |
| Render Pipeline | **URP 17.4.0**, **Deferred** rendering in `PC_Renderer.asset` |
| Input | **New Input System 1.19.0** — `activeInputHandler = 1`, legacy Input is **disabled** |
| NavMesh | **AI Navigation 2.0.12** — only via `NavMeshSurface` component, `Navigation Static` flag is **deprecated and removed** |
| UI | **uGUI + TextMeshPro** (built into Unity 6). UI Toolkit — Editor tools only |
| Persistence | `Application.persistentDataPath` + JSON |
| Networking | Netcode for GameObjects — Block J only, do not use earlier |

---

## Project Architecture

```
Assets/
├── _Project/              ← all game code and content
│   ├── Scripts/
│   │   ├── Core/          Market.Core        (ServiceLocator, EventBus, SceneLoader, TimeSystem)
│   │   ├── Player/        Market.Player      (FirstPersonController, HeadBob)
│   │   ├── Interaction/   Market.Interaction (InteractionSystem, IInteractable)
│   │   ├── Economy/       Market.Economy     (MoneySystem, Inventory, ItemSO, ItemDatabase)
│   │   ├── Market/        Market.Market      (MarketStall, StallSlot)
│   │   ├── NPC/           Market.NPC         (NPCVisitor, NPCSpawner, NPCTypeSO)
│   │   ├── World/         Market.World       (DaylightSystem, MoonVisualFactory)
│   │   ├── UI/            Market.UI          (HUD, MainMenu)
│   │   ├── Persistence/   Market.Persistence (SaveSystem, GameSaver, SaveData)
│   │   └── Debug/         Market.DebugTools  (debug-only — FileLogger, DebugTimeControl, ...)
│   ├── Data/              ScriptableObject instances (Items, Crops, NPCTypes, ItemDatabase...)
│   ├── Art/Prefabs/
│   └── Scenes/            Bootstrap, MainMenu, Market
├── ThirdParty/            third-party asset packs — do not modify structure
└── _ArchiveAssets/        outside Assets folder, not imported by Unity
```

### Core Services (registered in `GameBootstrap`, accessible via `ServiceLocator`)
- `EventBus` — type-safe game events (`IGameEvent` structs)
- `SceneLoader` — async scene loading
- `SaveSystem` — JSON save/load, has `ShouldLoadOnStart` flag for "Continue" button
- `TimeSystem` — игровые часы и дни, тикается из `GameBootstrap.Update()`
- `PriceCalculator` / price service — single read point for fixed `ItemSO` prices. No hidden gameplay modifiers, no drought multipliers, no demand coefficients. Supplier uses buy price; stall uses suggested sell price.

### Scene Object Coordinators (knows scene refs, calls services)
- `GameSaver` — saves money/inventory/stalls/player position; F5 to save; auto-loads if `SaveSystem.ShouldLoadOnStart`
- `DaylightSystem` — controls sun/moon rotation, ambient, skybox exposure based on `TimeSystem`
- `NPCSpawner` — spawns NPCs from spawn points, instantiates from `NPCTypeSO` prefabs
- Future `StaffSystem` — cashiers, stockers, cleaners. Automation is a progression reward after the player has learned the manual loop.
- Future attraction systems — visible signboards, display windows, discount ads for overstocked goods, promo days, decor. Do not implement invisible customer-attraction coefficients.

---

## Script Writing Rules

### Always do
- **Namespace** on every file: `namespace Market.<Subsystem>`
- **[SerializeField] private** instead of `public` for Inspector fields
- **Null-check** SerializeField dependencies in `Awake()` with `Debug.LogError`
- **[RequireComponent]** when a script depends on another component on the same object
- **Unsubscribe from events** in `OnDisable()` / `OnDestroy()` — every subscription must have a matching unsubscription
- **Readonly structs** for EventBus events — cheaper allocations
- **Separate data** (ScriptableObject) **from logic** (MonoBehaviour / plain class)
- Cache component references in `Awake()`, never in `Update()`
- **Fallback for direct scene Play** — services may be missing if user starts from `Market` scene without `Bootstrap`. Use `ServiceLocator.TryGet<T>()` and create local instance if needed.

### Never do
- `FindObjectOfType` — use `ServiceLocator.Get<T>()` or `[SerializeField]`
- `GameObject.Find` — always slow and fragile
- Static MonoBehaviour singletons — use `ServiceLocator` instead
- `public` fields — always `[SerializeField] private`
- Heavy logic in `Update()` without caching
- Legacy `Input.GetKey` — use `Keyboard.current[Key.X]` or `InputAction`
- `OnGUI` — use uGUI/TMP only

### Code Style
- PascalCase: classes, methods, properties, public events
- `_camelCase` for private fields (`_amount`, `_controller`)
- `OnEnable` / `OnDisable` — event subscription/unsubscription only
- Initialization in `Awake()`, scene wiring in `Start()`
- **Short methods, single responsibility.** Если метод >30 строк или делает 2+ вещей — разбивай на хелперы (см. `GameSaver` / `GameBootstrap` / `MainMenuController` как референс).
- **`[Header]` группировка** SerializeField'ов: References / Settings / Tuning / Debug.
- **`[Tooltip]` на любом неочевидном поле** — Inspector становится самодокументируемым.
- **XML doc-comments** на каждом публичном классе и нетривиальном методе.
- **`[RequireComponent]`** если компонент жёстко зависит от другого на том же GameObject.
- **`Validate`/`Resolve` методы**: вместо инлайн-чеков в Awake() — отдельный `ValidateReferences()` метод, который вызывается из Awake. Чище и легче читать.
- **try/catch вокруг I/O** (File.Read/Write, JSON parse) — никогда не позволяй исключению пробросить наверх, логируй и возвращай null/false.
- **null check после Destroy** — после `Destroy(_visual)` обнуляй ссылку (`_visual = null`), иначе остаётся «висячий» референс.
- **Не дублируй проверки**: если `MoneySystem.TrySpend` уже проверяет баланс, не пиши `if (CanAfford) TrySpend` — просто `if (!TrySpend) error`.
- **Mathf.Clamp01 — следи** что условия после него учитывают [0,1] диапазон. Был баг в HeadBob где `speedFactor > 1f` после Clamp01 → всегда false. Проверяй raw value до клампа.

---

## Unity 6 — Required Patterns

### Input (New Input System)
```csharp
// Correct:
Keyboard.current[Key.F1].wasPressedThisFrame
_moveAction.ReadValue<Vector2>()
_interactAction.started += OnInteract; // subscribe in OnEnable

// Wrong:
Input.GetKeyDown(KeyCode.F1) // legacy — disabled in this project
```

### NavMesh (AI Navigation 2.0.12)
```csharp
// Correct:
// Add NavMeshSurface component to the floor GameObject, click Bake on it
// NavMeshObstacle with Carving = true on the player
// For NPC: snap destination to NavMesh via NavMesh.SamplePosition() before SetDestination()

// Wrong:
// Navigation Static flag (removed in Unity 6)
// Window → AI → Navigation → Bake (old workflow)
```

### URP — gotchas learned
- **Deferred mode** is set in `PC_Renderer.asset` (`m_RenderingMode: 2`). Some effects render differently — emission and transparent objects work, but render queue tweaking is fragile.
- **URP/Lit emission** для бесконечно ярких объектов (солнце-луна-фонари): включай `_EMISSION` keyword + `_EmissionColor` > 1, ставь `globalIlluminationFlags = None`. Работает в Forward и Deferred.
- **URP/Unlit рендерит чёрное без `_BaseMap`** — текстура умножается на `_BaseColor`. Если используешь без текстуры, ставь `Texture2D.whiteTexture` явно.
- **Procedural Skybox** не темнеет ночью сам. Нужно:
  1. `RenderSettings.sun = sunLight` (привязать солнце)
  2. Создать runtime-инстанс `RenderSettings.skybox` (`new Material(...)`) — иначе модифицируется ассет.
  3. Менять `_Exposure` каждый кадр.
- **Ambient Mode** должен быть `AmbientMode.Flat` если хочешь контролировать `RenderSettings.ambientLight` из кода. Иначе skybox/трилайт перебивает.
- **Environment Lighting Source** в Lighting окне можно оставить `Skybox`, код всё равно переключит в `Flat` через `RenderSettings.ambientMode`.

### URP Materials (property names)
```csharp
// Correct (URP):
mat.SetColor("_BaseColor", color);
mat.SetTexture("_BaseMap", tex);

// Built-in fallback (если URP-шейдер не найден):
mat.SetColor("_Color", color);
mat.SetTexture("_MainTex", tex);

// Лучше всего — задавать оба, см. MoonVisualFactory.
```

### Coroutine vs async
- Coroutines — for Unity-dependent timers and `WaitForSeconds`
- `async/await` with `Awaitable` (Unity 6) — for I/O (file loading, network)

### Scene Loading
```csharp
// Correct — use SceneLoader service:
ServiceLocator.Get<SceneLoader>().Load(SceneNames.Market);

// Wrong — don't call directly:
SceneManager.LoadScene("Market");
```

### Service Pattern
- **Plain C# class** lifecycle services (TimeSystem, SaveSystem, EventBus, SceneLoader): registered in `GameBootstrap.InitializeServices()`. They live in `ServiceLocator` across scenes.
- **MonoBehaviour services** that need scene context (MoneySystem, Inventory): live on scene objects, accessed via `[SerializeField]`.
- **Scene coordinator** pattern: MonoBehaviour that knows scene refs (e.g., `GameSaver`) and calls into plain-C# services.

### Refactor patterns (применены после code review)
- **Collect/Apply split** для save/load систем: `CollectInventory(data)` собирает данные, `ApplyInventory(data)` накатывает. Не мешать в одном методе.
- **Helper методы для длинного Update()**: `TickTime()`, `HandleEscape()` вместо одного гигантского `Update`.
- **Wire/Refresh/Show методы** в UI контроллерах: `WireButtons()` подписывает onClick, `RefreshContinueAvailability()` обновляет состояние кнопки.
- **Validate/Resolve паттерн**: вместо if-проверок в Awake — отдельные `ValidateReferences()` и `ResolveSaveSystem()` методы.
- **Фабрики для сложного создания GameObject'ов** — см. `MoonVisualFactory`. Если конструирование объекта занимает 30+ строк (шейдеры, материалы, renderer settings) — выноси в статический helper class.
- **Состояния через `EnterX/UpdateX`** для машины состояний — см. `NPCVisitor`. Один dispatcher `EnterState(next)` + `UpdateX` методы в Update.

---

## ScriptableObject Contracts

| SO | Purpose |
|---|---|
| `ItemSO` | Product: **stable `Id`** (для сейвов, не переименовывать!), name, category, buy/sell price, icon, worldPrefab, `AvailableInSeasons` |
| `ItemDatabase` | Array of all `ItemSO`. `Resolve(id, name)` — по Id с фолбэком на имя (миграция старых сейвов) |
| `NPCTypeSO` | NPC type: budget, walk speed, browse time, category preferences, prefab |
| `CropSO` | Crop: growth time, seasons, yield, growth stage meshes (Block D) |
| `RecipeSO` | Crafting recipe: ingredients → result (Block H) |
| `OrderSO` / `WishSO` | Visible NPC/board request: item, count, deadline, fixed reward (Block D) |

### Save format (`SaveData`)
- **version 2**: добавлены `day/hour/minute` (время) и `itemId` в инвентарь/слоты прилавка.
- Резолв предметов через `ItemDatabase.Resolve(itemId, itemName)` — Id основной, имя фолбэк.
- Сезон **не сохраняется** — выводится из `day` через `SeasonManager.RefreshSeason()` при загрузке.

### Skybox
- Встроенный **Default-Skybox** (Skybox/Procedural). `DaylightSystem` крутит `_Exposure` по высоте солнца, `SeasonManager` — `_SkyTint` по сезону.
- (Кастомный SkyboxNight.shader был откатан — не зашёл визуально.)

---

## Debug Tooling

- **FileLogger** — пишет все `Debug.Log` в `<project root>/game.log`. Инициализируется в `GameBootstrap.Awake()`. Удобно для диагностики без открытого Editor (мышь захвачена в FPS-контроллере).
- **DebugTimeControl** — `PageUp`/`PageDown` для скорости времени, `H` для пропуска часа.
- **DebugSupplierBuy** — клавиши 1-5 покупают товар по индексу.
- **DebugStallPlace** — F3 кладёт первый предмет инвентаря в первый свободный слот прилавка.
- **DebugMoneyInput** — F1/F2 для +100/-100 денег.
- **MarketAutoDebugger** — F9 включает/выключает автотест, F10 делает один цикл: покупка у поставщика → выкладка на прилавок → форс-спавн NPC → snapshot в `game.log`.

Debug-скрипты — временные. Лежат в `_Project/Scripts/Debug/`, namespace `Market.DebugTools`. Удалять после внедрения нормального UI.

---

## How to Respond

1. **Unity API questions** — answer strictly for Unity 6 (6000.x). If uncertain, say so explicitly or use web search on `docs.unity3d.com/6000.0`.
2. **Writing scripts** — follow all rules above, place in correct `_Project/Scripts/<Subsystem>/` folder, include Editor setup instructions.
3. **Editor tasks** — provide step-by-step instructions (Create → Add Component → fields), since there is no direct access to the Unity Editor.
4. **Debug scripts** — go in `_Project/Scripts/Debug/`, namespace `Market.DebugTools`. They are temporary.
5. **One plan step per request.** Do not skip ahead.
6. **After completing each step** — update `✅` in `dev_plan_3.md`.
7. **Sensible defaults in code** instead of "tell user to set values in Inspector". If the user has to manually configure 5+ fields, the script should have working defaults out of the box.
8. **Use FileLogger output** to diagnose issues when user can't navigate Editor (mouse locked in Play). Path: `C:\Users\bogre\My project\game.log`.
9. **Don't skip the obvious bug source** — если что-то работает «не так», смотри сначала на сериализованные значения в `.unity` или `.prefab` файлах через Grep. Сам проект файлы — источник правды о текущем состоянии Inspector.
10. **Refactor as you go** — добавляя новую фичу, сразу применяй паттерны выше (Headers, Tooltips, XML doc, helper methods). Не оставлять «потом почищу».

---

## Current Project State

### Completed
- **0.1** Folder structure and namespaces
- **0.2** Asset filtering (_ArchiveAssets outside Assets)
- **0.3** Bootstrap/MainMenu/Market scenes + SceneLoader
- **0.4** ServiceLocator + EventBus
- **0.5** Main menu (MainMenuController)
- **0.6** First build — working
- **A1** FirstPersonController + HeadBob
- **A2** InteractionSystem + IInteractable + InteractionPromptUI
- **A3** MoneySystem + MoneyHUD
- **A4** ItemSO + ItemCategory + Inventory
- **A5** SupplierShop (Debug)
- **A6** MarketStall + StallSlot (Debug)
- **A7** NavMeshSurface on Ground, NavMeshObstacle on Player
- **A8** NPCVisitor (NavMeshAgent, state machine, first sale)
- **A9** NPCSpawner + NPCTypeSO
- **A10** SaveSystem (money, inventory, stall, player position) + ItemDatabase
- **B1** TimeSystem (game clock, day/night, OnHourChanged/OnDayChanged) + TimeHUD
- **B2** DaylightSystem (астрономическая формула высоты светила: широта 55° + solarDeclination, лунные фазы, skybox exposure, ambient по sunHeight)
- **B3** NPCSpawner: плотность трафика по часу (AnimationCurve), масштаб spawnInterval и maxNPC
- **B4** SeasonManager (4 сезона, solarDeclination/skyTint, фильтр ассортимента поставщика по сезону)
- **B5** PriceCalculator / price service as fixed-price read point. Project direction: no hidden gameplay coefficients, no drought multipliers, no demand modifiers.
- **B7** MarketAutoDebugger (F9 auto loop, F10 one cycle, snapshots in `game.log`)

### Last review pass (после B4)
Code review проекта — исправлено: stable `ItemSO.Id` для сейвов, save format v2 (время+id), null-safety (disable component при критичном null в `FirstPersonController`/`InteractionSystem`), `SceneLoader` guard от двойной загрузки, `FileLogger` за `UNITY_EDITOR || DEVELOPMENT_BUILD`, debug-сток прилавка за `#if UNITY_EDITOR`, синхронизированы дефолты сезонов код↔сцена.
**Отложено:** мульти-прилавок (архитектура `NPCSpawner.targetStall` / `GameSaver.marketStall` пока на один прилавок) — переделывать перед несколькими прилавками.

### Up Next
- **B6** Simplify existing pricing code: remove/freeze `IPriceModifier`, `PriceContext`, `ModifierCount`, `DescribeModifiers` from gameplay API.
- Then **Block C** UI: InventoryUI, ShopUI, StallUI, PauseMenu. Do not build world-event economy before UI.
- Later in `dev_plan_4.md`: staff automation and customer attraction are core progression pillars. Add them as visible world systems, not hidden percentage buffs.
- Discount ads are allowed when player explicitly chooses an item and a sale price for the day. They should attract relevant NPCs visibly and help clear overstock; they must not be a hidden global demand multiplier.

### Full Plan
See `dev_plan_4.md` — fun-first plan: hands-on market loop, NPC personalities, orders, visible progression, no hidden economy coefficients.

---

## Available Assets

| Pack | Path | Use |
|---|---|---|
| Kenney Food Kit | `Assets/kenney_food-kit/Models/FBX format/` | 3D item models (ItemSO.worldPrefab) |
| Quaternius Farm Animals | `Assets/Farm Animals Animated by Quaternius/FBX/` | Livestock (Block G) |
| Quaternius Farm Buildings | `Assets/Farm Buildings by Quaternius/FBX/` | Farm structures (Block E) |
| Quaternius Fish Pack | `Assets/Fish Pack Animated by Quaternius/FBX/` | Fishing (Block F) |
| Textured Stylized Trees | `Assets/Textured Stylized Trees - May 2020/.../FBX/` | Scene decoration |
| UAL Standard | `Assets/Universal Animation Library[Standard]/.../Unity/` | NPC rig |
| Mixamo Animations | `Assets/Mixamo_animations/` | Walk, Idle, Talking NPC animations |
