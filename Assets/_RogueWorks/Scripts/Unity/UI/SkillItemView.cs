using RogueWorks.Unity.Data;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueWorks.Unity.UI
{
    /// <summary>
    /// Displays a single skill row/tile in the skill menu.
    /// Supports binding from either a ScriptableObject (SkillDefinition)
    /// or a lightweight (displayName, icon) pair.
    /// </summary>
    public class SkillItemView : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;
        [SerializeField] private GameObject selectedHighlight;

        /// <summary>
        /// Optional external click handler (wired by controller).
        /// </summary>
        public Action onClick;

        /// <summary>
        /// Bind from a ScriptableObject definition (editor-friendly path).
        /// </summary>
        /// <param name="def">SkillDefinition asset (may be null).</param>
        public void Bind(SkillDefinition def)
        {
            // Gracefully handle null asset
            var displayName = def != null && !string.IsNullOrWhiteSpace(def.displayName) ? def.displayName : "—";
            var sprite = def != null ? def.icon : null;

            Apply(displayName, sprite);
        }

        /// <summary>
        /// Bind from a lightweight runtime model (Core-driven path).
        /// </summary>
        /// <param name="displayName">Display name to show (falls back to "—" if null/empty).</param>
        /// <param name="sprite">Optional icon sprite.</param>
        public void Bind(string displayName, Sprite sprite)
        {
            Apply(displayName, sprite);
        }

        /// <summary>
        /// Sets the selected visual state for this item.
        /// </summary>
        /// <param name="on">True to highlight as selected.</param>
        public void SetSelected(bool on)
        {
            if (selectedHighlight) selectedHighlight.SetActive(on);
        }

        /// <summary>
        /// (Optional) Wire a Unity UI Button to this from the prefab.
        /// </summary>
        public void OnButtonClicked() => onClick?.Invoke();

        // ------------------------------------------------------------------
        // Internal helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Applies display name and icon to the UI with robust null-safety.
        /// </summary>
        /// <param name="displayName">Name to display; coerced to "—" if blank.</param>
        /// <param name="sprite">Icon sprite (nullable).</param>
        private void Apply(string displayName, Sprite sprite)
        {
            // Coerce display text
            var text = string.IsNullOrWhiteSpace(displayName) ? "—" : displayName;

            // Update label
            if (label != null)
            {
                label.text = text;
                // Optional: ensure label is enabled for rows without icons as well
                label.enabled = true;
            }

            // Update icon
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null; // hide image when no sprite provided
            }

            // Reset selection visual on bind
            SetSelected(false);
        }
    }
}
