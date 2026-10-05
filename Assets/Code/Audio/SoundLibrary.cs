using System;
using System.Collections.Generic;
using UnityEngine;

namespace Windy.Srpg.Game.Audio
{
    public enum SoundCue
    {
        Attack,
        CriticalHit,
        Skill,
        Heal,
        UnitDefeated
    }

    [Serializable]
    public sealed class SoundCueEntry
    {
        public SoundCue Cue;
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume = 1f;
    }

    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "TBS/Audio/Sound Library")]
    public sealed class SoundLibrary : ScriptableObject
    {
        [SerializeField] private List<SoundCueEntry> cues = new List<SoundCueEntry>();

        public bool TryGet(SoundCue cue, out AudioClip clip, out float volume)
        {
            foreach (SoundCueEntry entry in cues ?? new List<SoundCueEntry>())
            {
                if (entry == null || entry.Cue != cue || entry.Clip == null) continue;
                clip = entry.Clip;
                volume = Mathf.Clamp01(entry.Volume);
                return true;
            }

            clip = null;
            volume = 0f;
            return false;
        }
    }
}
