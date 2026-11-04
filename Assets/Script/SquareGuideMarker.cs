using UnityEngine;
using TMPro;

// Visual only: target transforms and lesson completion checks remain unchanged.
public sealed class SquareGuideMarker : MonoBehaviour
{
    private readonly LineRenderer[] corners = new LineRenderer[4];
    private static Material material;

    public static void Apply(GameObject target)
    {
        if (target != null && target.GetComponent<SquareGuideMarker>() == null)
            target.AddComponent<SquareGuideMarker>();
    }

    private void Awake()
    {
        if (material == null)
        {
            var shader = Resources.Load<Shader>("ProductionGuide");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = new Material(shader) { name = "Square guide corners" };
            material.mainTexture = Texture2D.whiteTexture;
        }
        for (int i = 0; i < 4; i++)
        {
            var part = new GameObject("Square corner " + i);
            part.transform.SetParent(transform, false);
            part.transform.rotation = Quaternion.Euler(90, 0, 0);
            var line = corners[i] = part.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = true;
            line.alignment = LineAlignment.TransformZ;
            line.widthMultiplier = .055f; line.positionCount = 3;
            line.startColor = line.endColor = new Color32(255, 196, 75, 255);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }
        LateUpdate();
    }

    private void LateUpdate()
    {
        // Legacy placement code may re-enable the disc: never let it render.
        foreach (var mesh in GetComponentsInChildren<MeshRenderer>(true)) mesh.enabled = false;
        foreach (var label in GetComponentsInChildren<TMP_Text>(true)) label.enabled = false;
        Vector3 centre = transform.position + Vector3.up * .05f;
        for (int i = 0; i < 4; i++)
        {
            if (corners[i] == null) continue;
            float x = i % 2 == 0 ? -1 : 1, z = i < 2 ? -1 : 1;
            Vector3 corner = centre + new Vector3(x * .42f, 0, z * .32f);
            corners[i].SetPosition(0, corner - Vector3.right * x * .18f);
            corners[i].SetPosition(1, corner);
            corners[i].SetPosition(2, corner - Vector3.forward * z * .16f);
        }
    }
}
