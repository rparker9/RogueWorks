using System.Collections.Generic;
using UnityEngine;

namespace RogueWorks.Unity.Data
{
    /// <summary>
    /// Scene bootstrap roster: which ActorDefinitions to spawn, where, and who is player-controlled.
    /// </summary>
    [CreateAssetMenu(fileName = "NewRoster", menuName = "RogueWorks/Spawn Roster", order = 1)]
    public sealed class RosterDefinition : ScriptableObject
    {
        [System.Serializable]
        public sealed class Entry
        {
            [Tooltip("Which actor definition to spawn.")]
            public ActorDefinition definition;

            [Tooltip("How many to spawn if Positions is empty.")]
            [Min(1)] public int count = 1;

            [Tooltip("Optional fixed grid positions (x,y in grid). If non-empty, one actor per position.")]
            public List<Vector2Int> positions = new();

            [Tooltip("If true, input will be bound to this spawned actor.")]
            public bool isPlayerControlled = false;
        }

        [Tooltip("All spawns in this roster.")]
        public List<Entry> entries = new();
    }
}
