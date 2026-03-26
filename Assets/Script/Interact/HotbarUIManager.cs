using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HotbarUIManager : MonoBehaviour
{
    [Header("UI References")]
    public Image[] slotBackgrounds;
    public TextMeshProUGUI[] slotTexts;
    public Image[] slotIcons;

    [Header("Colors")]
    public Color activeColor = new Color(1f, 1f, 1f, 0.8f);
    public Color inactiveColor = new Color(0f, 0f, 0f, 0.4f);

    private void Start()
    {
        HighlightSlot(0);

        // Hide all icons when the game starts
        for (int i = 0; i < slotIcons.Length; i++)
        {
            if (slotIcons[i] != null) slotIcons[i].gameObject.SetActive(false);
        }
    }

    public void HighlightSlot(int activeIndex)
    {
        for (int i = 0; i < slotBackgrounds.Length; i++)
        {
            if (slotBackgrounds[i] != null)
            {
                slotBackgrounds[i].color = (i == activeIndex) ? activeColor : inactiveColor;
            }
        }
    }

    public void UpdateSlot(int index, string itemName, Sprite itemIcon)
    {
        if (index >= 0 && index < slotTexts.Length && slotTexts[index] != null)
        {
            // --- THE FIX: Always strictly show JUST the slot number, never the equipment name! ---
            slotTexts[index].text = (index + 1).ToString();

            if (index < slotIcons.Length && slotIcons[index] != null)
            {
                if (itemIcon != null)
                {
                    // If we have an image, turn it on!
                    slotIcons[index].sprite = itemIcon;
                    slotIcons[index].gameObject.SetActive(true);
                }
                else
                {
                    // If the slot is empty (or missing an image), hide it completely!
                    slotIcons[index].sprite = null;
                    slotIcons[index].gameObject.SetActive(false);
                }
            }
        }
    }
}