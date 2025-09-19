// File: Assets/RogueWorks.Unity/Data/VfxDatabaseSO.cs
// Purpose: Id -> VisualEffectAsset + projectile prefab lookup as a ScriptableObject.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

[CreateAssetMenu(fileName = "VfxDatabase", menuName = "RogueWorks/Databases/Vfx")]
public sealed class VfxDatabase : ScriptableObject
{
    [System.Serializable]
    public struct Effect { public string Id; public VisualEffectAsset Asset; }

    [SerializeField] private List<Effect> effects = new();
    [Header("Projectile Prefabs (id is prefab name)")]
    [SerializeField] private List<GameObject> projectilePrefabs = new();

    private Dictionary<string, VisualEffectAsset> _effects;
    private Dictionary<string, GameObject> _projectiles;

    public void InitializeLookup()
    {
        _effects = new Dictionary<string, VisualEffectAsset>(effects.Count);
        foreach (var e in effects)
            if (!string.IsNullOrWhiteSpace(e.Id) && e.Asset) _effects[e.Id] = e.Asset;

        _projectiles = new Dictionary<string, GameObject>(projectilePrefabs.Count);
        foreach (var p in projectilePrefabs)
            if (p) _projectiles[p.name] = p;
    }

    public VisualEffectAsset FindEffect(string id)
        => (id != null && _effects != null && _effects.TryGetValue(id, out var a)) ? a : null;

    public GameObject FindProjectile(string id)
        => (id != null && _projectiles != null && _projectiles.TryGetValue(id, out var go)) ? go : null;
}
