using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class AlmanacManager
{
    [SerializeField] private GameObject profileCanvas;
    [SerializeField] private Button profileBackButton, profileCloseButton;
    private bool isProfileOpen;
    private RawImage profileCharacterImage;
    private RenderTexture profileCharacterTexture;
    private TMP_Text profileAccountId;
    private Image profileCardImage;

    private void ApplyAccountProfileStyle()
    {
        var stage = profileCanvas.transform.Find("Profile backdrop/Profile stage");
        if (stage == null) return;
        var logo = stage.Find("Director portrait");
        if (logo != null) logo.gameObject.SetActive(false);
        var caption = stage.Find("Director caption");
        if (caption != null) caption.gameObject.SetActive(false);
        var card = stage.Find("Career card");
        if (card == null || playerInfoPanel == null) return;
        profileCardImage = card.GetComponent<Image>();
        ExportUIArt.Apply(profileCardImage, "profileAccount");
        profileCardImage.color = Color.white;
        var title = playerInfoPanel.transform.Find("Section Title");
        if (title != null) title.gameObject.SetActive(false);
        var panelImage = playerInfoPanel.GetComponent<Image>();
        if (panelImage != null) panelImage.color = Color.clear;
        SetRect(playerNameText.rectTransform, Vector2.one * .5f, Vector2.one * .5f, new Vector2(60, 235), new Vector2(640, 70));
        playerNameText.fontSize = 30;
        if (profileAccountId == null)
            profileAccountId = BookText(playerInfoPanel.transform, "Account ID", new Vector2(95, 137), new Vector2(560, 45), 24);
        profileAccountId.richText = false;
        profileAccountId.color = new Color32(54, 46, 38, 255);
        profileAccountId.text = PlayerPrefs.GetString("PlayFabId", "GUEST");
        profileAccountId.enableAutoSizing = true;
        profileAccountId.fontSizeMin = 16;
        profileAccountId.fontSizeMax = 24;
        var rows = new[] { playerMoneyText, totalJobsText, currentLevelText, activeContractText };
        for (int i = 0; i < rows.Length; i++)
            if (rows[i] != null)
                SetRect(rows[i].rectTransform, Vector2.one * .5f, Vector2.one * .5f, new Vector2(60, -45 - i * 65), new Vector2(640, 60));
        if (profileCharacterImage == null)
        {
            var preview = new GameObject("Character preview", typeof(RectTransform), typeof(RawImage));
            preview.transform.SetParent(stage, false);
            profileCharacterImage = preview.GetComponent<RawImage>();
            profileCharacterImage.raycastTarget = false;
            SetRect(preview.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, new Vector2(-455, -30), new Vector2(560, 700));
        }
    }

    private void ReleaseProfilePreview()
    {
        if (profileCharacterImage != null) profileCharacterImage.texture = null;
        if (profileCharacterTexture == null) return;
        profileCharacterTexture.Release();
        Destroy(profileCharacterTexture);
        profileCharacterTexture = null;
    }

    private void RenderProfileCharacter()
    {
        ReleaseProfilePreview();
        var catalog = Resources.Load<ProductModelCatalog>("ProductModels");
        GameObject portraitModel = null;
        if (catalog != null && catalog.coffeeActorModels != null)
            foreach (var model in catalog.coffeeActorModels)
                if (model != null && model.name == "DefaultCharacGirlRig") { portraitModel = model; break; }
        if (profileCharacterImage == null || portraitModel == null) return;
        var portrait = Instantiate(portraitModel);
        portrait.transform.SetPositionAndRotation(new Vector3(20000, 20000, 20000), Quaternion.identity);
        foreach (var animator in portrait.GetComponentsInChildren<Animator>()) animator.enabled = false;
        // Copy meshes only: never clone gameplay scripts, colliders, cameras or inventory.
        var root = new GameObject("Profile mesh snapshot");
        var meshes = new System.Collections.Generic.List<Mesh>();
        root.transform.position = new Vector3(10000, 10000, 10000);
        try
        {
            Bounds bounds = new Bounds();
            bool hasBounds = false;
            foreach (var source in portrait.GetComponentsInChildren<Renderer>())
            {
                if (!source.enabled) continue;
                Mesh mesh = null;
                if (source is SkinnedMeshRenderer skin)
                {
                    mesh = new Mesh();
                    skin.BakeMesh(mesh);
                    meshes.Add(mesh);
                }
                else if (source is MeshRenderer)
                {
                    var filter = source.GetComponent<MeshFilter>();
                    if (filter != null) mesh = filter.sharedMesh;
                }
                if (mesh == null) continue;
                var part = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer));
                part.layer = 31;
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = source.transform.position - portrait.transform.position;
                part.transform.localRotation = source.transform.rotation;
                part.transform.localScale = source.transform.lossyScale;
                part.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!hasBounds) return;
            var cameraObject = new GameObject("Portrait camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.orthographic = true;
            camera.aspect = .8f;
            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.15f;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 20;
            camera.transform.position = bounds.center + Vector3.forward * (bounds.extents.z + 4);
            camera.transform.LookAt(bounds.center);
            var lightObject = new GameObject("Portrait light", typeof(Light));
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.position = bounds.center + new Vector3(-2, 2, 3);
            lightObject.transform.LookAt(bounds.center);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Spot;
            light.range = 12;
            light.spotAngle = 90;
            light.intensity = 3;
            light.cullingMask = 1 << 31;
            profileCharacterTexture = new RenderTexture(560, 700, 24, RenderTextureFormat.ARGB32);
            profileCharacterTexture.Create();
            camera.targetTexture = profileCharacterTexture;
            camera.Render();
            camera.targetTexture = null;
            profileCharacterImage.texture = profileCharacterTexture;
        }
        finally
        {
            portrait.SetActive(false);
            Destroy(portrait);
            root.SetActive(false);
            Destroy(root);
            foreach (var mesh in meshes) Destroy(mesh);
        }
    }

    private void BuildProfileUI()
    {
        if (profileCanvas != null) { BindProfileButtons(); return; }
        if (almanacCanvas == null) return;
        // Also hides the controls in previously baked Almanac hierarchies.
        if (playerInfoTabBtn != null) playerInfoTabBtn.gameObject.SetActive(false);
        if (achievementsTabBtn != null) achievementsTabBtn.gameObject.SetActive(false);
        profileCanvas = new GameObject("Player Profile", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        profileCanvas.transform.SetParent(almanacCanvas.transform.parent, false);
        var canvas = profileCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 65;
        var scaler = profileCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var backdrop = CreatePanel("Profile backdrop", profileCanvas.transform, Color.black);
        SetStretchRect(backdrop.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var stage = CreatePanel("Profile stage", backdrop.transform, Color.white);
        SetRect(stage.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(1920, 1080));
        ExportUIArt.Apply(stage.GetComponent<Image>(), "profileBackground");
        ProfileArtwork(stage.transform, "Player profile label", "profileLabel", new Vector2(-455, 420), new Vector2(485, 64));
        ProfileArtwork(stage.transform, "Director portrait", "profileIcon", new Vector2(-455, -65), new Vector2(290, 290));
        var hint = BookText(stage.transform, "Director caption", new Vector2(-455, -275), new Vector2(570, 80), 28);
        hint.text = "YOUR DIRECTING CAREER";
        ExportUIArt.OutlineText(hint);

        var card = CreatePanel("Career card", stage.transform, new Color32(252, 245, 220, 255));
        SetRect(card.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, new Vector2(475, 10), new Vector2(860, 720));
        var shadow = card.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(7, -8);
        shadow.effectColor = new Color(0, 0, 0, .4f);
        if (playerInfoPanel == null) BuildPlayerInfoPanel(card.transform);
        if (achievementsPanel == null) BuildAchievementsPanel(card.transform);
        foreach (var panel in new[] { playerInfoPanel, achievementsPanel })
        {
            panel.transform.SetParent(card.transform, false);
            SetStretchRect(panel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(24, 24), new Vector2(-24, -24));
            foreach (var text in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                text.color = new Color32(54, 46, 38, 255);
                text.fontSharedMaterial = text.font.material;
                text.enableAutoSizing = true;
                text.fontSizeMin = 20;
                text.fontSizeMax = text.fontSize;
            }
        }
        playerInfoPanel.transform.Find("Section Title").GetComponent<TextMeshProUGUI>().text = "PLAYER PROFILE";
        playerInfoTabBtn = ProfileTab(stage.transform, "Profile tab", "PROFILE", new Vector2(260, 425));
        achievementsTabBtn = ProfileTab(stage.transform, "Achievements tab", "ACHIEVEMENTS", new Vector2(660, 425));
        var back = BookButton(stage.transform, "Back to game", "", "profileBack", new Vector2(665, -425), new Vector2(235, 94));
        profileBackButton = back;
        var close = BookButton(stage.transform, "Close profile", "", "profileExit", new Vector2(910, 430), new Vector2(80, 80));
        profileCloseButton = close;
        BindProfileButtons();
        profileCanvas.SetActive(false);
    }

    private void BindProfileButtons()
    {
        foreach (var button in new[] { profileBackButton, profileCloseButton })
        {
            if (button == null || button.onClick.GetPersistentEventCount() > 0) continue;
            button.onClick.RemoveListener(ClosePlayerProfile);
            button.onClick.AddListener(ClosePlayerProfile);
        }
    }

    private void ProfileArtwork(Transform parent, string name, string key, Vector2 position, Vector2 size)
    {
        var panel = CreatePanel(name, parent, Color.white);
        SetRect(panel.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, position, size);
        ExportUIArt.Apply(panel.GetComponent<Image>(), key);
        panel.GetComponent<Image>().preserveAspect = true;
        panel.GetComponent<Image>().raycastTarget = false;
    }

    private Button ProfileTab(Transform parent, string name, string label, Vector2 position)
    {
        var button = CreateButton(name, parent, label);
        SetRect(button.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, position, new Vector2(365, 66));
        return button;
    }

    public void OpenPlayerProfile()
    {
        if (isProfileOpen) { ClosePlayerProfile(); return; }
        if (isAlmanacOpen || PauseManager.isPaused || bookSelectionAnimating) return;
        if (TutorialUIManager.Instance != null && TutorialUIManager.Instance.IsBossDialogueOpen()) return;
        if (ContractUIManager.Instance != null && (ContractUIManager.Instance.IsQualificationsOpen() || ContractUIManager.Instance.IsContractUIOpen())) return;
        var director = FindObjectOfType<DirectorTerminal>();
        if (director != null && director.IsTerminalActive()) return;
        var shop = FindObjectOfType<ShopTerminal>();
        if (shop != null && shop.IsTerminalActive()) return;
        var computer = FindObjectOfType<ComputerStation>();
        if (computer != null && computer.computerUICanvas != null && computer.computerUICanvas.activeInHierarchy) return;
        BuildProfileUI();
        if (profileCanvas == null) return;
        ApplyAccountProfileStyle();
        CaptureInputState();
        previousCursorLockState = CursorLockMode.Locked;
        previousCursorVisible = false;
        isProfileOpen = true;
        RefreshAllUI();
        OpenTab(0);
        profileCanvas.SetActive(true);
        RenderProfileCharacter();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ClosePlayerProfile()
    {
        if (!isProfileOpen) return;
        isProfileOpen = false;
        ReleaseProfilePreview();
        if (profileCanvas != null) profileCanvas.SetActive(false);
        RestoreInputState();
    }
}

