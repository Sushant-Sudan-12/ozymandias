using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class EnemyDetection : MonoBehaviour
{
    public enum PatrolType
    {
        [Tooltip("Goes back and forth tracing the path: A -> B -> C -> D -> C -> B -> A")]
        PingPong,
        [Tooltip("Loops in a continuous cycle: A -> B -> C -> D -> A -> B -> C -> D")]
        Cyclic,
        [Tooltip("Patrols once from start to end and stops at the final waypoint")]
        SingleRun
    }

    public enum EnemyState
    {
        Patrolling,
        WaitingAtWaypoint,
        SpottingPlayer,
        ChasingPlayer,
        BlockedByWall
    }

    [Header("Enemy Behavior Type (Stationary Ghost / Moving)")]
    [Tooltip("If true, this enemy/ghost stays completely stationary in place. It detects the player and fades the screen, but does not move along waypoints or chase.")]
    [SerializeField] private bool isStationary = false;

    [Tooltip("If stationary, whether the ghost rotates to face the player while detecting them.")]
    [SerializeField] private bool rotateToFacePlayerWhileStationary = true;

    [Header("Patrol & Waypoints (A, B, C, D...) - Active if Not Stationary")]
    [Tooltip("List of waypoint transforms to patrol between (e.g. A and B, or A, B, C, D).")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();

    [Tooltip("Movement pattern across waypoints (PingPong / Cyclic / SingleRun).")]
    [SerializeField] private PatrolType patrolType = PatrolType.PingPong;

    [Tooltip("Normal patrol movement speed between A and B.")]
    [SerializeField] private float patrolSpeed = 2.0f;

    [Tooltip("Distance threshold to consider a waypoint reached.")]
    [SerializeField] private float waypointReachDistance = 0.35f;

    [Tooltip("Pause duration (in seconds) upon reaching each waypoint.")]
    [SerializeField] private float waitTimeAtWaypoint = 0.6f;

    [Tooltip("Speed at which the 3D enemy rotates to face movement direction or the player.")]
    [SerializeField] private float rotationSpeed = 14f;

    [Header("Dual Detection Zones")]
    [Tooltip("Forward vision cone detection radius (far zone in front of enemy).")]
    [SerializeField] private float detectionRadius = 7f;

    [Tooltip("Close 360-degree proximity circle around enemy. If player is within this radius in ANY direction, they are spotted.")]
    [SerializeField] private float proximityRadius = 1.2f;

    [Tooltip("Enable conical field of view for the forward zone. (Proximity radius is always 360 degrees).")]
    [SerializeField] private bool useFieldOfView = true;

    [Tooltip("Field of view angle in degrees for the forward cone (e.g., 100 degrees in front).")]
    [Range(10f, 360f)]
    [SerializeField] private float viewAngle = 100f;

    [Header("Detection Timer & Screen Fade")]
    [Tooltip("Continuous sight duration (in seconds) before full detection and player death. (FadeScreen transitions 0 to 1 over this time).")]
    [SerializeField] private float detectionTimeRequired = 2.0f;

    [Tooltip("Whether to reload the active scene upon player death.")]
    [SerializeField] private bool reloadSceneOnDeath = true;

    [Header("Player Chase (When Moving Enemy is Detected)")]
    [Tooltip("Chase speed when player is detected (faster than normal patrol speed). Ignored if isStationary is true.")]
    [SerializeField] private float chaseSpeed = 3.5f;

    [Tooltip("Stopping distance from player when reaching them.")]
    [SerializeField] private float chaseStopDistance = 0.9f;

    [Header("Wall Collision / Anti-Clipping Settings")]
    [Tooltip("Distance ahead to probe for walls/obstacles blocking movement.")]
    [SerializeField] private float wallProbeDistance = 0.75f;

    [Tooltip("SphereCast radius used to detect obstacles in front of the enemy.")]
    [SerializeField] private float wallProbeRadius = 0.35f;

    [Tooltip("Brief pause (in seconds) when hitting a dynamic wall before turning around to return.")]
    [SerializeField] private float turnAroundWaitTime = 0.35f;

    [Header("Obstacle & Raycast Settings")]
    [Tooltip("LayerMask for obstacles (trees, walls, maze barriers) that block vision and movement.")]
    [SerializeField] private LayerMask obstacleLayer = ~0;

    [Tooltip("Whether obstacle raycasts should ignore trigger colliders.")]
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Sight & Probe Offsets")]
    [Tooltip("Height offset from enemy pivot for primary vision raycast origin (eye level).")]
    [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 0.35f, 0f);

    [Tooltip("Height offset from player pivot for primary vision raycast target (center of player).")]
    [SerializeField] private Vector3 playerTargetOffset = new Vector3(0f, 0.35f, 0f);

    [Header("FadeScreen UI Reference (Auto-detected if blank)")]
    [Tooltip("Optional CanvasGroup on the FadeScreen UI. Auto-detected in Canvas if left empty.")]
    [SerializeField] private CanvasGroup fadeScreenCanvasGroup;

    [Tooltip("Optional Image on the FadeScreen UI. Auto-detected in Canvas if left empty.")]
    [SerializeField] private Image fadeScreenImage;

    [Header("Physics")]
    [SerializeField] private float gravity = 20f;

    [Header("Player Reference (Auto-detected if blank)")]
    [Tooltip("The player transform to detect. Auto-detected via Tag, PlayerMovement component, or 'Player' GameObject if left empty.")]
    [SerializeField] private Transform playerTarget;

    [Header("Optional 3D Animator")]
    [Tooltip("Optional Animator component for standard 3D animations (Speed, IsMoving, IsChasing).")]
    [SerializeField] private Animator animator;

    [Header("Events & Notifications")]
    public UnityEvent OnPlayerDetected;
    public UnityEvent OnPlayerLost;
    public UnityEvent<float> OnDetectionProgressChanged; // Value from 0.0 to 1.0

    // Shared / Global Multi-Enemy Registry & Screen Fade Coordinator
    private static readonly List<EnemyDetection> s_ActiveEnemies = new List<EnemyDetection>();
    private static float s_CurrentGlobalScreenAlpha = 0f;
    private static bool s_IsReloadingScene = false;
    private static CanvasGroup s_SharedCanvasGroup;
    private static Image s_SharedImage;

    // Internal Components & State
    private CharacterController _controller;
    private Vector3 _verticalVelocity;

    private EnemyState _currentState = EnemyState.Patrolling;
    private int _currentWaypointIndex = 0;
    private int _previousWaypointIndex = 0;
    private int _patrolDirection = 1; // +1 = forward, -1 = reverse
    private float _waitTimer = 0f;
    private float _blockedTimer = 0f;

    private float _currentDetectionTimer = 0f;
    private bool _isPlayerInSight = false;
    private bool _isPlayerDetected = false;
    private RaycastHit _lastObstacleHit;
    private bool _hadObstacleHit = false;

    // Animator Hashes
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsChasingHash = Animator.StringToHash("IsChasing");

    // Public Getters
    public bool IsStationary => isStationary;
    public EnemyState CurrentState => _currentState;
    public bool IsPlayerInSight => _isPlayerInSight;
    public bool IsPlayerDetected => _isPlayerDetected;
    public float CurrentDetectionTimer => _currentDetectionTimer;
    public float DetectionTimeRequired => detectionTimeRequired;
    public float DetectionProgress => detectionTimeRequired > 0f ? Mathf.Clamp01(_currentDetectionTimer / detectionTimeRequired) : (_isPlayerInSight ? 1f : 0f);
    public Transform PlayerTarget => playerTarget;
    public int CurrentWaypointIndex => _currentWaypointIndex;

    private void OnEnable()
    {
        if (!s_ActiveEnemies.Contains(this))
        {
            s_ActiveEnemies.Add(this);
        }
    }

    private void OnDisable()
    {
        s_ActiveEnemies.Remove(this);
        if (s_ActiveEnemies.Count == 0)
        {
            s_CurrentGlobalScreenAlpha = 0f;
            ApplyFadeAlpha(0f);
        }
    }

    private void Awake()
    {
        s_IsReloadingScene = false;
        s_CurrentGlobalScreenAlpha = 0f;
        s_SharedCanvasGroup = null;
        s_SharedImage = null;
        s_HasSearchedFadeScreen = false;

        _controller = GetComponent<CharacterController>();
        if (_controller != null)
        {
            _controller.stepOffset = Mathf.Min(_controller.stepOffset, 0.2f);
            _controller.skinWidth = Mathf.Max(_controller.skinWidth, 0.05f);
        }

        if (!animator) animator = GetComponentInChildren<Animator>();

        if (fadeScreenCanvasGroup != null) s_SharedCanvasGroup = fadeScreenCanvasGroup;
        if (fadeScreenImage != null) s_SharedImage = fadeScreenImage;

        EnsurePlayerReference();
        ResolveSharedFadeScreen();
    }

    private void Start()
    {
        EnsurePlayerReference();
        ResolveSharedFadeScreen();
    }

    private void Update()
    {
        if (!playerTarget)
        {
            EnsurePlayerReference();
        }

        // 1. Update Line-of-Sight & Detection Timer for this enemy
        UpdateDetectionState();

        // 2. Execute Movement (with full wall collision and anti-clipping checks)
        UpdateBehaviorAndMovement();
    }

    private void LateUpdate()
    {
        // 3. Central Coordinator: The primary active enemy coordinates the shared screen fade
        CoordinateSharedScreenFade();
    }

    private void UpdateDetectionState()
    {
        bool wasInSight = _isPlayerInSight;
        _isPlayerInSight = CheckLineOfSight();

        if (_isPlayerInSight)
        {
            if (detectionTimeRequired <= 0.001f)
            {
                _currentDetectionTimer = 0.001f;
                _isPlayerDetected = true;
            }
            else
            {
                _currentDetectionTimer += Time.deltaTime;
                if (_currentDetectionTimer >= detectionTimeRequired)
                {
                    _currentDetectionTimer = detectionTimeRequired;
                    _isPlayerDetected = true;
                }
            }

            OnDetectionProgressChanged?.Invoke(DetectionProgress);

            if (_isPlayerDetected && !wasInSight)
            {
                Debug.Log("Player detected");
                OnPlayerDetected?.Invoke();
            }
        }
        else
        {
            // The exact moment a wall is introduced or player leaves detection zone:
            if (_isPlayerDetected || wasInSight)
            {
                _isPlayerDetected = false;
                _currentDetectionTimer = 0f;
                OnPlayerLost?.Invoke();
            }

            _currentDetectionTimer = 0f;
            OnDetectionProgressChanged?.Invoke(0f);
        }
    }

    /// <summary>
    /// Evaluates all active enemies in the scene. If at least one enemy sees the player,
    /// the screen smoothly transitions 0 -> 1 over detectionTimeRequired seconds.
    /// The moment no enemy sees the player (wall or out of range), screen alpha IMMEDIATELY resets to 0.
    /// </summary>
    private void CoordinateSharedScreenFade()
    {
        if (s_ActiveEnemies.Count == 0 || s_ActiveEnemies[0] != this) return;

        float maxProgress = 0f;
        float shortestDuration = detectionTimeRequired;

        for (int i = 0; i < s_ActiveEnemies.Count; i++)
        {
            var enemy = s_ActiveEnemies[i];
            if (enemy == null) continue;

            if (enemy._isPlayerInSight)
            {
                float p = enemy.DetectionProgress;
                if (p > maxProgress)
                {
                    maxProgress = p;
                    shortestDuration = enemy.detectionTimeRequired;
                }
            }
        }

        // Case 1: At least one enemy sees the player -> Fade screen 0 to 1 over detectionTimeRequired seconds
        if (maxProgress > 0.001f)
        {
            float fadeRate = shortestDuration > 0f ? (1f / shortestDuration) : 10f;
            s_CurrentGlobalScreenAlpha = Mathf.MoveTowards(s_CurrentGlobalScreenAlpha, maxProgress, fadeRate * Time.deltaTime);
            ApplyFadeAlpha(s_CurrentGlobalScreenAlpha);

            // Full detection reached (2 seconds / 1.0 alpha) -> Player dies & scene reloads
            if (maxProgress >= 0.999f && s_CurrentGlobalScreenAlpha >= 0.98f && !s_IsReloadingScene)
            {
                s_IsReloadingScene = true;
                ApplyFadeAlpha(1f);

                if (reloadSceneOnDeath)
                {
                    Scene activeScene = SceneManager.GetActiveScene();
                    SceneManager.LoadScene(activeScene.buildIndex);
                }
            }
        }
        // Case 2: Player escaped / wall introduced -> IMMEDIATELY put fade screen back to alpha 0
        else
        {
            s_CurrentGlobalScreenAlpha = 0f;
            ApplyFadeAlpha(0f);
        }
    }

    private static bool s_HasSearchedFadeScreen = false;

    private static void ResolveSharedFadeScreen()
    {
        if (s_SharedCanvasGroup != null || s_SharedImage != null) return;

        // 1. Check persistent PauseMenuController FadeScreen
        if (KKK.UI.PauseMenuController.Instance != null)
        {
            if (KKK.UI.PauseMenuController.Instance.FadeCanvasGroup != null)
            {
                s_SharedCanvasGroup = KKK.UI.PauseMenuController.Instance.FadeCanvasGroup;
                return;
            }
            if (KKK.UI.PauseMenuController.Instance.FadeImage != null)
            {
                s_SharedImage = KKK.UI.PauseMenuController.Instance.FadeImage;
                return;
            }
        }

        if (s_HasSearchedFadeScreen) return;
        s_HasSearchedFadeScreen = true;

        // 2. Fallback: Search for CanvasGroup with 'fade' in name
        var allGroups = FindObjectsByType<CanvasGroup>(FindObjectsSortMode.None);
        for (int i = 0; i < allGroups.Length; i++)
        {
            if (allGroups[i] != null && allGroups[i].gameObject.name.IndexOf("fade", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                s_SharedCanvasGroup = allGroups[i];
                return;
            }
        }

        // 3. Fallback: Search for Image with 'fade' in name
        var allImages = FindObjectsByType<Image>(FindObjectsSortMode.None);
        for (int i = 0; i < allImages.Length; i++)
        {
            if (allImages[i] != null && allImages[i].gameObject.name.IndexOf("fade", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                s_SharedImage = allImages[i];
                return;
            }
        }
    }

    private static void ApplyFadeAlpha(float alpha)
    {
        if (KKK.UI.PauseMenuController.Instance != null)
        {
            KKK.UI.PauseMenuController.SetDetectionFade(alpha);
            return;
        }

        ResolveSharedFadeScreen();

        if (s_SharedCanvasGroup != null)
        {
            s_SharedCanvasGroup.alpha = alpha;
            s_SharedCanvasGroup.blocksRaycasts = alpha > 0.05f;
        }

        if (s_SharedImage != null)
        {
            Color c = s_SharedImage.color;
            c.a = alpha;
            s_SharedImage.color = c;
        }
    }

    private void UpdateBehaviorAndMovement()
    {
        // =========================================================================
        // SPECIAL MODE: STATIONARY GHOST (Does not move or chase, only detects & fades)
        // =========================================================================
        if (isStationary)
        {
            if (_isPlayerInSight && playerTarget != null)
            {
                _currentState = EnemyState.SpottingPlayer;
                if (rotateToFacePlayerWhileStationary)
                {
                    Vector3 diff = playerTarget.position - transform.position;
                    diff.y = 0f;
                    if (diff.sqrMagnitude > 0.001f)
                    {
                        RotateTowards(diff.normalized);
                    }
                }
            }
            else
            {
                _currentState = EnemyState.Patrolling;
            }

            ApplyPhysicsMovement(Vector3.zero);
            return;
        }

        // =========================================================================
        // MOVING ENEMY: CHASE & PATROL
        // =========================================================================
        Vector3 moveVelocity = Vector3.zero;

        // --- CASE 1: PLAYER DETECTED -> LEAVE A-B PATH & CHASE PLAYER (FASTER SPEED) ---
        if (_isPlayerInSight && playerTarget != null)
        {
            _currentState = EnemyState.ChasingPlayer;

            Vector3 targetPos = playerTarget.position;
            Vector3 diff = targetPos - transform.position;
            diff.y = 0f;
            float distToPlayer = diff.magnitude;

            if (distToPlayer > chaseStopDistance)
            {
                Vector3 moveDir = diff.normalized;
                RotateTowards(moveDir);

                // Anti-clipping: Probe forward before moving towards player to ensure no wall is penetrated
                Vector3 desiredVelocity = moveDir * chaseSpeed;
                moveVelocity = PreventWallClipping(desiredVelocity);
            }
            else
            {
                Vector3 moveDir = diff.normalized;
                if (moveDir.sqrMagnitude > 0.001f) RotateTowards(moveDir);
                moveVelocity = Vector3.zero;
            }
        }
        // --- CASE 2: NORMAL PATROL PATH MOVEMENT (Sight broken / wall / out of range) ---
        else
        {
            CleanWaypointsList();

            if (waypoints.Count >= 2)
            {
                // Handle brief blocked turn-around pause
                if (_blockedTimer > 0f)
                {
                    _currentState = EnemyState.BlockedByWall;
                    _blockedTimer -= Time.deltaTime;
                    moveVelocity = Vector3.zero;
                }
                // Handle wait at reached waypoint
                else if (_waitTimer > 0f)
                {
                    _currentState = EnemyState.WaitingAtWaypoint;
                    _waitTimer -= Time.deltaTime;
                    moveVelocity = Vector3.zero;

                    if (_waitTimer <= 0f)
                    {
                        AdvanceToNextWaypoint();
                    }
                }
                // Actively moving to target waypoint along A-B path
                else
                {
                    _currentState = EnemyState.Patrolling;
                    Transform targetWp = waypoints[_currentWaypointIndex];

                    if (targetWp != null)
                    {
                        Vector3 targetPos = targetWp.position;
                        Vector3 diff = targetPos - transform.position;
                        diff.y = 0f;
                        float dist = diff.magnitude;

                        // Check if reached destination waypoint
                        if (dist <= waypointReachDistance)
                        {
                            _previousWaypointIndex = _currentWaypointIndex;
                            _waitTimer = waitTimeAtWaypoint;
                            moveVelocity = Vector3.zero;
                        }
                        else
                        {
                            Vector3 moveDir = diff.normalized;
                            RotateTowards(moveDir);

                            // Check if a dynamic wall has appeared between current pos and target waypoint
                            if (IsPathBlockedByWall(moveDir, dist))
                            {
                                HandleWallEncounter();
                                moveVelocity = Vector3.zero;
                            }
                            else
                            {
                                // Move at normal patrol speed with wall clipping prevention
                                Vector3 desiredVelocity = moveDir * patrolSpeed;
                                moveVelocity = PreventWallClipping(desiredVelocity);
                            }
                        }
                    }
                }
            }
            else if (waypoints.Count == 1 && waypoints[0] != null)
            {
                Vector3 diff = waypoints[0].position - transform.position;
                diff.y = 0f;
                if (diff.magnitude > waypointReachDistance)
                {
                    Vector3 moveDir = diff.normalized;
                    RotateTowards(moveDir);
                    Vector3 desiredVelocity = moveDir * patrolSpeed;
                    moveVelocity = PreventWallClipping(desiredVelocity);
                }
                else
                {
                    moveVelocity = Vector3.zero;
                }
            }
            else
            {
                moveVelocity = Vector3.zero;
            }
        }

        // --- Apply 3D Physics Movement & Animations ---
        ApplyPhysicsMovement(moveVelocity);
    }

    /// <summary>
    /// Returns the effective layer mask for obstacles.
    /// Automatically ensures Layer 0 (Default) and Layer 3 (Obstacles) are checked,
    /// so that walls created as standard 3D Cubes/Meshes always block enemy sight and movement.
    /// </summary>
    public int GetEffectiveObstacleMask()
    {
        int mask = obstacleLayer.value;
        if (mask == 0)
        {
            mask = ~0;
        }
        else
        {
            // Ensure Default (Layer 0) and Obstacles (Layer 3) are always included
            mask |= (1 << 0) | (1 << 3);
        }

        // Never test against Ignore Raycast (Layer 2) or UI (Layer 5)
        mask &= ~(1 << 2);
        mask &= ~(1 << 5);

        return mask;
    }

    /// <summary>
    /// Prevents the enemy from clipping or passing through 3D walls/obstacles.
    /// Slides smoothly along wall surfaces or halts forward penetration.
    /// </summary>
    private Vector3 PreventWallClipping(Vector3 desiredVelocity)
    {
        if (desiredVelocity.sqrMagnitude < 0.001f) return Vector3.zero;

        Vector3 moveDir = desiredVelocity.normalized;
        float moveDist = desiredVelocity.magnitude * Time.deltaTime + 0.15f;
        Vector3 origin = transform.position + new Vector3(0f, 0.35f, 0f);
        int mask = GetEffectiveObstacleMask();

        RaycastHit[] hits = Physics.SphereCastAll(origin, wallProbeRadius, moveDir, moveDist, mask, triggerInteraction);
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if (playerTarget != null && (hit.transform == playerTarget || hit.transform.IsChildOf(playerTarget))) continue;

            Vector3 normal = hit.normal;
            normal.y = 0f;
            if (normal.sqrMagnitude > 0.001f) normal.Normalize();

            Vector3 slideVelocity = Vector3.ProjectOnPlane(desiredVelocity, normal);
            slideVelocity.y = 0f;

            if (Vector3.Dot(slideVelocity.normalized, normal) < 0f || slideVelocity.sqrMagnitude < 0.05f)
            {
                return Vector3.zero;
            }

            return slideVelocity;
        }

        return desiredVelocity;
    }

    private void RotateTowards(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.001f) return;
        direction.y = 0f;
        Quaternion targetRot = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Checks whether an obstacle/wall is directly blocking movement ahead along the patrol path.
    /// </summary>
    private bool IsPathBlockedByWall(Vector3 moveDir, float distToWaypoint)
    {
        Vector3 probeOrigin = transform.position + new Vector3(0f, 0.35f, 0f);
        float checkDist = Mathf.Min(wallProbeDistance, distToWaypoint);
        int mask = GetEffectiveObstacleMask();

        RaycastHit[] hits = Physics.SphereCastAll(probeOrigin, wallProbeRadius, moveDir, checkDist, mask, triggerInteraction);
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if (playerTarget != null && (hit.transform == playerTarget || hit.transform.IsChildOf(playerTarget))) continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Called when a dynamic wall appears blocking the path between waypoints (e.g. between A and B).
    /// Stops the enemy, turns around at the wall, and returns to the origin waypoint.
    /// </summary>
    private void HandleWallEncounter()
    {
        // Reverse patrol direction so enemy goes back to previous waypoint
        _patrolDirection *= -1;

        // Switch target waypoint back to the previous one
        int temp = _currentWaypointIndex;
        _currentWaypointIndex = _previousWaypointIndex;
        _previousWaypointIndex = temp;

        _blockedTimer = turnAroundWaitTime;
    }

    private void AdvanceToNextWaypoint()
    {
        if (waypoints.Count < 2) return;

        _previousWaypointIndex = _currentWaypointIndex;

        switch (patrolType)
        {
            case PatrolType.PingPong:
                _currentWaypointIndex += _patrolDirection;
                if (_currentWaypointIndex >= waypoints.Count)
                {
                    _patrolDirection = -1;
                    _currentWaypointIndex = waypoints.Count - 2;
                }
                else if (_currentWaypointIndex < 0)
                {
                    _patrolDirection = 1;
                    _currentWaypointIndex = 1;
                }
                break;

            case PatrolType.Cyclic:
                _currentWaypointIndex = (_currentWaypointIndex + 1) % waypoints.Count;
                break;

            case PatrolType.SingleRun:
                if (_currentWaypointIndex < waypoints.Count - 1)
                {
                    _currentWaypointIndex++;
                }
                break;
        }

        _currentWaypointIndex = Mathf.Clamp(_currentWaypointIndex, 0, waypoints.Count - 1);
    }

    private void ApplyPhysicsMovement(Vector3 moveVelocity)
    {
        if (_controller != null)
        {
            if (_controller.isGrounded)
            {
                _verticalVelocity.y = -1f;
            }
            else
            {
                _verticalVelocity.y -= gravity * Time.deltaTime;
            }

            Vector3 finalMove = (moveVelocity + _verticalVelocity) * Time.deltaTime;
            _controller.Move(finalMove);
        }
        else
        {
            transform.position += moveVelocity * Time.deltaTime;
        }

        // Update optional 3D Animator parameters
        if (animator != null)
        {
            float flatSpeed = new Vector3(moveVelocity.x, 0f, moveVelocity.z).magnitude;
            animator.SetFloat(SpeedHash, flatSpeed);
            animator.SetBool(IsMovingHash, flatSpeed > 0.05f);
            animator.SetBool(IsChasingHash, _currentState == EnemyState.ChasingPlayer);
        }
    }

    /// <summary>
    /// Checks if the player is within detection zones (forward cone OR close 360 circle)
    /// and completely unobstructed by walls or obstacles.
    /// Performs robust multi-point direct raycasts and spherecasts against all wall/obstacle layers.
    /// If ANY wall or obstacle collider lies between the enemy and player, detection is blocked.
    /// </summary>
    public bool CheckLineOfSight()
    {
        if (!playerTarget) return false;

        Vector3 enemyBase = transform.position;
        Vector3 playerBase = playerTarget.position;
        Vector3 flatDiff = playerBase - enemyBase;
        flatDiff.y = 0f;
        float flatDistance = flatDiff.magnitude;

        if (flatDistance <= 0.001f)
        {
            _hadObstacleHit = false;
            return true;
        }

        // 1. Dual Zone Check:
        // A) Close Proximity 360-degree circle (e.g. 1.2m in ANY direction)
        bool inProximity = flatDistance <= proximityRadius;

        // B) Forward Radial Vision Cone (e.g. 7m within viewAngle)
        bool inForwardCone = false;
        if (!inProximity && flatDistance <= detectionRadius)
        {
            if (useFieldOfView)
            {
                Vector3 facingDir = transform.forward;
                facingDir.y = 0f;
                if (facingDir.sqrMagnitude > 0.001f)
                {
                    facingDir.Normalize();
                    Vector3 flatToPlayer = flatDiff.normalized;
                    float angle = Vector3.Angle(facingDir, flatToPlayer);
                    inForwardCone = angle <= (viewAngle * 0.5f);
                }
                else
                {
                    inForwardCone = true;
                }
            }
            else
            {
                inForwardCone = true;
            }
        }

        // If player is outside both zones -> Not in range
        if (!inProximity && !inForwardCone)
        {
            _hadObstacleHit = false;
            return false;
        }

        // 2. Obstacle / Wall Sightline Check:
        // Even if in proximity or forward cone, any wall between enemy and player MUST BLOCK sight.
        int mask = GetEffectiveObstacleMask();

        Vector3 primaryOrigin = enemyBase + eyeOffset;
        Vector3 primaryTarget = playerBase + playerTargetOffset;

        // Primary direct sightline check (eye level to player center)
        if (IsRayBlockedByObstacle(primaryOrigin, primaryTarget, mask))
        {
            // Primary sightline is directly blocked by a wall/obstacle
            return false;
        }

        // Additional body points check (head and feet)
        Vector3 playerHead = playerBase + playerTargetOffset + new Vector3(0f, 0.2f, 0f);
        Vector3 playerFeet = playerBase + new Vector3(0f, 0.15f, 0f);

        bool headBlocked = IsRayBlockedByObstacle(primaryOrigin, playerHead, mask);
        bool feetBlocked = IsRayBlockedByObstacle(primaryOrigin, playerFeet, mask);

        // Also check from a lower enemy eye height (0.2m) to prevent skimming over low obstacles
        Vector3 lowOrigin = enemyBase + new Vector3(0f, 0.2f, 0f);
        bool lowCenterBlocked = IsRayBlockedByObstacle(lowOrigin, primaryTarget, mask);

        if (lowCenterBlocked && (headBlocked || feetBlocked))
        {
            return false;
        }

        // Line of sight is completely clear!
        _hadObstacleHit = false;
        return true;
    }

    /// <summary>
    /// Performs direct RaycastAll and SphereCastAll from origin to target.
    /// Returns true if any obstacle/wall collider is hit between origin and target.
    /// </summary>
    private bool IsRayBlockedByObstacle(Vector3 origin, Vector3 target, int mask)
    {
        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.01f) return false;

        Vector3 normDir = dir.normalized;

        // A) Direct line raycast
        RaycastHit[] hits = Physics.RaycastAll(origin, normDir, dist, mask, triggerInteraction);
        if (hits != null && hits.Length > 0)
        {
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (playerTarget != null && (hit.transform == playerTarget || hit.transform.IsChildOf(playerTarget))) continue;

                // An obstacle wall is between enemy and player!
                _lastObstacleHit = hit;
                _hadObstacleHit = true;
                return true;
            }
        }

        // B) Thick volume SphereCast check (catches thin edges, mesh seams, and corners)
        RaycastHit[] sphereHits = Physics.SphereCastAll(origin, 0.12f, normDir, dist, mask, triggerInteraction);
        if (sphereHits != null && sphereHits.Length > 0)
        {
            Array.Sort(sphereHits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < sphereHits.Length; i++)
            {
                var hit = sphereHits[i];
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (playerTarget != null && (hit.transform == playerTarget || hit.transform.IsChildOf(playerTarget))) continue;

                _lastObstacleHit = hit;
                _hadObstacleHit = true;
                return true;
            }
        }

        return false;
    }

    private void CleanWaypointsList()
    {
        for (int i = waypoints.Count - 1; i >= 0; i--)
        {
            if (waypoints[i] == null) waypoints.RemoveAt(i);
        }
    }

    private void EnsurePlayerReference()
    {
        if (playerTarget) return;

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p)
        {
            playerTarget = p.transform;
            return;
        }

        var pm = FindFirstObjectByType<PlayerMovement>();
        if (pm)
        {
            playerTarget = pm.transform;
            return;
        }

        var pObj = GameObject.Find("Player");
        if (pObj)
        {
            playerTarget = pObj.transform;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 enemyEyePos = transform.position + eyeOffset;
        Vector3 facing = transform.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.001f) facing.Normalize();
        else facing = Vector3.forward;

        // 1. Draw Waypoints & Patrol Route (Cyan/Green) if Not Stationary
        if (!isStationary && waypoints != null && waypoints.Count > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] == null) continue;
                Vector3 wpPos = waypoints[i].position;
                Gizmos.DrawWireSphere(wpPos, waypointReachDistance);

                if (i < waypoints.Count - 1 && waypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(wpPos, waypoints[i + 1].position);
                }
                else if (patrolType == PatrolType.Cyclic && waypoints[0] != null && waypoints.Count > 2)
                {
                    Gizmos.color = new Color(0f, 0.8f, 1f, 0.4f);
                    Gizmos.DrawLine(wpPos, waypoints[0].position);
                    Gizmos.color = Color.cyan;
                }
            }

            if (_currentWaypointIndex < waypoints.Count && waypoints[_currentWaypointIndex] != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, waypoints[_currentWaypointIndex].position);
            }
        }

        // 2. Draw Forward Wall Probe Ray (Only if not stationary)
        if (!isStationary)
        {
            Gizmos.color = _currentState == EnemyState.BlockedByWall ? Color.magenta : new Color(1f, 0.5f, 0f, 0.6f);
            Gizmos.DrawRay(enemyEyePos, facing * wallProbeDistance);
            Gizmos.DrawWireSphere(enemyEyePos + facing * wallProbeDistance, wallProbeRadius);
        }

        // 3. Draw Close 360-degree Proximity Circle
        Gizmos.color = isStationary ? new Color(0.8f, 0.2f, 1f, 0.5f) : new Color(0.2f, 1f, 0.4f, 0.5f);
        DrawWireCircle(transform.position, proximityRadius);

        // 4. Draw Forward Detection Radius Circle & Cone
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.3f);
        DrawWireCircle(transform.position, detectionRadius);

        if (useFieldOfView && viewAngle < 360f)
        {
            Gizmos.color = _isPlayerDetected ? Color.red : (_isPlayerInSight ? Color.yellow : new Color(1f, 0.92f, 0.016f, 0.3f));
            float halfAngle = viewAngle * 0.5f;

            Quaternion leftRot = Quaternion.Euler(0f, -halfAngle, 0f);
            Quaternion rightRot = Quaternion.Euler(0f, halfAngle, 0f);

            Vector3 leftDir = leftRot * facing;
            Vector3 rightDir = rightRot * facing;

            Gizmos.DrawRay(enemyEyePos, leftDir * detectionRadius);
            Gizmos.DrawRay(enemyEyePos, rightDir * detectionRadius);
        }

        // 5. Draw Sightline to Player
        if (playerTarget != null)
        {
            Vector3 playerCenterPos = playerTarget.position + playerTargetOffset;

            if (_isPlayerDetected && _isPlayerInSight)
            {
                // Active detection -> Solid Red line & sphere
                Gizmos.color = Color.red;
                Gizmos.DrawLine(enemyEyePos, playerCenterPos);
                Gizmos.DrawWireSphere(playerCenterPos, 0.35f);
            }
            else if (_isPlayerInSight)
            {
                // In sight, building timer -> Yellow
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(enemyEyePos, playerCenterPos);
                Gizmos.DrawWireSphere(playerCenterPos, 0.2f);
            }
            else if (_hadObstacleHit)
            {
                // Blocked by obstacle (wall) -> Red line to obstacle, Gray to player
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
                Gizmos.DrawLine(enemyEyePos, _lastObstacleHit.point);
                Gizmos.DrawWireSphere(_lastObstacleHit.point, 0.15f);

                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                Gizmos.DrawLine(_lastObstacleHit.point, playerCenterPos);
            }
        }
    }

    private void DrawWireCircle(Vector3 center, float radius, int segments = 32)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(Mathf.Sin(0f) * radius, 0f, Mathf.Cos(0f) * radius);

        for (int i = 1; i <= segments; i++)
        {
            float rad = i * angleStep * Mathf.Deg2Rad;
            Vector3 nextPoint = center + new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }
}
