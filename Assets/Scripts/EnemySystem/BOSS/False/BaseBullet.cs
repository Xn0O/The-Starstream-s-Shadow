// BaseBullet.cs - 子弹基类
using UnityEngine;

public class BaseBullet : MonoBehaviour
{
    [Header("基础属性")]
    public float speed = 10f;
    public float damage = 10f;
    public float lifetime = 3f;
    public bool canBeParried = true;

    [Header("视觉")]
    public Color normalColor = Color.red;
    public Color parryableColor = Color.yellow;
    public Color parriedColor = Color.cyan;

    [Header("组件")]
    public SpriteRenderer spriteRenderer;
    public Collider2D bulletCollider;

    // 状态
    protected Rigidbody2D rb;
    protected Vector2 moveDirection;
    protected bool isParried = false;
    protected bool hasHitPlayer = false;

    // 弹反相关
    protected int parriedPlayerLayers = 0;
    protected PlayerParrySystem parrySystem;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        if (!bulletCollider) bulletCollider = GetComponent<Collider2D>();
    }

    protected virtual void Start()
    {
        // 自动销毁
        Destroy(gameObject, lifetime);

        // 设置初始颜色
        if (spriteRenderer)
            spriteRenderer.color = canBeParried ? parryableColor : normalColor;
    }

    protected virtual void Update()
    {
        if (rb && !isParried)
        {
            rb.velocity = moveDirection * speed;
        }

        // 旋转效果
        transform.Rotate(0, 0, 360 * Time.deltaTime);
    }

    public virtual void Initialize(Vector2 direction, float spd = -1, float dmg = -1)
    {
        moveDirection = direction.normalized;

        if (spd > 0) speed = spd;
        if (dmg > 0) damage = dmg;

        if (rb)
            rb.velocity = moveDirection * speed;
    }

    public virtual void Reflect(Vector2 reflectDirection, int playerLayers, PlayerParrySystem parrySys)
    {
        if (isParried || !canBeParried) return;

        isParried = true;
        parriedPlayerLayers = playerLayers;
        parrySystem = parrySys;
        moveDirection = reflectDirection;
        speed *= 1.5f; // 弹反后速度加快

        // 视觉变化
        if (spriteRenderer)
            spriteRenderer.color = parriedColor;

        // 重新设置速度
        if (rb)
            rb.velocity = moveDirection * speed;

        // 延长存在时间
        Destroy(gameObject, lifetime * 2f);

        Debug.Log($"子弹被弹反，玩家层数: {playerLayers}");
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        // 已经被弹反的子弹
        if (isParried)
        {
            // 击中敌人
            if (other.CompareTag("Enemy"))
            {
                OnHitEnemy(other.gameObject);
                return;
            }

            // 被弹反的子弹不应该再击中玩家
            if (other.CompareTag("Player"))
            {
                return;
            }
        }
        else
        {
            // 正常子弹击中玩家
            if (other.CompareTag("Player"))
            {
                OnHitPlayer(other.gameObject);
                return;
            }
        }
    }

    protected virtual void OnHitPlayer(GameObject player)
    {
        if (hasHitPlayer) return;

        PlayerController playerCtrl = player.GetComponent<PlayerController>();
        EclipseSystem eclipse = player.GetComponent<EclipseSystem>();

        if (playerCtrl && eclipse)
        {
            playerCtrl.TakeDamage(damage);
            eclipse.AddLayer(1); // 每颗子弹添加1层
            hasHitPlayer = true;

            Debug.Log($"子弹击中玩家，造成 {damage} 伤害，添加1层");
        }

        Destroy(gameObject);
    }

    protected virtual void OnHitEnemy(GameObject enemy)
    {
        if (!isParried) return;

        BaseEnemy baseEnemy = enemy.GetComponent<BaseEnemy>();
        if (baseEnemy)
        {
            // 计算弹反伤害（基于玩家层数）
            float reflectDamage = damage * (1 + parriedPlayerLayers * 0.1f);
            baseEnemy.TakeDamage(reflectDamage);

            // 通知弹反系统
            if (parrySystem != null)
            {
                parrySystem.OnParriedBulletHitEnemy((int)reflectDamage, enemy);
            }

            Debug.Log($"弹反子弹击中敌人，造成 {reflectDamage} 伤害");
        }

        Destroy(gameObject);
    }

    public Vector2 GetDirection()
    {
        return moveDirection;
    }

    public void SetParryable(bool parryable)
    {
        canBeParried = parryable;
        if (spriteRenderer)
        {
            spriteRenderer.color = parryable ? parryableColor : normalColor;
        }
    }
}