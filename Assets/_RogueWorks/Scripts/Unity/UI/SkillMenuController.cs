using System.Collections.Generic;
using UnityEngine;
using RogueWorks.Core.Primitives;
using RogueWorks.Core.Skills;
using RogueWorks.Unity.Runtime.Input;

namespace RogueWorks.Unity.UI
{
    /// <summary>
    /// Skill grid menu driven by the Actor's runtime skills only.
    /// - Data source is provided via SetRuntimeSkills(IReadOnlyList&lt;Skill&gt;).
    /// - Confirm emits the string skill id via PlayerInputAdapter.CommitSelectedSkill.
    /// </summary>
    public class SkillMenuController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private PlayerInputAdapter inputAdapter;   // assign in scene
        [SerializeField] private CanvasGroup canvasGroup;           // root of the menu
        [SerializeField] private RectTransform gridRoot;            // parent for item views
        [SerializeField] private SkillItemView itemPrefab;          // tile prefab

        [Header("Navigation")]
        [Tooltip("Columns in the grid. Rows will be computed.")]
        [SerializeField] private int columns = 4;

        [Tooltip("Seconds between repeats when holding stick/keys to navigate.")]
        [SerializeField] private float navRepeatInterval = 0.18f;

        [Tooltip("Deadzone for navigation vector.")]
        [SerializeField] private float navDeadZone = 0.35f;

        // Runtime rows
        private readonly List<Skill> _skills = new();
        private readonly List<SkillItemView> _items = new();

        private int _selectedIndex = 0;
        private bool _open;
        private float _navCooldown;

        private void Awake()
        {
            SetVisible(false, instant: true);
            ClearItems();
        }

        private void OnEnable()
        {
            if (!inputAdapter) return;
            inputAdapter.SkillMenuOpened += OnMenuOpened;
            inputAdapter.SkillMenuClosed += OnMenuClosed;
            inputAdapter.SkillMenuNavigated += OnMenuNavigated;
            inputAdapter.SkillMenuConfirmed += OnMenuConfirmed;
            inputAdapter.SkillMenuCanceled += OnMenuCanceled;
        }

        private void OnDisable()
        {
            if (!inputAdapter) return;
            inputAdapter.SkillMenuOpened -= OnMenuOpened;
            inputAdapter.SkillMenuClosed -= OnMenuClosed;
            inputAdapter.SkillMenuNavigated -= OnMenuNavigated;
            inputAdapter.SkillMenuConfirmed -= OnMenuConfirmed;
            inputAdapter.SkillMenuCanceled -= OnMenuCanceled;
        }

        private void Update()
        {
            if (!_open) return;
            if (_navCooldown > 0f)
                _navCooldown = Mathf.Max(0f, _navCooldown - Time.unscaledDeltaTime);
        }

        // ==========================================================
        // Public API
        // ==========================================================

        /// <summary>
        /// Populate menu with the actor's runtime skills.
        /// </summary>
        public void SetRuntimeSkills(IReadOnlyList<Skill> coreSkills)
        {
            _skills.Clear();
            if (coreSkills != null)
                _skills.AddRange(coreSkills);

            RebuildItems();
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _skills.Count - 1));
            UpdateSelectionVisuals();
        }

        public void ClearRuntimeSkills()
        {
            _skills.Clear();
            ClearItems();
            _selectedIndex = 0;
        }

        // ==========================================================
        // Input events
        // ==========================================================

        private void OnMenuOpened()
        {
            if (_skills.Count == 0)
            {
                Debug.LogWarning("[SkillMenuController] Open requested but no runtime skills are set. Call SetRuntimeSkills(actor.Skills) before opening.");
                return;
            }
            _open = true;
            SetVisible(true);
            _navCooldown = 0f;
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _skills.Count - 1);
            UpdateSelectionVisuals();
        }


        private void OnMenuClosed()
        {
            _open = false;
            SetVisible(false);
        }

        private void OnMenuNavigated(Vector2 v)
        {
            if (!_open || _skills.Count == 0) return;

            if (_navCooldown > 0f) return;
            if (Mathf.Abs(v.x) < navDeadZone && Mathf.Abs(v.y) < navDeadZone) return;

            int step = 0;
            if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            {
                step = v.x > 0 ? +1 : -1;
            }
            else
            {
                int rowStep = v.y > 0 ? -1 : +1; // up visually reduces row
                step = rowStep * columns;
            }

            MoveSelection(step);
            _navCooldown = navRepeatInterval;
        }

        private void OnMenuConfirmed()
        {
            if (!_open || _skills.Count == 0) return;

            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _skills.Count - 1);
            var skill = _skills[_selectedIndex];
            if (skill == null || string.IsNullOrEmpty(skill.Id)) return;

            inputAdapter.CommitSelectedSkill(skill.Id, dirOverride: null);
        }

        private void OnMenuCanceled()
        {
            if (_open) OnMenuClosed();
        }

        // ==========================================================
        // Helpers
        // ==========================================================

        private void MoveSelection(int delta)
        {
            if (_skills.Count == 0) return;

            int newIndex = Mathf.Clamp(_selectedIndex + delta, 0, _skills.Count - 1);
            if (newIndex == _selectedIndex) return;

            _selectedIndex = newIndex;
            UpdateSelectionVisuals();
        }

        private void RebuildItems()
        {
            ClearItems();

            for (int i = 0; i < _skills.Count; i++)
            {
                var s = _skills[i];
                var view = Instantiate(itemPrefab, gridRoot);

                view.Bind(s?.DisplayName ?? s?.Id ?? "—", null); // text only; icons can be wired later
                int idx = i;
                view.onClick = () =>
                {
                    _selectedIndex = idx;
                    UpdateSelectionVisuals();
                    OnMenuConfirmed();
                };

                _items.Add(view);
            }
        }

        private void ClearItems()
        {
            foreach (Transform child in gridRoot) Destroy(child.gameObject);
            _items.Clear();
        }

        private void UpdateSelectionVisuals()
        {
            for (int i = 0; i < _items.Count; i++)
                _items[i].SetSelected(i == _selectedIndex);
        }

        private void SetVisible(bool visible, bool instant = false)
        {
            if (!canvasGroup) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }
}
