using PlayerPrefs = GameSavePrefs;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public sealed class MainMenuOptionsHost : MonoBehaviour
{
    private SharedOptionsPanel view;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install(){SceneManager.sceneLoaded-=OnScene;SceneManager.sceneLoaded+=OnScene;}
    private static void OnScene(Scene scene,LoadSceneMode mode)
    {
        if(scene.name!="Main Menu")return;
        foreach(var root in scene.GetRootGameObjects())foreach(var rect in root.GetComponentsInChildren<RectTransform>(true))
            if(rect.name=="Options"&&rect.GetComponent<Button>()==null&&rect.GetComponent<MainMenuOptionsHost>()==null){rect.gameObject.AddComponent<MainMenuOptionsHost>();return;}
    }
    private void OnEnable()
    {
        if(view==null){foreach(Transform child in transform)child.gameObject.SetActive(false);view=new SharedOptionsPanel(transform,()=>gameObject.SetActive(false));}
        view.Open();
    }
    private void Update(){if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)view?.Close(false);}
    private void OnDisable(){view?.Close(false);}
}
