using System.Collections.Generic;

using RogueWorks.Core.Primitives;

namespace RogueWorks.Core.Animation
{
    /// <summary>
    /// Categories of presentation. Unity enforces parallel/sequential policy.
    /// </summary>
    public enum AnimationType
    {
        None,
        Movement,
        Face,
        Skill,
        TakeDamage,
        Death,
        Vfx
    }

    /// <summary>
    /// Semantic request for a visual beat. Unity builds clip graphs from these.
    /// </summary>
    public sealed class AnimationRequest
    {
        /// <summary>
        /// High-level presentation category.
        /// </summary>
        public AnimationType Type { get; set; }

        /// <summary>
        /// Owning actor id.
        /// </summary>
        public int ActorId { get; set; }

        /// <summary>
        /// Grid start (if applicable).
        /// </summary>
        public GridPos FromPosition { get; set; }

        /// <summary>
        /// Grid end (if applicable).
        /// </summary>
        public GridPos ToPosition { get; set; }

        /// <summary>
        /// Direction the actor should face (used for <see cref="AnimationType.Face"/>).
        /// </summary>
        public GridPos FacingDirection { get; set; }

        /// <summary>
        /// Multi-target impacts (Unity sequences per-target bundles).
        /// </summary>
        public IReadOnlyList<TargetRef> Targets { get; set; }

        /// <summary>
        /// Optional VFX hints (ids only)
        /// </summary>
        public VfxCue Vfx { get; set; }

        /// <summary>
        /// When true, Unity waits before continuing (sequential beats).
        /// </summary>
        public bool IsBlocking { get; set; }

        /// <summary>
        /// Optional label for logs; Unity chooses timing.
        /// </summary>
        public string LogTextLabel { get; set; }

        /// <summary>
        /// Optional skill/ability id (for rule resolution).
        /// </summary>
        public string SkillId { get; set; }   // optional (for rule resolution)

        /// <summary>
        /// Optional tag for custom routing/handling (e.g. "HEAL", "CRIT", etc).
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// Create a non-visual, label-only request (never blocking).
        /// </summary>
        public static AnimationRequest Label(string text) =>
            new AnimationRequest
            {
                Type = AnimationType.None,
                LogTextLabel = text,
                IsBlocking = false
            };

        /// <summary>
        /// Create a movement request (non-blocking).
        /// </summary>
        public static AnimationRequest Movement(int actorId, GridPos from, GridPos to) =>
            new()
            {
                Type = AnimationType.Movement,
                ActorId = actorId,
                FromPosition = from,
                ToPosition = to,
                IsBlocking = false
            };

        /// <summary>
        /// Create an skill request (blocking) with optional targets.
        /// </summary>
        public static AnimationRequest Skill(
            int actorId, GridPos from, GridPos to, TargetRef[] targets,
            string label = null, string skillId = null, string tag = null) =>
            new()
            {
                Type = AnimationType.Skill,
                ActorId = actorId,
                FromPosition = from,
                ToPosition = to,
                Targets = targets,
                IsBlocking = true,
                LogTextLabel = label,
                SkillId = skillId,
                Tag = tag,
                Vfx = null // let profile defaults apply when null, or keep if you want a specific cue
            };

        /// <summary>
        /// Create a non-blocking face/rotate request.
        /// </summary>
        public static AnimationRequest Face(int actorId, GridPos direction) =>
            new()
            {
                Type = AnimationType.Face,
                ActorId = actorId,
                FacingDirection = direction,
                IsBlocking = false
            };
    }
}
