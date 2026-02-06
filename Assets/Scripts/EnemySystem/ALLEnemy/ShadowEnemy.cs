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

        stateTimer += Time.deltaTime;

        // 更新伤害间隔计时
        UpdateDamageCooldown();

        // 检测玩家
        CheckPlayerInRange();

        UpdateState();
        UpdateRotation();
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
        // 向玩家冲刺
        rb.velocity = chargeDirection * chargeSpeed;
        currentMoveDirection = chargeDirection;

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

        // 受伤后进入追逐状态
        if (isAlive && PlayerTransform != null)
        {
            ChangeState(EnemyState.Chase);
        }
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

        // 攻击后进入冷却
        ChangeState(EnemyState.Cooldown);
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