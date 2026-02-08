using UnityEngine;

public class BossDestroyWatcher : MonoBehaviour
{
    [SerializeField] private GameObject bossToWatch;
    [SerializeField] private GameObject effectToShow;
    [SerializeField] private float checkInterval = 0.2f; // 检查间隔，减少性能消耗

    private void Start()
    {
        if (effectToShow != null)
            effectToShow.SetActive(false);

        StartCoroutine(WatchForDestruction());
    }

    private System.Collections.IEnumerator WatchForDestruction()
    {
        // 持续检查直到BOSS被销毁
        while (bossToWatch != null)
        {
            yield return new WaitForSeconds(checkInterval);
        }

        // BOSS已销毁
        OnBossDestroyed();
    }

    private void OnBossDestroyed()
    {
        if (effectToShow != null)
        {
            effectToShow.SetActive(true);
        }

        // 可选：销毁监听器自身
        // Destroy(this);
    }
}