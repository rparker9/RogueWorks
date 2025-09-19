using System;
using UnityEngine;
using UnityEngine.VFX;

// Core
using RogueWorks.Core.Animation;
using RogueWorks.Core.Loop;
using RogueWorks.Core.Model;
using RogueWorks.Core.Primitives;

// Unity
using RogueWorks.Unity.Animation;
using RogueWorks.Unity.Animation.Sinks;
using RogueWorks.Unity.Presentation;

namespace RogueWorks.Unity.Runtime
{
    /// <summary>
    /// All dependencies required to construct <see cref="SceneBootstrap"/> in one place.
    /// </summary>
    public sealed class SceneBootstrapOptions
    {
        /// <summary>
        /// Coroutine host (usually the GameRuntime MonoBehaviour).
        /// </summary>
        public MonoBehaviour Runner { get; set; }

        /// <summary>
        /// World width in tiles.
        /// </summary>
        public int GridWidth { get; set; }

        /// <summary>World height in tiles.</summary>
        public int GridHeight { get; set; }

        /// <summary>Grid->World mapper (apply Y offset inside the func).</summary>
        public Func<GridPos, float, Vector3> ToWorld { get; set; }

        /// <summary>Visual effects sink.</summary>
        public IVfxSink Vfx { get; set; }

        /// <summary>Audio one-shot sink.</summary>
        public ISfxSink Sfx { get; set; }

        /// <summary>Log sink (UI/game log).</summary>
        public ILogSink Log { get; set; }

        /// <summary>Camera shake sink.</summary>
        public ICameraShakeSink Shake { get; set; }

        /// <summary>Actor view lookup/registry.</summary>
        public IActorViewLookup ViewLookup { get; set; }

        /// <summary>Presentation profile (timings, offsets, etc).</summary>
        public PresentationProfile PresentationProfile { get; set; }

        /// <summary>Callback when a blocking segment starts (used to gate input/turns).</summary>
        public Action OnBlockStart { get; set; }

        /// <summary>Fail-fast guard so miswiring is obvious in-editor.</summary>
        public void Validate()
        {
            if (Runner == null) throw new ArgumentNullException(nameof(Runner));
            if (GridWidth <= 0) throw new ArgumentOutOfRangeException(nameof(GridWidth));
            if (GridHeight <= 0) throw new ArgumentOutOfRangeException(nameof(GridHeight));
            if (ToWorld == null) throw new ArgumentNullException(nameof(ToWorld));
            if (Vfx == null) throw new ArgumentNullException(nameof(Vfx));
            if (Sfx == null) throw new ArgumentNullException(nameof(Sfx));
            if (Log == null) throw new ArgumentNullException(nameof(Log));
            if (Shake == null) throw new ArgumentNullException(nameof(Shake));
            if (ViewLookup == null) throw new ArgumentNullException(nameof(ViewLookup));
            if (PresentationProfile == null) throw new ArgumentNullException(nameof(PresentationProfile));
            if (OnBlockStart == null) throw new ArgumentNullException(nameof(OnBlockStart));
        }
    }

    /// <summary>
    /// Composes Core (World/Energy/Engine) and Presentation (Sequencer/Orchestrator).
    /// </summary>
    public sealed class SceneBootstrap
    {
        /// <summary>The simulation world grid.</summary>
        public World World { get; }
        /// <summary>Actor energy/initiative clock.</summary>
        public EnergyClock Clock { get; }
        /// <summary>Turn engine that resolves intents and emits <see cref="AnimationRequest"/>.</summary>
        public TurnEngine Engine { get; }
        /// <summary>Coroutine-based clip sequencer.</summary>
        public AnimationSequencer Sequencer { get; }
        /// <summary>Router/dispatcher from semantic requests to clip graphs.</summary>
        public GameViewOrchestrator Orchestrator { get; }

        /// <summary>Create Core and Presentation using a validated options object.</summary>
        public SceneBootstrap(SceneBootstrapOptions opt)
        {
            opt.Validate();

            // Core (engine-agnostic)
            World = new World(opt.GridWidth, opt.GridHeight);
            Clock = new EnergyClock();
            Engine = new TurnEngine();

            // Presentation (Unity-specific)
            Sequencer = new AnimationSequencer(opt.Runner);
            Orchestrator = new GameViewOrchestrator(
                profile: opt.PresentationProfile,
                sequencer: Sequencer,
                vfx: opt.Vfx,
                sfx: opt.Sfx,
                log: opt.Log,
                shake: opt.Shake,
                toWorld: opt.ToWorld,
                viewLookup: opt.ViewLookup,
                onBlockStart: opt.OnBlockStart
            );
        }
    }
}
