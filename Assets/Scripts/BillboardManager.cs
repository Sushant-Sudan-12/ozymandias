using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class BillboardManager : MonoBehaviour
{
    private static readonly List<BillboardItem> Items = new List<BillboardItem>(1024);

    [SerializeField] private Camera targetCamera;

    public static void Register(BillboardItem item)
    {
        if (!Items.Contains(item))
            Items.Add(item);
    }

    public static void Unregister(BillboardItem item)
    {
        Items.Remove(item);
    }

    private void Awake()
    {
        FindCamera();
    }

    private void LateUpdate()
    {
        if (!targetCamera)
        {
            FindCamera();
            if (!targetCamera) return;
        }

        if (Items.Count == 0) return;

        // 1. Calculate Full Spherical Rotation (Matches camera tilt completely)
        Quaternion fullRotation = targetCamera.transform.rotation;

        // 2. Calculate Y-Axis Only Rotation (Don't Starve style: stays flat upright)
        Vector3 camForward = targetCamera.transform.forward;
        camForward.y = 0f;
        Quaternion yOnlyRotation = camForward.sqrMagnitude > 0.001f 
            ? Quaternion.LookRotation(camForward, Vector3.up) 
            : Quaternion.identity;

        // Batch apply rotation
        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            if (item == null) continue;

            if (item.Mode == BillboardItem.BillboardMode.YAxisOnly)
                item.ApplyRotation(yOnlyRotation);
            else
                item.ApplyRotation(fullRotation);
        }
    }

    private void FindCamera()
    {
        if (Camera.main != null)
            targetCamera = Camera.main;
        else
            targetCamera = FindFirstObjectByType<Camera>();
    }
}