using System;
using UnityEngine;
using RogueWorks.Core.Primitives;

namespace RogueWorks.Unity 
{
    /// <summary>
    /// Maps grid positions to world positions based on origin and tile size.
    /// Simple and serializable for Unity inspector use.
    /// </summary>
    [Serializable]
    public sealed class GridToWorldMapper
    {
        [SerializeField] private Vector3 origin = Vector3.zero;
        [SerializeField] private float tileSize = 1f;

        /// <summary>
        /// Map a grid tile to world position (XZ plane).
        /// </summary>
        public Vector3 ToWorld(GridPos gp) => origin + new Vector3(gp.X * tileSize, 0f, gp.Y * tileSize);
    }
}

