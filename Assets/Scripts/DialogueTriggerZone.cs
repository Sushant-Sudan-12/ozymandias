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
    /// Attach to a GameObject with a Trigger Collider (e.g. BoxCollider).
    /// Plays a dialogue sequence using the main DialogueManager when the player steps inside
    /// (e.g. entering a special area, reaching a sign, or triggering a mid-level story moment).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class DialogueTriggerZone : MonoBehaviour
    {
        [Header("Dialogue Content")]
        [Tooltip("The dialogue sequence played when stepping into this trigger zone.")]
        [SerializeField] private DialogueSequence dialogueSequence = new DialogueSequence("ZoneDialogue");

        [Header("Alternative: Play from SceneDialogueController")]
        [Tooltip("Optional: If specified, triggers the dialogue matching this ID in the SceneDialogueController instead.")]
        [SerializeField] private string sceneDialogueId = "";

        [Header("Trigger Behavior")]
        [Tooltip("Whether this dialogue zone triggers only once per level playthrough.")]
        [SerializeField] private bool triggerOnlyOnce = true;

        [Tooltip("If true, this dialogue will not repeat if the player dies and respawns.")]
        [SerializeField] private bool dontReplayOnDeathRespawn = true;

        [Tooltip("Optional delay before starting dialogue after stepping into the zone.")]
        [SerializeField] private float triggerDelay = 0f;

        [Header("Events")]
        public UnityEvent onDialogueTriggered;
        public UnityEvent onDialogueCompleted;

        // Static registry for death respawn tracking across all trigger zones
        private static readonly HashSet<string> s_TriggeredZoneIds = new HashSet<string>();

        // Runtime state
        private bool _hasTriggered = false;
        private Collider _collider;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            if (_collider != null)
            {
                _collider.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggerOnlyOnce && _hasTriggered) return;

            string zoneKey = GetZoneUniqueKey();
            if (dontReplayOnDeathRespawn && s_TriggeredZoneIds.Contains(zoneKey))
            {
                return;
            }

            if (IsPlayer(other))
            {
                _hasTriggered = true;
                s_TriggeredZoneIds.Add(zoneKey);

                if (triggerDelay > 0f)
                {
                    StartCoroutine(DelayedTriggerRoutine(triggerDelay));
                }
                else
                {
                    TriggerDialogue();
                }
            }
        }

        private bool IsPlayer(Collider col)
        {
            return col.CompareTag("Player") ||
                   col.GetComponent<PlayerMovement>() != null ||
                   col.GetComponentInParent<PlayerMovement>() != null;
        }

        private IEnumerator DelayedTriggerRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            TriggerDialogue();
        }

        /// <summary>
        /// Triggers the dialogue via DialogueManager or SceneDialogueController.
        /// </summary>
        public void TriggerDialogue()
        {
            onDialogueTriggered?.Invoke();

            // 1. If sceneDialogueId is specified, use SceneDialogueController
            if (!string.IsNullOrEmpty(sceneDialogueId) && SceneDialogueController.Current != null)
            {
                SceneDialogueController.Current.PlayDialogueById(sceneDialogueId);
                return;
            }

            // 2. Otherwise play local dialogueSequence
            if (dialogueSequence != null && dialogueSequence.lines != null && dialogueSequence.lines.Count > 0)
            {
                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.StartDialogue(dialogueSequence, () =>
                    {
                        onDialogueCompleted?.Invoke();
                    });
                }
                else
                {
                    Debug.LogWarning("[DialogueTriggerZone] No DialogueManager instance found in scene.");
                }
            }
        }

        private string GetZoneUniqueKey()
        {
            return SceneManager.GetActiveScene().name + "_" + gameObject.name + "_" + transform.position.ToString();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.75f, 0.2f, 0.35f);
            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(0.9f, 0.75f, 0.2f, 0.9f);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawSphere(sphere.center, sphere.radius);
                Gizmos.color = new Color(0.9f, 0.75f, 0.2f, 0.9f);
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, 1.5f);
            }
        }
    }
}
