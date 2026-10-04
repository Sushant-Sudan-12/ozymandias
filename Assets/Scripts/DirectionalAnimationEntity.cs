using UnityEngine;

public class DirectionalAnimationEntity : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator animator;
    [SerializeField] private Camera mainCamera;

    // 8 State Names for Idle (Preserved for full 8-directional idle facing):
    // 0: Facing toward Camera (South / Front)
    // 1: South-East (Front-Right)
    // 2: East (Right)
    // 3: North-East (Back-Right)
    // 4: North (Back)
    // 5: North-West (Back-Left)
    // 6: West (Left)
    // 7: South-West (Front-Left)
    private readonly string[] _idleStates = new string[8]
    {
        "Idle_S", "Idle_SE", "Idle_E", "Idle_NE",
        "Idle_N", "Idle_NW", "Idle_W", "Idle_SW"
    };

    // The physical 3D ground direction this entity is facing in world space
    public Vector3 WorldFacingDirection { get; private set; } = Vector3.back; // default facing South
    public bool IsMoving { get; private set; } = false;

    private int _currentRelativeDirection = 0;
    private float _lastSignedAngle = 180f;
    private string _currentPlayingClip = "";

    private void Awake()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>();

        if (!mainCamera)
            mainCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
    }

    private void OnEnable()
    {
        PlayerCameraRig.OnCameraAngleChanged += HandleCameraAngleChanged;
    }

    private void OnDisable()
    {
        PlayerCameraRig.OnCameraAngleChanged -= HandleCameraAngleChanged;
    }

    /// <summary>
    /// Call this from movement scripts (PlayerMovement or EnemyAI) every frame.
    /// </summary>
    public void SetMovement(Vector3 worldVelocity)
    {
        Vector3 flatVel = new Vector3(worldVelocity.x, 0f, worldVelocity.z);
        IsMoving = flatVel.sqrMagnitude > 0.05f;

        if (IsMoving)
        {
            // Update physical world facing to match the movement vector
            WorldFacingDirection = flatVel.normalized;
        }

        UpdateAnimationState();
    }

    private void HandleCameraAngleChanged()
    {
        // Even if totally stationary (Idle), recalculate sprite relative to new camera angle
        UpdateAnimationState();
    }

    public void UpdateAnimationState()
    {
        if (animator == null || mainCamera == null) return;

        // 1. Get the camera's flat look direction (forward projected onto XZ plane)
        Vector3 camForward = mainCamera.transform.forward;
        camForward.y = 0f;
        camForward.Normalize();

        // 2. Calculate signed angle between camera forward and entity world facing
        // If entity faces opposite to camForward (toward camera lens) -> Angle is ~180° (South/Front)
        // If entity faces same as camForward (away from camera lens) -> Angle is ~0° (North/Back)
        float signedAngle = Vector3.SignedAngle(camForward, WorldFacingDirection, Vector3.up);
        if (signedAngle < 0f) signedAngle += 360f; // [0, 360)
        _lastSignedAngle = signedAngle;

        // 3. Map angle to 8 discrete screen-relative sectors for Idle
        _currentRelativeDirection = AngleTo8WayIndex(signedAngle);

        // 4. Play matching Idle (8-way) or Walk state (4-way cardinal: S, E, N, W)
        string targetClip = IsMoving 
            ? Get4WayWalkState(_currentRelativeDirection, signedAngle)
            : _idleStates[_currentRelativeDirection];

        if (targetClip != _currentPlayingClip)
        {
            _currentPlayingClip = targetClip;
            animator.Play(targetClip, 0, 0f);
        }
    }

    /// <summary>
    /// Maps 8-way facing index to one of the 4 cardinal walk states (Walk_S, Walk_E, Walk_N, Walk_W).
    /// Diagonal directions (NE, NW, SE, SW) resolve to North/East or South/West.
    /// </summary>
    private string Get4WayWalkState(int eightWayIndex, float angle)
    {
        switch (eightWayIndex)
        {
            case 0: return "Walk_S"; // South (Front)
            case 2: return "Walk_E"; // East (Right)
            case 4: return "Walk_N"; // North (Back)
            case 6: return "Walk_W"; // West (Left)

            case 1: // South-East (112.5° - 157.5°) -> South or East
                return (angle > 135f) ? "Walk_S" : "Walk_E";

            case 3: // North-East (22.5° - 67.5°) -> North or East
                return (angle > 45f) ? "Walk_E" : "Walk_N";

            case 5: // North-West (292.5° - 337.5°) -> North or West
                return (angle > 315f) ? "Walk_N" : "Walk_W";

            case 7: // South-West (202.5° - 247.5°) -> South or West
                return (angle > 225f) ? "Walk_W" : "Walk_S";

            default:
                return "Walk_S";
        }
    }

    private int AngleTo8WayIndex(float angle)
    {
        // CamForward is 0° (North - entity facing away from camera)
        // 45° = North-East
        // 90° = East (entity facing camera's right)
        // 135° = South-East
        // 180° = South (entity facing directly toward camera lens)
        // 225° = South-West
        // 270° = West (entity facing camera's left)
        // 315° = North-West

        if (angle >= 337.5f || angle < 22.5f) return 4; // North (Back)
        if (angle >= 22.5f && angle < 67.5f)   return 3; // North-East
        if (angle >= 67.5f && angle < 112.5f)  return 2; // East (Right)
        if (angle >= 112.5f && angle < 157.5f) return 1; // South-East
        if (angle >= 157.5f && angle < 202.5f) return 0; // South (Front)
        if (angle >= 202.5f && angle < 247.5f) return 7; // South-West
        if (angle >= 247.5f && angle < 292.5f) return 6; // West (Left)
        return 5;                                        // North-West
    }
}
