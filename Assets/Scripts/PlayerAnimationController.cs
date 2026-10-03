using UnityEngine;
using KKK.UI;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private Animator animator;

    // Names of your animation states in the Animator:
    // 0: South, 1: South-East, 2: East, 3: North-East,
    // 4: North, 5: North-West, 6: West, 7: South-West
    private readonly string[] _idleStates = new string[]
    {
        "Idle_S", "Idle_SE", "Idle_E", "Idle_NE",
        "Idle_N", "Idle_NW", "Idle_W", "Idle_SW"
    };

    private readonly string[] _walkStates = new string[]
    {
        "Walk_S", "Walk_SE", "Walk_E", "Walk_NE",
        "Walk_N", "Walk_NW", "Walk_W", "Walk_SW"
    };

    private int _currentDirection = 0; // Default facing South
    private bool _wasMoving = false;
    private string _currentPlayingState = "";

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (animator == null) return;

        bool isPaused = Time.timeScale <= 0f || (PauseMenuController.Instance != null && PauseMenuController.Instance.IsPaused);
        bool isDialogueActive = DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive;

        if (isPaused || isDialogueActive)
        {
            if (_wasMoving)
            {
                string idleState = _idleStates[_currentDirection];
                if (idleState != _currentPlayingState)
                {
                    _currentPlayingState = idleState;
                    animator.Play(idleState, 0, 0f);
                }
                _wasMoving = false;
            }
            return;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector2 input = new Vector2(h, v);
        bool isMoving = input.sqrMagnitude > 0.01f;

        if (isMoving)
        {
            _currentDirection = Get8WayDirectionIndex(input);
        }

        // Determine which animation state to play
        string targetState = isMoving 
            ? _walkStates[_currentDirection] 
            : _idleStates[_currentDirection];

        // Only trigger Play when the state actually changes
        if (targetState != _currentPlayingState)
        {
            _currentPlayingState = targetState;
            animator.Play(targetState, 0, 0f);
        }

        _wasMoving = isMoving;
    }

    private int Get8WayDirectionIndex(Vector2 dir)
    {
        // Calculate angle where 0 degrees is East, 90 is North
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        // 8 sectors of 45 degrees, centered on cardinals and diagonals:
        // East (0 deg) -> Index 2
        // North-East (45 deg) -> Index 3
        // North (90 deg) -> Index 4
        // North-West (135 deg) -> Index 5
        // West (180 deg) -> Index 6
        // South-West (225 deg) -> Index 7
        // South (270 deg) -> Index 0
        // South-East (315 deg) -> Index 1

        if (angle >= 247.5f && angle < 292.5f) return 0; // S
        if (angle >= 292.5f && angle < 337.5f) return 1; // SE
        if (angle >= 337.5f || angle < 22.5f)  return 2; // East
        if (angle >= 22.5f && angle < 67.5f)   return 3; // NE
        if (angle >= 67.5f && angle < 112.5f)  return 4; // N
        if (angle >= 112.5f && angle < 157.5f) return 5; // NW
        if (angle >= 157.5f && angle < 202.5f) return 6; // W
        return 7;                                        // SW
    }
}