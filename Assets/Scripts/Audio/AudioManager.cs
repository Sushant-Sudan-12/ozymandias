using UnityEngine;

namespace KKK.Audio
{
    /// <summary>
    /// Static convenience wrapper / proxy for <see cref="AudioPersistentManager"/>.
    /// Allows easy global access via AudioManager.Instance or AudioManager.PlayMainMenuMusic(), etc.
    /// </summary>
    public static class AudioManager
    {
        public static AudioPersistentManager Instance => AudioPersistentManager.Instance;

        public static void PlayMainMenuMusic(bool crossfade = true)
        {
            if (AudioPersistentManager.Instance != null)
                AudioPersistentManager.Instance.PlayMainMenuMusic(crossfade);
        }

        public static void PlayBackgroundMusic(bool crossfade = true)
        {
            if (AudioPersistentManager.Instance != null)
                AudioPersistentManager.Instance.PlayBackgroundMusic(crossfade);
        }

        public static void PlaySFX(AudioClip clip, float volumeScale = 1.0f)
        {
            if (AudioPersistentManager.Instance != null)
                AudioPersistentManager.Instance.PlaySFX(clip, volumeScale);
        }

        public static void StopMusic(float fadeOutDuration = 1.0f)
        {
            if (AudioPersistentManager.Instance != null)
                AudioPersistentManager.Instance.StopMusic(fadeOutDuration);
        }

        public static void SetMusicVolume(float volume)
        {
            if (AudioPersistentManager.Instance != null)
                AudioPersistentManager.Instance.MasterMusicVolume = volume;
        }

        public static void SetSFXVolume(float volume)
        {
            if (AudioPersistentManager.Instance != null)
                AudioPersistentManager.Instance.SFXVolume = volume;
        }
    }
}
