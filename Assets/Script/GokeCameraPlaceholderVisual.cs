using UnityEngine;

public class GokeCameraPlaceholderVisual : MonoBehaviour
{
    private void Awake()
    {
        Renderer[] cameraRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer cameraRenderer in cameraRenderers)
        {
            cameraRenderer.enabled = false;
        }

        GameObject placeholderModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placeholderModel.name = "Level 2 Camera Placeholder Model";
        placeholderModel.layer = gameObject.layer;
        placeholderModel.transform.SetParent(transform, false);
        placeholderModel.transform.localPosition = new Vector3(-0.11f, 0.24f, 0.42f);
        placeholderModel.transform.localRotation = Quaternion.identity;
        placeholderModel.transform.localScale = new Vector3(0.23f, 0.45f, 0.82f);

        Collider placeholderCollider = placeholderModel.GetComponent<Collider>();
        if (placeholderCollider != null)
        {
            placeholderCollider.enabled = false;
            Destroy(placeholderCollider);
        }

        Renderer placeholderRenderer = placeholderModel.GetComponent<Renderer>();
        if (placeholderRenderer != null) placeholderRenderer.material.color = new Color(0.15f, 0.2f, 0.25f);
    }
}
