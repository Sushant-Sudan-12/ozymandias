using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using KKK.UI;

namespace KKK
{
    /// <summary>
    /// Placed in any level to control opening and completion dialogues.
    /// Connects automatically to the persistent DialogueManager.
    /// - Plays opening dialogue once per level session.
    /// - Does NOT repeat opening dialogue when the player dies and respawns.
    /// </summary>
    [DisallowMultipleComponent]
    public class SceneDialogueController : MonoBehaviour
    {
        public static SceneDialogueController Current { get; private set; }

        [Header("Scene Start Dialogue (Level Enter)")]
        [Tooltip("Whether to automatically play opening dialogue when entering this scene.")]
        [SerializeField] private bool playDialogueOnSceneEnter = true;

        [Tooltip("If true, does not repeat the level opening dialogue when respawning after dying to an enemy.")]
        [SerializeField] private bool dontReplayOnDeathRespawn = true;

        [Tooltip("Delay in seconds before starting opening dialogue after scene load.")]
        [SerializeField] private float enterDialogueDelay = 0.5f;

        [Tooltip("Opening dialogue lines for this scene.")]
        [SerializeField] private DialogueSequence onSceneEnterDialogue = new DialogueSequence("LevelEnter");

        [Header("Scene Completion Dialogue (Level Finish)")]
        [Tooltip("Dialogue played when stepping into the magic circle to complete the level.")]
        [SerializeField] private DialogueSequence onSceneFinishDialogue = new DialogueSequence("LevelFinish");

        [Header("Custom Scene Dialogues")]
        [Tooltip("Additional custom dialogues that can be triggered by name or events.")]
        [SerializeField] private List<DialogueSequence> customDialogues = new List<DialogueSequence>();

        [Header("Events")]
        public UnityEvent onEnterDialogueFinished;
        public UnityEvent onFinishDialogueFinished;

        // Static registry to prevent annoying repeated dialogues on death respawn
        private static readonly HashSet<string> s_PlayedIntroScenes = new HashSet<string>();

        public bool HasFinishDialogue => onSceneFinishDialogue != null && onSceneFinishDialogue.lines != null && onSceneFinishDialogue.lines.Count > 0;

        private void Awake()
        {
            Current = this;
        }

        private void Start()
        {
            string sceneName = SceneManager.GetActiveScene().name;

            // Check if opening dialogue was already played for this scene
            if (dontReplayOnDeathRespawn && s_PlayedIntroScenes.Contains(sceneName))
            {
                // Player died and respawned -> Do not play dialogue again
                return;
            }

            if (playDialogueOnSceneEnter && onSceneEnterDialogue != null && onSceneEnterDialogue.lines.Count > 0)
            {
                s_PlayedIntroScenes.Add(sceneName);
                StartCoroutine(PlayEnterDialogueRoutine());
            }
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        /// <summary>
        /// Clears the played dialogue memory (e.g. when starting a new game from MainMenu).
        /// </summary>
        public static void ResetDialogueHistory()
        {
            s_PlayedIntroScenes.Clear();
        }

        private IEnumerator PlayEnterDialogueRoutine()
        {
            if (enterDialogueDelay > 0f)
            {
                yield return new WaitForSeconds(enterDialogueDelay);
            }

            PlayEnterDialogue();
        }

        /// <summary>
        /// Plays the opening level entry dialogue.
        /// </summary>
        public void PlayEnterDialogue()
        {
            if (onSceneEnterDialogue == null || onSceneEnterDialogue.lines.Count == 0) return;

            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.StartDialogue(onSceneEnterDialogue, () =>
                {
                    onEnterDialogueFinished?.Invoke();
                });
            }
        }

        /// <summary>
        /// Plays the level finish dialogue.
        /// </summary>
        public void PlayFinishDialogue(Action onComplete = null)
        {
            if (onSceneFinishDialogue == null || onSceneFinishDialogue.lines.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.StartDialogue(onSceneFinishDialogue, () =>
                {
                    onFinishDialogueFinished?.Invoke();
                    onComplete?.Invoke();
                });
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// Plays a custom dialogue by its dialogueId.
        /// </summary>
        public void PlayDialogueById(string dialogueId)
        {
            if (string.IsNullOrEmpty(dialogueId)) return;

            for (int i = 0; i < customDialogues.Count; i++)
            {
                if (customDialogues[i] != null && string.Equals(customDialogues[i].dialogueId, dialogueId, StringComparison.OrdinalIgnoreCase))
                {
                    if (DialogueManager.Instance != null)
                    {
                        DialogueManager.Instance.StartDialogue(customDialogues[i]);
                    }
                    return;
                }
            }

            Debug.LogWarning($"[SceneDialogueController] Dialogue with ID '{dialogueId}' was not found in scene.");
        }
    }
}
