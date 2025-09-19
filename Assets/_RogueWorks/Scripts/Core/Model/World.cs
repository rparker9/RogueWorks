using RogueWorks.Core.Primitives;
using System.Collections.Generic;
using System.Linq;

namespace RogueWorks.Core.Model
{
    /// <summary>
    /// Minimal world: actor registry + tile blocking. Extend with map data later.
    /// </summary>
    public sealed class World
    {
        private readonly Dictionary<int, Actor> _actors = new();
        private int _nextActorId = 1;

        public int Width { get; }
        public int Height { get; }

        /// <summary>
        /// Create a new empty world with optional bounds.
        /// </summary>
        public World(int width = 64, int height = 64) { Width = width; Height = height; }

        /// <summary>
        /// Add a new actor and return it.
        /// </summary>
        public Actor AddActor(string name, TeamId team, GridPos position, int maxEnergy)
        {
            var a = new Actor(_nextActorId++, name, team, position, maxEnergy);
            _actors.Add(a.Id, a);
            
            return a;
        }

        /// <summary>
        /// Try get actor by id.
        /// </summary>
        public bool TryGetActor(int id, out Actor a) => _actors.TryGetValue(id, out a);

        /// <summary>
        /// All actors snapshot.
        /// </summary>
        public IEnumerable<Actor> Actors => _actors.Values;

        /// <summary>
        /// True if cell is inside bounds and no actor occupies it.
        /// </summary>
        public bool IsWalkable(GridPos p) => InInBounds(p) && !_actors.Values.Any(a => a.Position.Equals(p));

        /// <summary>
        /// Bounds check.
        /// </summary>
        public bool InInBounds(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        /// <summary>
        /// Enumerate enemies in a single tile (used by melee).
        /// </summary>
        public IEnumerable<Actor> ActorsAt(GridPos p) => _actors.Values.Where(a => a.Position.Equals(p));
    }
}
