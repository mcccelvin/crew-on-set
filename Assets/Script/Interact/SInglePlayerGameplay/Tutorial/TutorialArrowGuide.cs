using UnityEngine;

public class TutorialArrowGuide : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform player;
    public float forwardDistance = 1.5f;
    public float heightOffset = 1.2f;
    public float movementSpeed = 10f;

    [Header("Aim Settings")]
    public float pointAtHeightOffset = 1.0f;
    public bool isCylinderModel = true;

    private Transform currentTarget;
    private MeshRenderer[] arrowRenderers;

    private void Start()
    {
        if (player == null)
        {
            Player.PlayerController.PlayerController p = FindObjectOfType<Player.PlayerController.PlayerController>();
            if (p != null) player = p.transform;
        }

        arrowRenderers = GetComponentsInChildren<MeshRenderer>();
        SetArrowVisibility(false);
    }

    private void Update()
    {
        if (currentTarget == null || player == null)
        {
            SetArrowVisibility(false);
            return;
        }

        if (!currentTarget.gameObject.activeInHierarchy || currentTarget.IsChildOf(player))
        {
            SetArrowVisibility(false);
            return;
        }

        SetArrowVisibility(true);

        Vector3 idealPosition = player.position + (player.forward * forwardDistance) + (Vector3.up * heightOffset);
        transform.position = Vector3.Lerp(transform.position, idealPosition, Time.deltaTime * movementSpeed);

        // --- THE FIX: Look for an explicit AimTarget child ---
        Vector3 finalAimPoint = currentTarget.position + (Vector3.up * pointAtHeightOffset); // Default fallback

        // Search the children of our target. If one is named exactly "AimTarget", aim at that instead!
        Transform explicitTarget = currentTarget.Find("AimTarget");
        if (explicitTarget != null)
        {
            finalAimPoint = explicitTarget.position;
        }

        Vector3 directionToTarget = finalAimPoint - transform.position;

        if (directionToTarget.sqrMagnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

            if (isCylinderModel)
            {
                targetRotation *= Quaternion.Euler(90, 0, 0);
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * movementSpeed);
        }
    }

    public void PointAt(Transform target)
    {
        currentTarget = target;

        if (currentTarget != null && player != null)
        {
            transform.position = player.position + (player.forward * forwardDistance) + (Vector3.up * heightOffset);
        }
    }

    private void SetArrowVisibility(bool isVisible)
    {
        foreach (MeshRenderer r in arrowRenderers)
        {
            if (r != null) r.enabled = isVisible;
        }
    }
}