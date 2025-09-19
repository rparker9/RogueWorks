using RogueWorks.Core.Primitives;

namespace RogueWorks.Core.Actions
{
    /// <summary>Kind of action requested.</summary>
    public enum IntentType 
    { 
        None, 
        Move, 
        Rotate, 
        Skill 
    }

    /// <summary>
    /// Intent produced by controllers. Immutable struct.
    /// </summary>
    public readonly struct ActionIntent
    {
        public IntentType Type { get; }
        public GridPos Direction { get; }
        public string SkillId { get; }   // only set when Type == Skill

        // Constructor for movement/rotation
        public ActionIntent(IntentType type, GridPos direction)
        {
            Type = type;
            Direction = direction;
            SkillId = null;
        }

        // Constructor for skill
        private ActionIntent(IntentType type, GridPos direction, string skillId)
        {
            Type = type;
            Direction = direction;
            SkillId = skillId;
        }

        /// <summary>Create a skill intent with an id and facing/dir.</summary>
        public static ActionIntent OfSkill(string skillId, GridPos dir)
        {
            return new ActionIntent(IntentType.Skill, dir, skillId);
        }
    }
}
