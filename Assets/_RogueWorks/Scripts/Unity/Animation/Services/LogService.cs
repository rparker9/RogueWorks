using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace RogueWorks.Unity.Animation.Sinks
{
    /// <summary>Contract for the on-screen rolling log.</summary>
    public interface ILogSink
    {
        /// <summary>Append a line and optionally animate it in.</summary>
        IEnumerator Write(string text);
    }

    [Serializable]
    public sealed class LogService : ILogSink
    {
        [Tooltip("Newest at index 0")]
        [SerializeField] private TextMeshProUGUI[] slots = Array.Empty<TextMeshProUGUI>();
        [SerializeField] private float revealDelay = 0.08f;

        /// <summary>
        /// This function writes a new line of text to the log service.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public IEnumerator Write(string text)
        {
            if (slots == null || slots.Length == 0) 
                yield break;
            for (int i = slots.Length - 1; i >= 1; i--) 
                slots[i].text = slots[i - 1].text;
            slots[0].text = text ?? string.Empty;
            yield return new WaitForSeconds(revealDelay);
        }
    }
}
