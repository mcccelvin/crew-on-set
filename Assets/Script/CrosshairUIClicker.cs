using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class CrosshairUIClicker : MonoBehaviour
{
    [Header("Click Settings")]
    [Tooltip("How close you need to stand to the UI to click it")]
    public float clickRange = 4f;

    void Update()
    {
        if (AlmanacManager.Instance != null && AlmanacManager.Instance.IsOpen()) return;

        // When the player clicks the Left Mouse Button
        if (Input.GetMouseButtonDown(0))
        {
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
                if (result.distance <= clickRange || result.distance == 0)
                {
                    // Check if the UI element we are looking at has a Button component
                    Button button = result.gameObject.GetComponentInParent<Button>();

                    if (button != null && button.interactable)
                    {
                        // Press the button and stop!
                        button.onClick.Invoke();
                        return;
                    }
                }
            }
        }
    }
}
