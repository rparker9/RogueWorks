using System;

namespace RogueWorks.Core.Primitives
{
    /// <summary>
    /// Immutable integer grid position. Core never uses Unity types.
    /// </summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        /// <summary>
        /// X tile coordinate.
        /// </summary>
        public int X { get; }
        
        /// <summary>
        /// Y tile coordinate.
        /// </summary>
        public int Y { get; }

        /// <summary>
        /// Create a new grid position.
        /// </summary>
        public GridPos(int x, int y) 
        { 
            X = x; 
            Y = y; 
        }

        /// <summary>
        /// Add two positions component-wise.
        /// </summary>
        public static GridPos operator +(GridPos a, GridPos b) => new(a.X + b.X, a.Y + b.Y);

        /// <summary>
        /// Equality check.
        /// </summary>
        public bool Equals(GridPos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPos gp && Equals(gp);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";

        public static readonly GridPos Up = new(0, 1);
        public static readonly GridPos Down = new(0, -1);
        public static readonly GridPos Left = new(-1, 0);
        public static readonly GridPos Right = new(1, 0);
        public static readonly GridPos Zero = new(0, 0);
    }
}
