using UnityEngine;

namespace Player.Equipment
{
    public class SDCardItem : Equipment
    {
        [Tooltip("Check this if this card already has a recording on it")]
        public bool isUsedCard = false;

        [Tooltip("The exact name of the JSON file this card holds")]
        public string recordedFileName = "";

        // If the player left-clicks while holding the card, read the label!
        public override void OnUse(Camera playerCamera)
        {
            if (isUsedCard)
            {
                Debug.Log($"Inspecting SD Card. It holds the recording: {recordedFileName}");
            }
            else
            {
                Debug.Log("Inspecting SD Card. It is BLANK.");
            }
        }
    }
}