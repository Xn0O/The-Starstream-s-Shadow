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
    public int stopSpawnThreshold = 3;   // 停止生成小怪的阈值（≥3只停止）
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
    private Coroutine cleanupCoroutine;  // 清理协程

    // 小怪管理
    private List<GameObject> activeMinionList = new List<GameObject>();
    private bool shouldSpawnMinions = false; // 控制是否应该生成小怪

    protected override void Start()
    {
        base.Start();
        SetObjectState();
        StartStateCycle();
        ShowBossUI();

        // 启动定期清理协程
        if (cleanupCoroutine != null) StopCoroutine(cleanupCoroutine);
        cleanupCoroutine = StartCoroutine(CleanupInvalidMinionsRoutine());
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
            // 1. 隐藏状态（可召唤小怪阶段）
            ChangeState(MoleState.Hidden);
            shouldSpawnMinions = true; // 允许生成小怪
            StartSpawningCheck(); // 开始检查并生成小怪
            yield return new WaitForSeconds(hideTime);

            // 2. 警告状态（继续生成小怪）
            ChangeState(MoleState.Warning);
            yield return new WaitForSeconds(warningTime);

            // 3. 出现状态（停止召唤小怪，可攻击）
            ChangeState(MoleState.Emerged);
            shouldSpawnMinions = false; // 禁止生成小怪
            StopSpawning(); // 停止生成小怪
            yield return new WaitForSeconds(emergeTime);
        }
    }

    void ChangeState(MoleState newState)
    {
        currentState = newState;
        SetObjectState();
        canTakeDamage = (currentState == MoleState.Emerged);

        // 根据状态控制小怪生成
        if (currentState == MoleState.Emerged)
        {
            shouldSpawnMinions = false;
            StopSpawning();
        }
        else
        {
            shouldSpawnMinions = true;
            StartSpawningCheck();
        }
    }

    void SetObjectState()
    {
        if (hideObject) hideObject.SetActive(currentState == MoleState.Hidden);
        if (warningObject) warningObject.SetActive(currentState == MoleState.Warning);
        if (emergeObject) emergeObject.SetActive(currentState == MoleState.Emerged);
    }

    #region 小怪生成系统（智能阈值控制 + 自动清理）

    void StartSpawningCheck()
    {
        // 如果已经在生成、或不允许生成、或没有预制体，则不启动
        if (spawnCoroutine != null || !shouldSpawnMinions || minionPrefab == null)
            return;

        spawnCoroutine = StartCoroutine(SpawnMinionsCheckRoutine());
    }

    void StopSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    IEnumerator SpawnMinionsCheckRoutine()
    {
        // 只在允许生成且非出现状态下检查并生成小怪
        while (shouldSpawnMinions && currentState != MoleState.Emerged && isAlive)
        {
            yield return new WaitForSeconds(minionSpawnInterval);

            // 每次生成前先清理无效引用
            CleanInvalidMinionReferences();

            // 检查当前有效小怪数量是否小于阈值
            if (CanSpawnMinion())
            {
                SpawnMinion();
            }
            else
            {
                Debug.Log($"有效小怪数量 {activeMinionList.Count} ≥ 阈值 {stopSpawnThreshold}，暂停生成");
            }
        }
    }

    // 检查是否可以生成小怪
    bool CanSpawnMinion()
    {
        // 条件1：当前有效小怪数量小于停止阈值
        // 条件2：有预制体
        // 条件3：允许生成小怪
        // 条件4：当前状态不是出现状态
        return GetValidMinionCount() < stopSpawnThreshold &&
               minionPrefab != null &&
               shouldSpawnMinions &&
               currentState != MoleState.Emerged;
    }

    // 获取有效小怪数量
    int GetValidMinionCount()
    {
        int count = 0;
        for (int i = activeMinionList.Count - 1; i >= 0; i--)
        {
            if (activeMinionList[i] != null)
            {
                // 检查小怪是否还活着（如果有BaseEnemy组件）
                BaseEnemy enemy = activeMinionList[i].GetComponent<BaseEnemy>();
                if (enemy == null || enemy.isAlive)
                {
                    count++;
                }
                else
                {
                    // 小怪已死亡但还在列表中，移除它
                    activeMinionList.RemoveAt(i);
                    Debug.Log($"移除已死亡的小怪引用");
                }
            }
        }
        return count;
    }

    void SpawnMinion()
    {
        // 再次检查（防止协程延迟导致状态变化）
        if (!CanSpawnMinion())
            return;

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

        // 设置小怪标签
        minion.tag = "Enemy";

        // 启动监控协程，自动检测小怪死亡
        StartCoroutine(MonitorMinionLife(minion));

        Debug.Log($"生成小怪，有效数量: {GetValidMinionCount()}/{stopSpawnThreshold}");
    }

    // 监控小怪生命状态的协程
    IEnumerator MonitorMinionLife(GameObject minion)
    {
        if (minion == null) yield break;

        BaseEnemy minionEnemy = minion.GetComponent<BaseEnemy>();
        Transform minionTransform = minion.transform;

        // 持续监控直到小怪被销毁
        while (minion != null && minionTransform != null)
        {
            yield return new WaitForSeconds(0.5f); // 每0.5秒检查一次

            // 检查小怪是否还活着
            if (minionEnemy != null && !minionEnemy.isAlive)
            {
                // 小怪死亡，从列表中移除
                OnMinionDied(minion);
                yield break;
            }
        }

        // 如果执行到这里，说明小怪已经被销毁
        if (minion == null && activeMinionList.Contains(minion))
        {
            activeMinionList.Remove(minion);
            Debug.Log($"小怪被销毁，移除引用");
        }
    }

    // 定期清理无效引用的协程
    IEnumerator CleanupInvalidMinionsRoutine()
    {
        while (isAlive)
        {
            yield return new WaitForSeconds(2f); // 每2秒清理一次

            int removedCount = CleanInvalidMinionReferences();
            if (removedCount > 0)
            {
                Debug.Log($"自动清理了 {removedCount} 个无效小怪引用");

                // 清理后检查是否需要重新开始生成
                if (GetValidMinionCount() < stopSpawnThreshold && shouldSpawnMinions)
                {
                    // 确保没有正在运行的协程
                    if (spawnCoroutine != null)
                    {
                        StopCoroutine(spawnCoroutine);
                        spawnCoroutine = null;
                    }
                    StartSpawningCheck();
                }
            }
        }
    }

    // 清理无效的小怪引用
    int CleanInvalidMinionReferences()
    {
        int removedCount = 0;

        for (int i = activeMinionList.Count - 1; i >= 0; i--)
        {
            GameObject minion = activeMinionList[i];

            // 检查小怪是否已被销毁
            if (minion == null)
            {
                activeMinionList.RemoveAt(i);
                removedCount++;
                continue;
            }

            // 检查小怪是否还活着（如果有BaseEnemy组件）
            BaseEnemy enemy = minion.GetComponent<BaseEnemy>();
            if (enemy != null && !enemy.isAlive)
            {
                activeMinionList.RemoveAt(i);
                removedCount++;
            }
        }

        return removedCount;
    }

    // 小怪死亡时调用
    public void OnMinionDied(GameObject minion)
    {
        if (minion == null) return;

        bool wasRemoved = false;

        // 从列表中移除小怪
        for (int i = activeMinionList.Count - 1; i >= 0; i--)
        {
            if (activeMinionList[i] == minion)
            {
                activeMinionList.RemoveAt(i);
                wasRemoved = true;
                break;
            }
        }

        if (wasRemoved)
        {
            Debug.Log($"小怪死亡，有效数量: {GetValidMinionCount()}/{stopSpawnThreshold}");

            // 小怪死亡后，如果数量小于阈值且允许生成，重新开始生成检查
            if (GetValidMinionCount() < stopSpawnThreshold && shouldSpawnMinions)
            {
                // 确保没有正在运行的协程
                if (spawnCoroutine != null)
                {
                    StopCoroutine(spawnCoroutine);
                    spawnCoroutine = null;
                }
                StartSpawningCheck();
            }
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

        // 受伤后可能提前切换状态
        if (isAlive && Random.value < 0.3f)
        {
            StartCoroutine(EarlySwitchState());
        }
    }

    IEnumerator EarlySwitchState()
    {
        // 如果当前是出现状态，提前结束并开始生成小怪
        if (currentState == MoleState.Emerged)
        {
            ChangeState(MoleState.Hidden);
            shouldSpawnMinions = true; // 允许生成小怪
            StartSpawningCheck(); // 切换到隐藏状态后开始生成小怪
            yield return new WaitForSeconds(hideTime);
        }

        // 重新开始状态循环
        StartStateCycle();
    }

    protected override void OnDeath()
    {
        base.OnDeath();

        if (stateCoroutine != null)
            StopCoroutine(stateCoroutine);

        if (spawnCoroutine != null)
            StopCoroutine(spawnCoroutine);

        if (cleanupCoroutine != null)
            StopCoroutine(cleanupCoroutine);

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
        Debug.Log("Boss死亡，清理所有小怪");
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

    void OnGUI()
    {
        // 调试信息：显示当前小怪数量
        if (isAlive)
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 20;
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(10, 100, 300, 50),
                     $"小怪数量: {GetValidMinionCount()}/{stopSpawnThreshold} (列表: {activeMinionList.Count})", style);
            GUI.Label(new Rect(10, 130, 300, 50),
                     $"当前状态: {currentState}", style);
            GUI.Label(new Rect(10, 160, 300, 50),
                     $"允许生成: {shouldSpawnMinions}", style);
        }
    }
}