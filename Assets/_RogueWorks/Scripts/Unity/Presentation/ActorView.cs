using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RogueWorks.Unity.Presentation
{
    /// <summary>
    /// Unity-side visual for a Core actor. Provides smooth continuous motion by queuing waypoints.
    /// Uses a constant-speed path follower with smoothed speed (accelerate/decelerate) at start/stop.
    /// </summary>
    public sealed class ActorView : MonoBehaviour
    {
        [Tooltip("World-unique id from Core.World.AddActor(...)")]
        public int ActorId;
        [SerializeField] private Animator animator;

        [Header("Movement")]
        [Tooltip("Linear cruise speed in world units per second (e.g., tiles/sec if tile size = 1).")]
        [SerializeField, Min(0.01f)] private float moveUnitsPerSecond = 5f;

        [Tooltip("Distance considered 'arrived' at a waypoint.")]
        [SerializeField, Min(0.0001f)] private float arriveEpsilon = 0.001f;

        [Header("Speed Damping (Start/Stop)")]
        [Tooltip("Seconds to smoothly accelerate from 0 to cruise speed.")]
        [SerializeField, Min(0f)] private float accelTime = 0.08f;

        [Tooltip("Seconds to smoothly decelerate from cruise to 0 when no waypoints remain.")]
        [SerializeField, Min(0f)] private float decelTime = 0.10f;

        [Header("Rotation")]
        [SerializeField] private float rotateSeconds = 0.1f;

        /// <summary>True while a waypoint is being processed or queued.</summary>
        public bool IsMoving => _mover != null || _queue.Count > 0 || _currentSpeed > 0.0001f;

        public bool HasPath => _queue.Count > 0;

        // --- Internal mover state ---
        private readonly Queue<Waypoint> _queue = new();
        private Coroutine _mover;

        private struct Waypoint
        {
            public Vector3 Pos;
            public Action OnArrive;
        }

        // Speed smoothing state
        private float _currentSpeed;     // current linear speed (units/sec)
        private float _speedVel;         // smoothdamp velocity for speed
        private bool _stopRequested;    // hard stop flag set by ClearQueuedMovement(true)

        /// <summary>
        /// Queue a move to a world position. Returns when THIS waypoint is reached,
        /// while the mover keeps flowing through subsequent waypoints without stopping.
        /// </summary>
        public IEnumerator MoveTo(Vector3 worldPos)
        {
            bool done = false;
            _queue.Enqueue(new Waypoint { Pos = worldPos, OnArrive = () => done = true });
            if (_mover == null) _mover = StartCoroutine(Mover());

            // Wait only for THIS waypoint to complete.
            while (!done) yield return null;
        }

        /// <summary>Rotate to a yaw angle in degrees (Y axis) over a short tween.</summary>
        public IEnumerator RotateToYaw(float targetYawDeg)
        {
            Quaternion start = transform.rotation;
            Quaternion end = Quaternion.Euler(0f, targetYawDeg, 0f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, rotateSeconds);
                transform.rotation = Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            transform.rotation = end;
        }

        public IEnumerator PlayAttack() { if (animator) animator.SetTrigger("Attack"); yield return new WaitForSeconds(0.06f); }
        public IEnumerator PlayHit() { if (animator) animator.SetTrigger("Hit"); yield return new WaitForSeconds(0.05f); }
        public IEnumerator PlayDeath() { if (animator) animator.SetTrigger("Death"); yield return new WaitForSeconds(0.12f); }

        /// <summary>
        /// Clear pending waypoints. If stopImmediately is true, request a decel-to-stop from current position.
        /// </summary>
        public void ClearQueuedMovement(bool stopImmediately = false)
        {
            _queue.Clear();
            if (stopImmediately && _mover != null)
                _stopRequested = true; // decelerate to 0, then exit
        }

        /// <summary>
        /// Path follower: consume segments using this frame's speed budget; exit when queue empty AND speed ~= 0.
        /// </summary>
        private IEnumerator Mover()
        {
            _stopRequested = false;

            // Tiny constants to avoid div-by-zero and micro-jitter
            const float kMinDist = 0.000001f;
            float eps2 = arriveEpsilon * arriveEpsilon;

            while (true)
            {
                // Determine desired (target) speed for this frame.
                bool hasPath = _queue.Count > 0;
                float targetSpeed = (_stopRequested || !hasPath) ? 0f : moveUnitsPerSecond;

                // Choose smoothing time based on accelerating vs decelerating.
                float smoothT = (targetSpeed > _currentSpeed) ? accelTime : decelTime;

                // Smooth currentSpeed toward targetSpeed (critically damped feel).
                _currentSpeed = (smoothT <= 0f)
                    ? targetSpeed
                    : Mathf.SmoothDamp(_currentSpeed, targetSpeed, ref _speedVel, smoothT, Mathf.Infinity, Time.deltaTime);

                // Distance budget this frame
                float remaining = _currentSpeed * Time.deltaTime;

                // Current position we will write back at the end of the frame
                Vector3 pos = transform.position;

                // Consume path as far as the budget allows (possibly multiple waypoints)
                while (remaining > 0f)
                {
                    if (_queue.Count == 0)
                        break;

                    var wp = _queue.Peek();  // snapshot head
                    Vector3 delta = wp.Pos - pos;
                    float dist = delta.magnitude;

                    // If we are effectively at this waypoint: snap, notify, pop, continue.
                    if (dist * dist <= eps2)
                    {
                        pos = wp.Pos;
                        wp.OnArrive?.Invoke();
                        if (_queue.Count > 0) _queue.Dequeue();
                        continue;
                    }

                    // If we cannot reach it with the remaining budget: advance along segment and finish frame
                    if (dist > remaining)
                    {
                        pos += delta * (remaining / Mathf.Max(kMinDist, dist));
                        remaining = 0f;
                        break;
                    }

                    // We can reach it and still have leftover budget: arrive, consume the distance, and continue to next
                    pos = wp.Pos;
                    remaining -= dist;
                    wp.OnArrive?.Invoke();
                    if (_queue.Count > 0) _queue.Dequeue();
                }

                // Write back position
                transform.position = pos;

                // Exit conditions:
                // 1) No path and we've decelerated to (almost) zero speed.
                if (_queue.Count == 0 && _currentSpeed <= 0.0001f)
                {
                    _currentSpeed = 0f;
                    _speedVel = 0f;
                    _stopRequested = false;
                    _mover = null;
                    yield break;
                }

                yield return null;
            }
        }
    }
}
