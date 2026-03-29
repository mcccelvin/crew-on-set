using UnityEngine;
using UnityEngine.UI;
using Player.PlayerController;
using UnityEngine.EventSystems;
using TMPro;

public class DirectorTerminal : MonoBehaviour
{
    [Header("Cameras & UI")]
    public Camera topDownCamera;
    public GameObject tabletUI;

    public TextMeshProUGUI selectionIndicatorText;

    [Header("Drag Settings")]
    public LayerMask moveableLayer;
    public float dragHeight = 0.1f;

    private GameObject playerCameraObj;
    private PlayerController playerController;
    private GameObject mainPlayerUI;

    private Rigidbody draggedRB;
    private GameObject selectedObject;

    private bool isTerminalActive = false;

    private void Start()
    {
        if (topDownCamera != null) topDownCamera.gameObject.SetActive(false);
        if (tabletUI != null) tabletUI.SetActive(false);
        if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: None";

        Canvas[] allCanvases = FindObjectsOfType<Canvas>(true);
        foreach (Canvas canvas in allCanvases)
        {
            Transform[] allChildren = canvas.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child.name == "Player UI" || child.name == "PlayerUI" || child.name == "Main UI")
                {
                    mainPlayerUI = child.gameObject;
                }
            }
        }
    }

    private void Update()
    {
        if (!isTerminalActive) return;

        // --- 1. IF WE ARE CURRENTLY HOLDING AN OBJECT ---
        if (draggedRB != null)
        {
            MoveObjectWithMouse();

            if (Input.GetMouseButtonDown(0))
            {
                if (!EventSystem.current.IsPointerOverGameObject())
                {
                    draggedRB.isKinematic = false;
                    draggedRB = null;
                }
            }
            return;
        }

        // --- 2. IF WE ARE NOT HOLDING ANYTHING ---
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                TryClickUIProp();
            }
            else
            {
                TrySelectObject();
            }
        }

        // Detect 'T' key to pick up
        if (Input.GetKeyDown(KeyCode.T) && selectedObject != null)
        {
            TryGrabSelectedObject();

            // --- TUTORIAL PING ---
            if (TutorialManager.Instance != null) TutorialManager.Instance.OnPropMovedWithT();
        }
    }

    private void TrySelectObject()
    {
        Ray ray = topDownCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            if (hit.collider.name.ToLower().Contains("wall") || hit.collider.GetComponentInParent<StageSetupManager>())
            {
                selectedObject = hit.collider.gameObject;
                if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: Stage Wall";
                return;
            }

            if (((1 << hit.collider.gameObject.layer) & moveableLayer) != 0)
            {
                selectedObject = hit.collider.gameObject;
                if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: " + selectedObject.name.Replace("(Clone)", "");
                return;
            }
        }

        selectedObject = null;
        if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: None";
    }

    private void TryGrabSelectedObject()
    {
        if (selectedObject.name.ToLower().Contains("wall") || selectedObject.GetComponentInParent<StageSetupManager>()) return;

        Rigidbody rb = selectedObject.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            draggedRB = rb;
            draggedRB.isKinematic = true;
        }
    }

    private void TryClickUIProp()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            PropUIData propData = result.gameObject.GetComponent<PropUIData>();
            if (propData != null && propData.propPrefab != null)
            {
                Plane groundPlane = new Plane(Vector3.up, new Vector3(0, dragHeight, 0));
                Ray ray = topDownCamera.ScreenPointToRay(Input.mousePosition);

                if (groundPlane.Raycast(ray, out float enter))
                {
                    Vector3 spawnWorldPos = ray.GetPoint(enter);
                    GameObject newProp = Instantiate(propData.propPrefab, spawnWorldPos, Quaternion.identity);
                    newProp.layer = LayerMask.NameToLayer("Props");

                    selectedObject = newProp;
                    if (selectionIndicatorText != null) selectionIndicatorText.text = "Selected: " + selectedObject.name.Replace("(Clone)", "");

                    draggedRB = newProp.GetComponent<Rigidbody>();
                    if (draggedRB != null) draggedRB.isKinematic = true;

                    // --- TUTORIAL PING ---
                    if (TutorialManager.Instance != null) TutorialManager.Instance.OnPropSpawnedFromUI();
                }
                return;
            }
        }
    }

    private void MoveObjectWithMouse()
    {
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, dragHeight, 0));
        Ray ray = topDownCamera.ScreenPointToRay(Input.mousePosition);

        if (groundPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            draggedRB.MovePosition(new Vector3(hitPoint.x, dragHeight, hitPoint.z));
        }
    }

    public void SetSelectedPropColor(float r, float g, float b)
    {
        if (selectedObject != null)
        {
            Color newCol = new Color(r, g, b, 1f);
            MeshRenderer[] renderers = selectedObject.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer ren in renderers)
            {
                ren.material.color = newCol;
            }

            StageSetupManager stageManager = selectedObject.GetComponentInParent<StageSetupManager>();
            if (stageManager != null)
            {
                stageManager.currentWallColor = newCol;
            }

            // --- TUTORIAL PING ---
            if (TutorialManager.Instance != null) TutorialManager.Instance.OnWallColorChanged();
        }
    }

    public void OpenTerminal(GameObject pCam, PlayerController pController)
    {
        isTerminalActive = true;
        playerCameraObj = pCam;
        playerController = pController;
        if (playerController != null) playerController.enabled = false;
        if (playerCameraObj != null) playerCameraObj.SetActive(false);
        if (topDownCamera != null) topDownCamera.gameObject.SetActive(true);
        if (tabletUI != null) tabletUI.SetActive(true);
        if (mainPlayerUI != null) mainPlayerUI.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // --- TUTORIAL PING ---
        if (TutorialManager.Instance != null) TutorialManager.Instance.OnTabletOpened();
    }

    public void CloseTerminal()
    {
        isTerminalActive = false;
        if (playerController != null) playerController.enabled = true;
        if (playerCameraObj != null) playerCameraObj.SetActive(true);
        if (topDownCamera != null) topDownCamera.gameObject.SetActive(false);
        if (tabletUI != null) tabletUI.SetActive(false);
        if (mainPlayerUI != null) mainPlayerUI.SetActive(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (TutorialManager.Instance != null) TutorialManager.Instance.OnTabletClosed();
    }
}