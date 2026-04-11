using UnityEngine;
using UnityEngine.UI;
using Player.PlayerController;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;

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
    private bool isTerminalActive = false;
    private bool justGrabbed = false;

    private LineRenderer selectionOutline;

    public bool HasWall() { return currentWall != null; }

    private void Start()
    {
        if (tabletUI != null) tabletUI.SetActive(false);
        if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: None";

        if (spawnWallButton != null) spawnWallButton.SetActive(true);
        if (colorControlPanel != null) colorControlPanel.SetActive(true);

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

        TutorialClampSliders();

        UpdateUIText();

        if (draggedObject != null)
        {
            MoveObjectWithMouse();
            UpdateSelectionOutline();

            if (!justGrabbed && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0)))
            {
                if (IsMouseOverViewport()) DropDraggedProp();
            }
            justGrabbed = false;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (IsMouseOverViewport()) TrySelect3DObject();
        }
        else if (Input.GetMouseButtonDown(1))
        {
            if (IsMouseOverViewport()) TryDelete3DObject();
        }

        bool isWall = selectedObject != null && (selectedObject.name.ToLower().Contains("wall") || selectedObject.name.ToLower().Contains("stage") || selectedObject.name.ToLower().Contains("studio") || selectedObject.name.ToLower().Contains("backdrop"));

        if (Input.GetKeyDown(KeyCode.T) && selectedObject != null && !isWall)
        {
            draggedObject = selectedObject;
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
            float bTarget = bSlider.maxValue > 1f ? 150f : 150f / 255f;
            if (bSlider != null)
            {
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
            if (CareerManager.Instance != null)
            {
                if (CareerManager.Instance.playerMoney >= 50)
                {
                    CareerManager.Instance.playerMoney -= 50;
                    CareerManager.Instance.UpdateMoneyUI();
                }
                else
                {
                    if (TutorialManager.Instance != null) TutorialManager.Instance.ShowWarning("The wall costs 50 B-Coins!");
                    return;
                }
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

        if (currentWall != null) { Destroy(currentWall); currentWall = null; }

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
        if (rSlider != null && rValueText != null) rValueText.text = Mathf.RoundToInt(rSlider.maxValue > 1f ? rSlider.value : rSlider.value * 255f).ToString();
        if (gSlider != null && gValueText != null) gValueText.text = Mathf.RoundToInt(gSlider.maxValue > 1f ? gSlider.value : gSlider.value * 255f).ToString();
        if (bSlider != null && bValueText != null) bValueText.text = Mathf.RoundToInt(bSlider.maxValue > 1f ? bSlider.value : bSlider.value * 255f).ToString();
        if (bCoinsText != null && CareerManager.Instance != null) bCoinsText.text = CareerManager.Instance.playerMoney.ToString() + " B-Coins";
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
                selectionOutline.material = new Material(Shader.Find("Sprites/Default"));
                selectionOutline.startColor = Color.green; selectionOutline.endColor = Color.green;
            }

            bool isBlinkOn = (Time.time % 0.6f) > 0.3f;
            selectionOutline.enabled = isBlinkOn;

            if (isBlinkOn)
            {
                Bounds bounds = new Bounds(selectedObject.transform.position, Vector3.zero);
                Renderer[] rends = selectedObject.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    bounds = rends[0].bounds;
                    foreach (Renderer r in rends) bounds.Encapsulate(r.bounds);
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
        return RectTransformUtility.RectangleContainsScreenPoint(viewportUI, Input.mousePosition, null);
    }

    private Ray GetMouseRay()
    {
        if (viewportUI != null && topDownCamera != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewportUI, Input.mousePosition, null, out Vector2 localPoint);
            float normalizedX = (localPoint.x - viewportUI.rect.x) / viewportUI.rect.width;
            float normalizedY = (localPoint.y - viewportUI.rect.y) / viewportUI.rect.height;
            return topDownCamera.ViewportPointToRay(new Vector3(normalizedX, normalizedY, 0));
        }
        return topDownCamera != null ? topDownCamera.ScreenPointToRay(Input.mousePosition) : new Ray();
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
                if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: " + selectedObject.name.Replace("(Clone)", "").Replace("_Wrapper", "");

                Renderer[] rens = selectedObject.GetComponentsInChildren<Renderer>();
                if (rens.Length > 0)
                {
                    SyncSlidersToColor(rens[0].material.color);
                }

                // --- NEW: Tell Tutorial we clicked a prop! ---
                if (TutorialManager.Instance != null) TutorialManager.Instance.OnObjectSelected(selectedObject.name);

                return;
            }

            string objName = hit.collider.name.ToLower();
            if (objName.Contains("wall") || objName.Contains("stage") || objName.Contains("studio") || objName.Contains("backdrop"))
            {
                selectedObject = hit.collider.gameObject;
                if (selectionIndicatorText != null) selectionIndicatorText.text = "";
                SyncSlidersToColor(currentWallColor);

                // --- NEW: Tell Tutorial we clicked the wall! ---
                if (TutorialManager.Instance != null) TutorialManager.Instance.OnObjectSelected(selectedObject.name);

                return;
            }
        }

        selectedObject = null;
        if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: None";
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
                    if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: None";
                }
                Destroy(rb.gameObject);
                return;
            }
        }
    }

    public void StartDraggingNewProp(GameObject prefab3D)
    {
        if (TutorialManager.Instance != null)
        {
            string propName = prefab3D.name.ToLower();
            if (propName.Contains("cube") && !TutorialManager.Instance.CanUseTabletFeature("SpawnCube")) return;
            if ((propName.Contains("flower") || propName.Contains("floral")) && !TutorialManager.Instance.CanUseTabletFeature("SpawnFlower")) return;
        }

        if (CareerManager.Instance != null && CareerManager.Instance.playerMoney >= 50)
        {
            CareerManager.Instance.playerMoney -= 50;
            CareerManager.Instance.UpdateMoneyUI();

            if (TutorialManager.Instance != null && TutorialManager.Instance.currentStep >= TutorialManager.TutorialStep.FreePlayDirectorTablet)
            {
                TutorialManager.Instance.ShowWarning("Spawned Prop! (-50 B-Coins)");
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
        justGrabbed = true;

        if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: " + selectedObject.name.Replace("(Clone)", "").Replace("_Wrapper", "");

        if (rSlider != null) rSlider.value = rSlider.maxValue;
        if (gSlider != null) gSlider.value = gSlider.maxValue;
        if (bSlider != null) bSlider.value = bSlider.maxValue;
    }

    public void DropDraggedProp()
    {
        if (draggedObject != null)
        {
            string propName = draggedObject.name.ToLower();
            draggedObject = null;
        }
    }

    private void MoveObjectWithMouse()
    {
        Ray ray = GetMouseRay();
        bool stacked = false;

        Collider[] draggedColliders = draggedObject.GetComponentsInChildren<Collider>();
        foreach (var col in draggedColliders) col.enabled = false;

        int layerMask = (1 << LayerMask.NameToLayer("Props")) | (1 << LayerMask.NameToLayer("Default"));

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, layerMask))
        {
            if (hit.normal.y > 0.5f)
            {
                Renderer[] rends = draggedObject.GetComponentsInChildren<Renderer>();
                float bottomOffset = 0f;
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    foreach (Renderer r in rends) b.Encapsulate(r.bounds);
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

                Renderer[] rends = draggedObject.GetComponentsInChildren<Renderer>();
                float bottomOffset = 0f;
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    foreach (Renderer r in rends) b.Encapsulate(r.bounds);
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
        if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: None";

        if (selectionOutline != null) selectionOutline.enabled = false;
    }

    public void SetSelectedPropColor(float r, float g, float b)
    {
        Color newCol = NormalizeColor(r, g, b);

        bool isWall = selectedObject != null && (selectedObject.name.ToLower().Contains("wall") || selectedObject.name.ToLower().Contains("stage") || selectedObject.name.ToLower().Contains("studio") || selectedObject.name.ToLower().Contains("backdrop"));

        if (selectedObject != null && !isWall)
        {
            foreach (MeshRenderer ren in selectedObject.GetComponentsInChildren<MeshRenderer>())
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

        int progress = PlayerPrefs.GetInt("TutorialProgress", 0);

        foreach (LevelPropBank bank in propDatabase)
        {
            bool isTutorialMatch = (progress < 2 && bank.progressLevel < 2);
            bool isLevelMatch = (progress == bank.progressLevel);

            if (isTutorialMatch || isLevelMatch)
            {
                foreach (GameObject allowedPrefab in bank.allowedProps)
                {
                    GameObject newUICard = Instantiate(uiPropCardPrefab, propUIContainer);
                    UIDragProp dragScript = newUICard.GetComponent<UIDragProp>();
                    if (dragScript == null) dragScript = newUICard.AddComponent<UIDragProp>();
                    dragScript.Setup(allowedPrefab, this);
                }
            }
        }
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
}