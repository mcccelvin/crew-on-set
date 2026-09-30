using UnityEngine;
using UnityEngine.UI;

// Code-drawn iris; no texture imports or scene references required.
public sealed class LoadingSwirlGraphic : MaskableGraphic
{
    public float radius;
    public float rotation;
    public void SetShape(float size, float angle)
    {
        radius = size; rotation = angle; SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        const int segments = 160;
        Vector2 center = rectTransform.rect.center;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2 / segments;
            float b = (i + 1) * Mathf.PI * 2 / segments;
            float ra = radius * (1 + .065f * Mathf.Sin(a * 5 + rotation));
            float rb = radius * (1 + .065f * Mathf.Sin(b * 5 + rotation));
            Color tint = Color.Lerp(color, new Color(.12f, .65f, 1f, 1f),
                .22f * (1 + Mathf.Sin(a * 5 + rotation)));
            int start = vh.currentVertCount;
            vh.AddVert(center, tint, Vector2.zero);
            vh.AddVert(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ra, tint, Vector2.zero);
            vh.AddVert(center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * rb, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }
    }
}
