using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A tessellated paper leaf; geometry and text share the same curl at each animation frame.
public sealed class AlmanacPageCurl : MaskableGraphic
{
    public Texture texture;
    public Rect textureRegion;
    public float curl;
    public override Texture mainTexture => texture != null ? texture : Texture2D.whiteTexture;

    public Vector3 Bend(Vector3 point)
    {
        Rect rect = rectTransform.rect;
        float distance = Mathf.Clamp01((rect.xMax - point.x) / rect.width);
        float angle = curl * (1.35f + .2f * distance);
        point.x = rect.xMax - distance * rect.width * Mathf.Cos(angle);
        point.y += Mathf.Sin(distance * Mathf.PI * .5f) * curl * 38f *
            ((point.y - rect.center.y) / (rect.height * .5f));
        return point;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        const int columns = 48, rows = 24;
        for (int y = 0; y <= rows; y++)
        for (int x = 0; x <= columns; x++)
        {
            float u = x / (float)columns, v = y / (float)rows;
            var vertex = UIVertex.simpleVert;
            vertex.position = Bend(new Vector3(Mathf.Lerp(rect.xMin, rect.xMax, u), Mathf.Lerp(rect.yMin, rect.yMax, v)));
            float shade = 1f - curl * (.08f + .25f * Mathf.Sin(u * Mathf.PI));
            vertex.color = new Color(shade, shade, shade, 1f);
            vertex.uv0 = new Vector2(textureRegion.x + u * textureRegion.width, textureRegion.y + v * textureRegion.height);
            mesh.AddVert(vertex);
            if (x == columns || y == rows) continue;
            int index = y * (columns + 1) + x;
            mesh.AddTriangle(index, index + columns + 1, index + 1);
            mesh.AddTriangle(index + 1, index + columns + 1, index + columns + 2);
        }
    }

    public void SetFrame(float value)
    {
        curl = value;
        SetVerticesDirty();
        foreach (var text in GetComponentsInChildren<TextMeshProUGUI>())
        {
            text.ForceMeshUpdate();
            var info = text.textInfo;
            for (int i = 0; i < info.meshInfo.Length; i++)
            {
                var vertices = info.meshInfo[i].vertices;
                for (int j = 0; j < info.meshInfo[i].vertexCount; j++)
                {
                    Vector3 local = rectTransform.InverseTransformPoint(text.transform.TransformPoint(vertices[j]));
                    vertices[j] = text.transform.InverseTransformPoint(rectTransform.TransformPoint(Bend(local)));
                }
            }
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }
    }
}
