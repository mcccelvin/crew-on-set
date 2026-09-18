using UnityEngine;
using UnityEngine.UI;
public sealed class CircularSliderKnob : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        const int segments = 48;
        mesh.AddVert(center, color, new Vector2(0.5f, 0.5f));
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            mesh.AddVert(center + direction * radius, color, Vector2.one * 0.5f + direction * 0.5f);
        }
        for (int i = 0; i < segments; i++)
            mesh.AddTriangle(0, i + 1, (i + 1) % segments + 1);
    }
}
