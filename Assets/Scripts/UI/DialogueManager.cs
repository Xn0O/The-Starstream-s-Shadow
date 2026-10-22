using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;

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

    [Header("下一页按键设置")]
    [Tooltip("主下一页按键")]
    public KeyCode nextKey = KeyCode.Space;

    [Tooltip("备选下一页按键列表（支持多个按键）")]
    public KeyCode[] alternativeNextKeys = new KeyCode[]
    {
        KeyCode.Return,
        KeyCode.KeypadEnter,
        KeyCode.RightArrow,
        KeyCode.DownArrow
    };

    [Header("上一页按键设置")]
    [Tooltip("主上一页按键")]
    public KeyCode prevKey = KeyCode.A;

    [Tooltip("备选上一页按键列表（支持多个按键）")]
    public KeyCode[] alternativePrevKeys = new KeyCode[]
    {
        KeyCode.LeftArrow,
        KeyCode.UpArrow
    };

    [Header("跳过打字按键设置")]
    [Tooltip("主跳过打字按键")]
    public KeyCode skipTypingKey = KeyCode.S;

    [Tooltip("备选跳过打字按键列表（支持多个按键）")]
    public KeyCode[] alternativeSkipKeys = new KeyCode[]
    {
        KeyCode.LeftControl,
        KeyCode.RightControl,
        KeyCode.LeftShift,
        KeyCode.RightShift
    };

    [Header("快速跳过按键设置")]
    [Tooltip("快速跳过所有对话到结尾")]
    public KeyCode skipToEndKey = KeyCode.Escape;

    [Tooltip("备选快速跳过按键")]
    public KeyCode[] alternativeSkipToEndKeys = new KeyCode[]
    {
        KeyCode.Backspace,
        KeyCode.Delete
    };

    [Header("效果设置")]
    public bool autoStartTyping = true;
    public bool playPageSound = true;
    public float fadeDuration = 0.3f;

    [Header("音量设置")]
    [Range(0, 1)]
    public float typingSoundVolume = 0.5f;
    [Range(0, 1)]
    public float pageSoundVolume = 0.7f;

    [Header("按键检测设置")]
    [Tooltip("是否允许鼠标点击翻页")]
    public bool allowMouseClick = true;
    [Tooltip("鼠标左键是否作为下一页按键")]
    public bool mouseLeftClickAsNext = true;
    [Tooltip("鼠标右键是否作为上一页按键")]
    public bool mouseRightClickAsPrev = false;

    [Header("颜色标记设置")]
    [Tooltip("是否启用颜色标记系统")]
    public bool enableColorTags = true;

    [Tooltip("预定义颜色标签 - 键:标签名, 值:颜色")]
    public List<ColorTag> colorTags = new List<ColorTag>()
    {
        new ColorTag("red", Color.red),
        new ColorTag("blue", Color.blue),
        new ColorTag("green", Color.green),
        new ColorTag("yellow", Color.yellow),
        new ColorTag("orange", new Color(1f, 0.5f, 0f)),
        new ColorTag("purple", new Color(0.5f, 0f, 0.5f)),
        new ColorTag("cyan", Color.cyan),
        new ColorTag("magenta", Color.magenta),
        new ColorTag("gray", Color.gray)
    };

    [System.Serializable]
    public class ColorTag
    {
        public string tagName;
        public Color color;

        public ColorTag(string name, Color col)
        {
            tagName = name;
            color = col;
        }
    }

    // 用于检测按键的辅助属性
    public bool AnyNextKeyPressed
    {
        get
        {
            bool pressed = Input.GetKeyDown(nextKey) ||
                          alternativeNextKeys.Any(key => Input.GetKeyDown(key));

            if (allowMouseClick && mouseLeftClickAsNext && Input.GetMouseButtonDown(0))
                pressed = true;

            return pressed;
        }
    }

    public bool AnyPrevKeyPressed
    {
        get
        {
            bool pressed = Input.GetKeyDown(prevKey) ||
                          alternativePrevKeys.Any(key => Input.GetKeyDown(key));

            if (allowMouseClick && mouseRightClickAsPrev && Input.GetMouseButtonDown(1))
                pressed = true;

            return pressed;
        }
    }

    public bool AnySkipKeyPressed
    {
        get
        {
            return Input.GetKeyDown(skipTypingKey) ||
                   alternativeSkipKeys.Any(key => Input.GetKeyDown(key));
        }
    }

    public bool AnySkipToEndKeyPressed
    {
        get
        {
            return Input.GetKeyDown(skipToEndKey) ||
                   alternativeSkipToEndKeys.Any(key => Input.GetKeyDown(key));
        }
    }

    private int currentPageIndex = 0;
    private bool isTransitioning = false;
    private bool isTyping = false;
    private Coroutine typingCoroutine;
    private Coroutine transitionCoroutine;
    private AudioSource audioSource;
    private List<ColorMarker> currentColorMarkers = new List<ColorMarker>();
    private string currentPlainText = "";
    private int chineseFontSize;
    private float chineseLineSpacing;

    // 颜色标记结构
    private struct ColorMarker
    {
        public int startIndex;
        public int length;
        public string htmlColor;

        public ColorMarker(int start, int len, string color)
        {
            startIndex = start;
            length = len;
            htmlColor = color;
        }

        public bool ContainsIndex(int index)
        {
            return index >= startIndex && index < startIndex + length;
        }
    }

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        ApplyLanguageLayout();

        if (dialoguePages.Count > 0)
        {
            ShowPage(0);
        }
        else
        {
            Debug.LogWarning("没有添加任何对白页面！");
        }

        if (continueIndicator != null)
        {
            continueIndicator.SetActive(false);
        }

        Debug.Log("对白管理器按键配置：");
        Debug.Log($"下一页按键: {nextKey} + {alternativeNextKeys.Length}个备选按键");
        Debug.Log($"上一页按键: {prevKey} + {alternativePrevKeys.Length}个备选按键");
        Debug.Log($"跳过打字按键: {skipTypingKey} + {alternativeSkipKeys.Length}个备选按键");
    }

    void Update()
    {
        if (isTransitioning) return;

        if (AnySkipToEndKeyPressed && dialoguePages.Count > 0)
        {
            SkipToEnd();
            return;
        }

        if (isTyping && AnySkipKeyPressed)
        {
            SkipTyping();
            return;
        }

        if (!isTyping)
        {
            if (AnyNextKeyPressed)
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
            else if (AnyPrevKeyPressed)
            {
                if (currentPageIndex > 0)
                {
                    PreviousPage();
                }
            }
        }

        if (continueIndicator != null)
        {
            continueIndicator.SetActive(!isTyping && dialoguePages.Count > 0);
        }
    }

    void NextPage()
    {
        if (currentPageIndex >= dialoguePages.Count - 1) return;
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

    void SkipToEnd()
    {
        if (dialoguePages.Count == 0) return;

        Debug.Log("快速跳到对话结尾");

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }
        if (isTyping && typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        isTransitioning = false;
        isTyping = false;
        currentPageIndex = dialoguePages.Count - 1;
        ShowPage(currentPageIndex);

        if (isTyping)
        {
            SkipTyping();
        }
    }

    IEnumerator TransitionToPage(int newPageIndex)
    {
        isTransitioning = true;

        if (isTyping && typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            isTyping = false;
        }

        if (fadeDuration > 0 && dialoguePanel != null)
        {
            yield return StartCoroutine(FadePanel(0f));
        }
        else
        {
            yield return null;
        }

        DialoguePage currentPage = dialoguePages[currentPageIndex];
        if (currentPage.showObjects != null)
        {
            foreach (GameObject obj in currentPage.showObjects)
            {
                if (obj != null)
                    obj.SetActive(false);
            }
        }

        ShowPage(newPageIndex);

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

    void OnEnable()
    {
        Localization.LanguageChanged += RefreshLocalizedText;
    }

    void OnDisable()
    {
        Localization.LanguageChanged -= RefreshLocalizedText;
    }

    void RefreshLocalizedText()
    {
        if (dialoguePages == null || dialoguePages.Count == 0) return;

        int index = Mathf.Clamp(currentPageIndex, 0, dialoguePages.Count - 1);
        ShowPage(index);
    }

    void ApplyLanguageLayout()
    {
        if (dialogueText == null) return;

        if (chineseFontSize <= 0)
        {
            chineseFontSize = dialogueText.fontSize;
            chineseLineSpacing = dialogueText.lineSpacing;
        }

        if (Localization.IsEnglish)
        {
            // 英文译文更长，缩小字号与行距以避免超出对话框。
            dialogueText.fontSize = Mathf.Max(1, Mathf.RoundToInt(chineseFontSize * 0.75f));
            dialogueText.lineSpacing = chineseLineSpacing * 0.6f;
        }
        else
        {
            dialogueText.fontSize = chineseFontSize;
            dialogueText.lineSpacing = chineseLineSpacing;
        }
    }

    void ShowPage(int pageIndex)
    {
        currentPageIndex = pageIndex;
        ApplyLanguageLayout();
        DialoguePage page = dialoguePages[pageIndex];
        string pageText = Localization.GetDialogue(SceneManager.GetActiveScene().name, pageIndex, page.text);

        Debug.Log($"显示第 {pageIndex + 1} 页，使用打字机: {page.useTypewriter}，速度: {page.typingSpeed}");

        if (dialogueText != null)
        {
            dialogueText.text = "";
        }

        if (pageCounterText != null)
        {
            pageCounterText.text = $"{pageIndex + 1}/{dialoguePages.Count}";
        }

        if (playPageSound && page.pageSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(page.pageSound, pageSoundVolume);
        }

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

        // 解析颜色标记和获取纯文本
        currentColorMarkers.Clear();
        if (enableColorTags)
        {
            currentPlainText = ParseColorMarkersAndGetPlainText(pageText);
        }
        else
        {
            currentPlainText = pageText;
        }

        // 开始打字机效果
        if (page.useTypewriter && !string.IsNullOrEmpty(pageText) && dialogueText != null)
        {
            if (autoStartTyping)
            {
                isTyping = true;
                typingCoroutine = StartCoroutine(TypeTextCoroutine(currentPlainText, page.typingSpeed, page.typingSound));
            }
        }
        else
        {
            if (dialogueText != null)
            {
                dialogueText.text = ApplyColorMarkers(currentPlainText, currentPlainText.Length);
            }
            isTyping = false;
        }
    }

    IEnumerator TypeTextCoroutine(string plainText, float typingSpeed, AudioClip typingSoundClip)
    {
        Debug.Log($"开始打字，长度: {plainText.Length}");

        if (typingSpeed <= 0)
            typingSpeed = 30f;

        // 英文文本更长，打字速度提高 4 倍。
        if (Localization.IsEnglish)
            typingSpeed *= 4f;

        float delayPerChar = 1f / typingSpeed;

        for (int i = 0; i <= plainText.Length; i++)
        {
            if (!isTyping)
                break;

            if (dialogueText != null)
            {
                string displayText = ApplyColorMarkers(plainText, i);
                dialogueText.text = displayText;
            }

            if (typingSoundClip != null && audioSource != null && i < plainText.Length)
            {
                char c = plainText[i];
                if (c != ' ' && c != '\n' && c != '\t')
                {
                    bool playSound = true;

                    // 英文按单词播放，避免每个字母都响、听起来像机关枪。
                    if (Localization.IsEnglish && i > 0)
                        playSound = char.IsWhiteSpace(plainText[i - 1]);

                    if (playSound)
                        audioSource.PlayOneShot(typingSoundClip, typingSoundVolume);
                }
            }

            if (i < plainText.Length)
            {
                float extraDelay = 0f;
                char currentChar = plainText[i];

                if (",;。，".IndexOf(currentChar) >= 0)
                    extraDelay = delayPerChar * 2f;
                else if (".!?！？".IndexOf(currentChar) >= 0)
                    extraDelay = delayPerChar * 3f;
                else if (currentChar == '\n')
                    extraDelay = delayPerChar * 1.5f;

                yield return new WaitForSeconds(delayPerChar + extraDelay);
            }
        }

        if (dialogueText != null)
        {
            dialogueText.text = ApplyColorMarkers(plainText, plainText.Length);
        }
        isTyping = false;
        Debug.Log("打字完成");
    }

    void SkipTyping()
    {
        if (!isTyping || typingCoroutine == null)
            return;

        StopCoroutine(typingCoroutine);

        if (dialogueText != null)
        {
            dialogueText.text = ApplyColorMarkers(currentPlainText, currentPlainText.Length);
        }

        isTyping = false;
        Debug.Log("跳过打字");
    }

    void OnDialogueEnd()
    {
        Debug.Log("对白结束");
    }

    // ============== 新的颜色标记系统 ==============

    // 解析颜色标记并获取纯文本
    private string ParseColorMarkersAndGetPlainText(string text)
    {
        if (!enableColorTags || string.IsNullOrEmpty(text))
            return text;

        List<ColorMarker> markers = new List<ColorMarker>();
        System.Text.StringBuilder plainTextBuilder = new System.Text.StringBuilder();

        int position = 0;
        while (position < text.Length)
        {
            // 检查是否是颜色标签开始
            if (text[position] == '[' && position + 1 < text.Length)
            {
                bool foundTag = false;

                // 尝试匹配十六进制颜色标签
                if (text[position + 1] == '#')
                {
                    foundTag = TryParseHexColorTag(text, position, markers, plainTextBuilder, ref position);
                }
                // 尝试匹配预定义颜色标签
                else if (char.IsLetter(text[position + 1]))
                {
                    foundTag = TryParsePredefinedColorTag(text, position, markers, plainTextBuilder, ref position);
                }
                // 尝试匹配RGB颜色标签
                else if (char.IsDigit(text[position + 1]))
                {
                    foundTag = TryParseRgbColorTag(text, position, markers, plainTextBuilder, ref position);
                }

                if (!foundTag)
                {
                    // 不是颜色标签，添加字符
                    plainTextBuilder.Append(text[position]);
                    position++;
                }
            }
            else
            {
                // 普通字符
                plainTextBuilder.Append(text[position]);
                position++;
            }
        }

        // 存储标记
        currentColorMarkers = markers;

        return plainTextBuilder.ToString();
    }

    // 尝试解析十六进制颜色标签
    private bool TryParseHexColorTag(string text, int startPos, List<ColorMarker> markers, System.Text.StringBuilder plainText, ref int position)
    {
        // 查找标签结束位置
        int tagEnd = text.IndexOf(']', startPos);
        if (tagEnd == -1) return false;

        string tagContent = text.Substring(startPos + 2, tagEnd - startPos - 2);

        // 验证十六进制颜色代码（6或8位）
        if (tagContent.Length != 6 && tagContent.Length != 8) return false;
        foreach (char c in tagContent)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f')))
                return false;
        }

        // 查找结束标签
        string endTag = $"[/#{tagContent}]";
        int contentEnd = text.IndexOf(endTag, tagEnd + 1);
        if (contentEnd == -1) return false;

        // 提取内容
        string content = text.Substring(tagEnd + 1, contentEnd - tagEnd - 1);

        // 记录颜色标记
        int startIndex = plainText.Length;
        markers.Add(new ColorMarker(startIndex, content.Length, $"#{tagContent}"));

        // 添加内容到纯文本
        plainText.Append(content);

        // 更新位置
        position = contentEnd + endTag.Length;

        return true;
    }

    // 尝试解析预定义颜色标签
    private bool TryParsePredefinedColorTag(string text, int startPos, List<ColorMarker> markers, System.Text.StringBuilder plainText, ref int position)
    {
        // 查找标签结束位置
        int tagEnd = text.IndexOf(']', startPos);
        if (tagEnd == -1) return false;

        string tagName = text.Substring(startPos + 1, tagEnd - startPos - 1);

        // 查找对应的颜色
        var colorTag = colorTags.FirstOrDefault(t => t.tagName == tagName);
        if (colorTag == null) return false;

        // 查找结束标签
        string endTag = $"[/{tagName}]";
        int contentEnd = text.IndexOf(endTag, tagEnd + 1);
        if (contentEnd == -1) return false;

        // 提取内容
        string content = text.Substring(tagEnd + 1, contentEnd - tagEnd - 1);

        // 记录颜色标记
        int startIndex = plainText.Length;
        markers.Add(new ColorMarker(startIndex, content.Length, ColorToHex(colorTag.color)));

        // 添加内容到纯文本
        plainText.Append(content);

        // 更新位置
        position = contentEnd + endTag.Length;

        return true;
    }

    // 尝试解析RGB颜色标签
    private bool TryParseRgbColorTag(string text, int startPos, List<ColorMarker> markers, System.Text.StringBuilder plainText, ref int position)
    {
        // 查找标签结束位置
        int tagEnd = text.IndexOf(']', startPos);
        if (tagEnd == -1) return false;

        string tagContent = text.Substring(startPos + 1, tagEnd - startPos - 1);

        // 解析RGB或RGBA值
        string[] parts = tagContent.Split(',');
        if (parts.Length != 3 && parts.Length != 4) return false;

        // 验证数字
        foreach (string part in parts)
        {
            if (!int.TryParse(part, out int value) || value < 0 || value > 255)
                return false;
        }

        int r = int.Parse(parts[0]);
        int g = int.Parse(parts[1]);
        int b = int.Parse(parts[2]);
        float a = parts.Length == 4 ? int.Parse(parts[3]) / 255f : 1f;

        // 查找结束标签
        string endTag = $"[/{tagContent}]";
        int contentEnd = text.IndexOf(endTag, tagEnd + 1);
        if (contentEnd == -1) return false;

        // 提取内容
        string content = text.Substring(tagEnd + 1, contentEnd - tagEnd - 1);

        // 创建颜色并记录标记
        Color color = new Color(r / 255f, g / 255f, b / 255f, a);
        int startIndex = plainText.Length;
        markers.Add(new ColorMarker(startIndex, content.Length, ColorToHex(color)));

        // 添加内容到纯文本
        plainText.Append(content);

        // 更新位置
        position = contentEnd + endTag.Length;

        return true;
    }

    // 应用颜色标记到文本
    private string ApplyColorMarkers(string plainText, int visibleLength)
    {
        if (currentColorMarkers.Count == 0 || visibleLength <= 0)
            return plainText.Substring(0, Mathf.Min(visibleLength, plainText.Length));

        // 对标记按开始位置排序
        var sortedMarkers = currentColorMarkers
            .Where(m => m.startIndex < visibleLength)
            .OrderBy(m => m.startIndex)
            .ToList();

        if (sortedMarkers.Count == 0)
            return plainText.Substring(0, Mathf.Min(visibleLength, plainText.Length));

        System.Text.StringBuilder result = new System.Text.StringBuilder();
        int currentPos = 0;
        int markerIndex = 0;

        while (currentPos < visibleLength && currentPos < plainText.Length)
        {
            // 检查当前位置是否有颜色标记开始
            if (markerIndex < sortedMarkers.Count && currentPos == sortedMarkers[markerIndex].startIndex)
            {
                var marker = sortedMarkers[markerIndex];
                int markerEnd = Mathf.Min(marker.startIndex + marker.length, visibleLength);
                int markerVisibleLength = markerEnd - marker.startIndex;

                if (markerVisibleLength > 0)
                {
                    // 添加颜色标签和内容
                    result.Append($"<color={marker.htmlColor}>");
                    result.Append(plainText.Substring(currentPos, markerVisibleLength));
                    result.Append("</color>");
                    currentPos += markerVisibleLength;
                    markerIndex++;
                }
                else
                {
                    markerIndex++;
                }
            }
            else
            {
                // 没有颜色标记，添加普通字符
                result.Append(plainText[currentPos]);
                currentPos++;

                // 跳过已过的标记
                while (markerIndex < sortedMarkers.Count &&
                       sortedMarkers[markerIndex].startIndex + sortedMarkers[markerIndex].length <= currentPos)
                {
                    markerIndex++;
                }
            }
        }

        return result.ToString();
    }

    // 将Color转换为十六进制字符串
    private string ColorToHex(Color color)
    {
        return $"#{ColorUtility.ToHtmlStringRGBA(color)}";
    }

    // 添加预定义颜色标签
    public void AddColorTag(string tagName, Color color)
    {
        if (!colorTags.Any(t => t.tagName == tagName))
        {
            colorTags.Add(new ColorTag(tagName, color));
            Debug.Log($"已添加颜色标签: {tagName} -> {ColorToHex(color)}");
        }
    }

    // 移除预定义颜色标签
    public void RemoveColorTag(string tagName)
    {
        var tag = colorTags.FirstOrDefault(t => t.tagName == tagName);
        if (tag != null)
        {
            colorTags.Remove(tag);
            Debug.Log($"已移除颜色标签: {tagName}");
        }
    }

    // 测试颜色标记
    [ContextMenu("测试颜色标记")]
    void TestColorTags()
    {
        string testText = "你可以尝试[yellow]操作一下[/yellow].触碰敌人[red]叠加层数[/red]";
        Debug.Log($"测试文本: {testText}");

        string plainText = ParseColorMarkersAndGetPlainText(testText);
        Debug.Log($"纯文本: {plainText}");
        Debug.Log($"颜色标记数量: {currentColorMarkers.Count}");

        foreach (var marker in currentColorMarkers)
        {
            Debug.Log($"标记: 位置={marker.startIndex}, 长度={marker.length}, 颜色={marker.htmlColor}");
        }

        string withColors = ApplyColorMarkers(plainText, plainText.Length);
        Debug.Log($"应用颜色后: {withColors}");

        if (dialogueText != null)
        {
            dialogueText.text = withColors;
        }
    }

    public string GetKeyConfigInfo()
    {
        return $"按键配置：\n" +
               $"下一页：{nextKey} + {string.Join(", ", alternativeNextKeys)}\n" +
               $"上一页：{prevKey} + {string.Join(", ", alternativePrevKeys)}\n" +
               $"跳过打字：{skipTypingKey} + {string.Join(", ", alternativeSkipKeys)}\n" +
               $"快速跳过：{skipToEndKey} + {string.Join(", ", alternativeSkipToEndKeys)}\n" +
               $"鼠标点击：左键={(mouseLeftClickAsNext ? "下一页" : "关闭")}，右键={(mouseRightClickAsPrev ? "上一页" : "关闭")}";
    }

    public void AddNextKey(KeyCode newKey)
    {
        if (!alternativeNextKeys.Contains(newKey) && newKey != nextKey)
        {
            var list = new List<KeyCode>(alternativeNextKeys);
            list.Add(newKey);
            alternativeNextKeys = list.ToArray();
            Debug.Log($"已添加下一页按键：{newKey}");
        }
    }

    public void RemoveNextKey(KeyCode keyToRemove)
    {
        if (alternativeNextKeys.Contains(keyToRemove))
        {
            var list = new List<KeyCode>(alternativeNextKeys);
            list.Remove(keyToRemove);
            alternativeNextKeys = list.ToArray();
            Debug.Log($"已移除下一页按键：{keyToRemove}");
        }
    }

    [ContextMenu("测试打字机")]
    void TestTypewriter()
    {
        if (dialogueText != null && !isTyping)
        {
            string testText = "你可以尝试[yellow]操作一下[/yellow].触碰敌人[red]叠加层数[/red]";
            string plainText = ParseColorMarkersAndGetPlainText(testText);

            dialogueText.text = "";
            isTyping = true;

            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
            }
            typingCoroutine = StartCoroutine(TypeTextCoroutine(plainText, 20f, null));
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

    [ContextMenu("打印按键配置")]
    void PrintKeyConfig()
    {
        Debug.Log(GetKeyConfigInfo());
    }
}