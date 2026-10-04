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

    [Header("Footstep Audio (Randomized Clips)")]
    [Tooltip("List of footstep audio clips to randomly cycle through while walking.")]
    [SerializeField] private AudioClip[] footstepClips;

    [Tooltip("Fallback single footstep audio clip if list is empty.")]
    [SerializeField] private AudioClip fallbackFootstepClip;

    [Range(0.05f, 1.5f)]
    [Tooltip("Time interval / gap (in seconds) between footsteps while walking.")]
    [SerializeField] private float stepInterval = 0.35f;

    [Range(0f, 1f)]
    [Tooltip("Volume slider for footstep sounds.")]
    [SerializeField] private float footstepVolume = 0.8f;

    [Range(0.5f, 1.5f)]
    [Tooltip("Base pitch for footsteps.")]
    [SerializeField] private float footstepPitch = 1.0f;

    [Range(0f, 0.3f)]
    [Tooltip("Subtle random pitch variation per footstep for natural sound.")]
    [SerializeField] private float pitchVariation = 0.05f;

    [Tooltip("AudioSource component used to play footsteps. Auto-created if unassigned.")]
    [SerializeField] private AudioSource footstepAudioSource;

    private CharacterController _controller;
    private DirectionalAnimationEntity _animEntity;
    private Vector3 _verticalVelocity;
    private float _stepTimer = 0f;
    private bool _isWalkingLastFrame = false;
    private int _lastClipIndex = -1;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _animEntity = GetComponent<DirectionalAnimationEntity>();

        if (!playerCamera)
            playerCamera = GetComponentInChildren<Camera>();

        InitializeFootstepAudioSource();
    }

    private void OnDisable()
    {
        StopFootstepAudio();
    }

    private void InitializeFootstepAudioSource()
    {
        if (footstepAudioSource == null)
        {
            footstepAudioSource = GetComponent<AudioSource>();
            if (footstepAudioSource == null)
            {
                footstepAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        footstepAudioSource.playOnAwake = false;
        footstepAudioSource.loop = false;
        footstepAudioSource.spatialBlend = 0f; // 2D Audio
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

            StopFootstepAudio();
            return;
        }

        // Movement input
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

        // Gravity
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

        // Feed velocity to the directional animation entity
        if (_animEntity != null)
        {
            _animEntity.SetMovement(worldMoveVelocity);
        }

        // Footstep Audio handling (discrete randomized footstep clips)
        bool isWalking = _controller.isGrounded && inputDir.sqrMagnitude > 0.001f && worldMoveVelocity.sqrMagnitude > 0.01f;
        HandleFootstepAudio(isWalking);
    }

    private void HandleFootstepAudio(bool isWalking)
    {
        if (footstepAudioSource == null) return;

        if (isWalking)
        {
            // If the player just started walking from a complete stop:
            // Immediately play a new random footstep clip!
            if (!_isWalkingLastFrame)
            {
                PlayRandomFootstepSound();
                _stepTimer = 0f;
            }
            else
            {
                _stepTimer += Time.deltaTime;
                if (_stepTimer >= stepInterval)
                {
                    _stepTimer = 0f;
                    PlayRandomFootstepSound();
                }
            }

            _isWalkingLastFrame = true;
        }
        else
        {
            StopFootstepAudio();
        }
    }

    private void PlayRandomFootstepSound()
    {
        AudioClip clip = GetRandomFootstepClip();
        if (clip == null) return;

        if (footstepAudioSource != null)
        {
            float pitchOffset = Random.Range(-pitchVariation, pitchVariation);
            footstepAudioSource.pitch = footstepPitch + pitchOffset;
            footstepAudioSource.PlayOneShot(clip, footstepVolume);
        }
    }

    /// <summary>
    /// Picks a random clip from the list, ensuring the first step and subsequent steps change each time without repeating.
    /// </summary>
    private AudioClip GetRandomFootstepClip()
    {
        if (footstepClips != null && footstepClips.Length > 0)
        {
            if (footstepClips.Length == 1)
            {
                return footstepClips[0] != null ? footstepClips[0] : fallbackFootstepClip;
            }

            // Pick a random clip index that is different from the last one played
            int newIndex = Random.Range(0, footstepClips.Length);
            if (newIndex == _lastClipIndex)
            {
                // Shift to a different index to guarantee a fresh clip every step
                newIndex = (newIndex + Random.Range(1, footstepClips.Length)) % footstepClips.Length;
            }

            _lastClipIndex = newIndex;

            if (footstepClips[newIndex] != null)
            {
                return footstepClips[newIndex];
            }
        }

        return fallbackFootstepClip;
    }

    private void StopFootstepAudio()
    {
        _isWalkingLastFrame = false;
        _stepTimer = 0f;
    }
}