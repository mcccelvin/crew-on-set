using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    private Camera cam;
    [SerializeField]
    private float distance = 3f;
    [SerializeField]
    private LayerMask mask; // Layer mask to specify which layers are interactable
    private PlayerUI playerUI; // Reference to the PlayerUI script to update the prompt text
    private InputManager inputManager;
    void Start()
    {
        cam = GetComponentInChildren<Camera>();
        playerUI = GetComponent<PlayerUI>();
        inputManager = GetComponent<InputManager>();
    }

    void Update()
    {
        playerUI.UpdateText(string.Empty);
        //create a ray at the center of the camera position in the direction it is facing
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        Debug.DrawRay(ray.origin, ray.direction * distance);
        RaycastHit hitInfo; // variable to store information about what the ray hit
        if (Physics.Raycast(ray, out hitInfo, distance, mask)) //cast the ray and check if it hits something within the specified distance
        {
            if (hitInfo.collider.GetComponent<Interactable>() != null) //check if the object hit has an Interactable component
            {
                Interactable interactable = hitInfo.collider.GetComponent<Interactable>();
                playerUI.UpdateText(hitInfo.collider.GetComponent<Interactable>().promptMessage);
                if (inputManager.onFoot.Interact.triggered) //check if the interact button is pressed
                {
                    interactable.BaseInteract(); //call the BaseInteract method on the Interactable component
                }
            }

        }

    }
            
}
