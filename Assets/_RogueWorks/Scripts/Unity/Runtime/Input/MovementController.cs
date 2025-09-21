using RogueWorks.Core.Actions;
using RogueWorks.Core.Primitives;
using RogueWorks.Unity.Data;
using UnityEngine;

namespace RogueWorks.Unity.Runtime.Input
{
    /// <summary>
    /// Handles movement and rotation input with repeat timing and diagonal equalization.
    /// </summary>
    public class MovementController
    {
        private readonly PlayerInputConfig _config;

        // Repeat timing state
        private float _stepCooldown;
        private bool _hadFirstStepThisHold;
        private GridPos _queuedDir;
        private bool _hasQueuedDir;

        // Facing tracking
        private GridPos _lastFacing = GridPos.Right;

        // Buffer for continuous intents
        private ActionIntent? _bufferContinuous;

        public bool HasBufferedIntent => _bufferContinuous.HasValue || _hasQueuedDir;
        public GridPos LastFacing => _lastFacing;

        public MovementController(PlayerInputConfig config)
        {
            _config = config;
        }

        public void UpdateTiming(float deltaTime)
        {
            if (_stepCooldown > 0f)
                _stepCooldown = Mathf.Max(0f, _stepCooldown - deltaTime);
        }

        public void ResetState()
        {
            _bufferContinuous = null;
            _stepCooldown = 0f;
            _hadFirstStepThisHold = false;
            _hasQueuedDir = false;
            _queuedDir = GridPos.Zero;
        }

        public ActionIntent? GetContinuousIntent()
        {
            if (_bufferContinuous.HasValue)
            {
                var intent = _bufferContinuous.Value;
                _bufferContinuous = null;
                return intent;
            }

            if (_hasQueuedDir && _stepCooldown <= 0f)
            {
                TryFireContinuous(IntentType.Move, _queuedDir);
                if (_bufferContinuous.HasValue)
                {
                    var intent = _bufferContinuous.Value;
                    _bufferContinuous = null;
                    return intent;
                }
            }

            return null;
        }

        public void TryMove(Vector2 moveVector, bool isBlocked, bool isSuppressed)
        {
            if (isSuppressed || isBlocked) return;

            GridPos dir = VectorToGrid8(moveVector, _config.deadZone);
            bool held = IsNonZero(moveVector, _config.deadZone) && !dir.Equals(GridPos.Zero);

            if (!held)
            {
                ResetState();
                return;
            }

            TryFireContinuous(IntentType.Move, dir);
        }

        public void TryRotate(Vector2 moveVector, bool isBlocked, bool isSuppressed)
        {
            if (isSuppressed || isBlocked) return;

            GridPos dir = VectorToGrid8(moveVector, _config.deadZone);
            TryFireContinuous(IntentType.Rotate, dir);
        }

        public void SetLastFacing(GridPos facing)
        {
            _lastFacing = facing;
        }

        public void ClearBuffers()
        {
            ResetState();
        }

        private void TryFireContinuous(IntentType type, GridPos dir)
        {
            if (_stepCooldown > 0f)
            {
                _queuedDir = dir;
                _hasQueuedDir = true;
                return;
            }

            float nextDelay;
            if (!_hadFirstStepThisHold)
            {
                nextDelay = Mathf.Max(0f, _config.firstRepeatDelay) * StepCost(dir);
                _hadFirstStepThisHold = true;
            }
            else
            {
                nextDelay = (_config.keepRepeatOnDirChange
                    ? _config.holdRepeatInterval
                    : (HasDirChangedSinceLastFire(dir) ? _config.changeDelay : _config.holdRepeatInterval)) * StepCost(dir);
            }

            _bufferContinuous = new ActionIntent(type, dir);
            _lastFacing = dir;
            _stepCooldown = Mathf.Max(0f, nextDelay);

            _queuedDir = dir;
            _hasQueuedDir = false;
        }

        private float StepCost(GridPos dir)
        {
            if (!_config.diagonalEqualizationEnabled) return 1f;
            return (dir.X != 0 && dir.Y != 0) ? Mathf.Max(1f, _config.diagonalDelayScale) : 1f;
        }

        private bool HasDirChangedSinceLastFire(GridPos current)
        {
            return !_queuedDir.Equals(current);
        }

        private static bool IsNonZero(Vector2 v, float dz) =>
            Mathf.Abs(v.x) >= dz || Mathf.Abs(v.y) >= dz;

        private static GridPos VectorToGrid8(Vector2 v, float dz)
        {
            float x = Mathf.Abs(v.x) >= dz ? Mathf.Sign(v.x) : 0f;
            float y = Mathf.Abs(v.y) >= dz ? Mathf.Sign(v.y) : 0f;
            if (x == 0f && y == 0f) return GridPos.Zero;
            int ix = x > 0f ? 1 : (x < 0f ? -1 : 0);
            int iy = y > 0f ? 1 : (y < 0f ? -1 : 0);
            return new GridPos(ix, iy);
        }
    }
}