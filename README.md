# RogueWorks

# RogueWorks – Project Index (Latest Snapshot)

## 0) Key Entry Points & Runtime Flow

* **Unity/Runtime/SceneBootstrap.cs** → Builds services, wires databases, constructs world, spawns views.
* **Unity/Runtime/GameRuntime.cs** → Main update loop; hands intents to Core; triggers animation batches.
* **Unity/Runtime/Input/** → User input adapters/controllers that produce **Core intents**.
* **Core/Loop/TurnEngine.cs** + **Core/Loop/EnergyClock.cs** → Turn/energy system (who can act when).
* **Unity/Animation/AnimationSequencer.cs** + **Unity/Animation/AnimationRouter.cs** → Turn Core animation requests into VFX/SFX/camera/log clips and play them.

---

## 1) Core (Engine‑agnostic)

### 1.1 Primitives

* **Core/Primitives/GridPos.cs** – Integer grid coordinates, helpers.

### 1.2 Model

* **Core/Model/Actor.cs** – Actor data (position, energy, skills, etc.).
* **Core/Model/World.cs** – Holds actors/tiles; resolve actions into outcomes.

### 1.3 Actions

* **Core/Actions/IAction.cs** – Interface for Core actions.
* **Core/Actions/ActionIntent.cs** – Player/AI intent payload (move, attack, etc.).
* **Core/Actions/ActionOutcome.cs** – Result of an action (hits, moves, logs, animation reqs).

### 1.4 Controllers

* **Core/Controllers/IActorController.cs** – Contract for anything that yields intents (player/AI).

### 1.5 Skills

* **Core/Skills/Skill.cs** – Core skill logic.

### 1.6 Animation (Core side)

* **Core/Animation/AnimationRequest.cs** – Semantic requests (impact, projectile, etc.).
* **Core/Animation/TargetRef.cs** – Refers to grid positions/actors.
* **Core/Animation/VfxCue.cs** – Symbolic cue names/types for VFX.

### 1.7 Loop

* **Core/Loop/EnergyClock.cs** – Actor energy accrual/spend.
* **Core/Loop/TurnEngine.cs** – Orders actors, resolves intents to outcomes; emits animation requests.

---

## 2) Unity Layer (Presentation, IO, Services)

### 2.1 Data (ScriptableObjects / DBs)

* **Unity/Data/ActorDefinition.cs** – Authoring actor prefab/data.
* **Unity/Data/RosterDefinition.cs** – Starting roster / spawn list.
* **Unity/Data/SkillDefinition.cs** – Unity descriptions for Core skills.
* **Unity/Data/PlayerInputConfig.cs** – Bindings/config for input.
* **Unity/Data/SfxDatabase.cs** – Maps cue names → `AudioClip`.
* **Unity/Data/VfxDatabase.cs** – Maps cue names → `VisualEffectAsset`.

### 2.2 Presentation

* **Unity/Presentation/ActorView\.cs** – Smooth movement between waypoints; plays anim hooks.
* **Unity/Presentation/ActorViewRegistry.cs** – Maps Core ActorId ↔ Unity view.
* **Unity/Presentation/GameViewOrchestrator.cs** – Facade the runtime uses to trigger presentation.
* **Unity/Presentation/PresentationProfile.cs** – Tunables for presentation behavior.

### 2.3 Animation (Unity)

* **Unity/Animation/ClipPrimitives.cs** – Low‑level clip structs/coroutines.
* **Unity/Animation/AnimationRouter.cs** – Builds clip graphs from Core `AnimationRequest`.
* **Unity/Animation/AnimationSequencer.cs** – Queues/plays clips; honors blocking/parallel flags.
* **Unity/Animation/Services/**

  * **CameraShakeService.cs** – Camera impulse helpers.
  * **LogService.cs** – UI/log sink.
  * **SfxOneShotService.cs** – Play one‑shot SFX.
  * **VfxService.cs** – Spawn/stop VFX Graphs.

### 2.4 Runtime (Glue/Boot/Loop)

* **Unity/Runtime/SceneBootstrap.cs** – Build world/services; validate assets; DI‑like wiring.
* **Unity/Runtime/GameRuntime.cs** – Orchestrates Core tick → outcomes → Animation batch.
* **Unity/Runtime/AnimationBatchRunner.cs** – Batching bridge between Core outcomes and sequencer.
* **Unity/Runtime/ActorSpawner.cs** – Instantiates `ActorView` prefabs at grid/world positions.
* **Unity/Runtime/Input/**

  * **InputSystemHandler.cs** – Raw Unity Input System bindings.
  * **PlayerInputAdapter.cs** – Implements `IActorController`; exposes intents (move/attack/skill).
  * **MovementController.cs** – Repeat/cadence and gating for movement.
  * **SkillController.cs** – Skill wheel/menu hold logic and gating.

### 2.5 UI

* **Unity/UI/SkillMenuController.cs** – Skill wheel/menu open/close & selection.
* **Unity/UI/SkillItemView\.cs** – Visual for one skill option.

---

## 3) Who Talks to Whom (Quick Reference)

* `PlayerInputAdapter` → yields `ActionIntent` (via `IActorController`) → `GameRuntime` / `TurnEngine`.
* `TurnEngine` → computes `ActionOutcome` → emits `AnimationRequest`.
* `AnimationBatchRunner`/`GameViewOrchestrator` → `AnimationRouter` → `AnimationSequencer` → Services (VFX/SFX/Log/Camera).
* `ActorViewRegistry` → maps `ActorId` ↔ `ActorView` for positioning and hit animations.

---

## 4) Hotspots / Frequent Bug Origins

* **Input gating vs. UI hold:** `SkillController` and `PlayerInputAdapter` must not gate the very action that closes the menu.
* **Database keys:** `VfxDatabase`/`SfxDatabase` cue name mismatches cause null lookups at runtime.
* **Sequencer blocking:** `AnimationSequencer` must release blocking requests even if a view is missing (defensive timeout).
* **ActorView movement:** Continuous speed + damping without pausing at waypoints; ensure final settle logic doesn’t stall.
* **Registry:** Missing ActorId registration yields “No view for actor” warnings; ensure spawn order assigns IDs.

---

## 5) Quick‑Find Tokens (copy/paste into IDE search)

* Entry loop: `class GameRuntime`, `TickTurn`, `ApplyOutcomes`
* Intents: `struct ActionIntent`, `IActorController.GetIntent`
* Outcomes: `struct ActionOutcome`
* Anim: `AnimationRequestType`, `AnimationRouter.Build*`, `AnimationSequencer.Enqueue`
* Input: `InputSystemHandler`, `PlayerInputAdapter`, `MovementController`, `SkillController`
* DBs: `VfxDatabase`, `SfxDatabase` (look for dictionary/lookup)
* Views: `ActorViewRegistry`, `ActorView.EnqueueWaypoint`

---

## 6) Triage Checklist (use during bug reports)

1. What **intent** was expected vs. produced? (log from `PlayerInputAdapter`)
2. Did `TurnEngine` accept the intent and emit an **outcome**?
3. Did `AnimationBatchRunner` convert outcomes into **AnimationRequests**?
4. Did `AnimationRouter` resolve **VFX/SFX** assets successfully?
5. Did `AnimationSequencer` mark **blocking** requests as completed?
6. Did `ActorView` receive **waypoints** and keep moving without stalls?
7. Any **null/lookup** warnings from services/registries?

> Keep this index updated when you upload a new snapshot; I’ll revise this map accordingly.
