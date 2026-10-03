using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KKK.UI
{
    /// <summary>
    /// Persistent Dialogue Manager attached to the UI Manager in the MainMenu scene.
    /// Supports TextMeshPro (TMP_Text / TextMeshProUGUI) for speaker name and dialogue body text.
    /// Manages the 4 UI elements:
    /// 1. Speaker Image (Image)
    /// 2. Speaker Name (TMP_Text)
    /// 3. Dialogue Body (TMP_Text)
    /// 4. Skip Gameobject / Indicator (GameObject)
    /// Advances / skips with Space, Enter, E, or Mouse Click.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        [Header("UI Bindings (TextMeshPro)")]
        [Tooltip("The root panel GameObject of the dialogue box.")]
        [SerializeField] private GameObject dialoguePanel;

        [Tooltip("Speaker character portrait / avatar image.")]
        [SerializeField] private Image speakerImage;

        [Tooltip("TextMeshPro component displaying the speaker's name.")]
        [SerializeField] private TMP_Text speakerNameText;

        [Tooltip("TextMeshPro component displaying the dialogue sentence.")]
        [SerializeField] private TMP_Text dialogueText;

        [Tooltip("GameObject / Image for the skip prompt (Space / Click) that activates during dialogue.")]
        [SerializeField] private GameObject skipGameObject;

        [Header("Optional Legacy Text Fallback")]
        [Tooltip("Optional standard UI Text for speaker name (if not using TextMeshPro).")]
        [SerializeField] private Text legacySpeakerNameText;

        [Tooltip("Optional standard UI Text for dialogue body (if not using TextMeshPro).")]
        [SerializeField] private Text legacyDialogueText;

        [Header("Controls")]
        [Tooltip("Keyboard keys that advance dialogue or fast-forward typing.")]
        [SerializeField] private KeyCode[] advanceKeys = new KeyCode[] { KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter, KeyCode.E };

        // Events
        public static event Action OnDialogueStarted;
        public static event Action OnDialogueEnded;

        // Runtime State
        private Queue<DialogueLine> _linesQueue = new Queue<DialogueLine>();
        private DialogueLine _currentLine;
        private Coroutine _typewriterCoroutine;
        private bool _isTyping = false;
        private bool _isOpen = false;
        private Action _onSequenceComplete;

        public bool IsDialogueActive => _isOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (dialoguePanel != null)
            {
                dialoguePanel.SetActive(false);
            }

            if (skipGameObject != null)
            {
                skipGameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!_isOpen) return;

            // Check advance keys (Space, Enter, E)
            for (int i = 0; i < advanceKeys.Length; i++)
            {
                if (Input.GetKeyDown(advanceKeys[i]))
                {
                    AdvanceOrSkip();
                    break;
                }
            }

            // Also advance on left mouse click anywhere
            if (Input.GetMouseButtonDown(0))
            {
                AdvanceOrSkip();
            }
        }

        /// <summary>
        /// Starts a full dialogue sequence.
        /// </summary>
        public void StartDialogue(DialogueSequence sequence, Action onComplete = null)
        {
            if (sequence == null || sequence.lines == null || sequence.lines.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            _onSequenceComplete = () =>
            {
                sequence.onComplete?.Invoke();
                onComplete?.Invoke();
            };

            _linesQueue.Clear();
            foreach (var line in sequence.lines)
            {
                if (line != null && !string.IsNullOrEmpty(line.text))
                {
                    _linesQueue.Enqueue(line);
                }
            }

            if (_linesQueue.Count == 0)
            {
                _onSequenceComplete?.Invoke();
                return;
            }

            OpenDialogueBox();
            DisplayNextLine();
        }

        /// <summary>
        /// Convenience method to play a single line of dialogue.
        /// </summary>
        public static void Play(string speaker, string text, Action onComplete = null)
        {
            if (Instance == null)
            {
                Debug.LogWarning("[DialogueManager] No active DialogueManager found in scene.");
                onComplete?.Invoke();
                return;
            }

            var seq = new DialogueSequence("QuickDialogue");
            seq.lines.Add(new DialogueLine { speakerName = speaker, text = text });
            Instance.StartDialogue(seq, onComplete);
        }

        private void OpenDialogueBox()
        {
            _isOpen = true;
            if (dialoguePanel != null)
            {
                dialoguePanel.SetActive(true);
            }

            if (skipGameObject != null)
            {
                skipGameObject.SetActive(true);
            }

            OnDialogueStarted?.Invoke();
        }

        private void CloseDialogueBox()
        {
            _isOpen = false;
            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
                _typewriterCoroutine = null;
            }
            _isTyping = false;

            if (dialoguePanel != null)
            {
                dialoguePanel.SetActive(false);
            }

            if (skipGameObject != null)
            {
                skipGameObject.SetActive(false);
            }

            OnDialogueEnded?.Invoke();

            var cb = _onSequenceComplete;
            _onSequenceComplete = null;
            cb?.Invoke();
        }

        private void DisplayNextLine()
        {
            if (_linesQueue.Count == 0)
            {
                CloseDialogueBox();
                return;
            }

            _currentLine = _linesQueue.Dequeue();

            // Set Speaker Name
            string speaker = !string.IsNullOrEmpty(_currentLine.speakerName) ? _currentLine.speakerName : "";
            SetSpeakerNameText(speaker);

            // Set Speaker Image / Portrait
            if (speakerImage != null)
            {
                if (_currentLine.portrait != null)
                {
                    speakerImage.sprite = _currentLine.portrait;
                    speakerImage.gameObject.SetActive(true);
                }
                else if (speakerImage.sprite == null)
                {
                    // No portrait sprite
                }
            }

            // Play voice clip if available
            if (_currentLine.voiceClip != null)
            {
                AudioSource.PlayClipAtPoint(_currentLine.voiceClip, Camera.main != null ? Camera.main.transform.position : transform.position);
            }

            // Typewriter or instant text
            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
            }

            if (_currentLine.typingSpeed > 0.001f)
            {
                _typewriterCoroutine = StartCoroutine(TypewriterRoutine(_currentLine.text, _currentLine.typingSpeed));
            }
            else
            {
                SetDialogueBodyText(_currentLine.text);
                _isTyping = false;
            }
        }

        private IEnumerator TypewriterRoutine(string fullText, float delay)
        {
            _isTyping = true;
            SetDialogueBodyText("");

            for (int i = 0; i <= fullText.Length; i++)
            {
                SetDialogueBodyText(fullText.Substring(0, i));
                yield return new WaitForSecondsRealtime(delay);
            }

            _isTyping = false;
        }

        private void SetSpeakerNameText(string text)
        {
            if (speakerNameText != null)
            {
                speakerNameText.text = text;
            }

            if (legacySpeakerNameText != null)
            {
                legacySpeakerNameText.text = text;
            }
        }

        private void SetDialogueBodyText(string text)
        {
            if (dialogueText != null)
            {
                dialogueText.text = text;
            }

            if (legacyDialogueText != null)
            {
                legacyDialogueText.text = text;
            }
        }

        /// <summary>
        /// Advances to the next line or instantly skips the typewriter animation.
        /// </summary>
        public void AdvanceOrSkip()
        {
            if (!_isOpen) return;

            if (_isTyping)
            {
                // Skip typing animation
                if (_typewriterCoroutine != null)
                {
                    StopCoroutine(_typewriterCoroutine);
                    _typewriterCoroutine = null;
                }

                _isTyping = false;
                if (_currentLine != null)
                {
                    SetDialogueBodyText(_currentLine.text);
                }
            }
            else
            {
                // Next line
                DisplayNextLine();
            }
        }
    }
}
