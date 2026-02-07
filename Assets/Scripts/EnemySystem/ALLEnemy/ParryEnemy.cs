using UnityEngine;
using System.Collections;

public class ParryEnemy : BaseEnemy
{
    [Header("弹反敌人设置")]
    [SerializeField] private GameObject normalModel;
    [SerializeField] private GameObject parryModel;
    [SerializeField] private GameObject windupModel; // 新增：蓄力阶段模型

    [Header("简单行为设置")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float patrolRange = 5f;

    [Header("冲撞设置")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float chargeWindupTime = 1f; // 蓄力时间
    [SerializeField] private float chargeSpeed = 15f;
    [SerializeField] private float chargeAcceleration = 20f; // 冲撞加速度
    [SerializeField] private float chargeDuration = 2f;
    [SerializeField] private float chargeCooldown = 1.5f;

    [Header("弹反设置")]
    [SerializeField] private float parrySpeed = 20f;
    [SerializeField] private float parryDuration = 3f;
    [SerializeField] private float damageMultiplier = 10f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("碰撞反弹设置")]
    [SerializeField] private float enemyReboundForce = 12f;
    [SerializeField] private float reboundDuration = 0.5f;
    [SerializeField] private float stunDuration = 0.3f;
    [SerializeField] private float collisionDamageCooldown = 0.8f;

    [Header("冲撞消失设置")]
    [SerializeField] private float chargeMissTimeout = 3f;
    [SerializeField] private bool destroyOnMiss = true;
    [SerializeField] private GameObject timeoutEffectPrefab;
    [SerializeField] private float timeoutEffectScale = 1f;

    [Header("蓄力特效设置")]
    [SerializeField] private GameObject windupEffectPrefab; // 蓄力特效
    [SerializeField] private bool showWindupEffect = true; // 是否显示蓄力特效
    [SerializeField] private float windupFlashSpeed = 3f; // 蓄力闪烁速度

    // 简化状态
    private enum SimpleState { Patrol, Windup, Charging, Parry, Rebound, Cooldown, Timeout }
    private SimpleState currentState = SimpleState.Patrol;

    private Vector2 patrolCenter;
    private Vector2 moveDirection;
    private float stateTimer = 0f;
    private float lastDamageTaken = 0f;
    private float lastCollisionDamageTime = 0f;
    private float chargeMissTimer = 0f;
    private bool hasHitPlayer = false;
    private Vector2 reboundDirection;
    private GameObject currentWindupEffect; // 当前蓄力特效实例
    private Vector2 chargeDirection; // 冲撞方向
    private float currentChargeSpeed = 0f; // 当前冲撞速度

    #region 生命周期

    protected override void Awake()
    {
        base.Awake();
        patrolCenter = transform.position;
        moveDirection = Random.insideUnitCircle.normalized;

        canTakeDamage = true;
        isAlive = true;
    }

    protected override void Start()
    {
        base.Start();

        canTakeDamage = true;
        isAlive = true;
    }

    protected override void Update()
    {
        if (!isAlive) return;

        base.Update();

        UpdateSimpleBehavior();

        // 更新冲撞未命中计时器
        UpdateChargeMissTimer();
    }

    #endregion

    #region 抽象方法实现

    protected override void UpdateEnemy()
    {
        // 行为在UpdateSimpleBehavior中处理
    }

    protected override void HandlePlayerCollision(GameObject player)
    {
        if (!isAlive) return;

        // 在冲撞状态处理碰撞
        if (currentState == SimpleState.Charging)
        {
            // 检查伤害冷却
            if (Time.time - lastCollisionDamageTime < collisionDamageCooldown)
            {
                return;
            }

            lastCollisionDamageTime = Time.time;

            var playerController = player.GetComponent<PlayerController>();
            if (playerController != null)
            {
                // 标记已撞到玩家
                hasHitPlayer = true;

                // 1. 对玩家造成伤害
                playerController.TakeDamage(collisionDamage);

                // 2. 给玩家叠加层数
                if (playerEclipse != null)
                {
                    playerEclipse.AddLayer(layerAddAmount);
                    Debug.Log($"弹反敌人撞到玩家，增加层数");
                }

                // 3. 敌人自己朝反方向反弹
                TriggerEnemyRebound(player);
            }
        }
    }

    protected override void OnStateChanged(EnemyState newState)
    {
        // 简单实现
    }

    #endregion

    #region 蓄力阶段视觉效果

    private void SpawnWindupEffect()
    {
        if (windupEffectPrefab != null && showWindupEffect && currentWindupEffect == null)
        {
            currentWindupEffect = Instantiate(windupEffectPrefab, transform.position, Quaternion.identity, transform);
            currentWindupEffect.transform.localScale = Vector3.one;

            Debug.Log("生成蓄力特效");
        }
    }

    private void ClearWindupEffect()
    {
        if (currentWindupEffect != null)
        {
            Destroy(currentWindupEffect);
            currentWindupEffect = null;
        }
    }

    #endregion

    #region 冲撞未命中消失系统

    private void UpdateChargeMissTimer()
    {
        if (currentState == SimpleState.Charging)
        {
            chargeMissTimer += Time.deltaTime;

            if (chargeMissTimer >= chargeMissTimeout && !hasHitPlayer)
            {
                TriggerChargeMissTimeout();
            }
        }
        else
        {
            chargeMissTimer = 0f;
            hasHitPlayer = false;
        }
    }

    private void TriggerChargeMissTimeout()
    {
        Debug.Log($"冲撞未命中，{chargeMissTimeout}秒后消失");

        if (destroyOnMiss)
        {
            PlayTimeoutEffect();
            Die();
        }
        else
        {
            ChangeState(SimpleState.Timeout);
            stateTimer = 1f;
        }
    }

    private void PlayTimeoutEffect()
    {
        if (timeoutEffectPrefab != null)
        {
            GameObject effect = Instantiate(timeoutEffectPrefab, transform.position, Quaternion.identity);
            effect.transform.localScale = Vector3.one * timeoutEffectScale;

            Animator animator = effect.GetComponent<Animator>();
            if (animator != null)
            {
                animator.Play("Timeout", 0, 0f);
                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                if (clips.Length > 0)
                {
                    Destroy(effect, clips[0].length + 0.1f);
                }
                else
                {
                    Destroy(effect, 1f);
                }
            }
            else
            {
                Destroy(effect, 1f);
            }
        }
    }

    private void HandleTimeoutState()
    {
        if (stateTimer <= 0)
        {
            ChangeState(SimpleState.Patrol);
        }
    }

    #endregion

    #region 核心：敌人反弹系统

    private void TriggerEnemyRebound(GameObject player)
    {
        Vector2 playerToEnemy = ((Vector2)transform.position - (Vector2)player.transform.position).normalized;

        float angleVariation = Random.Range(-30f, 30f) * Mathf.Deg2Rad;
        reboundDirection = new Vector2(
            playerToEnemy.x * Mathf.Cos(angleVariation) - playerToEnemy.y * Mathf.Sin(angleVariation),
            playerToEnemy.x * Mathf.Sin(angleVariation) + playerToEnemy.y * Mathf.Cos(angleVariation)
        ).normalized;

        Debug.Log($"敌人反弹，方向: {reboundDirection}, 力量: {enemyReboundForce}");

        ChangeState(SimpleState.Rebound);
        stateTimer = reboundDuration;
        ApplyReboundForce();
    }

    private void ApplyReboundForce()
    {
        rb.velocity = Vector2.zero;
        rb.AddForce(reboundDirection * enemyReboundForce, ForceMode2D.Impulse);
    }

    private void HandleReboundState()
    {
        if (stateTimer <= 0)
        {
            StartCoroutine(ReboundStun());
        }
    }

    private IEnumerator ReboundStun()
    {
        float elapsed = 0f;
        Vector2 startVelocity = rb.velocity;
        while (elapsed < 0.2f)
        {
            rb.velocity = Vector2.Lerp(startVelocity, Vector2.zero, elapsed / 0.2f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        rb.velocity = Vector2.zero;

        yield return new WaitForSeconds(stunDuration);

        ChangeState(SimpleState.Cooldown);
        stateTimer = chargeCooldown;
    }

    #endregion

    #region 简单行为逻辑

    private void UpdateSimpleBehavior()
    {
        stateTimer -= Time.deltaTime;

        switch (currentState)
        {
            case SimpleState.Patrol:
                HandlePatrol();
                CheckPlayerForCharge();
                break;

            case SimpleState.Windup:
                HandleWindup();
                break;

            case SimpleState.Charging:
                HandleCharging();
                break;

            case SimpleState.Parry:
                HandleParry();
                break;

            case SimpleState.Rebound:
                HandleReboundState();
                break;

            case SimpleState.Cooldown:
                HandleCooldown();
                break;

            case SimpleState.Timeout:
                HandleTimeoutState();
                break;
        }
    }

    private void HandlePatrol()
    {
        if (stateTimer <= 0)
        {
            moveDirection = Random.insideUnitCircle.normalized;
            stateTimer = Random.Range(2f, 4f);
        }

        rb.velocity = moveDirection * moveSpeed;

        if (Vector2.Distance(transform.position, patrolCenter) > patrolRange)
        {
            moveDirection = (patrolCenter - (Vector2)transform.position).normalized;
        }
    }

    private void HandleWindup()
    {
        // 蓄力期间停止移动，面向玩家
        rb.velocity = Vector2.zero;

        if (playerTransform != null)
        {
            Vector2 direction = (playerTransform.position - transform.position).normalized;
            chargeDirection = direction; // 保存冲撞方向
            UpdateFacingDirection(direction);
        }

        if (stateTimer <= 0)
        {
            // 蓄力结束，开始冲撞
            ChangeState(SimpleState.Charging);
            stateTimer = chargeDuration;

            // 开始冲撞时重置速度
            currentChargeSpeed = 0f;

            // 重置未命中计时器
            chargeMissTimer = 0f;
            hasHitPlayer = false;
        }
    }

    private void HandleCharging()
    {
        // 冲撞速度逐渐增加
        if (currentChargeSpeed < chargeSpeed)
        {
            currentChargeSpeed = Mathf.Min(chargeSpeed, currentChargeSpeed + chargeAcceleration * Time.deltaTime);
        }

        // 应用冲撞速度
        rb.velocity = chargeDirection * currentChargeSpeed;

        if (stateTimer <= 0)
        {
            ChangeState(SimpleState.Cooldown);
            stateTimer = chargeCooldown;
            currentChargeSpeed = 0f; // 重置速度
        }
    }

    private void HandleParry()
    {
        if (stateTimer <= 0)
        {
            Die();
        }
    }

    private void HandleCooldown()
    {
        if (stateTimer <= 0)
        {
            ChangeState(SimpleState.Patrol);
        }
    }

    private void CheckPlayerForCharge()
    {
        if (playerTransform == null) return;

        float distance = Vector2.Distance(transform.position, playerTransform.position);
        if (distance <= detectionRange)
        {
            // 发现玩家，进入蓄力阶段
            ChangeState(SimpleState.Windup);
            stateTimer = chargeWindupTime;
        }
    }

    private void ChangeState(SimpleState newState)
    {
        // 离开当前状态前的清理
        OnExitState(currentState);

        currentState = newState;
        stateTimer = 0f;

        // 进入新状态的设置
        switch (newState)
        {
            case SimpleState.Patrol:
                SetPatrolState();
                break;

            case SimpleState.Windup:
                SetWindupState();
                break;

            case SimpleState.Charging:
                SetChargingState();
                break;

            case SimpleState.Parry:
                SetParryState();
                break;

            case SimpleState.Rebound:
                SetReboundState();
                break;

            case SimpleState.Cooldown:
                SetCooldownState();
                break;

            case SimpleState.Timeout:
                SetTimeoutState();
                break;
        }
    }

    private void OnExitState(SimpleState oldState)
    {
        // 离开蓄力状态时清理特效
        if (oldState == SimpleState.Windup)
        {
            ClearWindupEffect();
        }
    }

    private void SetPatrolState()
    {
        SetModelActive(normalModel, true);
        SetModelActive(parryModel, false);
        SetModelActive(windupModel, false); // 关闭蓄力模型

        canTakeDamage = true;
        rb.velocity = Vector2.zero;

        chargeMissTimer = 0f;
        hasHitPlayer = false;
        currentChargeSpeed = 0f; // 重置速度
    }

    private void SetWindupState()
    {
        // 显示蓄力模型，隐藏其他模型
        SetModelActive(normalModel, false);
        SetModelActive(parryModel, false);
        SetModelActive(windupModel, true); // 显示蓄力模型

        canTakeDamage = true;
        rb.velocity = Vector2.zero;

        // 生成蓄力特效
        SpawnWindupEffect();
    }

    private void SetChargingState()
    {
        // 冲撞时显示普通模型
        SetModelActive(normalModel, true);
        SetModelActive(parryModel, false);
        SetModelActive(windupModel, false); // 关闭蓄力模型

        canTakeDamage = true;
        stateTimer = chargeDuration;

        chargeMissTimer = 0f;
        hasHitPlayer = false;
        currentChargeSpeed = 0f; // 冲撞开始时速度为0
    }

    private void SetParryState()
    {
        SetModelActive(normalModel, false);
        SetModelActive(parryModel, true);
        SetModelActive(windupModel, false); // 关闭蓄力模型

        canTakeDamage = false;

        if (playerTransform != null)
        {
            Vector2 bounceDirection = ((Vector2)transform.position - (Vector2)playerTransform.position).normalized;
            rb.velocity = bounceDirection * parrySpeed;
        }

        stateTimer = parryDuration;
    }

    private void SetReboundState()
    {
        SetModelActive(normalModel, true);
        SetModelActive(parryModel, false);
        SetModelActive(windupModel, false); // 关闭蓄力模型

        canTakeDamage = false;
    }

    private void SetCooldownState()
    {
        SetModelActive(normalModel, true);
        SetModelActive(parryModel, false);
        SetModelActive(windupModel, false); // 关闭蓄力模型

        canTakeDamage = true;
        rb.velocity = Vector2.zero;
        currentChargeSpeed = 0f; // 重置速度
    }

    private void SetTimeoutState()
    {
        SetModelActive(normalModel, true);
        SetModelActive(parryModel, false);
        SetModelActive(windupModel, false); // 关闭蓄力模型

        canTakeDamage = false;
        rb.velocity = Vector2.zero;
        currentChargeSpeed = 0f; // 重置速度
    }

    private void SetModelActive(GameObject model, bool active)
    {
        if (model != null)
        {
            model.SetActive(active);
        }
    }

    private void UpdateFacingDirection(Vector2 direction)
    {
        if (mainSprite == null) return;

        if (Mathf.Abs(direction.x) > 0.1f)
        {
            mainSprite.flipX = direction.x < 0;
        }
    }

    #endregion

    #region 关键：TakeDamage方法

    public override void TakeDamage(float damage)
    {
        if (!isAlive) return;

        if (!canTakeDamage)
        {
            UIManager ui = FindObjectOfType<UIManager>();
            if (ui != null)
            {
                ui.ShowDamageText(transform.position, 0, Color.gray);
            }
            return;
        }

        if (currentState == SimpleState.Charging)
        {
            lastDamageTaken = damage;
            TriggerParry();
            return;
        }

        base.TakeDamage(damage);
    }

    private void TriggerParry()
    {
        ChangeState(SimpleState.Parry);
    }

    #endregion

    #region 碰撞处理

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        base.OnCollisionEnter2D(collision);

        if (currentState == SimpleState.Parry)
        {
            CheckForEnemyCollision();
        }
    }

    private void CheckForEnemyCollision()
    {
        if (currentState != SimpleState.Parry) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, col.bounds.extents.x, enemyLayer);

        foreach (var hit in hits)
        {
            if (hit.gameObject != gameObject && hit.TryGetComponent<BaseEnemy>(out var enemy))
            {
                enemy.TakeDamage(lastDamageTaken * damageMultiplier);
                Die();
                return;
            }
        }
    }

    #endregion

    #region 调试方法

    [ContextMenu("调试：测试蓄力效果")]
    public void DebugTestWindup()
    {
        if (currentState == SimpleState.Patrol)
        {
            ChangeState(SimpleState.Windup);
            stateTimer = 2f; // 设置2秒蓄力
            Debug.Log("进入蓄力状态，测试蓄力模型");
        }
    }

    [ContextMenu("调试：打印状态")]
    public void DebugPrintStatus()
    {
        Debug.Log($"=== ParryEnemy状态 ===");
        Debug.Log($"当前状态: {currentState}");
        Debug.Log($"生命值: {currentHealth}/{maxHealth}");
        Debug.Log($"刚体速度: {rb.velocity}");
        Debug.Log($"当前冲撞速度: {currentChargeSpeed}/{chargeSpeed}");
        Debug.Log($"状态计时器: {stateTimer}");
        Debug.Log($"冲撞未命中计时: {chargeMissTimer}/{chargeMissTimeout}");
        Debug.Log($"是否撞到过玩家: {hasHitPlayer}");
        Debug.Log($"冲撞方向: {chargeDirection}");
    }

    #endregion
}