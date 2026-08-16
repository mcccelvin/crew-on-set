using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class CrosshairUIClicker : MonoBehaviour
{
    public static CrosshairUIClicker Instance;

    [Header("Click Settings")]
    [Tooltip("How close you need to stand to the UI to click it")]
    public float clickRange = 4f;

    private int clickConsumedFrame = -1;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (AlmanacManager.Instance != null && AlmanacManager.Instance.IsOpen()) return;
        if (PauseManager.isPaused) return;
        if (TutorialUIManager.Instance != null && TutorialUIManager.Instance.IsBossDialogueOpen()) return;
        if (ContractUIManager.Instance != null && ContractUIManager.Instance.IsContractUIOpen()) return;
        if (Cursor.visible || Cursor.lockState != CursorLockMode.Locked) return;

        // When the player clicks the Left Mouse Button
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            TryClickButton();
        }
    }

    public static bool TryClickButton()
    {
        if (Instance == null) return false;
        if (PauseManager.isPaused) return false;
        if (TutorialUIManager.Instance != null && TutorialUIManager.Instance.IsBossDialogueOpen()) return false;
        if (ContractUIManager.Instance != null && ContractUIManager.Instance.IsContractUIOpen()) return false;
        if (Cursor.visible || Cursor.lockState != CursorLockMode.Locked) return false;
        if (Instance.clickConsumedFrame == Time.frameCount) return true;
        if (EventSystem.current == null) return false;

        // Create a virtual "mouse pointer" fixed at the exact center of the screen
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = new Vector2(Screen.width / 2f, Screen.height / 2f)
        };

        // Shoot a raycast to see if the center of the screen is looking at any UI
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            // Verify we are standing close enough to the UI panel to interact
            if (result.distance <= Instance.clickRange || result.distance == 0)
            {
                // Check if the UI element we are looking at has a Button component
                Button button = result.gameObject.GetComponentInParent<Button>();

                if (button != null && button.interactable)
                {
                    Instance.clickConsumedFrame = Time.frameCount;
                    button.onClick.Invoke();
                    return true;
                }
            }
        }

        return false;
    }
}
