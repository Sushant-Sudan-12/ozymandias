using UnityEngine;

public class FadeObstacle : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    private float _targetAlpha = 1f;
    private float _currentAlpha = 1f;
    private bool _isOccluding = false;

    private void Awake()
    {
        if (!spriteRenderer)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void SetOccluded(float alpha)
    {
        _isOccluding = true;
        _targetAlpha = alpha;
    }

    public void ClearOcclusion()
    {
        _isOccluding = false;
        _targetAlpha = 1f;
    }

    public void UpdateFade(float fadeSpeed)
    {
        if (spriteRenderer == null) return;

        _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, fadeSpeed * Time.deltaTime);
        Color c = spriteRenderer.color;
        c.a = _currentAlpha;
        spriteRenderer.color = c;
    }

    public bool IsFullyRestored => !_isOccluding && Mathf.Approximately(_currentAlpha, 1f);
}