using UnityEngine;
using System.Collections.Generic;

public class TutorialGlowTarget : MonoBehaviour
{
    [Header("Glow Settings")]
    public Color glowColor = Color.yellow;
    public float pulseSpeed = 2f;
    public float maxIntensity = 1.5f;

    [Header("Missing Parts?")]
    [Tooltip("Drag the Monitor, Tower, and Mouse here so they glow too!")]
    public GameObject[] extraPartsToGlow;

    private List<Material> objectMaterials = new List<Material>();
    private List<Color> originalEmissions = new List<Color>();
    private bool isGlowing = false;
    private readonly List<MeshRenderer> visualRenderers = new List<MeshRenderer>();

    private void CollectVisualRenderers()
    {
        if (visualRenderers.Count > 0) return;
        visualRenderers.AddRange(GetComponentsInChildren<MeshRenderer>());
        if (extraPartsToGlow == null) return;
        foreach (var part in extraPartsToGlow)
        {
            if (part == null) continue;
            foreach (var renderer in part.GetComponentsInChildren<MeshRenderer>())
                if (!visualRenderers.Contains(renderer)) visualRenderers.Add(renderer);
        }
    }

    public bool TryGetVisualBounds(out Bounds bounds)
    {
        CollectVisualRenderers();
        bounds = new Bounds();
        bool found = false;
        foreach (var renderer in visualRenderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found;
    }

    private void Start()
    {
        CollectVisualRenderers();

        // 3. Set up the emission for everything we found
        foreach (MeshRenderer renderer in visualRenderers)
        {
            if (renderer != null && renderer.material != null)
            {
                Material mat = renderer.material;
                mat.EnableKeyword("_EMISSION");

                objectMaterials.Add(mat);

                if (mat.HasProperty("_EmissionColor"))
                {
                    originalEmissions.Add(mat.GetColor("_EmissionColor"));
                }
                else
                {
                    originalEmissions.Add(Color.black);
                }
            }
        }
    }

    private void Update()
    {
        if (isGlowing && objectMaterials.Count > 0)
        {
            float emission = Mathf.PingPong(Time.time * pulseSpeed, maxIntensity);
            Color finalColor = glowColor * Mathf.LinearToGammaSpace(emission);

            foreach (Material mat in objectMaterials)
            {
                if (mat != null)
                {
                    mat.SetColor("_EmissionColor", finalColor);
                }
            }
        }
    }

    public void StartGlowing()
    {
        isGlowing = true;
    }

    public void StopGlowing()
    {
        isGlowing = false;

        for (int i = 0; i < objectMaterials.Count; i++)
        {
            if (objectMaterials[i] != null)
            {
                objectMaterials[i].SetColor("_EmissionColor", originalEmissions[i]);
            }
        }
    }

    private void OnDestroy()
    {
        foreach (Material mat in objectMaterials)
        {
            if (mat != null) Destroy(mat);
        }

        objectMaterials.Clear();
        originalEmissions.Clear();
    }
}
