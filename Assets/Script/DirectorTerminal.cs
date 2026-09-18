using UnityEngine;
using UnityEngine.UI;
using Player.PlayerController;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[System.Serializable]
public class LevelPropBank
{
    public string levelName = "New Level";
    public int progressLevel;
    public List<GameObject> allowedProps = new List<GameObject>();
}

public class DirectorTerminal : MonoBehaviour
{
    [Header("Cameras & UI")]
    public Camera topDownCamera;
    public RectTransform viewportUI;
    public GameObject tabletUI;
    public TextMeshProUGUI selectionIndicatorText;

    [Header("Color Panel Syncing")]
    public Slider rSlider;
    public Slider gSlider;
    public Slider bSlider;

    [Header("Live UI Readouts")]
    public TextMeshProUGUI rValueText;
    public TextMeshProUGUI gValueText;
    public TextMeshProUGUI bValueText;
    public TextMeshProUGUI bCoinsText;

    [Header("Dynamic UI Spawning")]
    public Transform propUIContainer;
    public GameObject uiPropCardPrefab;

    [Header("Dynamic Prop Bin Database")]
    public List<LevelPropBank> propDatabase = new List<LevelPropBank>();

    [Header("Level 3 Vehicle & Level 4 Cast")]
    public int actorHireCost = ProductionEconomy.ActorBase;
    public int carSpawnCost = ProductionEconomy.Vehicle;

    [Header("Drag Settings")]
    public LayerMask moveableLayer;
    public float dragHeight = 0.1f;

    [Header("Stage Setup Integration (NEW)")]
    public GameObject wallPrefab;
    public Transform spawnPoint;
    public GameObject spawnWallButton;
    public GameObject colorControlPanel;
    private GameObject currentWall;
    public Color currentWallColor = Color.white;

    private GameObject playerCameraObj;
    private PlayerController playerController;
    private GameObject mainPlayerUI;
    private bool playerControllerWasEnabled = true;
    private bool playerCouldMove = true;
    private bool playerCouldLook = true;
    private bool hasPlayerStateSnapshot;

    private GameObject draggedObject;
    private GameObject selectedObject;
    private Collider[] draggedColliders;
    private readonly List<Collider> temporarilyDisabledColliders = new List<Collider>();
    private Renderer[] draggedRenderers;
    private Renderer[] selectedRenderers;
    private bool isTerminalActive = false;
    private bool justGrabbed = false;
    private bool showPropCostWarningOnDrop = false;
    private bool hasShownPropCostWarning = false;
    [SerializeField] private Button poseActorButton;
    private DirectorColorFields colorFields;
    [SerializeField] private GameObject interiorPicker;
    private GameObject interiorPreview;
    private int previewStyle = -1;
    [SerializeField] private TMP_Text[] interiorLabels = new TMP_Text[5];
    private static bool OwnsInterior(int style) => GameSavePrefs.GetInt("OwnedInterior." + style) == 1;

    private void RefreshInteriorLabels()
    {
        for (int i = 0; i < 3; i++)
            if (interiorLabels[i] != null) interiorLabels[i].text = StageInterior.Title(i) + "\n" + (OwnsInterior(i) ? "OWNED - PREVIEW" : StageInterior.Cost(i) + " B - PREVIEW");
        if (interiorLabels[3] != null) interiorLabels[3].text = previewStyle < 0 ? "SELECT A SET" : OwnsInterior(previewStyle) ? "USE SET" : "BUY SET";
    }

    private void BuyOrUseInterior()
    {
        if (previewStyle < 0) return;
        if (OwnsInterior(previewStyle)) { SelectInterior(previewStyle); return; }
        if (CareerManager.Instance == null || !CareerManager.Instance.TrySpendMoney(StageInterior.Cost(previewStyle))) return;
        GameSavePrefs.SetInt("OwnedInterior." + previewStyle, 1);
        GameSavePrefs.Save();
        GameSaveManager.Instance?.SaveCheckpoint();
        RefreshInteriorLabels();
        GameFeedback.Show("SET PURCHASED - " + StageInterior.Title(previewStyle) + "\nChoose USE SET to place it. Owned sets can be reused for free.");
    }

    private void CancelInteriorPreview()
    {
        if(interiorPreview!=null){interiorPreview.SetActive(false);Destroy(interiorPreview);}
        interiorPreview=null;previewStyle=-1;
        if (currentWall != null) currentWall.SetActive(true);
    }
    private void PreviewInterior(int style)
    {
        CancelInteriorPreview();
        if(wallPrefab==null||spawnPoint==null)return;
        GameObject placedWall = currentWall;
        if (placedWall != null) placedWall.SetActive(false);
        interiorPreview=Instantiate(wallPrefab,spawnPoint.position,spawnPoint.rotation);
        interiorPreview.name="Unpurchased set preview";
        // Use the exact purchased-set alignment and paint without registering a purchase.
        currentWall = interiorPreview;
        try
        {
            RaiseBackdropFloorAboveStage();
            ApplyColorToWall(style == 0 ? Color.white : new Color(128f/255,80f/255,46f/255));
        }
        finally { currentWall = placedWall; }
        StageInterior.Furnish(interiorPreview,style);
        foreach(var collider in interiorPreview.GetComponentsInChildren<Collider>())collider.enabled=false;
        previewStyle=style;
        RefreshInteriorLabels();
        GameFeedback.Show("PREVIEW - " + StageInterior.Title(style) + (OwnsInterior(style) ? "\nChoose USE SET. No additional charge." : "\nChoose BUY SET to purchase for " + StageInterior.Cost(style) + " B-Coins."));
    }

    private int displayedRValue = int.MinValue;
    private int displayedGValue = int.MinValue;
    private int displayedBValue = int.MinValue;
    private int displayedMoney = int.MinValue;
    private bool pendingTutorialColorCheck;
    private bool pendingColorIsWall;
    private Color pendingColor;

    private LineRenderer selectionOutline;
    private Material selectionOutlineMaterial;

    public bool HasWall() { return currentWall != null; }
    public GameObject GetCurrentWall() { return currentWall; }

    public GameObject CreatePracticeWall(Color wallColor)
    {
        if (wallPrefab == null || spawnPoint == null) return null;

        if (currentWall != null) Destroy(currentWall);

        currentWall = Instantiate(wallPrefab, spawnPoint.position, spawnPoint.rotation);
        RaiseBackdropFloorAboveStage();
        currentWall.name = "Goke Practice Wall";
        currentWallColor = wallColor;
        ApplyColorToWall(currentWallColor);

        if (spawnWallButton != null) spawnWallButton.SetActive(false);
        if (colorControlPanel != null) colorControlPanel.SetActive(true);
        SyncSlidersToColor(currentWallColor);
        return currentWall;
    }

    public void RemovePracticeWall(GameObject practiceWall)
    {
        if (practiceWall == null || currentWall != practiceWall) return;

        if (selectedObject == currentWall)
        {
            selectedObject = null;
            selectedRenderers = null;
        }

        Destroy(currentWall);
        currentWall = null;
        currentWallColor = Color.white;

        if (spawnWallButton != null) spawnWallButton.SetActive(true);
        if (colorControlPanel != null) colorControlPanel.SetActive(true);

        SyncSlidersToColor(Color.white);
    }

    private void Start()
    {
        if (tabletUI != null) tabletUI.SetActive(false);
        if (selectionIndicatorText != null)
        {
            selectionIndicatorText.text = "Selected: None";
            selectionIndicatorText.gameObject.SetActive(false);
        }

        if (spawnWallButton != null) spawnWallButton.SetActive(true);
        if (colorControlPanel != null) colorControlPanel.SetActive(true);

        CreatePoseActorButton();
        colorFields = GetComponent<DirectorColorFields>();
        if(colorFields==null) colorFields = gameObject.AddComponent<DirectorColorFields>();
        colorFields.Initialize(this);
        UpdateStageButtonLabel();

        Canvas[] allCanvases = FindObjectsOfType<Canvas>(true);
        foreach (Canvas canvas in allCanvases)
        {
            Transform[] allChildren = canvas.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child.name == "Player UI" || child.name == "PlayerUI" || child.name == "Main UI")
                    mainPlayerUI = child.gameObject;
            }
        }
    }

    private void Update()
    {
        if (!isTerminalActive) return;
        if (PauseManager.isPaused) return;
        if (interiorPicker != null && interiorPicker.activeSelf) return;

        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;

        UpdateColorSliderAvailability();
        UpdateUIText();

        if (colorFields != null && colorFields.IsEditing) return;

        ActorBot actorBot = selectedObject != null ? selectedObject.GetComponent<ActorBot>() : null;
        if (actorBot != null)
        {
            if(keyboard!=null&&keyboard.bKey.wasPressedThisFrame){actorBot.SetStartMark();GameFeedback.Show("START MARK saved. Move the actor to the end position, then press N.");}
            if(keyboard!=null&&keyboard.nKey.wasPressedThisFrame){actorBot.SetEndMark();}
            if(keyboard!=null&&keyboard.kKey.wasPressedThisFrame)actorBot.RehearseWalk();
            if(keyboard!=null&&keyboard.jKey.wasPressedThisFrame)actorBot.ReturnToStartMark();
            if(keyboard!=null&&keyboard.hKey.wasPressedThisFrame)actorBot.ClearWalk();
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) actorBot.transform.Rotate(0, 15, 0, Space.World);
            if (selectionIndicatorText != null)
                selectionIndicatorText.text = ActorBot.TierName(actorBot.SkillTier) + " ACTOR | " +
                    actorBot.GetComponent<CubeActor>().GetPoseName() + " | R: turn\nB: start | N: end | K: walk | J: reset | H: clear walk";
        }

        AutomotiveGrip grip = selectedObject != null ? selectedObject.GetComponent<AutomotiveGrip>() : null;
        if (grip != null && keyboard != null)
        {
            if (keyboard.qKey.wasPressedThisFrame) grip.transform.Rotate(0,-15,0,Space.World);
            if (keyboard.eKey.wasPressedThisFrame) grip.transform.Rotate(0,15,0,Space.World);
            if (keyboard.fKey.wasPressedThisFrame) grip.CyclePower();
            if (selectionIndicatorText != null) selectionIndicatorText.text=grip.Controls;
        }

        StageLightStrip strip = selectedObject != null ? selectedObject.GetComponent<StageLightStrip>() : null;
        if (strip != null && keyboard != null)
        {
            if (keyboard.rKey.wasPressedThisFrame) strip.CycleTilt();
            if (keyboard.qKey.wasPressedThisFrame) strip.transform.Rotate(0, -15, 0, Space.World);
            if (keyboard.eKey.wasPressedThisFrame) strip.transform.Rotate(0, 15, 0, Space.World);
            if (keyboard.fKey.wasPressedThisFrame) strip.CyclePower();
            if (selectionIndicatorText != null)
                selectionIndicatorText.text = "LIGHT STRIP  |  R: tilt  Q/E: turn  F: " + strip.PowerLabel;
        }

        if (draggedObject != null)
        {
            if (mouse == null)
            {
                UpdateSelectionOutline();
                return;
            }

            MoveObjectWithMouse();
            UpdateSelectionOutline();

            if (!justGrabbed && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                if (IsMouseOverViewport()) DropDraggedProp();
            }
            justGrabbed = false;
            return;
        }

        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            if (IsMouseOverViewport()) TrySelect3DObject();
        }
        else if (mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            if (IsMouseOverViewport()) TryDelete3DObject();
        }

        bool isWall = IsWallObject(selectedObject);

        if (keyboard != null && keyboard.tKey.wasPressedThisFrame && selectedObject != null && !isWall)
        {
            draggedObject = selectedObject;
            draggedColliders = draggedObject.GetComponentsInChildren<Collider>();
            draggedRenderers = selectedRenderers;
            justGrabbed = true;
        }

        UpdateSelectionOutline();
    }

    private void LateUpdate()
    {
        // The tablet owns the mouse while it is open. Tutorial dialogue and other
        // systems also manage the cursor, so enforce this after their Update calls.
        if (!isTerminalActive) return;

        CheckPendingTutorialColor();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void QueueTutorialColorCheck(Color color, bool isWall)
    {
        pendingColor = color;
        pendingColorIsWall = isWall;
        pendingTutorialColorCheck = true;
    }

    private void CheckPendingTutorialColor()
    {
        if (!pendingTutorialColorCheck || PauseManager.isPaused) return;

        // Slider callbacks run throughout a drag. Crossing the target value is
        // not a finished edit: the player must release on the requested color.
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed) return;

        pendingTutorialColorCheck = false;
        TutorialManager tutorial = TutorialManager.Instance;
        if (tutorial == null) return;

        if (pendingColorIsWall)
            tutorial.CheckWallColor(pendingColor.r * 255f, pendingColor.g * 255f, pendingColor.b * 255f);
        else
            tutorial.CheckCubeColor(pendingColor.r * 255f, pendingColor.g * 255f, pendingColor.b * 255f);
    }

    private Color NormalizeColor(float r, float g, float b)
    {
        return new Color(NormalizeChannel(r, rSlider), NormalizeChannel(g, gSlider), NormalizeChannel(b, bSlider), 1f);
    }

    private Color SnapTutorialPaintColor(Color color, bool isWall)
    {
        TutorialManager tutorial = TutorialManager.Instance;
        if (tutorial == null) return color;

        Color snappedColor = tutorial.SnapTutorialPaintColor(color, isWall);
        if (snappedColor.b != color.b && bSlider != null)
        {
            // Keep the handle, readout, and material on the same exact value.
            // Silent updates avoid recursively firing the slider's paint callback.
            bSlider.SetValueWithoutNotify(bSlider.maxValue > 1f ? snappedColor.b * 255f : snappedColor.b);
            UpdateUIText();
        }

        return snappedColor;
    }

    private static float NormalizeChannel(float value, Slider slider)
    {
        // Use the configured range, not the current value: 1 in a 0-255
        // slider means 1/255, while 1 in a 0-1 slider means full intensity.
        bool usesByteRange = slider != null ? slider.maxValue > 1f : value > 1f;
        return Mathf.Clamp01(usesByteRange ? value / 255f : value);
    }

    public void SpawnWall()
    {
        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanUseTabletFeature("AddWall")) return;
        if (CampaignProgression.GetCurrentLevel() >= 4 && tabletUI != null && spawnWallButton != null)
        {
            ShowInteriorPicker();
            return;
        }
        SelectInterior(0);
    }

    public void SelectInterior(int style)
    {
        if (style < 0 || style > 2 || (style != 0 && CampaignProgression.GetCurrentLevel() < 4)) return;
        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanUseTabletFeature("AddWall")) return;

        bool reusable = CampaignProgression.GetCurrentLevel() >= 4;
        if (reusable && !OwnsInterior(style)) return;
        if ((currentWall == null || reusable) && wallPrefab != null && spawnPoint != null)
        {
            if (!reusable && CareerManager.Instance != null && !CareerManager.Instance.TrySpendMoney(StageInterior.Cost(style)))
            {
                return;
            }

            CancelInteriorPreview();
            if (currentWall != null)
            {
                if (selectedObject == currentWall) selectedObject = null;
                currentWall.SetActive(false);
                Destroy(currentWall);
            }
            currentWall = Instantiate(wallPrefab, spawnPoint.position, spawnPoint.rotation);
            RaiseBackdropFloorAboveStage();
            if (spawnWallButton != null) spawnWallButton.SetActive(reusable);

            currentWallColor = style == 0 ? Color.white : new Color(128f/255,80f/255,46f/255);
            ApplyColorToWall(currentWallColor);
            StageInterior.Furnish(currentWall, style);

            SyncSlidersToColor(currentWallColor);

            if (TutorialManager.Instance != null) TutorialManager.Instance.OnWallAdded();
            if (interiorPicker != null) interiorPicker.SetActive(false);
        }
    }

    private bool interiorButtonsBound;
#if UNITY_EDITOR
    public void BakeHierarchyUI()
    {
        if (tabletUI != null && spawnWallButton != null) { ShowInteriorPicker(); interiorPicker.SetActive(false); }
        CreatePoseActorButton();
        var fields=GetComponent<DirectorColorFields>();
        if (fields == null) fields=gameObject.AddComponent<DirectorColorFields>();
        fields.Initialize(this);
    }
#endif

    private void ShowInteriorPicker()
    {
        if (interiorPicker == null)
        {
            interiorPicker = new GameObject("Choose Stage Interior", typeof(RectTransform), typeof(Image));
            var rect = interiorPicker.GetComponent<RectTransform>();
            rect.SetParent(tabletUI.transform, false);
            rect.anchorMin = new Vector2(.72f,0); rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            interiorPicker.GetComponent<Image>().color = new Color(.95f,.92f,.84f,1f);
            for (int i=0;i<5;i++)
            {
                int choice = i;
                var option = Instantiate(spawnWallButton, rect);
                option.name = i == 4 ? "Cancel Interior" : i == 3 ? "Buy Interior" : StageInterior.Title(i);
                option.SetActive(true);
                var optionRect = option.GetComponent<RectTransform>();
                optionRect.anchorMin = new Vector2(.04f, .78f-i*.16f);
                optionRect.anchorMax = new Vector2(.96f, .91f-i*.16f);
                optionRect.offsetMin = Vector2.zero; optionRect.offsetMax = Vector2.zero;
                var label = option.GetComponentInChildren<TMP_Text>(true);
                interiorLabels[i] = label;
                if (label != null)
                {
                    label.text = i == 4 ? "CANCEL" : i==3 ? "BUY SET" : "PREVIEW " + StageInterior.Title(i) + " - " + StageInterior.Cost(i) + " B";
                    label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = 30;
                }
                var button = option.GetComponent<Button>();
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => {
                    if(choice==4){CancelInteriorPreview();interiorPicker.SetActive(false);}
                    else if(choice==3) BuyOrUseInterior();
                    else PreviewInterior(choice);
                });
            }
        }
        if (!interiorButtonsBound)
        {
            interiorButtonsBound = true;
            for (int i=0;i<interiorLabels.Length;i++)
            {
                int choice=i;
                var button=interiorLabels[i].GetComponentInParent<Button>(true);
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => {
                    if(choice==4){CancelInteriorPreview();interiorPicker.SetActive(false);}
                    else if(choice==3) BuyOrUseInterior(); else PreviewInterior(choice);
                });
            }
        }
        interiorPicker.SetActive(true);
        RefreshInteriorLabels();
        interiorPicker.transform.SetAsLastSibling();
    }

    public void SetCustomColor(float r, float g, float b)
    {
        if (!CanUseColorSliders()) return;

        currentWallColor = SnapTutorialPaintColor(NormalizeColor(r, g, b), true);
        if (currentWall != null)
        {
            ApplyColorToWall(currentWallColor);
            QueueTutorialColorCheck(currentWallColor, true);
        }
    }

    private void ApplyColorToWall(Color newColor)
    {
        if (currentWall != null)
        {
            MeshRenderer[] renderers = currentWall.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers)
            {
                // Paint the backdrop and its floor together; leave the separate hardware alone.
                bool isBackdrop = renderer.name.StartsWith("Screen", System.StringComparison.OrdinalIgnoreCase) ||
                    renderer.name.Equals("wall", System.StringComparison.OrdinalIgnoreCase) ||
                    renderer.name.StartsWith("Backdrop", System.StringComparison.OrdinalIgnoreCase);
                if (isBackdrop)
                    foreach (Material material in renderer.materials)
                        if (material != null && material.HasProperty("_Color")) material.color = newColor;
            }
        }
    }

    private void RaiseBackdropFloorAboveStage()
    {
        // The exported floor sits below the old wall anchor. Align in world space:
        // the studio's Stage hierarchy is rotated and scaled non-uniformly.
        if (currentWall == null || spawnPoint == null) return;
        Renderer platform = spawnPoint.GetComponentInParent<Renderer>();
        if (platform == null)
        {
            GameObject stage = GameObject.Find("Stage");
            if (stage != null)
                foreach (Renderer candidate in stage.GetComponentsInChildren<Renderer>())
                    if (candidate.name.Equals("stage", System.StringComparison.OrdinalIgnoreCase)) { platform = candidate; break; }
        }
        if (platform == null) return;
        foreach (Renderer screen in currentWall.GetComponentsInChildren<Renderer>())
        {
            if (!screen.name.StartsWith("Screen", System.StringComparison.OrdinalIgnoreCase)) continue;
            float lift = platform.bounds.max.y + 0.025f - screen.bounds.min.y;
            if (Mathf.Abs(lift) > 0.001f) currentWall.transform.position += Vector3.up * lift;
            break;
        }
        GroundBackdropStands();
    }

    private void GroundBackdropStands()
    {
        Physics.SyncTransforms();
        foreach (Renderer stand in currentWall.GetComponentsInChildren<Renderer>())
        {
            if (!stand.name.StartsWith("tripod", System.StringComparison.OrdinalIgnoreCase)) continue;
            Bounds bounds = stand.bounds;
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(bounds.center.x, bounds.max.y + .1f, bounds.center.z),
                Vector3.down, 100f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(currentWall.transform) || hit.rigidbody != null ||
                    hit.collider.gameObject.layer == LayerMask.NameToLayer("Props") || hit.normal.y < .5f) continue;
                float height = bounds.size.y;
                if (height < .01f || hit.point.y >= bounds.max.y) break;
                // Extend the stand down to its own supporting surface while keeping
                // its top attached to the raised backdrop rail.
                Transform pivot = new GameObject("Stand ground alignment").transform;
                pivot.SetParent(currentWall.transform, false);
                pivot.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                pivot.rotation = Quaternion.identity;
                stand.transform.SetParent(pivot, true);
                pivot.localScale = new Vector3(1, (bounds.max.y - hit.point.y) / height, 1);
                break;
            }
        }
    }

    public void ClearStage()
    {
        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanUseTabletFeature("ClearStage")) return;

        if (currentWall != null)
        {
            currentWall.SetActive(false);
            if (selectedObject == currentWall)
            {
                selectedObject = null;
                selectedRenderers = null;
            }

            Destroy(currentWall);
            currentWall = null;
        }

        currentWallColor = Color.white;

        SyncSlidersToColor(Color.white);

        if (spawnWallButton != null) spawnWallButton.SetActive(true);
        if (colorControlPanel != null) colorControlPanel.SetActive(true);
        ClearAllProps();
    }

    private void UpdateUIText()
    {
        int rValue = rSlider != null ? Mathf.RoundToInt(rSlider.maxValue > 1f ? rSlider.value : rSlider.value * 255f) : 0;
        int gValue = gSlider != null ? Mathf.RoundToInt(gSlider.maxValue > 1f ? gSlider.value : gSlider.value * 255f) : 0;
        int bValue = bSlider != null ? Mathf.RoundToInt(bSlider.maxValue > 1f ? bSlider.value : bSlider.value * 255f) : 0;

        if (rValueText != null && displayedRValue != rValue)
        {
            displayedRValue = rValue;
            rValueText.text = rValue.ToString();
        }

        if (gValueText != null && displayedGValue != gValue)
        {
            displayedGValue = gValue;
            gValueText.text = gValue.ToString();
        }

        if (bValueText != null && displayedBValue != bValue)
        {
            displayedBValue = bValue;
            bValueText.text = bValue.ToString();
        }

        if (bCoinsText != null && CareerManager.Instance != null && displayedMoney != CareerManager.Instance.playerMoney)
        {
            displayedMoney = CareerManager.Instance.playerMoney;
            bCoinsText.text = displayedMoney + " B-Coins";
        }
    }

    public bool CanUseColorSliders()
    {
        ImportedProductVisual imported = selectedObject != null ? selectedObject.GetComponentInChildren<ImportedProductVisual>() : null;
        if (imported != null && !imported.CanRecolor) return false;
        return TutorialManager.Instance == null ||
               TutorialManager.Instance.CanUseTabletFeature("ColorSliders");
    }

    private void UpdateColorSliderAvailability()
    {
        bool canUseColorSliders = CanUseColorSliders();

        if (rSlider != null) rSlider.interactable = canUseColorSliders;
        if (gSlider != null) gSlider.interactable = canUseColorSliders;
        if (bSlider != null) bSlider.interactable = canUseColorSliders;
        if (colorFields != null) colorFields.Refresh(canUseColorSliders);
    }

    private void SyncSlidersToColor(Color color)
    {
        // Selecting/spawning an object updates the readout without repainting
        // another object or triggering tutorial checks for intermediate RGB values.
        pendingTutorialColorCheck = false;
        if (rSlider != null) rSlider.SetValueWithoutNotify(rSlider.maxValue > 1f ? color.r * 255f : color.r);
        if (gSlider != null) gSlider.SetValueWithoutNotify(gSlider.maxValue > 1f ? color.g * 255f : color.g);
        if (bSlider != null) bSlider.SetValueWithoutNotify(bSlider.maxValue > 1f ? color.b * 255f : color.b);

        UpdateUIText();
    }

    private void UpdateSelectionOutline()
    {
        if (selectedObject != null)
        {
            if (selectionOutline == null)
            {
                GameObject outlineObj = new GameObject("SelectionOutline");
                selectionOutline = outlineObj.AddComponent<LineRenderer>();
                selectionOutline.startWidth = 0.05f; selectionOutline.endWidth = 0.05f;
                selectionOutline.positionCount = 5; selectionOutline.useWorldSpace = true;
                selectionOutline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                selectionOutlineMaterial = new Material(Shader.Find("Sprites/Default"));
                selectionOutline.material = selectionOutlineMaterial;
                selectionOutline.startColor = Color.green; selectionOutline.endColor = Color.green;
            }

            bool isBlinkOn = (Time.time % 0.6f) > 0.3f;
            selectionOutline.enabled = isBlinkOn;

            if (isBlinkOn)
            {
                Bounds bounds = new Bounds(selectedObject.transform.position, Vector3.zero);
                if (selectedRenderers != null && selectedRenderers.Length > 0)
                {
                    bool hasBounds = false;
                    foreach (Renderer r in selectedRenderers)
                    {
                        if (r == null) continue;
                        if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                        else bounds.Encapsulate(r.bounds);
                    }
                }

                float pad = 0.1f;
                float minX = bounds.min.x - pad; float maxX = bounds.max.x + pad;
                float minZ = bounds.min.z - pad; float maxZ = bounds.max.z + pad;
                float yHeight = dragHeight + 0.02f;

                selectionOutline.SetPosition(0, new Vector3(minX, yHeight, minZ));
                selectionOutline.SetPosition(1, new Vector3(minX, yHeight, maxZ));
                selectionOutline.SetPosition(2, new Vector3(maxX, yHeight, maxZ));
                selectionOutline.SetPosition(3, new Vector3(maxX, yHeight, minZ));
                selectionOutline.SetPosition(4, new Vector3(minX, yHeight, minZ));
            }
        }
        else
        {
            if (selectionOutline != null) selectionOutline.enabled = false;
        }
    }

    private bool IsMouseOverViewport()
    {
        if (viewportUI == null) return true;

        Mouse mouse = Mouse.current;
        if (mouse == null) return false;

        return RectTransformUtility.RectangleContainsScreenPoint(viewportUI, mouse.position.ReadValue(), null);
    }

    private Ray GetMouseRay()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return new Ray();

        Vector2 mousePosition = mouse.position.ReadValue();

        if (viewportUI != null && topDownCamera != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewportUI, mousePosition, null, out Vector2 localPoint);
            float normalizedX = (localPoint.x - viewportUI.rect.x) / viewportUI.rect.width;
            float normalizedY = (localPoint.y - viewportUI.rect.y) / viewportUI.rect.height;
            return topDownCamera.ViewportPointToRay(new Vector3(normalizedX, normalizedY, 0));
        }
        return topDownCamera != null ? topDownCamera.ScreenPointToRay(mousePosition) : new Ray();
    }

    private static Rigidbody FindPropRoot(Collider collider)
    {
        // Imported prefabs can contain their own Rigidbody inside the tablet wrapper.
        Rigidbody root = null;
        int propsLayer = LayerMask.NameToLayer("Props");
        for (Transform t = collider != null ? collider.transform : null; t != null; t = t.parent)
        {
            if (t.gameObject.layer != propsLayer) break;
            if (t.TryGetComponent(out Rigidbody body)) root = body;
        }
        return root;
    }

    private void TrySelect3DObject()
    {
        Ray ray = GetMouseRay();
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Rigidbody rb = FindPropRoot(hit.collider);
            if (rb != null && rb.gameObject.layer == LayerMask.NameToLayer("Props"))
            {
                selectedObject = rb.gameObject;
                selectedRenderers = selectedObject.GetComponentsInChildren<Renderer>();
                if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: " + selectedObject.name.Replace("(Clone)", "").Replace("_Wrapper", "");
                if (selectionIndicatorText != null && selectedObject.GetComponent<StageLightStrip>() != null)
                    selectionIndicatorText.text += "  |  R: tilt  Q/E: turn";

                if (selectedRenderers.Length > 0)
                {
                    Material material = selectedRenderers[0].sharedMaterial;
                    if (material != null && material.HasProperty("_Color")) SyncSlidersToColor(material.color);
                }

                // --- NEW: Tell Tutorial we clicked a prop! ---
                if (TutorialManager.Instance != null) TutorialManager.Instance.OnObjectSelected(selectedObject.name);

                UpdatePoseActorButton();

                return;
            }

            bool belongsToCurrentWall = currentWall != null && hit.collider.transform.IsChildOf(currentWall.transform);
            if (belongsToCurrentWall || IsWallName(hit.collider.name))
            {
                selectedObject = belongsToCurrentWall ? currentWall : hit.collider.gameObject;
                selectedRenderers = selectedObject.GetComponentsInChildren<Renderer>();
                if (selectionIndicatorText != null) selectionIndicatorText.text = "";
                SyncSlidersToColor(currentWallColor);

                // --- NEW: Tell Tutorial we clicked the wall! ---
                if (TutorialManager.Instance != null) TutorialManager.Instance.OnObjectSelected(selectedObject.name);

                UpdatePoseActorButton();

                return;
            }
        }

        selectedObject = null;
        selectedRenderers = null;
        if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: None";
        UpdatePoseActorButton();
    }

    private void TryDelete3DObject()
    {
        Ray ray = GetMouseRay();
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Rigidbody rb = FindPropRoot(hit.collider);
            if (rb != null && rb.gameObject.layer == LayerMask.NameToLayer("Props"))
            {
                if (selectedObject != null && selectedObject.transform.IsChildOf(rb.transform))
                {
                    selectedObject = null;
                    selectedRenderers = null;
                    if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: None";
                    UpdatePoseActorButton();
                    if (selectionOutline != null) selectionOutline.enabled = false;
                }
                if (draggedObject != null && draggedObject.transform.IsChildOf(rb.transform))
                {
                    draggedObject = null;
                    draggedColliders = null;
                    draggedRenderers = null;
                    showPropCostWarningOnDrop = false;
                }
                Destroy(rb.gameObject);
                return;
            }
        }
    }

    public void StartDraggingNewProp(GameObject prefab3D)
    {
        if (draggedObject != null) return;

        showPropCostWarningOnDrop = false;

        if (TutorialManager.Instance != null)
        {
            string propName = prefab3D.name.ToLower();
            if (propName.Contains("cube") && !TutorialManager.Instance.CanUseTabletFeature("SpawnCube")) return;
            if ((propName.Contains("flower") || propName.Contains("floral")) && !TutorialManager.Instance.CanUseTabletFeature("SpawnFlower")) return;
        }

        if (CareerManager.Instance != null && CareerManager.Instance.TrySpendMoney(ProductionEconomy.Prop))
        {
            if (TutorialManager.Instance != null && TutorialManager.Instance.currentStep >= TutorialManager.TutorialStep.FreePlayDirectorTablet && !hasShownPropCostWarning)
            {
                showPropCostWarningOnDrop = true;
            }
        }
        else if (CareerManager.Instance != null)
        {
            if (TutorialManager.Instance != null) TutorialManager.Instance.ShowWarning("Not enough money! Props cost 250 B-Coins.");
            return;
        }

        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, dragHeight, 0));
        Ray ray = GetMouseRay();
        Vector3 spawnPos = Vector3.zero;

        if (groundPlane.Raycast(ray, out float enter)) spawnPos = ray.GetPoint(enter);

        GameObject wrapper = new GameObject(prefab3D.name + "_Wrapper");
        wrapper.transform.position = spawnPos;

        string sourceName = prefab3D.name.ToLowerInvariant();
        int productLevel = sourceName.Contains("goke") || sourceName.Contains("coke") ? 2 :
            sourceName.Contains("flower") || sourceName.Contains("floral") ? 1 : 0;
        GameObject visualProp = productLevel > 0 ? ProductModelCatalog.Create(productLevel, prefab3D.name) : null;
        bool isFlowerTable = sourceName.Contains("cube") && CampaignProgression.GetCurrentLevel() == 1;
        if (isFlowerTable) visualProp = ProductModelCatalog.CreateFurniture(true, new Vector3(1, 1, 1));
        bool usesImportedProduct = visualProp != null;
        if (usesImportedProduct) visualProp.transform.SetParent(wrapper.transform, false);
        else visualProp = Instantiate(prefab3D, wrapper.transform);
        visualProp.transform.localPosition = Vector3.zero;

        Renderer[] rends = visualProp.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in rends)
        {
            if (!usesImportedProduct && productLevel != 1) r.material.color = Color.white;
        }

        BoxCollider box = visualProp.GetComponent<BoxCollider>();
        if (box != null && box.size.x < 0.1f)
        {
            box.size = new Vector3(1, 1, 1);
            box.center = Vector3.zero;
        }

        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            foreach (Renderer r in rends) b.Encapsulate(r.bounds);
            float bottomEdge = b.min.y - wrapper.transform.position.y;
            visualProp.transform.localPosition = new Vector3(0, -bottomEdge, 0);
        }

        foreach (Transform t in wrapper.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = LayerMask.NameToLayer("Props");
        }

        Rigidbody newRb = wrapper.AddComponent<Rigidbody>();
        newRb.isKinematic = true;

        draggedObject = wrapper;
        selectedObject = wrapper;
        draggedColliders = draggedObject.GetComponentsInChildren<Collider>();
        draggedRenderers = rends;
        selectedRenderers = rends;
        justGrabbed = true;

        if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: " + selectedObject.name.Replace("(Clone)", "").Replace("_Wrapper", "");

        Material previewMaterial = rends.Length > 0 ? rends[0].sharedMaterial : null;
        SyncSlidersToColor(previewMaterial != null && previewMaterial.HasProperty("_Color") ? previewMaterial.color : Color.white);

        UpdatePoseActorButton();

        if (TutorialManager.Instance != null) TutorialManager.Instance.OnPropPickedFromUI(wrapper);
    }

    public void StartDraggingStageItem(string itemName, bool isActor, int itemIndex)
    {
        if (draggedObject != null) return;

        showPropCostWarningOnDrop = false;

        int itemCost = isActor ? ActorBot.HirePrice(itemIndex, ProductionEconomy.ActorBase) : itemIndex == -1 ? ProductionEconomy.LightStrip : itemIndex <= -2 ? AutomotiveGrip.Cost(itemIndex) : ProductionEconomy.Vehicle;
        if (CareerManager.Instance != null && !CareerManager.Instance.TrySpendMoney(itemCost))
        {
            if (TutorialManager.Instance != null)
            {
                string itemType = isActor ? "Actor" : "Car";
                TutorialManager.Instance.ShowWarning("Not enough money! " + itemType + " costs " + itemCost + " B-Coins.");
            }
            return;
        }

        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, dragHeight, 0));
        Ray ray = GetMouseRay();
        Vector3 spawnPos = Vector3.zero;

        if (groundPlane.Raycast(ray, out float enter)) spawnPos = ray.GetPoint(enter);

        GameObject wrapper = isActor ? CreateCubeActor(itemName, itemIndex) :
            itemIndex == -1 ? StageLightStrip.Create() : itemIndex <= -2 ? AutomotiveGrip.Create(itemIndex == -2) : CreateCubeCar(itemName);
        wrapper.transform.position = spawnPos;

        foreach (Transform t in wrapper.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = LayerMask.NameToLayer("Props");
        }

        Rigidbody newRb = wrapper.AddComponent<Rigidbody>();
        newRb.isKinematic = true;

        draggedObject = wrapper;
        selectedObject = wrapper;
        draggedColliders = wrapper.GetComponentsInChildren<Collider>();
        draggedRenderers = wrapper.GetComponentsInChildren<Renderer>();
        selectedRenderers = draggedRenderers;
        justGrabbed = true;

        if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: " + itemName +
            (wrapper.GetComponent<StageLightStrip>() != null ? "  |  R: tilt  Q/E: turn" : "");

        SyncSelectedProductColor();
        UpdatePoseActorButton();
    }

    public void StartDraggingCampaignProduct(string itemName, int campaignLevel)
    {
        if (draggedObject != null) return;

        showPropCostWarningOnDrop = false;

        if (CareerManager.Instance != null && !CareerManager.Instance.TrySpendMoney(ProductionEconomy.Prop))
        {
            if (TutorialManager.Instance != null) TutorialManager.Instance.ShowWarning("Not enough money! Props cost 250 B-Coins.");
            return;
        }

        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, dragHeight, 0));
        Ray ray = GetMouseRay();
        Vector3 spawnPos = Vector3.zero;

        if (groundPlane.Raycast(ray, out float enter)) spawnPos = ray.GetPoint(enter);

        GameObject wrapper = CreateCubeCampaignProduct(itemName, campaignLevel);
        wrapper.transform.position = spawnPos;

        foreach (Transform t in wrapper.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = LayerMask.NameToLayer("Props");
        }

        Rigidbody newRb = wrapper.AddComponent<Rigidbody>();
        newRb.isKinematic = true;

        draggedObject = wrapper;
        selectedObject = wrapper;
        draggedColliders = wrapper.GetComponentsInChildren<Collider>();
        draggedRenderers = wrapper.GetComponentsInChildren<Renderer>();
        selectedRenderers = draggedRenderers;
        justGrabbed = true;
        showPropCostWarningOnDrop = !hasShownPropCostWarning;

        if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: " + itemName;

        SyncSelectedProductColor();
        UpdatePoseActorButton();
    }

    private void UpdateStageButtonLabel()
    {
        if (spawnWallButton == null) return;
        TMP_Text label = spawnWallButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = CampaignProgression.GetCurrentLevel() >= 4 ? "CHOOSE SET" : "ADD WALL";
    }

    public Color GetSliderColor()
    {
        return NormalizeColor(rSlider != null ? rSlider.value : 0, gSlider != null ? gSlider.value : 0, bSlider != null ? bSlider.value : 0);
    }

    public void ApplyTypedColor(Color color)
    {
        if (!CanUseColorSliders()) return;
        SyncSlidersToColor(color);
        SetSelectedPropColor(rSlider != null && rSlider.maxValue > 1 ? color.r * 255 : color.r,
            gSlider != null && gSlider.maxValue > 1 ? color.g * 255 : color.g,
            bSlider != null && bSlider.maxValue > 1 ? color.b * 255 : color.b);
    }

    private void SyncSelectedProductColor()
    {
        if (selectedRenderers == null || selectedRenderers.Length == 0) return;
        Material material = selectedRenderers[0].sharedMaterial;
        if (material != null && material.HasProperty("_Color")) SyncSlidersToColor(material.color);
    }

    public void DropDraggedProp()
    {
        if (draggedObject != null)
        {
            if (TutorialManager.Instance != null && !TutorialManager.Instance.CanPlaceTutorialProp(draggedObject)) return;
            GameObject placedObject = draggedObject;
            bool shouldShowPropCostWarning = showPropCostWarningOnDrop;
            draggedObject = null;
            draggedColliders = null;
            draggedRenderers = null;
            showPropCostWarningOnDrop = false;

            if (TutorialManager.Instance != null)
                TutorialManager.Instance.OnPropPlaced(placedObject);

            CubeActor placedActor = placedObject.GetComponent<CubeActor>();
            if (placedActor != null && CampaignLevelManager.Instance != null)
            {
                CampaignLevelManager.Instance.OnActorPlaced(placedObject);
            }

            if (shouldShowPropCostWarning && TutorialManager.Instance != null)
            {
                hasShownPropCostWarning = true;
                TutorialManager.Instance.ShowTimedWarning("Spawned Prop! (-250 B-Coins)", 3f);
            }
        }
    }

    public bool IsPlacingProp()
    {
        return draggedObject != null;
    }

    private void MoveObjectWithMouse()
    {
        Ray ray = GetMouseRay();
        bool stacked = false;

        if (draggedColliders == null) draggedColliders = draggedObject.GetComponentsInChildren<Collider>();
        if (draggedRenderers == null) draggedRenderers = draggedObject.GetComponentsInChildren<Renderer>();

        int layerMask = (1 << LayerMask.NameToLayer("Props")) | (1 << LayerMask.NameToLayer("Default"));

        if (RaycastPastDraggedObject(ray, out RaycastHit hit, layerMask))
        {
            if (hit.normal.y > 0.5f)
            {
                float bottomOffset = 0f;
                if (draggedRenderers.Length > 0)
                {
                    Bounds b = draggedRenderers[0].bounds;
                    foreach (Renderer r in draggedRenderers) b.Encapsulate(r.bounds);
                    bottomOffset = draggedObject.transform.position.y - b.min.y;
                }

                draggedObject.transform.position = hit.point + new Vector3(0, bottomOffset + dragHeight, 0);
                stacked = true;
            }
        }


        if (!stacked)
        {
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, dragHeight, 0));
            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 flatHit = ray.GetPoint(enter);

                float bottomOffset = 0f;
                if (draggedRenderers.Length > 0)
                {
                    Bounds b = draggedRenderers[0].bounds;
                    foreach (Renderer r in draggedRenderers) b.Encapsulate(r.bounds);
                    bottomOffset = draggedObject.transform.position.y - b.min.y;
                }

                draggedObject.transform.position = flatHit + new Vector3(0, bottomOffset, 0);
            }
        }
    }

    private bool RaycastPastDraggedObject(Ray ray, out RaycastHit hit, int layerMask)
    {
        temporarilyDisabledColliders.Clear();
        try
        {
            foreach (Collider collider in draggedColliders)
            {
                // Procedural visuals may remove their temporary colliders after the drag starts.
                if (collider == null || !collider.enabled) continue;
                temporarilyDisabledColliders.Add(collider);
                collider.enabled = false;
            }
            return Physics.Raycast(ray, out hit, 100f, layerMask);
        }
        finally
        {
            foreach (Collider collider in temporarilyDisabledColliders)
                if (collider != null) collider.enabled = true;
            temporarilyDisabledColliders.Clear();
        }
    }

    public void ClearAllProps()
    {
        Rigidbody[] allRBs = FindObjectsOfType<Rigidbody>();
        foreach (Rigidbody rb in allRBs)
        {
            if (rb.gameObject.layer == LayerMask.NameToLayer("Props")) Destroy(rb.gameObject);
        }

        selectedObject = null;
        draggedObject = null;
        selectedRenderers = null;
        draggedColliders = null;
        draggedRenderers = null;
        showPropCostWarningOnDrop = false;
        if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: None";

        UpdatePoseActorButton();

        if (selectionOutline != null) selectionOutline.enabled = false;
    }

    public void SetSelectedPropColor(float r, float g, float b)
    {
        if (!CanUseColorSliders()) return;

        Color newCol = NormalizeColor(r, g, b);

        bool isWall = IsWallObject(selectedObject);

        if (selectedObject != null && (selectedObject.GetComponent<CubeActor>() != null || selectedObject.GetComponent<AutomotiveGrip>() != null)) return;

        if (selectedObject != null && !isWall)
        {
            StageLightStrip strip = selectedObject.GetComponent<StageLightStrip>();
            if (strip != null) { strip.SetColor(newCol); return; }
            ImportedProductVisual imported = selectedObject.GetComponentInChildren<ImportedProductVisual>();
            if (imported != null)
            {
                imported.SetBodyColor(newCol);
                return;
            }
            newCol = SnapTutorialPaintColor(newCol, false);
            if (selectedRenderers == null) selectedRenderers = selectedObject.GetComponentsInChildren<Renderer>();
            foreach (Renderer ren in selectedRenderers)
            {
                ren.material.color = newCol;
            }

            QueueTutorialColorCheck(newCol, false);
        }
        else
        {
            SetCustomColor(r, g, b);
        }
    }

    private void GeneratePropBankUI()
    {
        if (propUIContainer == null || uiPropCardPrefab == null) return;
        foreach (Transform child in propUIContainer) Destroy(child.gameObject);

        int currentLevel = CampaignProgression.GetCurrentLevel();

        foreach (LevelPropBank bank in propDatabase)
        {
            bool isTutorialMatch = (currentLevel == 1 && bank.progressLevel < 2);
            bool isLevelMatch = (currentLevel == bank.progressLevel);

            if (isTutorialMatch || isLevelMatch)
            {
                foreach (GameObject allowedPrefab in bank.allowedProps)
                {
                    GameObject newUICard = Instantiate(uiPropCardPrefab, propUIContainer);
                    UIDragProp dragScript = newUICard.GetComponent<UIDragProp>();
                    if (dragScript == null) dragScript = newUICard.AddComponent<UIDragProp>();
                    dragScript.Setup(allowedPrefab, this);

                    if (TutorialManager.Instance != null)
                    {
                        TutorialManager.Instance.RegisterDirectorPropCard(allowedPrefab.name, newUICard.GetComponent<RectTransform>());
                    }
                }
            }
        }

        if (currentLevel >= 4)
        {
            CreateStageItemCard("ACTOR A", true, 0);
            CreateStageItemCard("ACTOR B", true, 1);
            CreateStageItemCard("ACTOR C", true, 2);
        }

        if (currentLevel == 3 || currentLevel == 5)
        {
            CreateStageItemCard("TERRARI CAR", false, 0);
        }

        if (currentLevel >= 3)
        {



        }

        if (currentLevel == 4) CreateCampaignProductCard("KAPE KULTURA PRODUCT", 4);
        if (currentLevel == 5) CreateCampaignProductCard("HARAYA PRODUCT", 5);
    }

    public void OpenTerminal(GameObject pCam, PlayerController pController)
    {
        if (isTerminalActive)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (PauseManager.isPaused ||
            (AlmanacManager.Instance != null && AlmanacManager.Instance.IsOpen()) ||
            (ContractUIManager.Instance != null && ContractUIManager.Instance.IsContractUIOpen()))
        {
            return;
        }

        isTerminalActive = true;
        playerCameraObj = pCam;
        playerController = pController;
        if (playerController != null)
        {
            playerControllerWasEnabled = playerController.enabled;
            playerCouldMove = playerController.canMove;
            playerCouldLook = playerController.canLook;
            hasPlayerStateSnapshot = true;
            playerController.canMove = false;
            playerController.canLook = false;
            playerController.enabled = false;
        }

        if (tabletUI != null) tabletUI.SetActive(true);
        if (selectionIndicatorText != null) selectionIndicatorText.gameObject.SetActive(true);
        if (mainPlayerUI != null) mainPlayerUI.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GeneratePropBankUI();
        UpdateStageButtonLabel();
        UpdatePoseActorButton();
        if (TutorialManager.Instance != null) TutorialManager.Instance.OnTabletOpened();
    }

    public void CloseTerminal()
    {
        CancelInteriorPreview();
        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanCloseUI("DirectorTerminal"))
        {
            return;
        }

        isTerminalActive = false;
        if (interiorPicker != null) interiorPicker.SetActive(false);
        RestorePlayerAfterTerminal();

        if (tabletUI != null) tabletUI.SetActive(false);
        if (selectionIndicatorText != null) selectionIndicatorText.gameObject.SetActive(false);
        if (mainPlayerUI != null) mainPlayerUI.SetActive(true);

        if (selectionOutline != null) selectionOutline.enabled = false;

        if (TutorialManager.Instance != null) TutorialManager.Instance.OnTabletClosed();

        ApplyCursorStateAfterTerminal();
        StartCoroutine(RecheckCursorAfterTerminalClose());
    }

    private void RestorePlayerAfterTerminal()
    {
        if (!hasPlayerStateSnapshot || playerController == null) return;

        playerController.canMove = playerCouldMove;
        playerController.canLook = playerCouldLook;
        playerController.enabled = playerControllerWasEnabled;
        hasPlayerStateSnapshot = false;
    }

    private void ApplyCursorStateAfterTerminal()
    {
        bool anotherMenuNeedsCursor = PauseManager.isPaused ||
            (AlmanacManager.Instance != null && AlmanacManager.Instance.IsOpen()) ||
            (ContractUIManager.Instance != null && ContractUIManager.Instance.IsContractUIOpen());

        Cursor.lockState = anotherMenuNeedsCursor ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = anotherMenuNeedsCursor;
    }

    private IEnumerator RecheckCursorAfterTerminalClose()
    {
        yield return null;
        if (!isTerminalActive) ApplyCursorStateAfterTerminal();
    }

    public bool IsTerminalActive()
    {
        return isTerminalActive;
    }

    private bool IsWallObject(GameObject targetObject)
    {
        return targetObject != null && IsWallName(targetObject.name);
    }

    private void CreateStageItemCard(string itemName, bool isActor, int itemIndex)
    {
        GameObject newUICard = Instantiate(uiPropCardPrefab, propUIContainer);
        UIDragStageItem dragScript = newUICard.GetComponent<UIDragStageItem>();
        if (dragScript == null) dragScript = newUICard.AddComponent<UIDragStageItem>();
        dragScript.Setup(itemName, isActor, itemIndex, this);

        TextMeshProUGUI cardText = newUICard.GetComponentInChildren<TextMeshProUGUI>();
        if (cardText != null)
        {
            cardText.text = itemName + (isActor ? "\n" + ActorBot.TierName(itemIndex) : "") + "\n" +
                (isActor ? ActorBot.HirePrice(itemIndex, ProductionEconomy.ActorBase) : itemIndex == -1 ? ProductionEconomy.LightStrip : itemIndex <= -2 ? AutomotiveGrip.Cost(itemIndex) : ProductionEconomy.Vehicle) + " B";
            if (isActor)
            {
                cardText.enableAutoSizing = true;
                cardText.fontSizeMin = 12f;
                cardText.fontSizeMax = 24f;
            }
        }
    }

    private void CreateCampaignProductCard(string itemName, int campaignLevel)
    {
        GameObject newUICard = Instantiate(uiPropCardPrefab, propUIContainer);
        UIDragCampaignProduct dragScript = newUICard.GetComponent<UIDragCampaignProduct>();
        if (dragScript == null) dragScript = newUICard.AddComponent<UIDragCampaignProduct>();
        dragScript.Setup(itemName, campaignLevel, this);

        TextMeshProUGUI cardText = newUICard.GetComponentInChildren<TextMeshProUGUI>();
        if (cardText != null) cardText.text = itemName + "\n" + ProductionEconomy.Prop + " B";
    }

    private GameObject CreateCubeActor(string actorName, int actorIndex)
    {
        GameObject actor = new GameObject(actorName + "_Wrapper");
        CubeActor cubeActor = actor.AddComponent<CubeActor>();
        if (ActorBot.TryCreate(actor, actorIndex)) return actor;

        Color shirtColor = actorIndex == 0 ? new Color(0.8f, 0.15f, 0.15f) :
                           actorIndex == 1 ? new Color(0.15f, 0.35f, 0.85f) :
                           new Color(0.15f, 0.7f, 0.3f);
        Color skinColor = actorIndex == 2 ? new Color(0.45f, 0.25f, 0.12f) : new Color(0.9f, 0.65f, 0.4f);
        Color pantsColor = actorIndex == 1 ? new Color(0.12f, 0.12f, 0.12f) : new Color(0.1f, 0.18f, 0.35f);

        CreateCubePart("Body", actor.transform, new Vector3(0, 1.35f, 0), new Vector3(0.7f, 0.9f, 0.35f), shirtColor);
        CreateCubePart("Head", actor.transform, new Vector3(0, 2.1f, 0), new Vector3(0.5f, 0.5f, 0.5f), skinColor);

        Transform leftArm = CreateLimbPivot("Left Arm", actor.transform, new Vector3(-0.45f, 1.7f, 0), new Vector3(0.22f, 0.8f, 0.22f), shirtColor);
        Transform rightArm = CreateLimbPivot("Right Arm", actor.transform, new Vector3(0.45f, 1.7f, 0), new Vector3(0.22f, 0.8f, 0.22f), shirtColor);
        Transform leftLeg = CreateLimbPivot("Left Leg", actor.transform, new Vector3(-0.2f, 0.9f, 0), new Vector3(0.28f, 0.9f, 0.3f), pantsColor);
        Transform rightLeg = CreateLimbPivot("Right Leg", actor.transform, new Vector3(0.2f, 0.9f, 0), new Vector3(0.28f, 0.9f, 0.3f), pantsColor);

        cubeActor.Setup(leftArm, rightArm, leftLeg, rightLeg);
        return actor;
    }

    private GameObject CreateCubeCar(string carName)
    {
        GameObject imported = ProductModelCatalog.Create(3, carName + "_Wrapper");
        return imported != null ? imported : LamborminiVehicleVisual.Create(carName);
    }

    private GameObject CreateCubeCampaignProduct(string productName, int campaignLevel)
    {
        GameObject imported = ProductModelCatalog.Create(campaignLevel, productName + "_Wrapper");
        if (imported != null) return imported;
        GameObject product = new GameObject(productName + "_Wrapper");
        CampaignProduct campaignProduct = product.AddComponent<CampaignProduct>();
        campaignProduct.campaignLevel = campaignLevel;
        product.AddComponent<RecordableSubject>();

        if (campaignLevel == 4)
        {
            CreateCubePart("Coffee Package", product.transform, new Vector3(0, 0.65f, 0), new Vector3(0.8f, 1.3f, 0.8f), new Color(0.34f, 0.16f, 0.06f));
            CreateCubePart("Kape Label", product.transform, new Vector3(0, 0.75f, -0.43f), new Vector3(0.55f, 0.45f, 0.05f), new Color(0.95f, 0.72f, 0.22f));
        }
        else
        {
            CreateCubePart("Haraya Package", product.transform, new Vector3(0, 0.55f, 0), new Vector3(1.1f, 1.1f, 0.7f), new Color(0.05f, 0.55f, 0.58f));
            CreateCubePart("Haraya Label", product.transform, new Vector3(0, 0.6f, -0.38f), new Vector3(0.75f, 0.4f, 0.05f), new Color(1f, 0.78f, 0.2f));
        }

        return product;
    }

    private Transform CreateLimbPivot(string limbName, Transform parent, Vector3 localPosition, Vector3 limbScale, Color limbColor)
    {
        GameObject pivot = new GameObject(limbName + " Pivot");
        pivot.transform.SetParent(parent);
        pivot.transform.localPosition = localPosition;
        pivot.transform.localRotation = Quaternion.identity;

        CreateCubePart(limbName, pivot.transform, new Vector3(0, -limbScale.y * 0.5f, 0), limbScale, limbColor);
        return pivot.transform;
    }

    private GameObject CreateCubePart(string partName, Transform parent, Vector3 localPosition, Vector3 localScale, Color partColor)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = partName;
        cube.transform.SetParent(parent);
        cube.transform.localPosition = localPosition;
        cube.transform.localRotation = Quaternion.identity;
        cube.transform.localScale = localScale;

        Renderer cubeRenderer = cube.GetComponent<Renderer>();
        if (cubeRenderer != null) cubeRenderer.material.color = partColor;

        return cube;
    }

    private void CreatePoseActorButton()
    {
        if(poseActorButton!=null)
        {
            poseActorButton.onClick.RemoveListener(PoseSelectedActor);
            poseActorButton.onClick.AddListener(PoseSelectedActor);
            UpdatePoseActorButton();return;
        }
        if (spawnWallButton == null) return;

        GameObject poseButtonObject = Instantiate(spawnWallButton, spawnWallButton.transform.parent);
        poseButtonObject.name = "Pose Actor Button";

        RectTransform poseRect = poseButtonObject.GetComponent<RectTransform>();
        RectTransform wallRect = spawnWallButton.GetComponent<RectTransform>();
        if (poseRect != null && wallRect != null)
        {
            poseRect.anchoredPosition = wallRect.anchoredPosition + new Vector2(0, wallRect.sizeDelta.y + 30f);
        }

        TextMeshProUGUI buttonText = poseButtonObject.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null) buttonText.text = "POSE ACTOR";

        poseActorButton = poseButtonObject.GetComponent<Button>();
        if (poseActorButton != null)
        {
            poseActorButton.onClick = new Button.ButtonClickedEvent();
            poseActorButton.onClick.AddListener(PoseSelectedActor);
        }

        poseButtonObject.SetActive(CampaignProgression.GetCurrentLevel() >= 4);
        UpdatePoseActorButton();
    }

    public void PoseSelectedActor()
    {
        if (selectedObject == null) return;

        CubeActor cubeActor = selectedObject.GetComponent<CubeActor>();
        if (cubeActor == null) return;

        cubeActor.CyclePose();
        if (selectionIndicatorText != null)
        {
            selectionIndicatorText.text = "Selected: " + selectedObject.name.Replace("(Clone)", "").Replace("_Wrapper", "") + " - " + cubeActor.GetPoseName();
        }

        if (CampaignLevelManager.Instance != null) CampaignLevelManager.Instance.OnActorPosed(cubeActor);
    }

    private void UpdatePoseActorButton()
    {
        if (poseActorButton == null) return;

        poseActorButton.gameObject.SetActive(CampaignProgression.GetCurrentLevel() >= 4);
        poseActorButton.interactable = selectedObject != null && selectedObject.GetComponent<CubeActor>() != null;
    }

    private bool IsWallName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return false;

        return objectName.IndexOf("wall", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("stage", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("studio", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("backdrop", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void OnDestroy()
    {
        CancelInteriorPreview();
        if (selectionOutlineMaterial != null) Destroy(selectionOutlineMaterial);
    }
}

public class CubeVehicle : MonoBehaviour
{
}

public class CubeActor : MonoBehaviour
{
    private Transform leftArmPivot;
    private Transform rightArmPivot;
    private Transform leftLegPivot;
    private Transform rightLegPivot;
    private int currentPose = 0;

    public void Setup(Transform leftArm, Transform rightArm, Transform leftLeg, Transform rightLeg)
    {
        leftArmPivot = leftArm;
        rightArmPivot = rightArm;
        leftLegPivot = leftLeg;
        rightLegPivot = rightLeg;

        ApplyPose();
    }

    public void CyclePose()
    {
        currentPose++;
        if (currentPose > 2) currentPose = 0;

        ApplyPose();
    }

    public void SetPose(int pose)
    {
        currentPose = Mathf.Clamp(pose, 0, 2);
        ApplyPose();
    }

    public void BeginTake()
    {
        var bot = GetComponent<ActorBot>();
        if (bot != null) bot.RestartTake();
    }

    public string GetPoseName()
    {
        if (currentPose == 1) return "Wave";
        if (currentPose == 2) return "Action";
        return "Neutral";
    }

    private void ApplyPose()
    {
        var bot = GetComponent<ActorBot>();
        if (bot != null) { bot.SetPerformance(currentPose); return; }
        if (leftArmPivot == null || rightArmPivot == null || leftLegPivot == null || rightLegPivot == null) return;

        leftArmPivot.localRotation = Quaternion.Euler(0, 0, -5f);
        rightArmPivot.localRotation = Quaternion.Euler(0, 0, 5f);
        leftLegPivot.localRotation = Quaternion.Euler(0, 0, -2f);
        rightLegPivot.localRotation = Quaternion.Euler(0, 0, 2f);

        if (currentPose == 1)
        {
            leftArmPivot.localRotation = Quaternion.Euler(0, 0, -145f);
            rightArmPivot.localRotation = Quaternion.Euler(0, 0, 20f);
        }
        else if (currentPose == 2)
        {
            leftArmPivot.localRotation = Quaternion.Euler(0, 0, -70f);
            rightArmPivot.localRotation = Quaternion.Euler(0, 0, 70f);
            leftLegPivot.localRotation = Quaternion.Euler(0, 0, -15f);
            rightLegPivot.localRotation = Quaternion.Euler(0, 0, 15f);
        }
    }
}

public class UIDragStageItem : MonoBehaviour, IPointerClickHandler
{
    private string itemName;
    private bool isActor;
    private int itemIndex;
    private DirectorTerminal terminal;

    public void Setup(string displayName, bool actor, int index, DirectorTerminal term)
    {
        itemName = displayName;
        isActor = actor;
        itemIndex = index;
        terminal = term;

        TextMeshProUGUI label = GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = displayName;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (terminal != null) terminal.StartDraggingStageItem(itemName, isActor, itemIndex);
    }
}

public class UIDragCampaignProduct : MonoBehaviour, IPointerClickHandler
{
    private string itemName;
    private int campaignLevel;
    private DirectorTerminal terminal;

    public void Setup(string displayName, int level, DirectorTerminal term)
    {
        itemName = displayName;
        campaignLevel = level;
        terminal = term;

        TextMeshProUGUI label = GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = displayName;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (terminal != null) terminal.StartDraggingCampaignProduct(itemName, campaignLevel);
    }
}

