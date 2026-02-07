using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RushBoss : BaseEnemy
{
    [Header("Boss状态设置")]
    [SerializeField] private float idleTime = 3f;              // 呆在原地时间
    [SerializeField] private float windupTime = 2f;           // 蓄力时间
    [SerializeField] private float dashTime = 1f;             // 冲撞时间
    [SerializeField] private float dashSpeed = 20f;           // 冲撞速度

    [Header("召唤设置")]
    [SerializeField] private GameObject minionPrefab;         // 小怪预制体
    [SerializeField] private int minionCount = 3;            // 召唤数量
    [SerializeField] private float summonRadius = 3f;        // 召唤半径
    [SerializeField] private Transform[] customSpawnPoints;  // 自定义生成点（可选）
    [SerializeField] private bool autoRespawnMinions = true; // 小怪死亡后自动重新召唤
    [SerializeField] private float respawnDelay = 2f;        // 重新召唤延迟

    [Header("冲撞设置")]
    [SerializeField] private float dashErrorAngle = 15f;      // 冲撞误差角度
    [SerializeField] private GameObject windupIndicator;      // 蓄力指示器（粗线）
    [SerializeField] private float indicatorLength = 5f;      // 指示器长度

    [Header("视觉模型")]
    [SerializeField] private GameObject idleModel;            // 呆在原地模型
    [SerializeField] private GameObject windupModel;         // 蓄力模型
    [SerializeField] private GameObject dashModel;           // 冲撞模型

    [Header("Boss UI设置")]
    public string bossName = "冲锋Boss";                     // Boss名称
    public Color bossUIColor = Color.red;                    // Boss UI颜色

    [Header("物理设置")]
    [SerializeField] private bool immovableInIdle = true;    // 静止时不可移动
    [SerializeField] private float bossMass = 100f;          // Boss质量（很重，难以推动）

    [Header("伤害间隔设置")]
    public float collisionDamageInterval = 0.5f;             // 碰撞伤害间隔
    private float collisionTimer = 0f;                       // 碰撞计时器

    [Header("攻击检测设置")]
    public float playerAttackDamage = 10f;                   // 玩家攻击默认伤害

    [Header("指示器平滑设置")]
    [SerializeField] private float indicatorSmoothSpeed = 5f; // 指示器平滑速度
    [SerializeField] private bool useSmoothRotation = true;   // 是否使用平滑旋转

    // 状态机
    private enum BossState
    {
        Idle,           // 呆在原地
        Windup,         // 蓄力
        Dashing         // 冲撞
    }

    private BossState currentState = BossState.Idle;
    private float stateTimer = 0f;
    private Vector2 dashDirection;

    // 小怪管理
    private List<GameObject> activeMinionList = new List<GameObject>();
    private Coroutine stateCoroutine;
    private Coroutine cleanupCoroutine;
    private Coroutine respawnCoroutine;  // 重新召唤协程

    // 召唤控制
    private bool hasSummonedThisCycle = false;

    // 指示器相关
    private Vector2 targetDashDirection;      // 目标冲撞方向
    private float randomAngleForDash;         // 保存随机误差角度

    protected override void Start()
    {
        // 关键：调用基类Start确保基础设置正确
        base.Start();

        // 确保所有必需组件
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!col) col = GetComponent<Collider2D>();

        // 关键：设置Boss物理属性，防止被玩家推动
        SetupPhysics();

        // 设置指示器
        if (windupIndicator != null)
        {
            // 确保指示器是Boss的子物体
            if (windupIndicator.transform.parent != transform)
            {
                windupIndicator.transform.SetParent(transform);
            }

            // 重置局部位置到中心
            windupIndicator.transform.localPosition = Vector3.zero;
            windupIndicator.transform.localRotation = Quaternion.identity;

            windupIndicator.SetActive(false);
        }

        // 初始化状态
        StartStateCycle();

        // 显示Boss血条
        ShowBossUI();

        // 启动清理协程
        if (cleanupCoroutine != null) StopCoroutine(cleanupCoroutine);
        cleanupCoroutine = StartCoroutine(CleanupInvalidMinionsRoutine());
    }

    // 关键方法：设置物理属性防止被推动
    private void SetupPhysics()
    {
        if (rb == null) return;

        rb.mass = bossMass; // 设置很大的质量，玩家很难推动
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; // 只冻结旋转

        // 根据状态决定是否冻结位置
        UpdatePhysicsConstraints();
    }

    // 根据状态更新物理约束
    private void UpdatePhysicsConstraints()
    {
        if (rb == null) return;

        switch (currentState)
        {
            case BossState.Idle:
            case BossState.Windup:
                // 静止和蓄力状态：冻结位置，不能被推动
                if (immovableInIdle)
                {
                    rb.constraints = RigidbodyConstraints2D.FreezeAll; // 冻结所有
                    rb.velocity = Vector2.zero; // 确保速度为0
                }
                break;

            case BossState.Dashing:
                // 冲撞状态：解除冻结，可以移动
                rb.constraints = RigidbodyConstraints2D.FreezeRotation; // 只冻结旋转
                break;
        }
    }

    protected override void Update()
    {
        if (!isAlive) return;

        base.Update();

        // 更新碰撞计时器
        if (collisionTimer > 0)
        {
            collisionTimer -= Time.deltaTime;
        }

        UpdateState();
    }

    protected override void UpdateEnemy()
    {
        // 状态更新逻辑在UpdateState中处理
    }

    void StartStateCycle()
    {
        if (stateCoroutine != null) StopCoroutine(stateCoroutine);
        stateCoroutine = StartCoroutine(StateCycle());
    }

    IEnumerator StateCycle()
    {
        hasSummonedThisCycle = false;

        while (isAlive)
        {
            // 1. 呆在原地状态
            SetIdleState();
            yield return new WaitForSeconds(idleTime);

            // 2. 蓄力状态
            SetWindupState();
            yield return new WaitForSeconds(windupTime);

            // 3. 冲撞状态
            SetDashState();
            yield return new WaitForSeconds(dashTime);

            // 冲撞结束后直接回到Idle状态
        }
    }

    private void UpdateState()
    {
        stateTimer += Time.deltaTime;

        // 更新蓄力指示器方向 - 修复抖动问题
        if (currentState == BossState.Windup && playerTransform)
        {
            UpdateWindupDirection();
        }
    }

    private void UpdateWindupDirection()
    {
        if (!playerTransform) return;

        Vector2 toPlayer = playerTransform.position - transform.position;

        bool shouldRecalculate =
            stateTimer < 0.1f || // 刚进入状态
            toPlayer.sqrMagnitude > 4f || // 玩家移动了较大距离
            Random.value < 0.01f; // 1%概率每帧（约每2秒一次）

        if (shouldRecalculate)
        {
            Vector2 baseDirection = toPlayer.normalized;

            if (stateTimer < 0.1f) // 只在开始时或定期重新随机
            {
                randomAngleForDash = Random.Range(-dashErrorAngle, dashErrorAngle);
            }

            targetDashDirection = Quaternion.Euler(0, 0, randomAngleForDash) * baseDirection;
        }

        if (useSmoothRotation)
        {
            dashDirection = Vector2.Lerp(dashDirection, targetDashDirection,
                Time.deltaTime * indicatorSmoothSpeed);
        }
        else
        {
            dashDirection = targetDashDirection;
        }

        if (windupIndicator != null && windupIndicator.activeSelf)
        {
            Quaternion targetRotation = Quaternion.LookRotation(Vector3.forward, dashDirection);

            if (useSmoothRotation)
            {
                windupIndicator.transform.rotation = Quaternion.Slerp(
                    windupIndicator.transform.rotation,
                    targetRotation,
                    Time.deltaTime * indicatorSmoothSpeed
                );
            }
            else
            {
                windupIndicator.transform.rotation = targetRotation;
            }

            SpriteRenderer sprite = windupIndicator.GetComponent<SpriteRenderer>();
            if (sprite != null)
            {
                windupIndicator.transform.localScale = new Vector3(
                    windupIndicator.transform.localScale.x,
                    indicatorLength,
                    windupIndicator.transform.localScale.z
                );
            }
        }
    }

    #region 状态切换方法

    private void SetIdleState()
    {
        // 1. 明确设置canTakeDamage
        canTakeDamage = true;

        // 2. 调用模型切换方法
        SwitchModel(idleModel);

        // 3. 设置其他状态相关参数
        currentState = BossState.Idle;
        stateTimer = 0f;
        rb.velocity = Vector2.zero;

        // 关键：更新物理约束
        UpdatePhysicsConstraints();

        // 修复：总是检查是否需要召唤小怪
        CheckAndSummonMinions();

        // 隐藏指示器
        if (windupIndicator)
            windupIndicator.SetActive(false);

        // 状态变化回调
        OnStateChanged(EnemyState.Idle);
    }

    private void SetWindupState()
    {
        // 1. 明确设置canTakeDamage
        canTakeDamage = true;

        // 2. 调用模型切换方法
        SwitchModel(windupModel);

        // 3. 设置其他状态相关参数
        currentState = BossState.Windup;
        stateTimer = 0f;
        rb.velocity = Vector2.zero;

        // 关键：更新物理约束
        UpdatePhysicsConstraints();

        // 重置方向相关变量
        dashDirection = Vector2.zero;
        targetDashDirection = Vector2.zero;

        // 显示蓄力指示器
        if (windupIndicator)
        {
            windupIndicator.SetActive(true);
            windupIndicator.transform.position = transform.position;

            if (playerTransform)
            {
                Vector2 toPlayer = playerTransform.position - transform.position;
                Vector2 baseDirection = toPlayer.normalized;

                randomAngleForDash = Random.Range(-dashErrorAngle, dashErrorAngle);
                targetDashDirection = Quaternion.Euler(0, 0, randomAngleForDash) * baseDirection;

                dashDirection = targetDashDirection;
                windupIndicator.transform.up = dashDirection;

                SpriteRenderer sprite = windupIndicator.GetComponent<SpriteRenderer>();
                if (sprite != null)
                {
                    windupIndicator.transform.localScale = new Vector3(
                        windupIndicator.transform.localScale.x,
                        indicatorLength,
                        windupIndicator.transform.localScale.z
                    );
                }
            }
        }

        // 状态变化回调
        OnStateChanged(EnemyState.Attack);
    }

    private void SetDashState()
    {
        // 1. 明确设置canTakeDamage
        canTakeDamage = true;

        // 2. 调用模型切换方法
        SwitchModel(dashModel);

        // 3. 设置其他状态相关参数
        currentState = BossState.Dashing;
        stateTimer = 0f;

        // 关键：更新物理约束（冲撞时可以移动）
        UpdatePhysicsConstraints();

        // 隐藏蓄力指示器
        if (windupIndicator)
            windupIndicator.SetActive(false);

        // 开始冲撞
        StartCoroutine(DashMovement());

        // 状态变化回调
        OnStateChanged(EnemyState.Attack);
    }

    private IEnumerator DashMovement()
    {
        // 冲撞移动
        rb.velocity = dashDirection * dashSpeed;
        yield return new WaitForSeconds(dashTime);

        // 冲撞结束
        EndDash();
    }

    private void EndDash()
    {
        rb.velocity = Vector2.zero;
        SetIdleState();
    }

    #endregion

    #region 小怪管理 - 修复召唤逻辑

    // 修复：检查并召唤小怪
    private void CheckAndSummonMinions()
    {
        // 只在Idle状态时召唤小怪
        if (currentState != BossState.Idle) return;

        int currentCount = GetValidMinionCount();

        // 修复：当小怪数量不足时，总是召唤
        if (currentCount < minionCount)
        {
            // 如果是第一次召唤或者已经召唤过但小怪不足
            if (!hasSummonedThisCycle || currentCount == 0)
            {
                SummonMinions();
                hasSummonedThisCycle = true;
            }
        }
    }

    private void SummonMinions()
    {
        // 先清理无效引用
        CleanInvalidMinionReferences();

        int currentCount = GetValidMinionCount();
        int needToSpawn = Mathf.Max(0, minionCount - currentCount);

        if (needToSpawn <= 0) return;

        Debug.Log($"需要召唤 {needToSpawn} 个小怪，当前有 {currentCount}/{minionCount}");

        for (int i = 0; i < needToSpawn; i++)
        {
            Vector2 spawnPosition = GetSpawnPosition(currentCount + i);
            GameObject minion = Instantiate(minionPrefab, spawnPosition, Quaternion.identity);
            minion.tag = "Enemy";
            activeMinionList.Add(minion);

            // 启动监控协程
            StartCoroutine(MonitorMinionLife(minion));
        }
    }

    private Vector2 GetSpawnPosition(int index)
    {
        if (customSpawnPoints != null && customSpawnPoints.Length > 0)
        {
            int pointIndex = index % customSpawnPoints.Length;
            return customSpawnPoints[pointIndex].position;
        }

        float angle = index * (360f / Mathf.Max(1, minionCount));
        Vector2 direction = Quaternion.Euler(0, 0, angle) * Vector2.up;
        return (Vector2)transform.position + direction * summonRadius;
    }

    private int GetValidMinionCount()
    {
        CleanInvalidMinionReferences();
        return activeMinionList.Count;
    }

    // 修复：监控小怪生命
    private IEnumerator MonitorMinionLife(GameObject minion)
    {
        if (minion == null) yield break;

        BaseEnemy minionEnemy = minion.GetComponent<BaseEnemy>();
        float checkInterval = 0.5f;

        while (minion != null && minionEnemy != null && minionEnemy.isAlive)
        {
            yield return new WaitForSeconds(checkInterval);
        }

        // 小怪死亡或被销毁
        OnMinionDied(minion);
    }

    // 修复：小怪死亡处理
    private void OnMinionDied(GameObject minion)
    {
        if (minion == null) return;

        // 从列表中移除
        if (activeMinionList.Contains(minion))
        {
            activeMinionList.Remove(minion);
            Debug.Log($"小怪死亡，剩余小怪: {activeMinionList.Count}/{minionCount}");
        }

        // 关键修复：小怪死亡后检查是否需要重新召唤
        if (autoRespawnMinions && isAlive)
        {
            // 延迟后重新召唤
            if (respawnCoroutine != null)
                StopCoroutine(respawnCoroutine);

            respawnCoroutine = StartCoroutine(RespawnMinionDelayed());
        }
    }

    // 延迟重新召唤小怪
    private IEnumerator RespawnMinionDelayed()
    {
        yield return new WaitForSeconds(respawnDelay);

        // 检查是否还在Idle状态且需要召唤
        if (isAlive && currentState == BossState.Idle)
        {
            int currentCount = GetValidMinionCount();
            if (currentCount < minionCount)
            {
                Debug.Log($"延迟重新召唤小怪，当前: {currentCount}/{minionCount}");
                SummonMinions();
            }
        }
    }

    private IEnumerator CleanupInvalidMinionsRoutine()
    {
        while (isAlive)
        {
            yield return new WaitForSeconds(2f);
            CleanInvalidMinionReferences();
        }
    }

    private void CleanInvalidMinionReferences()
    {
        for (int i = activeMinionList.Count - 1; i >= 0; i--)
        {
            GameObject minion = activeMinionList[i];

            if (minion == null)
            {
                activeMinionList.RemoveAt(i);
                Debug.Log($"清理了无效小怪引用");
                continue;
            }

            BaseEnemy enemy = minion.GetComponent<BaseEnemy>();
            if (enemy != null && !enemy.isAlive)
            {
                activeMinionList.RemoveAt(i);
                Debug.Log($"清理了死亡小怪引用");
            }
        }
    }

    #endregion

    

    #region 视觉模型切换

    private void SwitchModel(GameObject targetModel)
    {
        // 先隐藏所有模型
        HideAllModels();

        // 显示目标模型
        if (targetModel != null)
            targetModel.SetActive(true);
    }

    private void HideAllModels()
    {
        if (idleModel) idleModel.SetActive(false);
        if (windupModel) windupModel.SetActive(false);
        if (dashModel) dashModel.SetActive(false);
    }

    #endregion

    #region 碰撞检测和物理反应处理

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        base.OnCollisionEnter2D(collision);

        if (collision.gameObject.CompareTag("Player"))
        {
            if (collisionTimer <= 0)
            {
                HandlePlayerCollision(collision.gameObject);
            }

            if (currentState == BossState.Idle || currentState == BossState.Windup)
            {
                rb.velocity = Vector2.zero;
            }
        }
    }

    protected override void OnCollisionStay2D(Collision2D collision)
    {
        base.OnCollisionStay2D(collision);

        if (collision.gameObject.CompareTag("Player"))
        {
            if (currentState == BossState.Idle || currentState == BossState.Windup)
            {
                rb.velocity = Vector2.zero;
            }
        }
    }

    protected override void HandlePlayerCollision(GameObject player)
    {
        if (!isAlive || collisionTimer > 0) return;

        if (player != null)
        {
            PlayerController playerController = player.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.TakeDamage(collisionDamage);
            }

            if (playerEclipse != null)
            {
                playerEclipse.AddLayer(layerAddAmount);
            }

            collisionTimer = collisionDamageInterval;
        }
    }

    #endregion

    #region 伤害处理

    public override void TakeDamage(float damage)
    {
        if (!isAlive || !canTakeDamage) return;

        base.TakeDamage(damage);
        SafeUpdateBossUI();
    }

    #endregion

    #region Boss UI管理

    void ShowBossUI()
    {
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui != null)
        {
            ui.ShowBossHealthBar(bossName, currentHealth, maxHealth, bossUIColor);
        }
    }

    void SafeUpdateBossUI()
    {
        try
        {
            UIManager ui = FindObjectOfType<UIManager>();
            if (ui != null)
            {
                ui.UpdateBossHealth(currentHealth, maxHealth);
            }
        }
        catch (System.NullReferenceException e)
        {
            Debug.LogWarning($"更新Boss血条时出错: {e.Message}");
        }
    }

    void HideBossUI()
    {
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui != null) ui.HideBossHealthBar();
    }

    #endregion

    #region 抽象方法实现

    protected override void OnStateChanged(EnemyState newState)
    {
        // 状态变化回调
    }

    #endregion

    #region 重写基类方法

    protected override void OnDeath()
    {
        base.OnDeath();

        if (stateCoroutine != null)
            StopCoroutine(stateCoroutine);

        if (cleanupCoroutine != null)
            StopCoroutine(cleanupCoroutine);

        if (respawnCoroutine != null)
            StopCoroutine(respawnCoroutine);

        // 清理小怪
        foreach (GameObject minion in activeMinionList)
        {
            if (minion != null) Destroy(minion);
        }
        activeMinionList.Clear();

        HideBossUI();
    }

    #endregion

    #region 调试工具

    [ContextMenu("手动召唤小怪")]
    public void ManuallySummonMinions()
    {
        if (isAlive)
        {
            Debug.Log("手动召唤小怪");
            SummonMinions();
        }
    }

    [ContextMenu("检查小怪状态")]
    public void CheckMinionStatus()
    {
        int validCount = GetValidMinionCount();
        Debug.Log($"小怪状态 - 有效数量: {validCount}/{minionCount}, 列表长度: {activeMinionList.Count}");
        Debug.Log($"已召唤标记: {hasSummonedThisCycle}");
        Debug.Log($"当前状态: {currentState}");
    }

    void OnGUI()
    {
        if (isAlive)
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 16;
            style.normal.textColor = Color.white;

            GUI.Label(new Rect(10, 100, 300, 20),
                     $"Boss状态: {currentState}", style);
            GUI.Label(new Rect(10, 120, 300, 20),
                     $"生命: {currentHealth:F0}/{maxHealth:F0}", style);
            GUI.Label(new Rect(10, 140, 300, 20),
                     $"小怪: {GetValidMinionCount()}/{minionCount}", style);
            GUI.Label(new Rect(10, 160, 300, 20),
                     $"已召唤: {hasSummonedThisCycle}", style);
            GUI.Label(new Rect(10, 180, 300, 20),
                     $"自动召唤: {autoRespawnMinions}", style);
        }
    }

    #endregion
}