using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Animations;
using UnityEngine.Playables;

public partial class AlmanacManager
{
    [SerializeField] private GameObject profileCanvas;
    [SerializeField] private Button profileBackButton, profileCloseButton;
    private bool isProfileOpen;
    private bool profileClosing;
    private RawImage profileCharacterImage;
    private RenderTexture profileCharacterTexture;
    private ProfileCharacterPreview profileLivePreview;
    private static TMP_FontAsset profileLabelFont;
    private static Material profileLabelMaterial;
    private static TMP_FontAsset profileBodyFont;
    private static Material profileBodyMaterial;

    private void ApplyProfileTypography()
    {
        if (profileCanvas == null) return;
        if (profileLabelFont == null)
            profileLabelFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF") ?? TMP_Settings.defaultFontAsset;
        if (profileBodyFont == null)
            profileBodyFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF") ?? TMP_Settings.defaultFontAsset;
        if (profileLabelFont == null || profileBodyFont == null) return;
        if (profileLabelMaterial == null)
        {
            profileLabelMaterial = new Material(profileLabelFont.material) { name = "Profile button lettering" };
            profileLabelMaterial.EnableKeyword("OUTLINE_ON");
            profileLabelMaterial.SetColor("_FaceColor", Color.white);
            profileLabelMaterial.SetColor("_OutlineColor", Color.black);
            profileLabelMaterial.SetFloat("_OutlineWidth", .10f);
            profileLabelMaterial.SetFloat("_OutlineSoftness", 0);
        }
        if (profileBodyMaterial == null)
        {
            profileBodyMaterial = new Material(profileBodyFont.material) { name = "Profile clean paper text" };
            profileBodyMaterial.DisableKeyword("OUTLINE_ON");
            profileBodyMaterial.SetColor("_FaceColor", Color.white);
            profileBodyMaterial.SetFloat("_OutlineWidth", 0f);
            profileBodyMaterial.SetFloat("_OutlineSoftness", 0f);
        }
        var stage = profileCanvas.transform.Find("Profile backdrop/Profile stage") ?? profileCanvas.transform;
        foreach (var text in stage.GetComponentsInChildren<TMP_Text>(true))
        {
            if (profileShopPanel != null && text.transform.IsChildOf(profileShopPanel.transform))
            {
                StyleProfileShopTypography(text);
                continue;
            }
            var button = text.GetComponentInParent<Button>(true);
            bool onColoredButton = button != null && !button.name.StartsWith("Cosmetic category ", System.StringComparison.Ordinal);
            bool onWallet = text.GetComponentInParent<CCoinProfileWidget>(true) != null;
            bool onCrewHeader = text.transform.parent != null && text.transform.parent.name == "Crew file header";
            bool lightLettering = onColoredButton || onWallet || onCrewHeader;
            text.font = lightLettering ? profileLabelFont : profileBodyFont;
            text.fontSharedMaterial = lightLettering ? profileLabelMaterial : profileBodyMaterial;
            bool heading = text.name == "Title" || text.name == "Name" || text.name == "Value" ||
                text.name.IndexOf("heading", System.StringComparison.OrdinalIgnoreCase) >= 0;
            text.fontStyle = lightLettering || heading || button != null ? FontStyles.Bold : FontStyles.Normal;
            if (lightLettering) text.color = Color.white;
            else if (text.color != ProfileGreen && text.color != ProfileMuted) text.color = ProfileInk;
            text.characterSpacing = 0f;
            text.UpdateMeshPadding();
        }
        foreach (var input in stage.GetComponentsInChildren<TMP_InputField>(true))
        {
            input.customCaretColor = true;
            input.caretColor = ProfileInk;
            input.selectionColor = new Color32(164, 122, 77, 95);
        }
    }
    private TMP_Text profileAccountId;
    private Image profileCardImage;
    private readonly System.Collections.Generic.List<string> profileTryOnParts = new System.Collections.Generic.List<string>();

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
        if (profileAccountId == null)
            profileAccountId = BookText(playerInfoPanel.transform, "Account ID", new Vector2(95, 137), new Vector2(560, 45), 24);
        profileAccountId.richText = false;
        profileAccountId.color = new Color32(54, 46, 38, 255);
        string accountId = PlayerPrefs.GetString("PlayFabId", "").Trim();
        profileAccountId.text = string.IsNullOrEmpty(accountId) ? "GUEST" : accountId;
        profileAccountId.enableAutoSizing = true;
        profileAccountId.fontSizeMin = 16;
        profileAccountId.fontSizeMax = 24;
        var rows = new[] { playerMoneyText, totalJobsText, currentLevelText, activeContractText };
        for (int i = 0; i < rows.Length; i++)
            if (rows[i] != null)
                rows[i].gameObject.SetActive(false); // Career values now live in Stats; this page uses the authored Bio area.
        if (profileCharacterImage == null)
        {
            var preview = new GameObject("Character preview", typeof(RectTransform), typeof(RawImage));
            preview.transform.SetParent(stage, false);
            profileCharacterImage = preview.GetComponent<RawImage>();
            profileCharacterImage.raycastTarget = false;
            SetRect(preview.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, new Vector2(-455, -90), new Vector2(560, 700));
        }
        CCoinProfileWidget.Attach(stage,profileCharacterImage,profileCanvas.transform);
        var wallet = stage.Find("Account cosmetic wallet/Open cosmetic shop");
        if (wallet != null) wallet.gameObject.SetActive(false); // Shop is now the shared third sidebar tab.
        EnsureSharedProfileUI();
    }

    private void ReleaseProfilePreview()
    {
        if (profileLivePreview != null) profileLivePreview.Release();
        if (profileCharacterImage != null) profileCharacterImage.texture = null;
        if (profileCharacterTexture == null) return;
        profileCharacterTexture.Release();
        Destroy(profileCharacterTexture);
        profileCharacterTexture = null;
    }

    private void RenderProfileCharacter()
    {
        profileTryOnParts.Clear();
        RenderDressedProfileCharacter(CCoinService.Ensure().EquippedParts);
    }

    private void RenderDressedProfileCharacter(string[] parts)
    {
        ReleaseProfilePreview();
        var catalog = Resources.Load<ProductModelCatalog>("ProductModels");
        GameObject portraitModel = null;
        if (catalog != null && catalog.coffeeActorModels != null)
            foreach (var model in catalog.coffeeActorModels)
                if (model != null && model.name == "DefaultCharacGirlRig") { portraitModel = model; break; }
        if (profileCharacterImage == null || portraitModel == null) return;
        var portrait = Instantiate(portraitModel);
        // Dress the actual character at rest, then pose its original skeleton.
        CharacterCosmeticRig.Load()?.Apply(portrait,parts);
        portrait.transform.SetPositionAndRotation(new Vector3(20000, 20000, 20000), Quaternion.identity);
        foreach (var animator in portrait.GetComponentsInChildren<Animator>())
        {
            // Use the same bundled Humanoid idle as the stage actors, then freeze a mesh snapshot.
            if (animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman)
            {
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                foreach (var clip in Resources.LoadAll<AnimationClip>("Character/Animations/Stand--Idle.anim"))
                {
                    if (clip.name.StartsWith("__preview__") || !clip.humanMotion) continue;
                    var graph = PlayableGraph.Create("Profile idle snapshot");
                    try
                    {
                        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                        var idle = AnimationClipPlayable.Create(graph, clip);
                        AnimationPlayableOutput.Create(graph, "Portrait", animator).SetSourcePlayable(idle);
                        graph.Play(); graph.Evaluate(0f);
                        animator.enabled = false;
                    }
                    finally { graph.Destroy(); }
                    break;
                }
            }
            animator.enabled = false;
        }
        // The catalog entry is the imported character FBX, not the player/gameplay prefab.
        // Keep the isolated rig alive while the profile is open, for idle and greeting motion.
        var root = new GameObject("Profile portrait snapshot");
        root.transform.position = new Vector3(10000, 10000, 10000);
        portrait.transform.SetParent(root.transform, true);
        foreach (var script in portrait.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
        foreach (var collider in portrait.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var otherCamera in portrait.GetComponentsInChildren<Camera>(true)) otherCamera.enabled = false;
        foreach (var otherLight in portrait.GetComponentsInChildren<Light>(true)) otherLight.enabled = false;
        bool retained = false;
        try
        {
            Bounds bounds = new Bounds();
            bool hasBounds = false;
            foreach (var source in portrait.GetComponentsInChildren<Renderer>())
            {
                if (!source.enabled) continue;
                source.gameObject.layer = 31;
                source.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                source.receiveShadows = false;
                if (source is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
                if (!hasBounds) { bounds = source.bounds; hasBounds = true; }
                else bounds.Encapsulate(source.bounds);
            }
            if (!hasBounds) return;
            // Imported rigs can carry a 100x scale. Frame the copied meshes at portrait size
            // instead of assuming their original depth fits inside a 20-metre camera range.
            float portraitScale = 1.85f / Mathf.Max(.01f, bounds.size.y);
            root.transform.localScale = Vector3.one * portraitScale;
            hasBounds = false;
            foreach (var renderer in portrait.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
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
            // Directional illumination is independent of imported root scale and light range.
            light.type = LightType.Directional;
            light.range = 12;
            light.spotAngle = 90;
            light.intensity = 1.3f;
            light.cullingMask = 1 << 31;
            profileCharacterTexture = new RenderTexture(560, 700, 24, RenderTextureFormat.ARGB32);
            profileCharacterTexture.Create();
            camera.targetTexture = profileCharacterTexture;
            camera.Render();
            camera.targetTexture = null;
            profileCharacterImage.texture = profileCharacterTexture;
            profileLivePreview = profileCharacterImage.GetComponent<ProfileCharacterPreview>();
            if (profileLivePreview == null) profileLivePreview = profileCharacterImage.gameObject.AddComponent<ProfileCharacterPreview>();
            profileLivePreview.Initialize(root, portrait, camera, light, profileCharacterTexture);
            retained = true;
        }
        finally
        {
            if (!retained)
            {
                root.SetActive(false);
                Destroy(root);
            }
        }
    }

    private void BuildProfileUI()
    {
        if (profileCanvas != null) { BindProfileButtons(); EnsureProfileProgressUI(); return; }
        if (almanacCanvas == null && !profileMenuOnly) return;
        // Also hides the controls in previously baked Almanac hierarchies.
        if (playerInfoTabBtn != null) playerInfoTabBtn.gameObject.SetActive(false);
        if (achievementsTabBtn != null) achievementsTabBtn.gameObject.SetActive(false);
        profileCanvas = new GameObject("Player Profile", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        if (almanacCanvas != null) profileCanvas.transform.SetParent(almanacCanvas.transform.parent, false);
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
        EnsureProfileProgressUI();
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
        profileMenuOnly=false;profileMenuClosed=null;profileAccountStats=null;profileAccountSyncRequested=false;
        BuildProfileUI();
        if (profileCanvas == null) return;
        ApplyAccountProfileStyle();
        profileInputsDirty = false; RefreshAccountFields(true);
        CaptureInputState();
        previousCursorLockState = CursorLockMode.Locked;
        previousCursorVisible = false;
        isProfileOpen = true;
        RefreshAllUI();
        OpenTab(0);
        profileCanvas.SetActive(true);
        profileInputsDirty = false; RefreshAccountFields(true);
        CCoinService.Ensure().Refresh();
        AccountProfileData.Refresh();
        RenderProfileCharacter();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ClosePlayerProfile()
    {
        if (!isProfileOpen || profileClosing) return;
        profileClosing = true;
        UITransition.Hide(profileCanvas, FinishClosePlayerProfile);
    }

    private void FinishClosePlayerProfile()
    {
        profileClosing = false;
        isProfileOpen = false;
        ReleaseProfilePreview();
        if (profileCanvas != null)
        {
            var cosmeticShop = profileCanvas.transform.Find("C-Coins cosmetic shop");
            if (cosmeticShop != null) cosmeticShop.gameObject.SetActive(false);
            var coinShop = profileCanvas.transform.Find("C-Coins pack shop");
            if (coinShop != null) coinShop.gameObject.SetActive(false);
        }
        if (profileCanvas != null) profileCanvas.SetActive(false);
        RestoreInputState();
        var callback = profileMenuClosed; profileMenuClosed = null;
        callback?.Invoke();
    }
}

