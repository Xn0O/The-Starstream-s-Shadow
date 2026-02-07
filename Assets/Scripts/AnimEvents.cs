using UnityEngine;
using UnityEngine.SceneManagement;

public class AnimEvents : MonoBehaviour
{
    //销毁自身
    public void DestroySelf()
    {
        Destroy(gameObject);
    }

    // 退出游戏
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    // 切换场景到Test
    public void to_Test()
    {
        SceneManager.LoadScene("Test");
    }
    public void L0_0()
    {
        SceneManager.LoadScene("L0_0");
    }
    public void Talk()
    {
        SceneManager.LoadScene("Talk");
    }
    public void BOSS1()
    {
        SceneManager.LoadScene("BOSS1");
    }
    public void BOSS2()
    {
        SceneManager.LoadScene("BOSS2");
    }

}