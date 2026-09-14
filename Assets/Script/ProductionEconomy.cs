using UnityEngine;

// One source for gameplay prices. Existing bank balances are never converted.
public static class ProductionEconomy
{
    public const int StartingBudget = 9000;
    public const int Camera = 4000, AdvancedCamera = 6000, PanelLight = 1200, SoftLight = 4500, SDCard = 150;
    public const int Prop = 250, Wall = 500, Vehicle = 2500, LightStrip = 900, ActorBase = 750;
    public static int Advance(int level) => level == 2 ? 10500 : level == 3 ? 8500 : level == 4 ? 6500 : level == 5 ? 9500 : StartingBudget;
    public static int CompletionBonus(int level) => level == 1 ? 2000 : level == 2 ? 2500 : level == 3 ? 3000 : level == 4 ? 3500 : 5000;
    public static int EquipmentPrice(string name, int fallback)
    {
        string item = (name ?? "").ToUpperInvariant();
        if (item.Contains("LEVEL 2") && item.Contains("CAMERA")) return AdvancedCamera;
        if (item.Contains("SOFT LIGHT")) return SoftLight;
        if (item.Contains("SD") && item.Contains("CARD")) return SDCard;
        if (item.Contains("160 LED") || item.Contains("LOW LIGHT") || item.Contains("LOWLIGHT")) return PanelLight;
        if (item.Contains("NONY") || item.Contains("LOW CAMERA") || item.Contains("LOWCAM")) return Camera;
        return fallback;
    }
}
