using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ChargeMasterBoss : BaseEnemy
{
    [Header("Boss状态设置")]
    public GameObject patrolStateObject;      // 巡逻状态物体
    public GameObject chargeStateObject;      // 蓄力冲撞状态物体
    public GameObject shootStateObject;       // 射击状态物体
    public GameObject staggerStateObject;     // 硬直状态物体

    [Header("巡逻设置")]
    public float patrolSpeed = 3f;           // 巡逻速度
    public Vector2 patrolRange = new Vector2(10f, 5f); // 巡逻范围
    public float patrolChangeTime = 2f;      // 改变巡逻方向的时间间隔

    [Header("蓄力冲撞设置")]
    public float chargeWindupTime = 1.5f;    // 蓄力时间
    public float chargeSpeed = 8f;           // 冲撞速度
    public float chargeDuration = 1.5f;      // 冲撞持续时间
    public float chargeCooldown = 3f;        // 冲撞冷却时间

    [Header("射击设置")]
    public GameObject bulletPrefab;          // 子弹预制体
    public int bulletsPerBurst = 2;          // 每次连发子弹数
    public float burstInterval = 0.3f;       // 连发间隔
    public float shootCooldown = 2f;         // 射击冷却时间

    [Header("子弹属性")]
    public float bulletSpeed = 7f;           // 子弹速度
    public float bulletDamage = 15f;         // 子弹伤害
    public float fastBulletSpeed = 10f;      // 快子弹速度
    public float slowBulletSpeed = 4f;       // 慢子弹速度

    [Header("快慢刀设置")]
    public bool useFastSlowPattern = true;   // 是否使用快慢刀模式
    [Range(0f, 1f)] public float fastSlowChance = 0.3f; // 快慢刀出现的概率
    public float fastSlowDelay = 0.5f;       // 快慢刀之间的延迟

    [Header("硬直设置")]
    public float staggerDuration = 1.5f;     // 硬直时间
    public float chargeStaggerTime = 1f;     // 冲撞后硬直时间
    public float reflectStaggerTime = 2f;    // 被弹反后的硬直时间

    [Header("伤害设置")]
    public float chargeDamage = 20f;         // 冲撞伤害
    public float reflectDamageMultiplier = 2f; // 弹反伤害倍率

    [Header("Boss UI设置")]
    public string bossName = "充能大师";      // Boss名称
    public Color bossUIColor = Color.magenta; // Boss UI颜色

    // Boss状态枚举
    private enum ChargeBossState
    {
        Patrol,           // 巡逻
        ChargeWindup,     // 蓄力冲撞准备
        Charge,           // 冲撞中
        ShootWindup,      // 射击准备
        Shoot,            // 射击中
        Stagger,          // 硬直
        Cooldown          // 冷却
    }

    // 状态相关
    private ChargeBossState currentState = ChargeBossState.Patrol;
    private Coroutine stateCoroutine;
    private Vector2 patrolTarget;
    private float patrolTimer = 0f;

    // 冷却计时器
    private float chargeCooldownTimer = 0f;
    private float shootCooldownTimer = 0f;

    // 玩家弹反相关
    private bool isVulnerableToReflect = false; // 是否处于可被弹反的状态
    private float vulnerabilityWindow = 0.3f;   // 可被弹反的时间窗口

    protected override void Start()
    {
        base.Start();
        SetStateObject();
        ShowBossUI();
        SetRandomPatrolTarget();
    }

    protected override void UpdateEnemy()
    {
        if (!isAlive) return;

        // 更新冷却计时器
        UpdateCooldowns();

        // 根据当前状态执行行为
        switch (currentState)
        {
            case ChargeBossState.Patrol:
                HandlePatrol();
                break;

            case ChargeBossState.Charge:
                HandleCharge();
                break;
        }
    }

    void UpdateCooldowns()
    {
        if (chargeCooldownTimer > 0)
            chargeCooldownTimer -= Time.deltaTime;

        if (shootCooldownTimer > 0)
            shootCooldownTimer -= Time.deltaTime;
    }

    void SetStateObject()
    {
        // 隐藏所有状态物体
        if (patrolStateObject) patrolStateObject.SetActive(false);
        if (chargeStateObject) chargeStateObject.SetActive(false);
        if (shootStateObject) shootStateObject.SetActive(false);
        if (staggerStateObject) staggerStateObject.SetActive(false);

        // 显示当前状态物体
        switch (currentState)
        {
            case ChargeBossState.Patrol:
            case ChargeBossState.Cooldown:
                if (patrolStateObject) patrolStateObject.SetActive(true);
                break;

            case ChargeBossState.ChargeWindup:
            case ChargeBossState.Charge:
                if (chargeStateObject) chargeStateObject.SetActive(true);
                break;

            case ChargeBossState.ShootWindup:
            case ChargeBossState.Shoot:
                if (shootStateObject) shootStateObject.SetActive(true);
                break;

            case ChargeBossState.Stagger:
                if (staggerStateObject) staggerStateObject.SetActive(true);
                break;
        }
    }

    #region 巡逻状态

    void SetRandomPatrolTarget()
    {
        // 在巡逻范围内随机选择一个位置
        float randomX = Random.Range(-patrolRange.x / 2, patrolRange.x / 2);
        float randomY = Random.Range(-patrolRange.y / 2, patrolRange.y / 2);
        patrolTarget = (Vector2)transform.position + new Vector2(randomX, randomY);
    }

    void HandlePatrol()
    {
        patrolTimer += Time.deltaTime;

        // 到达目标点或时间到了就重新选择目标
        if (Vector2.Distance(transform.position, patrolTarget) < 0.5f || patrolTimer > patrolChangeTime)
        {
            SetRandomPatrolTarget();
            patrolTimer = 0f;
        }

        // 向目标点移动
        Vector2 direction = (patrolTarget - (Vector2)transform.position).normalized;
        rb.velocity = direction * patrolSpeed;

        // 决定是否切换到攻击状态
        if (CanAttack())
        {
            ChooseAttack();
        }
    }

    bool CanAttack()
    {
        if (!playerTransform) return false;
        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        return (chargeCooldownTimer <= 0 || shootCooldownTimer <= 0) && distanceToPlayer < 15f;
    }

    void ChooseAttack()
    {
        if (chargeCooldownTimer <= 0 && shootCooldownTimer <= 0)
        {
            if (Random.value > 0.5f)
                StartChargeWindup();
            else
                StartShootWindup();
        }
        else if (chargeCooldownTimer <= 0)
        {
            StartChargeWindup();
        }
        else if (shootCooldownTimer <= 0)
        {
            StartShootWindup();
        }
    }

    #endregion

    #region 冲撞攻击

    void StartChargeWindup()
    {
        if (stateCoroutine != null) StopCoroutine(stateCoroutine);
        stateCoroutine = StartCoroutine(ChargeWindupRoutine());
    }

    IEnumerator ChargeWindupRoutine()
    {
        currentState = ChargeBossState.ChargeWindup;
        SetStateObject();
        rb.velocity = Vector2.zero;

        // 蓄力时间
        yield return new WaitForSeconds(chargeWindupTime);

        // 开始冲撞
        StartCharge();
    }

    void StartCharge()
    {
        if (stateCoroutine != null) StopCoroutine(stateCoroutine);
        stateCoroutine = StartCoroutine(ChargeRoutine());
    }

    IEnumerator ChargeRoutine()
    {
        currentState = ChargeBossState.Charge;
        SetStateObject();

        // 计算冲撞方向（指向玩家）
        Vector2 chargeDirection = playerTransform
            ? ((Vector2)playerTransform.position - (Vector2)transform.position).normalized
            : Vector2.right;

        // 开始冲撞
        float chargeTimer = 0f;
        while (chargeTimer < chargeDuration)
        {
            rb.velocity = chargeDirection * chargeSpeed;
            chargeTimer += Time.deltaTime;
            yield return null;
        }

        // 冲撞结束，进入硬直
        rb.velocity = Vector2.zero;
        StartStagger(chargeStaggerTime);
        chargeCooldownTimer = chargeCooldown;
    }

    void HandleCharge()
    {
        // 冲撞中的处理（已经在协程中控制）
    }

    #endregion

    #region 射击攻击

    void StartShootWindup()
    {
        if (stateCoroutine != null) StopCoroutine(stateCoroutine);
        stateCoroutine = StartCoroutine(ShootWindupRoutine());
    }

    IEnumerator ShootWindupRoutine()
    {
        currentState = ChargeBossState.ShootWindup;
        SetStateObject();
        rb.velocity = Vector2.zero;

        // 短暂准备时间
        yield return new WaitForSeconds(0.5f);

        // 开始射击
        StartShooting();
    }

    void StartShooting()
    {
        if (stateCoroutine != null) StopCoroutine(stateCoroutine);
        stateCoroutine = StartCoroutine(ShootingRoutine());
    }

    IEnumerator ShootingRoutine()
    {
        currentState = ChargeBossState.Shoot;
        SetStateObject();

        // 进入可被弹反状态
        isVulnerableToReflect = true;
        yield return new WaitForSeconds(vulnerabilityWindow);
        isVulnerableToReflect = false;

        // 决定使用哪种射击模式
        bool useFastSlow = useFastSlowPattern && Random.value < fastSlowChance;

        if (useFastSlow)
        {
            // 快慢刀模式
            yield return StartCoroutine(ShootFastSlowBurst());
        }
        else
        {
            // 正常连发模式
            for (int i = 0; i < bulletsPerBurst; i++)
            {
                ShootBullet(bulletSpeed);
                yield return new WaitForSeconds(burstInterval);
            }
        }

        // 射击结束，进入冷却
        StartStagger(0.5f);
        shootCooldownTimer = shootCooldown;
    }

    IEnumerator ShootFastSlowBurst()
    {
        // 第一发快弹
        ShootBullet(fastBulletSpeed, true);

        // 延迟后发射慢弹
        yield return new WaitForSeconds(fastSlowDelay);

        // 再次进入可被弹反状态（针对慢弹）
        isVulnerableToReflect = true;
        yield return new WaitForSeconds(vulnerabilityWindow);
        isVulnerableToReflect = false;

        // 第二发慢弹
        ShootBullet(slowBulletSpeed, false);
    }

    void ShootBullet(float speed, bool isFast = false)
    {
        if (!bulletPrefab || !playerTransform) return;

        // 计算射击方向
        Vector2 direction = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;

        // 创建子弹
        GameObject bulletObj = Instantiate(bulletPrefab, transform.position, Quaternion.identity);

        // 添加子弹组件（使用BossBullet脚本）
        BossBullet bullet = bulletObj.GetComponent<BossBullet>();
        if (!bullet) bullet = bulletObj.AddComponent<BossBullet>();

        // 初始化子弹
        bullet.Initialize(direction, speed, bulletDamage, this, isFast);
    }

    #endregion

    #region 硬直状态

    void StartStagger(float duration)
    {
        if (stateCoroutine != null) StopCoroutine(stateCoroutine);
        stateCoroutine = StartCoroutine(StaggerRoutine(duration));
    }

    IEnumerator StaggerRoutine(float duration)
    {
        currentState = ChargeBossState.Stagger;
        SetStateObject();
        rb.velocity = Vector2.zero;

        // 硬直时间
        yield return new WaitForSeconds(duration);

        // 短暂冷却后回到巡逻
        StartCooldown(0.5f);
    }

    void StartCooldown(float duration)
    {
        if (stateCoroutine != null) StopCoroutine(stateCoroutine);
        stateCoroutine = StartCoroutine(CooldownRoutine(duration));
    }

    IEnumerator CooldownRoutine(float duration)
    {
        currentState = ChargeBossState.Cooldown;
        SetStateObject();

        yield return new WaitForSeconds(duration);

        // 回到巡逻状态
        currentState = ChargeBossState.Patrol;
        SetStateObject();
        patrolTimer = 0f;
        SetRandomPatrolTarget();
    }

    #endregion

    #region 弹反相关

    // 添加这个方法来解决错误
    public void OnBulletParried(BossBullet bullet, int playerLayers)
    {
        if (!isAlive) return;

        // 计算弹反伤害
        float reflectDamage = bulletDamage * reflectDamageMultiplier * (1 + playerLayers * 0.1f);

        // 对Boss造成伤害
        TakeDamage(reflectDamage);

        // 如果处于可被弹反状态，进入更长的硬直
        if (isVulnerableToReflect)
        {
            StartStagger(reflectStaggerTime);

            // 显示弹反成功文字
            UIManager ui = FindObjectOfType<UIManager>();
            if (ui)
            {
                // 尝试使用反射调用ShowText方法，或者使用其他方法
                var showTextMethod = ui.GetType().GetMethod("ShowText");
                if (showTextMethod != null)
                {
                    showTextMethod.Invoke(ui, new object[] { transform.position, "完美弹反!", Color.yellow });
                }
                else
                {
                    // 如果没有ShowText方法，使用现有的显示伤害方法
                    ui.ShowPlayerDealDamageText(transform.position, (int)reflectDamage, Color.yellow);
                }
            }
        }

        Debug.Log($"子弹被弹反，BOSS受到 {reflectDamage} 伤害，玩家层数: {playerLayers}");
    }

    #endregion

    #region 碰撞处理

    protected override void HandlePlayerCollision(GameObject player)
    {
        if (!isAlive || !canTakeDamage) return;

        PlayerController playerCtrl = player.GetComponent<PlayerController>();
        EclipseSystem eclipse = player.GetComponent<EclipseSystem>();

        if (playerCtrl && eclipse)
        {
            // 根据状态造成不同伤害和层数
            float damage = currentState == ChargeBossState.Charge ? chargeDamage : collisionDamage;
            int layersToAdd = currentState == ChargeBossState.Charge ? 3 : layerAddAmount;

            // 造成伤害并添加层数
            playerCtrl.TakeDamage(damage);
            eclipse.AddLayer(layersToAdd);

            Debug.Log($"Boss对玩家造成 {damage} 伤害，添加 {layersToAdd} 层");
        }
    }

    #endregion

    #region 重写BaseEnemy方法

    public override void TakeDamage(float damage)
    {
        if (!canTakeDamage) return;
        base.TakeDamage(damage);
        UpdateBossUI();
    }

    protected override void OnDeath()
    {
        base.OnDeath();

        if (stateCoroutine != null)
            StopCoroutine(stateCoroutine);

        HideBossUI();
    }

    protected override void OnStateChanged(EnemyState newState)
    {
        // 简单实现
    }

    #endregion

    #region Boss UI管理

    void ShowBossUI()
    {
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui) ui.ShowBossHealthBar(bossName, currentHealth, maxHealth, bossUIColor);
    }

    void UpdateBossUI()
    {
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui) ui.UpdateBossHealth(currentHealth, maxHealth);
    }

    void HideBossUI()
    {
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui) ui.HideBossHealthBar();
    }

    #endregion

    #region 调试和编辑

    void OnDrawGizmosSelected()
    {
        // 绘制巡逻范围
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireCube(transform.position, new Vector3(patrolRange.x, patrolRange.y, 0));

        // 绘制当前状态指示
        Gizmos.color = GetStateColor();
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.5f);
    }

    Color GetStateColor()
    {
        switch (currentState)
        {
            case ChargeBossState.Patrol: return Color.green;
            case ChargeBossState.ChargeWindup: return Color.yellow;
            case ChargeBossState.Charge: return Color.red;
            case ChargeBossState.ShootWindup: return Color.blue;
            case ChargeBossState.Shoot: return Color.cyan;
            case ChargeBossState.Stagger: return Color.magenta;
            case ChargeBossState.Cooldown: return Color.gray;
            default: return Color.white;
        }
    }

    #endregion
}