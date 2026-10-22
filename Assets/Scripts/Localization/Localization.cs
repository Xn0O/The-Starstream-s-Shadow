using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class LocalizationEntry
{
    public string zh;
    public string en;
}

[Serializable]
public class DialogueTranslation
{
    public string scene;
    public string[] en;
}

[Serializable]
public class RuntimeTextEntry
{
    public string key;
    public string zh;
    public string en;
}

[Serializable]
public class LocalizationData
{
    public LocalizationEntry[] ui;
    public DialogueTranslation[] dialogues;
    public RuntimeTextEntry[] runtime;
}

/// <summary>
/// 游戏本地化入口。数据存放在 Resources/Localization/localization.json。
/// 中文文本直接使用场景中原有的文本，英文通过 zh -> en 映射替换。
/// </summary>
public static class Localization
{
    public const string PrefKey = "GameLanguage";
    public const string Chinese = "zh";
    public const string English = "en";

    public static event Action LanguageChanged;

    private static readonly Dictionary<string, string> zhToEn = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> enToZh = new Dictionary<string, string>();
    private static readonly Dictionary<string, string[]> dialogueEn = new Dictionary<string, string[]>();
    private static readonly Dictionary<string, string> runtimeZh = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> runtimeEn = new Dictionary<string, string>();

    private static Font mainFont;
    private static bool loaded;
    private static bool needsPrompt;

    public static string CurrentLanguage { get; private set; } = Chinese;
    public static bool IsEnglish { get { return CurrentLanguage == English; } }
    public static bool NeedsPrompt { get { return needsPrompt; } }

    public static Font MainFont
    {
        get
        {
            EnsureLoaded();
            return mainFont;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        string saved = PlayerPrefs.GetString(PrefKey, "");
        needsPrompt = string.IsNullOrEmpty(saved);
        CurrentLanguage = saved == English ? English : Chinese;
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;

        TextAsset asset = Resources.Load<TextAsset>("Localization/localization");
        if (asset == null)
        {
            Debug.LogError("[Localization] 找不到 Resources/Localization/localization.json");
            return;
        }

        loaded = true;

        mainFont = Resources.Load<Font>("Fonts/SourceHanSansSC-Regular");
        if (mainFont == null)
        {
            mainFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Debug.LogWarning("[Localization] 未找到思源黑体，已回退到内置字体。");
        }

        LocalizationData data = JsonUtility.FromJson<LocalizationData>(asset.text);
        if (data == null) return;

        if (data.ui != null)
        {
            foreach (LocalizationEntry entry in data.ui)
            {
                if (entry == null || string.IsNullOrEmpty(entry.zh)) continue;
                zhToEn[entry.zh] = entry.en;
                if (!string.IsNullOrEmpty(entry.en) && !enToZh.ContainsKey(entry.en))
                    enToZh[entry.en] = entry.zh;
            }
        }

        if (data.dialogues != null)
        {
            foreach (DialogueTranslation entry in data.dialogues)
            {
                if (entry == null || string.IsNullOrEmpty(entry.scene)) continue;
                dialogueEn[entry.scene] = entry.en;
            }
        }

        if (data.runtime != null)
        {
            foreach (RuntimeTextEntry entry in data.runtime)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key)) continue;
                runtimeZh[entry.key] = entry.zh;
                runtimeEn[entry.key] = entry.en;
            }
        }
    }

    public static void MarkPromptShown()
    {
        needsPrompt = false;
    }

    public static void SetLanguage(string language)
    {
        string next = language == English ? English : Chinese;
        CurrentLanguage = next;
        needsPrompt = false;

        PlayerPrefs.SetString(PrefKey, next);
        PlayerPrefs.Save();

        ApplyToScene();

        if (LanguageChanged != null)
            LanguageChanged();
    }

    /// <summary>
    /// 把当前场景中所有 Text（包含未激活对象）切换成当前语言。
    /// 字体保持场景中原有的设置，不做替换。
    /// </summary>
    public static void ApplyToScene()
    {
        EnsureLoaded();

        Text[] texts = UnityEngine.Object.FindObjectsOfType<Text>(true);
        foreach (Text text in texts)
        {
            if (text == null) continue;

            string current = text.text;
            if (string.IsNullOrEmpty(current)) continue;

            string translated = IsEnglish ? TranslateToEnglish(current) : TranslateToChinese(current);
            if (translated != current)
                text.text = translated;
        }
    }

    public static string Translate(string text)
    {
        EnsureLoaded();
        return IsEnglish ? TranslateToEnglish(text) : TranslateToChinese(text);
    }

    public static string TranslateToEnglish(string text)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(text)) return text;

        string result;
        if (zhToEn.TryGetValue(text, out result)) return result;

        // 兼容尾部换行数量不一致的情况
        string trimmed = text.TrimEnd('\n', '\r');
        if (trimmed.Length != text.Length && zhToEn.TryGetValue(trimmed, out result))
            return result + text.Substring(trimmed.Length);

        return text;
    }

    public static string TranslateToChinese(string text)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(text)) return text;

        string result;
        if (enToZh.TryGetValue(text, out result)) return result;

        string trimmed = text.TrimEnd('\n', '\r');
        if (trimmed.Length != text.Length && enToZh.TryGetValue(trimmed, out result))
            return result + text.Substring(trimmed.Length);

        return text;
    }

    /// <summary>
    /// 获取场景中第 pageIndex 页对话的本地化文本；没有英文数据时回退到原中文。
    /// </summary>
    public static string GetDialogue(string sceneName, int pageIndex, string fallback)
    {
        EnsureLoaded();
        if (!IsEnglish) return fallback;

        string[] pages;
        if (dialogueEn.TryGetValue(sceneName, out pages) &&
            pages != null &&
            pageIndex >= 0 &&
            pageIndex < pages.Length &&
            !string.IsNullOrEmpty(pages[pageIndex]))
        {
            return pages[pageIndex];
        }

        return fallback;
    }

    public static string Get(string key)
    {
        EnsureLoaded();
        string result;
        if ((IsEnglish ? runtimeEn : runtimeZh).TryGetValue(key, out result))
            return result;
        return key;
    }

    public static string GetFormat(string key, params object[] args)
    {
        string template = Get(key);
        try
        {
            return string.Format(template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }
}
