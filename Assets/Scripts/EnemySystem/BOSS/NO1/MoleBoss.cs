using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MoleBoss : BaseEnemy
{
    [Header("地鼠阶段设置")]
    public float emergeTime = 3f;        // 出现时间（可攻击）
    public float hideTime = 4f;          // 隐藏时间（无敌）
    public float warningTime = 1f;       // 出现前警告时间

    [Header("GameObject切换")]
    public GameObject hideObject;        // 隐藏状态的物体
    public GameObject warningObject;     // 警告状态的物体
    public GameObject emergeObject;      // 出现状态的物体

    [Header("小怪生成设置")]
    public GameObject minionPrefab;      // 召唤的小怪预制体
    public float minionSpawnInterval = 1f; // 生成小怪的时间间隔
    public int maxMinions = 5;           // 最大同时存在的小怪
    public Vector2[] spawnPositions;     // 可选：固定生成位置，如果为空则随机生成

    [Header("碰撞伤害设置")]
    public float collisionDamageInterval = 0.5f; // 碰撞伤害间隔
    private float collisionTimer = 0f;   // 碰撞计时器

    [Header("Boss UI设置")]
    public string bossName = "地鼠王";    // Boss名称（显示在UI上）
    public Color bossUIColor = Color.yellow; // Boss UI颜色

    // 状态相关
    private enum MoleState { Hidden, Warning, Emerged }
    private MoleState currentState = MoleState.Hidden;
    private Coroutine stateCoroutine;
    private Coroutine spawnCoroutine;

    // 小怪管理
    private List<GameObject> activeMinionList = new List<GameObject>();

    protected override void Start()
    {
        base.Start();
        SetObjectState();
        StartStateCycle();
        ShowBossUI();
    }

    protected override void UpdateEnemy()
    {
        // 更新碰撞计时器
        if (collisionTimer > 0)
        {
            collisionTimer -= Time.deltaTime;
        }
    }

    void StartStateCycle()
    {
        if (stateCoroutine != null) StopCoroutine(stateCoroutine);
        stateCoroutine = StartCoroutine(StateCycle());
    }

    IEnumerator StateCycle()
    {
        while (isAlive)
        {
            // 1. 隐藏状态
            ChangeState(MoleState.Hidden);
            StopSpawning(); // 停止生成小怪
            yield return new WaitForSeconds(hideTime);

            // 2. 警告状态
            ChangeState(MoleState.Warning);
            yield return new WaitForSeconds(warningTime);

            // 3. 出现状态
            ChangeState(MoleState.Emerged);
            StartSpawning(); // 开始生成小怪
            yield return new WaitForSeconds(emergeTime);
        }
    }

    void ChangeState(MoleState newState)
    {
        currentState = newState;
        SetObjectState();
        canTakeDamage = (currentState == MoleState.Emerged);
    }

    void SetObjectState()
    {
        if (hideObject) hideObject.SetActive(currentState == MoleState.Hidden);
        if (warningObject) warningObject.SetActive(currentState == MoleState.Warning);
        if (emergeObject) emergeObject.SetActive(currentState == MoleState.Emerged);
    }

    #region 小怪生成系统（简化版）

    void StartSpawning()
    {
        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnMinionsRoutine());
    }

    void StopSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    IEnumerator SpawnMinionsRoutine()
    {
        while (currentState == MoleState.Emerged && isAlive)
        {
            yield return new WaitForSeconds(minionSpawnInterval);

            if (activeMinionList.Count < maxMinions && minionPrefab != null)
            {
                SpawnMinion();
            }
        }
    }

    void SpawnMinion()
    {
        // 生成位置
        Vector2 spawnPos;

        if (spawnPositions != null && spawnPositions.Length > 0)
        {
            // 从预设位置中随机选择一个
            int randomIndex = Random.Range(0, spawnPositions.Length);
            spawnPos = (Vector2)transform.position + spawnPositions[randomIndex];
        }
        else
        {
            // 随机位置（以Boss为中心，半径2-5个单位）
            float radius = Random.Range(2f, 5f);
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            spawnPos = (Vector2)transform.position + new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius
            );
        }

        GameObject minion = Instantiate(minionPrefab, spawnPos, Quaternion.identity);
        activeMinionList.Add(minion);

        // 设置小怪死亡回调
        BaseEnemy minionEnemy = minion.GetComponent<BaseEnemy>();
        if (minionEnemy != null)
        {
            // 可以在这里初始化小怪
            minion.tag = "Enemy";

            // 监听小怪死亡事件（如果有的话）
            // minionEnemy.OnDeathEvent += OnMinionDied;
        }

        Debug.Log($"生成小怪，当前位置小怪数量: {activeMinionList.Count}");
    }

    // 小怪死亡时调用
    public void OnMinionDied(GameObject minion)
    {
        if (activeMinionList.Contains(minion))
        {
            activeMinionList.Remove(minion);
        }
    }

    #endregion

    #region 碰撞伤害系统（添加间隔）

    protected override void HandlePlayerCollision(GameObject player)
    {
        if (!isAlive || !canTakeDamage || collisionTimer > 0)
            return;
        EclipseSystem Eclipce = player.GetComponent<EclipseSystem>();
        PlayerController playerCtrl = player.GetComponent<PlayerController>();
        if (playerCtrl)
        {
            // 造成伤害
            playerCtrl.TakeDamage(collisionDamage);
            Eclipce.AddLayer(layerAddAmount);
            // 重置计时器
            collisionTimer = collisionDamageInterval;

            Debug.Log($"对玩家造成 {collisionDamage} 伤害，下次伤害在 {collisionDamageInterval} 秒后");
        }
    }

    #endregion

    #region 重写BaseEnemy方法

    public override void TakeDamage(float damage)
    {
        if (!canTakeDamage)
        {
            UIManager ui = FindObjectOfType<UIManager>();
            if (ui) ui.ShowDamageText(transform.position, 0, Color.gray);
            return;
        }

        base.TakeDamage(damage);
        UpdateBossUI();

        // 受伤后可能提前隐藏
        if (isAlive && Random.value < 0.3f)
        {
            StartCoroutine(EarlyHide());
        }
    }

    IEnumerator EarlyHide()
    {
        StopSpawning(); // 停止生成小怪
        ChangeState(MoleState.Hidden);
        yield return new WaitForSeconds(hideTime);
        StartStateCycle();
    }

    protected override void OnDeath()
    {
        base.OnDeath();

        if (stateCoroutine != null)
            StopCoroutine(stateCoroutine);

        StopSpawning(); // 停止生成小怪
        HideBossUI();
        ClearMinions();
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

    #region 小怪清理

    void ClearMinions()
    {
        foreach (GameObject minion in activeMinionList)
        {
            if (minion != null)
                Destroy(minion);
        }
        activeMinionList.Clear();
    }

    #endregion

    #region 调试和编辑

    void OnDrawGizmosSelected()
    {
        // 绘制生成位置
        if (spawnPositions != null)
        {
            Gizmos.color = Color.green;
            foreach (Vector2 pos in spawnPositions)
            {
                Vector2 worldPos = (Vector2)transform.position + pos;
                Gizmos.DrawWireSphere(worldPos, 0.5f);
                Gizmos.DrawLine(transform.position, worldPos);
            }
        }

        // 绘制生成区域（如果使用随机生成）
        if (spawnPositions == null || spawnPositions.Length == 0)
        {
            Gizmos.color = new Color(0, 1, 0, 0.2f);
            Gizmos.DrawWireSphere(transform.position, 3f);
        }

        // 状态指示
        Gizmos.color = GetStateColor();
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.5f);
    }

    Color GetStateColor()
    {
        switch (currentState)
        {
            case MoleState.Hidden: return Color.gray;
            case MoleState.Warning: return Color.yellow;
            case MoleState.Emerged: return Color.red;
            default: return Color.white;
        }
    }

    #endregion
}