using System.Collections;
using UnityEngine;

namespace KKK
{
    /// <summary>
    /// Attach to a GameObject with a Trigger Collider (e.g. BoxCollider).
    /// Activates a specific UI GameObject (like a 'WASD to Move' or 'QE to Rotate' popup)
    /// when the player steps inside, and deactivates it when the player leaves.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class UITriggerZone : MonoBehaviour
    {
        [Header("Target UI GameObject")]
        [Tooltip("The UI GameObject/Panel on your Canvas to activate when inside this trigger zone.")]
        [SerializeField] private GameObject targetUIElement;

        [Tooltip("Whether to ensure the target UI element starts disabled when the scene loads.")]
        [SerializeField] private bool hideAtStart = true;

        [Header("Trigger Behavior")]
        [Tooltip("Whether to automatically deactivate the UI element when the player steps out of this zone.")]
        [SerializeField] private bool hideOnExit = true;

        [Tooltip("If greater than 0, automatically hides the UI element after this many seconds even if still inside.")]
        [SerializeField] private float autoHideDuration = 0f;

        [Tooltip("If true, this trigger zone will only activate the UI once and never again.")]
        [SerializeField] private bool triggerOnlyOnce = false;

        // Runtime state
        private bool _hasTriggered = false;
        private bool _isPlayerInside = false;
        private Coroutine _autoHideCoroutine;
        private Collider _collider;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            if (_collider != null)
            {
                _collider.isTrigger = true;
            }

            if (hideAtStart && targetUIElement != null)
            {
                targetUIElement.SetActive(false);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggerOnlyOnce && _hasTriggered) return;

            if (IsPlayer(other))
            {
                _isPlayerInside = true;
                _hasTriggered = true;

                if (targetUIElement != null)
                {
                    targetUIElement.SetActive(true);
                }

                if (autoHideDuration > 0f)
                {
                    if (_autoHideCoroutine != null) StopCoroutine(_autoHideCoroutine);
                    _autoHideCoroutine = StartCoroutine(AutoHideRoutine(autoHideDuration));
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!_isPlayerInside) return;

            if (IsPlayer(other))
            {
                _isPlayerInside = false;

                if (_autoHideCoroutine != null)
                {
                    StopCoroutine(_autoHideCoroutine);
                    _autoHideCoroutine = null;
                }

                if (hideOnExit && targetUIElement != null)
                {
                    targetUIElement.SetActive(false);
                }

                if (triggerOnlyOnce)
                {
                    gameObject.SetActive(false);
                }
            }
        }

        private bool IsPlayer(Collider col)
        {
            return col.CompareTag("Player") ||
                   col.GetComponent<PlayerMovement>() != null ||
                   col.GetComponentInParent<PlayerMovement>() != null;
        }

        private IEnumerator AutoHideRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (targetUIElement != null)
            {
                targetUIElement.SetActive(false);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawSphere(sphere.center, sphere.radius);
                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, 1.5f);
            }
        }
    }
}
