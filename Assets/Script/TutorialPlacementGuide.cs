using TMPro;
using UnityEngine;

// Keep the tutorial's graded target and its visible tablet-view marker at the same spot.
public static class TutorialPlacementGuide
{
    public static void Show(GameObject marker, GameObject backdrop, Camera tabletCamera)
    {
        if (marker == null) return;
        Vector3 point = marker.transform.position;
        Bounds floorArea = new Bounds(point, new Vector3(6, 0, 6));
        bool grounded = false;
        if (backdrop != null)
        {
            foreach (var renderer in backdrop.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.name.StartsWith("Screen", System.StringComparison.OrdinalIgnoreCase)) continue;
                Bounds bounds = renderer.bounds;
                var centre = new Vector3(bounds.center.x, bounds.max.y + 1, bounds.center.z);
                // Query the backdrop itself so a dragged table cannot become the target floor.
                foreach (var collider in backdrop.GetComponentsInChildren<Collider>())
                {
                    if (!collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy) continue;
                    if (!collider.Raycast(new Ray(centre, Vector3.down), out RaycastHit hit, bounds.size.y + 3) || hit.normal.y < .7f) continue;
                    point = hit.point; floorArea = bounds; grounded = true; break;
                }
                if (grounded) break;
            }
        }
        if (!grounded)
        {
            var hits = Physics.RaycastAll(point + Vector3.up * 8, Vector3.down, 20, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.normal.y < .7f || hit.rigidbody != null || hit.collider.transform.IsChildOf(marker.transform) ||
                    hit.collider.gameObject.layer == LayerMask.NameToLayer("Props")) continue;
                point.y = hit.point.y; grounded = true; break;
            }
        }
        if (grounded) marker.transform.position = point + Vector3.up * .025f;
        marker.SetActive(true);
        foreach (var renderer in marker.GetComponentsInChildren<Renderer>())
        { renderer.enabled = true; renderer.forceRenderingOff = false; }
        if (tabletCamera == null) return;

        Vector3 visualPoint = marker.transform.position + Vector3.up * .12f;
        Vector3 viewport = tabletCamera.WorldToViewportPoint(visualPoint);
        Vector3 sight = visualPoint - tabletCamera.transform.position;
        bool obscured = false;
        foreach (var hit in Physics.RaycastAll(tabletCamera.transform.position, sight.normalized, sight.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.collider.transform.IsChildOf(marker.transform) && hit.rigidbody == null &&
                hit.collider.gameObject.layer != LayerMask.NameToLayer("Props")) { obscured = true; break; }
        if (viewport.z <= 0 || viewport.x < .1f || viewport.x > .9f || viewport.y < .1f || viewport.y > .9f || obscured)
        {
            // The old front-facing view can be filled by the raised pink wall.
            // Show the actual placement floor from above; mouse rays use this same camera.
            float height = Mathf.Max(6, backdrop != null ? floorArea.max.y - point.y + 2 : 6);
            tabletCamera.transform.position = marker.transform.position + Vector3.up * height;
            tabletCamera.transform.rotation = Quaternion.Euler(90, tabletCamera.transform.eulerAngles.y, 0);
            tabletCamera.orthographic = true;
            tabletCamera.orthographicSize = Mathf.Clamp(Mathf.Max(floorArea.extents.z,
                floorArea.extents.x / Mathf.Max(.3f, tabletCamera.aspect)) + .6f, 3, 8);
            tabletCamera.farClipPlane = Mathf.Max(tabletCamera.farClipPlane, height + 15);
        }
        foreach (var label in marker.GetComponentsInChildren<TextMeshPro>())
        {
            label.transform.rotation = tabletCamera.transform.rotation;
            // Keep the caption beside the circle in the tablet view, not underneath it.
            label.transform.position = marker.transform.position + Vector3.up * .3f + tabletCamera.transform.up * .95f;
        }
    }
}
