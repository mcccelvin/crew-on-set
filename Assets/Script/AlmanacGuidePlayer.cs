using PlayerPrefs = GameSavePrefs;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class AlmanacGuidePlayer : MonoBehaviour
{
    private const int previewLayer = 30;
    private const float guideDuration = 12f;

    [SerializeField] private RawImage previewImage;
    [SerializeField] private TextMeshProUGUI captionText;
    [SerializeField] private TextMeshProUGUI playPauseText;
    private GameObject previewRoot;
    private GameObject subjectGroup;
    private Camera guideCamera;
    private RenderTexture guideTexture;
    private List<Material> guideMaterials = new List<Material>();
    private bool isGuideOpen = false;
    private bool isPlaying = false;
    private float playbackTime = 0f;

    public void Initialize(RawImage targetPreviewImage, TextMeshProUGUI targetCaptionText, TextMeshProUGUI targetPlayPauseText)
    {
        previewImage = targetPreviewImage;
        captionText = targetCaptionText;
        playPauseText = targetPlayPauseText;
    }

    public void OpenGuide(GameObject subjectPrefab)
    {
        if (previewRoot == null) CreateGuideScene(subjectPrefab);

        isGuideOpen = true;
        isPlaying = true;
        playbackTime = 0f;

        if (guideCamera != null) guideCamera.enabled = true;
        if (previewImage != null) previewImage.texture = guideTexture;

        UpdateGuideFrame();
        UpdatePlayPauseText();
    }

    public void CloseGuide()
    {
        isGuideOpen = false;
        isPlaying = false;
        if (guideCamera != null) guideCamera.enabled = false;
        UpdatePlayPauseText();
    }

    public void TogglePlayPause()
    {
        if (!isGuideOpen) return;

        isPlaying = !isPlaying;
        UpdatePlayPauseText();
    }

    public void Replay()
    {
        if (!isGuideOpen) return;

        playbackTime = 0f;
        isPlaying = true;
        UpdateGuideFrame();
        UpdatePlayPauseText();
    }

    private void Update()
    {

        if (!isGuideOpen || !isPlaying) return;

        playbackTime += Time.unscaledDeltaTime;
        if (playbackTime >= guideDuration) playbackTime = 0f;
        UpdateGuideFrame();
    }

    private void UpdateGuideFrame()
    {
        if (subjectGroup == null) return;

        float horizontalPosition = 0f;

        if (playbackTime < 3f)
        {
            horizontalPosition = 0f;
            if (captionText != null) captionText.text = "<color=#FF6B6B>NOT THE REQUESTED COMPOSITION:</color> The product is centered and ignores the Rule of Thirds brief.";
        }
        else if (playbackTime < 5f)
        {
            float movementProgress = Mathf.SmoothStep(0f, 1f, (playbackTime - 3f) / 2f);
            horizontalPosition = Mathf.Lerp(0f, -2.05f, movementProgress);
            if (captionText != null) captionText.text = "Move the product toward a vertical grid line while keeping the important label visible.";
        }
        else if (playbackTime < 9f)
        {
            horizontalPosition = -2.05f;
            if (captionText != null) captionText.text = "<color=#65F28B>STRONG COMPOSITION:</color> The product sits on the left third with useful open space on the right.";
        }
        else
        {
            horizontalPosition = -2.05f;
            if (captionText != null) captionText.text = "Keep the main detail close to a grid intersection, then confirm the product remains fully visible before recording.";
        }

        subjectGroup.transform.localPosition = new Vector3(horizontalPosition, 0f, 0f);
        subjectGroup.transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(playbackTime * 0.8f) * 4f, 0f);
    }

    private void UpdatePlayPauseText()
    {
        if (playPauseText != null) playPauseText.text = isPlaying ? "PAUSE" : "PLAY";
    }

    private void CreateGuideScene(GameObject subjectPrefab)
    {
        previewRoot = new GameObject("Almanac Rule of Thirds Preview");
        previewRoot.transform.position = new Vector3(0f, -1000f, 0f);
        SetLayerRecursively(previewRoot, previewLayer);

        guideTexture = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32);
        guideTexture.name = "Rule of Thirds Guide Texture";
        guideTexture.Create();

        GameObject cameraObject = new GameObject("Guide Camera", typeof(Camera));
        cameraObject.transform.SetParent(previewRoot.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 2.1f, -8f);
        cameraObject.transform.LookAt(previewRoot.transform.TransformPoint(new Vector3(0f, 1.35f, 0f)));
        cameraObject.layer = previewLayer;

        guideCamera = cameraObject.GetComponent<Camera>();
        guideCamera.clearFlags = CameraClearFlags.SolidColor;
        guideCamera.backgroundColor = new Color(0.08f, 0.08f, 0.09f, 1f);
        guideCamera.orthographic = true;
        guideCamera.orthographicSize = 3.2f;
        guideCamera.cullingMask = 1 << previewLayer;
        guideCamera.targetTexture = guideTexture;

        CreatePreviewPrimitive("Red Commercial Backdrop", PrimitiveType.Cube, previewRoot.transform, new Vector3(0f, 2.5f, 2.6f), new Vector3(12f, 7f, 0.25f), new Color(0.55f, 0.045f, 0.055f));
        CreatePreviewPrimitive("Studio Floor", PrimitiveType.Cube, previewRoot.transform, new Vector3(0f, -0.15f, 0f), new Vector3(12f, 0.3f, 8f), new Color(0.55f, 0.42f, 0.25f));

        subjectGroup = new GameObject("Animated Product");
        subjectGroup.transform.SetParent(previewRoot.transform, false);
        subjectGroup.layer = previewLayer;

        CreatePreviewPrimitive("Product Pedestal", PrimitiveType.Cube, subjectGroup.transform, new Vector3(0f, 0.15f, 0f), new Vector3(1.8f, 0.3f, 1.5f), new Color(0.72f, 0.72f, 0.7f));

        if (subjectPrefab != null) CreateGameProduct(subjectPrefab);
        else CreateFallbackProduct();

        CreateGuideLight("Guide Key Light", 1.25f, new Vector3(35f, -35f, 0f));
        CreateGuideLight("Guide Fill Light", 0.45f, new Vector3(25f, 145f, 0f));
    }

    private void CreateGameProduct(GameObject subjectPrefab)
    {
        GameObject subjectObject = Instantiate(subjectPrefab, subjectGroup.transform);
        subjectObject.name = "Goke Product From Game";
        subjectObject.transform.localPosition = Vector3.zero;
        subjectObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        SetLayerRecursively(subjectObject, previewLayer);

        foreach (MonoBehaviour behaviour in subjectObject.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        foreach (Collider subjectCollider in subjectObject.GetComponentsInChildren<Collider>(true)) subjectCollider.enabled = false;
        foreach (Rigidbody subjectBody in subjectObject.GetComponentsInChildren<Rigidbody>(true))
        {
            subjectBody.isKinematic = true;
            subjectBody.useGravity = false;
        }

        Renderer[] subjectRenderers = subjectObject.GetComponentsInChildren<Renderer>(true);
        if (subjectRenderers.Length == 0)
        {
            Destroy(subjectObject);
            CreateFallbackProduct();
            return;
        }

        Bounds subjectBounds = subjectRenderers[0].bounds;
        for (int rendererIndex = 1; rendererIndex < subjectRenderers.Length; rendererIndex++) subjectBounds.Encapsulate(subjectRenderers[rendererIndex].bounds);

        if (subjectBounds.size.y > 0.001f)
        {
            float targetScale = 1.8f / subjectBounds.size.y;
            subjectObject.transform.localScale *= targetScale;

            subjectBounds = subjectRenderers[0].bounds;
            for (int rendererIndex = 1; rendererIndex < subjectRenderers.Length; rendererIndex++) subjectBounds.Encapsulate(subjectRenderers[rendererIndex].bounds);
        }

        Vector3 desiredCenter = subjectGroup.transform.TransformPoint(new Vector3(0f, 1.3f, 0f));
        subjectObject.transform.position += desiredCenter - subjectBounds.center;
    }

    private void CreateFallbackProduct()
    {
        GameObject product = CreatePreviewPrimitive("Goke Guide Can", PrimitiveType.Cylinder, subjectGroup.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.6f, 0.9f, 0.6f), new Color(0.75f, 0.03f, 0.04f));
        product.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

        GameObject label = CreatePreviewPrimitive("Goke Label", PrimitiveType.Cube, product.transform, new Vector3(0f, 0f, -0.51f), new Vector3(0.65f, 0.22f, 0.03f), Color.white);
        label.transform.localRotation = Quaternion.identity;
    }

    private GameObject CreatePreviewPrimitive(string objectName, PrimitiveType primitiveType, Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject previewObject = GameObject.CreatePrimitive(primitiveType);
        previewObject.name = objectName;
        previewObject.transform.SetParent(parent, false);
        previewObject.transform.localPosition = localPosition;
        previewObject.transform.localScale = localScale;
        previewObject.layer = previewLayer;

        Collider previewCollider = previewObject.GetComponent<Collider>();
        if (previewCollider != null) Destroy(previewCollider);

        Renderer previewRenderer = previewObject.GetComponent<Renderer>();
        if (previewRenderer != null)
        {
            Material previewMaterial = CreateGuideMaterial(color);
            previewRenderer.sharedMaterial = previewMaterial;
        }

        return previewObject;
    }

    private Material CreateGuideMaterial(Color color)
    {
        Shader guideShader = Shader.Find("Standard");
        if (guideShader == null) guideShader = Shader.Find("Universal Render Pipeline/Lit");

        Material guideMaterial = new Material(guideShader);
        guideMaterial.color = color;
        guideMaterials.Add(guideMaterial);
        return guideMaterial;
    }

    private void CreateGuideLight(string lightName, float intensity, Vector3 rotation)
    {
        GameObject lightObject = new GameObject(lightName, typeof(Light));
        lightObject.transform.SetParent(previewRoot.transform, false);
        lightObject.transform.localRotation = Quaternion.Euler(rotation);
        lightObject.layer = previewLayer;

        Light guideLight = lightObject.GetComponent<Light>();
        guideLight.type = LightType.Directional;
        guideLight.intensity = intensity;
        guideLight.cullingMask = 1 << previewLayer;
    }

    private void SetLayerRecursively(GameObject targetObject, int targetLayer)
    {
        targetObject.layer = targetLayer;
        foreach (Transform child in targetObject.transform) SetLayerRecursively(child.gameObject, targetLayer);
    }

    private void OnDestroy()
    {
        if (guideCamera != null) guideCamera.targetTexture = null;

        if (guideTexture != null)
        {
            guideTexture.Release();
            Destroy(guideTexture);
        }

        if (previewRoot != null) Destroy(previewRoot);

        foreach (Material guideMaterial in guideMaterials)
        {
            if (guideMaterial != null) Destroy(guideMaterial);
        }
        guideMaterials.Clear();
    }
}

