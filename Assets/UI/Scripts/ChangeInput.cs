using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class ChangeInput : MonoBehaviour
{
    EventSystem system;
    public Selectable firstInput;
    public Button submitButton;

    // Start is called before the first frame update
    void Start()
    {

        system = EventSystem.current;
        if (firstInput != null) firstInput.Select();
    }

    // Update is called once per frame
    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (system == null) system = EventSystem.current;

        if (keyboard.tabKey.wasPressedThisFrame && keyboard.leftShiftKey.isPressed)
        {
            Selectable current = system != null && system.currentSelectedGameObject != null ?
                                 system.currentSelectedGameObject.GetComponent<Selectable>() : null;
            if (current == null)
            {
                if (firstInput != null) firstInput.Select();
                return;
            }

            Selectable previous = current.FindSelectableOnUp();
            if (previous != null)
            {
                previous.Select();
            }
        }
        else if (keyboard.tabKey.wasPressedThisFrame)
        {
            Selectable current = system != null && system.currentSelectedGameObject != null ?
                                 system.currentSelectedGameObject.GetComponent<Selectable>() : null;
            if (current == null)
            {
                if (firstInput != null) firstInput.Select();
                return;
            }

            Selectable next = current.FindSelectableOnDown();
            if (next != null)
            {
                next.Select();
            }
        }
        else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
        {
            Button selectedButton = system != null && system.currentSelectedGameObject != null ?
                                    system.currentSelectedGameObject.GetComponent<Button>() : null;
            if (selectedButton == null && submitButton != null) submitButton.onClick.Invoke();
        }
    }
}
