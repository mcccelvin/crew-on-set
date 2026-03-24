using UnityEngine;

namespace Player.Equipment
{
    public class SDCardItem : Equipment
    {
        public bool isUsedCard = false;
        public string recordedFileName = "";
        public float videoDuration = 0f;

        public float videoScore = 0f; // The Total (out of 100)
        public float cameraScore = 0f; // NEW: Camera only (out of 70)
        public float lightScore = 0f;  // NEW: Light only (out of 30)

        public override void OnUse(Camera playerCamera)
        {
            if (isUsedCard)
            {
                // Tells you the split score when you click to inspect it!
                Debug.Log($"Card: {recordedFileName} | Cam: {cameraScore:F0}/70 | Light: {lightScore:F0}/30 | Total: {videoScore:F0}/100");
            }
            else
            {
                Debug.Log("Inspecting SD Card. It is BLANK.");
            }
        }
    }
}