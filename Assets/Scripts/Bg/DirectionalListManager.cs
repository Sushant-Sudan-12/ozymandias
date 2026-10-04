using System;
using System.Collections.Generic;
using UnityEngine;

public class DirectionalListManager : MonoBehaviour
{
    public enum CompassAngle8
    {
        North = 0,
        NorthEast = 1,
        East = 2,
        SouthEast = 3,
        South = 4,
        SouthWest = 5,
        West = 6,
        NorthWest = 7
    }

    public enum TimeState
    {
        Past,
        Present,
        Future
    }

    [System.Serializable]
    public class DirectionalGroup
    {
        public string groupName = "Group";
        public CompassAngle8 primaryCardinal;       // N, S, E, or W
        public CompassAngle8 linkedIntermediate;    // e.g. NE, NW, SE, SW
        public List<CompassAngle8> extraAngles = new List<CompassAngle8>(); // Optional additional angles where this group is active
        public List<GameObject> targetObjects = new List<GameObject>();

        [HideInInspector] public List<SpriteRenderer> cachedSprites = new List<SpriteRenderer>();
        [HideInInspector] public float currentAlpha = 1f;
        [HideInInspector] public float targetAlpha = 1f;
    }

    private struct ObjectEntry
    {
        public GameObject gameObject;
        public DirectionalGroup[] parentGroups;
    }

    private struct SpriteEntry
    {
        public SpriteRenderer spriteRenderer;
        public DirectionalGroup[] parentGroups;
    }

    [Header("4 Configurable Groups")]
    [SerializeField] private DirectionalGroup groupA = new DirectionalGroup { groupName = "North Group", primaryCardinal = CompassAngle8.North, linkedIntermediate = CompassAngle8.NorthEast };
    [SerializeField] private DirectionalGroup groupB = new DirectionalGroup { groupName = "East Group", primaryCardinal = CompassAngle8.East, linkedIntermediate = CompassAngle8.SouthEast };
    [SerializeField] private DirectionalGroup groupC = new DirectionalGroup { groupName = "South Group", primaryCardinal = CompassAngle8.South, linkedIntermediate = CompassAngle8.SouthWest };
    [SerializeField] private DirectionalGroup groupD = new DirectionalGroup { groupName = "West Group", primaryCardinal = CompassAngle8.West, linkedIntermediate = CompassAngle8.NorthWest };

    [Header("Optional Custom Groups (e.g. Past / Future)")]
    [SerializeField] private List<DirectionalGroup> customGroups = new List<DirectionalGroup>();

    [Header("Alpha Settings")]
    [SerializeField] private float cardinalAlpha = 1.0f;
    [SerializeField] private float intermediateAlpha = 0.45f; // 30-50%
    [SerializeField] private float fadeSpeed = 8f;
    [SerializeField] private bool deactivateWhenZero = true;

    [Header("--- Directional Light Rotation (Past / Present / Future) ---")]
    [Tooltip("The Directional Light in the scene whose Y-rotation lerps when switching between Past, Present, and Future. Auto-finds if unassigned.")]
    [SerializeField] private Light directionalLight;

    [Tooltip("Enable smooth Y-rotation lerping for the Directional Light.")]
    [SerializeField] private bool enableLightRotation = true;

    [Tooltip("Target Y rotation (in degrees) for the Directional Light in the Past.")]
    [SerializeField] private float pastLightYRotation = -45f;

    [Tooltip("Target Y rotation (in degrees) for the Directional Light in the Present.")]
    [SerializeField] private float presentLightYRotation = 0f;

    [Tooltip("Target Y rotation (in degrees) for the Directional Light in the Future.")]
    [SerializeField] private float futureLightYRotation = 45f;

    [Range(0.5f, 20f)]
    [Tooltip("Lerp speed for rotating the Directional Light between states.")]
    [SerializeField] private float lightLerpSpeed = 5f;

    [Header("Time State Angle Mappings")]
    [Tooltip("Angles that trigger the Past state (if not using group names).")]
    [SerializeField] private List<CompassAngle8> pastAngles = new List<CompassAngle8> { CompassAngle8.NorthEast, CompassAngle8.SouthWest };

    [Tooltip("Angles that trigger the Future state (if not using group names).")]
    [SerializeField] private List<CompassAngle8> futureAngles = new List<CompassAngle8> { CompassAngle8.NorthWest, CompassAngle8.SouthEast };

    [Tooltip("Angles that trigger the Present state.")]
    [SerializeField] private List<CompassAngle8> presentAngles = new List<CompassAngle8> { CompassAngle8.North, CompassAngle8.East, CompassAngle8.South, CompassAngle8.West };

    private DirectionalGroup[] _allGroups = Array.Empty<DirectionalGroup>();
    private readonly List<ObjectEntry> _uniqueObjects = new List<ObjectEntry>();
    private readonly List<SpriteEntry> _uniqueSprites = new List<SpriteEntry>();

    private CompassAngle8 _currentCompassDir = CompassAngle8.North;
    private TimeState _currentTimeState = TimeState.Present;

    private float _cachedLightPitch = 50f;
    private float _cachedLightRoll = 0f;
    private float _currentLightY = 0f;
    private float _targetLightY = 0f;

    public TimeState CurrentTimeState => _currentTimeState;
    public CompassAngle8 CurrentCompassDir => _currentCompassDir;

    private void Awake()
    {
        InitializeDirectionalLight();
        RebuildRegistry();
        UpdateGroupTargets(CompassAngle8.North, immediate: true);
    }

    private void InitializeDirectionalLight()
    {
        if (directionalLight == null)
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                {
                    directionalLight = lights[i];
                    break;
                }
            }
        }

        if (directionalLight != null)
        {
            Vector3 euler = directionalLight.transform.eulerAngles;
            _cachedLightPitch = euler.x;
            _cachedLightRoll = euler.z;
            _currentLightY = euler.y;
            _targetLightY = presentLightYRotation;
        }
    }

    private void Update()
    {
        // 1. Smoothly interpolate current alpha for every group
        for (int i = 0; i < _allGroups.Length; i++)
        {
            var grp = _allGroups[i];
            if (grp == null) continue;
            grp.currentAlpha = Mathf.MoveTowards(grp.currentAlpha, grp.targetAlpha, fadeSpeed * Time.deltaTime);
        }

        // 2. Apply combined max alpha to unique GameObjects and Sprites
        ApplyAllAlphas();

        // 3. Smoothly lerp Directional Light Y rotation between Past, Present, and Future
        if (enableLightRotation && directionalLight != null)
        {
            _currentLightY = Mathf.LerpAngle(_currentLightY, _targetLightY, lightLerpSpeed * Time.deltaTime);
            directionalLight.transform.rotation = Quaternion.Euler(_cachedLightPitch, _currentLightY, _cachedLightRoll);
        }
    }

    /// <summary>
    /// Re-indexes all unique GameObjects and Sprites across all groups.
    /// Ensures that if a GameObject is assigned to multiple groups (e.g. NS and EW),
    /// it remains active if ANY of its referencing groups is active.
    /// </summary>
    public void RebuildRegistry()
    {
        var groupList = new List<DirectionalGroup>();
        if (groupA != null) groupList.Add(groupA);
        if (groupB != null) groupList.Add(groupB);
        if (groupC != null) groupList.Add(groupC);
        if (groupD != null) groupList.Add(groupD);

        if (customGroups != null)
        {
            for (int i = 0; i < customGroups.Count; i++)
            {
                if (customGroups[i] != null && !groupList.Contains(customGroups[i]))
                    groupList.Add(customGroups[i]);
            }
        }

        _allGroups = groupList.ToArray();

        var objectGroupMap = new Dictionary<GameObject, HashSet<DirectionalGroup>>();
        var spriteGroupMap = new Dictionary<SpriteRenderer, HashSet<DirectionalGroup>>();

        for (int i = 0; i < _allGroups.Length; i++)
        {
            var grp = _allGroups[i];
            if (grp == null || grp.targetObjects == null) continue;

            grp.cachedSprites.Clear();

            for (int o = 0; o < grp.targetObjects.Count; o++)
            {
                var obj = grp.targetObjects[o];
                if (obj == null) continue;

                if (!objectGroupMap.TryGetValue(obj, out var objGroups))
                {
                    objGroups = new HashSet<DirectionalGroup>();
                    objectGroupMap[obj] = objGroups;
                }
                objGroups.Add(grp);

                var sprites = obj.GetComponentsInChildren<SpriteRenderer>(true);
                for (int s = 0; s < sprites.Length; s++)
                {
                    var sr = sprites[s];
                    if (sr == null) continue;

                    if (!grp.cachedSprites.Contains(sr))
                        grp.cachedSprites.Add(sr);

                    if (!spriteGroupMap.TryGetValue(sr, out var srGroups))
                    {
                        srGroups = new HashSet<DirectionalGroup>();
                        spriteGroupMap[sr] = srGroups;
                    }
                    srGroups.Add(grp);
                }
            }
        }

        _uniqueObjects.Clear();
        foreach (var kvp in objectGroupMap)
        {
            var groupsArray = new DirectionalGroup[kvp.Value.Count];
            kvp.Value.CopyTo(groupsArray);
            _uniqueObjects.Add(new ObjectEntry
            {
                gameObject = kvp.Key,
                parentGroups = groupsArray
            });
        }

        _uniqueSprites.Clear();
        foreach (var kvp in spriteGroupMap)
        {
            var groupsArray = new DirectionalGroup[kvp.Value.Count];
            kvp.Value.CopyTo(groupsArray);
            _uniqueSprites.Add(new SpriteEntry
            {
                spriteRenderer = kvp.Key,
                parentGroups = groupsArray
            });
        }
    }

    public void UpdateFromNeedleAngle(float needleAngle)
    {
        float norm = needleAngle % 360f;
        if (norm < 0f) norm += 360f;

        CompassAngle8 dir;
        if (norm >= 337.5f || norm < 22.5f)       dir = CompassAngle8.North;
        else if (norm >= 22.5f && norm < 67.5f)   dir = CompassAngle8.NorthWest;
        else if (norm >= 67.5f && norm < 112.5f)  dir = CompassAngle8.West;
        else if (norm >= 112.5f && norm < 157.5f) dir = CompassAngle8.SouthWest;
        else if (norm >= 157.5f && norm < 202.5f) dir = CompassAngle8.South;
        else if (norm >= 202.5f && norm < 247.5f) dir = CompassAngle8.SouthEast;
        else if (norm >= 247.5f && norm < 292.5f) dir = CompassAngle8.East;
        else                                      dir = CompassAngle8.NorthEast;

        if (dir != _currentCompassDir)
        {
            _currentCompassDir = dir;
            UpdateGroupTargets(dir, immediate: false);
        }
    }

    private void UpdateGroupTargets(CompassAngle8 compassDir, bool immediate)
    {
        for (int i = 0; i < _allGroups.Length; i++)
        {
            var grp = _allGroups[i];
            if (grp == null) continue;

            // If compass matches the primary cardinal or extra angles -> 100% (cardinalAlpha)
            if (grp.primaryCardinal == compassDir || (grp.extraAngles != null && grp.extraAngles.Contains(compassDir)))
            {
                grp.targetAlpha = cardinalAlpha;
            }
            // If compass matches the linked intermediate -> 30-50% (intermediateAlpha)
            else if (grp.linkedIntermediate == compassDir)
            {
                grp.targetAlpha = intermediateAlpha;
            }
            // Not connected to this angle -> 0%
            else
            {
                grp.targetAlpha = 0f;
            }

            if (immediate)
            {
                grp.currentAlpha = grp.targetAlpha;
            }
        }

        // Update target TimeState and Directional Light Y-rotation
        UpdateTimeStateAndLightTarget(compassDir, immediate);

        if (immediate)
        {
            ApplyAllAlphas();
        }
    }

    private void UpdateTimeStateAndLightTarget(CompassAngle8 compassDir, bool immediate)
    {
        // 1. Check if group names explicitly identify Past / Future
        bool isPast = false;
        bool isFuture = false;

        for (int i = 0; i < _allGroups.Length; i++)
        {
            var grp = _allGroups[i];
            if (grp == null || string.IsNullOrEmpty(grp.groupName)) continue;

            bool matchesDir = (grp.primaryCardinal == compassDir) || 
                              (grp.linkedIntermediate == compassDir) || 
                              (grp.extraAngles != null && grp.extraAngles.Contains(compassDir));

            if (matchesDir)
            {
                if (grp.groupName.IndexOf("Past", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isPast = true;
                    break;
                }
                else if (grp.groupName.IndexOf("Future", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isFuture = true;
                    break;
                }
            }
        }

        // 2. Fallback to configured angle lists if group names did not match
        if (!isPast && !isFuture)
        {
            if (pastAngles != null && pastAngles.Contains(compassDir))
            {
                isPast = true;
            }
            else if (futureAngles != null && futureAngles.Contains(compassDir))
            {
                isFuture = true;
            }
        }

        // 3. Resolve target state and light Y rotation
        if (isPast)
        {
            _currentTimeState = TimeState.Past;
            _targetLightY = pastLightYRotation;
        }
        else if (isFuture)
        {
            _currentTimeState = TimeState.Future;
            _targetLightY = futureLightYRotation;
        }
        else
        {
            _currentTimeState = TimeState.Present;
            _targetLightY = presentLightYRotation;
        }

        if (immediate && directionalLight != null)
        {
            _currentLightY = _targetLightY;
            directionalLight.transform.rotation = Quaternion.Euler(_cachedLightPitch, _currentLightY, _cachedLightRoll);
        }
    }

    private void ApplyAllAlphas()
    {
        // 1. Manage GameObject active/inactive states
        if (deactivateWhenZero)
        {
            for (int i = 0; i < _uniqueObjects.Count; i++)
            {
                var entry = _uniqueObjects[i];
                if (entry.gameObject == null) continue;

                float maxAlpha = 0f;
                for (int g = 0; g < entry.parentGroups.Length; g++)
                {
                    float a = entry.parentGroups[g].currentAlpha;
                    if (a > maxAlpha) maxAlpha = a;
                }

                // If any group referencing this GameObject is active (> 0), keep the GameObject active.
                // Only deactivate if ALL referencing groups are 0.
                bool shouldBeActive = maxAlpha > 0.001f;
                if (entry.gameObject.activeSelf != shouldBeActive)
                {
                    entry.gameObject.SetActive(shouldBeActive);
                }
            }
        }

        // 2. Set SpriteRenderer alphas based on max alpha of all referencing groups
        for (int i = 0; i < _uniqueSprites.Count; i++)
        {
            var entry = _uniqueSprites[i];
            if (entry.spriteRenderer == null) continue;

            float maxAlpha = 0f;
            for (int g = 0; g < entry.parentGroups.Length; g++)
            {
                float a = entry.parentGroups[g].currentAlpha;
                if (a > maxAlpha) maxAlpha = a;
            }

            Color c = entry.spriteRenderer.color;
            c.a = maxAlpha;
            entry.spriteRenderer.color = c;
        }
    }
}