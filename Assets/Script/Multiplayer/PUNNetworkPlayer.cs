using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

// Inherit from MonoBehaviourPun to easily access the photonView
public class PUNNetworkPlayer : MonoBehaviourPun
{
    public float moveSpeed = 5f;
    public Camera localCamera;

    void Start()
    {
        // If this is NOT my player (it's my friend), turn off their camera!
        if (!photonView.IsMine)
        {
            if (localCamera != null) localCamera.gameObject.SetActive(false);

            // Optional: Turn off the AudioListener too so you don't hear out of their head
            AudioListener listener = GetComponentInChildren<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
    }

    void Update()
    {
        // Only let the keyboard move the character if THIS computer owns it
        if (photonView.IsMine)
        {
            Keyboard keyboard = Keyboard.current;
            Vector2 moveInput = Vector2.zero;

            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveInput.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveInput.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveInput.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveInput.y += 1f;
            }

            Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y).normalized;
            transform.position += moveDirection * moveSpeed * Time.deltaTime;
        }
    }
}
