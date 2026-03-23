using UnityEngine;
using TMPro;

public class ContractGrader : MonoBehaviour
{
    [Header("Client Feedback UI")]
    public TextMeshProUGUI clientFeedbackText;

    private ComputerStation computer;

    private void Start()
    {
        computer = FindObjectOfType<ComputerStation>();
        if (clientFeedbackText != null) clientFeedbackText.text = "Waiting for submission...";
    }

    public void GradeVideo(string fileName)
    {
        if (computer == null) return;

        // Grab the duration (X) and score (Y) from the computer memory
        Vector2 gradeData = computer.GetTapeGrade(fileName);
        float duration = gradeData.x;
        float score = gradeData.y;

        // --- LEVEL 1 RULES ---
        // 1. Length: Must be between 18 and 22 seconds
        // 2. Score: Must be greater than 70/100 (good framing and focus)

        if (duration < 18f)
        {
            UpdateFeedback("CLIENT REJECTED:\nVideo is too short! We need 20 seconds of footage.", Color.red);
            return;
        }

        if (duration > 22f)
        {
            UpdateFeedback("CLIENT REJECTED:\nVideo is too long! We strictly need a 20-second teaser.", Color.red);
            return;
        }

        if (score < 70f)
        {
            UpdateFeedback($"CLIENT REJECTED:\nThe camera work is too sloppy (Score: {score:F0}/100). Make sure the vase is centered and in perfect focus!", Color.red);
            return;
        }

        // --- PASS! ---
        UpdateFeedback($"CLIENT APPROVED!\nScore: {score:F0}/100. Outstanding work! Payment transferred.", Color.green);

        // Call the custom method you already built to handle the payout and update the UI!
        if (CareerManager.Instance != null)
        {
            CareerManager.Instance.CompleteActiveJob(30000);
        }
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