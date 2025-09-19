using System;
using System.Collections;
using UnityEngine;

namespace RogueWorks.Unity.Animation.Sinks
{
    /// <summary>Contract for one-shot sound playback.</summary>
    public interface ISfxSink
    {
        /// <summary>Play a one-shot clip at optional world position. Yields for the audible duration.</summary>
        IEnumerator PlayOneShot(AudioClip clip, Vector3? worldPos = null, float volume = 1f, float pitch = 1f);
    }

    [Serializable]
    public sealed class SfxOneShotService : ISfxSink
    {
        [SerializeField] private int poolSize = 8;
        [SerializeField] private AudioSource audioPrefab;

        private AudioSource[] _pool;
        private int _next;
        private Transform _root;

        public void Initialize(Transform parent)
        {
            _root = parent;
            _pool = new AudioSource[Mathf.Max(1, poolSize)];
            for (int i = 0; i < _pool.Length; i++) _pool[i] = UnityEngine.Object.Instantiate(audioPrefab, _root);
        }

        public IEnumerator PlayOneShot(AudioClip clip, Vector3? worldPos = null, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || _pool == null) yield break;

            var src = _pool[_next];
            _next = (_next + 1) % _pool.Length;

            if (worldPos.HasValue) src.transform.position = worldPos.Value;

            src.pitch = pitch;
            src.PlayOneShot(clip, volume);

            yield return new WaitForSeconds(Mathf.Max(0.01f, clip.length / Mathf.Max(0.01f, pitch)));
        }
    }
}
