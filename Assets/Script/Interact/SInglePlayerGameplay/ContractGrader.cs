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

        Vector4 gradeData = computer.GetTapeGrade(fileName);
        float duration = gradeData.x;
        float score = gradeData.y;
        float camScore = gradeData.z;
        float lightScore = gradeData.w;

        // --- 1. DURATION CHECKS ---
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

        // --- 2. CAMERA & LIGHTING CHECKS ---
        if (score < 70f)
        {
            UpdateFeedback($"CLIENT REJECTED:\nThe footage is too sloppy (Total: {score:F0}/100).\nCamera Work: {camScore:F0}/70\nLighting: {lightScore:F0}/30", Color.red);
            return;
        }

        // --- 3. THE NEW STAGE SETUP CHECK! ---
        StageSetupManager stage = FindObjectOfType<StageSetupManager>();
        if (stage != null)
        {
            // Did they even spawn the wall?
            if (!stage.HasWall())
            {
                UpdateFeedback("CLIENT REJECTED:\nLazy set design! The vase is just sitting in an empty room. Use the Stage Terminal to build a backdrop.", Color.red);
                return;
            }

            // Is the wall the correct color?
            if (stage.currentWallColor != Color.red)
            {
                UpdateFeedback("CLIENT REJECTED:\nWrong brand colors! We explicitly asked for a RED background for the Crystal Blooms commercial.", Color.red);
                return;
            }
        }

        // --- PASS! ---
        string passMessage = $"CLIENT APPROVED!\n\n" +
                             $"Camera Work: {camScore:F0} / 70\n" +
                             $"Lighting: {lightScore:F0} / 30\n" +
                             $"Set Design: Perfect Red Backdrop\n" +
                             $"------------------\n" +
                             $"TOTAL SCORE: {score:F0} / 100\n\n" +
                             $"Outstanding work! Payment transferred.";

        UpdateFeedback(passMessage, Color.green);

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