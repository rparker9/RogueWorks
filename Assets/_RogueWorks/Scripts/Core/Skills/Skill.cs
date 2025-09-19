// File: RogueWorks.Core/Skills/ISkillCatalog.cs
// Namespace: RogueWorks.Core.Skills
// Purpose: Core-side catalog so TurnEngine can resolve a skill id -> Skill (pure data).

using System;

namespace RogueWorks.Core.Skills
{
    public interface ISkillCatalog
    {
        /// <summary>Return true if a skill with this id exists.</summary>
        bool TryGet(string id, out Skill skill);
    }

    /// <summary>Minimal Core-side representation of a skill (no Unity types).</summary>
    [Serializable]
    public sealed class Skill
    {
        public string Id;
        public string DisplayName;
        public SkillType SkillType;     // Physical/Special/Status
        public DamageType DamageType;   // Fire/Ice/etc
        public SkillTargeting Targeting; // Line, TileInFront, Self, etc.
        public int Power;               // base power
        public int Accuracy;            // 0..100
        public int MaxRange;            // for Line/Arc
        public int ArcDegrees;          // for Arc
        public int RingRadius;          // for Ring
        public bool FriendlyOnly;
        public bool Piercing;
        public string StatusId;
        public int StatusChance;
    }

    public enum SkillType { Physical, Special, Status }
    public enum SkillTargeting { TileInFront, Line, Ring, Arc, CurrentTile, Self, Allies, RandomDirection, EntireRoom, EntireFloor, NoTarget }
    public enum DamageType { Normal, Fire, Ice, Electric, Wind, Holy, Dark }
}
