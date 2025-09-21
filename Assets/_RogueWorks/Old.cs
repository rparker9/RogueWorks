using UnityEngine;

namespace RogueWorks
{
    /*
using UnityEngine;
using UnityEngine.InputSystem;
using RogueWorks.Core.Actions;
using RogueWorks.Core.Controllers;
using RogueWorks.Core.Primitives;

namespace RogueWorks.Unity.Runtime.Input
{
    #region Small Structs
    // ==========================
    // Small Inspector Structs
    // ==========================

    /// <summary>Basic input shaping.</summary>
    [System.Serializable]
    public struct InputFiltering
    {
        [Range(0f, 0.9f)]
        [Tooltip("Minimum stick/axis magnitude required to count as input.")]
        public float deadZone;
    }

    /// <summary>Move/Rotate repeat timing (formerly 'cadence').</summary>
    [System.Serializable]
    public struct MoveTiming
    {
        [Min(0f)]
        [Tooltip("Seconds before the second step can fire after the first immediate step.")]
        public float firstRepeatDelay;

        [Min(0f)]
        [Tooltip("Seconds between steps in steady hold.")]
        public float holdRepeatInterval;

        [Tooltip("If true, changing direction mid-hold does NOT grant an immediate extra step.")]
        public bool keepRepeatOnDirChange;

        [Min(0f)]
        [Tooltip("Extra delay after a direction change if keepRepeatOnDirChange is false.")]
        public float changeDelay;
    }

    /// <summary>Make diagonals feel the same speed as cardinals.</summary>
    [System.Serializable]
    public struct DiagonalEqualization
    {
        [Tooltip("If true, diagonals use a longer repeat time so their effective speed matches cardinals.")]
        public bool enabled;

        [Min(1f)]
        [Tooltip("Delay scale for diagonal steps (~= sqrt(2) = 1.41421356).")]
        public float diagonalDelayScale;
    }

    /// <summary>Rules for accepting skill presses while the view is busy.</summary>
    [System.Serializable]
    public struct SkillGating
    {
        [Tooltip("If true, ignore skill presses while a blocking animation is playing.")]
        public bool ignoreWhileBlocked;

        [Tooltip("If true, after ignoring due to blocking, require RELEASE before next skill is accepted.")]
        public bool requireReleaseAfterBlock;
    }

    /// <summary>Whether to remember a discrete press during blocking (when not hard-gating).</summary>
    [System.Serializable]
    public struct BufferingPolicy
    {
        [Tooltip("If blocking is active and you are NOT ignoring skills, buffer the most recent discrete to fire later.")]
        public bool bufferDiscreteWhileBlocked;
    }

    /// <summary>Quick cast and menu behavior.</summary>
    [System.Serializable]
    public struct SkillConfig
    {
        [Tooltip("Skill id bound to the quick-cast input (temporary; swap to your real id type).")]
        public string quickSkillId;

        [Tooltip("If true, require a non-zero direction at press; otherwise use last facing (fallback Right).")]
        public bool requireDirectionOnQuick;

        [Tooltip("If true, holding the SkillMenu input opens a selector and suppresses movement/rotation.")]
        public bool holdOpensMenu;
    }
    #endregion

    /// <summary>
    /// Adapts Unity Input System to the Core by emitting paced ActionIntent events.
    /// - Move/Rotate: immediate first step, then repeat timing (with optional diagonal equalization).
    /// - Skills: quick-cast (tap) or menu (hold). Press-time direction snapshot + continuous suppression prevents facing flips.
    /// - Discrete buffering & gating mirror your previous behavior.
    /// </summary>
    public sealed class PlayerInputAdapter : MonoBehaviour, IActorController
    {
        // --------------------------------------------------------------------------
        // Inspector (flat input refs stay here; everything else is grouped)
        // --------------------------------------------------------------------------

        [Header("References")]
        [Tooltip("Input Actions asset containing the Gameplay map.")]
        [SerializeField] private InputActionAsset input;

        [Tooltip("Scene host used for view-blocking and readiness checks.")]
        [SerializeField] private GameRuntime runtime;

        [Space(6)]
        [Header("Input Filtering")]
        [SerializeField] private InputFiltering filtering = new InputFiltering { deadZone = 0.25f };

        [Space(6)]
        [Header("Movement Timing (Continuous Move/Rotate)")]
        [SerializeField]
        private MoveTiming timing = new MoveTiming
        {
            firstRepeatDelay = 0.2f,
            holdRepeatInterval = 0.2f,
            keepRepeatOnDirChange = true,
            changeDelay = 0.0f
        };

        [Space(6)]
        [Header("Diagonal Equalization")]
        [SerializeField]
        private DiagonalEqualization diagonal = new DiagonalEqualization
        {
            enabled = true,
            diagonalDelayScale = 1.41421356f
        };

        [Space(6)]
        [Header("Skill Gating")]
        [SerializeField]
        private SkillGating gating = new SkillGating
        {
            ignoreWhileBlocked = true,
            requireReleaseAfterBlock = true
        };

        [Space(6)]
        [Header("Buffering Policy")]
        [SerializeField]
        private BufferingPolicy buffering = new BufferingPolicy
        {
            bufferDiscreteWhileBlocked = true
        };

        [Space(6)]
        [Header("Skills")]
        [SerializeField]
        private SkillConfig skills = new SkillConfig
        {
            quickSkillId = "",
            requireDirectionOnQuick = false,
            holdOpensMenu = true
        };

        [Header("Action Map Names")]
        [SerializeField] private string gameplayMapName = "Gameplay";
        [SerializeField] private string uiMapName = "UI";

        [Header("Gameplay Action Names")]
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string turnActionName = "Turn";
        [SerializeField] private string quickSkillActionName = "QuickSkill";
        [SerializeField] private string skillMenuActionName = "SkillMenu";

        [Header("UI Action Names")]
        [SerializeField] private string uiNavigateActionName = "Navigate";
        [SerializeField] private string uiConfirmActionName = "Confirm";
        [SerializeField] private string uiCancelActionName = "Cancel";

        // --------------------------------------------------------------------------
        // Runtime state
        // --------------------------------------------------------------------------

        /// <summary>True if any intent is currently buffered (discrete or continuous).</summary>
        public bool HasBufferedIntent => _bufferDiscrete.HasValue || _bufferContinuous.HasValue;

        // Buffers
        private ActionIntent? _bufferDiscrete;   // Skill (fire-and-forget)
        private ActionIntent? _bufferContinuous; // Move/Rotate (hold; not kept across blocks)

        // Facing
        private GridPos _lastFacing = GridPos.Right;

        // Input action maps & actions
        private InputActionMap _gameplayMap, _uiMap;
        private InputAction _move, _turn;
        private InputAction _quickSkill, _skillMenu, _uiNavigate, _uiConfirm, _uiCancel;

        // Raw input sampling
        private bool _moveHeldNow;
        private Vector2 _moveVector;
        private bool _turnHeld;

        // Cached delegates for reliable unsubscribe
        private System.Action<InputAction.CallbackContext> _onMovePerformed;
        private System.Action<InputAction.CallbackContext> _onMoveCanceled;
        private System.Action<InputAction.CallbackContext> _onTurnStarted;
        private System.Action<InputAction.CallbackContext> _onTurnCanceled;

        private System.Action<InputAction.CallbackContext> _onQuickSkill;
        private System.Action<InputAction.CallbackContext> _onMenuStart;
        private System.Action<InputAction.CallbackContext> _onMenuEnd;
        private System.Action<InputAction.CallbackContext> _onNav;
        private System.Action<InputAction.CallbackContext> _onConfirm;
        private System.Action<InputAction.CallbackContext> _onCancel;

        // Repeat timing (global)
        private float _stepCooldown;        // seconds until next step allowed
        private bool _hadFirstStepThisHold;
        private GridPos _queuedDir;
        private bool _hasQueuedDir;

        // Discrete deferral (view not ready)
        private bool _skillPending;
        private ActionIntent _skillPendingIntent;

        // Quick-skill latch across block (require release)
        private bool _quickSkillHeld;
        private bool _skillPressLockedUntilRelease;

        // Face-lock / suppression while a discrete is in-flight
        private bool _faceLockActive;
        private bool _suppressContinuousUntilDiscreteConsumed;

        // Skill menu state
        private bool _skillMenuOpen;

        // ---- Skill menu events (UI hooks) ----
        public event System.Action SkillMenuOpened;
        public event System.Action SkillMenuClosed;
        public event System.Action<Vector2> SkillMenuNavigated;  // e.g., WASD/Stick
        public event System.Action SkillMenuConfirmed;
        public event System.Action SkillMenuCanceled;

        // --------------------------------------------------------------------------
        // Unity lifecycle
        // --------------------------------------------------------------------------

        private void OnEnable()
        {
            // ----- Maps -----
            _gameplayMap = input.FindActionMap(gameplayMapName, true);
            _uiMap = input.FindActionMap(uiMapName, true);

            // ----- Gameplay actions -----
            _move = _gameplayMap.FindAction(moveActionName, true);
            _turn = _gameplayMap.FindAction(turnActionName, true);
            _quickSkill = _gameplayMap.FindAction(quickSkillActionName, false);
            _skillMenu = _gameplayMap.FindAction(skillMenuActionName, false);

            // Movement sampling
            _onMovePerformed = ctx =>
            {
                _moveVector = ctx.ReadValue<Vector2>();
                _moveHeldNow = IsNonZero(_moveVector, filtering.deadZone);
            };
            _onMoveCanceled = _ => { _moveVector = Vector2.zero; _moveHeldNow = false; };

            _onTurnStarted = _ => _turnHeld = true;
            _onTurnCanceled = _ => _turnHeld = false;

            _move.performed += _onMovePerformed;
            _move.canceled += _onMoveCanceled;
            _turn.started += _onTurnStarted;
            _turn.canceled += _onTurnCanceled;

            if (!_gameplayMap.enabled) _gameplayMap.Enable();

            // Quick skill (optional)
            if (_quickSkill != null)
            {
                _onQuickSkill = ctx => OnQuickSkill(ctx);
                _quickSkill.performed += _onQuickSkill;
                _quickSkill.canceled += OnQuickSkillCanceled;
            }

            // Skill menu hold (optional)
            if (_skillMenu != null)
            {
                _onMenuStart = ctx => OnSkillMenuStart(ctx); // open on press
                _onMenuEnd = ctx => OnSkillMenuEnd(ctx);   // close on release

                // MORE ROBUST: some interaction setups only fire performed
                _skillMenu.started += _onMenuStart;
                _skillMenu.performed += _onMenuStart;  // <— add this line
                _skillMenu.canceled += _onMenuEnd;
            }
            else
            {
                Debug.LogWarning("[PlayerInputAdapter] Gameplay action 'SkillMenu' not found. Menu will not open.");
            }

            // ----- UI actions (same physical controls as Move, but in UI map) -----
            _uiNavigate = _uiMap.FindAction(uiNavigateActionName, false);
            _uiConfirm = _uiMap.FindAction(uiConfirmActionName, false);
            _uiCancel = _uiMap.FindAction(uiCancelActionName, false);

            if (_uiNavigate != null)
            {
                _onNav = ctx => { if (_skillMenuOpen) SkillMenuNavigated?.Invoke(ctx.ReadValue<Vector2>()); };
                _uiNavigate.performed += _onNav;
            }
            if (_uiConfirm != null)
            {
                _onConfirm = ctx => { if (_skillMenuOpen && ctx.performed) SkillMenuConfirmed?.Invoke(); };
                _uiConfirm.performed += _onConfirm;
            }
            if (_uiCancel != null)
            {
                _onCancel = ctx => { if (_skillMenuOpen && ctx.performed) SkillMenuCanceled?.Invoke(); };
                _uiCancel.performed += _onCancel;
            }

            // Start with Gameplay enabled, UI disabled
            if (_uiMap.enabled) _uiMap.Disable();

            ResetContinuousState();
        }

        private void OnDisable()
        {
            // Gameplay
            if (_move != null)
            {
                if (_onMovePerformed != null) _move.performed -= _onMovePerformed;
                if (_onMoveCanceled != null) _move.canceled -= _onMoveCanceled;
            }
            if (_turn != null)
            {
                if (_onTurnStarted != null) _turn.started -= _onTurnStarted;
                if (_onTurnCanceled != null) _turn.canceled -= _onTurnCanceled;
            }
            if (_quickSkill != null)
            {
                if (_onQuickSkill != null) _quickSkill.performed -= _onQuickSkill;
                _quickSkill.canceled -= OnQuickSkillCanceled;
            }
            if (_skillMenu != null)
            {
                if (_onMenuStart != null)
                {
                    _skillMenu.started -= _onMenuStart;
                    _skillMenu.performed -= _onMenuStart;
                }
                if (_onMenuEnd != null)
                    _skillMenu.canceled -= _onMenuEnd;
            }

            // UI
            if (_uiNavigate != null && _onNav != null) _uiNavigate.performed -= _onNav;
            if (_uiConfirm != null && _onConfirm != null) _uiConfirm.performed -= _onConfirm;
            if (_uiCancel != null && _onCancel != null) _uiCancel.performed -= _onCancel;
        }

        private void Update()
        {
            // Release a pending skill once the view is aligned and unblocked.
            if (_skillPending && runtime != null && runtime.IsPlayerViewReadyForAction() && !runtime.IsBlockingView)
            {
                _skillPending = false;
                EnqueueDiscrete(_skillPendingIntent);
            }

            // Global repeat timer decay
            if (_stepCooldown > 0f)
                _stepCooldown = Mathf.Max(0f, _stepCooldown - Time.deltaTime);

            // Read current 8-way dir
            GridPos dir = VectorToGrid8(_moveVector, filtering.deadZone);
            bool held = _moveHeldNow && !dir.Equals(GridPos.Zero);

            if (!held)
            {
                ResetContinuousState();
                return;
            }

            // Block continuous while a discrete (skill) is in-flight or menu is open
            if (_suppressContinuousUntilDiscreteConsumed || _faceLockActive || _skillMenuOpen)
                return;

            // Turn-in-place shares the same repeat timing
            if (_turnHeld)
            {
                TryFireContinuous(IntentType.Rotate, dir);
                return;
            }

            TryFireContinuous(IntentType.Move, dir);
        }

        // --------------------------------------------------------------------------
        // IActorController
        // --------------------------------------------------------------------------

        /// <summary>Return a single intent ready this frame: discrete first, then continuous.</summary>
        public ActionIntent? GetIntent()
        {
            if (_bufferDiscrete.HasValue)
            {
                var d = _bufferDiscrete.Value;
                _bufferDiscrete = null;

                // Discrete consumed by Core -> unlock continuous next frame.
                _faceLockActive = false;
                _suppressContinuousUntilDiscreteConsumed = false;

                // Ensure no stale continuous leaks this slice.
                _bufferContinuous = null;
                _hasQueuedDir = false;

                return d;
            }

            if (_bufferContinuous.HasValue)
            {
                var c = _bufferContinuous.Value;
                _bufferContinuous = null;
                return c;
            }

            if (_hasQueuedDir && _stepCooldown <= 0f)
            {
                TryFireContinuous(IntentType.Move, _queuedDir);
                if (_bufferContinuous.HasValue)
                {
                    var c2 = _bufferContinuous.Value;
                    _bufferContinuous = null;
                    return c2;
                }
            }

            return null;
        }

        // --------------------------------------------------------------------------
        // Skills: Quick cast + Menu
        // --------------------------------------------------------------------------

        private void OnQuickSkill(InputAction.CallbackContext ctx)
        {
            _quickSkillHeld = true;

            // Direction snapshot at press-time
            GridPos dir = VectorToGrid8(_moveVector, filtering.deadZone);
            if (skills.requireDirectionOnQuick && dir.Equals(GridPos.Zero))
                return; // demand explicit dir

            if (dir.Equals(GridPos.Zero)) dir = _lastFacing;
            if (dir.Equals(GridPos.Zero)) dir = GridPos.Right;

            bool isBlocked = runtime != null && runtime.IsBlockingView;
            if (gating.ignoreWhileBlocked && isBlocked)
            {
                if (gating.requireReleaseAfterBlock) _skillPressLockedUntilRelease = true;
                return;
            }
            if (_skillPressLockedUntilRelease) return;

            var intent = MakeSkillIntent(skills.quickSkillId, dir);

            // Defer if view not ready
            if (runtime != null && !runtime.IsPlayerViewReadyForAction())
            {
                if (!gating.ignoreWhileBlocked || !isBlocked)
                {
                    _skillPending = true;
                    _skillPendingIntent = intent;

                    _faceLockActive = true;
                    _suppressContinuousUntilDiscreteConsumed = true;

                    _bufferContinuous = null;
                    _hasQueuedDir = false;
                }
                return;
            }

            // Cast now
            EnqueueDiscrete(intent);
            _lastFacing = dir;

            _faceLockActive = true;
            _suppressContinuousUntilDiscreteConsumed = true;
            _bufferContinuous = null;
            _hasQueuedDir = false;
        }

        private void OnQuickSkillCanceled(InputAction.CallbackContext ctx)
        {
            _quickSkillHeld = false;
            if (gating.requireReleaseAfterBlock) _skillPressLockedUntilRelease = false;
        }

        private void OnSkillMenuStart(InputAction.CallbackContext ctx)
        {
            if (!skills.holdOpensMenu || _skillMenuOpen)
                return;
            _skillMenuOpen = true;

            // Keep Gameplay map enabled, but disable gameplay actions that should not run while menu is open.
            SetGameplayActionsEnabled(false);
            // Leave _skillMenu enabled so we get the 'canceled' when user releases.

            // Turn UI map on for navigation/confirm/cancel.
            if (!_uiMap.enabled)
                _uiMap.Enable();

            // Clear any queued gameplay so nothing fires after close.
            _bufferContinuous = null;
            _hasQueuedDir = false;

            SkillMenuOpened?.Invoke();
        }

        private void OnSkillMenuEnd(InputAction.CallbackContext ctx)
        {
            if (!_skillMenuOpen) return;
            _skillMenuOpen = false;

            // Turn UI map off; resume gameplay actions.
            if (_uiMap.enabled) _uiMap.Disable();
            SetGameplayActionsEnabled(true);

            // Let gameplay resume next frame (we already keep _suppressContinuous... off here)
            _suppressContinuousUntilDiscreteConsumed = false;

            SkillMenuClosed?.Invoke();
        }

        /// <summary>
        /// Called by the skill menu UI after a selection is made. Optionally pass a direction override.
        /// CommitSelectedSkill(...) already receives the id; QuickSkill uses skills.quickSkillId
        /// </summary>
        public void CommitSelectedSkill(string skillId, GridPos? dirOverride = null)
        {
            GridPos dir = dirOverride ?? VectorToGrid8(_moveVector, filtering.deadZone);
            if (dir.Equals(GridPos.Zero)) dir = _lastFacing;
            if (dir.Equals(GridPos.Zero)) dir = GridPos.Right;

            bool isBlocked = runtime != null && runtime.IsBlockingView;
            if (gating.ignoreWhileBlocked && isBlocked)
            {
                if (gating.requireReleaseAfterBlock) _skillPressLockedUntilRelease = true;
                return;
            }

            var intent = MakeSkillIntent(skillId, dir);

            if (runtime != null && !runtime.IsPlayerViewReadyForAction())
            {
                if (!gating.ignoreWhileBlocked || !isBlocked)
                {
                    _skillPending = true;
                    _skillPendingIntent = intent;

                    _faceLockActive = true;
                    _suppressContinuousUntilDiscreteConsumed = true;

                    _bufferContinuous = null;
                    _hasQueuedDir = false;
                }
            }
            else
            {
                EnqueueDiscrete(intent);
                _lastFacing = dir;

                _faceLockActive = true;
                _suppressContinuousUntilDiscreteConsumed = true;
                _bufferContinuous = null;
                _hasQueuedDir = false;
            }

            if (_skillMenuOpen) OnSkillMenuEnd(default);
        }

        private ActionIntent MakeSkillIntent(string skillId, GridPos dir)
        {
            return ActionIntent.OfSkill(skillId, dir);  // embeds the id
        }

        // --------------------------------------------------------------------------
        // Input -> Intent (Move/Rotate)
        // --------------------------------------------------------------------------

        /// <summary>
        /// Attempt to emit a continuous intent (Move/Rotate) respecting timing and blocking.
        /// Commits facing only when a step actually fires.
        /// </summary>
        private void TryFireContinuous(IntentType type, GridPos dir)
        {
            if (_suppressContinuousUntilDiscreteConsumed || _skillMenuOpen)
                return;

            bool blocked = runtime != null && runtime.IsBlockingView;
            if (blocked)
            {
                _bufferContinuous = null;
                return;
            }

            if (_stepCooldown > 0f)
            {
                _queuedDir = dir;
                _hasQueuedDir = true;
                return;
            }

            float nextDelay;
            if (!_hadFirstStepThisHold)
            {
                nextDelay = Mathf.Max(0f, timing.firstRepeatDelay) * StepCost(dir);
                _hadFirstStepThisHold = true;
            }
            else
            {
                nextDelay = (timing.keepRepeatOnDirChange
                    ? timing.holdRepeatInterval
                    : (HasDirChangedSinceLastFire(dir) ? timing.changeDelay : timing.holdRepeatInterval)) * StepCost(dir);
            }

            _bufferContinuous = new ActionIntent(type, dir);
            _lastFacing = dir; // commit facing only when a step actually fires
            _stepCooldown = Mathf.Max(0f, nextDelay);

            _queuedDir = dir;
            _hasQueuedDir = false;
        }

        // --------------------------------------------------------------------------
        // Buffering helpers & runtime resets
        // --------------------------------------------------------------------------

        /// <summary>Queue a discrete action (skill) if current policy allows.</summary>
        private void EnqueueDiscrete(ActionIntent intent)
        {
            bool blocked = runtime != null && runtime.IsBlockingView;

            if (gating.ignoreWhileBlocked && blocked)
                return;

            if (blocked)
            {
                if (buffering.bufferDiscreteWhileBlocked) _bufferDiscrete = intent; // latest wins
                return;
            }

            _bufferDiscrete = intent;
        }

        /// <summary>Clears all movement/rotation timing state and continuous buffer.</summary>
        public void ClearContinuousBuffer() => ResetContinuousState();

        /// <summary>Clears both discrete and continuous buffers.</summary>
        public void ClearAllBuffers()
        {
            _bufferDiscrete = null;
            ResetContinuousState();
        }

        /// <summary>
        /// Called when a blocking clip begins (GameRuntime.onBlockStart):
        /// drop pending/latched skill, and release any locks.
        /// </summary>
        public void ClearAttackBuffersOnBlockStart() // keep name for compatibility with current GameRuntime
        {
            _bufferDiscrete = null;
            _skillPending = false;

            if (gating.requireReleaseAfterBlock)
                _skillPressLockedUntilRelease = _quickSkillHeld;

            _faceLockActive = false;
            _suppressContinuousUntilDiscreteConsumed = false;
        }

        /// <summary>Local reset for movement/rotation buffering + repeat timing.</summary>
        private void ResetContinuousState()
        {
            _bufferContinuous = null;

            _stepCooldown = 0f;
            _hadFirstStepThisHold = false;

            _hasQueuedDir = false;
            _queuedDir = GridPos.Zero;
        }

        // --------------------------------------------------------------------------
        // Math helpers
        // --------------------------------------------------------------------------
        private void SetGameplayActionsEnabled(bool enable)
        {
            if (_move != null) { if (enable) _move.Enable(); else _move.Disable(); }
            if (_turn != null) { if (enable) _turn.Enable(); else _turn.Disable(); }
            if (_quickSkill != null) { if (enable) _quickSkill.Enable(); else _quickSkill.Disable(); }

            // IMPORTANT: do NOT touch _skillMenu here. It must remain enabled to receive 'canceled'.
        }

        private float StepCost(GridPos dir)
        {
            if (!diagonal.enabled) return 1f;
            return (dir.X != 0 && dir.Y != 0) ? Mathf.Max(1f, diagonal.diagonalDelayScale) : 1f;
        }

        private bool HasDirChangedSinceLastFire(GridPos current)
        {
            // Compare to last-fired direction (stored in _queuedDir right after firing).
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
*/
}
