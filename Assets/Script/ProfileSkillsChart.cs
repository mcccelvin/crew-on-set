using UnityEngine;
using UnityEngine.UI;

// A native UI mesh, not the fixed sample polygon embedded in the old stats PNG.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ProfileSkillsChart : Graphic
{
    public float[] scores = new float[5];
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .46f;
        var outline = new Color32(52, 44, 34, 255);
        Vector2 Point(int i, float amount) { float angle = (90 - i * 72) * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * amount; }
        void Triangle(Vector2 a, Vector2 b, Vector2 c, Color tint)
        { int n = mesh.currentVertCount; mesh.AddVert(a, tint, Vector2.zero); mesh.AddVert(b, tint, Vector2.zero); mesh.AddVert(c, tint, Vector2.zero); mesh.AddTriangle(n, n + 1, n + 2); }
        void Line(Vector2 a, Vector2 b, float width, Color tint)
        { Vector2 d = (b - a).normalized; Vector2 w = new Vector2(-d.y, d.x) * width * .5f; Triangle(a - w, a + w, b + w, tint); Triangle(a - w, b + w, b - w, tint); }
        for (int ring = 1; ring <= 5; ring++) for (int i = 0; i < 5; i++) Line(Point(i, ring / 5f), Point((i + 1) % 5, ring / 5f), ring == 5 ? 3 : 1, ring == 5 ? outline : new Color32(182, 167, 136, 255));
        for (int i = 0; i < 5; i++) Line(Vector2.zero, Point(i, 1), 1, outline);
        for (int i = 0; i < 5; i++)
        {
            Vector2 a = Point(i, Mathf.Clamp01(scores[i] / 100)), b = Point((i + 1) % 5, Mathf.Clamp01(scores[(i + 1) % 5] / 100));
            Triangle(Vector2.zero, a, b, new Color32(244, 162, 20, 150)); Line(a, b, 3, new Color32(173, 94, 12, 255));
        }
    }
}
