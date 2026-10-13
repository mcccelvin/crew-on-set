using System.Collections.Generic;
using UnityEngine;

// Instance-owned repair: leave imported clothing and its material assets unchanged.
public sealed class CosmeticClothingSurface : MonoBehaviour
{
    private Mesh surface;

    public void Initialize(SkinnedMeshRenderer skin)
    {
        var source = skin.sharedMesh;
        if (source == null || !source.isReadable) return;
        surface = Instantiate(source);
        surface.name = source.name + " closed fabric surface";
        var vertices = source.vertices;
        var weights = source.boneWeights;
        // Vertices split for UVs or hard normals must still move together at seams.
        if (weights.Length == vertices.Length)
        {
            var seams = new Dictionary<Vector3Int, BoneWeight>();
            float precision = 100000f / Mathf.Max(source.bounds.size.magnitude, .001f);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i] * precision;
                var key = new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
                if (seams.TryGetValue(key, out var weight)) weights[i] = weight;
                else seams.Add(key, weights[i]);
            }
            surface.boneWeights = weights;
        }
        // Clothing shells have no thickness. Add reverse faces in the same submesh
        // so sleeves/necklines remain visible without replacing the outfit shader.
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            if (source.GetTopology(sub) != MeshTopology.Triangles) continue;
            int[] front = source.GetTriangles(sub);
            var both = new int[front.Length * 2];
            System.Array.Copy(front, both, front.Length);
            for (int i = 0; i < front.Length; i += 3)
            {
                both[front.Length + i] = front[i];
                both[front.Length + i + 1] = front[i + 2];
                both[front.Length + i + 2] = front[i + 1];
            }
            surface.SetTriangles(both, sub, false);
        }
        skin.sharedMesh = surface;
    }

    private void OnDestroy()
    {
        if (surface == null) return;
        if (Application.isPlaying) Destroy(surface); else DestroyImmediate(surface);
    }
}
