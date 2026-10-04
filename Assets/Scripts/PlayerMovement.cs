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

    [Header("Footstep / Walking Audio")]
    [Tooltip("Audio clip for footstep or continuous walking sound.")]
    [SerializeField] private AudioClip footstepAudioClip;

    [Tooltip("Optional multiple footstep clips for randomized footsteps (used if discrete steps mode).")]
    [SerializeField] private AudioClip[] footstepClips;

    [Tooltip("If TRUE: loops a continuous walking sound while moving, and immediately stops when player stops. If FALSE: plays individual footstep sounds at timed intervals.")]
    [SerializeField] private bool continuousWalkingSound = false;

    [Range(0.05f, 2.0f)]
    [Tooltip("Time interval (gap in seconds) between footstep sounds when continuous walking sound is unchecked.")]
    [SerializeField] private float stepInterval = 0.35f;

    [Range(0f, 1f)]
    [Tooltip("Volume slider for footsteps / walking sound.")]
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
    private bool _wasWalkingLastFrame = false;

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

        // Feed velocity to the directional animation entity
        if (_animEntity != null)
        {
            _animEntity.SetMovement(worldMoveVelocity);
        }

        // Footstep / Walking Audio handling
        bool isWalking = _controller.isGrounded && inputDir.sqrMagnitude > 0.001f && worldMoveVelocity.sqrMagnitude > 0.01f;
        HandleFootstepAudio(isWalking);
    }

    private void HandleFootstepAudio(bool isWalking)
    {
        if (footstepAudioSource == null) return;

        if (isWalking)
        {
            if (continuousWalkingSound)
            {
                // Continuous loop walking sound
                AudioClip clipToPlay = footstepAudioClip != null ? footstepAudioClip : GetRandomFootstepClip();
                if (clipToPlay != null)
                {
                    if (footstepAudioSource.clip != clipToPlay)
                    {
                        footstepAudioSource.clip = clipToPlay;
                    }

                    footstepAudioSource.loop = true;
                    footstepAudioSource.volume = footstepVolume;
                    footstepAudioSource.pitch = footstepPitch;

                    if (!footstepAudioSource.isPlaying)
                    {
                        footstepAudioSource.Play();
                    }
                }
            }
            else
            {
                // Discrete footstep interval mode
                if (footstepAudioSource.isPlaying && footstepAudioSource.loop)
                {
                    footstepAudioSource.Stop();
                }

                // If just started walking, play first footstep immediately
                if (!_wasWalkingLastFrame)
                {
                    _stepTimer = stepInterval;
                }

                _stepTimer += Time.deltaTime;
                if (_stepTimer >= stepInterval)
                {
                    _stepTimer = 0f;
                    PlaySingleFootstepSound();
                }
            }

            _wasWalkingLastFrame = true;
        }
        else
        {
            StopFootstepAudio();
        }
    }

    private void PlaySingleFootstepSound()
    {
        AudioClip clip = GetRandomFootstepClip();
        if (clip == null) clip = footstepAudioClip;

        if (clip != null && footstepAudioSource != null)
        {
            float pitchOffset = Random.Range(-pitchVariation, pitchVariation);
            footstepAudioSource.pitch = footstepPitch + pitchOffset;
            footstepAudioSource.PlayOneShot(clip, footstepVolume);
        }
    }

    private AudioClip GetRandomFootstepClip()
    {
        if (footstepClips != null && footstepClips.Length > 0)
        {
            int index = Random.Range(0, footstepClips.Length);
            if (footstepClips[index] != null)
            {
                return footstepClips[index];
            }
        }
        return footstepAudioClip;
    }

    private void StopFootstepAudio()
    {
        if (footstepAudioSource != null && footstepAudioSource.isPlaying && footstepAudioSource.loop)
        {
            footstepAudioSource.Stop();
        }

        _stepTimer = stepInterval * 0.9f; // Prime for immediate next step when resuming walk
        _wasWalkingLastFrame = false;
    }
}