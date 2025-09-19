using RogueWorks.Core.Actions;

namespace RogueWorks.Core.Controllers
{
    /// <summary>
    /// Supplies the next intent for a given actor. Could be player input, AI, replay, etc.
    /// </summary>
    public interface IActorController
    {
        /// <summary>
        /// Ask the controller for an intent when it's this actor's turn.
        /// Return null to skip.
        /// </summary>
        ActionIntent? GetIntent();
    }
}
