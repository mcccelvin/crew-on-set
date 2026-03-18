using UnityEngine;

public class HideDuringReplay : MonoBehaviour
{
    private Renderer[] allRenderers;

    void Awake()
    {
        // Automatically find all the 3D visual meshes attached to this object
        allRenderers = GetComponentsInChildren<Renderer>(true);
    }

    // Turns the 3D graphics on or off without deleting the object!
    public void SetVisible(bool isVisible)
    {
        foreach (Renderer r in allRenderers)
        {
            if (r != null) r.enabled = isVisible;
        }
    }
}