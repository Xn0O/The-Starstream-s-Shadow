using UnityEngine;
using System.Collections;

public class EnemyShadow : MonoBehaviour
{
    public enum EnemyState { Idle, Patrol, Charging, Attacking, Cooldown }

    [Header("基础设置")]
    public float maxHealth = 100f;
    public float currentHealth;
    public int layerAddAmount = 3;
    public float collisionDamage = 10f;

    [Header("移动设置")]
    public float patrolSpeed = 2f;
    public float chargeSpeed = 8f;
    public float patrolRadius = 5f;

    [Header("攻击设置")]
    public float chargeTime = 1f;
    public float attackDuration = 0.5f;
    public float cooldownDuration = 2f;
    public float detectionRange = 8f;

    [Header("旋转设置")]
    public float rotationSpeed = 10f;
    public bool useSmoothRotation = true;

    [Header("动画效果")]
    public GameObject hitAnimationPrefab; // 新增：受击动画预制体
    public GameObject deathAnimationPrefab; // 新增：死亡动画预制体
    public float animationScaleMultiplier = 1f; // 动画大小乘数

    [Header("粒子效果")]
    public ParticleSystem trailParticles;  // 尾部跟随粒子
    public ParticleSystem hitParticles;    // 击中粒子
    public float minTrailEmission = 5f;    // 最小粒子发射率
    public float maxTrailEmission = 30f;   // 最大粒子发射率

    [Header("蓄力圆环")]
    public GameObject chargeRingPrefab;    // 蓄力圆环预制体
    public float ringStartScale = 0.5f;    // 圆环初始大小
    public float ringEndScale = 3f;        // 圆环最大大小
    public float ringDuration = 1f;        // 圆环持续时间
    public Color ringColor = Color.yellow; // 圆环颜色

    [Header("状态栏")]
    public EnemyState currentState = EnemyState.Idle;

    [Header("视觉设置")]
    public SpriteRenderer mainSprite;      // 主要Sprite
    public Color normalColor = Color.white;  // 正常颜色

    private float stateTimer = 0f;
    private Vector2 chargeDirection;
    private Vector2 patrolTarget;
    private Vector2 currentMoveDirection = Vector2.right;
    private float targetRotation = 0f;
    private float currentRotation = 0f;
    private GameObject currentChargeRing;  // 当前蓄力圆环
    private Transform playerTransform;
    private EclipseSystem playerEclipse;
    private Vector2 spawnPosition;

    // 组件引用
    private Rigidbody2D rb;

    void Start()
    {
        // 获取或添加组件
        if (!mainSprite)
            mainSprite = GetComponent<SpriteRenderer>();

        rb = GetComponent<Rigidbody2D>();
        spawnPosition = transform.position;
        currentHealth = maxHealth;

        // 设置初始颜色
        if (mainSprite)
            mainSprite.color = normalColor;

        FindPlayer();
        ChangeState(EnemyState.Idle);
        SetRandomPatrolTarget();

        // 初始化旋转
        currentRotation = transform.eulerAngles.z;
        targetRotation = currentRotation;

        // 初始化粒子系统
        SetupParticles();
    }

    void SetupParticles()
    {
        if (trailParticles)
        {
            // 确保粒子系统开始时是停止状态
            trailParticles.Stop();

            // 配置粒子系统
            var main = trailParticles.main;

            main.startSpeed = -2f; // 负值表示向反方向喷射

            var emission = trailParticles.emission;
            emission.rateOverTime = 0f; // 初始为0
        }
    }

    void Update()
    {
        stateTimer += Time.deltaTime;

        if (!playerTransform)
        {
            FindPlayer();
        }

        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdleState();
                currentMoveDirection = Vector2.zero;
                break;

            case EnemyState.Patrol:
                UpdatePatrolState();
                break;

            case EnemyState.Charging:
                UpdateChargingState();
                break;

            case EnemyState.Attacking:
                UpdateAttackingState();
                break;

            case EnemyState.Cooldown:
                UpdateCooldownState();
                currentMoveDirection = Vector2.zero;
                break;
        }

        UpdateRotation();
        UpdateTrailParticles();
    }

    void UpdateRotation()
    {
        if (currentMoveDirection.magnitude > 0.1f)
        {
            // 计算目标旋转角度（将移动方向转换为角度）
            targetRotation = Mathf.Atan2(currentMoveDirection.y, currentMoveDirection.x) * Mathf.Rad2Deg;
        }

        if (useSmoothRotation)
        {
            // 平滑旋转
            currentRotation = Mathf.LerpAngle(currentRotation, targetRotation, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0, 0, currentRotation);
        }
        else
        {
            // 立即旋转
            transform.rotation = Quaternion.Euler(0, 0, targetRotation);
        }
    }

    void UpdateTrailParticles()
    {
        if (!trailParticles) return;

        float currentSpeed = rb.velocity.magnitude;
        float emissionRate = 0f;

        // 根据速度和状态调整粒子发射率
        switch (currentState)
        {
            case EnemyState.Patrol:
                emissionRate = Mathf.Lerp(minTrailEmission, maxTrailEmission, currentSpeed / patrolSpeed);
                break;

            case EnemyState.Attacking:
                emissionRate = maxTrailEmission * 2f; // 攻击时粒子加倍
                break;

            default:
                emissionRate = minTrailEmission * 0.5f; // 其他状态少量粒子
                break;
        }

        // 更新粒子发射率
        var emission = trailParticles.emission;
        emission.rateOverTime = emissionRate;

        // 如果粒子系统未运行且应该发射，则启动
        if (emissionRate > 0 && !trailParticles.isPlaying)
        {
            trailParticles.Play();
        }
        else if (emissionRate <= 0 && trailParticles.isPlaying)
        {
            trailParticles.Stop();
        }

        // 根据移动方向调整粒子方向
        if (currentMoveDirection.magnitude > 0.1f)
        {
            // 粒子应该向移动反方向发射（形成尾部效果）
            float particleRotation = Mathf.Atan2(-currentMoveDirection.y, -currentMoveDirection.x) * Mathf.Rad2Deg;
            var main = trailParticles.main;
            main.startRotation = particleRotation * Mathf.Deg2Rad;
        }
    }

    void UpdateIdleState()
    {
        rb.velocity = Vector2.zero;

        if (stateTimer >= 1f)
        {
            ChangeState(EnemyState.Patrol);
        }
    }

    void UpdatePatrolState()
    {
        Vector2 directionToTarget = (patrolTarget - (Vector2)transform.position).normalized;
        rb.velocity = directionToTarget * patrolSpeed;
        currentMoveDirection = directionToTarget;

        float distanceToTarget = Vector2.Distance(transform.position, patrolTarget);
        if (distanceToTarget < 0.5f || stateTimer >= 3f)
        {
            SetRandomPatrolTarget();
            stateTimer = 0f;
        }

        CheckForPlayer();
    }

    void UpdateChargingState()
    {
        rb.velocity = Vector2.zero;

        if (playerTransform)
        {
            Vector2 toPlayer = (playerTransform.position - transform.position).normalized;
            currentMoveDirection = toPlayer;
            chargeDirection = toPlayer;
        }

        // 更新蓄力圆环（如果存在）
        if (currentChargeRing)
        {
            float chargeProgress = stateTimer / chargeTime;
            UpdateChargeRing(chargeProgress);
        }

        if (stateTimer >= chargeTime)
        {
            ChangeState(EnemyState.Attacking);
        }
    }

    void UpdateAttackingState()
    {
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

        if (stateTimer >= cooldownDuration)
        {
            ChangeState(EnemyState.Patrol);
        }
    }

    void CreateChargeRing()
    {
        if (!chargeRingPrefab) return;

        // 创建圆环
        currentChargeRing = Instantiate(chargeRingPrefab, transform.position, Quaternion.identity, transform);

        // 设置初始大小和透明度
        SpriteRenderer ringSprite = currentChargeRing.GetComponent<SpriteRenderer>();
        if (ringSprite)
        {
            ringSprite.color = new Color(ringColor.r, ringColor.g, ringColor.b, 0.7f);
        }

        currentChargeRing.transform.localScale = Vector3.one * ringStartScale;
    }

    void UpdateChargeRing(float progress)
    {
        if (!currentChargeRing) return;

        // 计算当前大小
        float currentScale = Mathf.Lerp(ringStartScale, ringEndScale, progress);
        currentChargeRing.transform.localScale = Vector3.one * currentScale;

        // 计算透明度（随进度增加而减少）
        SpriteRenderer ringSprite = currentChargeRing.GetComponent<SpriteRenderer>();
        if (ringSprite)
        {
            float alpha = Mathf.Lerp(0.7f, 0f, progress);
            ringSprite.color = new Color(ringColor.r, ringColor.g, ringColor.b, alpha);
        }
    }

    void DestroyChargeRing()
    {
        if (currentChargeRing)
        {
            // 淡出效果
            StartCoroutine(FadeOutRing());
            currentChargeRing = null;
        }
    }

    IEnumerator FadeOutRing()
    {
        if (!currentChargeRing) yield break;

        SpriteRenderer ringSprite = currentChargeRing.GetComponent<SpriteRenderer>();
        if (!ringSprite) yield break;

        float fadeTime = 0.3f;
        float elapsed = 0f;
        Color startColor = ringSprite.color;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fadeTime;

            Color newColor = startColor;
            newColor.a = Mathf.Lerp(startColor.a, 0f, progress);
            ringSprite.color = newColor;

            yield return null;
        }

        Destroy(currentChargeRing);
    }

    void CheckForPlayer()
    {
        if (!playerTransform) return;

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= detectionRange)
        {
            ChangeState(EnemyState.Charging);
        }
    }

    void SetRandomPatrolTarget()
    {
        Vector2 randomOffset = Random.insideUnitCircle * patrolRadius;
        patrolTarget = spawnPosition + randomOffset;
    }

    void ChangeState(EnemyState newState)
    {
        // 退出当前状态的处理
        switch (currentState)
        {
            case EnemyState.Charging:
                // 清除蓄力圆环
                DestroyChargeRing();
                break;
        }

        // 设置新状态
        currentState = newState;
        stateTimer = 0f;

        // 进入新状态的处理
        switch (newState)
        {
            case EnemyState.Idle:
                rb.velocity = Vector2.zero;
                break;

            case EnemyState.Patrol:
                SetRandomPatrolTarget();
                break;

            case EnemyState.Charging:
                rb.velocity = Vector2.zero;
                CreateChargeRing();
                break;

            case EnemyState.Attacking:
                // 攻击开始时播放粒子效果
                PlayHitParticles();
                break;

            case EnemyState.Cooldown:
                rb.velocity = Vector2.zero;
                break;
        }
    }

    void PlayHitParticles()
    {
        if (hitParticles)
        {
            hitParticles.Play();
        }
    }

    void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            playerTransform = player.transform;
            playerEclipse = player.GetComponent<EclipseSystem>();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player") && currentState == EnemyState.Attacking)
        {
            AttackPlayer(collision.gameObject);
        }
        if (collision.gameObject.CompareTag("Player"))
        {
            // 玩家持续接触时造成伤害
            if (currentState == EnemyState.Attacking)
            {
                AttackPlayer(collision.gameObject);
            }
            else if (currentState != EnemyState.Idle && currentState != EnemyState.Cooldown)
            {
                // 其他状态造成接触伤害（除了待机和冷却）
                PlayerController playerCtrl = collision.gameObject.GetComponent<PlayerController>();
                if (playerCtrl)
                {
                    playerCtrl.TakeDamage(2f); // 接触伤害
                    playerEclipse.AddLayer(1);
                }
            }
        }
    }

    void AttackPlayer(GameObject player)
    {
        if (playerEclipse)
        {
            playerEclipse.AddLayer(layerAddAmount);
        }

        PlayerController playerCtrl = player.GetComponent<PlayerController>();
        if (playerCtrl)
        {
            playerCtrl.TakeDamage(collisionDamage);
        }

        ChangeState(EnemyState.Cooldown);
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;

        // 显示伤害数字
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui)
        {
            ui.ShowDamageText(transform.position, damage, new Color(1f, 0.8f, 0f));
        }

        // 受伤时闪烁效果
        StartCoroutine(HitFlash());

        // 播放受击动画
        PlayHitAnimation();

        if (playerTransform)
        {
            ChangeState(EnemyState.Charging);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void PlayHitAnimation()
    {
        if (hitAnimationPrefab != null)
        {
            // 在敌人位置创建受击动画
            GameObject hitAnimation = Instantiate(hitAnimationPrefab, transform.position, Quaternion.identity);

            // 设置动画大小
            hitAnimation.transform.localScale = Vector3.one * animationScaleMultiplier;

            // 获取Animator组件并播放动画
            Animator animator = hitAnimation.GetComponent<Animator>();
            if (animator != null)
            {
                // 触发动画播放
                animator.Play("Hit", 0, 0f);

                // 获取动画长度自动销毁
                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                if (clips.Length > 0)
                {
                    // 假设第一个动画是我们想要播放的
                    float animationLength = clips[0].length;
                    Destroy(hitAnimation, animationLength + 0.1f);
                }
                else
                {
                    // 如果没有动画剪辑信息，使用默认时间
                    Destroy(hitAnimation, 0.5f);
                }
            }
            else
            {
                Debug.LogWarning("受击动画预制体没有Animator组件！");
                // 如果没有Animator，简单延迟后销毁
                Destroy(hitAnimation, 0.5f);
            }
        }
    }

    IEnumerator HitFlash()
    {
        if (!mainSprite) yield break;

        Color originalColor = mainSprite.color;
        mainSprite.color = Color.white;

        yield return new WaitForSeconds(0.1f);

        mainSprite.color = originalColor;
    }

    void Die()
    {
        Debug.Log($"敌人死亡: {gameObject.name}");

        // 播放死亡动画
        PlayDeathAnimation();

        // 播放死亡粒子效果
        if (trailParticles)
        {
            trailParticles.Stop();
            var emission = trailParticles.emission;
            emission.rateOverTime = 0;
        }

        if (hitParticles)
        {
            hitParticles.Play();
        }

        // 死亡动画（缩放消失）和销毁
        StartCoroutine(DeathAnimation());
    }

    void PlayDeathAnimation()
    {
        if (deathAnimationPrefab != null)
        {
            // 在敌人位置创建死亡动画
            GameObject deathAnimation = Instantiate(deathAnimationPrefab, transform.position, Quaternion.identity);

            // 设置动画大小
            deathAnimation.transform.localScale = Vector3.one * animationScaleMultiplier;

            // 获取Animator组件并播放动画
            Animator animator = deathAnimation.GetComponent<Animator>();
            if (animator != null)
            {
                // 触发动画播放
                animator.Play("Death", 0, 0f);

                // 获取动画长度自动销毁
                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                if (clips.Length > 0)
                {
                    // 假设第一个动画是我们想要播放的
                    float animationLength = clips[0].length;
                    Destroy(deathAnimation, animationLength + 0.2f);
                }
                else
                {
                    // 如果没有动画剪辑信息，使用默认时间
                    Destroy(deathAnimation, 1f);
                }
            }
            else
            {
                Debug.LogWarning("死亡动画预制体没有Animator组件！");
                // 如果没有Animator，简单延迟后销毁
                Destroy(deathAnimation, 1f);
            }
        }
    }

    IEnumerator DeathAnimation()
    {
        // 禁用碰撞和移动
        if (GetComponent<Collider2D>())
            GetComponent<Collider2D>().enabled = false;
        rb.velocity = Vector2.zero;
        rb.isKinematic = true;

        // 等待一小段时间让死亡动画播放
        yield return new WaitForSeconds(0.1f);

        // 缩放消失
        float duration = 0.3f; // 缩短消失时间，因为已经有死亡动画
        float elapsed = 0f;
        Vector3 originalScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            // 缩放和淡出
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, progress);

            if (mainSprite)
            {
                Color color = mainSprite.color;
                color.a = Mathf.Lerp(1f, 0f, progress);
                mainSprite.color = color;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        // 绘制检测范围（黄色）
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // 绘制巡逻范围（蓝色）
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(Application.isPlaying ? (Vector3)spawnPosition : transform.position, patrolRadius);

        // 绘制当前面向方向（红色箭头）
        Gizmos.color = Color.red;
        Vector3 directionEnd = transform.position + (Vector3)currentMoveDirection;
        Gizmos.DrawLine(transform.position, directionEnd);
        Gizmos.DrawSphere(directionEnd, 0.1f);
    }
}