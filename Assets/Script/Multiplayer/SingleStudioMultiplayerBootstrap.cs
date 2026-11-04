using Photon.Pun;
using UnityEngine;

// The same authored scene serves both modes. Run before solo Awake/Start so
// tutorial controllers and the local career player cannot claim this room.
[DefaultExecutionOrder(-32000)]
public sealed class SingleStudioMultiplayerBootstrap : MonoBehaviour
{
    private void Awake()
    {
        if (!PhotonNetwork.InRoom || gameObject.scene.name != "SingleStudio") return;
        if (!GameSavePrefs.IsRoomSession) GameSavePrefs.BeginRoomSession();
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            if (root.name == "Controllers" || root.name == "UI" || root.CompareTag("Player"))
            {
                root.SetActive(false);
                continue;
            }
            // Physical stations remain in place; room adapters own their input.
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (script is DirectorTerminal || script is ShopTerminal || script is ComputerStation || script is TutorialGlowTarget)
                    script.enabled = false;
        }
        StudioArrivalTour.CancelForMultiplayer();
        var network = new GameObject("Shared studio multiplayer session");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(network,gameObject.scene);
        network.AddComponent<PUNSpawner>();
    }
}
