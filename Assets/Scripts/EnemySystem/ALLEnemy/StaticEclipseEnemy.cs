using UnityEngine;

// 静止星蚀怪 - 新手教程专用（适配PlayerController+EclipseSystem+Boss血条显示）
[RequireComponent(typeof(Collider2D))]
public class StaticEclipseEnemy : BaseEnemy
{
    [Header("静止怪专属设置")]
    [Tooltip("对玩家造成伤害的间隔时间（秒）")]
    [SerializeField] private float damageInterval = 2f; // 每2秒触发一次
    [Tooltip("每次伤害时给玩家增加的星蚀层数")]
    [SerializeField] private int eclipseLayerAdd = 1; // 每次加1层

    [Header("血条显示配置（复用Boss血条UI）")]
    [Tooltip("小怪在血条上显示的名称")]
    [SerializeField] private string enemyName = "教程小怪";
    [Tooltip("血条填充颜色")]
    [SerializeField] private Color healthBarColor = Color.green; // 默认绿色

    // 私有变量
    private float _lastDamageTime; // 上一次造成伤害的时间戳
    private UIManager _uiManager; // 缓存UIManager引用，避免重复查找

    #region 核心抽象方法实现
    // 静止怪的更新逻辑：空实现（因为完全不动）
    protected override void UpdateEnemy()
    {
        // 静止怪不需要任何移动/行为更新，留空即可
    }

    // 处理玩家碰撞逻辑（核心：冷却控制+伤害+星蚀层数）
    protected override void HandlePlayerCollision(GameObject player)
    {
        // 1. 检查是否满足伤害条件：存活 + 冷却时间已到 + 找到玩家星蚀系统
        if (!isAlive || Time.time - _lastDamageTime < damageInterval || playerEclipse == null)
        {
            return; // 不满足条件则直接返回
        }

        // 2. 更新最后一次伤害时间（重置冷却）
        _lastDamageTime = Time.time;

        // 调用PlayerController的受伤方法
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.TakeDamage(collisionDamage);
            Debug.Log($"静止怪对玩家造成 {collisionDamage} 点伤害！当前玩家生命值：{playerController.currentHealth}");
        }

        // 调用EclipseSystem的AddLayer方法积累星蚀层数
        playerEclipse.AddLayer(eclipseLayerAdd);
        Debug.Log($"玩家星蚀层数+{eclipseLayerAdd}，当前层数：{playerEclipse.CurrentLayer}");
    }

    // 状态改变逻辑：静止怪无状态变化，空实现
    protected override void OnStateChanged(EnemyState newState)
    {
        // 静止怪不需要状态切换，留空即可
    }
    #endregion

    #region 生命周期重写（初始化+血条显示）
    protected override void Awake()
    {
        base.Awake();

        // 强制设置刚体为Kinematic（静止怪不需要物理移动）
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.gravityScale = 0; // 取消重力，防止掉落
        }

        // 建议将碰撞器设为触发器（更适合范围检测）
        if (col != null)
        {
            col.isTrigger = true; // 触发器不会有物理碰撞，只检测范围
        }

        // 缓存UIManager引用（只查找一次，优化性能）
        _uiManager = FindObjectOfType<UIManager>();
        if (_uiManager == null)
        {
            Debug.LogWarning("未找到UIManager，小怪血条无法显示！");
        }
    }

    protected override void Start()
    {
        base.Start();
        // 初始化冷却时间：游戏开始时立即可以触发第一次伤害
        _lastDamageTime = -damageInterval;

        // 【关键1】初始化并显示小怪的血条（复用Boss血条UI）
        if (_uiManager != null)
        {
            _uiManager.ShowBossHealthBar(enemyName, currentHealth, maxHealth, healthBarColor);
        }
    }
    #endregion

    #region 重写父类方法（更新/隐藏血条）
    // 【关键2】重写TakeDamage，受伤害时更新血条
    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage); // 先执行父类的受伤逻辑（扣血、特效等）

        // 受伤后更新血条显示
        if (_uiManager != null && isAlive)
        {
            _uiManager.UpdateBossHealth(currentHealth, maxHealth);
        }
    }

    // 【关键3】重写Die，死亡时隐藏血条
    protected override void Die()
    {
        base.Die(); // 先执行父类的死亡逻辑（禁用碰撞、播放特效等）

        // 死亡后隐藏血条
        if (_uiManager != null)
        {
            _uiManager.HideBossHealthBar();
        }
    }
    #endregion
}