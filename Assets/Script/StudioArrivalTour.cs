using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class StudioArrivalTour : MonoBehaviour
{
    public static bool IsPendingOrPlaying { get; private set; }
    private static StudioArrivalTour active;
    private Camera view;
    private Camera gameplayView;
    private bool gameplayWasEnabled;
    private bool createdView;
    private readonly List<AudioListener> mutedListeners = new List<AudioListener>();
    private Player.PlayerController.PlayerController player;
    private Transform parent;
    private Vector3 position;
    private Quaternion rotation;
    private float fov;
    private bool captured, wasEnabled, couldMove, couldLook;
    private string taskShotName;
    private bool completed;
    private readonly Dictionary<Renderer, bool> playerVisibility = new Dictionary<Renderer, bool>();
    private readonly Dictionary<Renderer, bool> markerVisibility = new Dictionary<Renderer, bool>();

    private void HideStageMarkers()
    {
        // Hide only the authored guide circles, not their triggers or tutorial logic.
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var target in root.GetComponentsInChildren<Transform>(true))
            {
                string key = ShotKey(target.name);
                if (key != "pointa" && key != "pointb" && key != "pointc") continue;
                foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
                {
                    if (!markerVisibility.ContainsKey(renderer))
                        markerVisibility.Add(renderer, renderer.forceRenderingOff);
                    renderer.forceRenderingOff = true;
                }
            }
    }

    private void RestoreStageMarkers()
    {
        foreach (var entry in markerVisibility)
            if (entry.Key != null) entry.Key.forceRenderingOff = entry.Value;
        markerVisibility.Clear();
    }

    private void HidePlayerVisuals()
    {
        foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
        {
            playerVisibility[renderer] = renderer.forceRenderingOff;
            renderer.forceRenderingOff = true;
        }
    }

    private void RestorePlayerVisuals()
    {
        foreach (var entry in playerVisibility)
            if (entry.Key != null) entry.Key.forceRenderingOff = entry.Value;
        playerVisibility.Clear();
    }

    public static IEnumerator ShowTask(string shotName)
    {
        var tour = new GameObject("Tutorial Target Showcase").AddComponent<StudioArrivalTour>();
        tour.taskShotName = shotName;
        while (tour != null && !tour.completed) yield return null;
        if (tour != null) Destroy(tour.gameObject);
    }

    private static string ShotKey(string name) => name.Replace(" ", "").ToLowerInvariant();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        active = null;
        IsPendingOrPlaying = false;
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded -= DisableIdleShowcaseCameras;
        SceneManager.sceneLoaded += DisableIdleShowcaseCameras;
    }

    private static void DisableIdleShowcaseCameras(Scene scene, LoadSceneMode mode)
    {
        // Runs on every level load, even when no intro/tutorial is scheduled.
        // Disable components only: keep the authored shot transforms available.
        foreach (var root in scene.GetRootGameObjects())
            foreach (var target in root.GetComponentsInChildren<Transform>(true))
            {
                if (ShotKey(target.name) != "showcasecamera") continue;
                foreach (var camera in target.GetComponentsInChildren<Camera>(true))
                    camera.enabled = false;
                foreach (var listener in target.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = false;
            }
    }
    public static void Queue()
    {
        IsPendingOrPlaying = true;
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
    }
    private static void OnLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "SingleStudio") return;
        SceneManager.sceneLoaded -= OnLoaded;
        BeginWelcomeTour();
    }
    public static void BeginWelcomeTour()
    {
        // Retain the completed instance until scene unload to avoid playing twice.
        if (active != null) return;
        IsPendingOrPlaying = true;
        active = new GameObject("Studio Arrival Showcase").AddComponent<StudioArrivalTour>();
    }
    private IEnumerator Start()
    {
        for (int frame = 0; frame < 120; frame++)
        {
            yield return null;
            player = FindObjectOfType<Player.PlayerController.PlayerController>();
            gameplayView = player != null ? player.GameplayCamera : null;
            if (gameplayView != null) break;
        }
        if (gameplayView == null)
        {
            Debug.LogError("Studio showcase: player's gameplay camera was not found.");
            Finish(); yield break;
        }
        Transform showcase = null;
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == "ShowcaseCamera") { showcase = child; break; }
        if (showcase == null)
        {
            Debug.LogWarning("Studio showcase: save the ShowcaseCamera hierarchy in SingleStudio before playing.");
            Finish(); yield break;
        }
        var points = new List<Transform>();
        string[] wanted = taskShotName == null ? new[] { "IntroShowcase1", "IntroShowcase2" } : new[] { taskShotName };
        foreach (string name in wanted)
            foreach (var child in showcase.GetComponentsInChildren<Transform>(true))
                if (ShotKey(child.name) == ShotKey(name)) { points.Add(child); break; }
        if (points.Count != wanted.Length)
        {
            Debug.LogWarning("Studio showcase: missing angle " + string.Join(", ", wanted));
            Finish(); yield break;
        }
        // Capture every authored pose before moving the rendering camera.
        var positions = new Vector3[points.Count];
        var rotations = new Quaternion[points.Count];
        var fieldsOfView = new float[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            positions[i] = points[i].position;
            rotations[i] = points[i].rotation;
            var shot = points[i].GetComponent<Camera>();
            fieldsOfView[i] = shot != null ? shot.fieldOfView : 55f;
        }
        var cameras = showcase.GetComponentsInChildren<Camera>(true);
        foreach (var camera in cameras) camera.enabled = false;
        foreach (var listener in showcase.GetComponentsInChildren<AudioListener>(true))
            if (listener.enabled) { mutedListeners.Add(listener); listener.enabled = false; }
        // Use an authored camera when available; empty shot markers also work.
        foreach (var camera in cameras)
            if (camera.gameObject.activeInHierarchy) { view = camera; break; }
        if (view == null)
        {
            view = new GameObject("Showcase Render Camera").AddComponent<Camera>();
            view.CopyFrom(gameplayView);
            createdView = true;
        }
        parent = view.transform.parent;
        position = view.transform.localPosition;
        rotation = view.transform.localRotation;
        fov = view.fieldOfView;
        wasEnabled = player.enabled;
        couldMove = player.canMove; couldLook = player.canLook;
        captured = true;
        gameplayWasEnabled = gameplayView.enabled;
        gameplayView.enabled = false;
        player.canMove = player.canLook = false;
        player.enabled = false;
        HidePlayerVisuals();
        HideStageMarkers();
        view.transform.SetParent(null, true);
        var rig = FindObjectOfType<StudioShowcaseRig>();
        view.transform.SetPositionAndRotation(positions[0], rotations[0]);
        view.fieldOfView = fieldsOfView[0];
        view.enabled = true;
        float duration = rig != null ? Mathf.Max(1, rig.secondsPerShot) : 3f;
        yield return MoveView(positions[0], rotations[0], fieldsOfView[0], taskShotName == null ? 1.2f : 2.2f);
        for (int i = 1; i < points.Count; i++)
        {
            yield return MoveView(positions[i], rotations[i], fieldsOfView[i], duration);
        }
        yield return MoveView(gameplayView.transform.position, gameplayView.transform.rotation, gameplayView.fieldOfView, 1.5f);
        if (taskShotName == null)
        {
            // Switch to the actual player's view for the welcoming look-around.
            var showcaseView = view;
            showcaseView.enabled = false;
            gameplayView.enabled = true;
            RestorePlayerVisuals();
            view = gameplayView;
            Quaternion forward = view.transform.rotation;
            Vector3 eye = view.transform.position;
            yield return MoveView(eye, Quaternion.AngleAxis(-28f, Vector3.up) * forward, view.fieldOfView, 1.1f);
            yield return MoveView(eye, Quaternion.AngleAxis(28f, Vector3.up) * forward, view.fieldOfView, 1.7f);
            yield return MoveView(eye, forward, view.fieldOfView, 1.1f);
            view = showcaseView;
        }
        Finish();
    }
    private IEnumerator MoveView(Vector3 destination, Quaternion aim, float targetFov, float duration)
    {
        Vector3 from = view.transform.position;
        Quaternion fromRotation = view.transform.rotation;
        float fromFov = view.fieldOfView;
        for (float elapsed = 0; elapsed < duration;)
        {
            if (!PauseManager.isPaused)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / (duration * .8f));
                t = t * t * t * (t * (t * 6f - 15f) + 10f);
                view.transform.SetPositionAndRotation(Vector3.Lerp(from, destination, t), Quaternion.Slerp(fromRotation, aim, t));
                view.fieldOfView = Mathf.Lerp(fromFov, targetFov, t);
            }
            yield return null;
        }
    }
    private void Finish()
    {
        RestorePlayerVisuals();
        RestoreStageMarkers();
        if (captured)
        {
            if (view != null)
            {
                view.enabled = false;
                view.transform.SetParent(parent, false);
                view.transform.localPosition = position;
                view.transform.localRotation = rotation;
                view.fieldOfView = fov;
                if (createdView) Destroy(view.gameObject);
            }
            if (gameplayView != null)
            {
                gameplayView.enabled = gameplayWasEnabled;
                // Another showcase can have disabled gameplay when this shot began.
                // Do not restore that transient state if it leaves no display camera.
                bool hasDisplayCamera = false;
                foreach (var camera in Camera.allCameras)
                    if (camera.isActiveAndEnabled && camera.targetTexture == null &&
                        camera.targetDisplay == gameplayView.targetDisplay)
                    { hasDisplayCamera = true; break; }
                if (!hasDisplayCamera)
                {
                    gameplayView.gameObject.SetActive(true);
                    gameplayView.enabled = true;
                }
            }
            if (player != null)
            {
                player.enabled = wasEnabled;
                player.canMove = couldMove; player.canLook = couldLook;
                player.SyncLookToCamera();
            }
            captured = false;
        }
        if (active == this) IsPendingOrPlaying = false;
        completed = true;
    }
    private void OnDestroy()
    {
        // A completed old scene must not cancel the next scene's queued tour.
        if (captured) Finish();
        if (active == this) active = null;
    }
}
