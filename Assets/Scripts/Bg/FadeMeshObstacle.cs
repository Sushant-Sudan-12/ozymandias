using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class FadeMeshObstacle : MonoBehaviour
{
    [System.Serializable]
    private class RendererMaterialData
    {
        public Renderer renderer;
        public Material[] originalMaterials;
        public Material[] runtimeTransparentMats;
        public Color[] baseColors;
        public int[] colorPropIDs;
        public bool needsMaterialSwap;
    }

    [Header("Renderer (Auto-detected if blank)")]
    [Tooltip("Primary MeshRenderer. If blank, auto-detected on this GameObject or children.")]
    [SerializeField] private MeshRenderer meshRenderer;

    [Tooltip("Optional additional renderers (e.g. multi-part meshes or sub-objects).")]
    [SerializeField] private List<Renderer> additionalRenderers = new List<Renderer>();

    private readonly List<RendererMaterialData> _rendererDataList = new List<RendererMaterialData>();
    private MaterialPropertyBlock _propBlock;

    private float _targetAlpha = 1f;
    private float _currentAlpha = 1f;
    private bool _isOccluded = false;
    private bool _isSwapped = false;
    private int _lastFadeUpdateFrame = -1;

    // Common Color Property IDs
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProp = Shader.PropertyToID("_Color");
    private static readonly int TintColorProp = Shader.PropertyToID("_TintColor");

    private void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
        InitializeRenderers();
    }

    public void InitializeRenderers()
    {
        if (_propBlock == null)
            _propBlock = new MaterialPropertyBlock();

        CleanupRuntimeMaterials();
        _rendererDataList.Clear();

        // 1. Gather all renderers
        var renderersToProcess = new List<Renderer>();

        if (meshRenderer != null && !renderersToProcess.Contains(meshRenderer))
        {
            renderersToProcess.Add(meshRenderer);
        }

        if (additionalRenderers != null && additionalRenderers.Count > 0)
        {
            for (int i = 0; i < additionalRenderers.Count; i++)
            {
                var r = additionalRenderers[i];
                if (r != null && !renderersToProcess.Contains(r))
                    renderersToProcess.Add(r);
            }
        }

        // Auto-detect all 3D mesh renderers if none manually assigned
        if (renderersToProcess.Count == 0)
        {
            var found = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < found.Length; i++)
            {
                var r = found[i];
                if (r is SpriteRenderer) continue; // Handled by 2D FadeObstacle
                if (r != null && !renderersToProcess.Contains(r))
                    renderersToProcess.Add(r);
            }
        }

        if (renderersToProcess.Count == 0)
        {
            Debug.LogWarning($"[FadeMeshObstacle] No Renderers found on '{gameObject.name}' or its children!");
            return;
        }

        // 2. Cache default materials and initial colors for each renderer
        for (int rIndex = 0; rIndex < renderersToProcess.Count; rIndex++)
        {
            var rend = renderersToProcess[rIndex];
            if (rend == null) continue;

            var sharedMats = rend.sharedMaterials;
            if (sharedMats == null || sharedMats.Length == 0) continue;

            var data = new RendererMaterialData
            {
                renderer = rend,
                originalMaterials = sharedMats,
                runtimeTransparentMats = new Material[sharedMats.Length],
                baseColors = new Color[sharedMats.Length],
                colorPropIDs = new int[sharedMats.Length],
                needsMaterialSwap = false
            };

            for (int m = 0; m < sharedMats.Length; m++)
            {
                Material sourceMat = sharedMats[m];
                if (sourceMat == null) continue;

                // Identify color property and read initial base color
                int propId = 0;
                Color baseColor = Color.white;

                if (sourceMat.HasProperty(BaseColorProp))
                {
                    propId = BaseColorProp;
                    baseColor = sourceMat.GetColor(BaseColorProp);
                }
                else if (sourceMat.HasProperty(ColorProp))
                {
                    propId = ColorProp;
                    baseColor = sourceMat.GetColor(ColorProp);
                }
                else if (sourceMat.HasProperty(TintColorProp))
                {
                    propId = TintColorProp;
                    baseColor = sourceMat.GetColor(TintColorProp);
                }

                data.colorPropIDs[m] = propId;
                data.baseColors[m] = baseColor;

                // Check if material is set to Transparent surface type
                bool isTransparent = IsMaterialTransparent(sourceMat);

                if (!isTransparent)
                {
                    // Fallback: Clone this renderer's exact source material and configure transparent keywords/blend mode
                    Material transClone = new Material(sourceMat);
                    SetupMaterialForTransparency(transClone);
                    data.runtimeTransparentMats[m] = transClone;
                    data.needsMaterialSwap = true;
                }
                else
                {
                    data.runtimeTransparentMats[m] = sourceMat;
                }
            }

            _rendererDataList.Add(data);
        }

        RestoreFullAlpha();
    }

    /// <summary>
    /// Checks if a material already supports alpha transparency blending.
    /// </summary>
    public static bool IsMaterialTransparent(Material mat)
    {
        if (mat == null) return false;

        if (mat.HasProperty("_Surface") && Mathf.Approximately(mat.GetFloat("_Surface"), 1f))
            return true;

        if (mat.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent)
            return true;

        string renderType = mat.GetTag("RenderType", false, "");
        if (renderType.Equals("Transparent", System.StringComparison.OrdinalIgnoreCase))
            return true;

        if (mat.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") || mat.IsKeywordEnabled("_ALPHABLEND_ON"))
            return true;

        return false;
    }

    /// <summary>
    /// Configures URP and Standard shader keywords and blend modes for alpha transparency.
    /// </summary>
    public static void SetupMaterialForTransparency(Material mat)
    {
        if (mat == null) return;

        // URP Lit / Simple Lit / Unlit settings
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f); // 1 = Transparent
        }

        if (mat.HasProperty("_Blend"))
        {
            mat.SetFloat("_Blend", 0f); // 0 = Alpha Blend
        }

        if (mat.HasProperty("_SrcBlend"))
        {
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (mat.HasProperty("_DstBlend"))
        {
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (mat.HasProperty("_SrcBlendAlpha"))
        {
            mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        }

        if (mat.HasProperty("_DstBlendAlpha"))
        {
            mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (mat.HasProperty("_ZWrite"))
        {
            mat.SetFloat("_ZWrite", 0f); // ZWrite off for transparent
        }

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.DisableKeyword("_ALPHAMODULATE_ON");

        // Built-in Standard Shader settings
        if (mat.HasProperty("_Mode"))
        {
            mat.SetFloat("_Mode", 2f); // 2 = Fade mode
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    public void SetOccluded(float targetAlpha)
    {
        if (_rendererDataList.Count == 0)
            InitializeRenderers();

        _targetAlpha = targetAlpha;
        _isOccluded = true;

        if (!_isSwapped)
        {
            _isSwapped = true;
            for (int i = 0; i < _rendererDataList.Count; i++)
            {
                var data = _rendererDataList[i];
                if (data.needsMaterialSwap && data.renderer != null && data.runtimeTransparentMats != null)
                {
                    data.renderer.sharedMaterials = data.runtimeTransparentMats;
                }
            }
        }
    }

    public void ClearOcclusion()
    {
        _isOccluded = false;
        _targetAlpha = 1f;
    }

    public void UpdateFade(float fadeSpeed)
    {
        _lastFadeUpdateFrame = Time.frameCount;
        DoFadeStep(fadeSpeed);
    }

    private void Update()
    {
        // Fallback update if not driven by CameraOcclusionFader this frame
        if (_lastFadeUpdateFrame != Time.frameCount && (_isOccluded || !Mathf.Approximately(_currentAlpha, 1f) || _isSwapped))
        {
            DoFadeStep(8f);
        }
    }

    private void DoFadeStep(float fadeSpeed)
    {
        if (_rendererDataList.Count == 0) return;
        if (!_isOccluded && Mathf.Approximately(_currentAlpha, 1f) && !_isSwapped) return;

        _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, fadeSpeed * Time.deltaTime);

        for (int r = 0; r < _rendererDataList.Count; r++)
        {
            var data = _rendererDataList[r];
            if (data.renderer == null) continue;

            if (data.needsMaterialSwap && _isSwapped)
            {
                data.renderer.sharedMaterials = data.runtimeTransparentMats;
            }

            for (int m = 0; m < data.baseColors.Length; m++)
            {
                _propBlock.Clear();

                Color baseColor = data.baseColors[m];
                Color fadedColor = baseColor;
                fadedColor.a = baseColor.a * _currentAlpha;

                int propId = data.colorPropIDs[m];
                if (propId != 0)
                {
                    _propBlock.SetColor(propId, fadedColor);
                }
                _propBlock.SetColor(BaseColorProp, fadedColor);
                _propBlock.SetColor(ColorProp, fadedColor);

                if (data.baseColors.Length > 1)
                {
                    data.renderer.SetPropertyBlock(_propBlock, m);
                }
                else
                {
                    data.renderer.SetPropertyBlock(_propBlock);
                }

                // Also update runtime material instance if swapped
                if (data.needsMaterialSwap && data.runtimeTransparentMats != null && m < data.runtimeTransparentMats.Length)
                {
                    Material tMat = data.runtimeTransparentMats[m];
                    if (tMat != null)
                    {
                        if (propId != 0) tMat.SetColor(propId, fadedColor);
                        if (tMat.HasProperty(BaseColorProp)) tMat.SetColor(BaseColorProp, fadedColor);
                        if (tMat.HasProperty(ColorProp)) tMat.SetColor(ColorProp, fadedColor);
                    }
                }
            }
        }

        // Restore full default state once fully returned to 1f alpha
        if (!_isOccluded && _currentAlpha >= 0.999f)
        {
            RestoreFullAlpha();
        }
    }

    private void RestoreFullAlpha()
    {
        _currentAlpha = 1f;
        _isSwapped = false;

        for (int i = 0; i < _rendererDataList.Count; i++)
        {
            var data = _rendererDataList[i];
            if (data.renderer == null) continue;

            if (data.needsMaterialSwap && data.originalMaterials != null)
            {
                data.renderer.sharedMaterials = data.originalMaterials;
            }

            // Clear property blocks to restore original default material appearance
            if (data.baseColors != null && data.baseColors.Length > 1)
            {
                for (int m = 0; m < data.baseColors.Length; m++)
                {
                    data.renderer.SetPropertyBlock(null, m);
                }
            }
            data.renderer.SetPropertyBlock(null);
        }
    }

    public bool IsFullyRestored => !_isOccluded && Mathf.Approximately(_currentAlpha, 1f) && !_isSwapped;

    private void CleanupRuntimeMaterials()
    {
        for (int i = 0; i < _rendererDataList.Count; i++)
        {
            var data = _rendererDataList[i];
            if (data.needsMaterialSwap && data.runtimeTransparentMats != null)
            {
                for (int m = 0; m < data.runtimeTransparentMats.Length; m++)
                {
                    if (data.runtimeTransparentMats[m] != null && data.runtimeTransparentMats[m] != data.originalMaterials[m])
                    {
                        Destroy(data.runtimeTransparentMats[m]);
                    }
                }
            }
        }
    }

    private void OnDestroy()
    {
        CleanupRuntimeMaterials();
        _rendererDataList.Clear();
    }
}