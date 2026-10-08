using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Carries the selected cover into place, then unfolds it. No spring or overshoot.
[DisallowMultipleComponent]
public sealed class ContractFolderMotion : MonoBehaviour
{
    [Range(.35f, 1f)] public float duration = .75f;
    [Range(.2f, .7f)] public float approachDuration = .42f;
    [Range(.4f, .6f)] public float spine = .49f;
    private RectTransform book, hinge, overlay;
    private Image artwork;
    private RawImage inside, outside, shadow, pages;
    private CanvasGroup panelGroup;
    private Vector2 position, approachPosition;
    private Vector3 scale, approachScale;
    private bool imageEnabled, panelInteractable, panelRaycasts, prepared, approaching;
    private float panelAlpha;
    private Coroutine opening;
    private readonly List<ContentState> content = new List<ContentState>();

    private struct ContentState { public CanvasGroup group; public float alpha; }

    public void Play(Sprite closedCover, RectTransform selectedFolder = null)
    {
        StopAndRestore();
        if (!Prepare(closedCover)) return;
        BeginSelection(selectedFolder);
        opening = StartCoroutine(Open());
    }

    private bool Prepare(Sprite closedCover)
    {
        book = transform as RectTransform;
        artwork = GetComponent<Image>();
        if (book == null || artwork == null || artwork.sprite == null) return false;
        position = book.anchoredPosition;
        scale = book.localScale;
        imageEnabled = artwork.enabled;
        panelGroup = transform.parent.GetComponent<CanvasGroup>();
        if (panelGroup == null) panelGroup = transform.parent.gameObject.AddComponent<CanvasGroup>();
        panelAlpha = panelGroup.alpha;
        panelInteractable = panelGroup.interactable;
        panelRaycasts = panelGroup.blocksRaycasts;
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = true;
        content.Clear();
        foreach (Transform child in transform)
        {
            if (child == overlay) continue;
            var group = child.GetComponent<CanvasGroup>();
            if (group == null) group = child.gameObject.AddComponent<CanvasGroup>();
            content.Add(new ContentState { group = group, alpha = group.alpha });
        }
        BuildOverlay();
        var sprite = artwork.sprite;
        Rect uv = SpriteUV(sprite);
        pages.texture = sprite.texture;
        pages.uvRect = new Rect(uv.x + uv.width * spine, uv.y, uv.width * (1 - spine), uv.height);
        inside.texture = sprite.texture;
        inside.uvRect = new Rect(uv.x, uv.y, uv.width * spine, uv.height);
        var cover = closedCover != null ? closedCover : sprite;
        var coverUV = closedCover != null ? SpriteUV(cover) : inside.uvRect;
        outside.texture = cover.texture;
        // The outward-facing cover is viewed from the reverse side while folded shut.
        outside.uvRect = new Rect(coverUV.xMax, coverUV.y, -coverUV.width, coverUV.height);
        artwork.enabled = false;
        overlay.gameObject.SetActive(true);
        overlay.SetAsFirstSibling();
        prepared = true;
        Sample(0);
        return true;
    }

    private void BeginSelection(RectTransform selectedFolder)
    {
        if (!prepared || selectedFolder == null || book.parent == null || scale.x <= 0 || scale.y <= 0) return;
        // Capture the displayed card before the offer is hidden. Work in the
        // book parent's coordinates so canvas scaling/alternate resolutions agree.
        var corners = new Vector3[4];
        selectedFolder.GetWorldCorners(corners);
        for (int i = 0; i < corners.Length; i++) corners[i] = book.parent.InverseTransformPoint(corners[i]);
        float width = Vector3.Distance(corners[3], corners[0]);
        float height = Vector3.Distance(corners[1], corners[0]);
        if (width <= .01f || height <= .01f) return;
        Vector2 selectedCenter = (corners[0] + corners[2]) * .5f;

        // Retain the selected card's proportions during the entire zoom. Only
        // the outward cover uses this rect; the inside retains the full open art.
        var coverRect = outside.rectTransform;
        coverRect.anchorMin = coverRect.anchorMax = new Vector2(1, .5f);
        coverRect.pivot = new Vector2(1, .5f);
        coverRect.sizeDelta = new Vector2(book.rect.height * width / height * scale.y / scale.x, book.rect.height);
        coverRect.anchoredPosition = Vector2.zero;
        coverRect.GetWorldCorners(corners);
        for (int i = 0; i < corners.Length; i++) corners[i] = book.parent.InverseTransformPoint(corners[i]);
        Vector2 closedCenter = (corners[0] + corners[2]) * .5f;
        float startScale = height / Vector3.Distance(corners[1], corners[0]);
        Vector2 origin = book.localPosition;
        approachPosition = position + selectedCenter - Vector2.LerpUnclamped(origin, closedCenter, startScale);
        approachScale = scale * startScale;
        approaching = true;
        Sample(0);
    }

    private void BuildOverlay()
    {
        if (overlay == null)
        {
            overlay = new GameObject("Folder opening artwork", typeof(RectTransform)).GetComponent<RectTransform>();
            overlay.SetParent(book, false);
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            pages = Raw("Stationary pages", overlay);
            pages.rectTransform.anchorMin = new Vector2(spine, 0);
            pages.rectTransform.anchorMax = Vector2.one;
            pages.rectTransform.offsetMin = pages.rectTransform.offsetMax = Vector2.zero;
            shadow = Raw("Spine shadow", overlay);
            shadow.texture = Texture2D.whiteTexture;
            shadow.rectTransform.anchorMin = shadow.rectTransform.anchorMax = new Vector2(spine, .5f);
            shadow.rectTransform.pivot = new Vector2(0, .5f);
            hinge = new GameObject("Hinged cover", typeof(RectTransform)).GetComponent<RectTransform>();
            hinge.SetParent(overlay, false);
            hinge.anchorMin = hinge.anchorMax = new Vector2(spine, .5f);
            hinge.pivot = new Vector2(1, .5f);
            inside = Raw("Inside cover", hinge);
            outside = Raw("Outside cover", hinge);
        }
        hinge.sizeDelta = new Vector2(book.rect.width * spine, book.rect.height);
        hinge.anchoredPosition = Vector2.zero;
        outside.rectTransform.anchorMin = Vector2.zero;
        outside.rectTransform.anchorMax = Vector2.one;
        outside.rectTransform.pivot = new Vector2(.5f, .5f);
        outside.rectTransform.offsetMin = outside.rectTransform.offsetMax = Vector2.zero;
        shadow.rectTransform.sizeDelta = new Vector2(34, book.rect.height * .73f);
    }

    private static RawImage Raw(string name, Transform parent)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(parent, false);
        image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        image.raycastTarget = false;
        return image;
    }

    private static Rect SpriteUV(Sprite sprite)
    {
        // These runtime artwork sprites include transparent canvas margins.
        // GetOuterUV trims those margins; stretching that crop into the full
        // book rect makes the animation zoom in, then snap smaller on Restore.
        Rect rect = sprite.rect;
        return new Rect(rect.x / sprite.texture.width, rect.y / sprite.texture.height,
            rect.width / sprite.texture.width, rect.height / sprite.texture.height);
    }

    private IEnumerator Open()
    {
        float elapsed = 0;
        float totalDuration = Mathf.Max(.01f, duration) + (approaching ? Mathf.Max(.01f, approachDuration) : 0);
        while (elapsed < totalDuration)
        {
            Sample(elapsed / totalDuration);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        Sample(1);
        opening = null;
        Restore();
    }

    private void Sample(float progress)
    {
        if (!prepared) return;
        float t = Mathf.Clamp01(progress);
        float openProgress = t;
        if (approaching)
        {
            float zoomEnd = Mathf.Max(.01f, approachDuration) / (Mathf.Max(.01f, approachDuration) + Mathf.Max(.01f, duration));
            float zoom = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / zoomEnd));
            book.anchoredPosition = Vector2.Lerp(approachPosition, position, zoom);
            book.localScale = Vector3.Lerp(approachScale, scale, zoom);
            openProgress = Mathf.Clamp01((t - zoomEnd) / (1 - zoomEnd));
        }
        else
        {
            book.anchoredPosition = position;
            book.localScale = scale;
        }
        float eased = Mathf.SmoothStep(0, 1, openProgress);
        float angle = Mathf.Lerp(180, 0, eased);
        hinge.localRotation = Quaternion.Euler(0, angle, 0);
        bool front = angle > 90;
        outside.gameObject.SetActive(front);
        inside.gameObject.SetActive(!front);
        float fold = Mathf.Sin(angle * Mathf.Deg2Rad);
        inside.color = new Color(1 - .16f * fold, 1 - .16f * fold, 1 - .16f * fold, 1);
        shadow.color = new Color(.12f, .08f, .03f, .2f * Mathf.Max(0, fold));
        // Arrive before the hinge turns; no second scale animation or size snap.
        pages.color = new Color(1, 1, 1, approaching ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(openProgress / .18f)) : 1);
        panelGroup.alpha = approaching ? panelAlpha : panelAlpha * Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .18f));
        float reveal = Mathf.SmoothStep(0, 1, Mathf.Clamp01((openProgress - .48f) / .42f));
        foreach (var state in content) if (state.group != null) state.group.alpha = state.alpha * reveal;
    }

    private void Restore()
    {
        if (!prepared) return;
        if (book != null) { book.anchoredPosition = position; book.localScale = scale; }
        if (artwork != null) artwork.enabled = imageEnabled;
        if (overlay != null) overlay.gameObject.SetActive(false);
        if (panelGroup != null)
        { panelGroup.alpha = panelAlpha; panelGroup.interactable = panelInteractable; panelGroup.blocksRaycasts = panelRaycasts; }
        foreach (var state in content) if (state.group != null) state.group.alpha = state.alpha;
        content.Clear();
        prepared = false;
        approaching = false;
    }

    private void StopAndRestore()
    {
        if (opening != null) StopCoroutine(opening);
        opening = null;
        Restore();
    }

    private void OnDisable() { StopAndRestore(); }
}
