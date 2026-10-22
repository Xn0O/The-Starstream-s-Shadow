using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 开局语言选择弹窗，以及菜单右下角的“语言 / Language”切换按钮。
/// 整个界面在运行时生成，不需要修改 Menu 场景。
/// </summary>
public class LanguageSelectUI : MonoBehaviour
{
    private static LanguageSelectUI instance;

    private static readonly Color Purple = new Color(0.42f, 0.22f, 0.65f, 1f);
    private static readonly Color PurpleSelected = new Color(0.56f, 0.30f, 0.85f, 1f);
    private static readonly Color PurpleHover = new Color(0.66f, 0.40f, 0.95f, 1f);
    private static readonly Color PurplePressed = new Color(0.32f, 0.15f, 0.52f, 1f);

    private GameObject promptRoot;
    private Button menuButton;
    private Button zhButton;
    private Button enButton;
    private int selected;

    private readonly List<MenuButton> blockedMenuButtons = new List<MenuButton>();

    public static void EnsureMenuButton()
    {
        if (instance == null)
            CreateManager();

        if (instance != null)
            instance.CreateMenuButtonIfNeeded();
    }

    public static void ShowPrompt()
    {
        EnsureMenuButton();

        if (instance != null)
            instance.OpenPrompt();
    }

    private static void CreateManager()
    {
        if (!Application.isPlaying) return;
        if (!SceneManager.GetActiveScene().IsValid()) return;

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        GameObject root = new GameObject("LanguageUI", typeof(RectTransform));
        root.layer = 5; // UI 层，保证专用 UI 相机能渲染到
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();
        instance = root.AddComponent<LanguageSelectUI>();
    }

    private void CreateMenuButtonIfNeeded()
    {
        if (menuButton != null) return;

        GameObject buttonObject = CreateUIObject("LanguageButton", transform);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-24f, 24f);
        rect.sizeDelta = new Vector2(220f, 56f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = Color.white;

        menuButton = buttonObject.AddComponent<Button>();
        menuButton.targetGraphic = image;
        ConfigureButtonColors(menuButton);
        menuButton.onClick.AddListener(OpenPrompt);

        Text label = CreateText(buttonObject.transform, "语言 / Language", 24, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform, 8f);
        label.color = new Color(1f, 1f, 1f, 0.9f);
    }

    private void OpenPrompt()
    {
        if (promptRoot != null) return;

        if (menuButton != null)
            menuButton.gameObject.SetActive(false);

        SetMenuInputBlocked(true);

        promptRoot = CreateUIObject("LanguagePrompt", transform);
        Stretch(promptRoot.GetComponent<RectTransform>());

        Image dim = promptRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.82f);
        dim.raycastTarget = true;

        GameObject panel = CreateUIObject("Panel", promptRoot.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(700f, 300f);

        Text title = CreateText(panel.transform, "选择语言 / Select Language", 40, TextAnchor.MiddleCenter);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -36f);
        titleRect.sizeDelta = new Vector2(-80f, 64f);

        zhButton = CreateChoiceButton(panel.transform, "中文", new Vector2(-150f, -30f), 0);
        enButton = CreateChoiceButton(panel.transform, "English", new Vector2(150f, -30f), 1);

        selected = Localization.IsEnglish ? 1 : 0;
        UpdateHighlight();
    }

    private Button CreateChoiceButton(Transform parent, string label, Vector2 position, int index)
    {
        GameObject buttonObject = CreateUIObject("Choice" + index, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(260f, 90f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = Color.white;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        ConfigureButtonColors(button);
        button.onClick.AddListener(delegate { Choose(index); });

        Text text = CreateText(buttonObject.transform, label, 38, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform, 6f);

        return button;
    }

    private void Update()
    {
        if (promptRoot == null) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow) ||
            Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.W))
        {
            selected = 0;
            UpdateHighlight();
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                 Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.S))
        {
            selected = 1;
            UpdateHighlight();
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space))
        {
            Choose(selected);
        }
    }

    private void UpdateHighlight()
    {
        SetButtonSelected(zhButton, selected == 0);
        SetButtonSelected(enButton, selected == 1);
    }

    private static void SetButtonSelected(Button button, bool isSelected)
    {
        if (button == null) return;

        ColorBlock colors = button.colors;
        colors.normalColor = isSelected ? PurpleSelected : Purple;
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
    }

    private static void ConfigureButtonColors(Button button)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = Purple;
        colors.highlightedColor = PurpleHover;
        colors.pressedColor = PurplePressed;
        colors.selectedColor = Purple;
        colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private void Choose(int index)
    {
        string language = index == 0 ? Localization.Chinese : Localization.English;
        Localization.SetLanguage(language);
        ClosePrompt();
    }

    private void ClosePrompt()
    {
        if (promptRoot != null)
            Destroy(promptRoot);

        promptRoot = null;

        if (menuButton != null)
            menuButton.gameObject.SetActive(true);

        // 延迟一帧再恢复菜单输入，避免确认键在同一帧里立刻触发“开始游戏”。
        StartCoroutine(UnblockMenuNextFrame());
    }

    private IEnumerator UnblockMenuNextFrame()
    {
        yield return null;
        SetMenuInputBlocked(false);
    }

    private void SetMenuInputBlocked(bool blocked)
    {
        if (blocked)
        {
            blockedMenuButtons.Clear();
            MenuButton[] menuButtons = Object.FindObjectsOfType<MenuButton>(true);
            foreach (MenuButton button in menuButtons)
            {
                if (button == null || !button.enabled) continue;
                button.enabled = false;
                blockedMenuButtons.Add(button);
            }
        }
        else
        {
            foreach (MenuButton button in blockedMenuButtons)
            {
                if (button != null)
                    button.enabled = true;
            }
            blockedMenuButtons.Clear();
        }
    }

    private static Text CreateText(Transform parent, string content, int size, TextAnchor alignment)
    {
        GameObject textObject = CreateUIObject("Text", parent);
        Text text = textObject.AddComponent<Text>();
        text.font = Localization.MainFont;
        text.text = content;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.supportRichText = false;
        return text;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5; // UI 层，保证专用 UI 相机能渲染到
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rect, float padding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }
}
