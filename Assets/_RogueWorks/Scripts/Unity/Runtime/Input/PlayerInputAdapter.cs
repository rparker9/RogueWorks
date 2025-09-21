using RogueWorks.Core.Actions;
using RogueWorks.Core.Controllers;
using RogueWorks.Core.Primitives;
using RogueWorks.Unity.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RogueWorks.Unity.Runtime.Input
{
    /// <summary>
    /// Refactored PlayerInputAdapter using configuration asset and separated concerns.
    /// Coordinates between InputSystemHandler, MovementController, and SkillController.
    /// </summary>
    public sealed class PlayerInputAdapter : MonoBehaviour, IActorController
    {
        [Header("Configuration")]
        [SerializeField] private PlayerInputConfig config;

        [Header("References")]
        [SerializeField] private InputActionAsset input;
        [SerializeField] private GameRuntime runtime;

        // Controllers
        private InputSystemHandler _inputHandler;
        private MovementController _movementController;
        private SkillController _skillController;

        // Public interface for HasBufferedIntent
        public bool HasBufferedIntent =>
            _movementController?.HasBufferedIntent == true ||
            _skillController?.HasBufferedIntent == true;

        // Skill menu events (forwarded from SkillController)
        public event System.Action SkillMenuOpened;
        public event System.Action SkillMenuClosed;
        public event System.Action<Vector2> SkillMenuNavigated;
        public event System.Action SkillMenuConfirmed;
        public event System.Action SkillMenuCanceled;

        private void Awake()
        {
            if (!config)
            {
                Debug.LogError("[PlayerInputAdapter] PlayerInputConfig is not assigned!");
                enabled = false;
                return;
            }

            if (!input)
            {
                Debug.LogError("[PlayerInputAdapter] InputActionAsset is not assigned!");
                enabled = false;
                return;
            }

            InitializeControllers();
        }

        private void OnEnable()
        {
            _inputHandler?.Initialize(input);
            BindEvents();
        }

        private void OnDisable()
        {
            UnbindEvents();
            _inputHandler?.Cleanup();
        }

        private void Update()
        {
            _movementController?.UpdateTiming(Time.deltaTime);
            _skillController?.Update(runtime, _movementController?.LastFacing ?? GridPos.Right);
        }

        // --------------------------------------------------------------------------
        // IActorController
        // --------------------------------------------------------------------------

        public ActionIntent? GetIntent()
        {
            // Discrete first (skills)
            var discrete = _skillController?.GetDiscreteIntent();
            if (discrete.HasValue)
            {
                return discrete.Value;
            }

            // Then continuous (movement/rotation)
            var continuous = _movementController?.GetContinuousIntent();
            if (continuous.HasValue)
            {
                return continuous.Value;
            }

            return null;
        }

        // --------------------------------------------------------------------------
        // Public API
        // --------------------------------------------------------------------------

        /// <summary>
        /// Called by skill menu UI after a selection is made.
        /// </summary>
        public void CommitSelectedSkill(string skillId, GridPos? dirOverride = null)
        {
            _skillController?.CommitSelectedSkill(
                skillId,
                dirOverride,
                _inputHandler?.MoveVector ?? Vector2.zero,
                runtime,
                _movementController?.LastFacing ?? GridPos.Right
            );
        }

        /// <summary>
        /// Clear continuous buffer only.
        /// </summary>
        public void ClearContinuousBuffer()
        {
            _movementController?.ClearBuffers();
        }

        /// <summary>
        /// Clear all buffers.
        /// </summary>
        public void ClearAllBuffers()
        {
            _movementController?.ClearBuffers();
            _skillController?.ClearBuffers();
        }

        /// <summary>
        /// Called when a blocking clip begins.
        /// </summary>
        public void ClearAttackBuffersOnBlockStart()
        {
            _skillController?.ClearAttackBuffersOnBlockStart();
            _movementController?.ClearBuffers();
        }

        // --------------------------------------------------------------------------
        // Initialization
        // --------------------------------------------------------------------------

        private void InitializeControllers()
        {
            _inputHandler = new InputSystemHandler(config);
            _movementController = new MovementController(config);
            _skillController = new SkillController(config);
        }

        private void BindEvents()
        {
            if (_inputHandler == null || _movementController == null || _skillController == null)
                return;

            // Input handler events
            _inputHandler.MoveInput += OnMoveInput;
            _inputHandler.TurnInput += OnTurnInput;
            _inputHandler.QuickSkillPressed += OnQuickSkillPressed;
            _inputHandler.QuickSkillReleased += OnQuickSkillReleased;
            _inputHandler.SkillMenuPressed += OnSkillMenuPressed;
            _inputHandler.SkillMenuReleased += OnSkillMenuReleased;
            _inputHandler.UINavigated += OnUINavigated;
            _inputHandler.UIConfirmed += OnUIConfirmed;
            _inputHandler.UICanceled += OnUICanceled;

            // Skill controller events (forward to public events)
            _skillController.SkillMenuOpened += () => SkillMenuOpened?.Invoke();
            _skillController.SkillMenuClosed += () =>
            {
                _inputHandler.EnableGameplay();
                SkillMenuClosed?.Invoke();
            };
            _skillController.SkillMenuNavigated += nav => SkillMenuNavigated?.Invoke(nav);
            _skillController.SkillMenuConfirmed += () => SkillMenuConfirmed?.Invoke();
            _skillController.SkillMenuCanceled += () => SkillMenuCanceled?.Invoke();

            // Handle skill menu opening/closing for input maps
            _skillController.SkillMenuOpened += () => _inputHandler.EnableUI();
        }

        private void UnbindEvents()
        {
            if (_inputHandler == null || _movementController == null || _skillController == null)
                return;

            _inputHandler.MoveInput -= OnMoveInput;
            _inputHandler.TurnInput -= OnTurnInput;
            _inputHandler.QuickSkillPressed -= OnQuickSkillPressed;
            _inputHandler.QuickSkillReleased -= OnQuickSkillReleased;
            _inputHandler.SkillMenuPressed -= OnSkillMenuPressed;
            _inputHandler.SkillMenuReleased -= OnSkillMenuReleased;
            _inputHandler.UINavigated -= OnUINavigated;
            _inputHandler.UIConfirmed -= OnUIConfirmed;
            _inputHandler.UICanceled -= OnUICanceled;
        }

        // --------------------------------------------------------------------------
        // Input event handlers
        // --------------------------------------------------------------------------

        private void OnMoveInput(Vector2 moveVector)
        {
            if (_inputHandler.IsTurnHeld)
            {
                _movementController.TryRotate(
                    moveVector,
                    runtime?.IsBlockingView ?? false,
                    _skillController?.IsSuppressingContinuous ?? false
                );
            }
            else
            {
                _movementController.TryMove(
                    moveVector,
                    runtime?.IsBlockingView ?? false,
                    _skillController?.IsSuppressingContinuous ?? false
                );
            }
        }

        private void OnTurnInput(bool isHeld)
        {
            if (isHeld && _inputHandler.IsMoveHeld)
            {
                _movementController.TryRotate(
                    _inputHandler.MoveVector,
                    runtime?.IsBlockingView ?? false,
                    _skillController?.IsSuppressingContinuous ?? false
                );
            }
        }

        private void OnQuickSkillPressed(Vector2 moveVector)
        {
            _skillController.OnQuickSkill(
                moveVector,
                runtime,
                _movementController?.LastFacing ?? GridPos.Right
            );
        }

        private void OnQuickSkillReleased()
        {
            _skillController.OnQuickSkillCanceled();
        }

        private void OnSkillMenuPressed()
        {
            _skillController.OnSkillMenuStart();
        }

        private void OnSkillMenuReleased()
        {
            _skillController.OnSkillMenuEnd();
        }

        private void OnUINavigated(Vector2 navigation)
        {
            _skillController.OnSkillMenuNavigated(navigation);
        }

        private void OnUIConfirmed()
        {
            _skillController.OnSkillMenuConfirmed();
        }

        private void OnUICanceled()
        {
            _skillController.OnSkillMenuCanceled();
        }
    }
}