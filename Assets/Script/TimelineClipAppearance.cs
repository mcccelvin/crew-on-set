using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation only: timeline duration, drag and trim calculations remain unchanged.
public sealed class TimelineClipAppearance : MonoBehaviour
{
    private DraggableClip clip;
    private GameObject face;
    private TextMeshProUGUI title, bankDetails;
    private Image fill;
    private readonly Image[] trimEdges = new Image[2];
    private RawImage thumbnail, sourceThumbnail;
    private Graphic[] originals;
    private bool[] originalStates;

    private void Start()
    {
        clip = GetComponent<DraggableClip>();
        originals = GetComponentsInChildren<Graphic>(true);
        originalStates = new bool[originals.Length];
        for (int i = 0; i < originals.Length; i++) originalStates[i] = originals[i].enabled;
        sourceThumbnail = GetComponentInChildren<RawImage>(true);
        face = new GameObject("Timeline Clip Face", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(LayoutElement));
        face.GetComponent<LayoutElement>().ignoreLayout = true;
        var rect = (RectTransform)face.transform;
        rect.SetParent(transform, false);
        Stretch(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        fill = face.GetComponent<Image>();
        fill.color = new Color32(35, 66, 86, 255); fill.raycastTarget = false;
        var header = Make<Image>("Clip Header");
        Stretch(header.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(4, -30), new Vector2(-4, 0));
        header.color = new Color32(48, 97, 124, 255);
        thumbnail = Make<RawImage>("Source Thumbnail");
        Stretch(thumbnail.rectTransform, Vector2.zero, new Vector2(0, 1), new Vector2(10, 8), new Vector2(120, -36));
        title = Make<TextMeshProUGUI>("Clip Name and Duration");
        Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(12, -28), new Vector2(-12, -2));
        title.font = TMP_Settings.defaultFontAsset;
        title.fontSize = 16; title.color = Color.white;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.enableWordWrapping = false; title.overflowMode = TextOverflowModes.Ellipsis;
        bankDetails = Make<TextMeshProUGUI>("Source Details");
        Stretch(bankDetails.rectTransform, Vector2.zero, Vector2.one, new Vector2(132, 8), new Vector2(-12, -36));
        bankDetails.font = TMP_Settings.defaultFontAsset;
        bankDetails.fontSize = 16;
        bankDetails.color = new Color32(199, 218, 229, 255);
        bankDetails.alignment = TextAlignmentOptions.MidlineLeft;
        bankDetails.enableWordWrapping = true;
        bankDetails.overflowMode = TextOverflowModes.Ellipsis;
        for (int side = 0; side < 2; side++)
        {
            var edge = Make<Image>("Trim Edge");
            trimEdges[side] = edge;
            Stretch(edge.rectTransform, new Vector2(side, 0), new Vector2(side, 1),
                new Vector2(side == 0 ? 0 : -4, 0), new Vector2(side == 0 ? 4 : 0, 0));
            edge.color = new Color32(106, 180, 209, 255);
        }
        // Keep original objects for existing thumbnail references, but replace their visuals.
        for (int i = 0; i < originals.Length; i++)
            if (originals[i] != null && originals[i].gameObject != gameObject) originals[i].enabled = false;
    }

    private T Make<T>(string name) where T : Graphic
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(T));
        obj.transform.SetParent(face.transform, false);
        var graphic = obj.GetComponent<T>(); graphic.raycastTarget = false;
        return graphic;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = low; rect.offsetMax = high;
    }

    private void LateUpdate()
    {
        if (clip == null || face == null) return;
        bool timeline = clip.isOnTimeline;
        foreach (var edge in trimEdges) edge.enabled = timeline;
        fill.color = timeline ? new Color32(35, 66, 86, 255) : new Color32(35, 44, 53, 255);
        float duration = Mathf.Max(0, timeline ? clip.endFrame - clip.startFrame : clip.totalFrames) / Mathf.Max(1f, TapeSettings.framesPerSecond);
        title.text = Path.GetFileNameWithoutExtension(clip.clipFilePath) + "  |  " + duration.ToString("0.0") + " s";
        bankDetails.gameObject.SetActive(!timeline);
        bankDetails.text = "<b>SOURCE VIDEO</b>\n<size=85%>Drag to the video track</size>";
        thumbnail.texture = sourceThumbnail != null ? sourceThumbnail.texture : null;
        thumbnail.enabled = thumbnail.texture != null && ((RectTransform)transform).rect.width > 145f;
        bankDetails.rectTransform.offsetMin = new Vector2(thumbnail.enabled ? 132f : 12f, 8f);
    }

    private void OnDestroy()
    {
        if (originals != null)
            for (int i = 0; i < originals.Length; i++)
                if (originals[i] != null) originals[i].enabled = originalStates[i];
        if (face != null) Destroy(face);
    }
}
