using UnityEngine;
using TMPro;

public class ContractGrader : MonoBehaviour
{
    [Header("Client Feedback UI")]
    public TextMeshProUGUI clientFeedbackText;

    private void Start()
    {
        if (clientFeedbackText != null) clientFeedbackText.text = "Waiting for submission...";
    }

    public void GradeVideo(string fileName)
    {
        // --- GRADING TEMPORARILY DISABLED ---
        // We removed the grading logic from the Studio because 
        // the actual grading will now happen in the EditorScene!

        UpdateFeedback("FOOTAGE SECURED:\nPlease use the 'Send to Editor' button to compile and grade your final commercial in Post-Production.", Color.yellow);
    }

    private void UpdateFeedback(string message, Color color)
    {
        if (clientFeedbackText != null)
        {
            clientFeedbackText.text = message;
            clientFeedbackText.color = color;
        }
        Debug.Log($"GRADER: {message}");
    }
}