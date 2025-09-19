using RogueWorks.Core.Primitives;

namespace RogueWorks.Core.Animation
{
    /// <summary>
    /// Reference to a target at a known grid position.
    /// </summary>
    public readonly struct TargetRef
    {
        /// <summary>Unique actor id.</summary>
        public int ActorId { get; }
        /// <summary>Target position in grid space.</summary>
        public GridPos Position { get; }

        /// <summary>Create a new target reference.</summary>
        public TargetRef(int actorId, GridPos position)
        {
            ActorId = actorId;
            Position = position;
        }

        /// <summary>Factory helper.</summary>
        public static TargetRef Of(int actorId, GridPos position) => new(actorId, position);
    }
}
