using UnityEngine;
using UnityEngine.SceneManagement;

// Reuses the authored main-menu entry point; no second profile data/UI implementation.
public sealed class AccountProfileMenuHost : MonoBehaviour
{
    private AlmanacManager manager;
    private bool closingEntry;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { SceneManager.sceneLoaded -= Attach; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install() { SceneManager.sceneLoaded += Attach; }
    private static void Attach(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Account" && scene.name != "Main Menu") return;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas.name == "Account Profile" && canvas.GetComponent<AccountProfileMenuHost>() == null)
                    canvas.gameObject.AddComponent<AccountProfileMenuHost>();
    }
    private void OnEnable()
    {
        closingEntry = false;
        GetComponent<Canvas>().enabled = false;
        if (manager == null) manager = new GameObject("Menu player profile").AddComponent<AlmanacManager>();
        manager.OpenMenuProfile(() =>
        {
            if (this == null || closingEntry || !gameObject.activeInHierarchy) return;
            if (gameObject.scene.name == "Account") LoadingScreenController.LoadScene("Main Menu");
            else gameObject.SetActive(false);
        });
    }
    private void OnDisable()
    {
        closingEntry = true;
        if (manager != null) manager.ClosePlayerProfile();
    }
}
