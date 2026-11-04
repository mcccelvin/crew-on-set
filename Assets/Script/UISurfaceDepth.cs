using UnityEngine;
using UnityEngine.UI;

// Shared depth treatment. Works on the surface mesh, never on labels or photos.
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class UISurfaceDepth : BaseMeshEffect
{
    private Shadow addedShadow;
    public static void Apply(Image image)
    {
        if (image == null || image.type == Image.Type.Filled || image.GetComponent<Mask>() != null ||
            image.GetComponent<RectMask2D>() != null || image.color.a < .2f) return;
        var canvas = image.GetComponentInParent<Canvas>(true);
        if (canvas != null && canvas.rootCanvas.renderMode == RenderMode.WorldSpace) return;
        if (image.GetComponent<UISurfaceDepth>() == null) image.gameObject.AddComponent<UISurfaceDepth>();
    }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || vertices.currentVertCount == 0) return;
        var image = graphic as Image;
        if (image == null || image.color.a < .2f) return;
        // Authored bevel artwork keeps its original shading. Flat paper and
        // untextured controls gain a subtle lit top and shaded lower edge.
        bool flat = image.sprite == null || image.sprite.texture.name == "Crew paper corners";
        if (!flat) return;
        var rect = image.rectTransform.rect;
        UIVertex vertex = default(UIVertex);
        for (int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            float t = Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y);
            Color color = vertex.color;
            Color shaded = Color.Lerp(color * .87f, Color.Lerp(color, Color.white, .12f), t);
            shaded.a = color.a;
            vertex.color = shaded;
            vertices.SetUIVertex(vertex, i);
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (addedShadow != null) return;
        // Reuse existing drop shadows; Outline is also a Shadow subclass.
        foreach (var shadow in GetComponents<Shadow>())
            if (!(shadow is Outline) && shadow.enabled) return;
        var depth = gameObject.AddComponent<Shadow>();
        addedShadow = depth;
        depth.effectColor = new Color(0.08f, .05f, .03f, .28f);
        depth.effectDistance = new Vector2(3, -4);
        depth.useGraphicAlpha = true;
    }
}
