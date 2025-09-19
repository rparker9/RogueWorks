using System.Collections.Generic;

using RogueWorks.Core.Animation;

namespace RogueWorks.Core.Actions
{
    /// <summary>
    /// Outcome of a single action: any emitted animation requests (semantic) and a log message.
    /// </summary>
    public sealed class ActionOutcome
    {
        /// <summary>
        /// True if something meaningful happened (used by consume-turn rules).
        /// </summary>
        public bool ConsumedTurn { get; set; }

        /// <summary>
        /// Optional text for the game log (Unity displays it in sync with animations).
        /// </summary>
        public string LogText { get; set; }

        /// <summary>
        /// Semantic animation requests produced by this action.
        /// </summary>
        public List<AnimationRequest> Animations { get; } = new();

        /// <summary>
        /// Add a request and return this (fluent).
        /// </summary>
        public ActionOutcome Add(AnimationRequest rq) { Animations.Add(rq); return this; }
    }
}
