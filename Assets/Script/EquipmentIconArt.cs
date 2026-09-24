using UnityEngine;
using UnityEngine.UI;

// Shared by shop cards and held equipment; textures remain editable in ExportUIArt.
public static class EquipmentIconArt
{
    public static Sprite Get(string name, Sprite fallback = null)
    {
        string key;
        switch ((name ?? "").ToUpperInvariant())
        {
            case "NONY FX": case "FILMCAMERA": case "FILM CAMERA": key = "low_cam"; break;
            case "LEVEL 2 CAMERA": key = "mid_cam"; break;
            case "160 LED PANEL": case "FILMLIGHT": case "FILM LIGHT": key = "low_light"; break;
            case "LEVEL 3 SOFT LIGHT": key = "mid_light_softbox"; break;
            case "LIGHT STRIP": key = "high_light"; break;
            case "DIRECTOR MEGAPHONE": key = "low_director_megaphone"; break;
            case "DIRECTOR TABLET": case "TABLET": key = "low_director_tablet"; break;
            case "SD CARD": case "SDCARD": key = "sdcard_brown"; break;
            case "RECORDED SD CARD": key = "sdcard_red"; break;
            default: return fallback;
        }
        return ExportUIArt.Get("equipment_" + key) ?? fallback;
    }

    public static void Apply(Transform card, string name)
    {
        if (card == null) return;
        Sprite sprite = Get(name);
        if (sprite == null) return;
        foreach (Image image in card.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null || image.transform == card ||
                image.GetComponentInParent<Button>() != null) continue;
            string old = image.sprite.name.ToLowerInvariant();
            // Match artwork only, preserving panel backgrounds and button decorations.
            if (!(old.Contains("cam") || old.Contains("light") || old.Contains("sdcard") ||
                old.Contains("megaphone") || old.Contains("equipment_") || old.Contains("fx3"))) continue;
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = Color.white;
        }
    }
}
