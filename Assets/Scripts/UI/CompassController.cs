using UnityEngine;

public class CompassController : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private RectTransform needleHook;

    [Header("Camera Reference")]
    [SerializeField] private Transform cameraTransform;

    [Header("Directional Manager Link")]
    [SerializeField] private DirectionalListManager listManager;

    [Header("Smoothing")]
    [SerializeField] private float smoothSpeed = 10f;

    private float _currentNeedleAngle = 0f;

    private void Awake()
    {
        if (!needleHook)
            needleHook = GetComponent<RectTransform>();

        if (!cameraTransform && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (!listManager)
            listManager = FindFirstObjectByType<DirectionalListManager>();
    }

    private void LateUpdate()
    {
        if (!cameraTransform)
        {
            if (Camera.main != null) cameraTransform = Camera.main.transform;
            else return;
        }

        Vector3 camForward = cameraTransform.forward;
        camForward.y = 0f;

        if (camForward.sqrMagnitude < 0.001f) return;
        camForward.Normalize();

        // Calculate world angle relative to +Z (North = 0°)
        float cameraYaw = Vector3.SignedAngle(Vector3.forward, camForward, Vector3.up);
        float targetNeedleAngle = -cameraYaw;

        _currentNeedleAngle = Mathf.LerpAngle(_currentNeedleAngle, targetNeedleAngle, smoothSpeed * Time.deltaTime);

        // Rotate the needle hook UI
        if (needleHook)
            needleHook.localRotation = Quaternion.Euler(0f, 0f, _currentNeedleAngle);

        // Tell DirectionalListManager which way the compass is pointing
        if (listManager != null)
        {
            listManager.UpdateFromNeedleAngle(_currentNeedleAngle);
        }
    }
}