using UnityEngine;

namespace RogueWorks.Core.Animation
{
    /// <summary>
    /// Family descriptor for VFX lookups (Unity maps ids to assets).
    /// </summary>
    public enum VfxFamily 
    { 
        None, 
        Projectile, 
        Beam, 
        AoE,    
        Impact, 
        Buff 
    }

    /// <summary>
    /// Semantic VFX hint: family + identifiers + Y offset.
    /// </summary>
    public sealed class VfxCue
    {
        /// <summary>VFX family that best matches the request.</summary>
        public VfxFamily Family { get; set; } = VfxFamily.None;

        /// <summary>ID used by Unity lookup for impact-style effects.</summary>
        public string ImpactId { get; set; }

        /// <summary>Optional projectile prefab id.</summary>
        public string ProjectileId { get; set; }

        /// <summary>Optional beam asset id.</summary>
        public string BeamId { get; set; }

        /// <summary>Vertical world offset in meters applied by Unity.</summary>
        public float YOffset { get; set; } = 0f;
    }
}
