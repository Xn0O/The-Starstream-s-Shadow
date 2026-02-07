// BossBullet.cs - 修复版
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BossBullet : MonoBehaviour
{
    [Header("子弹属性")]
    public float speed = 10f;
    public float damage = 10f;
    public float lifetime = 3f;
    public bool canBeParried = true;

    [Header("视觉")]
    public Color normalColor = Color.red;
    public Color parryableColor = Color.yellow;
    public Color parriedColor = Color.cyan;

    // 组件
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Collider2D bulletCollider;

    // 状态
    private Vector2 moveDirection;
    private bool isParried = false;
    private bool hasHitSomething = false; // 新增：防止多次碰撞

    // 弹反相关
    private int parriedPlayerLayers = 0;
    private PlayerParrySystem parrySystem;
    private ChargeMasterBoss ownerBoss;
    private bool isFastBullet = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        bulletCollider = GetComponent<Collider2D>();

        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (!bulletCollider) bulletCollider = GetComponent<Collider2D>();
    }

    void Start()
    {
        // 自动销毁
        Destroy(gameObject, lifetime);

        // 设置初始颜色
        UpdateVisual();
    }

    void Update()
    {
        if (rb && !isParried)
        {
            rb.velocity = moveDirection * speed;
        }

        // 旋转效果
        transform.Rotate(0, 0, 360 * Time.deltaTime);
    }

    public void Initialize(Vector2 direction, float spd, float dmg, ChargeMasterBoss boss = null, bool fast = false)
    {
        moveDirection = direction.normalized;
        speed = spd > 0 ? spd : speed;
        damage = dmg > 0 ? dmg : damage;
        ownerBoss = boss;
        isFastBullet = fast;

        if (rb)
            rb.velocity = moveDirection * speed;

        UpdateVisual();
    }

    void UpdateVisual()
    {
        if (!spriteRenderer) return;

        if (isParried)
        {
            spriteRenderer.color = parriedColor;
        }
        else if (isFastBullet)
        {
            spriteRenderer.color = new Color(1f, 0.6f, 0.6f); // 浅红色表示快速
        }
        else
        {
            spriteRenderer.color = new Color(0.6f, 0.6f, 1f); // 浅蓝色表示慢速
        }
    }

    public void Reflect(Vector2 reflectDirection, int playerLayers, PlayerParrySystem parrySys)
    {
        if (isParried || !canBeParried) return;

        isParried = true;
        parriedPlayerLayers = playerLayers;
        parrySystem = parrySys;
        moveDirection = reflectDirection;
        speed *= 1.5f; // 弹反后速度加快

        // 视觉变化
        UpdateVisual();

        // 重新设置速度
        if (rb)
            rb.velocity = moveDirection * speed;

        // 延长存在时间
        Destroy(gameObject, lifetime * 2f);

        Debug.Log($"子弹被弹反，玩家层数: {playerLayers}, 新方向: {reflectDirection}");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHitSomething) return; // 防止多次碰撞

        // 已经被弹反的子弹
        if (isParried)
        {
            // 击中敌人（包括BOSS）
            if (other.CompareTag("Enemy"))
            {
                hasHitSomething = true;
                OnHitEnemy(other.gameObject);
                return;
            }
        }
        else
        {
            // 正常子弹击中玩家
            if (other.CompareTag("Player"))
            {
                hasHitSomething = true;
                OnHitPlayer(other.gameObject);
                return;
            }
        }
    }

    void OnHitPlayer(GameObject player)
    {
        PlayerController playerCtrl = player.GetComponent<PlayerController>();
        EclipseSystem eclipse = player.GetComponent<EclipseSystem>();

        if (playerCtrl && eclipse)
        {
            playerCtrl.TakeDamage(damage);
            eclipse.AddLayer(1); // 每颗子弹添加1层

            Debug.Log($"子弹击中玩家，造成 {damage} 伤害，添加1层");
        }

        // 立即销毁
        Destroy(gameObject);
    }

    void OnHitEnemy(GameObject enemy)
    {
        // 确保不是击中自己（如果有ownerBoss）
        if (ownerBoss != null && enemy.gameObject == ownerBoss.gameObject)
        {
            // 计算弹反伤害（基于玩家层数）
            float reflectDamage = damage * (1 + parriedPlayerLayers * 0.1f);

            // 对BOSS造成伤害
            ownerBoss.TakeDamage(reflectDamage);

            // 通知BOSS子弹被弹反
            ownerBoss.OnBulletParried(this, parriedPlayerLayers);

            Debug.Log($"弹反子弹击中BOSS，造成 {reflectDamage} 伤害");
        }
        else
        {
            // 击中其他敌人
            BaseEnemy otherEnemy = enemy.GetComponent<BaseEnemy>();
            if (otherEnemy)
            {
                float reflectDamage = damage * (1 + parriedPlayerLayers * 0.1f);
                otherEnemy.TakeDamage(reflectDamage);
                Debug.Log($"弹反子弹击中其他敌人，造成 {reflectDamage} 伤害");
            }
        }

        // 立即销毁
        Destroy(gameObject);
    }

    public Vector2 GetDirection()
    {
        return moveDirection;
    }
}