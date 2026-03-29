using UnityEngine;

public class PropBin : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject propPrefab; // Drag your Flower or Cube prefab here
    public Transform spawnPoint;  // Where the item appears on stage
    public LayerMask propLayer;   // Set this to "Props"

    public GameObject SpawnNewProp()
    {
        if (propPrefab != null && spawnPoint != null)
        {
            GameObject newProp = Instantiate(propPrefab, spawnPoint.position, Quaternion.identity);

            // Ensure the new prop is on the correct layer so it can be dragged
            newProp.layer = (int)Mathf.Log(propLayer.value, 2);

            return newProp;
        }
        return null;
    }
}   