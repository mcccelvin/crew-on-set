using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Typography only: keep the original paper, artwork, controls and their layout.
public static class LoginScreenPresentation
{
    private static readonly Color Ink = new Color32(48, 35, 27, 255);
    private static readonly Color Blue = new Color32(29, 76, 119, 255);
    private static TMP_FontAsset font;
    private static Material material;

    public static void Apply(TMP_InputField email, TMP_InputField password,
        TMP_InputField recoveryEmail, TMP_Text message)
    {
        if (email == null || password == null || email.transform.parent == null) return;
        font = font != null ? font : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF") ?? TMP_Settings.defaultFontAsset;
        if (font == null) return;
        if (material == null)
        {
            material = new Material(font.material) { name = "Sign-in readable text" };
            material.DisableKeyword("OUTLINE_ON");
            material.SetColor("_FaceColor", Color.white);
            material.SetFloat("_OutlineWidth", 0);
            material.SetFloat("_OutlineSoftness", 0);
        }
        var login = email.transform.parent;
        TextIn(login);
        Field(email, "Enter email");
        Field(password, "Enter password");
        var caption = login.Find("l-register")?.GetComponent<TMP_Text>();
        if (caption != null) caption.text = "Don't have an account?";
        KeyboardBinding(login, email, login.Find("login")?.GetComponent<Button>());
        if (recoveryEmail != null)
        {
            var recovery = recoveryEmail.transform.parent;
            TextIn(recovery);
            Field(recoveryEmail, "Enter email");
            KeyboardBinding(recovery, recoveryEmail, recovery.Find("resetpass")?.GetComponent<Button>());
        }
        // AccountManager routes feedback through GameFeedback's readable notice card.
        // Hide the old inline label, which was positioned across the form heading.
        if (message != null)
        {
            message.text = "";
            message.gameObject.SetActive(false);
        }
    }

    private static void TextIn(Transform parent)
    {
        foreach (var text in parent.GetComponentsInChildren<TMP_Text>(true))
        {
            var button = text.GetComponentInParent<Button>(true);
            bool primary = button != null &&
                (button.name == "login" || button.name == "login (1)" || button.name == "resetpass");
            Style(text, primary ? 42 : 23, primary ? Color.white : button != null ? Blue : Ink, button != null);
            if (primary)
            {
                text.enableAutoSizing = true;
                text.fontSizeMin = 20; text.fontSizeMax = 42;
            }
        }
    }

    private static void Field(TMP_InputField input, string hint)
    {
        input.fontAsset = font;
        input.richText = false;
        input.customCaretColor = true; input.caretColor = Ink;
        input.selectionColor = new Color32(85, 147, 202, 80);
        // Keep the original field artwork readable when focused.
        var colors = input.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(255, 249, 232, 255);
        colors.selectedColor = new Color32(240, 248, 255, 255);
        colors.pressedColor = colors.selectedColor;
        input.colors = colors;
        Style(input.textComponent, 28, Ink, false);
        InputText(input.textComponent);
        if (input.placeholder is TMP_Text placeholder)
        {
            Style(placeholder, 28, new Color32(103, 76, 51, 255), false);
            InputText(placeholder);
            placeholder.text = hint;
        }
        input.ForceLabelUpdate();
    }

    private static void InputText(TMP_Text text)
    {
        if (text == null) return;
        var margin = text.margin;
        // Preserve the icon-side inset; remove the old vertical text clipping.
        text.margin = new Vector4(margin.x, 0, margin.z, 0);
        text.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private static void KeyboardBinding(Transform panel, Selectable first, Button submit)
    {
        var binding = panel.GetComponent<ChangeInput>();
        if (binding != null) { binding.firstInput = first; binding.submitButton = submit; }
    }

    private static void Style(TMP_Text text, float points, Color color, bool bold)
    {
        if (text == null) return;
        text.font = font; text.fontSharedMaterial = material; text.color = color;
        text.fontSize = points; text.enableAutoSizing = false;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.characterSpacing = 0; text.richText = false;
        text.UpdateMeshPadding();
        // Keep authored geometry and the signup row's original text offsets.
    }
}
