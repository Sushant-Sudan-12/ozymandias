using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using KKK.Audio;

namespace KKK.UI
{
    /// <summary>
    /// Manages the MainMenu canvas with 3 buttons: Continue, New Game, and Quit.
    /// - Initially only 'New Game' and 'Quit' are shown.
    /// - When returning from any scene, 'Continue' is revealed to resume where the player left off.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene Configuration")]
        [Tooltip("First scene to load when clicking 'New Game'.")]
        [SerializeField] private string firstGameSceneName = "Forest";

        [Header("UI Buttons (3 Options)")]
        [Tooltip("Button to resume the last visited level. Hidden on first start.")]
        [SerializeField] private Button continueButton;

        [Tooltip("Button to start a fresh game from the Forest scene.")]
        [SerializeField] private Button newGameButton;

        [Tooltip("Button to exit the application.")]
        [SerializeField] private Button quitButton;

        [Header("Audio (Optional)")]
        [SerializeField] private AudioClip buttonClickAudio;
        [SerializeField] private AudioSource audioSource;

        private bool _isLoading = false;

        private void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            EnsureEventSystem();

            if (newGameButton == null || quitButton == null)
            {
                BuildMainMenuUI();
            }
            else
            {
                BindButtonListeners();
            }
        }

        private void Start()
        {
            RefreshContinueButtonVisibility();
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject eventObj = new GameObject("EventSystem");
                eventObj.AddComponent<EventSystem>();
                eventObj.AddComponent<StandaloneInputModule>();
            }
        }

        /// <summary>
        /// Updates the Continue button visibility based on whether saved progress exists.
        /// </summary>
        public void RefreshContinueButtonVisibility()
        {
            bool hasSave = GameProgressTracker.HasSavedGame();

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(hasSave);
            }
        }

        private void BindButtonListeners()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(ContinueGame);
            }

            if (newGameButton != null)
            {
                newGameButton.onClick.RemoveAllListeners();
                newGameButton.onClick.AddListener(NewGame);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(QuitGame);
            }
        }

        /// <summary>
        /// Continues the game by loading the last visited level.
        /// </summary>
        public void ContinueGame()
        {
            if (_isLoading) return;
            _isLoading = true;

            string targetScene = GameProgressTracker.GetLastPlayedScene();
            PlayClickSound();

            if (PauseMenuController.Instance != null)
            {
                PauseMenuController.LoadSceneWithFade(targetScene, 2.0f);
            }
            else
            {
                StartCoroutine(LoadSceneRoutine(targetScene));
            }
        }

        /// <summary>
        /// Starts a new game from the beginning (Forest scene).
        /// </summary>
        public void NewGame()
        {
            if (_isLoading) return;
            _isLoading = true;

            // Reset dialogue history so opening dialogues play for the new adventure
            SceneDialogueController.ResetDialogueHistory();

            // Save Forest as the starting scene
            GameProgressTracker.SaveScene(firstGameSceneName);

            PlayClickSound();

            if (PauseMenuController.Instance != null)
            {
                PauseMenuController.LoadSceneWithFade(firstGameSceneName, 2.0f);
            }
            else
            {
                StartCoroutine(LoadSceneRoutine(firstGameSceneName));
            }
        }

        /// <summary>
        /// Exits the application.
        /// </summary>
        public void QuitGame()
        {
            PlayClickSound();
            Debug.Log("[MainMenuController] Quitting Application...");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void PlayClickSound()
        {
            if (buttonClickAudio != null)
            {
                if (audioSource != null)
                {
                    audioSource.PlayOneShot(buttonClickAudio);
                }
                else if (AudioPersistentManager.Instance != null)
                {
                    AudioPersistentManager.Instance.PlaySFX(buttonClickAudio);
                }
            }
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            yield return new WaitForSeconds(0.2f);
            SceneManager.LoadScene(sceneName);
        }

        #region Procedural UI Hierarchy Builder
        private void BuildMainMenuUI()
        {
            // Canvas
            GameObject canvasObj = new GameObject("MainMenuCanvas");
            canvasObj.transform.SetParent(transform, false);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Background
            GameObject bgObj = new GameObject("Background", typeof(RectTransform));
            bgObj.transform.SetParent(canvasObj.transform, false);
            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.02f, 0.03f, 0.06f, 0.96f);

            // Title Container
            GameObject titleObj = new GameObject("TitleHeader", typeof(RectTransform));
            titleObj.transform.SetParent(canvasObj.transform, false);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 0.74f);
            titleRt.anchorMax = new Vector2(0.5f, 0.74f);
            titleRt.pivot = new Vector2(0.5f, 0.5f);
            titleRt.sizeDelta = new Vector2(900f, 150f);

            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = font;
            titleText.text = "KILL KULT KONTROL";
            titleText.fontSize = 56;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.2f, 1f, 0.75f);

            Outline titleOutline = titleObj.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0f, 0.4f, 0.3f, 0.8f);
            titleOutline.effectDistance = new Vector2(3, -3);

            // Subtitle
            GameObject subtitleObj = new GameObject("Subtitle", typeof(RectTransform));
            subtitleObj.transform.SetParent(titleObj.transform, false);
            RectTransform subRt = subtitleObj.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.5f, 0f);
            subRt.anchorMax = new Vector2(0.5f, 0f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -8f);
            subRt.sizeDelta = new Vector2(600f, 32f);

            Text subText = subtitleObj.AddComponent<Text>();
            subText.font = font;
            subText.text = "MAIN MENU";
            subText.fontSize = 20;
            subText.fontStyle = FontStyle.Italic;
            subText.alignment = TextAnchor.MiddleCenter;
            subText.color = new Color(0.7f, 0.85f, 0.9f, 0.85f);

            // Buttons Container
            GameObject btnContainer = new GameObject("ButtonsGroup", typeof(RectTransform));
            btnContainer.transform.SetParent(canvasObj.transform, false);
            RectTransform bcRt = btnContainer.GetComponent<RectTransform>();
            bcRt.anchorMin = new Vector2(0.5f, 0.36f);
            bcRt.anchorMax = new Vector2(0.5f, 0.36f);
            bcRt.pivot = new Vector2(0.5f, 0.5f);
            bcRt.sizeDelta = new Vector2(400f, 260f);

            VerticalLayoutGroup vlg = btnContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 18f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.MiddleCenter;

            // 1. Continue Button (Hidden initially until player plays any level)
            continueButton = CreateMenuButton(btnContainer.transform, "Continue", new Color(0.15f, 0.5f, 0.6f, 0.95f), new Color(0.3f, 0.9f, 1f), font);

            // 2. New Game Button
            newGameButton = CreateMenuButton(btnContainer.transform, "New Game", new Color(0.12f, 0.45f, 0.35f, 0.95f), new Color(0.2f, 0.9f, 0.7f), font);

            // 3. Quit Button
            quitButton = CreateMenuButton(btnContainer.transform, "Quit", new Color(0.4f, 0.15f, 0.18f, 0.95f), new Color(0.9f, 0.35f, 0.4f), font);

            BindButtonListeners();
            RefreshContinueButtonVisibility();
        }

        private Button CreateMenuButton(Transform parent, string label, Color baseColor, Color hoverGlowColor, Font font)
        {
            GameObject btnObj = new GameObject(label.Replace(" ", "") + "Button", typeof(RectTransform));
            btnObj.transform.SetParent(parent, false);

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(360f, 60f);

            Image img = btnObj.AddComponent<Image>();
            img.color = baseColor;

            Outline outline = btnObj.AddComponent<Outline>();
            outline.effectColor = hoverGlowColor * 0.7f;
            outline.effectDistance = new Vector2(2, -2);

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = baseColor;
            cb.highlightedColor = baseColor * 1.35f;
            cb.pressedColor = baseColor * 0.75f;
            cb.selectedColor = baseColor * 1.2f;
            btn.colors = cb;

            GameObject textObj = new GameObject("Label", typeof(RectTransform));
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            Text txt = textObj.AddComponent<Text>();
            txt.font = font;
            txt.text = label;
            txt.fontSize = 22;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;

            return btn;
        }
        #endregion
    }
}
