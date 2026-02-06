// PlayerParryDetector.cs - 玩家弹反检测器
using UnityEngine;

public class PlayerParryDetector : MonoBehaviour
{
    [SerializeField] private PlayerParrySystem parrySystem;

    void Start()
    {
        if (!parrySystem)
            parrySystem = GetComponentInParent<PlayerParrySystem>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!parrySystem) return;

        // 检测子弹
        if (other.CompareTag("Bullet") || other.CompareTag("EnemyBullet"))
        {
            GameObject bullet = other.gameObject;

            // 获取子弹方向
            BaseBullet bulletComp = bullet.GetComponent<BaseBullet>();
            if (bulletComp != null)
            {
                // 尝试弹反子弹
                bool parried = parrySystem.TryParryBullet(bullet, bulletComp.GetDirection());
                if (parried)
                {
                    Debug.Log("弹反检测器：成功弹反子弹");
                }
            }
        }
    }
}