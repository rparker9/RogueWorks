using RogueWorks.Unity.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RogueWorks.Unity.Runtime.Input
{
    /// <summary>
    /// Handles Unity Input System binding and callbacks.
    /// </summary>
    public class InputSystemHandler
    {
        private readonly PlayerInputConfig _config;

        // Input action maps & actions
        private InputActionAsset _input;
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
        private System.Action<InputAction.CallbackContext> _onQuickSkillCanceled;
        private System.Action<InputAction.CallbackContext> _onMenuStart;
        private System.Action<InputAction.CallbackContext> _onMenuEnd;
        private System.Action<InputAction.CallbackContext> _onNav;
        private System.Action<InputAction.CallbackContext> _onConfirm;
        private System.Action<InputAction.CallbackContext> _onCancel;

        // Events
        public event System.Action<Vector2> MoveInput;
        public event System.Action<bool> TurnInput;
        public event System.Action<Vector2> QuickSkillPressed;
        public event System.Action QuickSkillReleased;
        public event System.Action SkillMenuPressed;
        public event System.Action SkillMenuReleased;
        public event System.Action<Vector2> UINavigated;
        public event System.Action UIConfirmed;
        public event System.Action UICanceled;

        public bool IsMoveHeld => _moveHeldNow;
        public Vector2 MoveVector => _moveVector;
        public bool IsTurnHeld => _turnHeld;

        public InputSystemHandler(PlayerInputConfig config)
        {
            _config = config;
        }

        public void Initialize(InputActionAsset input)
        {
            _input = input;

            // Get maps
            _gameplayMap = _input.FindActionMap(_config.gameplayMapName, true);
            _uiMap = _input.FindActionMap(_config.uiMapName, true);

            // Get gameplay actions
            _move = _gameplayMap.FindAction(_config.moveActionName, true);
            _turn = _gameplayMap.FindAction(_config.turnActionName, true);
            _quickSkill = _gameplayMap.FindAction(_config.quickSkillActionName, false);
            _skillMenu = _gameplayMap.FindAction(_config.skillMenuActionName, false);

            // Get UI actions
            _uiNavigate = _uiMap.FindAction(_config.uiNavigateActionName, false);
            _uiConfirm = _uiMap.FindAction(_config.uiConfirmActionName, false);
            _uiCancel = _uiMap.FindAction(_config.uiCancelActionName, false);

            BindCallbacks();
            EnableGameplay();
        }

        public void Cleanup()
        {
            UnbindCallbacks();
        }

        public void EnableGameplay()
        {
            if (!_gameplayMap.enabled) _gameplayMap.Enable();
            if (_uiMap.enabled) _uiMap.Disable();
        }

        public void EnableUI()
        {
            if (!_uiMap.enabled) _uiMap.Enable();
            // Keep gameplay enabled but disable specific actions
            SetGameplayActionsEnabled(false);
        }

        public void SetGameplayActionsEnabled(bool enable)
        {
            if (_move != null) { if (enable) _move.Enable(); else _move.Disable(); }
            if (_turn != null) { if (enable) _turn.Enable(); else _turn.Disable(); }
            if (_quickSkill != null) { if (enable) _quickSkill.Enable(); else _quickSkill.Disable(); }
            // Note: _skillMenu stays enabled to receive canceled events
        }

        private void BindCallbacks()
        {
            // Movement sampling
            _onMovePerformed = ctx =>
            {
                _moveVector = ctx.ReadValue<Vector2>();
                _moveHeldNow = IsNonZero(_moveVector, _config.deadZone);
                MoveInput?.Invoke(_moveVector);
            };
            _onMoveCanceled = _ =>
            {
                _moveVector = Vector2.zero;
                _moveHeldNow = false;
                MoveInput?.Invoke(_moveVector);
            };

            _onTurnStarted = _ => { _turnHeld = true; TurnInput?.Invoke(true); };
            _onTurnCanceled = _ => { _turnHeld = false; TurnInput?.Invoke(false); };

            _move.performed += _onMovePerformed;
            _move.canceled += _onMoveCanceled;
            _turn.started += _onTurnStarted;
            _turn.canceled += _onTurnCanceled;

            // Quick skill
            if (_quickSkill != null)
            {
                _onQuickSkill = ctx => QuickSkillPressed?.Invoke(_moveVector);
                _onQuickSkillCanceled = _ => QuickSkillReleased?.Invoke();
                _quickSkill.performed += _onQuickSkill;
                _quickSkill.canceled += _onQuickSkillCanceled;
            }

            // Skill menu
            if (_skillMenu != null)
            {
                _onMenuStart = _ => SkillMenuPressed?.Invoke();
                _onMenuEnd = _ => SkillMenuReleased?.Invoke();
                _skillMenu.started += _onMenuStart;
                _skillMenu.performed += _onMenuStart;
                _skillMenu.canceled += _onMenuEnd;
            }

            // UI actions
            if (_uiNavigate != null)
            {
                _onNav = ctx => UINavigated?.Invoke(ctx.ReadValue<Vector2>());
                _uiNavigate.performed += _onNav;
            }
            if (_uiConfirm != null)
            {
                _onConfirm = ctx => { if (ctx.performed) UIConfirmed?.Invoke(); };
                _uiConfirm.performed += _onConfirm;
            }
            if (_uiCancel != null)
            {
                _onCancel = ctx => { if (ctx.performed) UICanceled?.Invoke(); };
                _uiCancel.performed += _onCancel;
            }
        }

        private void UnbindCallbacks()
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
                if (_onQuickSkillCanceled != null) _quickSkill.canceled -= _onQuickSkillCanceled;
            }
            if (_skillMenu != null)
            {
                if (_onMenuStart != null)
                {
                    _skillMenu.started -= _onMenuStart;
                    _skillMenu.performed -= _onMenuStart;
                }
                if (_onMenuEnd != null) _skillMenu.canceled -= _onMenuEnd;
            }

            // UI
            if (_uiNavigate != null && _onNav != null) _uiNavigate.performed -= _onNav;
            if (_uiConfirm != null && _onConfirm != null) _uiConfirm.performed -= _onConfirm;
            if (_uiCancel != null && _onCancel != null) _uiCancel.performed -= _onCancel;
        }

        private static bool IsNonZero(Vector2 v, float dz) =>
            Mathf.Abs(v.x) >= dz || Mathf.Abs(v.y) >= dz;
    }
}