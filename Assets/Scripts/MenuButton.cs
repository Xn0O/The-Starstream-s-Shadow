using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;

public class MenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("菜单GameObjects")]
    public GameObject startOFF;
    public GameObject startON;
    public GameObject quitOFF;
    public GameObject quitON;

    [Header("鼠标检测区域")]
    public RectTransform startButtonArea;  // 开始按钮的检测区域
    public RectTransform quitButtonArea;   // 退出按钮的检测区域

    [Header("转场效果")]
    public GameObject transitionObject;
    public float transitionDuration = 1.0f;

    [Header("输入设置")]
    public KeyCode[] confirmKeys = { KeyCode.Return, KeyCode.Space };
    public KeyCode[] upKeys = { KeyCode.UpArrow, KeyCode.W };
    public KeyCode[] downKeys = { KeyCode.DownArrow, KeyCode.S };

    [Header("初始状态")]
    public bool startSelected = true; // 初始选中开始

    [Header("音频设置")]
    [SerializeField] private AudioClip selectSound;
    [SerializeField] private AudioClip confirmSound;
    [SerializeField] private AudioSource audioSource;

    [Header("音频参数")]
    [Range(0f, 1f)] public float selectSoundVolume = 0.8f;
    [Range(0f, 1f)] public float confirmSoundVolume = 1.0f;
    [Range(0.1f, 3f)] public float selectSoundPitch = 1.0f;
    [Range(0.1f, 3f)] public float confirmSoundPitch = 1.0f;
    [Range(0f, 1f)] public float masterVolume = 1.0f; // 主音量控制

    private bool isTransitioning = false;
    private bool canInput = true;

    // 鼠标检测相关变量
    private bool isMouseOverStart = false;
    private bool isMouseOverQuit = false;
    private Camera uiCamera;

    void Start()
    {
        // 初始化UI状态
        UpdateMenuSelection();

        // 确保转场对象初始隐藏
        if (transitionObject)
        {
            transitionObject.SetActive(false);
        }

        // 获取UI相机
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (uiCamera == null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = Camera.main;
            }
        }

        // 隐藏所有GameObject，避免意外显示
        ValidateGameObjects();

        // 初始化鼠标状态
        isMouseOverStart = false;
        isMouseOverQuit = false;

        // 初始化音频源设置
        SetupAudioSource();
    }

    void ValidateGameObjects()
    {
        if (startOFF == null) Debug.LogWarning("startOFF GameObject未分配！");
        if (startON == null) Debug.LogWarning("startON GameObject未分配！");
        if (quitOFF == null) Debug.LogWarning("quitOFF GameObject未分配！");
        if (quitON == null) Debug.LogWarning("quitON GameObject未分配！");

        if (startButtonArea == null) Debug.LogWarning("startButtonArea未分配！");
        if (quitButtonArea == null) Debug.LogWarning("quitButtonArea未分配！");
    }

    void SetupAudioSource()
    {
        // 如果没有音频源，尝试获取或创建
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // 设置音频源初始参数
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D声音
    }

    void Update()
    {
        if (!canInput || isTransitioning) return;

        HandleInput();
        HandleMouseDetection();
    }

    void HandleInput()
    {
        // 上下选择
        bool upPressed = false;
        bool downPressed = false;

        foreach (KeyCode key in upKeys)
        {
            if (Input.GetKeyDown(key))
            {
                upPressed = true;
                break;
            }
        }

        foreach (KeyCode key in downKeys)
        {
            if (Input.GetKeyDown(key))
            {
                downPressed = true;
                break;
            }
        }

        if (upPressed)
        {
            SelectStart();
        }
        else if (downPressed)
        {
            SelectQuit();
        }

        // 确认键
        bool confirmPressed = false;
        foreach (KeyCode key in confirmKeys)
        {
            if (Input.GetKeyDown(key))
            {
                confirmPressed = true;
                break;
            }
        }

        if (confirmPressed)
        {
            ConfirmSelection();
        }
    }

    void HandleMouseDetection()
    {
        if (startButtonArea == null || quitButtonArea == null)
        {
            HandleMouseDetectionFallback();
            return;
        }

        Vector2 mousePosition = Input.mousePosition;

        bool wasOverStart = isMouseOverStart;
        bool wasOverQuit = isMouseOverQuit;

        isMouseOverStart = RectTransformUtility.RectangleContainsScreenPoint(startButtonArea, mousePosition, uiCamera);
        isMouseOverQuit = RectTransformUtility.RectangleContainsScreenPoint(quitButtonArea, mousePosition, uiCamera);

        if (isMouseOverStart && isMouseOverQuit)
        {
            isMouseOverQuit = false;
        }

        // 鼠标进入开始区域
        if (isMouseOverStart && !wasOverStart)
        {
            if (!startSelected)
            {
                SelectStart();
            }
        }
        // 鼠标进入退出区域
        else if (isMouseOverQuit && !wasOverQuit)
        {
            if (startSelected)
            {
                SelectQuit();
            }
        }

        // 检测鼠标点击
        if (Input.GetMouseButtonDown(0))
        {
            if (isMouseOverStart)
            {
                ConfirmSelection();
            }
            else if (isMouseOverQuit)
            {
                if (startSelected)
                {
                    startSelected = false;
                    UpdateMenuSelection();
                    PlaySound(selectSound, selectSoundVolume, selectSoundPitch);
                }
                ConfirmSelection();
            }
        }
    }

    void HandleMouseDetectionFallback()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        RaycastHit2D hit2D = Physics2D.Raycast(ray.origin, ray.direction);

        if (hit2D.collider != null)
        {
            GameObject hitObject = hit2D.collider.gameObject;
            CheckHitObject(hitObject);
        }
        else if (Physics.Raycast(ray, out hit))
        {
            GameObject hitObject = hit.collider.gameObject;
            CheckHitObject(hitObject);
        }
        else
        {
            isMouseOverStart = false;
            isMouseOverQuit = false;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (isMouseOverStart)
            {
                ConfirmSelection();
            }
            else if (isMouseOverQuit)
            {
                if (startSelected)
                {
                    startSelected = false;
                    UpdateMenuSelection();
                    PlaySound(selectSound, selectSoundVolume, selectSoundPitch);
                }
                ConfirmSelection();
            }
        }
    }

    void CheckHitObject(GameObject hitObject)
    {
        if (hitObject == startOFF || hitObject == startON ||
            (hitObject.transform.IsChildOf(startOFF != null ? startOFF.transform : null)) ||
            (hitObject.transform.IsChildOf(startON != null ? startON.transform : null)))
        {
            isMouseOverStart = true;
            isMouseOverQuit = false;
            if (!startSelected)
            {
                SelectStart();
            }
        }
        else if (hitObject == quitOFF || hitObject == quitON ||
                 (hitObject.transform.IsChildOf(quitOFF != null ? quitOFF.transform : null)) ||
                 (hitObject.transform.IsChildOf(quitON != null ? quitON.transform : null)))
        {
            isMouseOverStart = false;
            isMouseOverQuit = true;
            if (startSelected)
            {
                SelectQuit();
            }
        }
        else
        {
            isMouseOverStart = false;
            isMouseOverQuit = false;
        }
    }

    void SelectStart()
    {
        if (!startSelected)
        {
            startSelected = true;
            UpdateMenuSelection();
            PlaySound(selectSound, selectSoundVolume, selectSoundPitch);
        }
    }

    void SelectQuit()
    {
        if (startSelected)
        {
            startSelected = false;
            UpdateMenuSelection();
            PlaySound(selectSound, selectSoundVolume, selectSoundPitch);
        }
    }

    void UpdateMenuSelection()
    {
        // 更新开始按钮状态
        if (startOFF != null && startON != null)
        {
            startOFF.SetActive(!startSelected);
            startON.SetActive(startSelected);
        }

        // 更新退出按钮状态
        if (quitOFF != null && quitON != null)
        {
            quitOFF.SetActive(startSelected);
            quitON.SetActive(!startSelected);
        }
    }

    void ConfirmSelection()
    {
        PlaySound(confirmSound, confirmSoundVolume, confirmSoundPitch);

        if (startSelected)
        {
            StartGame();
        }
        else
        {
            QuitGame();
        }
    }

    void StartGame()
    {
        if (isTransitioning) return;

        isTransitioning = true;
        canInput = false;

        if (transitionObject)
        {
            transitionObject.SetActive(true);
        }
        else
        {
            Debug.Log("开始游戏");
        }
    }

    void QuitGame()
    {
        if (isTransitioning) return;

        isTransitioning = true;
        canInput = false;

        Debug.Log("退出游戏");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator TransitionToGame()
    {
        yield return new WaitForSeconds(transitionDuration);
        Debug.Log("转场完成，开始游戏");
    }

    void PlaySound(AudioClip clip, float volume = 1.0f, float pitch = 1.0f)
    {
        if (audioSource && clip)
        {
            // 应用主音量控制
            float finalVolume = Mathf.Clamp01(volume * masterVolume);

            // 设置音调
            audioSource.pitch = Mathf.Clamp(pitch, 0.1f, 3f);

            // 播放声音
            audioSource.PlayOneShot(clip, finalVolume);
        }
    }

    // 音频控制方法 - 可以在运行时动态调整
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
    }

    public void SetSelectSoundVolume(float volume)
    {
        selectSoundVolume = Mathf.Clamp01(volume);
    }

    public void SetConfirmSoundVolume(float volume)
    {
        confirmSoundVolume = Mathf.Clamp01(volume);
    }

    public void SetSelectSoundPitch(float pitch)
    {
        selectSoundPitch = Mathf.Clamp(pitch, 0.1f, 3f);
    }

    public void SetConfirmSoundPitch(float pitch)
    {
        confirmSoundPitch = Mathf.Clamp(pitch, 0.1f, 3f);
    }

    public void PlayTestSound(AudioClip clip, float volume = 1.0f, float pitch = 1.0f)
    {
        PlaySound(clip, volume, pitch);
    }

    // 保留原来的UI事件接口
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 由HandleMouseDetection处理
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 由HandleMouseDetection处理
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 由HandleMouseDetection处理
    }

    // 可选：添加鼠标支持
    public void OnStartButtonHover()
    {
        if (!startSelected && canInput)
        {
            SelectStart();
        }
    }

    public void OnQuitButtonHover()
    {
        if (startSelected && canInput)
        {
            SelectQuit();
        }
    }

    public void OnStartButtonClick()
    {
        if (canInput)
        {
            startSelected = true;
            UpdateMenuSelection();
            ConfirmSelection();
        }
    }

    public void OnQuitButtonClick()
    {
        if (canInput)
        {
            startSelected = false;
            UpdateMenuSelection();
            ConfirmSelection();
        }
    }

    // 重置菜单状态
    public void ResetMenu()
    {
        isTransitioning = false;
        canInput = true;
        startSelected = true;
        isMouseOverStart = false;
        isMouseOverQuit = false;
        UpdateMenuSelection();
    }

    // 调试方法
    [ContextMenu("测试选中开始")]
    void TestSelectStart()
    {
        SelectStart();
    }

    [ContextMenu("测试选中退出")]
    void TestSelectQuit()
    {
        SelectQuit();
    }

    [ContextMenu("测试选择音效")]
    void TestSelectSound()
    {
        PlaySound(selectSound, selectSoundVolume, selectSoundPitch);
    }

    [ContextMenu("测试确认音效")]
    void TestConfirmSound()
    {
        PlaySound(confirmSound, confirmSoundVolume, confirmSoundPitch);
    }

    // 调试：绘制检测区域
    void OnDrawGizmosSelected()
    {
        if (startButtonArea != null)
        {
            DrawRectGizmo(startButtonArea, Color.green);
        }

        if (quitButtonArea != null)
        {
            DrawRectGizmo(quitButtonArea, Color.red);
        }
    }

    void DrawRectGizmo(RectTransform rectTransform, Color color)
    {
        Gizmos.color = color;

        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        Gizmos.DrawLine(corners[0], corners[1]);
        Gizmos.DrawLine(corners[1], corners[2]);
        Gizmos.DrawLine(corners[2], corners[3]);
        Gizmos.DrawLine(corners[3], corners[0]);
    }
}