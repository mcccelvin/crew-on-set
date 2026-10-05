using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Flatten authored graphic meshes into the video pixels, not interactive export UI copies.
// No camera, scene layer, external encoder or Editor-only API is needed.
public sealed class VideoFrameCompositor : System.IDisposable
{
    public const int OutputWidth = 1920;
    public const int OutputHeight = 1080;
    private sealed class Drawing
    {
        public readonly Mesh mesh = new Mesh();
        public readonly CombineInstance[] copy = new CombineInstance[1];
        public Material material;
    }
    private readonly Dictionary<Graphic, Drawing> drawings = new Dictionary<Graphic, Drawing>();
    private readonly CommandBuffer commands = new CommandBuffer { name = "Embed commercial branding" };
    private RenderTexture frame;

    public Texture Compose(Texture footage, RectTransform monitor, DraggableOverlay[] overlays)
    {
        if (footage == null || monitor == null || monitor.rect.width <= 0 || monitor.rect.height <= 0) return footage;
        // Graphics use the commercial output resolution, even over a 64x64 test
        // tape. Upscale the footage first, then rasterize the original artwork.
        if (frame == null)
        {
            frame = new RenderTexture(OutputWidth, OutputHeight, 0, RenderTextureFormat.ARGB32)
                { name = "Commercial footage with embedded graphics", filterMode = FilterMode.Bilinear,
                  useMipMap = false, autoGenerateMips = false };
            frame.Create();
        }
        // Always start from the source frame: repeated scrubs must not accumulate logos.
        Graphics.Blit(footage, frame);
        commands.Clear(); commands.SetRenderTarget(frame);
        commands.SetViewport(new Rect(0, 0, frame.width, frame.height));
        commands.SetViewProjectionMatrices(Matrix4x4.identity,
            // Coordinates are already texture pixels, not an offscreen camera image.
            // Applying the camera render-texture Y flip would mirror overlay placement.
            GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(0, frame.width, 0, frame.height, -100, 100), false));
        float originalZTest = Shader.GetGlobalFloat("unity_GUIZTestMode");
        commands.SetGlobalFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
        var toPixels = MonitorToPixels(monitor.rect, frame.width, frame.height) * monitor.worldToLocalMatrix;
        if (overlays != null) foreach (var overlay in overlays)
        {
            if (overlay == null || !overlay.isOnTimeline || !overlay.gameObject.activeInHierarchy) continue;
            // Only graphic descendants of placed overlays, never guides, handles or the bin.
            foreach (var graphic in overlay.GetComponentsInChildren<Graphic>())
            {
                if (!graphic.enabled) continue;
                if (!drawings.TryGetValue(graphic, out var drawing))
                    drawings.Add(graphic, drawing = new Drawing());
                var sourceMesh = graphic.canvasRenderer.GetMesh();
                if (sourceMesh == null || sourceMesh.vertexCount == 0) continue;
                drawing.copy[0] = new CombineInstance { mesh = sourceMesh, transform = Matrix4x4.identity };
                drawing.mesh.Clear(); drawing.mesh.CombineMeshes(drawing.copy, true, false);
                if (drawing.mesh.vertexCount == 0) continue;
                var sourceMaterial = graphic.materialForRendering;
                if (drawing.material == null || drawing.material.shader != sourceMaterial.shader)
                {
                    if (drawing.material != null) Release(drawing.material);
                    drawing.material = new Material(sourceMaterial) { name = "Embedded " + graphic.name };
                }
                drawing.material.CopyPropertiesFromMaterial(sourceMaterial);
                drawing.material.mainTexture = graphic.mainTexture;
                // The output texture bounds are the clip. Do not carry scene UI stencil masks.
                drawing.material.DisableKeyword("UNITY_UI_CLIP_RECT");
                if (drawing.material.HasProperty("_StencilComp")) drawing.material.SetInt("_StencilComp", (int)CompareFunction.Always);
                if (drawing.material.HasProperty("_Stencil")) drawing.material.SetInt("_Stencil", 0);
                if (drawing.material.HasProperty("_StencilOp")) drawing.material.SetInt("_StencilOp", (int)StencilOp.Keep);
                float alpha = OverlayAlpha(graphic.transform, overlay.transform);
                if (alpha <= 0) continue;
                var colors = drawing.mesh.colors;
                Color tint = graphic.canvasRenderer.GetColor(); tint.a *= alpha;
                for (int i = 0; i < colors.Length; i++) colors[i] *= tint;
                drawing.mesh.colors = colors;
                commands.DrawMesh(drawing.mesh, toPixels * graphic.rectTransform.localToWorldMatrix, drawing.material, 0, 0);
            }
        }
        commands.SetGlobalFloat("unity_GUIZTestMode", originalZTest);
        var previousTarget = RenderTexture.active;
        try { Graphics.ExecuteCommandBuffer(commands); }
        finally { RenderTexture.active = previousTarget; }
        return frame;
    }

    public static Matrix4x4 MonitorToPixels(Rect monitor, float width, float height)
    {
        float x = width / monitor.width, y = height / monitor.height;
        return Matrix4x4.TRS(new Vector3(-monitor.xMin*x, -monitor.yMin*y, 0), Quaternion.identity, new Vector3(x,y,1));
    }
    private static float OverlayAlpha(Transform graphic, Transform root)
    {
        float alpha = 1;
        for (var item = graphic; item != null; item = item.parent)
        {
            foreach (var group in item.GetComponents<CanvasGroup>()) if (group.enabled) alpha *= group.alpha;
            if (item == root) break;
        }
        return alpha;
    }
    public void Dispose()
    {
        commands.Dispose();
        foreach (var drawing in drawings.Values)
        { Release(drawing.mesh); if (drawing.material != null) Release(drawing.material); }
        drawings.Clear();
        if (frame != null) { frame.Release(); Release(frame); frame = null; }
    }
    private static void Release(Object value)
    { if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
}
