using Photon.Pun;
using UnityEngine;

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
            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");

            Vector3 moveDirection = new Vector3(moveX, 0, moveZ).normalized;
            transform.position += moveDirection * moveSpeed * Time.deltaTime;
        }
    }
}