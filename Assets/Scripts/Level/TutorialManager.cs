using UnityEngine;
using UnityEngine.UI;
// 如果用TextMeshPro，取消下面注释
// using TMPro;

public class TutorialManager : MonoBehaviour
{
    [Header("教程文本配置")]
    public string[] tutorialPages; // 每页教程文字
    public Text tutorialContentText; // 文字显示组件（用TMP则改TextMeshProUGUI）
    public Text pageIndicatorText; // 页码显示组件

    [Header("教程图片/动画配置")]
    public Image tutorialImage; // 图片显示组件
    public Sprite[] tutorialSprites; // 每页对应的静态图片（和教程页数一致）
    public Animator tutorialAnimator; // 序列帧动画的Animator（可选，二选一）
    public string[] animationNames; // 每页对应的动画名称（和教程页数一致）

    [Header("角色控制")]
    public MonoBehaviour playerController; // 角色控制脚本

    private int currentPageIndex = 0;
    private bool isTutorialFinished = false;

    void Start()
    {
        // 初始化：禁用角色控制，显示第一页教程（文字+图片/动画）
        if (playerController != null)
        {
            playerController.enabled = false;
        }
        // 隐藏动画（如果不用），避免初始混乱
        if (tutorialAnimator != null)
        {
            tutorialAnimator.enabled = false;
        }
        UpdateTutorialUI();
    }

    void Update()
    {
        if (!isTutorialFinished)
        {
            // 空格/回车/鼠标左键翻页
            if (Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetMouseButtonDown(0))
            {
                NextPage();
            }
        }
    }

    /// <summary>
    /// 翻到下一页
    /// </summary>
    void NextPage()
    {
        if (currentPageIndex < tutorialPages.Length - 1)
        {
            currentPageIndex++;
            UpdateTutorialUI();
        }
        else
        {
            FinishTutorial();
        }
    }

    /// <summary>
    /// 更新教程UI（文字+图片/动画）
    /// </summary>
    void UpdateTutorialUI()
    {
        // 1. 更新文字和页码
        tutorialContentText.text = tutorialPages[currentPageIndex];
        pageIndicatorText.text = $"第 {currentPageIndex + 1} / {tutorialPages.Length} 页";

        // 2. 更新静态图片（二选一：优先图片，再动画）
        if (tutorialImage != null && tutorialSprites != null && tutorialSprites.Length > currentPageIndex)
        {
            // 显示图片时关闭动画
            if (tutorialAnimator != null) tutorialAnimator.enabled = false;
            tutorialImage.enabled = true;
            tutorialImage.sprite = tutorialSprites[currentPageIndex];
        }
        // 3. 更新序列帧动画（二选一）
        else if (tutorialAnimator != null && animationNames != null && animationNames.Length > currentPageIndex)
        {
            // 显示动画时隐藏图片
            if (tutorialImage != null) tutorialImage.enabled = false;
            tutorialAnimator.enabled = true;
            tutorialAnimator.Play(animationNames[currentPageIndex]); // 播放对应页的动画
        }
    }

    /// <summary>
    /// 完成教程，解锁角色
    /// </summary>
    void FinishTutorial()
    {
        isTutorialFinished = true;
        if (playerController != null)
        {
            playerController.enabled = true;
        }
        gameObject.SetActive(false);
    }

    // 按钮翻页备用
    public void OnClickNextButton()
    {
        NextPage();
    }
}