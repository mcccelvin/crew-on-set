using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared tutorial routing/presentation; lesson owners still choose and hide targets.
[DisallowMultipleComponent]
public sealed class ProductionGuideLine : MonoBehaviour
{
    const float Clearance = .24f, Lift = .045f, Cell = .7f;
    const int SearchLimit = 600;
    LineRenderer line;
    LineRenderer source;
    Transform player;
    Camera viewCamera;
    Transform objective;
    Collider objectiveCollider;
    MeshRenderer objectiveRenderer;
    TutorialGlowTarget computerVisuals;
    LineRenderer objectCue;
    Canvas directionCanvas;
    CanvasGroup directionGroup;
    TMP_Text directionLabel;
    RectTransform directionArrow;
    bool needsTurn, turnRight = true;
    Vector3 lastStart, lastGoal;
    float nextRoute;
    bool complete;
    readonly List<Vector3> points = new List<Vector3>();
    readonly RaycastHit[] hits = new RaycastHit[32];
    readonly Collider[] overlaps = new Collider[32];
    readonly LineRenderer[] brackets = new LineRenderer[4];
    static Material ribbonMaterial, markerMaterial;
    static Texture2D ribbonTexture;
    static Sprite directionPaperSprite;

    public static void Draw(LineRenderer renderer, Transform from, Transform target)
    {
        Draw(renderer, from, target, null);
    }

    public static void Draw(LineRenderer renderer, Transform from, Transform target, Camera gameplayCamera)
    {
        if (renderer == null || from == null || target == null) return;
        var guide = renderer.GetComponent<ProductionGuideLine>();
        if (guide == null) guide = renderer.gameObject.AddComponent<ProductionGuideLine>();
        guide.Initialize(renderer);
        guide.player = from;
        guide.viewCamera = gameplayCamera;
        if (guide.objective != target)
        {
            guide.objective = target;
            guide.objectiveCollider = target.GetComponent<Collider>();
            guide.objectiveRenderer = target.GetComponentInChildren<MeshRenderer>();
            guide.computerVisuals = target.GetComponent<ComputerStation>() != null ? target.GetComponent<TutorialGlowTarget>() : null;
            guide.nextRoute = 0;
        }
        guide.DrawRoute(from.position, guide.TargetBounds().center);
    }

    // Gameplay cameras need not carry MainCamera's tag; other station/preview
    // cameras must not decide which direction the player is actually looking.
    Camera GuideCamera => viewCamera != null ? viewCamera : Camera.main;

    void Initialize(LineRenderer renderer)
    {
        if (source != null) return;
        source = renderer;
        source.positionCount = 0;
        var shader = Resources.Load<Shader>("ProductionGuide");
        if (shader == null) return;
        // The serialized LineRenderer shares the tutorial-manager object with its
        // stage markers. Rotate a dedicated ribbon, never the owner's transform.
        var ribbon = new GameObject("Production floor ribbon");
        ribbon.transform.SetParent(transform, false);
        line = ribbon.AddComponent<LineRenderer>();
        if (ribbonMaterial == null)
        {
            ribbonTexture = new Texture2D(128, 32, TextureFormat.RGBA32, false);
            ribbonTexture.name = "Production route filmstrip";
            ribbonTexture.wrapMode = TextureWrapMode.Repeat;
            ribbonTexture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[128 * 32];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 128; x++)
            {
                // Film perforations and rightward chevrons, generated without replacing art.
                bool edge = (y >= 2 && y <= 6 || y >= 25 && y <= 29) && x % 24 < 9;
                int tip = 58 - Mathf.Abs(y - 16) * 2;
                bool arrow = Mathf.Abs(y - 16) <= 8 && x >= tip && x < tip + 6;
                bool rail = y == 9 || y == 22;
                pixels[y * 128 + x] = arrow ? new Color32(255, 196, 75, 255) :
                    edge || rail ? new Color32(82, 200, 255, 230) : new Color32(25, 99, 145, 60);
            }
            ribbonTexture.SetPixels32(pixels); ribbonTexture.Apply(false, true);
            ribbonMaterial = new Material(shader) { name = "Depth-tested production route" };
            ribbonMaterial.mainTexture = ribbonTexture;
            ribbonMaterial.SetFloat("_Flow", .65f);
            markerMaterial = new Material(shader) { name = "Depth-tested framing marks" };
            markerMaterial.mainTexture = Texture2D.whiteTexture;
        }
        Style(line, ribbonMaterial, .2f);
        line.textureMode = LineTextureMode.Tile;
        for (int i = 0; i < brackets.Length; i++)
        {
            var obj = new GameObject("Destination frame " + i);
            obj.transform.SetParent(transform, false);
            brackets[i] = obj.AddComponent<LineRenderer>();
            Style(brackets[i], markerMaterial, .055f);
            brackets[i].startColor = brackets[i].endColor = new Color32(255, 196, 75, 255);
            brackets[i].positionCount = 3;
            obj.SetActive(false);
        }
        var cue = new GameObject("Object direction cue");
        cue.transform.SetParent(transform, false);
        objectCue = cue.AddComponent<LineRenderer>();
        Style(objectCue, markerMaterial, .035f);
        objectCue.alignment = LineAlignment.View;
        objectCue.startColor = objectCue.endColor = new Color32(255, 196, 75, 220);
        cue.SetActive(false);
        CreateDirectionCue();
    }

    void CreateDirectionCue()
    {
        var root = new GameObject("Guide direction HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        directionCanvas = root.GetComponent<Canvas>();
        directionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        directionCanvas.sortingOrder = 60;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        directionGroup = root.GetComponent<CanvasGroup>();
        directionGroup.alpha = 0;
        directionGroup.blocksRaycasts = directionGroup.interactable = false;
        var plate = HudRect("Production direction", root.transform, new Vector2(348, 84), Vector2.zero);
        plate.anchorMin = plate.anchorMax = new Vector2(.5f, .24f);
        var ink = new Color32(73, 46, 29, 255);
        var shadow = HudBox("Paper shadow", plate, new Vector2(348, 84), new Vector2(3, -5), new Color32(42, 24, 15, 95));
        shadow.SetAsFirstSibling();
        HudBox("Paper outline", plate, new Vector2(348, 84), Vector2.zero, ink);
        HudBox("Cream paper", plate, new Vector2(342, 78), Vector2.zero, new Color32(252, 245, 220, 255));
        HudBar("Gold paper rule", plate, new Vector2(308, 2), new Vector2(0, -34), new Color32(203, 159, 72, 255));
        directionArrow = HudRect("Turn chevron", plate, new Vector2(58, 58), new Vector2(-130, 0));
        var gold = new Color32(255, 196, 75, 255);
        HudBox("Arrow outline", directionArrow, new Vector2(58, 58), Vector2.zero, ink);
        HudBox("Gold arrow badge", directionArrow, new Vector2(54, 54), Vector2.zero, gold);
        HudBar("Upper stroke", directionArrow, new Vector2(24, 5), new Vector2(0, 7), ink).localRotation = Quaternion.Euler(0, 0, -40);
        HudBar("Lower stroke", directionArrow, new Vector2(24, 5), new Vector2(0, -7), ink).localRotation = Quaternion.Euler(0, 0, 40);
        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF");
        var caption = HudRect("Guide caption", plate, new Vector2(242, 22), new Vector2(33, 22)).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) caption.font = font;
        caption.text = "STUDIO GUIDE"; caption.fontSize = 16;
        caption.color = new Color32(153, 99, 52, 255); caption.alignment = TextAlignmentOptions.MidlineLeft;
        caption.enableWordWrapping = false; caption.raycastTarget = false;
        var text = HudRect("Turn instruction", plate, new Vector2(242, 38), new Vector2(33, -5));
        directionLabel = text.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) directionLabel.font = font;
        directionLabel.fontSize = 27;
        directionLabel.color = ink;
        directionLabel.alignment = TextAlignmentOptions.MidlineLeft;
        directionLabel.enableWordWrapping = false;
        directionLabel.raycastTarget = false;
        root.SetActive(false);
    }

    static RectTransform HudBox(string name, Transform parent, Vector2 size, Vector2 position, Color color)
    {
        var rect = HudBar(name, parent, size, position, color);
        if (directionPaperSprite == null)
        {
            const int edge = 48; const float radius = 10;
            var texture = new Texture2D(edge, edge, TextureFormat.RGBA32, false) { name = "Guide paper corners", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[edge * edge];
            for (int y = 0; y < edge; y++) for (int x = 0; x < edge; x++)
            {
                var point = new Vector2(x + .5f, y + .5f);
                var nearest = new Vector2(Mathf.Clamp(point.x, radius, edge - radius), Mathf.Clamp(point.y, radius, edge - radius));
                pixels[y * edge + x] = new Color(1, 1, 1, Mathf.Clamp01(radius + .5f - Vector2.Distance(point, nearest)));
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            directionPaperSprite = Sprite.Create(texture, new Rect(0, 0, edge, edge), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, Vector4.one * 12);
        }
        var image = rect.GetComponent<Image>();
        image.sprite = directionPaperSprite; image.type = Image.Type.Sliced;
        return rect;
    }

    static RectTransform HudRect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size; rect.anchoredPosition = position;
        return rect;
    }

    static RectTransform HudBar(string name, Transform parent, Vector2 size, Vector2 position, Color color)
    {
        var rect = HudRect(name, parent, size, position);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false;
        return rect;
    }

    void UpdateDirectionCue(Camera camera, bool controlsActive)
    {
        if (directionCanvas == null) return;
        bool active = controlsActive && camera != null && camera.isActiveAndEnabled &&
            source != null && source.enabled && line != null && objective != null && player != null && complete && points.Count > 1;
        if (!active)
        {
            needsTurn = false; directionGroup.alpha = 0;
            directionCanvas.gameObject.SetActive(false);
            return;
        }
        // Point toward the next walkable part of the route, not through walls to the goal.
        Vector3 next = points[points.Count - 1];
        for (int i = 1; i < points.Count; i++)
            if (Vector3.ProjectOnPlane(points[i] - player.position, Vector3.up).sqrMagnitude > 2.25f)
            { next = points[i]; break; }
        Vector3 heading = Vector3.ProjectOnPlane(next - player.position, Vector3.up);
        Vector3 forward = Vector3.Cross(camera.transform.right, Vector3.up).normalized;
        float angle = heading.sqrMagnitude > .01f ? Vector3.SignedAngle(forward, heading, Vector3.up) : 0;
        float horizontalFov = 2 * Mathf.Atan(Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) * camera.aspect) * Mathf.Rad2Deg;
        float threshold = Mathf.Clamp(horizontalFov * .43f, 20, 65);
        needsTurn = Mathf.Abs(angle) > threshold - (needsTurn ? 8 : 0);
        if (Mathf.Abs(angle) < 170) turnRight = angle > 0;
        directionLabel.text = Mathf.Abs(angle) > 145 ? "TURN AROUND" : turnRight ? "TURN RIGHT" : "TURN LEFT";
        directionArrow.localScale = new Vector3(turnRight ? 1 : -1, 1, 1);
        directionGroup.alpha = Mathf.MoveTowards(directionGroup.alpha, needsTurn ? 1 : 0, Time.unscaledDeltaTime * 7);
        directionCanvas.gameObject.SetActive(needsTurn || directionGroup.alpha > .001f);
    }

    Bounds TargetBounds()
    {
        if (computerVisuals != null && computerVisuals.TryGetVisualBounds(out var computerBounds)) return computerBounds;
        if (objectiveCollider != null && objectiveCollider.enabled) return objectiveCollider.bounds;
        if (objectiveRenderer != null && objectiveRenderer.enabled) return objectiveRenderer.bounds;
        return new Bounds(objective != null ? objective.position : lastGoal, Vector3.zero);
    }

    static void Style(LineRenderer renderer, Material material, float width)
    {
        renderer.sharedMaterial = material;
        renderer.useWorldSpace = true;
        renderer.alignment = LineAlignment.TransformZ;
        renderer.transform.rotation = Quaternion.Euler(90, 0, 0);
        renderer.widthCurve = AnimationCurve.Linear(0, 1, 1, 1);
        renderer.widthMultiplier = width;
        renderer.startColor = renderer.endColor = Color.white;
        renderer.numCornerVertices = 3;
        renderer.numCapVertices = 2;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    void DrawRoute(Vector3 start, Vector3 goal)
    {
        if (line == null || ribbonMaterial == null) return;
        if (Time.unscaledTime < nextRoute && (start - lastStart).sqrMagnitude < .25f &&
            (goal - lastGoal).sqrMagnitude < .0225f) return;
        nextRoute = Time.unscaledTime + .8f;
        lastStart = start; lastGoal = goal;
        points.Clear();
        complete = FindRoute(start, goal);
        line.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i] + Vector3.up * Lift);
        UpdateBrackets();
    }

    bool Ignored(Collider collider) => collider == null || collider.isTrigger ||
        collider.transform == transform || collider.transform.IsChildOf(transform) ||
        player != null && (collider.transform == player || collider.transform.IsChildOf(player));

    bool Ground(Vector3 sample, out Vector3 floor)
    {
        floor = sample;
        int count = Physics.RaycastNonAlloc(sample + Vector3.up * 2f, Vector3.down, hits, 6f,
            ~0, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return false; // Do not trust a truncated collision query.
        float nearest = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (Ignored(hit.collider) || hit.normal.y < .65f || hit.point.y > sample.y + .5f ||
                hit.collider.attachedRigidbody != null) continue;
            if (hit.distance < nearest) { nearest = hit.distance; floor = hit.point; found = true; }
        }
        return found;
    }

    bool Free(Vector3 floor)
    {
        int count = Physics.OverlapCapsuleNonAlloc(floor + Vector3.up * (Clearance + .06f),
            floor + Vector3.up * 1.35f, Clearance, overlaps, ~0, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length) return false;
        for (int i = 0; i < count; i++) if (!Ignored(overlaps[i])) return false;
        return true;
    }

    bool Clear(Vector3 a, Vector3 b)
    {
        if (Mathf.Abs(a.y - b.y) > .42f) return false;
        Vector3 delta = b - a; delta.y = 0;
        if (delta.sqrMagnitude < .0001f) return true;
        // Use the higher tread for small stage steps, retaining full wall clearance.
        Vector3 basePoint = new Vector3(a.x, Mathf.Max(a.y, b.y), a.z);
        int count = Physics.CapsuleCastNonAlloc(basePoint + Vector3.up * (Clearance + .06f),
            basePoint + Vector3.up * 1.35f, Clearance, delta.normalized, hits, delta.magnitude,
            ~0, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return false;
        for (int i = 0; i < count; i++) if (!Ignored(hits[i].collider)) return false;
        return true;
    }

    bool FindApproach(Vector3 start, Vector3 target, out Vector3 goal)
    {
        // Elevated monitor/prop pivots should lead to standing space, not a desktop.
        Vector3 footTarget = new Vector3(target.x, start.y, target.z);
        if (Ground(footTarget, out goal) && Free(goal)) return true;
        Vector3 toward = start - footTarget; toward.y = 0;
        if (toward.sqrMagnitude < .01f) toward = Vector3.forward;
        toward.Normalize();
        // Station/prop pivots may be inside solid desks: finish at a standing point nearby.
        for (float radius = .7f; radius <= 2.1f; radius += .7f)
            for (int i = 0; i < 8; i++)
            {
                int offset = i % 2 == 0 ? i / 2 : -(i + 1) / 2;
                Vector3 sample = footTarget + Quaternion.Euler(0, offset * 45, 0) * toward * radius;
                if (Ground(sample, out goal) && Free(goal)) return true;
            }
        return false;
    }

    sealed class Node
    {
        public Vector3 point;
        public bool checkedFloor, walkable, closed;
        public float cost = float.PositiveInfinity, estimate;
        public int parent = -1;
    }

    bool FindRoute(Vector3 start, Vector3 target)
    {
        if (!Ground(start, out start) || !FindApproach(start, target, out Vector3 goal)) return false;
        if (Clear(start, goal) && AddGroundedSegment(start, goal)) return true;
        points.Clear();
        float minX = Mathf.Min(start.x, goal.x) - 3.5f, minZ = Mathf.Min(start.z, goal.z) - 3.5f;
        int columns = Mathf.CeilToInt((Mathf.Abs(start.x - goal.x) + 7f) / Cell) + 1;
        int rows = Mathf.CeilToInt((Mathf.Abs(start.z - goal.z) + 7f) / Cell) + 1;
        if (columns * rows > 2304) return false;
        var nodes = new Node[columns * rows];
        var open = new List<int>();
        int sx = Mathf.Clamp(Mathf.RoundToInt((start.x - minX) / Cell), 0, columns - 1);
        int sz = Mathf.Clamp(Mathf.RoundToInt((start.z - minZ) / Cell), 0, rows - 1);
        int first = sx + sz * columns;
        nodes[first] = new Node { point = start, checkedFloor = true, walkable = true, cost = 0,
            estimate = Vector3.Distance(start, goal) };
        open.Add(first);
        int finish = -1;
        for (int visited = 0; open.Count > 0 && visited < SearchLimit; visited++)
        {
            int best = 0;
            for (int i = 1; i < open.Count; i++)
                if (nodes[open[i]].cost + nodes[open[i]].estimate < nodes[open[best]].cost + nodes[open[best]].estimate) best = i;
            int current = open[best]; open.RemoveAt(best);
            var node = nodes[current]; node.closed = true;
            if (node.estimate < Cell * 1.6f && Clear(node.point, goal)) { finish = current; break; }
            int cx = current % columns, cz = current / columns;
            for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && z == 0) continue;
                int nx = cx + x, nz = cz + z;
                if (nx < 0 || nz < 0 || nx >= columns || nz >= rows) continue;
                int id = nx + nz * columns;
                var next = nodes[id] ?? (nodes[id] = new Node());
                if (next.closed) continue;
                if (!next.checkedFloor)
                {
                    next.checkedFloor = true;
                    next.walkable = Ground(new Vector3(minX + nx * Cell, node.point.y, minZ + nz * Cell), out next.point) && Free(next.point);
                    next.estimate = Vector3.Distance(next.point, goal);
                }
                if (!next.walkable || !Clear(node.point, next.point)) continue;
                float cost = node.cost + Vector3.Distance(node.point, next.point);
                if (cost >= next.cost) continue;
                next.cost = cost; next.parent = current;
                if (!open.Contains(id)) open.Add(id);
            }
        }
        if (finish < 0) return false; // Never substitute an unsafe straight line.
        var route = new List<Vector3> { goal };
        for (int id = finish; id >= 0; id = nodes[id].parent) route.Add(nodes[id].point);
        route.Reverse();
        for (int i = 0; i < route.Count - 1;)
        {
            int next = i + 1;
            // Shorten only collision-clear, grounded links; don't round corners through walls.
            for (int j = route.Count - 1; j > next; j--) if (Clear(route[i], route[j])) { next = j; break; }
            if (!AddGroundedSegment(route[i], route[next])) { points.Clear(); return false; }
            i = next;
        }
        return points.Count > 1;
    }

    bool AddGroundedSegment(Vector3 a, Vector3 b)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / .35f));
        Vector3 previous = a;
        if (points.Count == 0) points.Add(a);
        for (int i = 1; i <= steps; i++)
        {
            if (!Ground(Vector3.Lerp(a, b, (float)i / steps), out Vector3 floor) ||
                !Free(floor) || !Clear(previous, floor)) return false;
            points.Add(floor); previous = floor;
        }
        return true;
    }

    void UpdateBrackets()
    {
        bool show = source != null && source.enabled && line != null && objective != null && complete && points.Count > 1;
        Vector3 centre = show ? points[points.Count - 1] + Vector3.up * (Lift + .005f) : Vector3.zero;
        Vector3 right = Vector3.right, up = Vector3.forward;
        float halfWidth = .42f, halfHeight = .32f;
        bool onObject = false;
        if (show)
        {
            var bounds = TargetBounds();
            onObject = bounds.center.y - centre.y > .35f;
            if (onObject)
            {
                // The floor endpoint is a place to stand, not the interaction target.
                // Frame the actual tablet/prop bounds, even when its pivot is at its base.
                var camera = GuideCamera;
                Vector3 eye = camera != null ? camera.transform.position : player.position + Vector3.up * 1.6f;
                Vector3 facing = (eye - bounds.center).normalized;
                if (facing.sqrMagnitude < .01f) facing = Vector3.forward;
                right = Vector3.Cross(Vector3.up, facing).normalized;
                if (right.sqrMagnitude < .01f) right = Vector3.right;
                up = Vector3.Cross(facing, right).normalized;
                centre = bounds.center + facing * (ProjectedExtent(bounds.extents, facing) + .04f);
                halfWidth = Mathf.Max(ProjectedExtent(bounds.extents, right) + .06f, .18f);
                halfHeight = Mathf.Max(ProjectedExtent(bounds.extents, up) + .06f, .18f);
                if (camera != null) FrameVisibleBounds(camera, bounds, ref centre, ref right, ref up, ref halfWidth, ref halfHeight);
            }
        }
        for (int i = 0; i < brackets.Length; i++)
        {
            if (brackets[i] == null) continue;
            brackets[i].gameObject.SetActive(show);
            if (!show) continue;
            brackets[i].alignment = onObject ? LineAlignment.View : LineAlignment.TransformZ;
            float x = i % 2 == 0 ? -1 : 1, y = i < 2 ? -1 : 1;
            Vector3 corner = centre + right * (x * halfWidth) + up * (y * halfHeight);
            brackets[i].SetPosition(0, corner - right * (x * Mathf.Min(.18f, halfWidth * .5f)));
            brackets[i].SetPosition(1, corner);
            brackets[i].SetPosition(2, corner - up * (y * Mathf.Min(.18f, halfHeight * .5f)));
        }
        if (objectCue == null) return;
        objectCue.gameObject.SetActive(show && onObject);
        if (show && onObject) UpdateObjectCue(centre);
    }

    static float ProjectedExtent(Vector3 extents, Vector3 direction) =>
        Mathf.Abs(direction.x) * extents.x + Mathf.Abs(direction.y) * extents.y + Mathf.Abs(direction.z) * extents.z;

    static void FrameVisibleBounds(Camera camera, Bounds bounds, ref Vector3 centre, ref Vector3 right,
        ref Vector3 up, ref float halfWidth, ref float halfHeight)
    {
        Vector2 min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue;
        float depth = float.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 projected = camera.WorldToViewportPoint(corner);
            if (projected.z <= camera.nearClipPlane + .04f) return;
            min = Vector2.Min(min, projected); max = Vector2.Max(max, projected);
            depth = Mathf.Min(depth, projected.z);
        }
        // Match the visible silhouette in perspective, not a fixed square around a pivot.
        depth = Mathf.Max(camera.nearClipPlane + .02f, depth - .03f);
        Vector2 middle = (min + max) * .5f;
        centre = camera.ViewportToWorldPoint(new Vector3(middle.x, middle.y, depth));
        right = camera.transform.right; up = camera.transform.up;
        halfWidth = Vector3.Distance(centre, camera.ViewportToWorldPoint(new Vector3(max.x, middle.y, depth))) + .045f;
        halfHeight = Vector3.Distance(centre, camera.ViewportToWorldPoint(new Vector3(middle.x, max.y, depth))) + .045f;
    }

    void UpdateObjectCue(Vector3 end)
    {
        Vector3 start = points[points.Count - 1] + Vector3.up * Lift;
        Vector3 control = new Vector3(start.x, Mathf.Max(end.y + .25f, start.y + .5f), start.z);
        objectCue.positionCount = 13;
        objectCue.SetPosition(0, start);
        Vector3 previous = start;
        for (int i = 1; i <= 12; i++)
        {
            float t = i / 12f;
            Vector3 point = (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end;
            Vector3 delta = point - previous;
            int count = Physics.RaycastNonAlloc(previous, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            bool blocked = count == hits.Length;
            for (int j = 0; j < count; j++)
                if (!Ignored(hits[j].collider) && hits[j].collider.transform != objective &&
                    !hits[j].collider.transform.IsChildOf(objective)) blocked = true;
            // Hide a blocked connecting arc rather than drawing through a desk or wall.
            if (blocked) { objectCue.positionCount = 0; return; }
            objectCue.SetPosition(i, point);
            previous = point;
        }
    }

    void LateUpdate()
    {
        if (source != null) source.positionCount = 0;
        if (line != null) line.enabled = source != null && source.enabled;
        UpdateBrackets();
        UpdateDirectionCue(GuideCamera, Cursor.lockState == CursorLockMode.Locked && Time.timeScale > 0);
    }
    void OnDisable()
    {
        if (line != null) line.enabled = false;
        foreach (var bracket in brackets) if (bracket != null) bracket.gameObject.SetActive(false);
        if (objectCue != null) objectCue.gameObject.SetActive(false);
        if (directionCanvas != null) directionCanvas.gameObject.SetActive(false);
        if (directionGroup != null) directionGroup.alpha = 0;
        needsTurn = false;
        nextRoute = 0;
    }
}
