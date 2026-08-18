using UnityEngine;
using UnityEngine.UI;
using Player.PlayerController;
using TMPro;
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

    [Header("Level 3 Cast & Vehicle")]
    public int actorHireCost = 500;
    public int carSpawnCost = 50;

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

    private GameObject draggedObject;
    private GameObject selectedObject;
    private Collider[] draggedColliders;
    private Renderer[] draggedRenderers;
    private Renderer[] selectedRenderers;
    private bool isTerminalActive = false;
    private bool justGrabbed = false;
    private bool showPropCostWarningOnDrop = false;
    private bool hasShownPropCostWarning = false;
    private Button poseActorButton;

    private int displayedRValue = int.MinValue;
    private int displayedGValue = int.MinValue;
    private int displayedBValue = int.MinValue;
    private int displayedMoney = int.MinValue;

    private LineRenderer selectionOutline;
    private Material selectionOutlineMaterial;

    public bool HasWall() { return currentWall != null; }
    public GameObject GetCurrentWall() { return currentWall; }

    public GameObject CreatePracticeWall(Color wallColor)
    {
        if (wallPrefab == null || spawnPoint == null) return null;

        if (currentWall != null) Destroy(currentWall);

        currentWall = Instantiate(wallPrefab, spawnPoint.position, spawnPoint.rotation);
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

        if (rSlider != null) rSlider.value = rSlider.maxValue;
        if (gSlider != null) gSlider.value = gSlider.maxValue;
        if (bSlider != null) bSlider.value = bSlider.maxValue;
    }

    private void Start()
    {
        if (tabletUI != null) tabletUI.SetActive(false);
        if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: None";

        if (spawnWallButton != null) spawnWallButton.SetActive(true);
        if (colorControlPanel != null) colorControlPanel.SetActive(true);

        CreatePoseActorButton();

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

        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;

        TutorialClampSliders();

        UpdateUIText();

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

    private void TutorialClampSliders()
    {
        if (TutorialManager.Instance == null) return;

        var step = TutorialManager.Instance.currentStep;

        if (step == TutorialManager.TutorialStep.Tablet_PaintWall || step == TutorialManager.TutorialStep.Tablet_PaintCube)
        {
            if (bSlider != null)
            {
                float bTarget = bSlider.maxValue > 1f ? 150f : 150f / 255f;
                if (bSlider.value < bTarget)
                {
                    bSlider.value = bTarget;
                }
            }

            if (rSlider != null && rSlider.value < rSlider.maxValue)
            {
                rSlider.value = rSlider.maxValue;
            }
        }
    }

    private Color NormalizeColor(float r, float g, float b)
    {
        float normR = r > 1f ? r / 255f : r;
        float normG = g > 1f ? g / 255f : g;
        float normB = b > 1f ? b / 255f : b;
        return new Color(normR, normG, normB, 1f);
    }

    public void SpawnWall()
    {
        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanUseTabletFeature("AddWall")) return;

        if (currentWall == null && wallPrefab != null && spawnPoint != null)
        {
            if (CareerManager.Instance != null && !CareerManager.Instance.TrySpendMoney(50))
            {
                if (TutorialManager.Instance != null) TutorialManager.Instance.ShowWarning("The wall costs 50 B-Coins!");
                return;
            }

            currentWall = Instantiate(wallPrefab, spawnPoint.position, spawnPoint.rotation);
            if (spawnWallButton != null) spawnWallButton.SetActive(false);

            currentWallColor = Color.white;
            ApplyColorToWall(currentWallColor);

            if (rSlider != null) rSlider.value = rSlider.maxValue;
            if (gSlider != null) gSlider.value = gSlider.maxValue;
            if (bSlider != null) bSlider.value = bSlider.maxValue;

            if (TutorialManager.Instance != null) TutorialManager.Instance.OnWallAdded();
        }
    }

    public void SetCustomColor(float r, float g, float b)
    {
        currentWallColor = NormalizeColor(r, g, b);
        if (currentWall != null) ApplyColorToWall(currentWallColor);

        if (TutorialManager.Instance != null)
        {
            float rCheck = r > 1f ? r : r * 255f;
            float gCheck = g > 1f ? g : g * 255f;
            float bCheck = b > 1f ? b : b * 255f;
            TutorialManager.Instance.CheckWallColor(rCheck, gCheck, bCheck);
        }
    }

    private void ApplyColorToWall(Color newColor)
    {
        if (currentWall != null)
        {
            MeshRenderer[] renderers = currentWall.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers) renderer.material.color = newColor;
        }
    }

    public void ClearStage()
    {
        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanUseTabletFeature("ClearStage")) return;

        if (currentWall != null)
        {
            if (selectedObject == currentWall)
            {
                selectedObject = null;
                selectedRenderers = null;
            }

            Destroy(currentWall);
            currentWall = null;
        }

        currentWallColor = Color.white;

        if (rSlider != null) rSlider.value = rSlider.maxValue;
        if (gSlider != null) gSlider.value = gSlider.maxValue;
        if (bSlider != null) bSlider.value = bSlider.maxValue;

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

    private void SyncSlidersToColor(Color color)
    {
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
                    bounds = selectedRenderers[0].bounds;
                    foreach (Renderer r in selectedRenderers) bounds.Encapsulate(r.bounds);
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

    private void TrySelect3DObject()
    {
        Ray ray = GetMouseRay();
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Rigidbody rb = hit.collider.GetComponentInParent<Rigidbody>();
            if (rb != null && rb.gameObject.layer == LayerMask.NameToLayer("Props"))
            {
                selectedObject = rb.gameObject;
                selectedRenderers = selectedObject.GetComponentsInChildren<Renderer>();
                if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: " + selectedObject.name.Replace("(Clone)", "").Replace("_Wrapper", "");

                if (selectedRenderers.Length > 0)
                {
                    SyncSlidersToColor(selectedRenderers[0].material.color);
                }

                // --- NEW: Tell Tutorial we clicked a prop! ---
                if (TutorialManager.Instance != null) TutorialManager.Instance.OnObjectSelected(selectedObject.name);

                UpdatePoseActorButton();

                return;
            }

            if (IsWallName(hit.collider.name))
            {
                selectedObject = hit.collider.gameObject;
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
            Rigidbody rb = hit.collider.GetComponentInParent<Rigidbody>();
            if (rb != null && rb.gameObject.layer == LayerMask.NameToLayer("Props"))
            {
                if (selectedObject == rb.gameObject)
                {
                    selectedObject = null;
                    selectedRenderers = null;
                    if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: None";
                    UpdatePoseActorButton();
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

        if (CareerManager.Instance != null && CareerManager.Instance.TrySpendMoney(50))
        {
            if (TutorialManager.Instance != null && TutorialManager.Instance.currentStep >= TutorialManager.TutorialStep.FreePlayDirectorTablet && !hasShownPropCostWarning)
            {
                showPropCostWarningOnDrop = true;
            }
        }
        else if (CareerManager.Instance != null)
        {
            if (TutorialManager.Instance != null) TutorialManager.Instance.ShowWarning("Not enough money! Props cost 50 B-Coins.");
            return;
        }

        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, dragHeight, 0));
        Ray ray = GetMouseRay();
        Vector3 spawnPos = Vector3.zero;

        if (groundPlane.Raycast(ray, out float enter)) spawnPos = ray.GetPoint(enter);

        GameObject wrapper = new GameObject(prefab3D.name + "_Wrapper");
        wrapper.transform.position = spawnPos;

        GameObject visualProp = Instantiate(prefab3D, wrapper.transform);
        visualProp.transform.localPosition = Vector3.zero;

        Renderer[] rends = visualProp.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in rends)
        {
            r.material.color = Color.white;
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

        if (rSlider != null) rSlider.value = rSlider.maxValue;
        if (gSlider != null) gSlider.value = gSlider.maxValue;
        if (bSlider != null) bSlider.value = bSlider.maxValue;

        UpdatePoseActorButton();

        if (TutorialManager.Instance != null) TutorialManager.Instance.OnPropPickedFromUI(wrapper);
    }

    public void StartDraggingStageItem(string itemName, bool isActor, int itemIndex)
    {
        if (draggedObject != null) return;

        showPropCostWarningOnDrop = false;

        int itemCost = isActor ? actorHireCost : carSpawnCost;
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

        GameObject wrapper = isActor ? CreateCubeActor(itemName, itemIndex) : CreateCubeCar(itemName);
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

        if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: " + itemName;

        UpdatePoseActorButton();
    }

    public void StartDraggingCampaignProduct(string itemName, int campaignLevel)
    {
        if (draggedObject != null) return;

        showPropCostWarningOnDrop = false;

        if (CareerManager.Instance != null && !CareerManager.Instance.TrySpendMoney(50))
        {
            if (TutorialManager.Instance != null) TutorialManager.Instance.ShowWarning("Not enough money! Props cost 50 B-Coins.");
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

        UpdatePoseActorButton();
    }

    public void DropDraggedProp()
    {
        if (draggedObject != null)
        {
            GameObject placedObject = draggedObject;
            bool shouldShowPropCostWarning = showPropCostWarningOnDrop;
            draggedObject = null;
            draggedColliders = null;
            draggedRenderers = null;
            showPropCostWarningOnDrop = false;

            if (TutorialManager.Instance != null)
                TutorialManager.Instance.OnPropPlaced(placedObject);

            if (shouldShowPropCostWarning && TutorialManager.Instance != null)
            {
                hasShownPropCostWarning = true;
                TutorialManager.Instance.ShowTimedWarning("Spawned Prop! (-50 B-Coins)", 3f);
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
        foreach (var col in draggedColliders) col.enabled = false;

        int layerMask = (1 << LayerMask.NameToLayer("Props")) | (1 << LayerMask.NameToLayer("Default"));

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, layerMask))
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

        foreach (var col in draggedColliders) col.enabled = true;

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
        Color newCol = NormalizeColor(r, g, b);

        bool isWall = IsWallObject(selectedObject);

        if (selectedObject != null && selectedObject.GetComponent<CubeActor>() != null) return;

        if (selectedObject != null && !isWall)
        {
            if (selectedRenderers == null) selectedRenderers = selectedObject.GetComponentsInChildren<Renderer>();
            foreach (Renderer ren in selectedRenderers)
            {
                ren.material.color = newCol;
            }

            if (TutorialManager.Instance != null)
            {
                float rCheck = r > 1f ? r : r * 255f;
                float gCheck = g > 1f ? g : g * 255f;
                float bCheck = b > 1f ? b : b * 255f;
                TutorialManager.Instance.CheckCubeColor(rCheck, gCheck, bCheck);
            }
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

        if (currentLevel >= 3)
        {
            CreateStageItemCard("ACTOR A", true, 0);
            CreateStageItemCard("ACTOR B", true, 1);
            CreateStageItemCard("ACTOR C", true, 2);
        }

        if (currentLevel == 3 || currentLevel == 5)
        {
            CreateStageItemCard("LAMBORMINI CAR", false, 0);
        }

        if (currentLevel == 4) CreateCampaignProductCard("KAPE KULTURA PRODUCT", 4);
        if (currentLevel == 5) CreateCampaignProductCard("HARAYA PRODUCT", 5);
    }

    public void OpenTerminal(GameObject pCam, PlayerController pController)
    {
        isTerminalActive = true;
        playerCameraObj = pCam;
        playerController = pController;
        if (playerController != null) playerController.enabled = false;

        if (tabletUI != null) tabletUI.SetActive(true);
        if (mainPlayerUI != null) mainPlayerUI.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GeneratePropBankUI();
        UpdatePoseActorButton();
        if (TutorialManager.Instance != null) TutorialManager.Instance.OnTabletOpened();
    }

    public void CloseTerminal()
    {
        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanCloseUI("DirectorTerminal"))
        {
            return;
        }

        isTerminalActive = false;
        if (playerController != null) playerController.enabled = true;

        if (tabletUI != null) tabletUI.SetActive(false);
        if (mainPlayerUI != null) mainPlayerUI.SetActive(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (selectionOutline != null) selectionOutline.enabled = false;

        if (TutorialManager.Instance != null) TutorialManager.Instance.OnTabletClosed();
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
        if (cardText != null) cardText.text = itemName + "\n" + (isActor ? actorHireCost : carSpawnCost) + " B";
    }

    private void CreateCampaignProductCard(string itemName, int campaignLevel)
    {
        GameObject newUICard = Instantiate(uiPropCardPrefab, propUIContainer);
        UIDragCampaignProduct dragScript = newUICard.GetComponent<UIDragCampaignProduct>();
        if (dragScript == null) dragScript = newUICard.AddComponent<UIDragCampaignProduct>();
        dragScript.Setup(itemName, campaignLevel, this);

        TextMeshProUGUI cardText = newUICard.GetComponentInChildren<TextMeshProUGUI>();
        if (cardText != null) cardText.text = itemName + "\n50 B";
    }

    private GameObject CreateCubeActor(string actorName, int actorIndex)
    {
        GameObject actor = new GameObject(actorName + "_Wrapper");
        CubeActor cubeActor = actor.AddComponent<CubeActor>();

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
        GameObject car = new GameObject(carName + "_Wrapper");
        car.AddComponent<CubeVehicle>();

        CreateCubePart("Car Body", car.transform, new Vector3(0, 0.45f, 0), new Vector3(2.6f, 0.55f, 1.25f), new Color(0.75f, 0.05f, 0.05f));
        CreateCubePart("Car Cabin", car.transform, new Vector3(0.2f, 0.95f, 0), new Vector3(1.35f, 0.55f, 1f), new Color(0.25f, 0.35f, 0.45f));

        Color wheelColor = new Color(0.05f, 0.05f, 0.05f);
        CreateCubePart("Front Left Wheel", car.transform, new Vector3(0.85f, 0.2f, -0.7f), new Vector3(0.5f, 0.5f, 0.25f), wheelColor);
        CreateCubePart("Front Right Wheel", car.transform, new Vector3(0.85f, 0.2f, 0.7f), new Vector3(0.5f, 0.5f, 0.25f), wheelColor);
        CreateCubePart("Back Left Wheel", car.transform, new Vector3(-0.85f, 0.2f, -0.7f), new Vector3(0.5f, 0.5f, 0.25f), wheelColor);
        CreateCubePart("Back Right Wheel", car.transform, new Vector3(-0.85f, 0.2f, 0.7f), new Vector3(0.5f, 0.5f, 0.25f), wheelColor);

        return car;
    }

    private GameObject CreateCubeCampaignProduct(string productName, int campaignLevel)
    {
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
        if (spawnWallButton == null || poseActorButton != null) return;

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

        poseButtonObject.SetActive(PlayerPrefs.GetInt("TutorialProgress", 0) >= 3);
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
    }

    private void UpdatePoseActorButton()
    {
        if (poseActorButton == null) return;

        poseActorButton.gameObject.SetActive(PlayerPrefs.GetInt("TutorialProgress", 0) >= 3);
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

    public string GetPoseName()
    {
        if (currentPose == 1) return "Wave";
        if (currentPose == 2) return "Action";
        return "Neutral";
    }

    private void ApplyPose()
    {
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
