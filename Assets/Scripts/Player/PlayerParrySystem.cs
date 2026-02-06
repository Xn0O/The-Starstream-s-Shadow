// PlayerParrySystem.cs - 修复后的独立弹反系统
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerParrySystem : MonoBehaviour
{
    [Header("弹反设置")]
    public KeyCode parryKey = KeyCode.J;        // 弹反按键
    public float parryWindow = 0.2f;            // 弹反窗口时间
    public float parryRange = 2f;               // 弹反范围
    public GameObject parryEffect;              // 弹反特效

    [Header("冷却设置")]
    public float parryCooldown = 0.3f;          // 弹反冷却时间
    private float currentCooldown = 0f;

    [Header("奖励设置")]
    public float healPerLayerOnParry = 1f;      // 弹反成功每层恢复的生命值百分比
    public int layersToClearOnParry = 3;        // 弹反成功清除的层数

    // 组件引用
    private EclipseSystem eclipseSystem;
    private ReleaseSystem releaseSystem;
    private PlayerController playerController;

    // 弹反状态
    private bool isParryActive = false;
    private float parryTimer = 0f;
    private GameObject currentParriedBullet = null;

    void Start()
    {
        eclipseSystem = GetComponent<EclipseSystem>();
        releaseSystem = GetComponent<ReleaseSystem>();
        playerController = GetComponent<PlayerController>();

        if (!eclipseSystem)
            Debug.LogError("PlayerParrySystem需要EclipseSystem组件！");
        if (!releaseSystem)
            Debug.LogWarning("未找到ReleaseSystem，弹反失败时将不会释放能量");
    }

    void Update()
    {
        // 更新冷却
        if (currentCooldown > 0)
            currentCooldown -= Time.deltaTime;

        // 更新弹反窗口计时
        if (isParryActive)
        {
            parryTimer += Time.deltaTime;
            if (parryTimer > parryWindow)
            {
                isParryActive = false;
                parryTimer = 0f;
            }
        }

        // 检测弹反输入
        if (Input.GetKeyDown(parryKey) && currentCooldown <= 0)
        {
            StartParry();
        }
    }

    void StartParry()
    {
        isParryActive = true;
        parryTimer = 0f;

        // 播放弹反特效
        PlayParryEffect();

        // 开始冷却
        currentCooldown = parryCooldown;

        Debug.Log("弹反启动，窗口时间: " + parryWindow + "秒");
    }

    // 这个方法会被子弹调用
    public bool TryParryBullet(GameObject bullet, Vector2 bulletDirection)
    {
        if (!isParryActive || currentCooldown > 0)
            return false;

        // 计算弹反方向（指向最近的敌人）
        Transform nearestEnemy = FindNearestEnemy();
        Vector2 reflectDirection;

        if (nearestEnemy)
        {
            reflectDirection = (nearestEnemy.position - transform.position).normalized;
        }
        else
        {
            // 如果没有敌人，反弹回原来的方向
            reflectDirection = -bulletDirection.normalized;
        }

        // 获取子弹组件并反射
        BossBullet bossBullet = bullet.GetComponent<BossBullet>();
        if (bossBullet != null)
        {
            int playerLayers = eclipseSystem.CurrentLayer;
            bossBullet.Reflect(reflectDirection, playerLayers, this);
            currentParriedBullet = bullet;
            return true;
        }

        // 检查其他类型的子弹
        BaseBullet baseBullet = bullet.GetComponent<BaseBullet>();
        if (baseBullet != null)
        {
            int playerLayers = eclipseSystem.CurrentLayer;
            baseBullet.Reflect(reflectDirection, playerLayers, this);
            currentParriedBullet = bullet;
            return true;
        }

        return false;
    }

    // 当弹反的子弹击中敌人时调用
    public void OnParriedBulletHitEnemy(int damageDealt, GameObject enemy)
    {
        Debug.Log("弹反成功！子弹击中敌人，造成伤害: " + damageDealt);

        // 给予奖励
        GiveParryReward();

        // 显示成功提示
        ShowParrySuccessText();
    }

    void GiveParryReward()
    {
        if (!playerController || !eclipseSystem) return;

        // 恢复生命值
        int currentLayers = eclipseSystem.CurrentLayer;
        float healAmount = playerController.maxHealth * (healPerLayerOnParry / 100f) * currentLayers;
        playerController.Heal(healAmount);

        // 清除层数
        eclipseSystem.RemoveLayer(layersToClearOnParry);

        Debug.Log($"弹反奖励：恢复 {healAmount} 生命，清除 {layersToClearOnParry} 层");
    }

    Transform FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform nearest = null;
        float nearestDistance = Mathf.Infinity;

        foreach (var enemy in enemies)
        {
            float distance = Vector2.Distance(transform.position, enemy.transform.position);
            if (distance < nearestDistance)
            {
                nearest = enemy.transform;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    void PlayParryEffect()
    {
        if (!parryEffect) return;

        GameObject effect = Instantiate(parryEffect, transform.position, Quaternion.identity);
        Destroy(effect, 1f);
    }

    void ShowParrySuccessText()
    {
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui)
        {
            // 尝试使用ShowText方法
            var method = ui.GetType().GetMethod("ShowText");
            if (method != null)
            {
                method.Invoke(ui, new object[] { transform.position, "弹反成功!", Color.yellow });
            }
        }
    }

    // 如果弹反窗口内没有子弹，调用释放能量
    public void OnParryWindowEnd()
    {
        if (currentParriedBullet == null && eclipseSystem.CurrentLayer > 0)
        {
            // 弹反失败，释放能量
            if (releaseSystem != null)
            {
                releaseSystem.ReleaseEnergy();
                Debug.Log("弹反失败，释放能量攻击");
            }
        }

        currentParriedBullet = null;
        isParryActive = false;
    }

    // 调试用
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, parryRange);

        if (isParryActive)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, parryRange * 1.2f);
        }
    }
}