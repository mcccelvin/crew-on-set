using System.Collections.Generic;
using System.Collections;
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
    private bool populating;
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
        if (populating || !isActiveAndEnabled) return;
        StartCoroutine(PopulateBinRoutine());
    }

    private IEnumerator PopulateBinRoutine()
    {
        if (overlaysBinContent == null || brandingPrefabTemplate == null) yield break;
        populating = true;
        try
        {

        var bankRect = overlaysBinContent as RectTransform;
        if (bankRect != null)
        {
            bankRect.anchorMin = Vector2.zero;
            bankRect.anchorMax = Vector2.one;
            bankRect.offsetMin = new Vector2(36f, 254f);
            bankRect.offsetMax = new Vector2(-36f, -90f);
        }
        // The scene's disabled layout left every spawned logo at the same position.
        var oldLayout = overlaysBinContent.GetComponent<LayoutGroup>();
        if (oldLayout != null && !(oldLayout is HorizontalLayoutGroup))
        {
            oldLayout.enabled = false;
            Destroy(oldLayout);
            // Destroy is deferred. Unity rejects the replacement until the old
            // LayoutGroup has actually been removed at the end of this frame.
            yield return null;
            if (overlaysBinContent == null || brandingPrefabTemplate == null) yield break;
        }
        var layout = overlaysBinContent.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = overlaysBinContent.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.enabled = true;
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        layout.childScaleWidth = layout.childScaleHeight = false;

        // 1. Clear the bin
        foreach (Transform child in overlaysBinContent)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        // 2. Check Progress
        int currentLevel = CampaignProgression.GetCurrentLevel();
        List<BrandingData> activeList = currentLevel == 1 ? tutorialLogos : level1Logos;

        // 3. Spawn and Assign Sprites
        int spawnedCount = 0;
        foreach (BrandingData data in activeList)
        {
            if (data.logoSprite == null)
            {
                Debug.LogWarning("Overlay sprite is missing: " + data.logoName, this);
                continue;
            }
            GameObject newLogo = Instantiate(brandingPrefabTemplate, overlaysBinContent);
            spawnedCount++;

            // Set the Sprite
            Image logoImage = newLogo.GetComponent<Image>();
            if (logoImage != null)
            {
                logoImage.sprite = data.logoSprite;
                logoImage.preserveAspect = true;
            }
            newLogo.transform.localScale = Vector3.one;
            var fitter = newLogo.GetComponent<AspectRatioFitter>();
            if (fitter != null) fitter.enabled = false;
            var itemLayout = newLogo.GetComponent<LayoutElement>();
            if (itemLayout == null) itemLayout = newLogo.AddComponent<LayoutElement>();
            itemLayout.ignoreLayout = false;
            itemLayout.minWidth = itemLayout.minHeight = 0f;
            itemLayout.preferredWidth = itemLayout.preferredHeight = 0f;
            itemLayout.flexibleWidth = itemLayout.flexibleHeight = 1f;

            // Optional: Name the object for easier debugging
            newLogo.name = "Logo_" + data.logoName;
        }

        Debug.Log($"Populated bin with {spawnedCount} sprites.");
        if (bankRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(bankRect);
        }
        finally { populating = false; }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        populating = false;
    }
}
