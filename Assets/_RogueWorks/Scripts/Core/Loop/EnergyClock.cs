using RogueWorks.Core.Model;

namespace RogueWorks.Core.Loop
{
    /// <summary>
    /// Very small global clock: each tick grants energy; actors act when they reach cost.
    /// </summary>
    public sealed class EnergyClock
    {
        /// <summary>
        /// Advance time by one tick and grant energy to all actors.
        /// </summary>
        public void Tick(World world)
        {
            foreach (var a in world.Actors)
                a.GainEnergy();
        }
    }

}
