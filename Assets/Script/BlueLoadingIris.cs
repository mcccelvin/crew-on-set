using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Blue outside a shrinking transparent circle. The existing loading screen
// remains visible through the opening; no video, shader or texture is required.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BlueLoadingIris : MaskableGraphic
{
    // Shared with the progress fill so the wipe always matches the loading bar.
    public static readonly Color32 WipeBlue = new Color32(0, 235, 235, 255);
    private const int Segments = 256;
    private readonly List<float> angles = new List<float>(Segments + 4);
    private readonly UIVertex[] quad = new UIVertex[4];
    [SerializeField, Range(0, 1)] private float closure;

    public float Closure
    {
        get => closure;
        set
        {
            float next = Mathf.Clamp01(value);
            if (next == closure) return;
            closure = next;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        if (closure <= 0 || rect.width <= 0 || rect.height <= 0) return;
        Color32 ink = color;
        if (closure >= 1)
        {
            AddQuad(mesh, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax),
                new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMin), ink, ink);
            return;
        }
        Vector2 half = rect.size * .5f;
        Vector2 centre = rect.center;
        float feather = 1.25f / Mathf.Max(.001f, canvas != null ? canvas.scaleFactor : 1f);
        float radius = (half.magnitude + feather) * (1f - closure);
        angles.Clear();
        for (int i = 0; i < Segments; i++) angles.Add(i * Mathf.PI * 2f / Segments);
        // Exact rectangle-corner spokes prevent uncovered triangles at any aspect ratio.
        float corner = Mathf.Atan2(half.y, half.x);
        angles.Add(corner); angles.Add(Mathf.PI - corner);
        angles.Add(Mathf.PI + corner); angles.Add(Mathf.PI * 2f - corner);
        angles.Sort();
        Color32 transparent = ink; transparent.a = 0;
        for (int i = 0; i < angles.Count; i++)
        {
            Vector2 a = Direction(angles[i]), b = Direction(angles[(i + 1) % angles.Count]);
            float edgeA = EdgeDistance(a, half), edgeB = EdgeDistance(b, half);
            Vector2 insideA = centre + a * Mathf.Min(radius, edgeA);
            Vector2 insideB = centre + b * Mathf.Min(radius, edgeB);
            Vector2 softA = centre + a * Mathf.Min(radius + feather, edgeA);
            Vector2 softB = centre + b * Mathf.Min(radius + feather, edgeB);
            AddQuad(mesh, insideA, insideB, softB, softA, transparent, ink);
            AddQuad(mesh, softA, softB, centre + b * edgeB, centre + a * edgeA, ink, ink);
        }
    }

    private static Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    private static float EdgeDistance(Vector2 direction, Vector2 half) =>
        Mathf.Min(half.x / Mathf.Max(.000001f, Mathf.Abs(direction.x)),
            half.y / Mathf.Max(.000001f, Mathf.Abs(direction.y)));

    private void AddQuad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 inner, Color32 outer)
    {
        for (int i = 0; i < quad.Length; i++) quad[i] = UIVertex.simpleVert;
        quad[0].position = a; quad[1].position = b; quad[2].position = c; quad[3].position = d;
        quad[0].color = quad[1].color = inner; quad[2].color = quad[3].color = outer;
        mesh.AddUIVertexQuad(quad);
    }
}
