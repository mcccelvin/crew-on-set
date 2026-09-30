using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public sealed class TutorialStageMarker : MonoBehaviour
{
    private TextMeshPro label;
    private Camera playerView;
    private Material material;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= Setup;
        SceneManager.sceneLoaded += Setup;
    }
    private static void Setup(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var target in root.GetComponentsInChildren<Transform>(true))
            {
                string name = target.name.ToLowerInvariant();
                string title = name == "pointa" ? "LIGHT\n45%" : name == "pointb" ? "PRODUCT" : name == "pointc" ? "CAMERA" : null;
                if (title == null || target.GetComponent<TutorialStageMarker>() != null) continue;
                target.gameObject.AddComponent<TutorialStageMarker>().Build(title);
            }
    }
    private void Build(string title)
    {
        var renderer = GetComponentInChildren<MeshRenderer>(true);
        if (renderer != null)
        {
            material = new Material(renderer.sharedMaterial);
            material.color = Color.green;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.green * .65f);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        foreach (var oldLabel in GetComponentsInChildren<TMP_Text>(true)) oldLabel.gameObject.SetActive(false);
        label = new GameObject("Purpose Label").AddComponent<TextMeshPro>();
        label.transform.SetParent(transform, false);
        Vector3 scale = transform.lossyScale;
        label.transform.localScale = new Vector3(1f / Mathf.Max(.001f, Mathf.Abs(scale.x)), 1f / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1f / Mathf.Max(.001f, Mathf.Abs(scale.z)));
        label.transform.position = transform.position + Vector3.up * .35f;
        label.text = title;
        label.fontSize = 3f;
        label.color = Color.green;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(5f, 1.4f);
    }
    private void LateUpdate()
    {
        if (playerView == null)
        {
            var player = FindObjectOfType<Player.PlayerController.PlayerController>();
            if (player != null) playerView = player.GameplayCamera;
        }
        if (label != null && playerView != null) label.transform.rotation = playerView.transform.rotation;
    }
    private void OnDestroy() { if (material != null) Destroy(material); }
}
