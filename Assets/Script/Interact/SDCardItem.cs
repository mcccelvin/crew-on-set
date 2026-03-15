using UnityEngine;

namespace Player.Equipment
{
    public class SDCardItem : Equipment
    {
        [Tooltip("Check this if this card already has a recording on it")]
        public bool isUsedCard = false;

        // Optional: What happens if the player left-clicks while holding the SD card
        public override void OnUse(Camera playerCamera)
        {
            Debug.Log($"You are inspecting the SD Card. Status: {(isUsedCard ? "USED" : "BLANK")}");
        }
    }
}