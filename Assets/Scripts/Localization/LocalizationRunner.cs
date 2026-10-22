using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 在每次场景加载后应用本地化，并在 Menu 场景显示语言选择界面。
/// </summary>
public class LocalizationRunner : MonoBehaviour
{
    private static LocalizationRunner instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRunner()
    {
        if (instance != null) return;

        GameObject runnerObject = new GameObject("[LocalizationRunner]");
        DontDestroyOnLoad(runnerObject);

        instance = runnerObject.AddComponent<LocalizationRunner>();
        SceneManager.sceneLoaded += instance.OnSceneLoaded;

        // 第一个场景已经加载完成，sceneLoaded 不会再触发，手动处理一次。
        instance.StartCoroutine(instance.ApplyNextFrame(SceneManager.GetActiveScene().name));
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ApplyNextFrame(scene.name));
    }

    private IEnumerator ApplyNextFrame(string sceneName)
    {
        // 等一帧，确保场景内各脚本的 Start()（例如 TutorialManager）已经执行。
        yield return null;

        Localization.ApplyToScene();

        if (sceneName == "Menu")
        {
            if (Localization.NeedsPrompt)
            {
                Localization.MarkPromptShown();
                LanguageSelectUI.ShowPrompt();
            }
            else
            {
                LanguageSelectUI.EnsureMenuButton();
            }
        }
    }
}
