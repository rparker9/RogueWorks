using RogueWorks.Core.Actions;
using RogueWorks.Core.Primitives;
using RogueWorks.Unity.Data;
using UnityEngine;

namespace RogueWorks.Unity.Runtime.Input
{
    /// <summary>
    /// Handles skill input, gating, buffering, and skill menu state.
    /// </summary>
    public class SkillController
    {
        private readonly PlayerInputConfig _config;

        // Skill state
        private ActionIntent? _bufferDiscrete;
        private bool _skillPending;
        private ActionIntent _skillPendingIntent;

        // Quick skill state
        private bool _quickSkillHeld;
        private bool _skillPressLockedUntilRelease;

        // Skill menu state
        private bool _skillMenuOpen;

        // Face-lock state
        private bool _faceLockActive;
        private bool _suppressContinuousUntilDiscreteConsumed;

        public bool HasBufferedIntent => _bufferDiscrete.HasValue;
        public bool IsFaceLockActive => _faceLockActive;
        public bool IsSuppressingContinuous => _suppressContinuousUntilDiscreteConsumed || _faceLockActive || _skillMenuOpen;
        public bool IsSkillMenuOpen => _skillMenuOpen;

        // Events for skill menu
        public event System.Action SkillMenuOpened;
        public event System.Action SkillMenuClosed;
        public event System.Action<Vector2> SkillMenuNavigated;
        public event System.Action SkillMenuConfirmed;
        public event System.Action SkillMenuCanceled;

        public SkillController(PlayerInputConfig config)
        {
            _config = config;
        }

        public void Update(GameRuntime runtime, GridPos lastFacing)
        {
            // Release a pending skill once the view is aligned and unblocked
            if (_skillPending && runtime != null && runtime.IsPlayerViewReadyForAction() && !runtime.IsBlockingView)
            {
                _skillPending = false;
                EnqueueDiscrete(_skillPendingIntent, runtime);
            }
        }

        public ActionIntent? GetDiscreteIntent()
        {
            if (_bufferDiscrete.HasValue)
            {
                var intent = _bufferDiscrete.Value;
                _bufferDiscrete = null;

                // Discrete consumed -> unlock continuous next frame
                _faceLockActive = false;
                _suppressContinuousUntilDiscreteConsumed = false;

                return intent;
            }

            return null;
        }

        public void OnQuickSkill(Vector2 moveVector, GameRuntime runtime, GridPos lastFacing)
        {
            _quickSkillHeld = true;

            // Direction snapshot at press-time
            GridPos dir = VectorToGrid8(moveVector, _config.deadZone);
            if (_config.requireDirectionOnQuick && dir.Equals(GridPos.Zero))
                return;

            if (dir.Equals(GridPos.Zero)) dir = lastFacing;
            if (dir.Equals(GridPos.Zero)) dir = GridPos.Right;

            bool isBlocked = runtime != null && runtime.IsBlockingView;
            if (_config.ignoreSkillsWhileBlocked && isBlocked)
            {
                if (_config.requireReleaseAfterBlock) _skillPressLockedUntilRelease = true;
                return;
            }
            if (_skillPressLockedUntilRelease) return;

            var intent = MakeSkillIntent(_config.quickSkillId, dir);

            // Defer if view not ready
            if (runtime != null && !runtime.IsPlayerViewReadyForAction())
            {
                if (!_config.ignoreSkillsWhileBlocked || !isBlocked)
                {
                    _skillPending = true;
                    _skillPendingIntent = intent;
                    ActivateFaceLock();
                }
                return;
            }

            // Cast now
            EnqueueDiscrete(intent, runtime);
            ActivateFaceLock();
        }

        public void OnQuickSkillCanceled()
        {
            _quickSkillHeld = false;
            if (_config.requireReleaseAfterBlock) _skillPressLockedUntilRelease = false;
        }

        public void OnSkillMenuStart()
        {
            if (!_config.holdOpensMenu || _skillMenuOpen)
                return;

            _skillMenuOpen = true;
            SkillMenuOpened?.Invoke();
        }

        public void OnSkillMenuEnd()
        {
            if (!_skillMenuOpen) return;

            _skillMenuOpen = false;
            _suppressContinuousUntilDiscreteConsumed = false;
            SkillMenuClosed?.Invoke();
        }

        public void OnSkillMenuNavigated(Vector2 navigation)
        {
            if (_skillMenuOpen)
                SkillMenuNavigated?.Invoke(navigation);
        }

        public void OnSkillMenuConfirmed()
        {
            if (_skillMenuOpen)
                SkillMenuConfirmed?.Invoke();
        }

        public void OnSkillMenuCanceled()
        {
            if (_skillMenuOpen)
                SkillMenuCanceled?.Invoke();
        }

        public void CommitSelectedSkill(string skillId, GridPos? dirOverride, Vector2 moveVector, GameRuntime runtime, GridPos lastFacing)
        {
            GridPos dir = dirOverride ?? VectorToGrid8(moveVector, _config.deadZone);
            if (dir.Equals(GridPos.Zero)) dir = lastFacing;
            if (dir.Equals(GridPos.Zero)) dir = GridPos.Right;

            bool isBlocked = runtime != null && runtime.IsBlockingView;
            if (_config.ignoreSkillsWhileBlocked && isBlocked)
            {
                if (_config.requireReleaseAfterBlock) _skillPressLockedUntilRelease = true;
                return;
            }

            var intent = MakeSkillIntent(skillId, dir);

            if (runtime != null && !runtime.IsPlayerViewReadyForAction())
            {
                if (!_config.ignoreSkillsWhileBlocked || !isBlocked)
                {
                    _skillPending = true;
                    _skillPendingIntent = intent;
                    ActivateFaceLock();
                }
            }
            else
            {
                EnqueueDiscrete(intent, runtime);
                ActivateFaceLock();
            }

            if (_skillMenuOpen) OnSkillMenuEnd();
        }

        public void ClearBuffers()
        {
            _bufferDiscrete = null;
        }

        public void ClearAttackBuffersOnBlockStart()
        {
            _bufferDiscrete = null;
            _skillPending = false;

            if (_config.requireReleaseAfterBlock)
                _skillPressLockedUntilRelease = _quickSkillHeld;

            _faceLockActive = false;
            _suppressContinuousUntilDiscreteConsumed = false;
        }

        private void EnqueueDiscrete(ActionIntent intent, GameRuntime runtime)
        {
            bool blocked = runtime != null && runtime.IsBlockingView;

            if (_config.ignoreSkillsWhileBlocked && blocked)
                return;

            if (blocked)
            {
                if (_config.bufferDiscreteWhileBlocked)
                    _bufferDiscrete = intent; // latest wins
                return;
            }

            _bufferDiscrete = intent;
        }

        private void ActivateFaceLock()
        {
            _faceLockActive = true;
            _suppressContinuousUntilDiscreteConsumed = true;
        }

        private static ActionIntent MakeSkillIntent(string skillId, GridPos dir)
        {
            return ActionIntent.OfSkill(skillId, dir);
        }

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