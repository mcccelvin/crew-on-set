using System;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public sealed class LoadingScreenController : MonoBehaviourPunCallbacks
{
    private static LoadingScreenController instance;
    private Canvas overlay;
    private TMP_Text loadingLabel;
    private static readonly string[] GameplayTips =
    {
        "Read your contract's requirements before setting up your shot.",
        "Keep the actor and product clearly visible in your camera framing.",
        "Check your lighting through the camera before recording a take.",
        "Use the Director Tablet to arrange your stage before filming.",
        "Review your recorded takes before assembling your commercial.",
        "Trim unnecessary waiting from your clips to keep your edit engaging."
    };
    private bool loading, networkLoading;
    private float startedAt;
    private AsyncOperation sceneOperation;
    private RectTransform progressFill;
    private float displayedProgress;
    private bool sceneReady;
    private Sprite barSprite;
    private Texture2D barTexture;
    private CanvasGroup overlayFade;
    private bool closing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private static LoadingScreenController Ensure()
    {
        if (instance != null) return instance;
        var root = new GameObject("Loading Screen");
        instance = root.AddComponent<LoadingScreenController>();
        DontDestroyOnLoad(root);
        instance.Build();
        SceneManager.sceneLoaded += instance.SceneLoaded;
        return instance;
    }

    public static void LoadScene(string scene) { Ensure().BeginLocal(() => SceneManager.LoadSceneAsync(scene)); }
    public static void LoadScene(int scene) { Ensure().BeginLocal(() => SceneManager.LoadSceneAsync(scene)); }
    public static void LoadNetworkScene(string scene) { Ensure().BeginNetwork(() => PhotonNetwork.LoadLevel(scene)); }
    public static void LoadNetworkScene(int scene) { Ensure().BeginNetwork(() => PhotonNetwork.LoadLevel(scene)); }

    // Joining clients let Photon perform the synchronized load itself.
    public static void ShowNetworkLoading()
    {
        var screen = Ensure();
        if (!screen.loading) screen.Show(true);
    }

    private void BeginLocal(Func<AsyncOperation> start)
    {
        if (loading && !networkLoading) return;
        StopAllCoroutines();
        Show(false);
        StartCoroutine(LoadLocal(start));
    }

    private void BeginNetwork(Action start)
    {
        if (loading && !networkLoading) return;
        StopAllCoroutines();
        Show(true);
        StartCoroutine(StartNetwork(start));
    }

    private void Show(bool network)
    {
        loading = true; networkLoading = network;
        closing = false;
        if (overlayFade != null) overlayFade.alpha = 1f;
        startedAt = Time.realtimeSinceStartup;
        if (loadingLabel != null)
            loadingLabel.text = "<color=#00EBEB><b>TIP</b></color>  " + GameplayTips[UnityEngine.Random.Range(0, GameplayTips.Length)];
        sceneOperation = null;
        sceneReady = false;
        displayedProgress = 0f;
        if (progressFill != null) progressFill.gameObject.SetActive(false);
        Time.timeScale = 1f;
        PauseManager.isPaused = false;
        MultiplayerPauseManager.isPaused = false;
        overlay.gameObject.SetActive(true);
        Update();
    }

    private IEnumerator LoadLocal(Func<AsyncOperation> start)
    {
        // Render the overlay before starting scene work.
        yield return null;
        AsyncOperation operation = null;
        try { operation = start(); }
        catch (Exception error) { Debug.LogException(error); }
        if (operation == null) { Finish(); yield break; }
        sceneOperation = operation;
        operation.allowSceneActivation = false;
        while (operation.progress < .9f || Time.realtimeSinceStartup - startedAt < .65f)
            yield return null;
        operation.allowSceneActivation = true;
        while (!operation.isDone) yield return null;
        sceneReady = true;
        while (displayedProgress < 1f) yield return null;
        yield return new WaitForSecondsRealtime(.12f);
        yield return CloseWithFade();
    }

    private IEnumerator StartNetwork(Action start)
    {
        yield return null;
        try { start(); }
        catch (Exception error) { Debug.LogException(error); Finish(); }
    }

    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (loading && networkLoading && mode == LoadSceneMode.Single) StartCoroutine(FinishNetwork());
    }

    private IEnumerator FinishNetwork()
    {
        sceneReady = true;
        while (networkLoading && displayedProgress < 1f) yield return null;
        yield return new WaitForSecondsRealtime(.12f);
        if (networkLoading && !closing) yield return CloseWithFade();
    }

    private IEnumerator CloseWithFade()
    {
        if (closing) yield break;
        closing = true;
        for (float time = 0; time < .7f && loading; time += Time.unscaledDeltaTime)
        {
            overlayFade.alpha = 1f - Mathf.SmoothStep(0f, 1f, time / .7f);
            yield return null;
        }
        Finish();
    }

    public override void OnDisconnected(DisconnectCause cause) { if (networkLoading) Finish(); }
    public override void OnLeftRoom() { if (networkLoading) Finish(); }

    private void Finish()
    {
        loading = false; networkLoading = false;
        closing = false;
        if (overlayFade != null) overlayFade.alpha = 0f;
        if (overlay != null) overlay.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!loading || progressFill == null) return;
        float progress = networkLoading ? PhotonNetwork.LevelLoadingProgress :
            sceneOperation != null ? sceneOperation.progress / .9f : 0f;
        float target = sceneReady ? 1f : Mathf.Clamp01(progress) * .95f;
        displayedProgress = Mathf.MoveTowards(displayedProgress, Mathf.Max(displayedProgress, target), Time.unscaledDeltaTime * 1.5f);
        progressFill.gameObject.SetActive(displayedProgress > 0f);
        progressFill.sizeDelta = new Vector2(Mathf.Max(18f, 460f * displayedProgress), 18f);
    }

    private void Build()
    {
        var canvasObject = new GameObject("Loading Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        overlay = canvasObject.GetComponent<Canvas>();
        overlayFade = canvasObject.AddComponent<CanvasGroup>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 32760;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1024, 576);
        scaler.matchWidthOrHeight = .5f;
        var backdrop = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(canvasObject.transform, false);
        Stretch(backdrop.GetComponent<RectTransform>());
        backdrop.GetComponent<Image>().color = new Color32(17, 17, 17, 255);
        var imageObject = new GameObject("Main Menu Logo", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(backdrop.transform, false);
        var imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = imageRect.anchorMax = new Vector2(.5f, .5f);
        imageRect.anchoredPosition = new Vector2(0, 35);
        var logo = imageObject.GetComponent<RawImage>();
        logo.raycastTarget = false;
        var art = Resources.Load<ExportUIArt>("LoadingLogo");
        if (art != null && art.entries != null && art.entries.Length > 0)
            logo.texture = art.entries[0].texture;
        float aspect = logo.texture != null ? (float)logo.texture.width / logo.texture.height : 1.4f;
        imageRect.sizeDelta = new Vector2(340, 340 / aspect);
        var textObject = new GameObject("Gameplay Tip", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(backdrop.transform, false);
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = textRect.anchorMax = new Vector2(.5f, .5f);
        textRect.anchoredPosition = new Vector2(0, -205);
        textRect.sizeDelta = new Vector2(460, 64);
        loadingLabel = textObject.GetComponent<TextMeshProUGUI>();
        loadingLabel.font = TMP_Settings.defaultFontAsset;
        loadingLabel.fontSize = 19;
        loadingLabel.enableWordWrapping = true;
        loadingLabel.alignment = TextAlignmentOptions.Center;
        loadingLabel.color = Color.white;
        loadingLabel.raycastTarget = false;
        loadingLabel.text = "";
        BuildProgressBar(backdrop.transform);
        // Transparent full-screen graphic continues blocking UI clicks during the reveal.
        var blocker = new GameObject("Transition input shield", typeof(RectTransform), typeof(Image));
        blocker.transform.SetParent(canvasObject.transform, false);
        Stretch(blocker.GetComponent<RectTransform>());
        blocker.GetComponent<Image>().color = Color.clear;
        canvasObject.SetActive(false);
    }

    private void BuildProgressBar(Transform parent)
    {
        // Small antialiased rounded sprite, sliced so the ends stay round at any width.
        const int size = 32;
        barTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        barTexture.wrapMode = TextureWrapMode.Clamp;
        barTexture.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(16, 16));
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(16f - distance));
            }
        barTexture.SetPixels(pixels);
        barTexture.Apply(false, true);
        barSprite = Sprite.Create(barTexture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0,
            SpriteMeshType.FullRect, new Vector4(16, 16, 16, 16));
        var track = new GameObject("Loading Track", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(parent, false);
        var trackRect = track.GetComponent<RectTransform>();
        trackRect.anchorMin = trackRect.anchorMax = new Vector2(.5f, .5f);
        trackRect.anchoredPosition = new Vector2(0, -157);
        trackRect.sizeDelta = new Vector2(460, 18);
        StyleBar(track.GetComponent<Image>(), new Color32(100, 100, 100, 255));
        var fill = new GameObject("Loading Progress", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(track.transform, false);
        progressFill = fill.GetComponent<RectTransform>();
        progressFill.anchorMin = progressFill.anchorMax = new Vector2(0, .5f);
        progressFill.pivot = new Vector2(0, .5f);
        progressFill.anchoredPosition = Vector2.zero;
        progressFill.sizeDelta = new Vector2(18, 18);
        StyleBar(fill.GetComponent<Image>(), new Color32(0, 235, 235, 255));
        fill.SetActive(false);
    }

    private void StyleBar(Image image, Color color)
    {
        image.sprite = barSprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 2f;
        image.color = color;
        image.raycastTarget = false;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        if (barSprite != null) Destroy(barSprite);
        if (barTexture != null) Destroy(barTexture);
        if (instance == this) instance = null;
    }
}
