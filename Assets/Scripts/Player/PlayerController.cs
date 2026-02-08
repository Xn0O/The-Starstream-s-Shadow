using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 5f;
    public GameObject UpPic;
    public GameObject DownPic;
    public GameObject LeftPic;
    public GameObject RightPic;

    [Header("方向检测设置")]
    public float directionThreshold = 0.3f; // 方向检测阈值

    [Header("方向优先级")]
    public bool verticalPriority = true; // true:垂直优先, false:水平优先

    [Header("初始方向")]
    public Direction initialDirection = Direction.Down; // 初始显示的方向

    [Header("生命值")]
    public float maxHealth = 100f;
    public float currentHealth;
    public bool isDead = false; // 新增：死亡状态标志

    [Header("受伤震动设置")]
    public float damageShakeDuration = 0.15f;
    public float damageShakeMagnitude = 0.1f;

    [Header("大伤害震动设置")]
    public float heavyDamageShakeDuration = 0.25f;
    public float heavyDamageShakeMagnitude = 0.2f;
    public float heavyDamageThreshold = 10f; // 触发大伤害震动的伤害阈值

    [Header("受击特效")]
    public GameObject hitEffectPrefab; // 受击特效预制体
    public Vector3 hitEffectOffset = new Vector3(0, 0, 0); // 特效位置偏移
    public Transform hitEffectSpawnPoint; // 特效生成点（可选，如果不设置则使用玩家位置）

    [Header("死亡设置")]
    public GameObject deathObjectPrefab; // 死亡时替换的GameObject（可选）
    public GameObject deathUI; // 死亡界面UI（可选，直接在场景中拖入）
    public float deathDelay = 2f; // 死亡后延迟重新加载的时间

    private Rigidbody2D rb;
    private ScreenEffectManager screenEffectManager;
    private GameObject currentHitEffect; // 当前正在播放的受击特效

    // 记录最后一次有效的方向
    public enum Direction { None, Up, Down, Left, Right }
    private Direction lastValidDirection = Direction.Down; // 默认设为向下

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;
        isDead = false; // 确保开始时不是死亡状态

        // 初始化最后有效方向为初始方向
        lastValidDirection = initialDirection;

        // 初始显示向下的图
        ShowDirectionPic(initialDirection);

        // 获取屏幕效果管理器
        screenEffectManager = FindObjectOfType<ScreenEffectManager>();
        if (screenEffectManager == null)
        {
            Debug.LogWarning("未找到ScreenEffectManager，受伤震动效果将不可用");
        }

        // 隐藏死亡UI（如果存在）
        if (deathUI != null)
        {
            deathUI.SetActive(false);
        }
    }

    void Update()
    {
        // 如果玩家死亡，禁止移动输入
        if (isDead)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        // 移动输入
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        // 移动
        Vector2 moveDirection = new Vector2(moveX, moveY).normalized;
        rb.velocity = moveDirection * moveSpeed;

        // 更新图片显示
        UpdateDirectionPics(moveDirection);
    }

    // 被敌人攻击时调用
    public void TakeDamage(float damage)
    {
        // 如果已经死亡，不再受到伤害
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        Debug.Log($"玩家受到伤害: {damage}, 当前生命: {currentHealth}");

        // 调用UI显示伤害数字并统计玩家受伤害
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui)
        {
            ui.ShowPlayerTakenDamageText(transform.position, damage, Color.red);
        }

        // 添加受伤震动反馈
        ApplyDamageShake(damage);

        // 如果伤害值达到大伤害阈值，播放受击特效
        if (damage >= heavyDamageThreshold)
        {
            PlayHitEffect();
        }

        // 检查生命值是否小于等于0
        if (currentHealth <= 0 && !isDead)
        {
            Die();
        }
    }

    // 玩家死亡处理
    private void Die()
    {
        isDead = true;
        Debug.Log("玩家死亡！");

        // 停止移动
        rb.velocity = Vector2.zero;

        // 更换死亡GameObject（如果设置了）
        if (deathObjectPrefab != null)
        {
            GameObject deathObj = Instantiate(deathObjectPrefab, transform.position, Quaternion.identity);
            deathObj.name = "PlayerDeathObject";
        }

        // 隐藏玩家贴图
        HideAllDirectionPics();

        // 禁用玩家控制
        DisablePlayerControl();

        // 显示死亡界面（如果设置了）
        if (deathUI != null)
        {
            deathUI.SetActive(true);
        }

        // 延迟重新加载场景
        Invoke("ReloadCurrentScene", deathDelay);
    }

    // 禁用玩家控制
    private void DisablePlayerControl()
    {
        // 禁用Rigidbody2D
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.simulated = false; // 停止物理模拟
        }

        // 禁用所有碰撞器
        var colliders = GetComponents<Collider2D>();
        foreach (var collider in colliders)
        {
            collider.enabled = false;
        }

        Debug.Log("玩家控制已禁用");
    }

    // 重新加载当前场景
    private void ReloadCurrentScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // 治疗
    public void Heal(float amount)
    {
        // 如果已经死亡，不能治疗
        if (isDead) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

        Debug.Log($"玩家治疗: {amount}, 当前生命: {currentHealth}");

        // 调用UI显示治疗数字
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui)
        {
            ui.ShowHealText(transform.position, amount, Color.green);
        }
    }

    // 更新方向图片显示
    void UpdateDirectionPics(Vector2 moveDirection)
    {
        // 如果死亡，不更新方向
        if (isDead) return;

        // 检测移动方向
        bool isMovingUp = moveDirection.y > directionThreshold;
        bool isMovingDown = moveDirection.y < -directionThreshold;
        bool isMovingLeft = moveDirection.x < -directionThreshold;
        bool isMovingRight = moveDirection.x > directionThreshold;

        // 如果有移动输入
        if (moveDirection.magnitude > directionThreshold)
        {
            if (verticalPriority)
            {
                // 垂直优先
                if (isMovingUp)
                {
                    ShowDirectionPic(Direction.Up);
                }
                else if (isMovingDown)
                {
                    ShowDirectionPic(Direction.Down);
                }
                else if (isMovingLeft)
                {
                    ShowDirectionPic(Direction.Left);
                }
                else if (isMovingRight)
                {
                    ShowDirectionPic(Direction.Right);
                }
            }
            else
            {
                // 水平优先
                if (isMovingLeft)
                {
                    ShowDirectionPic(Direction.Left);
                }
                else if (isMovingRight)
                {
                    ShowDirectionPic(Direction.Right);
                }
                else if (isMovingUp)
                {
                    ShowDirectionPic(Direction.Up);
                }
                else if (isMovingDown)
                {
                    ShowDirectionPic(Direction.Down);
                }
            }
        }
        else
        {
            // 没有移动输入，保持最后一次有效的方向
            KeepLastDirection();
        }
    }

    // 显示指定方向的贴图
    void ShowDirectionPic(Direction direction)
    {
        // 如果死亡，不显示方向贴图
        if (isDead) return;

        // 隐藏所有贴图
        HideAllDirectionPics();

        // 显示指定方向的贴图
        switch (direction)
        {
            case Direction.Up:
                if (UpPic != null) UpPic.SetActive(true);
                break;
            case Direction.Down:
                if (DownPic != null) DownPic.SetActive(true);
                break;
            case Direction.Left:
                if (LeftPic != null) LeftPic.SetActive(true);
                break;
            case Direction.Right:
                if (RightPic != null) RightPic.SetActive(true);
                break;
        }

        // 记录当前方向
        lastValidDirection = direction;
    }

    // 保持最后一次的方向
    void KeepLastDirection()
    {
        if (isDead) return;

        if (lastValidDirection != Direction.None)
        {
            ShowDirectionPic(lastValidDirection);
        }
        else
        {
            // 如果从来没有过有效方向，显示初始方向
            ShowDirectionPic(initialDirection);
        }
    }

    // 隐藏所有方向贴图
    void HideAllDirectionPics()
    {
        if (UpPic != null) UpPic.SetActive(false);
        if (DownPic != null) DownPic.SetActive(false);
        if (LeftPic != null) LeftPic.SetActive(false);
        if (RightPic != null) RightPic.SetActive(false);
    }

    // 强制显示指定方向（可以从外部调用）
    public void SetDirection(Direction direction)
    {
        if (isDead) return;

        lastValidDirection = direction;
        ShowDirectionPic(direction);
    }

    // 播放受击特效（简化版）
    private void PlayHitEffect()
    {
        // 如果已经有一个受击特效在播放，先销毁它
        if (currentHitEffect != null)
        {
            Destroy(currentHitEffect);
        }

        // 检查是否有受击特效预制体
        if (hitEffectPrefab == null)
        {
            Debug.LogWarning("受击特效预制体未分配，请在Inspector中分配Hit Effect Prefab");
            return;
        }

        // 确定特效生成位置
        Vector3 spawnPosition;
        if (hitEffectSpawnPoint != null)
        {
            // 使用指定的生成点
            spawnPosition = hitEffectSpawnPoint.position + hitEffectOffset;
        }
        else
        {
            // 使用玩家位置加上偏移
            spawnPosition = transform.position + hitEffectOffset;
        }

        // 实例化受击特效（特效会自动销毁）
        currentHitEffect = Instantiate(hitEffectPrefab, spawnPosition, Quaternion.identity);

        Debug.Log("播放受击特效");
    }

    // 应用受伤震动效果
    private void ApplyDamageShake(float damage)
    {
        if (screenEffectManager == null) return;

        // 检查是否为大伤害
        if (damage >= heavyDamageThreshold)
        {
            // 使用大伤害震动效果
            ApplyHeavyDamageShake(damage);
        }
        else
        {
            // 使用普通伤害震动效果
            ApplyNormalDamageShake(damage);
        }
    }

    // 应用普通伤害震动效果
    private void ApplyNormalDamageShake(float damage)
    {
        // 根据伤害值调整震动强度
        float intensityMultiplier = Mathf.Clamp(damage / 30f, 0.3f, 1.5f);
        float actualShakeMagnitude = damageShakeMagnitude * intensityMultiplier;
        float actualShakeDuration = damageShakeDuration * Mathf.Clamp(intensityMultiplier, 0.8f, 1.5f);

        // 应用屏幕震动
        screenEffectManager.ShakeCamera(actualShakeDuration, actualShakeMagnitude);
    }

    // 应用大伤害震动效果
    private void ApplyHeavyDamageShake(float damage)
    {
        // 根据伤害值调整大伤害震动强度
        float intensityMultiplier = Mathf.Clamp(damage / 50f, 0.8f, 2.5f);
        float actualShakeMagnitude = heavyDamageShakeMagnitude * intensityMultiplier;
        float actualShakeDuration = heavyDamageShakeDuration * Mathf.Clamp(intensityMultiplier, 1f, 2f);

        // 应用更强的屏幕震动
        screenEffectManager.ShakeCamera(actualShakeDuration, actualShakeMagnitude);

        // 添加红色闪光效果
        Color flashColor = new Color(1f, 0.2f, 0.2f, 0.5f); // 红色闪光，更高的透明度
        float flashDuration = 0.2f + (damage * 0.005f); // 根据伤害调整闪光持续时间
        screenEffectManager.FlashScreen(flashColor, flashDuration);

        Debug.Log($"大伤害震动: 伤害={damage}, 震动强度={actualShakeMagnitude}, 持续时间={actualShakeDuration}");
    }

    // 调试方法
    [ContextMenu("测试死亡")]
    public void TestDeath()
    {
        TakeDamage(currentHealth + 1); // 受到超过当前生命的伤害
    }
}