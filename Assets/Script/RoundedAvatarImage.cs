using UnityEngine;
using UnityEngine.UI;

// Clip the portrait geometry itself, preserving the authored gold button surround.
public sealed class RoundedAvatarImage : RawImage
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = GetPixelAdjustedRect();
        if (rect.width <= 0 || rect.height <= 0) return;
        // The menu artwork uses a nonuniform RectTransform scale. Use proportional
        // radii on each axis so its on-screen corners remain evenly rounded.
        Vector2 radius = rect.size * .24f;
        AddVertex(mesh, rect.center, rect);
        const int segments = 12;
        for (int corner = 0; corner < 4; corner++)
        {
            var center = new Vector2(
                corner == 0 || corner == 3 ? rect.xMax - radius.x : rect.xMin + radius.x,
                corner < 2 ? rect.yMax - radius.y : rect.yMin + radius.y);
            for (int step = 0; step <= segments; step++)
            {
                float angle = (corner * 90f + step * 90f / segments) * Mathf.Deg2Rad;
                AddVertex(mesh, center + Vector2.Scale(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), radius), rect);
            }
        }
        int perimeter = 4 * (segments + 1);
        for (int index = 1; index <= perimeter; index++)
            mesh.AddTriangle(0, index, index == perimeter ? 1 : index + 1);
    }

    private void AddVertex(VertexHelper mesh, Vector2 position, Rect rect)
    {
        var uv = new Vector2(
            uvRect.x + (position.x - rect.xMin) / rect.width * uvRect.width,
            uvRect.y + (position.y - rect.yMin) / rect.height * uvRect.height);
        mesh.AddVert(position, color, uv);
    }
}
