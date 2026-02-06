using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using System.Collections;

public class ScreenEffectManager : MonoBehaviour
{
    public static ScreenEffectManager Instance { get; private set; }

    [Header("相机引用")]
    [SerializeField] private Camera mainCamera;

    [Header("后期处理引用")]
    [SerializeField] private PostProcessVolume postProcessVolume;

    // 后期处理效果
    private Bloom bloom;
    private Vignette vignette;
    private ColorGrading colorGrading;
    private ChromaticAberration chromaticAberration;

    [Header("震动设置")]
    [SerializeField] private float defaultShakeDuration = 0.3f;
    [SerializeField] private float defaultShakeMagnitude = 0.15f;

    [Header("闪光设置")]
    [SerializeField] private float defaultFlashDuration = 0.2f;
    [SerializeField] private AnimationCurve flashCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    // 震动相关
    private Vector3 originalCameraPosition;
    private bool isShaking = false;
    private Coroutine shakeCoroutine;

    void Awake()
    {
        // 单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        Initialize();
    }

    void Initialize()
    {
        // 获取主相机
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("找不到主相机！");
                return;
            }
        }

        originalCameraPosition = mainCamera.transform.localPosition;

        // 初始化后期处理
        InitializePostProcessing();
    }

    void InitializePostProcessing()
    {
        // 确保有PostProcessVolume
        if (postProcessVolume == null)
        {
            // 尝试从相机获取
            postProcessVolume = mainCamera.GetComponent<PostProcessVolume>();

            // 如果还没有，创建新的
            if (postProcessVolume == null)
            {
                GameObject volumeObj = new GameObject("PostProcessVolume");
                volumeObj.transform.SetParent(mainCamera.transform);
                volumeObj.transform.localPosition = Vector3.zero;
                postProcessVolume = volumeObj.AddComponent<PostProcessVolume>();
                postProcessVolume.isGlobal = true;
                postProcessVolume.priority = 100;
            }
        }

        // 创建或获取后期处理Profile
        if (postProcessVolume.profile == null)
        {
            postProcessVolume.profile = ScriptableObject.CreateInstance<PostProcessProfile>();
        }

        // 获取或创建Bloom效果（用于闪光）
        if (!postProcessVolume.profile.TryGetSettings(out bloom))
        {
            bloom = postProcessVolume.profile.AddSettings<Bloom>();
            bloom.enabled.Override(false);
            bloom.intensity.Override(0f);
            bloom.threshold.Override(1f);
            bloom.softKnee.Override(0.5f);
        }

        // 获取或创建Vignette效果（用于边缘闪光）
        if (!postProcessVolume.profile.TryGetSettings(out vignette))
        {
            vignette = postProcessVolume.profile.AddSettings<Vignette>();
            vignette.enabled.Override(false);
            vignette.intensity.Override(0f);
            vignette.color.Override(Color.white);
            vignette.smoothness.Override(0.2f);
        }

        // 获取或创建ColorGrading效果（用于色调变化）
        if (!postProcessVolume.profile.TryGetSettings(out colorGrading))
        {
            colorGrading = postProcessVolume.profile.AddSettings<ColorGrading>();
            colorGrading.enabled.Override(false);
        }

        // 获取或创建ChromaticAberration效果（用于色差）
        if (!postProcessVolume.profile.TryGetSettings(out chromaticAberration))
        {
            chromaticAberration = postProcessVolume.profile.AddSettings<ChromaticAberration>();
            chromaticAberration.enabled.Override(false);
            chromaticAberration.intensity.Override(0f);
        }
    }

    #region 屏幕震动方法

    public void ShakeCamera(float duration = 0.3f, float magnitude = 0.15f)
    {
        if (isShaking && shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }

        shakeCoroutine = StartCoroutine(ShakeCoroutine(duration, magnitude));
    }

    IEnumerator ShakeCoroutine(float duration, float magnitude)
    {
        isShaking = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // 计算衰减
            float damping = 1f - (elapsed / duration);

            // 随机偏移
            float x = Random.Range(-1f, 1f) * magnitude * damping;
            float y = Random.Range(-1f, 1f) * magnitude * damping;

            // 应用偏移
            mainCamera.transform.localPosition = originalCameraPosition + new Vector3(x, y, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 恢复原位置
        mainCamera.transform.localPosition = originalCameraPosition;
        isShaking = false;
    }

    #endregion

    #region 屏幕闪光方法

    public void FlashScreen(Color color, float duration = 0.2f, float intensity = 10f)
    {
        StartCoroutine(FlashCoroutine(color, duration, intensity));
    }

    IEnumerator FlashCoroutine(Color color, float duration, float maxIntensity)
    {
        bloom.enabled.Override(true);
        bloom.color.Override(color);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float curveValue = flashCurve.Evaluate(t);

            // 应用曲线控制强度
            bloom.intensity.Override(curveValue * maxIntensity);

            elapsed += Time.deltaTime;
            yield return null;
        }

        bloom.intensity.Override(0f);
        bloom.enabled.Override(false);
    }

    #endregion

    #region 边缘闪光方法

    public void EdgeFlash(Color color, float duration = 0.3f, float maxIntensity = 0.5f)
    {
        StartCoroutine(EdgeFlashCoroutine(color, duration, maxIntensity));
    }

    IEnumerator EdgeFlashCoroutine(Color color, float duration, float maxIntensity)
    {
        vignette.enabled.Override(true);
        vignette.color.Override(color);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float curveValue = flashCurve.Evaluate(t);

            vignette.intensity.Override(curveValue * maxIntensity);

            elapsed += Time.deltaTime;
            yield return null;
        }

        vignette.intensity.Override(0f);
        vignette.enabled.Override(false);
    }

    #endregion

    #region 时间慢放方法

    public void TimeSlow(float timeScale, float duration)
    {
        StartCoroutine(TimeSlowCoroutine(timeScale, duration));
    }

    IEnumerator TimeSlowCoroutine(float targetTimeScale, float duration)
    {
        float originalTimeScale = Time.timeScale;
        Time.timeScale = targetTimeScale;

        // 同时应用色差效果增强慢动作感
        if (chromaticAberration != null)
        {
            chromaticAberration.enabled.Override(true);
            chromaticAberration.intensity.Override(0.5f);
        }

        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = originalTimeScale;

        // 恢复色差
        if (chromaticAberration != null)
        {
            chromaticAberration.intensity.Override(0f);
            chromaticAberration.enabled.Override(false);
        }
    }

    #endregion

    #region 组合效果

    public void PlayReleaseEffects(int layers)
    {
        // 计算强度
        float intensity = Mathf.Clamp01(layers / 30f);

        // 震动
        float shakeDuration = defaultShakeDuration * (1f + intensity);
        float shakeMagnitude = defaultShakeMagnitude * (1f + intensity);
        ShakeCamera(shakeDuration, shakeMagnitude);

        // 闪光
        Color flashColor = new Color(0.8f, 0.3f, 1f, 1f); // 紫色
        float flashIntensity = 10f * (1f + intensity);
        FlashScreen(flashColor, defaultFlashDuration * (1f + intensity), flashIntensity);

        // 高层数额外效果
        if (layers >= 10)
        {
            // 边缘闪光
            Color edgeColor = new Color(1f, 0.6f, 0.2f, 1f); // 橙色
            EdgeFlash(edgeColor, 0.3f + (intensity * 0.2f), 0.3f + intensity);

            // 时间慢放
            if (layers >= 15)
            {
                float slowFactor = Mathf.Lerp(0.5f, 0.2f, Mathf.Clamp01((layers - 15) / 15f));
                TimeSlow(slowFactor, 0.3f + (intensity * 0.2f));
            }
        }
    }

    #endregion

    void OnDestroy()
    {
        // 确保重置所有效果
        if (bloom != null) bloom.enabled.Override(false);
        if (vignette != null) vignette.enabled.Override(false);
        if (chromaticAberration != null) chromaticAberration.enabled.Override(false);
        if (colorGrading != null) colorGrading.enabled.Override(false);

        // 恢复时间尺度
        Time.timeScale = 1f;
    }
}