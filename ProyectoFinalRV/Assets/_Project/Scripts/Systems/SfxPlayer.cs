using System;
using System.Collections.Generic;
using UnityEngine;

namespace QCXR
{
    /// Audio espacial 3D: reproduce cada sonido en la posición donde ocurrió el evento.
    public class SfxPlayer : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public SfxId id;
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 1f;
        }

        [SerializeField] Entry[] entries;

        readonly Dictionary<SfxId, Entry> map = new Dictionary<SfxId, Entry>();

        void Awake() { foreach (var e in entries) map[e.id] = e; }
        void OnEnable()  => GameEvents.SfxRequested += Play;
        void OnDisable() => GameEvents.SfxRequested -= Play;

        void Play(SfxId id, Vector3 position)
        {
            if (!map.TryGetValue(id, out var e) || e.clips == null || e.clips.Length == 0) return;
            var clip = e.clips[UnityEngine.Random.Range(0, e.clips.Length)];
            if (clip != null) AudioSource.PlayClipAtPoint(clip, position, e.volume);
        }
    }
}
