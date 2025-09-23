using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RogueWorks.Core.Primitives;
using RogueWorks.Core.Skills;
using RogueWorks.Unity.Runtime.Input;

namespace RogueWorks.Unity.UI
{
    /// <summary>
    /// Scrollable vertical list of skills with highlight + confirm.
    /// - Data source: SetRuntimeSkills(IReadOnlyList<Skill>).
    /// - Navigation: up/down via SkillMenuNavigated(Vector2).
    /// - Confirm: emits selected skill id via PlayerInputAdapter.CommitSelectedSkill.
    /// </summary>
    public sealed class SkillMenuController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private PlayerInputAdapter inputAdapter;   // assign in scene
        [SerializeField] private CanvasGroup canvasGroup;           // root visibility
        [SerializeField] private ScrollRect scrollRect;             // ScrollRect on ScrollView
        [SerializeField] private RectTransform listContent;         // "Content" under Viewport
        [SerializeField] private SkillItemView itemPrefab;          // row prefab

        [Header("Navigation")]
        [Tooltip("Seconds between repeats when holding stick/keys to navigate.")]
        [SerializeField] private float navRepeatInterval = 0.18f;

        [Tooltip("Deadzone for navigation vector magnitude.")]
        [SerializeField] private float navDeadZone = 0.35f;

        [Tooltip("Wrap selection at ends.")]
        [SerializeField] private bool wrapSelection = true;

        // Internal state
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

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Rebuilds the list from the actor's runtime skills.
        /// </summary>
        public void SetRuntimeSkills(IReadOnlyList<Skill> coreSkills)
        {
            _skills.Clear();
            if (coreSkills != null) _skills.AddRange(coreSkills);

            RebuildItems();
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _skills.Count - 1));
            UpdateSelectionVisuals(scrollToSelection: true);
        }

        /// <summary>
        /// Clears current list and selection.
        /// </summary>
        public void ClearRuntimeSkills()
        {
            _skills.Clear();
            ClearItems();
            _selectedIndex = 0;
        }

        // =====================================================================
        // Input events from PlayerInputAdapter
        // =====================================================================

        private void OnMenuOpened()
        {
            if (_skills.Count == 0)
            {
                Debug.LogWarning("[SkillMenuController] Open requested with no skills. Call SetRuntimeSkills(...) first.");
                return;
            }
            _open = true;
            SetVisible(true);
            _navCooldown = 0f;
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _skills.Count - 1);
            UpdateSelectionVisuals(scrollToSelection: true);
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

            // Use vertical intent primarily. If horizontal input is larger, map it to vertical steps as well.
            float absX = Mathf.Abs(v.x);
            float absY = Mathf.Abs(v.y);
            float mag = Mathf.Max(absX, absY);

            if (mag < navDeadZone) return;

            int step;
            if (absY >= absX)
            {
                // Up is negative step (visually up), down is positive step.
                step = (v.y > 0f) ? -1 : +1;
            }
            else
            {
                // Allow left/right to move as up/down for controller D-pads.
                step = (v.x < 0f) ? -1 : +1;
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

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>
        /// Adjusts selection index and updates visuals/scroll.
        /// </summary>
        private void MoveSelection(int delta)
        {
            if (_skills.Count == 0) return;

            int newIndex = _selectedIndex + delta;

            if (wrapSelection)
            {
                // Wrap around
                if (newIndex < 0) newIndex = _skills.Count - 1;
                else if (newIndex >= _skills.Count) newIndex = 0;
            }
            else
            {
                newIndex = Mathf.Clamp(newIndex, 0, _skills.Count - 1);
            }

            if (newIndex == _selectedIndex) return;

            _selectedIndex = newIndex;
            UpdateSelectionVisuals(scrollToSelection: true);
        }

        /// <summary>
        /// Recreates list items to match current _skills.
        /// </summary>
        private void RebuildItems()
        {
            ClearItems();

            for (int i = 0; i < _skills.Count; i++)
            {
                var s = _skills[i];
                var view = Instantiate(itemPrefab, listContent);

                // Bind text/icon (icons optional)
                view.Bind(s?.DisplayName ?? s?.Id ?? "—", null);

                int idx = i; // capture for lambda
                view.onClick = () =>
                {
                    _selectedIndex = idx;
                    UpdateSelectionVisuals(scrollToSelection: true);
                    OnMenuConfirmed();
                };

                _items.Add(view);
            }

            // Reset ScrollRect position to top when rebuilding
            if (scrollRect) scrollRect.normalizedPosition = new Vector2(scrollRect.normalizedPosition.x, 1f);
        }

        /// <summary>
        /// Destroys all current list rows.
        /// </summary>
        private void ClearItems()
        {
            if (!listContent) return;
            for (int i = listContent.childCount - 1; i >= 0; i--)
                Destroy(listContent.GetChild(i).gameObject);
            _items.Clear();
        }

        /// <summary>
        /// Applies selected highlight state and optionally scrolls selection into view.
        /// </summary>
        private void UpdateSelectionVisuals(bool scrollToSelection)
        {
            for (int i = 0; i < _items.Count; i++)
                _items[i].SetSelected(i == _selectedIndex);

            if (scrollToSelection)
                ScrollToIndex(_selectedIndex);
        }

        /// <summary>
        /// Scrolls the ScrollRect so that the selected row is fully visible.
        /// Assumes vertical scrolling with content pivot at top (0.5, 1).
        /// </summary>
        private void ScrollToIndex(int index)
        {
            if (!scrollRect || !listContent || _items.Count == 0) return;

            var viewport = scrollRect.viewport ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
            if (!viewport) return;

            // Compute item position in content space.
            var itemRt = _items[index].GetComponent<RectTransform>();
            if (!itemRt) return;

            // Convert bounds to content local Y.
            float contentHeight = listContent.rect.height;
            float viewportHeight = viewport.rect.height;

            // Top of item relative to content's top (assuming pivot.y = 1 on content)
            float itemTopY = Mathf.Abs(itemRt.anchoredPosition.y);
            float itemBottomY = itemTopY + itemRt.rect.height;

            // Current scroll offset (0 = top, 1 = bottom)
            float currentNormalized = scrollRect.verticalNormalizedPosition;

            // Visible window in content space (top to bottom)
            float viewTop = (1f - currentNormalized) * Mathf.Max(0f, contentHeight - viewportHeight);
            float viewBottom = viewTop + viewportHeight;

            bool above = itemTopY < viewTop;
            bool below = itemBottomY > viewBottom;

            if (!(above || below)) return; // already fully visible

            float targetTop;
            if (above)
            {
                // Bring item top to view top
                targetTop = itemTopY;
            }
            else
            {
                // Bring item bottom to view bottom
                targetTop = itemBottomY - viewportHeight;
            }

            float maxScrollRange = Mathf.Max(0f, contentHeight - viewportHeight);
            float newNorm = (maxScrollRange <= 0f) ? 1f : 1f - Mathf.Clamp01(targetTop / maxScrollRange);

            scrollRect.verticalNormalizedPosition = newNorm;
        }

        /// <summary>
        /// Sets menu visibility via CanvasGroup.
        /// </summary>
        private void SetVisible(bool visible, bool instant = false)
        {
            if (!canvasGroup) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }
}
