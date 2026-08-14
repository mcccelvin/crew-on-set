using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Required for Image component

[System.Serializable]
public struct BrandingData
{
    public string logoName;
    public Sprite logoSprite;
}

public class BrandingBinManager : MonoBehaviour
{
    [Header("Settings")]
    public GameObject brandingPrefabTemplate; // The Master Prefab from Step 1
    public Transform overlaysBinContent;      // The Scroll View Content

    [Header("Tutorial Assets")]
    public List<BrandingData> tutorialLogos = new List<BrandingData>();

    [Header("Level 1 Assets")]
    public List<BrandingData> level1Logos = new List<BrandingData>();

    private void Start()
    {
        PopulateBin();
    }

    public void PopulateBin()
    {
        if (overlaysBinContent == null || brandingPrefabTemplate == null) return;

        // 1. Clear the bin
        foreach (Transform child in overlaysBinContent) Destroy(child.gameObject);

        // 2. Check Progress
        int currentLevel = CampaignProgression.GetCurrentLevel();
        List<BrandingData> activeList = currentLevel == 1 ? tutorialLogos : level1Logos;

        // 3. Spawn and Assign Sprites
        foreach (BrandingData data in activeList)
        {
            GameObject newLogo = Instantiate(brandingPrefabTemplate, overlaysBinContent);

            // Set the Sprite
            Image logoImage = newLogo.GetComponent<Image>();
            if (logoImage != null) logoImage.sprite = data.logoSprite;

            // Optional: Name the object for easier debugging
            newLogo.name = "Logo_" + data.logoName;
        }

        Debug.Log($"Populated bin with {activeList.Count} sprites.");
    }
}
