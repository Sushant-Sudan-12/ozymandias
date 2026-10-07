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

    public enum BehaviorType
    {
        [Tooltip("Patrols along designated waypoints (A -> B -> C...).")]
        PatrolWaypoints,

        [Tooltip("Completely stationary in place. Detects player and fades screen without moving.")]
        StationaryGhost,

        [Tooltip("Random Collision: Moves forward until colliding directly with a wall, then turns into a new open direction and continues. Chases player whenever detected.")]
        RandomCollision
    }

    [Header("Enemy Behavior Type (Stationary / Waypoints / Random Collision)")]
    [Tooltip("Behavior mode for this enemy: PatrolWaypoints, StationaryGhost, or RandomCollision.")]
    [SerializeField] private BehaviorType behaviorType = BehaviorType.PatrolWaypoints;

    [Tooltip("Legacy fallback for stationary ghost. If true, overrides behavior to StationaryGhost.")]
    [SerializeField] private bool isStationary = false;

    [Tooltip("If stationary, whether the ghost rotates to face the player while detecting them.")]
    [SerializeField] private bool rotateToFacePlayerWhileStationary = true;

    [Header("Random Collision Mode Settings")]
    [Tooltip("Normal movement speed in RandomCollision wandering mode.")]
    [SerializeField] private float randomPatrolSpeed = 3.2f;

    [Tooltip("Pause duration (in seconds) when hitting a wall in RandomCollision mode before setting off in new direction (0 = continuous fluid motion).")]
    [SerializeField] private float randomWallBounceWaitTime = 0.0f;

    [Tooltip("Minimum duration (in seconds) to walk in a chosen direction before scouting a fresh path.")]
    [SerializeField] private float minRandomWanderDuration = 1.2f;

    [Tooltip("Maximum duration (in seconds) to walk in a chosen direction before scouting a fresh path.")]
    [SerializeField] private float maxRandomWanderDuration = 3.2f;

    [Tooltip("Whether the enemy actively hunts and tracks the player's blood scent / trail when wandering in RandomCollision mode.")]
    [SerializeField] private bool searchPlayerBloodScent = true;

    [Tooltip("Radius (in meters) within which the enemy can smell the player's blood trail and bias wandering towards them.")]
    [SerializeField] private float bloodScentRadius = 18f;

    [Tooltip("Strength of the blood scent tracking bias (0 = pure random wandering, 1 = aggressive blood trail hunting).")]
    [Range(0f, 1f)]
    [SerializeField] private float bloodScentBiasStrength = 0.85f;

    [Tooltip("Angular spread in degrees when bouncing off a wall (e.g. 120 degrees).")]
    [SerializeField] private float bounceAngleSpread = 120f;

    [Header("Patrol & Waypoints (A, B, C, D...) - Active if Not Stationary")]
    [Tooltip("List of waypoint transforms to patrol between (e.g. A and B, or A, B, C, D).")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();

    [Tooltip("Movement pattern across waypoints (PingPong / Cyclic / SingleRun).")]
    [SerializeField] private PatrolType patrolType = PatrolType.PingPong;

    [Tooltip("Normal patrol movement speed between A and B.")]
    [SerializeField] private float patrolSpeed = 3.5f;

    [Tooltip("Distance threshold to consider a waypoint reached.")]
    [SerializeField] private float waypointReachDistance = 0.35f;

    [Tooltip("Pause duration (in seconds) upon reaching each waypoint.")]
    [SerializeField] private float waitTimeAtWaypoint = 0.4f;

    [Tooltip("Speed multiplier at which the 3D enemy rotates to face movement direction or the player.")]
    [SerializeField] private float rotationSpeed = 30f;

    [Tooltip("Crisp turn speed in degrees per second (e.g. 1100 = 180° turn in 0.16s, voracious and snappy).")]
    [SerializeField] private float turnDegreesPerSecond = 1100f;

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
    [SerializeField] private float detectionTimeRequired = 1.5f;

    [Tooltip("Whether to reload the active scene upon player death.")]
    [SerializeField] private bool reloadSceneOnDeath = true;

    [Header("Enemy Chase Audio")]
    [Tooltip("Sound played while this enemy is sensing / chasing the player during the detection window (e.g. 1.5s).")]
    [SerializeField] private AudioClip enemyChaseSound;

    [Range(0f, 1f)]
    [Tooltip("Volume slider for the enemy chase sound.")]
    [SerializeField] private float enemyChaseVolume = 1.0f;

    [Tooltip("AudioSource component used to play the chase sound. Auto-created if unassigned.")]
    [SerializeField] private AudioSource chaseAudioSource;

    [Header("Player Chase (When Moving Enemy is Detected)")]
    [Tooltip("Chase speed when player is detected (faster than normal patrol speed). Ignored if isStationary is true.")]
    [SerializeField] private float chaseSpeed = 5.0f;

    [Tooltip("Stopping distance from player when reaching them.")]
    [SerializeField] private float chaseStopDistance = 0.9f;

    [Header("Wall Collision / Anti-Clipping Settings")]
    [Tooltip("Distance ahead to probe for walls/obstacles blocking movement.")]
    [SerializeField] private float wallProbeDistance = 0.75f;

    [Tooltip("SphereCast radius used to detect obstacles in front of the enemy.")]
    [SerializeField] private float wallProbeRadius = 0.35f;

    [Tooltip("Brief pause (in seconds) when hitting a dynamic wall before turning around to return.")]
    [SerializeField] private float turnAroundWaitTime = 0.05f;

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

    // Waypoint Obstacle Retreat & Detour State
    private bool _isRetreatingFromWall = false;
    private int _retryTargetWaypointIndex = -1;
    private bool _isDetourWandering = false;

    private float _currentDetectionTimer = 0f;
    private bool _isPlayerInSight = false;
    private bool _isPlayerDetected = false;
    private RaycastHit _lastObstacleHit;
    private bool _hadObstacleHit = false;

    // Random Collision Wandering & Anti-Stuck State
    private Vector3 _currentRandomDirection = Vector3.forward;
    private float _randomWanderTimer = 0f;
    private Vector3 _stuckSamplePosition = Vector3.zero;
    private float _stuckTimer = 0f;

    // Animator Hashes
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsChasingHash = Animator.StringToHash("IsChasing");

    // Public Getters
    public BehaviorType EffectiveBehaviorType => isStationary ? BehaviorType.StationaryGhost : behaviorType;
    public bool IsStationary => EffectiveBehaviorType == BehaviorType.StationaryGhost;
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
        StopChaseAudio();
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

        _controller = GetComponent<CharacterController>();
        if (_controller != null)
        {
            _controller.stepOffset = Mathf.Min(_controller.stepOffset, 0.2f);
            _controller.skinWidth = Mathf.Max(_controller.skinWidth, 0.05f);
        }

        if (!animator) animator = GetComponentInChildren<Animator>();

        if (fadeScreenCanvasGroup != null) s_SharedCanvasGroup = fadeScreenCanvasGroup;
        if (fadeScreenImage != null) s_SharedImage = fadeScreenImage;

        _currentRandomDirection = transform.forward;
        _currentRandomDirection.y = 0f;
        if (_currentRandomDirection.sqrMagnitude < 0.001f) _currentRandomDirection = Vector3.forward;
        _currentRandomDirection.Normalize();
        _randomWanderTimer = UnityEngine.Random.Range(minRandomWanderDuration, maxRandomWanderDuration);

        _stuckSamplePosition = transform.position;
        _stuckTimer = 0f;

        InitializeChaseAudioSource();
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
        bool isPaused = Time.timeScale <= 0f || (KKK.UI.PauseMenuController.Instance != null && KKK.UI.PauseMenuController.Instance.IsPaused);
        bool isDialogueActive = KKK.UI.DialogueManager.Instance != null && KKK.UI.DialogueManager.Instance.IsDialogueActive;

        if (isPaused || isDialogueActive)
        {
            StopChaseAudio();

            if (isDialogueActive)
            {
                _isPlayerInSight = false;
                _currentDetectionTimer = 0f;
                _isPlayerDetected = false;
                OnDetectionProgressChanged?.Invoke(0f);
            }

            ApplyPhysicsMovement(Vector3.zero);
            return;
        }

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
        bool isPaused = Time.timeScale <= 0f || (KKK.UI.PauseMenuController.Instance != null && KKK.UI.PauseMenuController.Instance.IsPaused);
        bool isDialogueActive = KKK.UI.DialogueManager.Instance != null && KKK.UI.DialogueManager.Instance.IsDialogueActive;

        if (isPaused || isDialogueActive)
        {
            if (isDialogueActive && s_CurrentGlobalScreenAlpha > 0f)
            {
                s_CurrentGlobalScreenAlpha = 0f;
                ApplyFadeAlpha(0f);
            }
            return;
        }

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

            PlayChaseAudio();
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

            StopChaseAudio();
        }
    }

    /// <summary>
    /// Evaluates all active enemies in the scene. If at least one enemy sees the player,
    /// the screen smoothly transitions 0 -> 1 over detectionTimeRequired seconds.
    /// The moment no enemy sees the player (wall or out of range), screen alpha IMMEDIATELY resets to 0.
    /// </summary>
    private void CoordinateSharedScreenFade()
    {
        float maxProgress = 0f;

        // Clean nulls and find highest detection progress among all active enemies
        for (int i = s_ActiveEnemies.Count - 1; i >= 0; i--)
        {
            if (s_ActiveEnemies[i] == null)
            {
                s_ActiveEnemies.RemoveAt(i);
                continue;
            }

            var enemy = s_ActiveEnemies[i];
            if (enemy.gameObject.activeInHierarchy && enemy._isPlayerInSight)
            {
                float p = enemy.DetectionProgress;
                if (p > maxProgress) maxProgress = p;
            }
        }

        // Also check this enemy instance directly
        if (_isPlayerInSight)
        {
            float p = DetectionProgress;
            if (p > maxProgress) maxProgress = p;
        }

        // Apply fade
        if (maxProgress > 0.001f)
        {
            s_CurrentGlobalScreenAlpha = maxProgress;
            ApplyFadeAlpha(s_CurrentGlobalScreenAlpha);

            // Full detection reached (e.g. 1.5s / 1.0 alpha) -> Player dies & scene reloads
            if (maxProgress >= 0.999f && !s_IsReloadingScene)
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
        else
        {
            if (s_CurrentGlobalScreenAlpha > 0f)
            {
                s_CurrentGlobalScreenAlpha = 0f;
                ApplyFadeAlpha(0f);
            }
        }
    }

    private void InitializeChaseAudioSource()
    {
        if (chaseAudioSource == null)
        {
            chaseAudioSource = GetComponent<AudioSource>();
            if (chaseAudioSource == null)
            {
                chaseAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        chaseAudioSource.playOnAwake = false;
        chaseAudioSource.loop = true;
        chaseAudioSource.spatialBlend = 0f; // 2D audio for clear chase tension
    }

    private void PlayChaseAudio()
    {
        if (enemyChaseSound == null) return;
        if (chaseAudioSource == null) InitializeChaseAudioSource();

        if (chaseAudioSource.clip != enemyChaseSound)
        {
            chaseAudioSource.clip = enemyChaseSound;
        }

        chaseAudioSource.loop = true;
        chaseAudioSource.volume = enemyChaseVolume;

        if (!chaseAudioSource.isPlaying)
        {
            chaseAudioSource.Play();
        }
    }

    private void StopChaseAudio()
    {
        if (chaseAudioSource != null && chaseAudioSource.isPlaying)
        {
            chaseAudioSource.Stop();
        }
    }

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

        // 2. Check scene CanvasGroups with 'fade' in name
        var allGroups = FindObjectsByType<CanvasGroup>(FindObjectsSortMode.None);
        for (int i = 0; i < allGroups.Length; i++)
        {
            if (allGroups[i] != null && allGroups[i].gameObject.name.IndexOf("fade", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                s_SharedCanvasGroup = allGroups[i];
                return;
            }
        }

        // 3. Check scene Images with 'fade' in name
        var allImages = FindObjectsByType<Image>(FindObjectsSortMode.None);
        for (int i = 0; i < allImages.Length; i++)
        {
            if (allImages[i] != null && allImages[i].gameObject.name.IndexOf("fade", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                s_SharedImage = allImages[i];
                SetupFadeImageProperties(s_SharedImage);
                return;
            }
        }

        // 4. Auto-create dedicated FadeCanvas overlay if missing (for standalone level testing)
        CreateFallbackFadeOverlay();
    }

    private static void CreateFallbackFadeOverlay()
    {
        if (s_SharedImage != null || s_SharedCanvasGroup != null) return;

        GameObject canvasGo = new GameObject("Runtime_EnemyFadeCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 9999;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject imageGo = new GameObject("FadeScreen");
        imageGo.transform.SetParent(canvasGo.transform, false);

        s_SharedImage = imageGo.AddComponent<Image>();
        SetupFadeImageProperties(s_SharedImage);
        s_SharedImage.color = new Color(0f, 0f, 0f, 0f);
        imageGo.SetActive(false);
    }

    private static void SetupFadeImageProperties(Image img)
    {
        if (img == null) return;
        img.sprite = null;
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        img.color = new Color(0f, 0f, 0f, img.color.a);

        RectTransform rt = img.rectTransform;
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.offsetMin = new Vector2(-200f, -200f);
            rt.offsetMax = new Vector2(200f, 200f);
        }
    }

    private static void ApplyFadeAlpha(float alpha)
    {
        if (KKK.UI.PauseMenuController.Instance != null)
        {
            KKK.UI.PauseMenuController.SetDetectionFade(alpha);
        }

        ResolveSharedFadeScreen();

        bool isVisible = alpha > 0.001f;

        if (s_SharedCanvasGroup != null)
        {
            if (s_SharedCanvasGroup.gameObject.activeSelf != isVisible)
            {
                s_SharedCanvasGroup.gameObject.SetActive(isVisible);
            }
            s_SharedCanvasGroup.alpha = alpha;
            s_SharedCanvasGroup.blocksRaycasts = (alpha > 0.05f);
        }

        if (s_SharedImage != null)
        {
            if (s_SharedImage.gameObject.activeSelf != isVisible)
            {
                s_SharedImage.gameObject.SetActive(isVisible);
            }
            Color c = s_SharedImage.color;
            c.r = 0f;
            c.g = 0f;
            c.b = 0f;
            c.a = alpha;
            s_SharedImage.color = c;
        }
    }

    private void UpdateBehaviorAndMovement()
    {
        BehaviorType currentBehavior = EffectiveBehaviorType;

        // =========================================================================
        // MODE 1: STATIONARY GHOST (Does not move or chase, only detects & fades)
        // =========================================================================
        if (currentBehavior == BehaviorType.StationaryGhost)
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

        Vector3 moveVelocity = Vector3.zero;

        // =========================================================================
        // CHASE MODE: SIGHT ACTIVE ON MOVING ENEMY (Both Waypoints & RandomCollision)
        // =========================================================================
        if (_isPlayerInSight && playerTarget != null)
        {
            _currentState = EnemyState.ChasingPlayer;

            Vector3 targetPos = playerTarget.position;
            Vector3 diff = targetPos - transform.position;
            diff.y = 0f;
            float distToPlayer = diff.magnitude;

            if (distToPlayer > chaseStopDistance)
            {
                Vector3 desiredDir = diff.normalized;
                Vector3 steeredDir = GetSteeredMoveDirection(desiredDir, distToPlayer, out _);

                RotateTowards(steeredDir);

                Vector3 desiredVelocity = steeredDir * chaseSpeed;
                moveVelocity = PreventWallClipping(desiredVelocity);
            }
            else
            {
                Vector3 moveDir = diff.normalized;
                if (moveDir.sqrMagnitude > 0.001f) RotateTowards(moveDir);
                moveVelocity = Vector3.zero;
            }
        }
        // =========================================================================
        // MODE 2: RANDOM COLLISION (Wanders relentlessly, bounces off walls, chases on sight, tracks blood scent)
        // =========================================================================
        else if (currentBehavior == BehaviorType.RandomCollision)
        {
            _currentState = EnemyState.Patrolling;

            // Anti-Stuck Watchdog: if enemy gets physically wedged in a corner or mesh seam
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer >= 0.2f)
            {
                Vector3 flatCurrentPos = new Vector3(transform.position.x, 0f, transform.position.z);
                Vector3 flatLastPos = new Vector3(_stuckSamplePosition.x, 0f, _stuckSamplePosition.z);
                float distMoved = Vector3.Distance(flatCurrentPos, flatLastPos);
                _stuckSamplePosition = transform.position;
                _stuckTimer = 0f;

                if (distMoved < 0.04f)
                {
                    // Force an instant bounce/turn to un-wedge
                    HandleRandomCollision(-transform.forward);
                }
            }

            // Ensure initial direction
            if (_currentRandomDirection.sqrMagnitude < 0.001f)
            {
                _currentRandomDirection = transform.forward;
                _currentRandomDirection.y = 0f;
                if (_currentRandomDirection.sqrMagnitude < 0.001f) _currentRandomDirection = Vector3.forward;
                _currentRandomDirection.Normalize();
            }

            // Periodic voracious scouting & player blood scent search
            _randomWanderTimer -= Time.deltaTime;
            if (_randomWanderTimer <= 0f)
            {
                _randomWanderTimer = UnityEngine.Random.Range(minRandomWanderDuration, maxRandomWanderDuration);
                _currentRandomDirection = PickScoutOrBloodScentDirection(_currentRandomDirection);
            }

            // Check if directly blocked ahead or if we can steer slightly through narrow paths
            Vector3 steeredDir = GetSteeredMoveDirection(_currentRandomDirection, wallProbeDistance, out bool isCompletelyBlocked);

            if (isCompletelyBlocked)
            {
                // Directly collided with a wall in front -> instant voracious turn and keep moving!
                RaycastHit obstacleHit;
                IsDirectionClear(_currentRandomDirection, wallProbeDistance, out obstacleHit);
                HandleRandomCollision(obstacleHit.normal != Vector3.zero ? obstacleHit.normal : -transform.forward);

                Vector3 desiredVelocity = _currentRandomDirection * randomPatrolSpeed;
                moveVelocity = PreventWallClipping(desiredVelocity);
            }
            else
            {
                _currentRandomDirection = steeredDir;
                RotateTowards(steeredDir);

                Vector3 desiredVelocity = steeredDir * randomPatrolSpeed;
                moveVelocity = PreventWallClipping(desiredVelocity);
            }
        }
        // =========================================================================
        // MODE 3: WAYPOINTS PATROL (A -> B -> C...)
        // =========================================================================
        else
        {
            CleanWaypointsList();

            if (waypoints.Count >= 2)
            {
                // Anti-Stuck Watchdog for PatrolWaypoints: if enemy is stuck against obstacle
                _stuckTimer += Time.deltaTime;
                if (_stuckTimer >= 0.25f)
                {
                    Vector3 flatCurrentPos = new Vector3(transform.position.x, 0f, transform.position.z);
                    Vector3 flatLastPos = new Vector3(_stuckSamplePosition.x, 0f, _stuckSamplePosition.z);
                    float distMoved = Vector3.Distance(flatCurrentPos, flatLastPos);
                    _stuckSamplePosition = transform.position;
                    _stuckTimer = 0f;

                    if (distMoved < 0.04f && _waitTimer <= 0f && _currentState != EnemyState.WaitingAtWaypoint)
                    {
                        // Physically stuck against a corner! Enter detour wander
                        EnterDetourWandering(-transform.forward);
                    }
                }

                // =========================================================================
                // DETOUR WANDER STATE: Navigate randomly until path to a waypoint is clear
                // =========================================================================
                if (_isDetourWandering)
                {
                    // Check if direct line-of-sight / steerable path to target waypoint or any other waypoint has opened
                    Transform targetWp = (_currentWaypointIndex >= 0 && _currentWaypointIndex < waypoints.Count) ? waypoints[_currentWaypointIndex] : null;
                    if (targetWp != null && CanSeeOrSteerToWaypoint(targetWp))
                    {
                        // Line to current target waypoint opened up! Return to path mode
                        _isDetourWandering = false;
                    }
                    else
                    {
                        // Check if any other waypoint on the patrol route has an open line
                        for (int i = 0; i < waypoints.Count; i++)
                        {
                            if (waypoints[i] != null && CanSeeOrSteerToWaypoint(waypoints[i]))
                            {
                                _currentWaypointIndex = i;
                                _isDetourWandering = false;
                                break;
                            }
                        }
                    }

                    if (_isDetourWandering)
                    {
                        // Actively wander in random mode while looking for an open line to path
                        _currentState = EnemyState.Patrolling;

                        _randomWanderTimer -= Time.deltaTime;
                        if (_randomWanderTimer <= 0f)
                        {
                            _randomWanderTimer = UnityEngine.Random.Range(minRandomWanderDuration, maxRandomWanderDuration);
                            _currentRandomDirection = PickScoutOrBloodScentDirection(_currentRandomDirection);
                        }

                        Vector3 steeredDir = GetSteeredMoveDirection(_currentRandomDirection, wallProbeDistance, out bool isCompletelyBlocked);

                        if (isCompletelyBlocked)
                        {
                            RaycastHit obstacleHit;
                            IsDirectionClear(_currentRandomDirection, wallProbeDistance, out obstacleHit);
                            HandleRandomCollision(obstacleHit.normal != Vector3.zero ? obstacleHit.normal : -transform.forward);

                            Vector3 desiredVelocity = _currentRandomDirection * patrolSpeed;
                            moveVelocity = PreventWallClipping(desiredVelocity);
                        }
                        else
                        {
                            _currentRandomDirection = steeredDir;
                            RotateTowards(steeredDir);

                            Vector3 desiredVelocity = steeredDir * patrolSpeed;
                            moveVelocity = PreventWallClipping(desiredVelocity);
                        }
                    }
                }

                // =========================================================================
                // NORMAL WAYPOINT PATROL STATE (A -> B -> C...)
                // =========================================================================
                if (!_isDetourWandering)
                {
                    // Handle wait at reached waypoint
                    if (_waitTimer > 0f)
                    {
                        _currentState = EnemyState.WaitingAtWaypoint;
                        _waitTimer -= Time.deltaTime;
                        moveVelocity = Vector3.zero;

                        // Actively turn to face the next destination waypoint while waiting!
                        Transform targetWp = waypoints[_currentWaypointIndex];
                        if (targetWp != null)
                        {
                            Vector3 diff = targetWp.position - transform.position;
                            diff.y = 0f;
                            if (diff.sqrMagnitude > 0.001f)
                            {
                                RotateTowards(diff.normalized);
                            }
                        }
                    }
                    // Actively moving to target waypoint along patrol route
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
                                AdvanceToNextWaypoint();
                                moveVelocity = Vector3.zero;

                                // Begin rotating to face the next waypoint immediately upon reaching this point
                                Transform newTargetWp = waypoints[_currentWaypointIndex];
                                if (newTargetWp != null)
                                {
                                    Vector3 nextDiff = newTargetWp.position - transform.position;
                                    nextDiff.y = 0f;
                                    if (nextDiff.sqrMagnitude > 0.001f)
                                    {
                                        RotateTowards(nextDiff.normalized, 1.5f);
                                    }
                                }
                            }
                            else
                            {
                                Vector3 desiredDir = diff.normalized;
                                Vector3 steeredDir = GetSteeredMoveDirection(desiredDir, dist, out bool isCompletelyBlocked);

                                if (isCompletelyBlocked)
                                {
                                    // Path to waypoint is blocked! Enter temporary random detour mode to find a way around
                                    RaycastHit obstacleHit;
                                    IsDirectionClear(desiredDir, wallProbeDistance, out obstacleHit);
                                    EnterDetourWandering(obstacleHit.normal != Vector3.zero ? obstacleHit.normal : -transform.forward);

                                    Vector3 desiredVelocity = _currentRandomDirection * patrolSpeed;
                                    moveVelocity = PreventWallClipping(desiredVelocity);
                                }
                                else
                                {
                                    RotateTowards(steeredDir);

                                    Vector3 desiredVelocity = steeredDir * patrolSpeed;
                                    moveVelocity = PreventWallClipping(desiredVelocity);
                                }
                            }
                        }
                    }
                }
            }
            else if (waypoints.Count == 1 && waypoints[0] != null)
            {
                Vector3 diff = waypoints[0].position - transform.position;
                diff.y = 0f;
                float dist = diff.magnitude;

                if (dist > waypointReachDistance)
                {
                    Vector3 desiredDir = diff.normalized;
                    Vector3 steeredDir = GetSteeredMoveDirection(desiredDir, dist, out bool isCompletelyBlocked);

                    if (!isCompletelyBlocked)
                    {
                        RotateTowards(steeredDir);
                        Vector3 desiredVelocity = steeredDir * patrolSpeed;
                        moveVelocity = PreventWallClipping(desiredVelocity);
                    }
                    else
                    {
                        EnterDetourWandering(-transform.forward);
                        Vector3 desiredVelocity = _currentRandomDirection * patrolSpeed;
                        moveVelocity = PreventWallClipping(desiredVelocity);
                    }
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
    /// Slides smoothly along wall surfaces or smoothly bounces in RandomCollision mode without halting.
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

            // If slide velocity points into the wall or is too small:
            if (Vector3.Dot(slideVelocity.normalized, normal) < 0.01f || slideVelocity.sqrMagnitude < 0.1f)
            {
                if (EffectiveBehaviorType == BehaviorType.RandomCollision || _isDetourWandering)
                {
                    HandleRandomCollision(normal);
                    float speed = EffectiveBehaviorType == BehaviorType.RandomCollision ? randomPatrolSpeed : patrolSpeed;
                    return _currentRandomDirection * speed;
                }
                return Vector3.zero;
            }

            return slideVelocity;
        }

        return desiredVelocity;
    }

    private void RotateTowards(Vector3 direction, float speedMultiplier = 1f)
    {
        if (direction.sqrMagnitude < 0.001f) return;
        direction.y = 0f;
        Quaternion targetRot = Quaternion.LookRotation(direction);
        float degSpeed = Mathf.Max(turnDegreesPerSecond, rotationSpeed * 40f) * speedMultiplier;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, degSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Intelligently steers towards targetDirection by evaluating direct line-of-sight first.
    /// If slightly obstructed by a side wall/corner in narrow pathways, tests small angle offsets
    /// (±15°, ±30°, ±45°, ±60°, ±75°) to shift around obstacles. Only flags isCompletelyBlocked if every forward angle is blocked.
    /// </summary>
    private Vector3 GetSteeredMoveDirection(Vector3 targetDirection, float checkDist, out bool isCompletelyBlocked)
    {
        isCompletelyBlocked = false;
        if (targetDirection.sqrMagnitude < 0.001f) return Vector3.zero;

        targetDirection.y = 0f;
        targetDirection.Normalize();

        float effectiveDist = Mathf.Min(wallProbeDistance, checkDist);

        // 1. Direct path test
        if (IsDirectionClear(targetDirection, effectiveDist, out _))
        {
            return targetDirection;
        }

        // 2. Whisker tests for narrow corridor cornering and side-wall shifts
        float[] testAngles = new float[] { 15f, -15f, 30f, -30f, 45f, -45f, 60f, -60f, 75f, -75f };
        for (int i = 0; i < testAngles.Length; i++)
        {
            Vector3 shiftedDir = Quaternion.Euler(0f, testAngles[i], 0f) * targetDirection;
            shiftedDir.y = 0f;
            shiftedDir.Normalize();

            if (IsDirectionClear(shiftedDir, effectiveDist * 0.9f, out _))
            {
                // Verify the shifted direction still makes forward progress towards destination
                if (Vector3.Dot(shiftedDir, targetDirection) > 0.25f)
                {
                    return shiftedDir;
                }
            }
        }

        // 3. If no forward/shifted angle is clear, path is directly and completely blocked
        isCompletelyBlocked = true;
        return targetDirection;
    }

    /// <summary>
    /// Evaluates candidate wander angles, actively searching for the player's blood scent if in range,
    /// or scouting wide-open corridor turns.
    /// </summary>
    private Vector3 PickScoutOrBloodScentDirection(Vector3 currentDir)
    {
        Vector3 enemyPos = transform.position;
        Vector3 bestDir = currentDir;
        float bestScore = -9999f;

        // Check if player is within blood scent range
        bool hasScent = false;
        Vector3 dirToPlayer = Vector3.forward;
        float scentFactor = 0f;

        if (searchPlayerBloodScent && playerTarget != null)
        {
            Vector3 diff = playerTarget.position - enemyPos;
            diff.y = 0f;
            float distToPlayer = diff.magnitude;
            if (distToPlayer > 0.01f && distToPlayer <= bloodScentRadius)
            {
                hasScent = true;
                dirToPlayer = diff.normalized;
                scentFactor = Mathf.Clamp01(1f - (distToPlayer / bloodScentRadius)) * bloodScentBiasStrength;
            }
        }

        // Test a range of scout angles
        float[] scoutAngles = new float[] { 0f, 25f, -25f, 45f, -45f, 70f, -70f, 90f, -90f, 120f, -120f };
        Vector3 probeOrigin = transform.position + eyeOffset;
        int mask = GetEffectiveObstacleMask();

        for (int i = 0; i < scoutAngles.Length; i++)
        {
            Vector3 testDir = Quaternion.Euler(0f, scoutAngles[i], 0f) * currentDir;
            testDir.y = 0f;
            testDir.Normalize();

            float clearDist = 12f;
            if (Physics.SphereCast(probeOrigin, wallProbeRadius, testDir, out RaycastHit hit, 12f, mask, triggerInteraction))
            {
                if (hit.transform != transform && !hit.transform.IsChildOf(transform))
                {
                    if (playerTarget == null || (hit.transform != playerTarget && !hit.transform.IsChildOf(playerTarget)))
                    {
                        clearDist = hit.distance;
                    }
                }
            }

            // Only consider directions with enough space to walk
            if (clearDist < wallProbeDistance * 1.2f) continue;

            float score = clearDist;

            // Forward momentum bonus
            score += Vector3.Dot(testDir, currentDir) * 1.5f;

            // Blood scent tracking bonus: strongly weights paths that lead towards the player
            if (hasScent)
            {
                float scentDot = Vector3.Dot(testDir, dirToPlayer);
                score += scentDot * (6.0f * scentFactor);
            }

            // Small random jitter to keep searching organic and unpredictable
            score += UnityEngine.Random.Range(0f, 1.2f);

            if (score > bestScore)
            {
                bestScore = score;
                bestDir = testDir;
            }
        }

        return bestDir;
    }

    /// <summary>
    /// Returns true if a probe in the given direction has no blocking wall colliders within checkDist.
    /// </summary>
    private bool IsDirectionClear(Vector3 dir, float checkDist, out RaycastHit obstacleHit)
    {
        Vector3 probeOrigin = transform.position + eyeOffset;
        float dist = Mathf.Max(0.05f, checkDist);
        int mask = GetEffectiveObstacleMask();

        if (Physics.SphereCast(probeOrigin, wallProbeRadius, dir, out obstacleHit, dist, mask, triggerInteraction))
        {
            if (obstacleHit.transform != transform && !obstacleHit.transform.IsChildOf(transform))
            {
                if (playerTarget == null || (obstacleHit.transform != playerTarget && !obstacleHit.transform.IsChildOf(playerTarget)))
                {
                    return false; // Hit a wall/obstacle
                }
            }
        }

        obstacleHit = default;
        return true;
    }

    /// <summary>
    /// Called when RandomCollision mode encounters a solid wall directly in front or gets stuck.
    /// Turns randomly between 90° and 270° away from current heading to pick a new open direction,
    /// biasing towards player blood scent if nearby, and immediately continues moving without stopping.
    /// </summary>
    private void HandleRandomCollision(Vector3 hitNormal)
    {
        _currentState = EnemyState.BlockedByWall;
        _blockedTimer = 0f; // Zero wait time: continuous fluid motion
        _randomWanderTimer = UnityEngine.Random.Range(minRandomWanderDuration, maxRandomWanderDuration);

        Vector3 currentFacing = transform.forward;
        currentFacing.y = 0f;
        if (currentFacing.sqrMagnitude < 0.001f) currentFacing = Vector3.forward;
        currentFacing.Normalize();

        Vector3 normal = hitNormal;
        normal.y = 0f;
        if (normal.sqrMagnitude > 0.001f) normal.Normalize();
        else normal = -currentFacing;

        // Check if player is within blood scent range
        bool hasScent = false;
        Vector3 dirToPlayer = Vector3.forward;
        float scentFactor = 0f;

        if (searchPlayerBloodScent && playerTarget != null)
        {
            Vector3 diff = playerTarget.position - transform.position;
            diff.y = 0f;
            float distToPlayer = diff.magnitude;
            if (distToPlayer > 0.01f && distToPlayer <= bloodScentRadius)
            {
                hasScent = true;
                dirToPlayer = diff.normalized;
                scentFactor = Mathf.Clamp01(1f - (distToPlayer / bloodScentRadius)) * bloodScentBiasStrength;
            }
        }

        // Generate candidate turn angles strictly between 90° and 270° at random
        float randomBaseAngle = UnityEngine.Random.Range(90f, 270f);
        float[] candidateAngles = new float[] {
            randomBaseAngle,
            Mathf.Clamp(randomBaseAngle + UnityEngine.Random.Range(-25f, 25f), 90f, 270f),
            Mathf.Clamp(randomBaseAngle + UnityEngine.Random.Range(-50f, 50f), 90f, 270f),
            UnityEngine.Random.Range(90f, 135f),   // Sharp right turn
            UnityEngine.Random.Range(225f, 270f),  // Sharp left turn
            UnityEngine.Random.Range(135f, 225f),  // Rear/diagonal turn
            UnityEngine.Random.Range(90f, 270f),
            UnityEngine.Random.Range(90f, 270f)
        };

        Vector3 probeOrigin = transform.position + eyeOffset;
        int mask = GetEffectiveObstacleMask();

        Vector3 bestDir = Quaternion.Euler(0f, randomBaseAngle, 0f) * currentFacing;
        bestDir.y = 0f;
        bestDir.Normalize();
        float maxScore = -9999f;

        for (int i = 0; i < candidateAngles.Length; i++)
        {
            Vector3 testDir = Quaternion.Euler(0f, candidateAngles[i], 0f) * currentFacing;
            testDir.y = 0f;
            testDir.Normalize();

            float clearDist = 15f;
            if (Physics.SphereCast(probeOrigin, wallProbeRadius, testDir, out RaycastHit hit, 15f, mask, triggerInteraction))
            {
                if (hit.transform != transform && !hit.transform.IsChildOf(transform))
                {
                    if (playerTarget == null || (hit.transform != playerTarget && !hit.transform.IsChildOf(playerTarget)))
                    {
                        clearDist = hit.distance;
                    }
                }
            }

            // Cap clearance distance to prevent straight hallways from always overriding lateral 90°-270° turns
            float cappedClearance = Mathf.Min(clearDist, 4.5f);
            float score = cappedClearance;

            // Penalize directions that directly crash into an adjacent wall
            if (clearDist < wallProbeDistance * 1.1f)
            {
                score -= 10f;
            }

            // Strongly reward pointing away from the obstacle/wall normal
            score += Vector3.Dot(testDir, normal) * 2.5f;

            // Strongly reward moving towards player's blood scent
            if (hasScent)
            {
                float scentDot = Vector3.Dot(testDir, dirToPlayer);
                score += scentDot * (5.0f * scentFactor);
            }

            // High random variance so turns dynamically span across 90°-270°
            score += UnityEngine.Random.Range(0f, 4.0f);

            if (score > maxScore)
            {
                maxScore = score;
                bestDir = testDir;
            }
        }

        _currentRandomDirection = bestDir;

        // Instantly execute rapid voracious rotation towards the new open heading
        RotateTowards(_currentRandomDirection, 4.0f);
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
    /// Enters temporary random collision detour wander when path to waypoint is blocked or enemy gets stuck.
    /// The moment an open path/line to any waypoint is discovered, automatically returns to waypoint patrol.
    /// </summary>
    private void EnterDetourWandering(Vector3 hitNormal = default)
    {
        _isDetourWandering = true;
        _currentState = EnemyState.Patrolling;
        _randomWanderTimer = UnityEngine.Random.Range(minRandomWanderDuration, maxRandomWanderDuration);

        if (hitNormal == default || hitNormal.sqrMagnitude < 0.001f)
        {
            hitNormal = -transform.forward;
        }

        HandleRandomCollision(hitNormal);
    }

    /// <summary>
    /// Checks whether an open, unobstructed direct sightline or steering corridor exists to the given waypoint.
    /// </summary>
    private bool CanSeeOrSteerToWaypoint(Transform wp)
    {
        if (wp == null) return false;

        Vector3 diff = wp.position - transform.position;
        diff.y = 0f;
        float dist = diff.magnitude;
        if (dist <= waypointReachDistance) return true;

        Vector3 dir = diff.normalized;
        Vector3 origin = transform.position + eyeOffset;
        int mask = GetEffectiveObstacleMask();

        // 1. Direct line of sight probe (SphereCast)
        if (!Physics.SphereCast(origin, wallProbeRadius * 0.75f, dir, out RaycastHit hit, dist, mask, triggerInteraction))
        {
            return true;
        }

        if (hit.transform == transform || hit.transform.IsChildOf(transform) || hit.transform == wp || hit.transform.IsChildOf(wp))
        {
            return true;
        }

        // 2. Whisker steering probe
        Vector3 steeredDir = GetSteeredMoveDirection(dir, dist, out bool isBlocked);
        if (!isBlocked && Vector3.Dot(steeredDir, dir) > 0.45f)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Called when a wall blocks the path between waypoints (e.g. between B and C).
    /// Turns the enemy around, retreats to origin waypoint (B), and once at B tries going to C again.
    /// </summary>
    private void HandleWallEncounter()
    {
        _isRetreatingFromWall = true;
        _retryTargetWaypointIndex = _currentWaypointIndex; // e.g. C

        // Retreat to origin waypoint (e.g. B)
        int retreatIndex = _previousWaypointIndex;
        if (retreatIndex < 0 || retreatIndex >= waypoints.Count || retreatIndex == _currentWaypointIndex)
        {
            retreatIndex = FindNearestWaypointExcluding(_currentWaypointIndex);
        }

        _currentWaypointIndex = retreatIndex;
        _blockedTimer = turnAroundWaitTime;

        // Instantly begin rapid rotation towards the retreat destination
        if (waypoints.Count > _currentWaypointIndex && waypoints[_currentWaypointIndex] != null)
        {
            Vector3 returnDiff = waypoints[_currentWaypointIndex].position - transform.position;
            returnDiff.y = 0f;
            if (returnDiff.sqrMagnitude > 0.001f)
            {
                RotateTowards(returnDiff.normalized, 3.0f);
            }
        }
    }

    private int FindNearestWaypointExcluding(int excludeIndex)
    {
        int bestIdx = 0;
        float minDist = float.MaxValue;
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (i == excludeIndex || waypoints[i] == null) continue;
            float d = Vector3.Distance(transform.position, waypoints[i].position);
            if (d < minDist)
            {
                minDist = d;
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    private void AdvanceToNextWaypoint()
    {
        if (waypoints.Count < 2) return;

        // If we retreated to origin waypoint (B) due to a wall block, now retry the intended target (C)!
        if (_isRetreatingFromWall && _retryTargetWaypointIndex >= 0 && _retryTargetWaypointIndex < waypoints.Count)
        {
            _isRetreatingFromWall = false;
            _previousWaypointIndex = _currentWaypointIndex;
            _currentWaypointIndex = _retryTargetWaypointIndex;
            _retryTargetWaypointIndex = -1;
            return;
        }

        _isRetreatingFromWall = false;
        _retryTargetWaypointIndex = -1;
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
        bool isPaused = Time.timeScale <= 0f || (KKK.UI.PauseMenuController.Instance != null && KKK.UI.PauseMenuController.Instance.IsPaused);
        bool isDialogueActive = KKK.UI.DialogueManager.Instance != null && KKK.UI.DialogueManager.Instance.IsDialogueActive;

        if (isPaused || isDialogueActive)
        {
            moveVelocity = Vector3.zero;
        }

        if (_controller != null)
        {
            if (isPaused || isDialogueActive)
            {
                // Freeze character controller displacement while in dialogue or paused
            }
            else
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
        }
        else
        {
            if (!isPaused && !isDialogueActive)
            {
                transform.position += moveVelocity * Time.deltaTime;
            }
        }

        // Update optional 3D Animator parameters
        if (animator != null)
        {
            float flatSpeed = (isPaused || isDialogueActive) ? 0f : new Vector3(moveVelocity.x, 0f, moveVelocity.z).magnitude;
            animator.SetFloat(SpeedHash, flatSpeed);
            animator.SetBool(IsMovingHash, flatSpeed > 0.05f);
            animator.SetBool(IsChasingHash, !isPaused && !isDialogueActive && _currentState == EnemyState.ChasingPlayer && flatSpeed > 0.05f);
        }

        var animEntity = GetComponent<DirectionalAnimationEntity>();
        if (animEntity != null)
        {
            animEntity.SetMovement((isPaused || isDialogueActive) ? Vector3.zero : moveVelocity);
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

        // 1. Draw Waypoints & Patrol Route (Cyan/Green) if in PatrolWaypoints mode
        if (EffectiveBehaviorType == BehaviorType.PatrolWaypoints && waypoints != null && waypoints.Count > 0)
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
                Gizmos.color = _isDetourWandering ? Color.yellow : Color.green;
                Gizmos.DrawLine(transform.position, waypoints[_currentWaypointIndex].position);
            }

            if (_isDetourWandering)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(enemyEyePos, _currentRandomDirection * (wallProbeDistance * 1.5f));
                Gizmos.DrawWireSphere(enemyEyePos + _currentRandomDirection * (wallProbeDistance * 1.5f), 0.15f);
            }
        }
        else if (EffectiveBehaviorType == BehaviorType.RandomCollision)
        {
            // Draw Random Collision Wander Heading
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(enemyEyePos, _currentRandomDirection * (wallProbeDistance * 1.5f));
            Gizmos.DrawWireSphere(enemyEyePos + _currentRandomDirection * (wallProbeDistance * 1.5f), 0.15f);

            // Draw Blood Scent hunting radius if enabled
            if (searchPlayerBloodScent)
            {
                Gizmos.color = new Color(0.9f, 0.1f, 0.2f, 0.2f);
                DrawWireCircle(transform.position, bloodScentRadius);
            }
        }

        // 2. Draw Forward Wall Probe Ray (Only if not stationary)
        if (EffectiveBehaviorType != BehaviorType.StationaryGhost)
        {
            Gizmos.color = _currentState == EnemyState.BlockedByWall ? Color.magenta : new Color(1f, 0.5f, 0f, 0.6f);
            Gizmos.DrawRay(enemyEyePos, facing * wallProbeDistance);
            Gizmos.DrawWireSphere(enemyEyePos + facing * wallProbeDistance, wallProbeRadius);
        }

        // 3. Draw Close 360-degree Proximity Circle
        Gizmos.color = EffectiveBehaviorType == BehaviorType.StationaryGhost ? new Color(0.8f, 0.2f, 1f, 0.5f) : new Color(0.2f, 1f, 0.4f, 0.5f);
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
