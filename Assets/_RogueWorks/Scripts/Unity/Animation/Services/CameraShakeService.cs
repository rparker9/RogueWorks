using System.Collections;
using UnityEngine;

namespace RogueWorks.Unity.Animation.Sinks
{
    /// <summary>Contract for camera shake impulses.</summary>
    public interface ICameraShakeSink
    {
        /// <summary>Trigger shake with the given amplitude and duration.</summary>
        IEnumerator Shake(float amplitude, float seconds);
    }

    public sealed class CameraShakeService : ICameraShakeSink
    {
        [SerializeField] private Transform target; // camera root
        [SerializeField] private float falloff = 12f;

        private Vector3 _base;

        public void Initialize(Transform cameraRoot)
        {
            target = cameraRoot != null ? cameraRoot : Camera.main?.transform;
            if (target != null) _base = target.localPosition;
        }

        public IEnumerator Shake(float amplitude, float seconds)
        {
            if (target == null || seconds <= 0f || amplitude <= 0f) yield break;

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(amplitude, 0f, t / seconds);
                var off = new Vector3(Mathf.PerlinNoise(0, Time.time * falloff) - 0.5f,
                                      Mathf.PerlinNoise(1, Time.time * falloff) - 0.5f, 0f) * (a * 2f);
                target.localPosition = _base + off;
                yield return null;
            }
            target.localPosition = _base;
        }
    }
}
