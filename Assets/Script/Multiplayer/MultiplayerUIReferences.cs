using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Read-only copies of the authored singleplayer views and their serialized bindings.
public sealed class MultiplayerUIReferences : MonoBehaviour
{
    [Serializable] public sealed class Entry { public string key; public UnityEngine.Object value; }
    public List<Entry> entries = new List<Entry>();
    [Serializable] public sealed class Artwork { public Image image; public string key; }
    public List<Artwork> artwork = new List<Artwork>();
    public int copyVersion;
    public string sourceSignature;
    public List<KnowledgeEntry> knowledge = new List<KnowledgeEntry>();
    public List<ShopItem> shop = new List<ShopItem>();
    public Vector3 deliveryPosition, tabletPosition, computerPosition, shopPosition;
    public GameObject playerModel;
    public RuntimeAnimatorController playerAnimatorController;
    public float playerHeight = 1.85f;
    public Vector3 tabletCameraPosition;
    public Quaternion tabletCameraRotation = Quaternion.identity;
    public float tabletCameraFieldOfView = 58f, tabletCameraSize = 5f;
    public bool hasTabletCamera, tabletCameraOrthographic;
    // Sprite.Create results are transient. Save the art key, then recreate the sprite on the copy.
    public void CaptureArtwork()
    {
        artwork.Clear();
        var catalog = Resources.Load<ExportUIArt>("ExportUIArt");
        foreach (var image in GetComponentsInChildren<Image>(true))
        {
            string key = ArtworkKey(image, catalog);
            if (!string.IsNullOrEmpty(key)) artwork.Add(new Artwork { image = image, key = key });
        }
    }
    public void RestoreArtwork()
    {
        // Also repairs copies made before artwork bindings were serialized.
        if (artwork == null || artwork.Count == 0) { artwork = new List<Artwork>(); CaptureArtwork(); }
        foreach (var item in artwork)
        {
            if (item.image == null || string.IsNullOrEmpty(item.key)) continue;
            var sprite = item.key.EndsWith("/white", StringComparison.Ordinal)
                ? ExportUIArt.GetWhite(item.key.Substring(0, item.key.Length - 6)) : ExportUIArt.Get(item.key);
            if (sprite != null) item.image.sprite = sprite;
        }
    }
    private static string ArtworkKey(Image image, ExportUIArt catalog)
    {
        const string prefix = "UI-EXPORT/";
        if (image.sprite != null) return image.sprite.name.StartsWith(prefix, StringComparison.Ordinal)
            ? image.sprite.name.Substring(prefix.Length) : null;
        if (image.name.StartsWith(prefix, StringComparison.Ordinal)) return image.name.Substring(prefix.Length);
        if (catalog != null && catalog.entries != null)
            foreach (var entry in catalog.entries) if (entry.key == image.name) return entry.key;
        // These are the authored object names in AlmanacBook, CareerManager and ContractUIManager.
        switch (image.name)
        {
            case "Illustrated Almanac": return "psdBook";
            case "Equipment tab": case "Techniques tab": case "Director record": case "Milestones": return "psdTab";
            case "Close book": case "Close brief": return "close";
            case "Previous page": case "Previous contract": return "left";
            case "Next page": case "Next contract": return "right";
            case "Category DIRECTOR": return "psdDirector";
            case "Category LIGHTING": return "psdLight";
            case "Category AUDIO": return "psdAudio";
            case "Category CAMERA": return "psdCamera";
            case "Category EDITING": return "psdEdit";
            case "Watch guide": case "Select contract": case "Accept contract": return "blueButton";
            case "Almanac HUD": return "almanacHud";
            case "Contract folder 0": case "Contract folder 1": case "Contract folder 2": return "psdClosedFolder";
            case "Qualifications Book": return "psdOpenFolder";
            case "Project title tape": return "tape";
            case "Reference photo frame": return "psdPhotoFrame";
            case "Settings frame": return "settingsFrame";
        }
        return null;
    }
    public T Get<T>(string key) where T : UnityEngine.Object
    {
        var entry = entries.Find(e => e.key == key);
        var value = entry != null ? entry.value : null;
        if (value is T typed) return typed;
        var obj = value as GameObject ?? (value as Component)?.gameObject;
        if (typeof(T) == typeof(GameObject)) return obj as T;
        return obj != null && typeof(Component).IsAssignableFrom(typeof(T)) ? obj.GetComponent(typeof(T)) as T : null;
    }
}
