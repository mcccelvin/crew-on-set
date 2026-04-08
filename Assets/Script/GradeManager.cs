using UnityEngine;

public class GradeManager : MonoBehaviour
{
    [Header("UI Reference")]
    public FinalGradePanelUI gradePanelUI;

    private void Start()
    {
        // --- THE FIX: Force the mouse to unlock so you can click buttons ---
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EvaluateSmuggledGrades();
    }

    public void EvaluateSmuggledGrades()
    {
        ProductionGrades grades = CrossSceneData.finalGrades;

        // Safety net for testing directly in scene
        if (string.IsNullOrEmpty(grades.letterGrade))
        {
            grades.preProductionScore = 100f;
            grades.productionScore = 85f;
            grades.postProductionScore = 90f;
            grades.letterGrade = "A";
            grades.feedback = "Test Feedback: Great work on the framing!";
            grades.earnedBCoins = 500;
        }

        if (CareerManager.Instance != null)
        {
            CareerManager.Instance.playerMoney += grades.earnedBCoins;
            PlayerPrefs.SetInt("PlayerMoney", CareerManager.Instance.playerMoney);
            PlayerPrefs.Save();
            CareerManager.Instance.CompleteActiveJob(grades.earnedBCoins);
        }

        if (gradePanelUI != null)
        {
            gradePanelUI.DisplayResults(grades);
        }
    }

    // Link your "Return to Studio" button to THIS function
    public void ReturnToStudio()
    {
        // Save tutorial progress if this was the tutorial
        if (PlayerPrefs.GetInt("TutorialProgress", 0) == 0)
        {
            PlayerPrefs.SetInt("TutorialProgress", 1);
            PlayerPrefs.Save();
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene("Studio");
    }
}