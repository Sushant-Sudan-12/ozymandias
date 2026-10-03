using UnityEngine;

public class BillboardItem : MonoBehaviour
{
    public enum BillboardMode
    {
        YAxisOnly,    // Don't Starve style: stays vertically upright (90 deg to floor)
        FullSpherical // Tilts backward to directly face camera lens
    }

    [SerializeField] private BillboardMode mode = BillboardMode.YAxisOnly;
    public BillboardMode Mode => mode;

    private void OnEnable() => BillboardManager.Register(this);
    private void OnDisable() => BillboardManager.Unregister(this);

    public void ApplyRotation(Quaternion rot)
    {
        transform.rotation = rot;
    }
}