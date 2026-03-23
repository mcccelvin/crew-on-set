using UnityEngine;

namespace Player.Equipment
{
    public class SDCardItem : Equipment
    {
        public bool isUsedCard = false;
        public string recordedFileName = "";
        public float videoDuration = 0f;

        // --- NEW: A variable to hold the final grade (0 to 100) ---
        public float videoScore = 0f;

        public override void OnUse(Camera playerCamera)
        {
            if (isUsedCard)
            {
                // --- NEW: Tell the player their score when they inspect it! ---
                Debug.Log($"Card: {recordedFileName} | Length: {videoDuration:F1}s | Score: {videoScore:F0}/100");
            }
            else
            {
                Debug.Log("Inspecting SD Card. It is BLANK.");
            }
        }
    }
}