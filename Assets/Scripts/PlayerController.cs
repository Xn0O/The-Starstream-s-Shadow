using UnityEngine;

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

    [Header("受伤震动设置")]
    public float damageShakeDuration = 0.15f;
    public float damageShakeMagnitude = 0.1f;
    [Header("大伤害震动设置")]
    public float heavyDamageShakeDuration = 0.25f;
    public float heavyDamageShakeMagnitude = 0.2f;
    public float heavyDamageThreshold = 10f; // 触发大伤害震动的伤害阈值

    private Rigidbody2D rb;
    private ScreenEffectManager screenEffectManager;

    // 记录最后一次有效的方向
    public enum Direction { None, Up, Down, Left, Right }
    private Direction lastValidDirection = Direction.Down; // 默认设为向下

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;

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
    }

    void Update()
    {
        // 移动输入
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        // 移动
        Vector2 moveDirection = new Vector2(moveX, moveY).normalized;
        rb.velocity = moveDirection * moveSpeed;

        // 更新图片显示
        UpdateDirectionPics(moveDirection);
    }

    // 更新方向图片显示
    void UpdateDirectionPics(Vector2 moveDirection)
    {
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
        lastValidDirection = direction;
        ShowDirectionPic(direction);
    }

    // 被敌人攻击时调用
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        Debug.Log($"玩家受到伤害: {damage}, 当前生命: {currentHealth}");

        // 调用UI显示伤害数字
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui)
        {
            ui.ShowDamageText(transform.position, damage, Color.red);
        }

        // 添加受伤震动反馈
        ApplyDamageShake(damage);
    }

    // 治疗
    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

        Debug.Log($"玩家治疗: {amount}, 当前生命: {currentHealth}");

        // 调用UI显示治疗数字
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui)
        {
            ui.ShowHealText(transform.position, amount, Color.green);
        }
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

    // 调试方法：在编辑器中测试方向显示
    [ContextMenu("测试向上显示")]
    public void TestUpDirection()
    {
        SetDirection(Direction.Up);
    }

    [ContextMenu("测试向下显示")]
    public void TestDownDirection()
    {
        SetDirection(Direction.Down);
    }

    [ContextMenu("测试向左显示")]
    public void TestLeftDirection()
    {
        SetDirection(Direction.Left);
    }

    [ContextMenu("测试向右显示")]
    public void TestRightDirection()
    {
        SetDirection(Direction.Right);
    }

    [ContextMenu("隐藏所有方向")]
    public void DebugHideAllDirections()
    {
        SetDirection(Direction.None);
    }

    [ContextMenu("重置为初始方向")]
    public void ResetToInitialDirection()
    {
        SetDirection(initialDirection);
    }
}