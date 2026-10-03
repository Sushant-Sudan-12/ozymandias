using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class DynamicSpriteSorter : MonoBehaviour
{
    [SerializeField] private int baseSortingOrder = 5000;
    [SerializeField] private float precisionMultiplier = 100f;
    [SerializeField] private float yPivotOffset = 0f;

    private SpriteRenderer _renderer;
    private Camera _mainCam;
    private Transform _camTransform;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _mainCam = Camera.main;
        if (_mainCam != null) _camTransform = _mainCam.transform;
    }

    private void LateUpdate()
    {
        if (_camTransform == null)
        {
            if (Camera.main != null)
            {
                _mainCam = Camera.main;
                _camTransform = _mainCam.transform;
            }
            return;
        }

        // Project position onto camera's forward vector (view-space depth)
        Vector3 worldPos = transform.position + new Vector3(0f, yPivotOffset, 0f);
        float distanceToCam = Vector3.Dot(worldPos - _camTransform.position, _camTransform.forward);

        // Closer objects get higher sortingOrder (rendered on top)
        _renderer.sortingOrder = baseSortingOrder - Mathf.RoundToInt(distanceToCam * precisionMultiplier);
    }
}