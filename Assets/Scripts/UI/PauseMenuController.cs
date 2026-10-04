using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KKK.UI
{
    /// <summary>
    /// Persistent UI Manager (DontDestroyOnLoad).
    /// - Manages the Pause Menu (Continue, Restart, MainMenu).
    /// - Manages the persistent FadeScreen for smooth 2-second scene transitions and enemy detection fading.
    /// - Sets Canvas sortingOrder to 1000 so UI and FadeScreen always render on top.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseMenuController : MonoBehaviour
    {
        public static PauseMenuController Instance { get; private set; }

        [Header("Canvas & Layering")]
        [Tooltip("The Canvas holding the Pause Menu and FadeScreen.")]
        [SerializeField] private Canvas pauseCanvas;

        [Tooltip("High sorting order ensuring UI and FadeScreen render in front of game elements.")]
        [SerializeField] private int pauseCanvasSortingOrder = 1000;

        [Header("UI Panel Reference")]
        [Tooltip("The root GameObject of the Pause Menu popup panel.")]
        [SerializeField] private GameObject pauseMenuPanel;

        [Header("Buttons (3 Options)")]
        [SerializeField] private Button continueButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Configuration")]
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Persistent Fade Screen & Scene Transition")]
        [Tooltip("CanvasGroup on the FadeScreen overlay. Used for scene transitions and enemy detection fade.")]
        [SerializeField] private CanvasGroup fadeScreenCanvasGroup;

        [Tooltip("Optional Image component on the FadeScreen overlay.")]
        [SerializeField] private Image fadeScreenImage;

        [Tooltip("Default duration for scene transition fade (in seconds).")]
        [SerializeField] private float defaultTransitionDuration = 2.0f;

        [Tooltip("Whether to automatically fade in from black whenever any scene loads.")]
        [SerializeField] private bool autoFadeInOnSceneLoad = true;

        // Runtime state
        private bool _isPaused = false;
        private bool _isTransitioning = false;
        private Coroutine _fadeCoroutine;
        private EventSystem _eventSystem;

        public bool IsPaused => _isPaused;
        public bool IsTransitioning => _isTransitioning;
        public CanvasGroup FadeCanvasGroup => fadeScreenCanvasGroup;
        public Image FadeImage => fadeScreenImage;

        public static event Action<bool> OnPauseStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureCanvasSorting();
            EnsureEventSystem();
            ResolveFadeScreenComponents();

            if (pauseMenuPanel != null)
            {
                pauseMenuPanel.SetActive(false);
            }

            BindButtonListeners();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            EnsureCanvasSorting();
            EnsureEventSystem();
            ResolveFadeScreenComponents();
            UpdateCursorState();

            if (autoFadeInOnSceneLoad)
            {
                FadeIn(defaultTransitionDuration);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;

                if (_isPaused)
                {
                    Time.timeScale = 1f;
                }

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Update()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene.Equals(mainMenuSceneName, StringComparison.OrdinalIgnoreCase) ||
                currentScene.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (_isPaused) SetPaused(false);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (Input.GetKeyDown(pauseKey))
            {
                TogglePause();
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureCanvasSorting();
            EnsureEventSystem();
            ResolveFadeScreenComponents();

            SetPaused(false);
            UpdateCursorState();

            if (autoFadeInOnSceneLoad)
            {
                FadeIn(defaultTransitionDuration);
            }
        }

        private void ResolveFadeScreenComponents()
        {
            if (fadeScreenCanvasGroup == null)
            {
                var cgList = GetComponentsInChildren<CanvasGroup>(true);
                for (int i = 0; i < cgList.Length; i++)
                {
                    if (cgList[i].gameObject.name.IndexOf("fade", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        fadeScreenCanvasGroup = cgList[i];
                        break;
                    }
                }
            }

            if (fadeScreenImage == null)
            {
                if (fadeScreenCanvasGroup != null)
                {
                    fadeScreenImage = fadeScreenCanvasGroup.GetComponent<Image>();
                }
                else
                {
                    var imgList = GetComponentsInChildren<Image>(true);
                    for (int i = 0; i < imgList.Length; i++)
                    {
                        if (imgList[i].gameObject.name.IndexOf("fade", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            fadeScreenImage = imgList[i];
                            break;
                        }
                    }
                }
            }

            // Ensure the FadeScreen Image is a solid flat color (NO rounded corner sprites)
            if (fadeScreenImage != null)
            {
                fadeScreenImage.sprite = null;
                fadeScreenImage.type = Image.Type.Simple;
                fadeScreenImage.color = new Color(0f, 0f, 0f, fadeScreenImage.color.a);
                fadeScreenImage.raycastTarget = false;

                // Stretch RectTransform with generous overscan (+200px) to guarantee full screen on any resolution
                RectTransform rt = fadeScreenImage.rectTransform;
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.offsetMin = new Vector2(-200f, -200f);
                    rt.offsetMax = new Vector2(200f, 200f);
                }
            }

            if (fadeScreenCanvasGroup != null)
            {
                fadeScreenCanvasGroup.blocksRaycasts = false;
                fadeScreenCanvasGroup.interactable = false;

                RectTransform rt = fadeScreenCanvasGroup.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.offsetMin = new Vector2(-200f, -200f);
                    rt.offsetMax = new Vector2(200f, 200f);
                }
            }
        }

        public void EnsureCanvasSorting()
        {
            if (pauseCanvas == null)
            {
                pauseCanvas = GetComponentInChildren<Canvas>(true);
                if (pauseCanvas == null)
                {
                    pauseCanvas = GetComponentInParent<Canvas>();
                }
            }

            if (pauseCanvas != null)
            {
                pauseCanvas.overrideSorting = true;
                pauseCanvas.sortingOrder = pauseCanvasSortingOrder;

                if (pauseCanvas.GetComponent<GraphicRaycaster>() == null)
                {
                    pauseCanvas.gameObject.AddComponent<GraphicRaycaster>();
                }
            }
        }

        private void EnsureEventSystem()
        {
            if (_eventSystem == null)
            {
                _eventSystem = GetComponentInChildren<EventSystem>(true);
            }

            if (_eventSystem != null && !_eventSystem.gameObject.activeSelf)
            {
                _eventSystem.gameObject.SetActive(true);
            }
        }

        private void BindButtonListeners()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(ResumeGame);
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(RestartLevel);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveAllListeners();
                mainMenuButton.onClick.AddListener(LoadMainMenu);
            }
        }

        public void TogglePause()
        {
            SetPaused(!_isPaused);
        }

        public void ResumeGame()
        {
            SetPaused(false);
        }

        public void RestartLevel()
        {
            SetPaused(false);
            Scene activeScene = SceneManager.GetActiveScene();
            LoadSceneWithFade(activeScene.name, defaultTransitionDuration);
        }

        public void LoadMainMenu()
        {
            SetPaused(false);
            LoadSceneWithFade(mainMenuSceneName, defaultTransitionDuration);
        }

        public void SetPaused(bool paused)
        {
            _isPaused = paused;
            Time.timeScale = _isPaused ? 0f : 1f;

            if (_isPaused)
            {
                EnsureCanvasSorting();
                EnsureEventSystem();
            }

            UpdateCursorState();

            if (pauseMenuPanel != null)
            {
                if (_isPaused)
                {
                    pauseMenuPanel.transform.SetAsLastSibling();
                }
                pauseMenuPanel.SetActive(_isPaused);
            }

            OnPauseStateChanged?.Invoke(_isPaused);
        }

        public void UpdateCursorState()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            bool isMainMenu = currentScene.Equals(mainMenuSceneName, StringComparison.OrdinalIgnoreCase) ||
                              currentScene.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isMainMenu || _isPaused)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        #region Fade & Scene Transition System
        /// <summary>
        /// Applies detection alpha from enemy detection to the shared FadeScreen.
        /// Ignored only if a scene transition fade is currently loading another scene.
        /// </summary>
        public static void SetDetectionFade(float alpha)
        {
            if (Instance == null) return;
            if (Instance._isTransitioning) return;

            // If an enemy spots the player, stop any non-essential idle fade coroutine
            if (alpha > 0.001f && Instance._fadeCoroutine != null && !Instance._isTransitioning)
            {
                Instance.StopCoroutine(Instance._fadeCoroutine);
                Instance._fadeCoroutine = null;
            }

            Instance.ApplyAlphaDirect(alpha);
        }

        private void ApplyAlphaDirect(float alpha)
        {
            bool isVisible = alpha > 0.001f;

            if (fadeScreenCanvasGroup != null)
            {
                if (fadeScreenCanvasGroup.gameObject.activeSelf != isVisible)
                {
                    fadeScreenCanvasGroup.gameObject.SetActive(isVisible);
                }

                fadeScreenCanvasGroup.alpha = alpha;
                fadeScreenCanvasGroup.blocksRaycasts = (alpha > 0.1f);
                fadeScreenCanvasGroup.interactable = false;
            }

            if (fadeScreenImage != null)
            {
                if (fadeScreenCanvasGroup == null && fadeScreenImage.gameObject.activeSelf != isVisible)
                {
                    fadeScreenImage.gameObject.SetActive(isVisible);
                }

                Color c = fadeScreenImage.color;
                c.r = 0f;
                c.g = 0f;
                c.b = 0f;
                c.a = alpha;
                fadeScreenImage.color = c;
                fadeScreenImage.raycastTarget = false;
            }
        }

        /// <summary>
        /// Loads a target scene smoothly with a fade out then fade in.
        /// </summary>
        public static void LoadSceneWithFade(string sceneName, float duration = 2.0f, Action onComplete = null)
        {
            if (Instance != null)
            {
                Instance.StartSceneFadeTransition(sceneName, duration, onComplete);
            }
            else
            {
                SceneManager.LoadScene(sceneName);
                onComplete?.Invoke();
            }
        }

        public void StartSceneFadeTransition(string sceneName, float duration, Action onComplete = null)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(SceneTransitionRoutine(sceneName, duration, onComplete));
        }

        private IEnumerator SceneTransitionRoutine(string sceneName, float duration, Action onComplete)
        {
            _isTransitioning = true;
            float halfDuration = Mathf.Max(0.01f, duration * 0.5f);

            // 1. Fade Out to black
            yield return FadeRoutine(0f, 1f, halfDuration);

            // 2. Load Scene
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            while (op != null && !op.isDone)
            {
                yield return null;
            }

            onComplete?.Invoke();

            // 3. Fade In from black
            yield return FadeRoutine(1f, 0f, halfDuration);
            _isTransitioning = false;
            _fadeCoroutine = null;
        }

        /// <summary>
        /// Fades screen from black to transparent (1 -> 0).
        /// </summary>
        public void FadeIn(float duration)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeInRoutine(duration));
        }

        private IEnumerator FadeInRoutine(float duration)
        {
            _isTransitioning = true;
            yield return FadeRoutine(1f, 0f, duration);
            _isTransitioning = false;
            _fadeCoroutine = null;
        }

        /// <summary>
        /// Fades screen from transparent to black (0 -> 1).
        /// </summary>
        public void FadeOut(float duration, Action onComplete = null)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOutAndCallback(duration, onComplete));
        }

        private IEnumerator FadeOutAndCallback(float duration, Action onComplete)
        {
            _isTransitioning = true;
            yield return FadeRoutine(0f, 1f, duration);
            onComplete?.Invoke();
            _isTransitioning = false;
            _fadeCoroutine = null;
        }

        private IEnumerator FadeRoutine(float startAlpha, float targetAlpha, float duration)
        {
            float elapsed = 0f;
            ApplyAlphaDirect(startAlpha);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                ApplyAlphaDirect(alpha);
                yield return null;
            }

            ApplyAlphaDirect(targetAlpha);
        }
        #endregion
    }
}
