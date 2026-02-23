using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Interactable : MonoBehaviour
{   
    public string promptMessage; // Message to display when the player can interact with this object

    public void BaseInteract()
    {
        Interact();
    }
    protected virtual void Interact()
    {
        // Initialize any necessary components or variables here
    }
}
