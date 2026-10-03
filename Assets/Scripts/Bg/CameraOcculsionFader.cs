using System.Collections.Generic;
using UnityEngine;

public class CameraOcclusionFader : MonoBehaviour
{
    [Header("Camera & Target References")]
    [Tooltip("The camera performing the occlusion check. If empty, uses this GameObject's Camera or Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("The player transform to keep in sight. If empty, auto-detected via Tag, PlayerMovement component, or 'Player' GameObject.")]
    [SerializeField] private Transform playerTarget;

    [Tooltip("Offset from the player's base position to their center/waist. Defaults to (0, 0.4, 0).")]
    [SerializeField] private Vector3 playerCenterOffset = new Vector3(0f, 0.4f, 0f);

    [Tooltip("Layers to check for obstacles. Defaults to Everything (~0).")]
    [SerializeField] private LayerMask obstacleLayer = ~0;

    [Header("Fade Settings")]
    [Range(0.05f, 0.95f)]
    [SerializeField] private float occludedAlpha = 0.35f;
    [SerializeField] private float fadeSpeed = 8f;
    [SerializeField] private float castRadius = 0.35f;
    [Tooltip("Safety margin to avoid fading objects behind the player.")]
    [SerializeField] private float playerBuffer = 0.05f;

    [Header("Multi-Point Sightlines (Covers both tall and low walls)")]
    [Tooltip("Cast extra sightlines to the player's head and feet so low walls and tall walls are both detected.")]
    [SerializeField] private bool multiPointSightlines = true;
    [SerializeField] private float headHeightOffset = 0.7f;
    [SerializeField] private float feetHeightOffset = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool logHitsToConsole = true;

    private readonly List<FadeMeshObstacle> _activeMeshObstacles = new List<FadeMeshObstacle>();
    private readonly HashSet<FadeMeshObstacle> _meshHitsThisFrame = new HashSet<FadeMeshObstacle>();

    private readonly List<FadeObstacle> _activeSpriteObstacles = new List<FadeObstacle>();
    private readonly HashSet<FadeObstacle> _spriteHitsThisFrame = new HashSet<FadeObstacle>();

    private void Awake()
    {
        EnsureReferences();
    }

    private void EnsureReferences()
    {
        if (!targetCamera)
        {
            targetCamera = GetComponent<Camera>();
            if (!targetCamera) targetCamera = Camera.main;
            if (!targetCamera) targetCamera = FindObjectOfType<Camera>();
        }

        if (!playerTarget)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p)
            {
                playerTarget = p.transform;
            }
            else
            {
                var pm = FindObjectOfType<PlayerMovement>();
                if (pm)
                {
                    playerTarget = pm.transform;
                }
                else
                {
                    var pObj = GameObject.Find("Player");
                    if (pObj) playerTarget = pObj.transform;
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (!playerTarget || !targetCamera)
        {
            EnsureReferences();
            if (!playerTarget || !targetCamera) return;
        }

        Vector3 rayStart = targetCamera.transform.position;
        _meshHitsThisFrame.Clear();
        _spriteHitsThisFrame.Clear();

        // 1. Primary center sightline (waist / center)
        Vector3 centerTarget = playerTarget.position + playerCenterOffset;
        CastSightline(rayStart, centerTarget);

        // 2. Extra sightlines (head & feet) to catch walls of any height
        if (multiPointSightlines)
        {
            Vector3 headTarget = playerTarget.position + new Vector3(0f, headHeightOffset, 0f);
            CastSightline(rayStart, headTarget);

            Vector3 feetTarget = playerTarget.position + new Vector3(0f, feetHeightOffset, 0f);
            CastSightline(rayStart, feetTarget);
        }

        // Restore unoccluded 3D meshes
        for (int i = _activeMeshObstacles.Count - 1; i >= 0; i--)
        {
            var m = _activeMeshObstacles[i];
            if (m == null)
            {
                _activeMeshObstacles.RemoveAt(i);
                continue;
            }

            if (!_meshHitsThisFrame.Contains(m))
            {
                m.ClearOcclusion();
            }

            m.UpdateFade(fadeSpeed);

            if (m.IsFullyRestored)
            {
                _activeMeshObstacles.RemoveAt(i);
            }
        }

        // Restore unoccluded 2D sprites
        for (int i = _activeSpriteObstacles.Count - 1; i >= 0; i--)
        {
            var s = _activeSpriteObstacles[i];
            if (s == null)
            {
                _activeSpriteObstacles.RemoveAt(i);
                continue;
            }

            if (!_spriteHitsThisFrame.Contains(s))
            {
                s.ClearOcclusion();
            }

            s.UpdateFade(fadeSpeed);

            if (s.IsFullyRestored)
            {
                _activeSpriteObstacles.RemoveAt(i);
            }
        }
    }

    private void CastSightline(Vector3 start, Vector3 target)
    {
        Vector3 dir = target - start;
        float totalDist = dir.magnitude;
        if (totalDist <= 0.05f) return;

        float castDist = Mathf.Max(0.05f, totalDist - playerBuffer);

        // Pinpoint direct Raycast with QueryTriggerInteraction.Collide
        RaycastHit[] rayHits = Physics.RaycastAll(start, dir.normalized, castDist, obstacleLayer, QueryTriggerInteraction.Collide);
        for (int i = 0; i < rayHits.Length; i++)
        {
            ProcessHit(rayHits[i], castDist);
        }

        // Volume SphereCast
        if (castRadius > 0.01f)
        {
            RaycastHit[] sphereHits = Physics.SphereCastAll(start, castRadius, dir.normalized, castDist, obstacleLayer, QueryTriggerInteraction.Collide);
            for (int i = 0; i < sphereHits.Length; i++)
            {
                ProcessHit(sphereHits[i], castDist);
            }
        }
    }

    private void ProcessHit(RaycastHit hit, float maxCastDist)
    {
        if (hit.collider == null) return;
        if (hit.distance >= maxCastDist) return;

        // Ignore hits on the player
        if (playerTarget != null && (hit.collider.transform == playerTarget || hit.collider.transform.IsChildOf(playerTarget)))
            return;

        // 1. Look for 3D Mesh Obstacle (Component)
        var meshObs = hit.collider.GetComponentInParent<FadeMeshObstacle>();
        if (meshObs == null)
            meshObs = hit.collider.GetComponentInChildren<FadeMeshObstacle>();
        if (meshObs == null && hit.collider.attachedRigidbody != null)
            meshObs = hit.collider.attachedRigidbody.GetComponent<FadeMeshObstacle>();

        // Auto-detect 3D MeshRenderer obstacles even if FadeMeshObstacle component was not manually attached
        if (meshObs == null)
        {
            var meshRend = hit.collider.GetComponentInParent<MeshRenderer>();
            if (meshRend == null) meshRend = hit.collider.GetComponentInChildren<MeshRenderer>();
            if (meshRend == null && hit.collider.transform.parent != null)
                meshRend = hit.collider.transform.parent.GetComponentInChildren<MeshRenderer>();

            if (meshRend != null && !IsIgnoredEnvironment(meshRend.gameObject))
            {
                meshObs = meshRend.gameObject.GetComponent<FadeMeshObstacle>();
                if (meshObs == null)
                {
                    meshObs = meshRend.gameObject.AddComponent<FadeMeshObstacle>();
                }
            }
        }

        if (meshObs != null)
        {
            if (logHitsToConsole && !_meshHitsThisFrame.Contains(meshObs))
                Debug.Log($"[CameraOcclusionFader] Fading 3D Mesh: '{meshObs.gameObject.name}' (Hit Collider: '{hit.collider.gameObject.name}', Dist: {hit.distance:F2}m)");

            meshObs.SetOccluded(occludedAlpha);
            _meshHitsThisFrame.Add(meshObs);

            if (!_activeMeshObstacles.Contains(meshObs))
                _activeMeshObstacles.Add(meshObs);

            return;
        }

        // 2. Look for 2D Sprite Obstacle
        var spriteObs = hit.collider.GetComponentInParent<FadeObstacle>();
        if (spriteObs == null)
            spriteObs = hit.collider.GetComponentInChildren<FadeObstacle>();
        if (spriteObs == null && hit.collider.attachedRigidbody != null)
            spriteObs = hit.collider.attachedRigidbody.GetComponent<FadeObstacle>();

        if (spriteObs != null)
        {
            if (logHitsToConsole && !_spriteHitsThisFrame.Contains(spriteObs))
                Debug.Log($"[CameraOcclusionFader] Fading 2D Sprite: '{spriteObs.gameObject.name}' (Hit Collider: '{hit.collider.gameObject.name}', Dist: {hit.distance:F2}m)");

            spriteObs.SetOccluded(occludedAlpha);
            _spriteHitsThisFrame.Add(spriteObs);

            if (!_activeSpriteObstacles.Contains(spriteObs))
                _activeSpriteObstacles.Add(spriteObs);
        }
    }

    private bool IsIgnoredEnvironment(GameObject go)
    {
        if (go == null) return true;
        if (playerTarget != null && (go.transform == playerTarget || go.transform.IsChildOf(playerTarget)))
            return true;
        if (go.CompareTag("Player") || go.CompareTag("MainCamera"))
            return true;

        string nameLower = go.name.ToLower();
        if (nameLower.Contains("floor") || nameLower.Contains("ground") || nameLower.Contains("terrain") || nameLower.Equals("plane"))
            return true;

        return false;
    }

    private void OnDrawGizmos()
    {
        Vector3 start = targetCamera ? targetCamera.transform.position : transform.position;
        if (!playerTarget)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) playerTarget = p.transform;
        }
        if (!playerTarget) return;

        Gizmos.color = (_meshHitsThisFrame.Count > 0 || _spriteHitsThisFrame.Count > 0) ? Color.green : new Color(1f, 0.3f, 0.3f, 0.6f);

        Vector3 center = playerTarget.position + playerCenterOffset;
        Gizmos.DrawLine(start, center);
        Gizmos.DrawWireSphere(start, castRadius);
        Gizmos.DrawWireSphere(center, 0.15f);

        if (multiPointSightlines)
        {
            Vector3 head = playerTarget.position + new Vector3(0f, headHeightOffset, 0f);
            Vector3 feet = playerTarget.position + new Vector3(0f, feetHeightOffset, 0f);
            Gizmos.DrawLine(start, head);
            Gizmos.DrawLine(start, feet);
            Gizmos.DrawWireSphere(head, 0.15f);
            Gizmos.DrawWireSphere(feet, 0.15f);
        }
    }
}