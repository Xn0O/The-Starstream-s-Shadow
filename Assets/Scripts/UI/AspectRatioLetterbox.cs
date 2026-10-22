using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 强制游戏画面按 16:9（1920x1080）显示，屏幕多余区域渲染为黑边。
/// 运行时自动生效，不需要修改任何场景；编辑器和打包版本都适用。
///
/// 场景相机保持全屏（避免 Postprocessing v2 的 viewport 警告），
/// 多余画面由最上层的黑色 UI 条遮住；UI 则通过专用相机渲染到
/// 同一个 16:9 视口内，因此不会跑到黑边上。
/// </summary>
public class AspectRatioLetterbox : MonoBehaviour
{
    public const float TargetAspect = 16f / 9f;

    private const int UILayer = 5;
    private const int BarsSortingOrder = 32000;

    private static AspectRatioLetterbox instance;

    private Camera backgroundCamera;
    private Camera uiCamera;

    private Canvas barsCanvas;
    private RectTransform leftBar;
    private RectTransform rightBar;
    private RectTransform topBar;
    private RectTransform bottomBar;

    private readonly HashSet<Canvas> convertedCanvases = new HashSet<Canvas>();
    private readonly Dictionary<Camera, float> baseOrthographicSizes = new Dictionary<Camera, float>();
    private readonly List<Camera> cleanupBuffer = new List<Camera>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        GameObject root = new GameObject("[AspectRatioLetterbox]");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<AspectRatioLetterbox>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        CreateCameras();
        CreateBars();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyCameraSettings();
        ApplyToCanvases();
    }

    private void CreateCameras()
    {
        // 铺满整个屏幕的黑色背景。
        backgroundCamera = CreateCamera("LetterboxBackgroundCamera", -1000f,
            CameraClearFlags.SolidColor, Color.black, 0);

        // 只渲染 UI 层，并限制在 16:9 视口内。
        uiCamera = CreateCamera("LetterboxUICamera", 1000f,
            CameraClearFlags.Depth, Color.clear, 1 << UILayer);
        uiCamera.nearClipPlane = 0.1f;
        uiCamera.farClipPlane = 100f;
    }

    private Camera CreateCamera(string name, float depth, CameraClearFlags clearFlags, Color color, int cullingMask)
    {
        GameObject cameraObject = new GameObject(name);
        cameraObject.transform.SetParent(transform, false);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.depth = depth;
        camera.clearFlags = clearFlags;
        camera.backgroundColor = color;
        camera.cullingMask = cullingMask;
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.useOcclusionCulling = false;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.targetDisplay = 0;
        return camera;
    }

    private void CreateBars()
    {
        GameObject root = new GameObject("LetterboxBars", typeof(RectTransform));
        DontDestroyOnLoad(root);

        barsCanvas = root.AddComponent<Canvas>();
        barsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        barsCanvas.sortingOrder = BarsSortingOrder;

        leftBar = CreateBar(root.transform, "LeftBar");
        rightBar = CreateBar(root.transform, "RightBar");
        topBar = CreateBar(root.transform, "TopBar");
        bottomBar = CreateBar(root.transform, "BottomBar");
    }

    private static RectTransform CreateBar(Transform parent, string name)
    {
        GameObject barObject = new GameObject(name, typeof(RectTransform));
        barObject.layer = UILayer;
        barObject.transform.SetParent(parent, false);

        Image image = barObject.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        return barObject.GetComponent<RectTransform>();
    }

    private void LateUpdate()
    {
        ApplyCameraSettings();
        UpdateBars();
        ApplyToCanvases();
    }

    private static Rect GetViewportRect()
    {
        float screenWidth = Mathf.Max(1, Screen.width);
        float screenHeight = Mathf.Max(1, Screen.height);
        float screenAspect = screenWidth / screenHeight;

        float width = 1f;
        float height = 1f;

        if (screenAspect > TargetAspect)
        {
            // 屏幕比 16:9 更宽，左右加黑边。
            width = TargetAspect / screenAspect;
        }
        else if (screenAspect < TargetAspect)
        {
            // 屏幕比 16:9 更高，上下加黑边。
            height = screenAspect / TargetAspect;
        }

        return new Rect((1f - width) * 0.5f, (1f - height) * 0.5f, width, height);
    }

    private void ApplyCameraSettings()
    {
        if (uiCamera != null)
            uiCamera.rect = GetViewportRect();

        float screenWidth = Mathf.Max(1, Screen.width);
        float screenHeight = Mathf.Max(1, Screen.height);
        float screenAspect = screenWidth / screenHeight;

        // 屏幕比 16:9 更高时扩大纵向视野，中心 16:9 区域
        // 才能显示和 16:9 屏幕一致的画面，多出的部分由黑边盖住。
        float verticalScale = screenAspect < TargetAspect ? TargetAspect / screenAspect : 1f;

        Camera[] cameras = Camera.allCameras;

        foreach (Camera camera in cameras)
        {
            if (camera == null || camera == uiCamera || camera == backgroundCamera) continue;
            if (camera.targetTexture != null) continue;

            // 保持全屏，避免 Postprocessing v2 在分块 GPU 上
            // 因相机 viewport 不完整而报警或产生画面瑕疵。
            camera.rect = new Rect(0f, 0f, 1f, 1f);

            if (camera.orthographic)
            {
                float baseSize;
                if (!baseOrthographicSizes.TryGetValue(camera, out baseSize))
                {
                    baseSize = camera.orthographicSize;
                    baseOrthographicSizes[camera] = baseSize;
                }

                camera.orthographicSize = baseSize * verticalScale;
            }
        }

        CleanupCameraCache();
    }

    private void CleanupCameraCache()
    {
        if (baseOrthographicSizes.Count == 0) return;

        cleanupBuffer.Clear();
        foreach (KeyValuePair<Camera, float> pair in baseOrthographicSizes)
        {
            if (pair.Key == null)
                cleanupBuffer.Add(pair.Key);
        }

        foreach (Camera camera in cleanupBuffer)
            baseOrthographicSizes.Remove(camera);
    }

    private void UpdateBars()
    {
        if (barsCanvas == null) return;

        Rect viewport = GetViewportRect();
        float barWidth = Screen.width * (1f - viewport.width) * 0.5f;
        float barHeight = Screen.height * (1f - viewport.height) * 0.5f;

        SetBar(leftBar, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
            new Vector2(barWidth, 0f));
        SetBar(rightBar, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            new Vector2(barWidth, 0f));
        SetBar(bottomBar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, barHeight));
        SetBar(topBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, barHeight));
    }

    private static void SetBar(RectTransform bar, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 sizeDelta)
    {
        if (bar == null) return;

        bar.anchorMin = anchorMin;
        bar.anchorMax = anchorMax;
        bar.pivot = pivot;
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta = sizeDelta;
    }

    private void ApplyToCanvases()
    {
        convertedCanvases.RemoveWhere(canvas => canvas == null);

        Canvas[] canvases = FindObjectsOfType<Canvas>(true);

        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || canvas == barsCanvas) continue;
            if (!canvas.isRootCanvas) continue;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (convertedCanvases.Contains(canvas)) continue;

            // 运行时动态创建的 UI 默认在 Default 层，统一改到 UI 层，
            // 否则只渲染 UI 层的 LetterboxUICamera 会看不到它们。
            SetLayerRecursively(canvas.transform, UILayer);

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 10f;

            convertedCanvases.Add(canvas);
        }
    }

    private static void SetLayerRecursively(Transform target, int layer)
    {
        target.gameObject.layer = layer;

        for (int i = 0; i < target.childCount; i++)
        {
            SetLayerRecursively(target.GetChild(i), layer);
        }
    }
}
