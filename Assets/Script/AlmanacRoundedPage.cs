using UnityEngine;
using UnityEngine.UI;

// Rounded geometry clips the paper texture itself, including its shadow.
public sealed class AlmanacRoundedPage : RawImage
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        float radius = Mathf.Min(34f, Mathf.Min(rect.width, rect.height) * .5f);
        AddVertex(mesh, rect.center, rect);
        const int segments = 12;
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 center = new Vector2(
                corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius,
                corner < 2 ? rect.yMax - radius : rect.yMin + radius);
            for (int step = 0; step <= segments; step++)
            {
                float angle = (corner * 90f + step * 90f / segments) * Mathf.Deg2Rad;
                AddVertex(mesh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, rect);
            }
        }
        int count = 4 * (segments + 1);
        for (int i = 0; i < count; i++)
            mesh.AddTriangle(0, 1 + (i + 1) % count, 1 + i);
    }

    private void AddVertex(VertexHelper mesh, Vector2 point, Rect rect)
    {
        var vertex = UIVertex.simpleVert;
        vertex.position = point;
        vertex.color = color;
        vertex.uv0 = new Vector2(
            uvRect.x + Mathf.InverseLerp(rect.xMin, rect.xMax, point.x) * uvRect.width,
            uvRect.y + Mathf.InverseLerp(rect.yMin, rect.yMax, point.y) * uvRect.height);
        mesh.AddVert(vertex);
    }
}
