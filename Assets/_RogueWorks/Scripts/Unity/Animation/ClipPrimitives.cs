using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RogueWorks.Unity.Animation
{
    /// <summary>
    /// Async unit of presentation (VFX, SFX, UI, camera, tweens, etc.).
    /// </summary>
    public interface IClip
    {
        /// <summary>
        /// Play the clip and yield until complete.
        /// </summary>
        IEnumerator Play(MonoBehaviour runner);
    }

    /// <summary>
    /// Run child clips strictly in order.
    /// </summary>
    public sealed class SequenceGroup : IClip
    {
        private readonly List<IClip> _children = new();
        
        /// <summary>
        /// Add a child to the end of the sequence.
        /// </summary>
        public SequenceGroup Add(IClip clip) { _children.Add(clip); return this; }

        /// <inheritdoc/>
        public IEnumerator Play(MonoBehaviour runner)
        {
            foreach (var c in _children) yield return runner.StartCoroutine(c.Play(runner));
        }
    }

    /// <summary>
    /// Run child clips concurrently, then wait for all.
    /// </summary>
    public sealed class ParallelGroup : IClip
    {
        private readonly List<IClip> _children = new();
        
        /// <summary>
        /// Add a child to run in parallel.
        /// </summary>
        public ParallelGroup Add(IClip clip) { _children.Add(clip); return this; }
        
        /// <inheritdoc/>
        public IEnumerator Play(MonoBehaviour runner)
        {
            var running = new List<Coroutine>(_children.Count);
            foreach (var c in _children) running.Add(runner.StartCoroutine(c.Play(runner)));
            foreach (var co in running) yield return co;
        }
    }

    /// <summary>
    /// Waits for a fixed duration.
    /// </summary>
    public sealed class DelayClip : IClip
    {
        private readonly float _seconds;
        /// <summary>
        /// Create a delay clip.
        /// </summary>
        
        public DelayClip(float seconds) => _seconds = seconds;
        
        /// <inheritdoc/>
        public IEnumerator Play(MonoBehaviour runner) { yield return new WaitForSeconds(_seconds); }
    }

    /// <summary>
    /// Turns an IEnumerator factory into a clip.
    /// </summary>
    public sealed class CoroutineClip : IClip
    {
        private readonly Func<MonoBehaviour, IEnumerator> _factory;
        
        /// <summary>
        /// Create a coroutine-backed clip.
        /// </summary>
        public CoroutineClip(Func<MonoBehaviour, IEnumerator> factory) => _factory = factory;
        
        /// <inheritdoc/>
        public IEnumerator Play(MonoBehaviour runner) => _factory(runner);
    }
}
