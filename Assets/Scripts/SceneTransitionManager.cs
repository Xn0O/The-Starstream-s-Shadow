using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance;

    [Header("转场动画预制体")]
    public GameObject transitionPrefab;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 在新的场景开始时播放转场动画
    public void PlayStartTransition()
    {
        if (transitionPrefab)
        {
            GameObject transition = Instantiate(transitionPrefab, Vector3.zero, Quaternion.identity);
            Destroy(transition, 2f); // 2秒后自动销毁
        }
    }

    // 切换场景
    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneWithTransition(sceneName));
    }

    IEnumerator LoadSceneWithTransition(string sceneName)
    {
        // 播放转场入场动画
        if (transitionPrefab)
        {
            GameObject transitionIn = Instantiate(transitionPrefab, Vector3.zero, Quaternion.identity);
            transitionIn.GetComponent<Animator>().Play(0);

            // 等待动画播放
            yield return new WaitForSeconds(1f);

            Destroy(transitionIn);
        }

        // 加载场景
        SceneManager.LoadScene(sceneName);

        // 在新场景中播放转场出场动画
        yield return null; // 等待一帧确保场景加载完成

        if (transitionPrefab)
        {
            GameObject transitionOut = Instantiate(transitionPrefab, Vector3.zero, Quaternion.identity);
            transitionOut.GetComponent<Animator>().Play(0);
            Destroy(transitionOut, 2f);
        }
    }
}