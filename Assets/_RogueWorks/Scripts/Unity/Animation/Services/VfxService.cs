// File: Assets/RogueWorks.Unity/Animation/Sinks/VfxService.cs
// Purpose: VFX Graph spawning and lifetime with a tiny pooled wrapper.

using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

namespace RogueWorks.Unity.Animation.Sinks
{
    public interface IVfxSink
    {
        /// <summary>Play a one-shot VFX asset at a position with optional Y offset.</summary>
        IEnumerator PlayOneShot(VisualEffectAsset asset, Vector3 position, float yOffset = 0f);

        /// <summary>Play a beam between endpoints and hold briefly.</summary>
        IEnumerator PlayBeam(VisualEffectAsset asset, Vector3 from, Vector3 to, float yOffset = 0f, float minSeconds = 0.08f);

        /// <summary>Launch a projectile prefab from->to and wait until its first impact.</summary>
        IEnumerator PlayProjectile(GameObject projectilePrefab, Vector3 from, Vector3 to, float yOffset = 0f);
    }

    [System.Serializable]
    public sealed class VfxService : IVfxSink
    {
        [Header("Pooling")]
        [SerializeField] private int poolSize = 12;

        [Tooltip("Prefab with a VisualEffect component as the root (wrapper object).")]
        [SerializeField] private GameObject vfxWrapperPrefab;

        private VisualEffect[] _pool;
        private int _next;
        private Transform _root;
        private MonoBehaviour _runner;

        /// <summary>Prepare pool and keep a runner for coroutines.</summary>
        public void Initialize(Transform parent, MonoBehaviour coroutineRunner)
        {
            _root = parent;
            _runner = coroutineRunner;

            var size = Mathf.Max(1, poolSize);
            _pool = new VisualEffect[size];
            for (int i = 0; i < size; i++)
            {
                var go = Object.Instantiate(vfxWrapperPrefab, _root);
                go.SetActive(false);
                _pool[i] = go.GetComponent<VisualEffect>();
            }
            _next = 0;
        }

        /// <summary>Rent the next pooled VisualEffect (round-robin).</summary>
        private VisualEffect Rent()
        {
            var v = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            return v;
        }

        /// <summary>Stop+hide and return the VisualEffect to the pool.</summary>
        private static void Return(VisualEffect v)
        {
            if (!v) return;
            v.Stop();
            v.gameObject.SetActive(false);
        }

        public IEnumerator PlayOneShot(VisualEffectAsset asset, Vector3 position, float yOffset = 0f)
        {
            if (asset == null || _pool == null || _pool.Length == 0) yield break;

            var v = Rent();
            if (!v) yield break;

            // Position with Y-offset applied.
            var p = position; p.y += yOffset;
            v.transform.position = p;

            v.visualEffectAsset = asset;
            v.gameObject.SetActive(true);
            v.Reinit();
            v.Play();

            // Let spawners tick at least one frame to create particles.
            yield return null;

            const float minSeconds = 0.08f;
            float t = 0f;
            // Wait either for minimum time or until all particles die.
            while (t < minSeconds || v.aliveParticleCount > 0)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Return(v);
        }

        public IEnumerator PlayBeam(VisualEffectAsset asset, Vector3 from, Vector3 to, float yOffset = 0f, float minSeconds = 0.08f)
        {
            if (asset == null || _pool == null || _pool.Length == 0) yield break;

            var v = Rent();
            if (!v) yield break;

            // Place at start; encode direction/length via exposed properties.
            var start = from + Vector3.up * yOffset;
            v.transform.position = start;

            var dir = to - from;
            v.visualEffectAsset = asset;
            v.gameObject.SetActive(true);
            v.Reinit();
            v.SetVector3("BeamDirection", dir.normalized);
            v.SetFloat("BeamLength", dir.magnitude);
            v.Play();

            yield return new WaitForSeconds(Mathf.Max(0f, minSeconds));

            Return(v);
        }

        public IEnumerator PlayProjectile(GameObject projectilePrefab, Vector3 from, Vector3 to, float yOffset = 0f)
        {
            if (!projectilePrefab) yield break;

            // Keep it lightweight; projectile prefab may contain its own VFX.
            var spawn = from + Vector3.up * yOffset;
            var target = to + Vector3.up * yOffset;

            var go = Object.Instantiate(projectilePrefab, spawn, Quaternion.LookRotation((target - spawn).normalized), _root);

            // Cheap mover (you can replace with a custom component or curve).
            const float speed = 12f;
            while ((go.transform.position - target).sqrMagnitude > 0.0025f)
            {
                go.transform.position = Vector3.MoveTowards(go.transform.position, target, speed * Time.deltaTime);
                yield return null;
            }

            Object.Destroy(go);
        }
    }
}
