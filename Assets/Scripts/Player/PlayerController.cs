using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    [Header("�ƶ�����")]
    public float moveSpeed = 5f;
    public GameObject UpPic;
    public GameObject DownPic;
    public GameObject LeftPic;
    public GameObject RightPic;

    [Header("����������")]
    public float directionThreshold = 0.3f; // ��������ֵ

    [Header("�������ȼ�")]
    public bool verticalPriority = true; // true:��ֱ����, false:ˮƽ����

    [Header("��ʼ����")]
    public Direction initialDirection = Direction.Down; // ��ʼ��ʾ�ķ���

    [Header("����ֵ")]
    public float maxHealth = 100f;
    public float currentHealth;
    public bool isDead = false; // ����������״̬��־

    [Header("����������")]
    public float damageShakeDuration = 0.15f;
    public float damageShakeMagnitude = 0.1f;

    [Header("���˺�������")]
    public float heavyDamageShakeDuration = 0.25f;
    public float heavyDamageShakeMagnitude = 0.2f;
    public float heavyDamageThreshold = 10f; // �������˺��𶯵��˺���ֵ

    [Header("�ܻ���Ч")]
    public GameObject hitEffectPrefab; // �ܻ���ЧԤ����
    public Vector3 hitEffectOffset = new Vector3(0, 0, 0); // ��Чλ��ƫ��
    public Transform hitEffectSpawnPoint; // ��Ч���ɵ㣨��ѡ�������������ʹ�����λ�ã�

    [Header("��������")]
    public GameObject deathObjectPrefab; // ����ʱ�滻��GameObject����ѡ��
    public GameObject deathUI; // ��������UI����ѡ��ֱ���ڳ��������룩
    public float deathDelay = 2f; // �������ӳ����¼��ص�ʱ��

    [Header("����ϵͳ")]
    public float jumpForce = 10f;
    public float groundCheckDistance = 0.2f;
    public LayerMask groundLayer = 1;
    public KeyCode jumpKey = KeyCode.Space;
    public Transform groundCheckPoint; // �����ţ���Ĭ��ʹ�ñ������ĵ�
    public GroundDetector groundDetector; // �����ӵķֲ�ⲿ����ѡ
    [SerializeField] private bool isGrounded = false;

    private Rigidbody2D rb;
    private ScreenEffectManager screenEffectManager;
    private GameObject currentHitEffect; // ��ǰ���ڲ��ŵ��ܻ���Ч

    // ��¼���һ����Ч�ķ���
    public enum Direction { None, Up, Down, Left, Right }
    private Direction lastValidDirection = Direction.Down; // Ĭ����Ϊ����

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;
        isDead = false; // ȷ����ʼʱ��������״̬

        // ��ʼ�������Ч����Ϊ��ʼ����
        lastValidDirection = initialDirection;

        // ��ʼ��ʾ���µ�ͼ
        ShowDirectionPic(initialDirection);

        // ��ȡ��ĻЧ��������
        screenEffectManager = FindObjectOfType<ScreenEffectManager>();
        if (screenEffectManager == null)
        {
            Debug.LogWarning("δ�ҵ�ScreenEffectManager��������Ч����������");
        }

        // ��������UI��������ڣ�
        if (deathUI != null)
        {
            deathUI.SetActive(false);
        }
    }

    void Update()
    {
        // ��������������ֹ�ƶ�����
        if (isDead)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        // 移动输入（本项目是俯视角四方向移动，X/Y 都要写入速度）
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        Vector2 moveDirection = new Vector2(moveX, moveY).normalized;
        rb.velocity = moveDirection * moveSpeed;

        // 实时更新方向图片
        UpdateDirectionPics(moveDirection);
    }

    void CheckGrounded()
    {
        // ���ȼ��GroundDetector
        if (groundDetector != null)
        {
            isGrounded = groundDetector.CheckGrounded();
            return;
        }

        // ��������ڲ����
        Vector2 origin = groundCheckPoint ? groundCheckPoint.position : transform.position;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundLayer);
        isGrounded = hit.collider != null;
    }

    void HandleJump()
    {
        if (Input.GetKeyDown(jumpKey) && isGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        }
    }

    // �����˹���ʱ����
    public void TakeDamage(float damage)
    {
        // ����Ѿ������������ܵ��˺�
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        Debug.Log($"����ܵ��˺�: {damage}, ��ǰ����: {currentHealth}");

        // ����UI��ʾ�˺����ֲ�ͳ��������˺�
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui)
        {
            ui.ShowPlayerTakenDamageText(transform.position, damage, Color.red);
        }

        // ���������𶯷���
        ApplyDamageShake(damage);

        // ����˺�ֵ�ﵽ���˺���ֵ�������ܻ���Ч
        if (damage >= heavyDamageThreshold)
        {
            PlayHitEffect();
        }

        // �������ֵ�Ƿ�С�ڵ���0
        if (currentHealth <= 0 && !isDead)
        {
            Die();
        }
    }

    // �����������
    private void Die()
    {
        isDead = true;
        Debug.Log("���������");

        // ֹͣ�ƶ�
        rb.velocity = Vector2.zero;

        // ��������GameObject����������ˣ�
        if (deathObjectPrefab != null)
        {
            GameObject deathObj = Instantiate(deathObjectPrefab, transform.position, Quaternion.identity);
            deathObj.name = "PlayerDeathObject";
        }

        // ���������ͼ
        HideAllDirectionPics();

        // ������ҿ���
        DisablePlayerControl();

        // ��ʾ�������棨��������ˣ�
        if (deathUI != null)
        {
            deathUI.SetActive(true);
        }

        // �ӳ����¼��س���
        Invoke("ReloadCurrentScene", deathDelay);
    }

    // ������ҿ���
    private void DisablePlayerControl()
    {
        // ����Rigidbody2D
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.simulated = false; // ֹͣ����ģ��
        }

        // ����������ײ��
        var colliders = GetComponents<Collider2D>();
        foreach (var collider in colliders)
        {
            collider.enabled = false;
        }

        Debug.Log("��ҿ����ѽ���");
    }

    // ���¼��ص�ǰ����
    private void ReloadCurrentScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ����
    public void Heal(float amount)
    {
        // ����Ѿ���������������
        if (isDead) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

        Debug.Log($"�������: {amount}, ��ǰ����: {currentHealth}");

        // ����UI��ʾ��������
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui)
        {
            ui.ShowHealText(transform.position, amount, Color.green);
        }
    }

    // ���·���ͼƬ��ʾ
    void UpdateDirectionPics(Vector2 moveDirection)
    {
        // ��������������·���
        if (isDead) return;

        // ����ƶ�����
        bool isMovingUp = moveDirection.y > directionThreshold;
        bool isMovingDown = moveDirection.y < -directionThreshold;
        bool isMovingLeft = moveDirection.x < -directionThreshold;
        bool isMovingRight = moveDirection.x > directionThreshold;

        // ������ƶ�����
        if (moveDirection.magnitude > directionThreshold)
        {
            if (verticalPriority)
            {
                // ��ֱ����
                if (isMovingUp)
                {
                    ShowDirectionPic(Direction.Up);
                }
                else if (isMovingDown)
                {
                    ShowDirectionPic(Direction.Down);
                }
                else if (isMovingLeft)
                {
                    ShowDirectionPic(Direction.Left);
                }
                else if (isMovingRight)
                {
                    ShowDirectionPic(Direction.Right);
                }
            }
            else
            {
                // ˮƽ����
                if (isMovingLeft)
                {
                    ShowDirectionPic(Direction.Left);
                }
                else if (isMovingRight)
                {
                    ShowDirectionPic(Direction.Right);
                }
                else if (isMovingUp)
                {
                    ShowDirectionPic(Direction.Up);
                }
                else if (isMovingDown)
                {
                    ShowDirectionPic(Direction.Down);
                }
            }
        }
        else
        {
            // û���ƶ����룬�������һ����Ч�ķ���
            KeepLastDirection();
        }
    }

    // ��ʾָ���������ͼ
    void ShowDirectionPic(Direction direction)
    {
        // �������������ʾ������ͼ
        if (isDead) return;

        // ����������ͼ
        HideAllDirectionPics();

        // ��ʾָ���������ͼ
        switch (direction)
        {
            case Direction.Up:
                if (UpPic != null) UpPic.SetActive(true);
                break;
            case Direction.Down:
                if (DownPic != null) DownPic.SetActive(true);
                break;
            case Direction.Left:
                if (LeftPic != null) LeftPic.SetActive(true);
                break;
            case Direction.Right:
                if (RightPic != null) RightPic.SetActive(true);
                break;
        }

        // ��¼��ǰ����
        lastValidDirection = direction;
    }

    // �������һ�εķ���
    void KeepLastDirection()
    {
        if (isDead) return;

        if (lastValidDirection != Direction.None)
        {
            ShowDirectionPic(lastValidDirection);
        }
        else
        {
            // �������û�й���Ч������ʾ��ʼ����
            ShowDirectionPic(initialDirection);
        }
    }

    // �������з�����ͼ
    void HideAllDirectionPics()
    {
        if (UpPic != null) UpPic.SetActive(false);
        if (DownPic != null) DownPic.SetActive(false);
        if (LeftPic != null) LeftPic.SetActive(false);
        if (RightPic != null) RightPic.SetActive(false);
    }

    // ǿ����ʾָ�����򣨿��Դ��ⲿ���ã�
    public void SetDirection(Direction direction)
    {
        if (isDead) return;

        lastValidDirection = direction;
        ShowDirectionPic(direction);
    }

    // �����ܻ���Ч���򻯰棩
    private void PlayHitEffect()
    {
        // ����Ѿ���һ���ܻ���Ч�ڲ��ţ���������
        if (currentHitEffect != null)
        {
            Destroy(currentHitEffect);
        }

        // ����Ƿ����ܻ���ЧԤ����
        if (hitEffectPrefab == null)
        {
            Debug.LogWarning("�ܻ���ЧԤ����δ���䣬����Inspector�з���Hit Effect Prefab");
            return;
        }

        // ȷ����Ч����λ��
        Vector3 spawnPosition;
        if (hitEffectSpawnPoint != null)
        {
            // ʹ��ָ�������ɵ�
            spawnPosition = hitEffectSpawnPoint.position + hitEffectOffset;
        }
        else
        {
            // ʹ�����λ�ü���ƫ��
            spawnPosition = transform.position + hitEffectOffset;
        }

        // ʵ�����ܻ���Ч����Ч���Զ����٣�
        currentHitEffect = Instantiate(hitEffectPrefab, spawnPosition, Quaternion.identity);

        Debug.Log("�����ܻ���Ч");
    }

    // Ӧ��������Ч��
    private void ApplyDamageShake(float damage)
    {
        if (screenEffectManager == null) return;

        // ����Ƿ�Ϊ���˺�
        if (damage >= heavyDamageThreshold)
        {
            // ʹ�ô��˺���Ч��
            ApplyHeavyDamageShake(damage);
        }
        else
        {
            // ʹ����ͨ�˺���Ч��
            ApplyNormalDamageShake(damage);
        }
    }

    // Ӧ����ͨ�˺���Ч��
    private void ApplyNormalDamageShake(float damage)
    {
        // �����˺�ֵ������ǿ��
        float intensityMultiplier = Mathf.Clamp(damage / 30f, 0.3f, 1.5f);
        float actualShakeMagnitude = damageShakeMagnitude * intensityMultiplier;
        float actualShakeDuration = damageShakeDuration * Mathf.Clamp(intensityMultiplier, 0.8f, 1.5f);

        // Ӧ����Ļ��
        screenEffectManager.ShakeCamera(actualShakeDuration, actualShakeMagnitude);
    }

    // Ӧ�ô��˺���Ч��
    private void ApplyHeavyDamageShake(float damage)
    {
        // �����˺�ֵ�������˺���ǿ��
        float intensityMultiplier = Mathf.Clamp(damage / 50f, 0.8f, 2.5f);
        float actualShakeMagnitude = heavyDamageShakeMagnitude * intensityMultiplier;
        float actualShakeDuration = heavyDamageShakeDuration * Mathf.Clamp(intensityMultiplier, 1f, 2f);

        // Ӧ�ø�ǿ����Ļ��
        screenEffectManager.ShakeCamera(actualShakeDuration, actualShakeMagnitude);

        // ���Ӻ�ɫ����Ч��
        Color flashColor = new Color(1f, 0.2f, 0.2f, 0.5f); // ��ɫ���⣬���ߵ�͸����
        float flashDuration = 0.2f + (damage * 0.005f); // �����˺������������ʱ��
        screenEffectManager.FlashScreen(flashColor, flashDuration);

        Debug.Log($"���˺���: �˺�={damage}, ��ǿ��={actualShakeMagnitude}, ����ʱ��={actualShakeDuration}");
    }

    // ���ӻ��߿��ӻ� - Scene��ͼ�пɼ�
    void OnDrawGizmos()
    {
        if (isDead) return;

        Vector2 origin = groundCheckPoint ? groundCheckPoint.position : transform.position;
        Vector2 end = origin + Vector2.down * groundCheckDistance;

        // �Ƿ���أ���=�̣���=��
        Gizmos.color = isGrounded ? Color.green : Color.red;

        // ������
        Gizmos.DrawLine(origin, end);

        // ���˵㻭һ��СԲȦ
        Gizmos.DrawWireSphere(end, 0.05f);

        // ���ԭ�㻭һ��С�㣨���ײ����Ӿ���
        Gizmos.DrawWireSphere(origin, 0.03f);
    }

    // ���Է���
    [ContextMenu("��������")]
    public void TestDeath()
    {
        TakeDamage(currentHealth + 1); // �ܵ�������ǰ�������˺�
    }
}