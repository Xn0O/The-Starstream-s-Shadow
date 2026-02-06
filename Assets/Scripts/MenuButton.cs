using UnityEngine;
using System.Collections;

public class MenuButton : MonoBehaviour
{
    [Header("菜单GameObjects")]
    public GameObject startOFF;
    public GameObject startON;
    public GameObject quitOFF;
    public GameObject quitON;

    [Header("转场效果")]
    public GameObject transitionObject;
    public float transitionDuration = 1.0f;

    [Header("输入设置")]
    public KeyCode[] confirmKeys = { KeyCode.Return, KeyCode.Space };
    public KeyCode[] upKeys = { KeyCode.UpArrow, KeyCode.W };
    public KeyCode[] downKeys = { KeyCode.DownArrow, KeyCode.S };

    [Header("初始状态")]
    public bool startSelected = true; // 初始选中开始

    [Header("音频")]
    public AudioClip selectSound;
    public AudioClip confirmSound;
    public AudioSource audioSource;

    private bool isTransitioning = false;
    private bool canInput = true;

    void Start()
    {
        // 初始化UI状态
        UpdateMenuSelection();

        // 确保转场对象初始隐藏
        if (transitionObject)
        {
            transitionObject.SetActive(false);
        }

        // 隐藏所有GameObject，避免意外显示
        ValidateGameObjects();
    }

    void ValidateGameObjects()
    {
        if (startOFF == null) Debug.LogError("startOFF GameObject未分配！");
        if (startON == null) Debug.LogError("startON GameObject未分配！");
        if (quitOFF == null) Debug.LogError("quitOFF GameObject未分配！");
        if (quitON == null) Debug.LogError("quitON GameObject未分配！");
    }

    void Update()
    {
        if (!canInput || isTransitioning) return;

        HandleInput();
    }

    void HandleInput()
    {
        // 上下选择 - 支持多个按键
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

        // 确认键 - 支持多个按键
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

    void SelectStart()
    {
        if (!startSelected)
        {
            startSelected = true;
            UpdateMenuSelection();
            PlaySound(selectSound);
        }
    }

    void SelectQuit()
    {
        if (startSelected)
        {
            startSelected = false;
            UpdateMenuSelection();
            PlaySound(selectSound);
        }
    }

    void UpdateMenuSelection()
    {
        // 更新开始按钮状态
        if (startOFF != null && startON != null)
        {
            //startOFF.SetActive(!startSelected);  // 非选中时显示OFF
            startON.SetActive(startSelected);     // 选中时显示ON
        }

        // 更新退出按钮状态
        if (quitOFF != null && quitON != null)
        {
            //quitOFF.SetActive(startSelected);     // 非选中时显示OFF（开始选中时退出显示OFF）
            quitON.SetActive(!startSelected);     // 选中时显示ON（开始未选中时退出显示ON）
        }
    }

    void ConfirmSelection()
    {
        PlaySound(confirmSound);

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

        // 显示转场效果
        if (transitionObject)
        {
            transitionObject.SetActive(true);

            // 可以添加转场动画
            //Animator animator = transitionObject.GetComponent<Animator>();
            //if (animator)
            //{
            //    animator.SetTrigger("Start");
            //}

            // 或者使用协程控制转场时间
            //StartCoroutine(TransitionToGame());
        }
        else
        {
            // 如果没有转场对象，直接加载游戏
            Debug.Log("开始游戏");
            // 这里可以添加加载场景的代码
            // SceneManager.LoadScene("GameScene");
        }
    }

    void QuitGame()
    {
        if (isTransitioning) return;

        isTransitioning = true;
        canInput = false;

        Debug.Log("退出游戏");

        // 如果是编辑器模式
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator TransitionToGame()
    {
        // 等待转场动画完成
        yield return new WaitForSeconds(transitionDuration);

        // 这里添加实际加载游戏的代码
        Debug.Log("转场完成，开始游戏");

        // 例如：SceneManager.LoadScene("GameScene");
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource && clip)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // 可选：添加鼠标支持（需要为GameObject添加Collider）
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

    // 重置菜单状态（可用于从其他场景返回）
    public void ResetMenu()
    {
        isTransitioning = false;
        canInput = true;
        startSelected = true;
        UpdateMenuSelection();
    }

    // 调试方法：在编辑器中测试
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

    // 检查按键是否被按下（辅助方法）
    private bool IsAnyKeyDown(KeyCode[] keys)
    {
        foreach (KeyCode key in keys)
        {
            if (Input.GetKeyDown(key))
                return true;
        }
        return false;
    }
}