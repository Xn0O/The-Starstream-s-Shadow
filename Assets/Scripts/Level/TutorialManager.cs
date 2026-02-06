using UnityEngine;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    [Header("教程设置")]
    public string tutorialText;
    public Text tutorialContentText;
    public CanvasGroup tutorialCanvas;
    public float fadeDuration = 0.5f;

    [Header("角色控制")]
    public MonoBehaviour playerController;

    private bool hasStartedFade = false;

    void Start()
    {
        if (playerController != null) playerController.enabled = false;
        tutorialContentText.text = tutorialText;
    }

    void Update()
    {
        // 检测空格或回车键
        if (!hasStartedFade && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
        {
            StartCoroutine(FadeOutTutorial());
        }
    }

    System.Collections.IEnumerator FadeOutTutorial()
    {
        hasStartedFade = true;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            tutorialCanvas.alpha = 1 - (timer / fadeDuration);
            yield return null;
        }

        if (playerController != null) playerController.enabled = true;
        gameObject.SetActive(false);
    }

    // 如果要用UI按钮触发
    public void StartFadeOut()
    {
        if (!hasStartedFade)
            StartCoroutine(FadeOutTutorial());
    }
}