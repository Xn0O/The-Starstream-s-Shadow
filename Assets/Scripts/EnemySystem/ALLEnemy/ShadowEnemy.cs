using UnityEngine;
using System.Collections;

public class ShadowEnemy : BaseEnemy
{
    [Header("移动设置")]
    public float patrolSpeed = 2f;
    public float chargeSpeed = 8f;
    public float patrolRadius = 5f;

    [Header("攻击设置")]
    public float chargeTime = 1f;
    public float attackDuration = 0.5f;
    public float cooldownDuration = 2f;
    public float detectionRange = 8f;

    [Header("伤害设置")]
    public float CollisionDamgeTime = 0.25f; // 碰撞伤害间隔时间

    [Header("击退设置")]
    public float knockbackForce = 5f;      // 被玩家击退的力
    public float knockbackDuration = 0.2f; // 击退持续时间
    public float stunDuration = 0.1f;      // 击退后的硬直时间
    private bool isKnockedBack = false;    // 是否正在被击退
    private float knockbackTimer = 0f;     // 击退计时器
    private Vector2 knockbackDirection;    // 击退方向
    private EnemyState stateBeforeKnockback; // 击退前的状态

    [Header("旋转设置")]
    public float rotationSpeed = 10f;
    public bool useSmoothRotation = true;

    // 移动相关
    private Vector2 spawnPosition;
    private Vector2 patrolTarget;
    private Vector2 chargeDirection;
    private Vector2 currentMoveDirection = Vector2.right;

    // 旋转相关
    private float targetRotation = 0f;
    private float currentRotation = 0f;

    // 状态相关
    private EnemyState currentState = EnemyState.Idle;
    private float stateTimer = 0f;
    private bool isPlayerInRange = false;

    // 伤害间隔控制
    private float lastCollisionDamageTime = 0f;
    private bool canDealCollisionDamage = true;

    protected override void Start()
    {
        base.Start();

        spawnPosition = transform.position;
        currentRotation = transform.eulerAngles.z;
        targetRotation = currentRotation;

        SetRandomPatrolTarget();
        ChangeState(EnemyState.Patrol); // 直接从巡逻开始
    }

    protected override void UpdateEnemy()
    {
        if (!isAlive) return;

        // 更新击退效果
        UpdateKnockback();

        stateTimer += Time.deltaTime;

        // 更新伤害间隔计时
        UpdateDamageCooldown();

        // 检测玩家
        CheckPlayerInRange();

        UpdateState();

        // 只有在非击退状态才更新旋转
        if (!isKnockedBack)
        {
            UpdateRotation();
        }
    }

    /// <summary>
    /// 更新击退效果
    /// </summary>
    void UpdateKnockback()
    {
        if (isKnockedBack)
        {
            knockbackTimer -= Time.deltaTime;

            if (knockbackTimer <= 0)
            {
                // 击退结束
                isKnockedBack = false;
                rb.velocity = Vector2.zero;
                Debug.Log("击退结束");
            }
        }
    }

    /// <summary>
    /// 更新伤害冷却计时
    /// </summary>
    void UpdateDamageCooldown()
    {
        // 如果正在冷却，检查是否完成
        if (!canDealCollisionDamage)
        {
            if (Time.time - lastCollisionDamageTime >= CollisionDamgeTime)
            {
                canDealCollisionDamage = true;
                Debug.Log("碰撞伤害冷却完成");
            }
        }
    }

    void UpdateState()
    {
        // 如果正在被击退，暂停状态更新
        if (isKnockedBack) return;

        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdleState();
                break;

            case EnemyState.Patrol:
                UpdatePatrolState();
                break;

            case EnemyState.Chase:
                UpdateChaseState();
                break;

            case EnemyState.Attack:
                UpdateAttackState();
                break;

            case EnemyState.Cooldown:
                UpdateCooldownState();
                break;
        }
    }

    void ChangeState(EnemyState newState)
    {
        // 如果正在被击退，不允许切换状态
        if (isKnockedBack) return;

        // 如果是切换到攻击状态，重置一些参数
        if (newState == EnemyState.Attack)
        {
            chargeDirection = currentMoveDirection;
        }

        currentState = newState;
        stateTimer = 0f;
        Debug.Log($"状态切换: {newState}");
    }

    #region 状态更新方法

    void UpdateIdleState()
    {
        rb.velocity = Vector2.zero;
        currentMoveDirection = Vector2.zero;

        if (stateTimer >= 1f)
        {
            ChangeState(EnemyState.Patrol);
        }
    }

    void UpdatePatrolState()
    {
        // 向巡逻目标移动
        Vector2 directionToTarget = (patrolTarget - (Vector2)transform.position).normalized;
        rb.velocity = directionToTarget * patrolSpeed;
        currentMoveDirection = directionToTarget;

        // 检查是否到达目标
        float distanceToTarget = Vector2.Distance(transform.position, patrolTarget);
        if (distanceToTarget < 0.5f || stateTimer >= 3f)
        {
            SetRandomPatrolTarget();
            stateTimer = 0f;
        }

        // 检查玩家是否在范围内
        if (isPlayerInRange)
        {
            ChangeState(EnemyState.Chase);
        }
    }

    void UpdateChaseState()
    {
        // 面向玩家蓄力
        if (PlayerTransform != null)
        {
            Vector2 toPlayer = (PlayerTransform.position - transform.position).normalized;
            currentMoveDirection = toPlayer;
            chargeDirection = toPlayer;
        }

        rb.velocity = Vector2.zero; // 蓄力时停止移动

        if (stateTimer >= chargeTime)
        {
            ChangeState(EnemyState.Attack);
        }
    }

    void UpdateAttackState()
    {
        // 只有在非击退状态才执行攻击移动
        if (!isKnockedBack)
        {
            // 向玩家冲刺
            rb.velocity = chargeDirection * chargeSpeed;
            currentMoveDirection = chargeDirection;
        }

        if (stateTimer >= attackDuration)
        {
            ChangeState(EnemyState.Cooldown);
        }
    }

    void UpdateCooldownState()
    {
        rb.velocity = Vector2.zero;
        currentMoveDirection = Vector2.zero;

        if (stateTimer >= cooldownDuration)
        {
            if (isPlayerInRange)
            {
                ChangeState(EnemyState.Chase);
            }
            else
            {
                ChangeState(EnemyState.Patrol);
            }
        }
    }

    #endregion

    #region 检测和移动

    void CheckPlayerInRange()
    {
        if (PlayerTransform == null)
        {
            isPlayerInRange = false;
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, PlayerTransform.position);
        isPlayerInRange = distanceToPlayer <= detectionRange;
    }

    void SetRandomPatrolTarget()
    {
        Vector2 randomOffset = Random.insideUnitCircle * patrolRadius;
        patrolTarget = spawnPosition + randomOffset;
    }

    void UpdateRotation()
    {
        if (currentMoveDirection.magnitude > 0.1f)
        {
            targetRotation = Mathf.Atan2(currentMoveDirection.y, currentMoveDirection.x) * Mathf.Rad2Deg;
        }

        if (useSmoothRotation)
        {
            currentRotation = Mathf.LerpAngle(currentRotation, targetRotation, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0, 0, currentRotation);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0, 0, targetRotation);
        }
    }

    #endregion

    #region 战斗系统

    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);

        // 受伤时触发击退效果
        if (isAlive && PlayerTransform != null)
        {
            // 保存击退前的状态
            stateBeforeKnockback = currentState;

            // 计算击退方向
            Vector2 knockbackDir = (transform.position - PlayerTransform.position).normalized;
            ApplyKnockback(knockbackDir);
        }

        // 受伤后进入追逐状态
        if (isAlive && PlayerTransform != null)
        {
            ChangeState(EnemyState.Chase);
        }
    }

    /// <summary>
    /// 应用击退效果 - 修复版本
    /// </summary>
    public void ApplyKnockback(Vector2 direction)
    {
        if (!isAlive || isKnockedBack) return;

        // 设置击退状态
        isKnockedBack = true;
        knockbackTimer = knockbackDuration;
        knockbackDirection = direction.normalized;

        // 保存当前状态（用于击退后恢复）
        stateBeforeKnockback = currentState;

        // 关键：击退时立即停止所有状态相关的移动
        rb.velocity = Vector2.zero;

        // 应用击退力
        rb.velocity = knockbackDirection * knockbackForce;

        Debug.Log($"击退方向: {knockbackDirection}, 力度: {knockbackForce}, 当前状态: {currentState}");

        // 保存当前状态，击退结束后恢复
        StartCoroutine(KnockbackRecoveryRoutine());
    }

    /// <summary>
    /// 击退恢复协程 - 修复版本
    /// </summary>
    IEnumerator KnockbackRecoveryRoutine()
    {
        // 等待击退持续时间
        yield return new WaitForSeconds(knockbackDuration);

        // 停止运动
        rb.velocity = Vector2.zero;

        // 短暂硬直
        yield return new WaitForSeconds(stunDuration);

        // 恢复移动
        isKnockedBack = false;

        // 关键修复：击退后应该进入冷却状态，而不是恢复攻击
        // 如果击退前是攻击状态，击退后应该进入冷却
        if (stateBeforeKnockback == EnemyState.Attack)
        {
            ChangeState(EnemyState.Cooldown);
        }
        // 如果击退前是追逐状态，重新开始追逐
        else if (stateBeforeKnockback == EnemyState.Chase && isPlayerInRange)
        {
            ChangeState(EnemyState.Chase);
        }
        // 其他状态回到巡逻
        else
        {
            ChangeState(EnemyState.Patrol);
        }

        Debug.Log($"击退恢复，从{stateBeforeKnockback}切换到{currentState}");
    }

    protected override void HandlePlayerCollision(GameObject player)
    {
        if (!isAlive) return;

        // 检查是否可以造成伤害
        if (!canDealCollisionDamage)
        {
            Debug.Log("碰撞伤害冷却中，跳过伤害");
            return;
        }

        // 根据当前状态处理碰撞
        if (currentState == EnemyState.Attack)
        {
            AttackPlayer(player);
        }
        else if (currentState != EnemyState.Idle && currentState != EnemyState.Cooldown)
        {
            // 接触伤害（较小）
            DealContactDamage(player, 2f, 1); // 2点伤害，1层层数
        }
    }

    void AttackPlayer(GameObject player)
    {
        // 检查是否可以造成伤害
        if (!canDealCollisionDamage)
        {
            Debug.Log("攻击伤害冷却中，跳过攻击");
            return;
        }

        // 给予玩家星蚀层数
        if (PlayerEclipse != null)
        {
            PlayerEclipse.AddLayer(layerAddAmount);
            Debug.Log($"给予玩家 {layerAddAmount} 层星蚀");
        }

        // 造成碰撞伤害
        DealCollisionDamage(player, collisionDamage);

        Debug.Log($"执行攻击伤害: {collisionDamage}");

        // 攻击时受到反作用力（怪物撞退）
        Vector2 playerToEnemy = (transform.position - player.transform.position).normalized;
        ApplyKnockback(playerToEnemy * 1.2f); // 攻击时的击退

        // 关键修复：攻击后不应该立即进入冷却，因为击退协程会处理
        // 击退结束后会自动进入冷却状态
    }

    /// <summary>
    /// 处理接触伤害
    /// </summary>
    void DealContactDamage(GameObject player, float damage, int layerAmount)
    {
        PlayerController playerCtrl = player.GetComponent<PlayerController>();
        if (playerCtrl)
        {
            // 造成伤害
            playerCtrl.TakeDamage(damage);

            // 给予层数
            if (PlayerEclipse != null)
            {
                PlayerEclipse.AddLayer(layerAmount);
                Debug.Log($"接触伤害给予 {layerAmount} 层星蚀");
            }

            // 触发伤害间隔
            TriggerDamageCooldown();

            // 怪物受到轻微反作用力
            Vector2 playerToEnemy = (transform.position - player.transform.position).normalized;
            ApplyKnockback(playerToEnemy * 0.8f); // 接触伤害的击退

            Debug.Log($"接触伤害: {damage}, 下次伤害在 {CollisionDamgeTime} 秒后");
        }
    }

    /// <summary>
    /// 处理碰撞伤害
    /// </summary>
    void DealCollisionDamage(GameObject player, float damage)
    {
        PlayerController playerCtrl = player.GetComponent<PlayerController>();
        if (playerCtrl)
        {
            playerCtrl.TakeDamage(damage);

            // 触发伤害间隔
            TriggerDamageCooldown();

            Debug.Log($"碰撞伤害: {damage}, 下次伤害在 {CollisionDamgeTime} 秒后");
        }
    }

    /// <summary>
    /// 触发伤害冷却
    /// </summary>
    void TriggerDamageCooldown()
    {
        canDealCollisionDamage = false;
        lastCollisionDamageTime = Time.time;
        Debug.Log($"开始碰撞伤害冷却: {CollisionDamgeTime}秒");
    }

    protected override void OnDeath()
    {
        base.OnDeath();

        // 停止所有运动
        rb.velocity = Vector2.zero;
        currentMoveDirection = Vector2.zero;

        // 死亡时取消所有击退效果
        isKnockedBack = false;
        StopAllCoroutines();

        // 缩放消失动画
        StartCoroutine(ScaleOutAnimation());
    }

    private IEnumerator ScaleOutAnimation()
    {
        float duration = 0.5f;
        float elapsed = 0f;
        Vector3 originalScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, progress);
            yield return null;
        }

        // 确保物体被销毁
        Destroy(gameObject);
    }

    #endregion

    #region 调试

    void OnDrawGizmosSelected()
    {
        // 检测范围
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // 巡逻范围
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(Application.isPlaying ? (Vector3)spawnPosition : transform.position, patrolRadius);

        // 移动方向
        Gizmos.color = Color.red;
        Vector3 directionEnd = transform.position + (Vector3)currentMoveDirection * 2f;
        Gizmos.DrawLine(transform.position, directionEnd);
        Gizmos.DrawSphere(directionEnd, 0.1f);

        // 状态指示器
        Gizmos.color = GetStateColor();
        Gizmos.DrawSphere(transform.position + Vector3.up * 1.5f, 0.3f);

        // 伤害冷却状态
        if (!canDealCollisionDamage)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.7f); // 半透明橙色
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.5f);
        }

        // 击退状态指示
        if (isKnockedBack)
        {
            Gizmos.color = new Color(1f, 0f, 1f, 0.8f); // 紫色表示击退
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2.5f, 0.4f);
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)knockbackDirection * 3f);
        }
    }

    Color GetStateColor()
    {
        switch (currentState)
        {
            case EnemyState.Idle: return Color.gray;
            case EnemyState.Patrol: return Color.blue;
            case EnemyState.Chase: return Color.yellow;
            case EnemyState.Attack: return Color.red;
            case EnemyState.Cooldown: return Color.magenta;
            default: return Color.white;
        }
    }

    #endregion

    protected override void OnStateChanged(EnemyState newState)
    {
        // 简单实现抽象方法
    }
}