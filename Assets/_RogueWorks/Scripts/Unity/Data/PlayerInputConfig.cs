using UnityEngine;

namespace RogueWorks.Unity.Data
{
    /// <summary>
    /// Configuration asset for player input behavior and timing.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerInputConfig", menuName = "RogueWorks/Player Input Config")]
    public class PlayerInputConfig : ScriptableObject
    {
        [Header("Input Filtering")]
        [Range(0f, 0.9f)]
        [Tooltip("Minimum stick/axis magnitude required to count as input.")]
        public float deadZone = 0.25f;

        [Header("Movement Timing")]
        [Min(0f)]
        [Tooltip("Seconds before the second step can fire after the first immediate step.")]
        public float firstRepeatDelay = 0.2f;

        [Min(0f)]
        [Tooltip("Seconds between steps in steady hold.")]
        public float holdRepeatInterval = 0.2f;

        [Tooltip("If true, changing direction mid-hold does NOT grant an immediate extra step.")]
        public bool keepRepeatOnDirChange = true;

        [Min(0f)]
        [Tooltip("Extra delay after a direction change if keepRepeatOnDirChange is false.")]
        public float changeDelay = 0.0f;

        [Header("Diagonal Equalization")]
        [Tooltip("If true, diagonals use a longer repeat time so their effective speed matches cardinals.")]
        public bool diagonalEqualizationEnabled = true;

        [Min(1f)]
        [Tooltip("Delay scale for diagonal steps (~= sqrt(2) = 1.41421356).")]
        public float diagonalDelayScale = 1.41421356f;

        [Header("Skill Gating")]
        [Tooltip("If true, ignore skill presses while a blocking animation is playing.")]
        public bool ignoreSkillsWhileBlocked = true;

        [Tooltip("If true, after ignoring due to blocking, require RELEASE before next skill is accepted.")]
        public bool requireReleaseAfterBlock = true;

        [Header("Buffering Policy")]
        [Tooltip("If blocking is active and you are NOT ignoring skills, buffer the most recent discrete to fire later.")]
        public bool bufferDiscreteWhileBlocked = true;

        [Header("Skills")]
        [Tooltip("Skill id bound to the quick-cast input.")]
        public string quickSkillId = "";

        [Tooltip("If true, require a non-zero direction at press; otherwise use last facing (fallback Right).")]
        public bool requireDirectionOnQuick = false;

        [Tooltip("If true, holding the SkillMenu input opens a selector and suppresses movement/rotation.")]
        public bool holdOpensMenu = true;

        [Header("Action Map Names")]
        public string gameplayMapName = "Gameplay";
        public string uiMapName = "SkillUI";

        [Header("Gameplay Action Names")]
        public string moveActionName = "Move";
        public string turnActionName = "Turn";
        public string quickSkillActionName = "QuickSkill";
        public string skillMenuActionName = "SkillMenu";

        [Header("UI Action Names")]
        public string uiNavigateActionName = "Navigate";
        public string uiConfirmActionName = "Confirm";
        public string uiCancelActionName = "Cancel";
    }
}