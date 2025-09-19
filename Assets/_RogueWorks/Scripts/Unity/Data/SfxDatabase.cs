// File: Assets/RogueWorks.Unity/Data/SfxDatabaseSO.cs
// Purpose: Id -> AudioClip lookup as a ScriptableObject.

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SfxDatabase", menuName = "RogueWorks/Databases/Sfx")]
public sealed class SfxDatabase : ScriptableObject
{
    [System.Serializable]
    public struct Entry { public string Id; public AudioClip Clip; }

    [SerializeField] private List<Entry> clips = new();

    private Dictionary<string, AudioClip> _map;

    /// <summary>Build fast lookup (called by GameRuntime during Awake).</summary>
    public void InitializeLookup()
    {
        _map = new Dictionary<string, AudioClip>(clips.Count);
        foreach (var e in clips)
            if (!string.IsNullOrWhiteSpace(e.Id) && e.Clip) _map[e.Id] = e.Clip;
    }

    /// <summary>Find an audio clip by id, or null.</summary>
    public AudioClip FindClip(string id)
        => (id != null && _map != null && _map.TryGetValue(id, out var c)) ? c : null;
}
