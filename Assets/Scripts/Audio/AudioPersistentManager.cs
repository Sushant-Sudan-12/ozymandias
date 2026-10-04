using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KKK.Audio
{
    /// <summary>
    /// Enum identifying which music track category is currently active.
    /// </summary>
    public enum MusicTrackType
    {
        None,
        MainMenu,
        GameplayBg,
        Custom
    }

    /// <summary>
    /// Persistent Audio Manager (DontDestroyOnLoad) for music & sound design.
    /// - Handles Main Menu music (plays in MainMenu scene).
    /// - Handles Gameplay Background music (plays from the moment a new game starts and loops across all levels).
    /// - Provides Inspector volume sliders with real-time OnValidate live updates.
    /// - Dual AudioSource crossfading for smooth music transitions.
    /// - Automatic scene detection routing (seamlessly loops BG music across gameplay levels without restarting).
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class AudioPersistentManager : MonoBehaviour
    {
        private static AudioPersistentManager _instance;

        public static AudioPersistentManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<AudioPersistentManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("AudioPersistentManager");
                        _instance = go.AddComponent<AudioPersistentManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("--- Music Clips ---")]
        [Tooltip("Audio clip for Main Menu music (plays in MainMenu scene).")]
        [SerializeField] private AudioClip mainMenuMusic;

        [Tooltip("Audio clip for Background music (plays from start of new game/level and loops across all levels).")]
        [SerializeField] private AudioClip gameplayBgMusic;

        [Header("--- Volume Controls (Inspector Sliders) ---")]
        [Range(0f, 1f)]
        [Tooltip("Master Volume for all music (0 = Muted, 1 = Max). Drag slider to adjust in real-time.")]
        [SerializeField] private float masterMusicVolume = 1.0f;

        [Range(0f, 1f)]
        [Tooltip("Volume multiplier specifically for Main Menu music.")]
        [SerializeField] private float mainMenuVolume = 1.0f;

        [Range(0f, 1f)]
        [Tooltip("Volume multiplier specifically for Gameplay Background music.")]
        [SerializeField] private float bgMusicVolume = 1.0f;

        [Range(0f, 1f)]
        [Tooltip("Master Volume multiplier for Sound Effects (SFX).")]
        [SerializeField] private float sfxVolume = 1.0f;

        [Tooltip("Mute all music playback instantly.")]
        [SerializeField] private bool mute = false;

        [Header("--- Crossfade Settings ---")]
        [Range(0.1f, 5.0f)]
        [Tooltip("Duration in seconds for smooth crossfading between music tracks.")]
        [SerializeField] private float crossfadeDuration = 1.5f;

        [Header("--- Scene Routing ---")]
        [Tooltip("Name of the Main Menu scene. When loading this scene, Main Menu music will play automatically.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Tooltip("Automatically switch music based on scene load events.")]
        [SerializeField] private bool autoSceneRouting = true;

        // Internal Audio Sources for dual-channel crossfading & SFX
        private AudioSource _audioSourceA;
        private AudioSource _audioSourceB;
        private AudioSource _sfxAudioSource;
        private AudioSource _activeSource;
        private AudioSource _inactiveSource;

        private MusicTrackType _currentTrackType = MusicTrackType.None;
        private Coroutine _fadeCoroutine;

        // Public Properties & Events
        public float MasterMusicVolume
        {
            get => masterMusicVolume;
            set
            {
                masterMusicVolume = Mathf.Clamp01(value);
                ApplyVolumeToActiveSource();
            }
        }

        public float MainMenuVolume
        {
            get => mainMenuVolume;
            set
            {
                mainMenuVolume = Mathf.Clamp01(value);
                ApplyVolumeToActiveSource();
            }
        }

        public float BgMusicVolume
        {
            get => bgMusicVolume;
            set
            {
                bgMusicVolume = Mathf.Clamp01(value);
                ApplyVolumeToActiveSource();
            }
        }

        public float SFXVolume
        {
            get => sfxVolume;
            set => sfxVolume = Mathf.Clamp01(value);
        }

        public bool IsMuted
        {
            get => mute;
            set
            {
                mute = value;
                ApplyVolumeToActiveSource();
            }
        }

        public AudioClip MainMenuMusicClip
        {
            get => mainMenuMusic;
            set => mainMenuMusic = value;
        }

        public AudioClip GameplayBgMusicClip
        {
            get => gameplayBgMusic;
            set => gameplayBgMusic = value;
        }

        public MusicTrackType CurrentTrackType => _currentTrackType;
        public AudioClip CurrentClip => _activeSource != null ? _activeSource.clip : null;
        public bool IsPlaying => _activeSource != null && _activeSource.isPlaying;

        public static event Action<MusicTrackType> OnMusicTrackChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeAudioSources();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            if (autoSceneRouting)
            {
                EvaluateSceneMusic(SceneManager.GetActiveScene().name);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                _instance = null;
            }
        }

        /// <summary>
        /// Real-time live updating when adjusting sliders in the Unity Inspector during Play Mode or Edit Mode.
        /// </summary>
        private void OnValidate()
        {
            masterMusicVolume = Mathf.Clamp01(masterMusicVolume);
            mainMenuVolume = Mathf.Clamp01(mainMenuVolume);
            bgMusicVolume = Mathf.Clamp01(bgMusicVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);
            crossfadeDuration = Mathf.Max(0.05f, crossfadeDuration);

            if (Application.isPlaying && _activeSource != null)
            {
                ApplyVolumeToActiveSource();
            }
        }

        /// <summary>
        /// Initializes the dual AudioSources for crossfading and the SFX AudioSource.
        /// </summary>
        private void InitializeAudioSources()
        {
            if (_audioSourceA == null)
            {
                _audioSourceA = gameObject.AddComponent<AudioSource>();
                _audioSourceA.playOnAwake = false;
                _audioSourceA.loop = true;
                _audioSourceA.spatialBlend = 0f; // 2D Audio
                _audioSourceA.volume = 0f;
            }

            if (_audioSourceB == null)
            {
                _audioSourceB = gameObject.AddComponent<AudioSource>();
                _audioSourceB.playOnAwake = false;
                _audioSourceB.loop = true;
                _audioSourceB.spatialBlend = 0f; // 2D Audio
                _audioSourceB.volume = 0f;
            }

            if (_sfxAudioSource == null)
            {
                _sfxAudioSource = gameObject.AddComponent<AudioSource>();
                _sfxAudioSource.playOnAwake = false;
                _sfxAudioSource.loop = false;
                _sfxAudioSource.spatialBlend = 0f; // 2D Audio
                _sfxAudioSource.volume = sfxVolume;
            }

            _activeSource = _audioSourceA;
            _inactiveSource = _audioSourceB;
        }

        /// <summary>
        /// Triggered automatically whenever any scene finishes loading.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!autoSceneRouting) return;
            EvaluateSceneMusic(scene.name);
        }

        /// <summary>
        /// Determines and triggers the appropriate music for the specified scene.
        /// </summary>
        public void EvaluateSceneMusic(string sceneName)
        {
            bool isMainMenu = IsMainMenuScene(sceneName);

            if (isMainMenu)
            {
                // In MainMenu scene -> Play MainMenu music if not already playing
                if (_currentTrackType != MusicTrackType.MainMenu || !_activeSource.isPlaying)
                {
                    PlayMainMenuMusic(crossfade: true);
                }
            }
            else
            {
                // In Gameplay scene (e.g. Forest, Tut, Lvl1, Lvl2, Lvl3, etc.)
                // If GameplayBg music is ALREADY playing across levels, DO NOT interrupt or restart it!
                if (_currentTrackType == MusicTrackType.GameplayBg && _activeSource.isPlaying)
                {
                    // Keep playing seamlessly across all levels
                    return;
                }

                // If not playing GameplayBg music, start playing it smoothly
                PlayBackgroundMusic(crossfade: true);
            }
        }

        private bool IsMainMenuScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            return sceneName.Equals(mainMenuSceneName, StringComparison.OrdinalIgnoreCase) ||
                   sceneName.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #region Public Playback Controls

        /// <summary>
        /// Plays the Main Menu music track. Loops seamlessly.
        /// </summary>
        public void PlayMainMenuMusic(bool crossfade = true)
        {
            if (mainMenuMusic == null)
            {
                Debug.LogWarning("[AudioPersistentManager] MainMenu music clip is not assigned in the Inspector.", this);
                return;
            }

            PlayTrack(mainMenuMusic, MusicTrackType.MainMenu, mainMenuVolume, crossfade, loop: true);
        }

        /// <summary>
        /// Plays the Gameplay Background music track. Loops seamlessly across all gameplay levels.
        /// </summary>
        public void PlayBackgroundMusic(bool crossfade = true)
        {
            if (gameplayBgMusic == null)
            {
                Debug.LogWarning("[AudioPersistentManager] Gameplay Background music clip is not assigned in the Inspector.", this);
                return;
            }

            PlayTrack(gameplayBgMusic, MusicTrackType.GameplayBg, bgMusicVolume, crossfade, loop: true);
        }

        /// <summary>
        /// Plays a custom music track.
        /// </summary>
        public void PlayCustomMusic(AudioClip clip, float volumeMultiplier = 1.0f, bool crossfade = true, bool loop = true)
        {
            if (clip == null) return;
            PlayTrack(clip, MusicTrackType.Custom, volumeMultiplier, crossfade, loop);
        }

        /// <summary>
        /// Stops the currently playing music track with an optional fade-out.
        /// </summary>
        public void StopMusic(float fadeOutDuration = 1.0f)
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            if (fadeOutDuration > 0.01f && gameObject.activeInHierarchy)
            {
                _fadeCoroutine = StartCoroutine(FadeOutRoutine(_activeSource, fadeOutDuration));
            }
            else
            {
                if (_activeSource != null)
                {
                    _activeSource.Stop();
                    _activeSource.volume = 0f;
                }
                _currentTrackType = MusicTrackType.None;
            }
        }

        /// <summary>
        /// Pauses the active music track.
        /// </summary>
        public void PauseMusic()
        {
            if (_activeSource != null && _activeSource.isPlaying)
            {
                _activeSource.Pause();
            }
        }

        /// <summary>
        /// Resumes the paused music track.
        /// </summary>
        public void ResumeMusic()
        {
            if (_activeSource != null && !_activeSource.isPlaying)
            {
                _activeSource.UnPause();
            }
        }

        /// <summary>
        /// Plays a one-shot SFX sound effect using the persistent SFX channel.
        /// </summary>
        public void PlaySFX(AudioClip clip, float volumeScale = 1.0f)
        {
            if (clip == null) return;
            if (_sfxAudioSource == null) InitializeAudioSources();

            float finalVolume = sfxVolume * Mathf.Clamp01(volumeScale);
            _sfxAudioSource.PlayOneShot(clip, finalVolume);
        }

        #endregion

        #region Internal Playback & Crossfading

        private void PlayTrack(AudioClip clip, MusicTrackType trackType, float specificMultiplier, bool crossfade, bool loop)
        {
            if (clip == null) return;

            if (_activeSource == null)
            {
                InitializeAudioSources();
            }

            // If the requested clip is already playing on active source, simply update volume
            if (_activeSource.isPlaying && _activeSource.clip == clip)
            {
                _currentTrackType = trackType;
                ApplyVolumeToActiveSource();
                return;
            }

            _currentTrackType = trackType;
            float targetVolume = GetTargetVolumeForType(trackType, specificMultiplier);

            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            if (crossfade && gameObject.activeInHierarchy && (_activeSource.isPlaying || (_inactiveSource != null && _inactiveSource.isPlaying)))
            {
                // Swap active and inactive sources for smooth crossfading
                AudioSource incoming = _inactiveSource;
                AudioSource outgoing = _activeSource;

                incoming.clip = clip;
                incoming.loop = loop;
                incoming.time = 0f;
                incoming.volume = 0f;
                incoming.Play();

                _activeSource = incoming;
                _inactiveSource = outgoing;

                _fadeCoroutine = StartCoroutine(CrossfadeRoutine(outgoing, incoming, targetVolume, crossfadeDuration));
            }
            else
            {
                // Instant switch
                if (_inactiveSource != null && _inactiveSource.isPlaying)
                {
                    _inactiveSource.Stop();
                    _inactiveSource.volume = 0f;
                }

                _activeSource.clip = clip;
                _activeSource.loop = loop;
                _activeSource.time = 0f;
                _activeSource.volume = mute ? 0f : targetVolume;
                _activeSource.Play();
            }

            OnMusicTrackChanged?.Invoke(_currentTrackType);
        }

        private IEnumerator CrossfadeRoutine(AudioSource outgoing, AudioSource incoming, float targetVolume, float duration)
        {
            float startOutgoingVol = outgoing != null ? outgoing.volume : 0f;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(timer / duration);
                float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);

                if (outgoing != null && outgoing.isPlaying)
                {
                    outgoing.volume = Mathf.Lerp(startOutgoingVol, 0f, smoothProgress);
                }

                if (incoming != null)
                {
                    float effectiveTarget = mute ? 0f : targetVolume;
                    incoming.volume = Mathf.Lerp(0f, effectiveTarget, smoothProgress);
                }

                yield return null;
            }

            if (outgoing != null)
            {
                outgoing.Stop();
                outgoing.volume = 0f;
                outgoing.clip = null;
            }

            if (incoming != null)
            {
                incoming.volume = mute ? 0f : targetVolume;
            }

            _fadeCoroutine = null;
        }

        private IEnumerator FadeOutRoutine(AudioSource source, float duration)
        {
            if (source == null || !source.isPlaying)
            {
                _currentTrackType = MusicTrackType.None;
                yield break;
            }

            float startVol = source.volume;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(timer / duration);
                source.volume = Mathf.Lerp(startVol, 0f, progress);
                yield return null;
            }

            source.Stop();
            source.volume = 0f;
            source.clip = null;
            _currentTrackType = MusicTrackType.None;
            _fadeCoroutine = null;
        }

        private float GetTargetVolumeForType(MusicTrackType trackType, float specificMultiplier)
        {
            if (mute) return 0f;

            float typeMultiplier = 1.0f;
            switch (trackType)
            {
                case MusicTrackType.MainMenu:
                    typeMultiplier = mainMenuVolume;
                    break;
                case MusicTrackType.GameplayBg:
                    typeMultiplier = bgMusicVolume;
                    break;
                case MusicTrackType.Custom:
                    typeMultiplier = specificMultiplier;
                    break;
            }

            return Mathf.Clamp01(masterMusicVolume * typeMultiplier);
        }

        private void ApplyVolumeToActiveSource()
        {
            if (_activeSource == null) return;

            if (mute)
            {
                _activeSource.volume = 0f;
                return;
            }

            float targetVol = 0f;
            switch (_currentTrackType)
            {
                case MusicTrackType.MainMenu:
                    targetVol = masterMusicVolume * mainMenuVolume;
                    break;
                case MusicTrackType.GameplayBg:
                    targetVol = masterMusicVolume * bgMusicVolume;
                    break;
                default:
                    targetVol = masterMusicVolume;
                    break;
            }

            // Only apply directly if not currently in the middle of a crossfade
            if (_fadeCoroutine == null)
            {
                _activeSource.volume = Mathf.Clamp01(targetVol);
            }
        }

        #endregion
    }
}
