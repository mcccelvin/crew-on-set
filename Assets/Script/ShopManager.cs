using UnityEngine;

public class ShopManager : MonoBehaviour
{
    [Header("Shop Inventory")]
    [Tooltip("Drag the equipment prefabs you want to sell here")]
    public GameObject[] equipmentPrefabs;

    [Tooltip("Type the B coin cost for each item in the exact same order")]
    public int[] equipmentCosts;

    [Header("Delivery")]
    [Tooltip("Where should the item spawn when bought? (e.g., a delivery box or table)")]
    public Transform deliveryZone;

    // Link this to your "Buy" buttons! (0 for first item, 1 for second, etc.)
    public void BuyItem(int itemIndex)
    {
        // 1. Safety check to make sure the item exists in our lists
        if (itemIndex < 0 || itemIndex >= equipmentPrefabs.Length || itemIndex >= equipmentCosts.Length)
        {
            Debug.LogWarning("Shop Error: Item index is out of range!");
            return;
        }

        int cost = equipmentCosts[itemIndex];

        // 2. Check if the player has enough B coins in their CareerManager!
        if (CareerManager.Instance != null && CareerManager.Instance.TrySpendMoney(cost))
        {
            // 3. Spawn the brand new equipment at the delivery zone
            GameObject boughtItem = Instantiate(equipmentPrefabs[itemIndex], deliveryZone.position, deliveryZone.rotation);

            Debug.Log($"SUCCESS: Bought item {itemIndex} for {cost} B coins! Remaining balance: {CareerManager.Instance.playerMoney}");

            // 4. Tell the Boss you bought an item so the tutorial can continue!
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnEquipmentBought();
            }
        }
        else
        {
            // If they are too broke, tell them!
            Debug.Log("DECLINED: Not enough B coins to buy this item!");
        }
    }

}
