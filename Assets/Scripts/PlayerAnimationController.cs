using UnityEngine;
using KKK.UI;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private Animator animator;

    // 8-Directional Idle States
    // 0: South, 1: South-East, 2: East, 3: North-East,
    // 4: North, 5: North-West, 6: West, 7: South-West
    private readonly string[] _idleStates = new string[]
    {
        "Idle_S", "Idle_SE", "Idle_E", "Idle_NE",
        "Idle_N", "Idle_NW", "Idle_W", "Idle_SW"
    };

    // 4-Directional Walk States (Cardinal: South, East, North, West)
    // Diagonal walking animations (NE, NW, SE, SW) are removed and map to cardinals.
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

        // Determine which animation state to play:
        // - Idle uses full 8-way directional states.
        // - Walk uses 4-way cardinal states (S, E, N, W) with diagonal mapping.
        string targetState = isMoving 
            ? Get4WayWalkState(_currentDirection, input)
            : _idleStates[_currentDirection];

        // Only trigger Play when the state actually changes
        if (targetState != _currentPlayingState)
        {
            _currentPlayingState = targetState;
            animator.Play(targetState, 0, 0f);
        }

        _wasMoving = isMoving;
    }

    /// <summary>
    /// Maps 8-way facing direction to one of the 4 cardinal walk states (Walk_S, Walk_E, Walk_N, Walk_W).
    /// Diagonal directions (NE, NW, SE, SW) resolve to North/East or South/West based on dominant input axis.
    /// </summary>
    private string Get4WayWalkState(int dirIndex, Vector2 input)
    {
        switch (dirIndex)
        {
            case 0: return "Walk_S"; // South
            case 2: return "Walk_E"; // East
            case 4: return "Walk_N"; // North
            case 6: return "Walk_W"; // West

            case 1: // South-East -> South or East
                return Mathf.Abs(input.x) > Mathf.Abs(input.y) ? "Walk_E" : "Walk_S";
            case 3: // North-East -> North or East
                return Mathf.Abs(input.x) > Mathf.Abs(input.y) ? "Walk_E" : "Walk_N";
            case 5: // North-West -> North or West
                return Mathf.Abs(input.x) > Mathf.Abs(input.y) ? "Walk_W" : "Walk_N";
            case 7: // South-West -> South or West
                return Mathf.Abs(input.x) > Mathf.Abs(input.y) ? "Walk_W" : "Walk_S";

            default:
                return "Walk_S";
        }
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