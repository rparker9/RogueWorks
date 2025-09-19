// File: Assets/RogueWorks.Unity/Animation/AnimationSequencer.cs
// Purpose: POCO queue runner. Needs a MonoBehaviour runner supplied by GameRuntime.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RogueWorks.Unity.Animation
{
    public sealed class AnimationSequencer
    {
        private readonly Queue<IClip> _movement = new();
        private readonly Queue<IClip> _actions = new();
        private bool _runningMovement, _runningActions;
        private readonly MonoBehaviour _runner;

        public AnimationSequencer(MonoBehaviour runner) => _runner = runner;

        public void EnqueueMovement(IClip clip)
        {
            _movement.Enqueue(clip);
            if (!_runningMovement) _runner.StartCoroutine(RunMovement());
        }

        /// <summary>Drop all queued movement clips. The clip currently playing (if any) is not interrupted.</summary>
        public void ClearQueuedMovement()
        {
            _movement.Clear();
        }


        public void EnqueueAction(IClip clip)
        {
            _actions.Enqueue(clip);
            if (!_runningActions) _runner.StartCoroutine(RunActions());
        }

        public IEnumerator RunImmediate(IClip clip) => clip.Play(_runner);

        private IEnumerator RunMovement()
        {
            _runningMovement = true;
            while (_movement.Count > 0) yield return _runner.StartCoroutine(_movement.Dequeue().Play(_runner));
            _runningMovement = false;
        }

        private IEnumerator RunActions()
        {
            _runningActions = true;
            while (_actions.Count > 0) yield return _runner.StartCoroutine(_actions.Dequeue().Play(_runner));
            _runningActions = false;
        }
    }
}
