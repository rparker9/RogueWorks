// File: Assets/RogueWorks.Unity/Presentation/GameViewOrchestrator.cs
// Namespace: RogueWorks.Unity.Presentation
//
// Purpose:
// - Convert AnimationRequest -> IClip via AnimationRouter.
// - Schedule clips on AnimationSequencer: movement queue vs action queue vs immediate (blocking).
// - Optionally push label-only requests to the log sink without playing a visual.
//
// Notes:
// - Preserves your existing behavior (blocking uses RunImmediate, non-blocking enqueues).
// - Adds safe early-out for DebugLabel-only requests and null-clip guard.

using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

using RogueWorks.Core.Animation;
using RogueWorks.Core.Primitives;

using RogueWorks.Unity.Animation;
using RogueWorks.Unity.Animation.Sinks;


namespace RogueWorks.Unity.Presentation
{
    /// <summary>
    /// Orchestrates Unity-side playback for semantic <see cref="AnimationRequest"/> batches.
    /// Uses <see cref="AnimationRouter"/> to build clip graphs and <see cref="AnimationSequencer"/> to play them.
    /// </summary>
    public sealed class GameViewOrchestrator
    {
        /// <summary>
        /// True while a blocking segment is being played immediately.
        /// </summary>
        public bool IsBlocking { get; private set; }

        private readonly System.Action _onBlockStart;
        private readonly AnimationSequencer _sequencer;
        private readonly AnimationRouter _router;
        private readonly ILogSink _log; // keep a direct reference for label-only requests

        /// <summary>
        /// Construct a new orchestrator.
        /// </summary>
        public GameViewOrchestrator(
            PresentationProfile profile,
            AnimationSequencer sequencer,
            IVfxSink vfx,
            ISfxSink sfx,
            ILogSink log,
            ICameraShakeSink shake,
            System.Func<GridPos, float, Vector3> toWorld,
            IActorViewLookup viewLookup,
            System.Action onBlockStart = null)
        {
            _sequencer = sequencer;
            _onBlockStart = onBlockStart;
            _log = log;

            _router = new AnimationRouter(
                profile,
                vfx, sfx, log, shake,
                toWorld,
                viewLookup);
        }

        /// <summary>
        /// Build and play a single request according to its type and blocking flags.
        /// </summary>
        public IEnumerator Handle(AnimationRequest rq)
        {
            // 1) Label-only request: show text and exit fast (non-blocking).
            if (!string.IsNullOrEmpty(rq.LogTextLabel) && rq.Type == AnimationType.None)
            {
                _log?.Write(rq.LogTextLabel);
                yield break;
            }

            // 2) Build clip (router might return null if assets missing or unsupported type).
            var clip = _router.Build(rq);
            if (clip == null)
            {
                // Optional: still surface the label for easier debugging
                if (!string.IsNullOrEmpty(rq.LogTextLabel))
                    _log?.Write($"[NoClip] {rq.LogTextLabel}");
                
                yield break;
            }

            // 3) Schedule/play based on type & blocking.
            if (rq.IsBlocking)
            {
                // Blocking: gate immediately; caller will usually set IsBlockingView before batch.
                SetBlocking(true);
                yield return _sequencer.RunImmediate(clip);
                
                SetBlocking(false);
                yield break;
            }

            // Non-blocking:
            if (rq.Type == AnimationType.Movement)
            {
                // Movement: enqueue on movement lane (non-blocking).
                _sequencer.EnqueueMovement(clip);
            }
            else
            {
                // Action (skill, impact, etc): enqueue on action lane (non-blocking).
                _sequencer.EnqueueAction(clip);
            }
        }

        /// <summary>
        /// Set the blocking state and invoke the start callback if entering blocking.
        /// </summary>
        /// <param name="v"></param>
        private void SetBlocking(bool v)
        {
            IsBlocking = v;
            if (v) 
                _onBlockStart?.Invoke();
        }
    }
}
