using System;
using System.Collections;
using UnityEngine;

[ExecuteAlways]
public class PlayerCameraRig : MonoBehaviour
{
    // Event fired whenever camera steps or orbits
    public static event Action OnCameraAngleChanged;

    [Header("Camera Reference")]
    [SerializeField] private Transform cameraTransform;

    [Header("Hovering Ring Settings")]
    [SerializeField] private float hoverHeight = 9f;
    [SerializeField] private float ringRadius = 7f;
    [SerializeField] private float lookAtHeightOffset = 1.2f;

    [Header("Rotation Settings")]
    [SerializeField] private float stepAngle = 45f;
    [SerializeField] private float rotateSpeed = 10f;

    [Header("Current Angle State")]
    [SerializeField] private float currentAngle = 0f;

    private int _targetStepIndex = 0;
    private float _targetAngle = 0f;

    private void Awake()
    {
        if (cameraTransform == null && transform.childCount > 0)
            cameraTransform = transform.GetChild(0);

        _targetStepIndex = Mathf.RoundToInt(currentAngle / stepAngle);
        _targetAngle = _targetStepIndex * stepAngle;
        currentAngle = _targetAngle;

        UpdateCameraPosition(currentAngle);
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            bool isPaused = Time.timeScale <= 0f || (KKK.UI.PauseMenuController.Instance != null && KKK.UI.PauseMenuController.Instance.IsPaused);
            bool isDialogueActive = KKK.UI.DialogueManager.Instance != null && KKK.UI.DialogueManager.Instance.IsDialogueActive;

            if (isPaused || isDialogueActive) return;

            bool stepped = false;

            // E: Clockwise (-)
            if (Input.GetKeyDown(KeyCode.E))
            {
                _targetStepIndex--;
                _targetAngle = _targetStepIndex * stepAngle;
                stepped = true;
            }
            // Q: Counter-Clockwise (+)
            else if (Input.GetKeyDown(KeyCode.Q))
            {
                _targetStepIndex++;
                _targetAngle = _targetStepIndex * stepAngle;
                stepped = true;
            }

            if (stepped)
            {
                // Instantly notify entities that viewing angle changed
                OnCameraAngleChanged?.Invoke();
            }

            if (Mathf.Abs(currentAngle - _targetAngle) > 0.01f)
            {
                currentAngle = Mathf.MoveTowards(currentAngle, _targetAngle, rotateSpeed * 45f * Time.deltaTime);
            }
            else
            {
                currentAngle = _targetAngle;
            }
        }
    }

    private void LateUpdate()
    {
        transform.rotation = Quaternion.identity;
        UpdateCameraPosition(currentAngle);
    }

    private void UpdateCameraPosition(float angleInDegrees)
    {
        if (cameraTransform == null)
        {
            if (transform.childCount > 0)
                cameraTransform = transform.GetChild(0);
            else
                return;
        }

        float rad = angleInDegrees * Mathf.Deg2Rad;
        Vector3 ringCenter = transform.position + (Vector3.up * hoverHeight);
        Vector3 offset = new Vector3(Mathf.Sin(rad) * ringRadius, 0f, -Mathf.Cos(rad) * ringRadius);

        cameraTransform.position = ringCenter + offset;
        Vector3 targetPoint = transform.position + (Vector3.up * lookAtHeightOffset);
        cameraTransform.LookAt(targetPoint);
    }

    private void OnValidate()
    {
        _targetStepIndex = Mathf.RoundToInt(currentAngle / stepAngle);
        _targetAngle = _targetStepIndex * stepAngle;
        UpdateCameraPosition(currentAngle);
    }
}