using UnityEngine;

// Own the runtime wall-mesh copy across previews and set replacements.
public sealed class GeneratedFurnitureMesh : MonoBehaviour
{
    public Mesh mesh;
    private void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
