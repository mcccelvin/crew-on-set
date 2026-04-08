using UnityEngine;

public interface IInteractable
{
    // Called when the player presses 'E' while holding the item
    void OnInteract(GameObject player);

    // Called when the player presses 'G' to drop the item
    void OnDrop();
}