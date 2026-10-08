using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Small, reusable paper cards for in-game notices and task guidance.
public static class CrewPaperStyle
{
    public static readonly Color32 Paper = new Color32(252, 245, 220, 255);
    public static readonly Color32 Ink = new Color32(73, 46, 29, 255);
    public static readonly Color32 MutedInk = new Color32(127, 95, 65, 255);
    public static readonly Color32 Gold = new Color32(255, 196, 75, 255);
    public const float HintGap = 12f;
    public const float HintVerticalPadding = 12f;
    private const float HintSidePadding = 18f;
    private static Sprite corners;

    public static void Round(Image image)
    {
        if (image == null) return;
        if (corners == null)
        {
            const int edge = 48; const float radius = 10;
            var texture = new Texture2D(edge, edge, TextureFormat.RGBA32, false)
                { name = "Crew paper corners", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[edge * edge];
            for (int y = 0; y < edge; y++) for (int x = 0; x < edge; x++)
            {
                var point = new Vector2(x + .5f, y + .5f);
                var nearest = new Vector2(Mathf.Clamp(point.x, radius, edge - radius), Mathf.Clamp(point.y, radius, edge - radius));
                pixels[y * edge + x] = new Color(1, 1, 1, Mathf.Clamp01(radius + .5f - Vector2.Distance(point, nearest)));
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            corners = Sprite.Create(texture, new Rect(0, 0, edge, edge), Vector2.one * .5f, 100, 0,
                SpriteMeshType.FullRect, Vector4.one * 12);
        }
        image.overrideSprite = null;
        image.sprite = corners;
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
    }

    public static void Card(Image image)
    {
        if (image == null) return;
        Round(image); image.color = Paper;
        var outline = image.GetComponent<Outline>();
        if (outline == null) outline = image.gameObject.AddComponent<Outline>();
        outline.enabled = true; outline.effectColor = Ink; outline.effectDistance = new Vector2(2, -2);
        var shadow = image.GetComponent<Shadow>();
        // Outline derives from Shadow; select a separate drop-shadow component.
        foreach (var effect in image.GetComponents<Shadow>()) if (!(effect is Outline)) shadow = effect;
        if (shadow == null || shadow is Outline) shadow = image.gameObject.AddComponent<Shadow>();
        shadow.enabled = true; shadow.effectColor = new Color32(42, 24, 15, 95); shadow.effectDistance = new Vector2(3, -4);
    }

    public static void ActionButton(Button button, bool danger = false, bool primary = false)
    {
        if (button == null) return;
        var image = button.GetComponent<Image>();
        if (image == null) return;
        Card(image);
        image.color = danger ? new Color32(158, 62, 39, 255) : primary ? Gold : Paper;
        image.raycastTarget = true;
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = Color.white, highlightedColor = new Color(1, .95f, .83f),
            selectedColor = new Color(1, .95f, .83f), pressedColor = new Color(.82f, .75f, .65f),
            disabledColor = new Color(.7f, .7f, .7f, .7f), colorMultiplier = 1, fadeDuration = .1f
        };
        foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            label.color = danger ? Paper : Ink;
            label.raycastTarget = false;
        }
    }

    public static void Hint(RectTransform row, TMP_Text label, Image icon, Image underline)
    {
        if (row == null || label == null) return;
        var background = row.GetComponent<Image>();
        if (background == null) background = row.gameObject.AddComponent<Image>();
        Card(background);
        foreach (var graphic in row.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        float inset = HintInset(icon);
        label.rectTransform.localScale = Vector3.one;
        label.rectTransform.localRotation = Quaternion.identity;
        label.margin = Vector4.zero;
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(inset, HintVerticalPadding);
        label.rectTransform.offsetMax = new Vector2(-HintSidePadding, -HintVerticalPadding);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = true; label.overflowMode = TextOverflowModes.Overflow;
        if (icon != null)
        {
            icon.color = Gold;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, .5f);
            icon.rectTransform.pivot = Vector2.one * .5f;
            icon.rectTransform.anchoredPosition = new Vector2(24, 0); icon.rectTransform.sizeDelta = new Vector2(18, 18);
        }
        if (underline != null)
        {
            underline.color = Gold;
            underline.rectTransform.anchorMin = Vector2.zero; underline.rectTransform.anchorMax = new Vector2(1, 0);
            underline.rectTransform.pivot = new Vector2(.5f, 0);
            underline.rectTransform.offsetMin = new Vector2(16, 5); underline.rectTransform.offsetMax = new Vector2(-16, 8);
        }
    }

    private static float HintInset(Image icon) => icon != null && icon.gameObject.activeSelf ? 48 : HintSidePadding;
    public static float HintHeight(TMP_Text label, float width, Image icon) => label == null ? 54 :
        Mathf.Max(54, Mathf.Ceil(label.GetPreferredValues(label.text,
            Mathf.Max(1, width - HintInset(icon) - HintSidePadding), Mathf.Infinity).y) + HintVerticalPadding * 2);

    // Own the geometry, so wrapping and animation cannot leave cards in old slots.
    public static float PlaceHint(RectTransform row, TMP_Text label, Image icon, Image underline,
        Vector2 topLeft, float width)
    {
        Hint(row, label, icon, underline);
        float height = HintHeight(label, width, icon);
        row.localScale = Vector3.one;
        row.localRotation = Quaternion.identity;
        row.pivot = new Vector2(0, 1);
        row.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        row.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        row.anchoredPosition = topLeft;
        return height;
    }

    public sealed class HintStack
    {
        private RectTransform origin;
        private Vector2 topLeft, layoutTopLeft, anchorMin, anchorMax;
        private float width, layoutWidth, offset;

        public void Begin(RectTransform firstRow, bool dockTopRight = false)
        {
            if (origin != firstRow)
            {
                origin = firstRow;
                // Bake the prefab's stretched X scale into width; text and padding stay unscaled.
                width = firstRow.rect.width * Mathf.Abs(firstRow.localScale.x);
                topLeft = (Vector2)firstRow.localPosition + new Vector2(
                    -width * firstRow.pivot.x,
                    firstRow.rect.height * (1 - firstRow.pivot.y) * Mathf.Abs(firstRow.localScale.y));
                anchorMin = firstRow.anchorMin;
                anchorMax = firstRow.anchorMax;
                var parent = firstRow.parent as RectTransform;
                if (parent != null) topLeft -= AnchorPoint(parent);
            }
            layoutTopLeft = topLeft;
            layoutWidth = width;
            var bounds = firstRow.parent as RectTransform;
            if (bounds != null)
            {
                Vector2 anchor = AnchorPoint(bounds);
                layoutWidth = Mathf.Min(width, Mathf.Max(1, bounds.rect.width - HintSidePadding * 2));
                layoutTopLeft.x = Mathf.Clamp(topLeft.x, bounds.rect.xMin + HintSidePadding - anchor.x,
                    bounds.rect.xMax - HintSidePadding - anchor.x - layoutWidth);
                layoutTopLeft.y = Mathf.Min(topLeft.y, bounds.rect.yMax - HintVerticalPadding - anchor.y);
                if (dockTopRight)
                {
                    layoutTopLeft.x = bounds.rect.xMax - HintSidePadding - anchor.x - layoutWidth;
                    layoutTopLeft.y = bounds.rect.yMax - 24 - anchor.y;
                }
            }
            offset = 0;
        }

        private Vector2 AnchorPoint(RectTransform parent) => parent.rect.min +
            Vector2.Scale(parent.rect.size, new Vector2(anchorMin.x, anchorMax.y));

        public void Add(RectTransform row, TMP_Text label, Image icon, Image underline)
        {
            if (origin == null || row == null || label == null || !row.gameObject.activeSelf) return;
            row.anchorMin = anchorMin;
            row.anchorMax = anchorMax;
            offset += PlaceHint(row, label, icon, underline, layoutTopLeft + Vector2.down * offset, layoutWidth) + HintGap;
        }
    }
}
