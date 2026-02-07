using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DialogueManager : MonoBehaviour
{
    [System.Serializable]
    public class DialoguePage
    {
        [TextArea(3, 10)]
        public string text;

        public GameObject[] showObjects;
        public GameObject[] hideObjects;

        [Header("打字机设置")]
        public bool useTypewriter = true;
        [Range(0, 100)]
        public float typingSpeed = 30f;
        public AudioClip typingSound;
        public AudioClip pageSound;
    }

    [Header("UI组件")]
    public Text dialogueText;
    public GameObject dialoguePanel;
    public Text pageCounterText;
    public GameObject continueIndicator;

    [Header("对白内容")]
    public List<DialoguePage> dialoguePages = new List<DialoguePage>();

    [Header("控制设置")]
    public KeyCode nextKey = KeyCode.Space;
    public KeyCode altNextKey = KeyCode.Return;
    public KeyCode prevKey = KeyCode.A;
    public KeyCode altPrevKey = KeyCode.LeftArrow;
    public KeyCode skipTypingKey = KeyCode.S;

    [Header("效果设置")]
    public bool autoStartTyping = true;
    public bool playPageSound = true;
    public float fadeDuration = 0.3f;

    private int currentPageIndex = 0;
    private bool isTransitioning = false;
    private bool isTyping = false;
    private Coroutine typingCoroutine;
    private Coroutine transitionCoroutine;
    private AudioSource audioSource;

    void Start()
    {
        // 获取或添加AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 初始化显示
        if (dialoguePages.Count > 0)
        {
            ShowPage(0);
        }
        else
        {
            Debug.LogWarning("没有添加任何对白页面！");
        }

        // 隐藏继续提示
        if (continueIndicator != null)
        {
            continueIndicator.SetActive(false);
        }
    }

    void Update()
    {
        if (isTransitioning) return;

        // 检测跳过打字
        if (isTyping && Input.GetKeyDown(skipTypingKey))
        {
            SkipTyping();
            return;
        }

        // 检测翻页（只在打字完成时）
        if (!isTyping)
        {
            if (Input.GetKeyDown(nextKey) || Input.GetKeyDown(altNextKey))
            {
                if (currentPageIndex < dialoguePages.Count - 1)
                {
                    NextPage();
                }
                else
                {
                    OnDialogueEnd();
                }
            }
            else if (Input.GetKeyDown(prevKey) || Input.GetKeyDown(altPrevKey))
            {
                if (currentPageIndex > 0)
                {
                    PreviousPage();
                }
            }
        }

        // 更新继续提示
        if (continueIndicator != null)
        {
            continueIndicator.SetActive(!isTyping && dialoguePages.Count > 0);
        }
    }

    void NextPage()
    {
        if (currentPageIndex >= dialoguePages.Count - 1) return;

        // 如果已经在过渡中，不重复开始
        if (isTransitioning) return;

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }
        transitionCoroutine = StartCoroutine(TransitionToPage(currentPageIndex + 1));
    }

    void PreviousPage()
    {
        if (currentPageIndex <= 0) return;

        if (isTransitioning) return;

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }
        transitionCoroutine = StartCoroutine(TransitionToPage(currentPageIndex - 1));
    }

    IEnumerator TransitionToPage(int newPageIndex)
    {
        isTransitioning = true;

        // 停止正在进行的打字
        if (isTyping && typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            isTyping = false;
        }

        // 淡出效果
        if (fadeDuration > 0 && dialoguePanel != null)
        {
            yield return StartCoroutine(FadePanel(0f));
        }
        else
        {
            // 确保协程至少执行一次
            yield return null;
        }

        // 隐藏当前页对象
        DialoguePage currentPage = dialoguePages[currentPageIndex];
        if (currentPage.showObjects != null)
        {
            foreach (GameObject obj in currentPage.showObjects)
            {
                if (obj != null)
                    obj.SetActive(false);
            }
        }

        // 显示新页面
        ShowPage(newPageIndex);

        // 淡入效果
        if (fadeDuration > 0 && dialoguePanel != null)
        {
            yield return StartCoroutine(FadePanel(1f));
        }

        isTransitioning = false;
        transitionCoroutine = null;
    }

    IEnumerator FadePanel(float targetAlpha)
    {
        CanvasGroup canvasGroup = dialoguePanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = dialoguePanel.AddComponent<CanvasGroup>();
        }

        float startAlpha = canvasGroup.alpha;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeDuration;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    void ShowPage(int pageIndex)
    {
        currentPageIndex = pageIndex;
        DialoguePage page = dialoguePages[pageIndex];

        Debug.Log($"显示第 {pageIndex + 1} 页，使用打字机: {page.useTypewriter}，速度: {page.typingSpeed}");

        // 清空文本
        if (dialogueText != null)
        {
            dialogueText.text = "";
        }

        // 更新页码
        if (pageCounterText != null)
        {
            pageCounterText.text = $"{pageIndex + 1}/{dialoguePages.Count}";
        }

        // 播放页面音效
        if (playPageSound && page.pageSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(page.pageSound);
        }

        // 处理GameObjects
        if (page.hideObjects != null)
        {
            foreach (GameObject obj in page.hideObjects)
            {
                if (obj != null)
                    obj.SetActive(false);
            }
        }

        if (page.showObjects != null)
        {
            foreach (GameObject obj in page.showObjects)
            {
                if (obj != null)
                    obj.SetActive(true);
            }
        }

        // 开始打字机效果
        if (page.useTypewriter && !string.IsNullOrEmpty(page.text) && dialogueText != null)
        {
            if (autoStartTyping)
            {
                isTyping = true;
                typingCoroutine = StartCoroutine(TypeTextCoroutine(page.text, page.typingSpeed, page.typingSound));
            }
        }
        else
        {
            // 直接显示文本
            if (dialogueText != null)
            {
                dialogueText.text = page.text;
            }
            isTyping = false;
        }
    }

    IEnumerator TypeTextCoroutine(string fullText, float typingSpeed, AudioClip typingSoundClip)
    {
        Debug.Log($"开始打字: {fullText.Substring(0, Mathf.Min(20, fullText.Length))}...");

        // 确保有合理的打字速度
        if (typingSpeed <= 0)
            typingSpeed = 30f;

        float delayPerChar = 1f / typingSpeed;

        // 逐字显示
        for (int i = 0; i <= fullText.Length; i++)
        {
            if (!isTyping)
                break; // 被跳过

            if (dialogueText != null)
            {
                dialogueText.text = fullText.Substring(0, i);
            }

            // 播放打字音效
            if (typingSoundClip != null && audioSource != null && i < fullText.Length)
            {
                char c = fullText[i];
                if (c != ' ' && c != '\n' && c != '\t')
                {
                    audioSource.PlayOneShot(typingSoundClip, 0.1f);
                }
            }

            // 延迟
            if (i < fullText.Length)
            {
                float extraDelay = 0f;
                char currentChar = fullText[i];

                // 标点符号额外延迟
                if (",;。，".IndexOf(currentChar) >= 0)
                    extraDelay = delayPerChar * 2f;
                else if (".!?！？".IndexOf(currentChar) >= 0)
                    extraDelay = delayPerChar * 3f;
                else if (currentChar == '\n')
                    extraDelay = delayPerChar * 1.5f;

                yield return new WaitForSeconds(delayPerChar + extraDelay);
            }
        }

        // 完成
        if (dialogueText != null)
        {
            dialogueText.text = fullText;
        }
        isTyping = false;
        Debug.Log("打字完成");
    }

    void SkipTyping()
    {
        if (!isTyping || typingCoroutine == null)
            return;

        StopCoroutine(typingCoroutine);

        DialoguePage page = dialoguePages[currentPageIndex];
        if (dialogueText != null)
        {
            dialogueText.text = page.text;
        }

        isTyping = false;
        Debug.Log("跳过打字");
    }

    void OnDialogueEnd()
    {
        Debug.Log("对白结束");
        // 可以在这里触发后续事件
    }

    // 测试方法
    [ContextMenu("测试打字机")]
    void TestTypewriter()
    {
        if (dialoguePages.Count > 0 && dialogueText != null && !isTyping)
        {
            // 临时测试
            string testText = "这是一个测试文本，用于验证打字机效果是否正常工作。";
            dialogueText.text = "";
            isTyping = true;

            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
            }
            typingCoroutine = StartCoroutine(TypeTextCoroutine(testText, 20f, null));
        }
    }

    [ContextMenu("重置到第一页")]
    void ResetToFirstPage()
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        isTransitioning = false;
        isTyping = false;
        currentPageIndex = 0;

        if (dialoguePages.Count > 0)
        {
            ShowPage(0);
        }
    }
}