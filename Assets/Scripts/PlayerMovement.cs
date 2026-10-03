using UnityEngine;
using KKK.UI;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(DirectionalAnimationEntity))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float gravity = 20f;

    [Header("Camera Reference")]
    [SerializeField] private Camera playerCamera;

    private CharacterController _controller;
    private DirectionalAnimationEntity _animEntity;
    private Vector3 _verticalVelocity;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _animEntity = GetComponent<DirectionalAnimationEntity>();

        if (!playerCamera)
            playerCamera = GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        bool isPaused = Time.timeScale <= 0f || (PauseMenuController.Instance != null && PauseMenuController.Instance.IsPaused);
        bool isDialogueActive = DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive;

        if (isPaused || isDialogueActive)
        {
            if (_animEntity != null)
            {
                _animEntity.SetMovement(Vector3.zero);
            }
            return;
        }

        // Unrestricted movement input
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 inputDir = new Vector3(h, 0f, v).normalized;
        Vector3 moveDir = Vector3.zero;

        if (inputDir.sqrMagnitude > 0.001f && playerCamera != null)
        {
            Vector3 camForward = playerCamera.transform.forward;
            Vector3 camRight = playerCamera.transform.right;

            camForward.y = 0f;
            camRight.y = 0f;

            camForward.Normalize();
            camRight.Normalize();

            // Screen-relative movement
            moveDir = (camForward * inputDir.z) + (camRight * inputDir.x);
        }

        // Standard gravity
        if (_controller.isGrounded)
        {
            _verticalVelocity.y = -1f;
        }
        else
        {
            _verticalVelocity.y -= gravity * Time.deltaTime;
        }

        Vector3 worldMoveVelocity = moveDir * moveSpeed;
        _controller.Move((worldMoveVelocity + _verticalVelocity) * Time.deltaTime);

        // Feed velocity to the 8-directional animation entity
        if (_animEntity != null)
        {
            _animEntity.SetMovement(worldMoveVelocity);
        }
    }
}