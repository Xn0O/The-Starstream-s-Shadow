using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public abstract class BaseEnemy : MonoBehaviour
{
    [Header("基础属性")]
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float currentHealth;
    [SerializeField] protected int layerAddAmount = 3;
    [SerializeField] protected float collisionDamage = 10f;

    [Header("视觉设置")]
    public SpriteRenderer mainSprite;
    public Color normalColor = Color.white;

    [Header("动画效果")]
    public GameObject hitAnimationPrefab;
    public GameObject deathAnimationPrefab;
    public float animationScaleMultiplier = 1f;

    [Header("粒子效果")]
    public ParticleSystem trailParticles;
    public ParticleSystem hitParticles;
    public float minTrailEmission = 5f;
    public float maxTrailEmission = 30f;

    [Header("状态")]
    public bool isAlive = true;
    public bool canTakeDamage = true;

    // 组件引用
    protected Rigidbody2D rb;
    protected Collider2D col;
    protected Transform playerTransform;
    protected EclipseSystem playerEclipse;

    // 公开属性供其他脚本访问
    public Transform PlayerTransform => playerTransform;
    public EclipseSystem PlayerEclipse => playerEclipse;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercentage => currentHealth / maxHealth;

    // 受击闪烁
    private Coroutine hitFlashCoroutine;

    #region Unity生命周期

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        if (!mainSprite)
            mainSprite = GetComponent<SpriteRenderer>();
    }

    protected virtual void Start()
    {
        currentHealth = maxHealth;
        isAlive = true;
        canTakeDamage = true;

        if (mainSprite)
            mainSprite.color = normalColor;

        FindPlayer();
        SetupParticles();
    }

    protected virtual void Update()
    {
        if (!isAlive) return;

        // 基础更新逻辑
        UpdateEnemy();
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isAlive) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            HandlePlayerCollision(collision.gameObject);
        }
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        // 子类可以重写处理触发器碰撞
    }

    #endregion

    #region 抽象方法 - 必须由子类实现

    protected abstract void UpdateEnemy();
    protected abstract void HandlePlayerCollision(GameObject player);
    protected abstract void OnStateChanged(EnemyState newState);

    #endregion

    #region 通用功能 - 所有敌人都需要
    // 在BaseEnemy.cs中添加
    protected virtual void OnCollisionStay2D(Collision2D collision)
    {
        if (!isAlive) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            HandlePlayerCollision(collision.gameObject);
        }
    }

    // 或者使用OnTrigger
    protected virtual void OnTriggerStay2D(Collider2D other)
    {
        if (!isAlive) return;

        if (other.CompareTag("Player"))
        {
            HandlePlayerCollision(other.gameObject);
        }
    }
    public virtual void TakeDamage(float damage)
    {
        if (!isAlive || !canTakeDamage) return;

        // 减少生命值
        currentHealth -= damage;

        // 显示伤害数字
        ShowDamageNumber(damage);

        // 受伤效果
        PlayHitEffects();

        // 检查死亡
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // 受伤反馈
            OnTakeDamage(damage);
        }
    }

    protected virtual void ShowDamageNumber(float damage)
    {
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui)
        {
            ui.ShowDamageText(transform.position, Mathf.RoundToInt(damage), GetDamageColor());
        }
    }

    protected virtual Color GetDamageColor()
    {
        return new Color(1f, 0.8f, 0f); // 黄色
    }

    protected virtual void PlayHitEffects()
    {
        // 闪烁效果
        PlayHitFlash();

        // 受击动画
        PlayHitAnimation();

        // 粒子效果
        if (hitParticles)
            hitParticles.Play();
    }

    protected virtual void PlayHitFlash()
    {
        if (!mainSprite) return;

        if (hitFlashCoroutine != null)
            StopCoroutine(hitFlashCoroutine);

        hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
    }

    protected virtual void PlayHitAnimation()
    {
        if (hitAnimationPrefab != null)
        {
            GameObject hitAnimation = Instantiate(hitAnimationPrefab, transform.position, Quaternion.identity);
            hitAnimation.transform.localScale = Vector3.one * animationScaleMultiplier;

            // 自动销毁
            Animator animator = hitAnimation.GetComponent<Animator>();
            if (animator != null)
            {
                animator.Play("Hit", 0, 0f);
                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                if (clips.Length > 0)
                {
                    Destroy(hitAnimation, clips[0].length + 0.1f);
                }
                else
                {
                    Destroy(hitAnimation, 0.5f);
                }
            }
            else
            {
                Destroy(hitAnimation, 0.5f);
            }
        }
    }

    protected virtual void Die()
    {
        isAlive = false;
        canTakeDamage = false;

        // 禁用碰撞和物理
        if (col) col.enabled = false;
        if (rb) rb.isKinematic = true;

        // 播放死亡效果
        PlayDeathEffects();

        // 死亡回调
        OnDeath();

        // 销毁对象
        Destroy(gameObject, 1.5f); // 给足够时间播放动画
    }

    protected virtual void PlayDeathEffects()
    {
        // 死亡动画
        PlayDeathAnimation();

        // 停止移动粒子
        if (trailParticles)
        {
            trailParticles.Stop();
            var emission = trailParticles.emission;
            emission.rateOverTime = 0;
        }

        // 播放死亡粒子
        if (hitParticles)
            hitParticles.Play();
    }

    protected virtual void PlayDeathAnimation()
    {
        if (deathAnimationPrefab != null)
        {
            GameObject deathAnimation = Instantiate(deathAnimationPrefab, transform.position, Quaternion.identity);
            deathAnimation.transform.localScale = Vector3.one * animationScaleMultiplier;

            Animator animator = deathAnimation.GetComponent<Animator>();
            if (animator != null)
            {
                animator.Play("Death", 0, 0f);
                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                if (clips.Length > 0)
                {
                    Destroy(deathAnimation, clips[0].length + 0.2f);
                }
                else
                {
                    Destroy(deathAnimation, 1f);
                }
            }
            else
            {
                Destroy(deathAnimation, 1f);
            }
        }
    }

    protected virtual void OnTakeDamage(float damage)
    {
        // 子类可以重写添加特定受伤行为
    }

    protected virtual void OnDeath()
    {
        // 子类可以重写添加特定死亡行为
        Debug.Log($"{gameObject.name} 已死亡");
    }

    protected virtual void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            playerTransform = player.transform;
            playerEclipse = player.GetComponent<EclipseSystem>();
        }
    }

    protected virtual void SetupParticles()
    {
        if (trailParticles)
        {
            trailParticles.Stop();
            var main = trailParticles.main;
            main.startSpeed = -2f;

            var emission = trailParticles.emission;
            emission.rateOverTime = 0f;
        }
    }

    #endregion

    #region 协程

    private IEnumerator HitFlashRoutine()
    {
        if (!mainSprite) yield break;

        Color originalColor = mainSprite.color;
        mainSprite.color = Color.white;

        yield return new WaitForSeconds(0.1f);

        mainSprite.color = originalColor;

        hitFlashCoroutine = null;
    }

    #endregion

    #region 工具方法

    public float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    public void SetInvulnerable(bool invulnerable)
    {
        canTakeDamage = !invulnerable;
    }

    #endregion
}

// 通用敌人状态枚举
public enum EnemyState
{
    Idle,
    Patrol,
    Chase,
    Attack,
    Cooldown,
    Stunned,
    Dead
}