using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    public void QuitGame()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    public void ChangeScene(int scene)
    {
        string target = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(scene));
        if (SceneManager.GetActiveScene().name == "Main Menu" && (target == "CutScene" || target == "SingleStudio"))
        {
            GameSaveManager.Ensure().OpenMenu();
            return;
        }
        SceneManager.LoadScene(scene);
    }
}
