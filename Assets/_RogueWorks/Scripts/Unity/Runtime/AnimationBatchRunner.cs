using System.Collections;
using System.Collections.Generic;
using RogueWorks.Core.Animation;
using RogueWorks.Unity.Presentation;
using UnityEngine;

namespace RogueWorks.Unity.Runtime
{
    /// <summary>
    /// Plays batches of <see cref="AnimationRequest"/> sequentially through the orchestrator.
    /// Pure POCO; uses an injected MonoBehaviour to run coroutines.
    /// </summary>
    public sealed class AnimationBatchRunner
    {
        private readonly MonoBehaviour _runner;           // coroutine host (GameRuntime)
        private readonly GameViewOrchestrator _orch;      // request -> clip graph
        private Coroutine _inflight;                      // active batch handle

        /// <summary>Create a batch runner bound to a runner and orchestrator.</summary>
        public AnimationBatchRunner(MonoBehaviour runner, GameViewOrchestrator orchestrator)
        {
            _runner = runner;
            _orch = orchestrator;
        }

        /// <summary>
        /// Starts playing a batch; cancels any in-flight batch to prevent overlap.
        /// </summary>
        public void Play(IReadOnlyList<AnimationRequest> batch)
        {
            if (_inflight != null) { _runner.StopCoroutine(_inflight); _inflight = null; }
            _inflight = _runner.StartCoroutine(DrainCo(batch));
        }

        /// <summary>
        /// True if a batch coroutine is currently running.
        /// </summary>
        public bool IsRunning => _inflight != null;

        /// <summary>
        /// Coroutine that drains the batch in-order, awaiting each request.
        /// </summary>
        private IEnumerator DrainCo(IReadOnlyList<AnimationRequest> batch)
        {
            for (int i = 0; i < batch.Count; i++)
                yield return _runner.StartCoroutine(_orch.Handle(batch[i]));
            _inflight = null;
        }
    }
}
