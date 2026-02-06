using UnityEngine;
using UnityEngine.SceneManagement;

public class OUTscene : MonoBehaviour
{
    public string targetSceneName = "";
    public bool useAnimatorEvent = true; // 是否使用动画事件

    void Update()
    {
        // 如果卡住，按空格键强制加载
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ForceLoadScene();
        }
    }

    public void LoadTargetScene()
    {
        if (useAnimatorEvent)
        {
            // 动画事件调用
            StartCoroutine(LoadSceneCoroutine());
        }
    }

    System.Collections.IEnumerator LoadSceneCoroutine()
    {
        Debug.Log("动画事件触发！");

        // 等待一帧确保动画完成
        yield return null;

        if (!string.IsNullOrEmpty(targetSceneName))
        {
            SceneManager.LoadScene(targetSceneName);
        }
    }

    public void SetTargetScene(string sceneName)
    {
        targetSceneName = sceneName;

        Animator anim = GetComponent<Animator>();
        if (anim)
        {
            anim.Play("SkipIn_1", 0, 0f);

            // 如果不使用动画事件，可以在播放后计时加载
            if (!useAnimatorEvent)
            {
                float animLength = anim.GetCurrentAnimatorStateInfo(0).length;
                Invoke("ForceLoadScene", animLength);
            }
        }
    }

    void ForceLoadScene()
    {
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            Debug.Log($"强制加载场景: {targetSceneName}");
            SceneManager.LoadScene(targetSceneName);
        }
    }
}