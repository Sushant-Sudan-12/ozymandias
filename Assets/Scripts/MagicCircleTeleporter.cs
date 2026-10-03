using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using KKK.UI;

namespace KKK
{
    /// <summary>
    /// Magic Circle Collider trigger zone.
    /// - Starts at initialScale (default 0.5).
    /// - As the player stands inside, it scales up smoothly to activeScale (1.0) over 'durationToTeleport' (default 1s).
    /// - Once fully charged, it teleports the player to 'targetSceneName'.
    /// - If the player steps out before completing, it smoothly shrinks back down.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class MagicCircleTeleporter : MonoBehaviour
    {
        [Header("Scene Destination")]
        [Tooltip("Exact name of the scene to load upon teleportation (e.g. Starting, Area1, Area2, Area3, Forest).")]
        [SerializeField] private string targetSceneName = "Starting";

        [Header("Teleport Duration")]
        [Tooltip("Time in seconds the player must stand inside the circle to trigger teleportation.")]
        [SerializeField] private float durationToTeleport = 1.0f;

        [Header("Scaling Animation (0.5 -> 1.0)")]
        [Tooltip("The Transform to scale up as it charges (defaults to this object or first child if unassigned).")]
        [SerializeField] private Transform visualTransform;

        [Tooltip("Scale of the magic circle when empty.")]
        [SerializeField] private Vector3 initialScale = new Vector3(0.5f, 0.5f, 0.5f);

        [Tooltip("Scale of the magic circle when fully charged right before teleporting.")]
        [SerializeField] private Vector3 activeScale = new Vector3(1.0f, 1.0f, 1.0f);

        [Tooltip("Speed at which the circle shrinks back to initial scale when player leaves.")]
        [SerializeField] private float shrinkSpeed = 3f;

        [Header("Optional Visual & Audio Effects")]
        [Tooltip("Optional Light that brightens as the circle charges.")]
        [SerializeField] private Light circleLight;
        [SerializeField] private float baseLightIntensity = 0.5f;
        [SerializeField] private float maxLightIntensity = 3.0f;

        [Tooltip("Optional sound played when teleportation occurs.")]
        [SerializeField] private AudioClip teleportSound;

        [Tooltip("Optional particle system played when charging/teleporting.")]
        [SerializeField] private ParticleSystem chargeParticles;

        // Runtime state
        private bool _isPlayerInside = false;
        private float _chargeTimer = 0f;
        private bool _hasTeleported = false;
        private Collider _triggerCollider;

        private void Awake()
        {
            _triggerCollider = GetComponent<Collider>();
            if (_triggerCollider != null)
            {
                _triggerCollider.isTrigger = true;
            }

            if (visualTransform == null)
            {
                visualTransform = transform.childCount > 0 ? transform.GetChild(0) : transform;
            }

            if (visualTransform != null)
            {
                visualTransform.localScale = initialScale;
            }

            if (circleLight != null)
            {
                circleLight.intensity = baseLightIntensity;
            }
        }

        private void Update()
        {
            if (_hasTeleported) return;

            if (_isPlayerInside)
            {
                // Accumulate charging time
                _chargeTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(_chargeTimer / Mathf.Max(0.01f, durationToTeleport));

                // Scale up: 0.5 -> 1.0
                if (visualTransform != null)
                {
                    visualTransform.localScale = Vector3.Lerp(initialScale, activeScale, progress);
                }

                // Brighten light if present
                if (circleLight != null)
                {
                    circleLight.intensity = Mathf.Lerp(baseLightIntensity, maxLightIntensity, progress);
                }

                // Teleport trigger
                if (_chargeTimer >= durationToTeleport)
                {
                    TeleportToNextScene();
                }
            }
            else
            {
                // Player stepped out: smoothly shrink back to initialScale (0.5)
                if (_chargeTimer > 0f)
                {
                    _chargeTimer = Mathf.MoveTowards(_chargeTimer, 0f, shrinkSpeed * Time.deltaTime);
                    float progress = Mathf.Clamp01(_chargeTimer / Mathf.Max(0.01f, durationToTeleport));

                    if (visualTransform != null)
                    {
                        visualTransform.localScale = Vector3.Lerp(initialScale, activeScale, progress);
                    }

                    if (circleLight != null)
                    {
                        circleLight.intensity = Mathf.Lerp(baseLightIntensity, maxLightIntensity, progress);
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other))
            {
                _isPlayerInside = true;

                if (chargeParticles != null && !chargeParticles.isPlaying)
                {
                    chargeParticles.Play();
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsPlayer(other))
            {
                _isPlayerInside = false;
            }
        }

        private bool IsPlayer(Collider col)
        {
            return col.CompareTag("Player") ||
                   col.GetComponent<PlayerMovement>() != null ||
                   col.GetComponentInParent<PlayerMovement>() != null;
        }

        private void TeleportToNextScene()
        {
            if (_hasTeleported) return;
            _hasTeleported = true;

            if (teleportSound != null)
            {
                AudioSource.PlayClipAtPoint(teleportSound, transform.position);
            }

            // Save the destination scene so Continue button loads the new scene
            GameProgressTracker.SaveScene(targetSceneName);

            if (SceneDialogueController.Current != null && SceneDialogueController.Current.HasFinishDialogue)
            {
                SceneDialogueController.Current.PlayFinishDialogue(() =>
                {
                    LoadTargetScene();
                });
            }
            else
            {
                LoadTargetScene();
            }
        }

        private void LoadTargetScene()
        {
            if (!string.IsNullOrEmpty(targetSceneName))
            {
                PauseMenuController.LoadSceneWithFade(targetSceneName, 2.0f);
            }
            else
            {
                Debug.LogError($"[MagicCircleTeleporter] Target Scene Name is empty on {gameObject.name}!");
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.4f);
            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawSphere(sphere.center, sphere.radius);
                Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, 1.5f);
            }
        }
    }
}
