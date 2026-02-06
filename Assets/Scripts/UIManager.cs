using UnityEngine;
using UnityEngine.UI;
using System.Collections; // 关键：添加协程必需的命名空间
using System.Collections.Generic;
using System.Linq;

public class UIManager : MonoBehaviour
{
    [Header("生命值UI")]
    public Text healthText;
    public Slider healthSlider;

    [Header("星蚀UI")]
    public Text eclipseText;
    public Image eclipseBar;

    [Header("释放提示")]
    public Text releaseHintText;
    public GameObject releaseHintObject;

    [Header("引用")]
    public PlayerController playerController;
    public EclipseSystem eclipseSystem;
    public ReleaseSystem releaseSystem;

    [Header("伤害数字")]
    public GameObject damageTextPrefab;
    public Canvas gameCanvas;

    [Header("对象池设置")]
    public int damageTextPoolSize = 20;

    [Header("文本偏移设置")]
    [Tooltip("水平方向最大随机偏移")]
    public float horizontalOffset = 40f;
    [Tooltip("垂直方向最大随机偏移")]
    public float verticalOffset = 20f;

    // 对象池相关变量
    private Queue<GameObject> damageTextPool;
    private Queue<GameObject> activeDamageTexts;
    // Z轴偏移计数器 - 防止伤害数字重叠
    private float zOffsetCounter = 0f;

    // 防止重复显示的防抖机制
    private Dictionary<string, float> lastDamageTime = new Dictionary<string, float>();
    private readonly float damageCooldown = 0.1f; // 100毫秒内不重复显示同一伤害/治疗

    [Header("伤害/治疗统计设置")]
    public float statsTimeWindow = 5f; // 统计时间窗口（秒）- 保留无影响，不删除

    // 核心统计变量
    private float playerTotalTakenDamage; // 玩家受到的总伤害（保留）
    private float maxDealDamage;          // 玩家造成的最大单次伤害（保留）
    private float maxHealing;             // 最大单次治疗（保留）
    private int totalEclipseLayerAccumulated; // 新增：星蚀累计层数（独立于实时层数，释放不消耗）

    // 数据结构 - 保留无影响，不删除（避免修改其他逻辑）
    [System.Serializable]
    private class DamageRecord
    {
        public float amount;   // 数值
        public float timestamp;// 时间戳
        public DamageRecord(float amount)
        {
            this.amount = amount;
            this.timestamp = Time.time;
        }
    }

    [System.Serializable]
    private class HealRecord
    {
        public float amount;   // 数值
        public float timestamp;// 时间戳
        public HealRecord(float amount)
        {
            this.amount = amount;
            this.timestamp = Time.time;
        }
    }

    [Header("统计面板UI")]
    public GameObject statsPanel;                // 统计面板根对象
    public KeyCode toggleStatsKey = KeyCode.Tab; // 切换面板显示/隐藏的按键
    public bool statsPanelVisible = false;       // 面板当前显隐状态
    private CanvasGroup statsPanelCanvasGroup;   // 面板淡入淡出组件（自动添加/获取）

    [Header("统计面板")]
    public Text playerTakenTotalDamageText;  // 玩家累计受到伤害数（保留）
    public Text playerDealMaxDamageText;     // 玩家对敌人造成最大伤害数（保留）
    public Text playerHealMaxText;           // 最大治疗数（保留）
    public Text playerEclipseTotalLayerText; // 星蚀累计层数（保留，改为显示累计值）

    [Header("面板动画设置")]
    public float panelFadeTime = 0.3f;               // 面板淡入淡出时间

    [Header("Boss UI")]
    public GameObject bossHealthBar;          // Boss血条面板根对象
    public Text bossNameText;                 // Boss名称文本
    public Slider bossHealthSlider;           // Boss血条滑动条
    public Image bossHealthFill;              // Boss血条填充图片
    public Text bossHealthText;               // Boss血量数值文本

    #region Boss血条相关功能
    /// <summary>
    /// 显示Boss血条并初始化数据
    /// </summary>
    /// <param name="bossName">Boss名称</param>
    /// <param name="currentHealth">当前血量</param>
    /// <param name="maxHealth">最大血量</param>
    /// <param name="color">血条填充颜色</param>
    public void ShowBossHealthBar(string bossName, float currentHealth, float maxHealth, Color color)
    {
        if (!bossHealthBar) return;

        bossHealthBar.SetActive(true);

        if (bossNameText) bossNameText.text = bossName;
        if (bossHealthSlider)
        {
            bossHealthSlider.maxValue = maxHealth;
            bossHealthSlider.value = currentHealth;
        }
        if (bossHealthFill) bossHealthFill.color = color;

        UpdateBossHealthText(currentHealth, maxHealth);
    }

    /// <summary>
    /// 更新Boss当前血量
    /// </summary>
    /// <param name="currentHealth">当前血量</param>
    /// <param name="maxHealth">最大血量</param>
    public void UpdateBossHealth(float currentHealth, float maxHealth)
    {
        if (!bossHealthBar || !bossHealthBar.activeSelf) return;

        if (bossHealthSlider)
        {
            bossHealthSlider.value = currentHealth;
        }

        UpdateBossHealthText(currentHealth, maxHealth);
    }

    /// <summary>
    /// 更新Boss血量文本显示
    /// </summary>
    /// <param name="currentHealth">当前血量</param>
    /// <param name="maxHealth">最大血量</param>
    void UpdateBossHealthText(float currentHealth, float maxHealth)
    {
        if (bossHealthText)
        {
            bossHealthText.text = $"{Mathf.RoundToInt(currentHealth)}/{Mathf.RoundToInt(maxHealth)}";
        }
    }

    /// <summary>
    /// 隐藏Boss血条
    /// </summary>
    public void HideBossHealthBar()
    {
        if (bossHealthBar) bossHealthBar.SetActive(false);
    }

    /// <summary>
    /// Boss受到伤害时显示伤害数字（调用通用伤害数字显示方法）
    /// </summary>
    /// <param name="position">伤害数字显示位置</param>
    /// <param name="damage">伤害值</param>
    /// <param name="color">伤害数字颜色</param>
    public void ShowBossDamageText(Vector3 position, float damage, Color color)
    {
        ShowDamageText(position, damage, color);
    }
    #endregion

    private void Start()
    {
        // 初始化伤害数字对象池
        InitializeObjectPools();
        // 自动获取缺失的核心脚本引用
        AutoGetReference();
        // 初始化生命值滑动条
        InitializeHealthSlider();
        // 初始化UI显示（生命值/星蚀/释放提示）
        UpdateHealthUI();
        UpdateEclipseUI();
        // 初始化统计面板
        InitializeStatsPanel();
        // 初始化统计数据
        InitStatsData();

        Debug.Log($"伤害/治疗统计系统初始化完成，按 {toggleStatsKey} 键显示/隐藏统计面板");
    }

    private void Update()
    {
        // 实时更新核心UI（生命值/星蚀/释放提示）
        UpdateHealthUI();
        UpdateEclipseUI();
        UpdateReleaseHint();

        // 清理过期的伤害数字对象，回收至对象池
        CleanupActiveTexts();

        // 检测按键切换统计面板
        if (Input.GetKeyDown(toggleStatsKey))
        {
            ToggleStatsPanel();
        }

        // 面板可见时，实时更新统计数据显示
        if (statsPanelVisible && statsPanel)
        {
            UpdateStatsPanel();
        }
    }

    #region 初始化相关方法
    /// <summary>
    /// 初始化伤害数字对象池
    /// </summary>
    void InitializeObjectPools()
    {
        damageTextPool = new Queue<GameObject>();
        activeDamageTexts = new Queue<GameObject>();

        if (damageTextPrefab && gameCanvas)
        {
            for (int i = 0; i < damageTextPoolSize; i++)
            {
                GameObject damageTextObj = Instantiate(damageTextPrefab);
                damageTextObj.transform.SetParent(gameCanvas.transform, false);
                damageTextObj.SetActive(false);
                damageTextPool.Enqueue(damageTextObj);
            }
            Debug.Log($"伤害数字对象池初始化完成，大小: {damageTextPoolSize}");
        }
        else
        {
            Debug.LogWarning("伤害数字预制体或Canvas未设置，对象池初始化失败");
        }
    }

    /// <summary>
    /// 自动获取缺失的核心脚本引用（防止Inspector未赋值）
    /// </summary>
    private void AutoGetReference()
    {
        if (!playerController)
            playerController = FindObjectOfType<PlayerController>();
        if (!eclipseSystem)
            eclipseSystem = FindObjectOfType<EclipseSystem>();
        if (!releaseSystem)
            releaseSystem = FindObjectOfType<ReleaseSystem>();
        if (!gameCanvas)
            gameCanvas = FindObjectOfType<Canvas>();
        if (!healthSlider)
        {
            // 尝试从子物体查找生命值滑动条
            healthSlider = GetComponentInChildren<Slider>(true);
            if (healthSlider)
            {
                Debug.Log("重新获取到Slider组件");
            }
        }
    }

    /// <summary>
    /// 初始化生命值滑动条
    /// </summary>
    void InitializeHealthSlider()
    {
        if (playerController && healthSlider)
        {
            // 设置滑动条的最大值为玩家的最大生命值
            healthSlider.maxValue = playerController.maxHealth;
            healthSlider.minValue = 0f;

            // 设置滑动条的初始值为玩家当前生命值
            healthSlider.value = playerController.currentHealth;

            Debug.Log($"初始化生命值滑动条: Max={healthSlider.maxValue}, Current={playerController.currentHealth}");
        }
    }

    /// <summary>
    /// 初始化统计面板（获取/添加CanvasGroup，设置初始状态）
    /// </summary>
    private void InitializeStatsPanel()
    {
        if (statsPanel)
        {
            // 获取或添加CanvasGroup组件
            statsPanelCanvasGroup = statsPanel.GetComponent<CanvasGroup>();
            if (statsPanelCanvasGroup == null)
            {
                statsPanelCanvasGroup = statsPanel.AddComponent<CanvasGroup>();
            }

            // 初始隐藏面板
            statsPanelVisible = false;
            statsPanelCanvasGroup.alpha = 0f;
            statsPanelCanvasGroup.interactable = false;
            statsPanelCanvasGroup.blocksRaycasts = false;

            Debug.Log("统计面板初始化完成");
        }
        else
        {
            Debug.LogWarning("统计面板未分配，请在Inspector中分配Stats Panel");
        }
    }

    /// <summary>
    /// 初始化统计数据（仅初始化4个需求项+新增累计层数）
    /// </summary>
    private void InitStatsData()
    {
        playerTotalTakenDamage = 0;          // 玩家累计受伤害置零
        maxDealDamage = 0;                   // 玩家最大造伤置零
        maxHealing = 0;                      // 最大治疗置零
        totalEclipseLayerAccumulated = 0;    // 星蚀累计层数置零
        Debug.Log("已初始化统计数据（仅保留4个核心项）");
    }
    #endregion

    #region 核心UI更新方法
    /// <summary>
    /// 更新玩家生命值UI（文本+滑动条）
    /// </summary>
    void UpdateHealthUI()
    {
        if (playerController && healthText && healthSlider)
        {
            // 更新生命值文本
            healthText.text = $"{playerController.currentHealth:F0}/{playerController.maxHealth:F0}";

            // 确保滑动条的最大值与玩家最大生命值同步（以防最大生命值动态变化）
            if (healthSlider.maxValue != playerController.maxHealth)
            {
                healthSlider.maxValue = playerController.maxHealth;
            }

            // 更新滑动条值
            healthSlider.value = playerController.currentHealth;
        }
        else
        {
            // 如果引用丢失，尝试重新获取
            AutoGetReference();
        }
    }

    /// <summary>
    /// 更新星蚀UI（层数文本+进度条+颜色）
    /// </summary>
    void UpdateEclipseUI()
    {
        if (eclipseSystem && eclipseText && eclipseBar)
        {
            // 更新星蚀层数文本（实时剩余层数，不变）
            eclipseText.text = $"*{eclipseSystem.CurrentLayer}";

            // 更新层数条填充量（根据最大层数30计算）
            float fillAmount = (float)eclipseSystem.CurrentLayer / 30f;
            eclipseBar.fillAmount = fillAmount;

            // 根据层数改变星蚀条颜色
            if (eclipseSystem.CurrentLayer >= 20)
                eclipseBar.color = Color.red;
            else if (eclipseSystem.CurrentLayer >= 6)
                eclipseBar.color = Color.yellow;
            else
                eclipseBar.color = Color.white; // 基础色
        }
    }

    /// <summary>
    /// 更新释放提示UI的显示/隐藏
    /// </summary>
    void UpdateReleaseHint()
    {
        if (releaseSystem && releaseHintObject)
        {
            // 有星蚀层数时显示提示，无层数时隐藏
            bool shouldShow = eclipseSystem && eclipseSystem.CurrentLayer > 0;
            releaseHintObject.SetActive(shouldShow);

            if (releaseHintText && shouldShow)
            {
                releaseHintText.text = $"按 {releaseSystem.releaseKey} 键释放能量";
            }
        }
    }
    #endregion

    #region 统计面板核心逻辑
    /// <summary>
    /// 切换统计面板的显示/隐藏状态
    /// </summary>
    public void ToggleStatsPanel()
    {
        if (!statsPanel) return;

        statsPanelVisible = !statsPanelVisible;

        if (statsPanelVisible)
        {
            ShowStatsPanel();
        }
        else
        {
            HideStatsPanel();
        }
    }

    /// <summary>
    /// 显示统计面板（淡入+启用交互）
    /// </summary>
    public void ShowStatsPanel()
    {
        if (!statsPanel || statsPanelCanvasGroup == null) return;

        // 启用面板交互和射线检测
        statsPanelCanvasGroup.interactable = true;
        statsPanelCanvasGroup.blocksRaycasts = true;

        // 淡入效果
        StartCoroutine(FadePanel(0f, 1f, panelFadeTime));
    }

    /// <summary>
    /// 隐藏统计面板（淡出+禁用交互）
    /// </summary>
    public void HideStatsPanel()
    {
        if (!statsPanel || statsPanelCanvasGroup == null) return;

        // 禁用面板交互和射线检测
        statsPanelCanvasGroup.interactable = false;
        statsPanelCanvasGroup.blocksRaycasts = false;

        // 淡出效果
        StartCoroutine(FadePanel(1f, 0f, panelFadeTime));
    }

    /// <summary>
    /// 面板淡入淡出协程
    /// </summary>
    /// <param name="startAlpha">起始透明度</param>
    /// <param name="targetAlpha">目标透明度</param>
    /// <param name="duration">过渡时间</param>
    private IEnumerator FadePanel(float startAlpha, float targetAlpha, float duration) // 修复：非泛型IEnumerator
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            statsPanelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            yield return null;
        }

        // 确保最终透明度为目标值
        statsPanelCanvasGroup.alpha = targetAlpha;
    }

    /// <summary>
    /// 更新统计面板的文本显示（仅显示4个需求项，无冗余）
    /// </summary>
    private void UpdateStatsPanel()
    {
        if (!statsPanel) return;

        // 仅更新4个需求统计项，保留原UI样式
        if (playerTakenTotalDamageText)
            playerTakenTotalDamageText.text = $"{playerTotalTakenDamage:F0}";//玩家累计受到伤害数
        if (playerDealMaxDamageText)
            playerDealMaxDamageText.text = $"{maxDealDamage:F0}";//玩家对敌人造成最大伤害数
        if (playerHealMaxText)
            playerHealMaxText.text = $"{maxHealing:F0}";//最大治疗数
        if (playerEclipseTotalLayerText)
            playerEclipseTotalLayerText.text = $"{totalEclipseLayerAccumulated}";//星蚀累计层数（新增独立值）
    }

    /// <summary>
    /// 重置所有统计数据（外部可调用，如战斗结束后）
    /// </summary>
    public void ResetAllStats()
    {
        InitStatsData();
        Debug.Log("已重置所有核心统计数据（4个需求项）");
    }
    #endregion

    #region 伤害/治疗数字显示+统计
    /// <summary>
    /// 通用伤害数字显示方法（Boss受伤/外部调用，修复：改为public）
    /// </summary>
    /// <param name="worldPosition">世界坐标</param>
    /// <param name="damage">伤害值</param>
    /// <param name="color">数字颜色</param>
    public void ShowDamageText(Vector3 worldPosition, float damage, Color color)
    {
        if (!CanShowDamageText(worldPosition, damage, "boss_damage")) return;
        SpawnDamageText(worldPosition, $"-{damage:F0}", color);
    }

    /// <summary>
    /// 玩家造成伤害时调用 - 显示伤害数字+统计【最大造伤】
    /// </summary>
    /// <param name="worldPos">伤害数字显示世界坐标</param>
    /// <param name="damage">伤害值</param>
    /// <param name="color">数字颜色</param>
    public void ShowPlayerDealDamageText(Vector3 worldPos, float damage, Color color)
    {
        if (!CanShowDamageText(worldPos, damage, "player_deal")) return;
        // 显示伤害数字
        SpawnDamageText(worldPos, $"-{damage:F0}", color);
        // 仅统计最大造伤（删除总造伤统计）
        RecordPlayerDealDamage(damage);
    }

    /// <summary>
    /// 玩家受到伤害时调用 - 显示伤害数字+统计【累计受伤害】
    /// </summary>
    /// <param name="worldPos">伤害数字显示世界坐标</param>
    /// <param name="damage">伤害值</param>
    /// <param name="color">数字颜色</param>
    public void ShowPlayerTakenDamageText(Vector3 worldPos, float damage, Color color)
    {
        if (!CanShowDamageText(worldPos, damage, "player_taken")) return;
        // 显示伤害数字
        SpawnDamageText(worldPos, $"-{damage:F0}", color);
        // 统计累计受伤害
        RecordPlayerTakenDamage(damage);
    }

    /// <summary>
    /// 兼容原有调用：ShowHealText（PlayerController仍在调用此方法，修复CS1061）
    /// </summary>
    /// <param name="worldPosition">世界坐标</param>
    /// <param name="amount">治疗值</param>
    /// <param name="color">数字颜色</param>
    public void ShowHealText(Vector3 worldPosition, float amount, Color color)
    {
        ShowPlayerHealText(worldPosition, amount, color);
    }

    /// <summary>
    /// 玩家获得治疗时调用 - 显示治疗数字+统计【最大治疗】
    /// </summary>
    /// <param name="worldPos">治疗数字显示世界坐标</param>
    /// <param name="heal">治疗值</param>
    /// <param name="color">数字颜色</param>
    public void ShowPlayerHealText(Vector3 worldPos, float heal, Color color)
    {
        if (!CanShowDamageText(worldPos, heal, "player_heal")) return;
        // 显示治疗数字
        SpawnDamageText(worldPos, $"+{heal:F0}", color);
        // 仅统计最大治疗（删除总治疗统计）
        RecordPlayerHealing(heal);
    }

    /// <summary>
    /// 防抖检测 - 防止短时间内同一位置重复显示相同数值的伤害/治疗
    /// </summary>
    /// <param name="pos">世界坐标</param>
    /// <param name="value">伤害/治疗值</param>
    /// <param name="type">类型标识</param>
    /// <returns>是否可以显示</returns>
    private bool CanShowDamageText(Vector3 pos, float value, string type)
    {
        if (!damageTextPrefab || !gameCanvas) return false;

        // 生成唯一标识符
        string key = $"{type}_{pos.x:F0}_{pos.y:F0}_{pos.z:F0}_{value:F0}";

        // 检查最近是否显示过相同的伤害/治疗
        if (lastDamageTime.ContainsKey(key))
        {
            float timeSinceLast = Time.time - lastDamageTime[key];
            if (timeSinceLast < damageCooldown)
            {
                return false;
            }
        }

        // 更新最后显示时间
        lastDamageTime[key] = Time.time;

        // 清理过期的记录（避免内存泄漏）
        if (lastDamageTime.Count > 100)
        {
            List<string> keysToRemove = new List<string>();
            foreach (var entry in lastDamageTime)
            {
                if (Time.time - entry.Value > 1.0f) // 清理1秒前的记录
                {
                    keysToRemove.Add(entry.Key);
                }
            }

            foreach (var keyToRemove in keysToRemove)
            {
                lastDamageTime.Remove(keyToRemove);
            }
        }

        return true;
    }

    /// <summary>
    /// 生成伤害/治疗数字（对象池复用）
    /// </summary>
    /// <param name="worldPos">世界坐标</param>
    /// <param name="text">显示的文本（-伤害/+治疗）</param>
    /// <param name="color">文本颜色</param>
    private void SpawnDamageText(Vector3 worldPos, string text, Color color)
    {
        CleanupActiveTexts();

        // 世界坐标转屏幕坐标
        Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPos);

        // 增加随机偏移防止重叠
        float randomX = Random.Range(-horizontalOffset, horizontalOffset);
        float randomY = Random.Range(-verticalOffset, verticalOffset);

        // 使用Z轴分层防止重叠
        zOffsetCounter -= 0.1f;
        if (zOffsetCounter < -10f) zOffsetCounter = 0f;

        screenPosition += new Vector3(randomX, randomY, zOffsetCounter);

        // 从对象池获取伤害文本对象
        GameObject textObj = GetDamageTextFromPool();
        if (textObj == null) return;

        // 设置位置和显示状态
        textObj.transform.position = screenPosition;
        textObj.SetActive(true);

        // 设置文本内容和颜色
        Text txt = textObj.GetComponent<Text>();
        if (txt)
        {
            txt.text = text;
            txt.color = color;
        }

        // 添加到活动队列
        activeDamageTexts.Enqueue(textObj);

        // 启动动画效果
        StartCoroutine(BounceEffect(textObj));
        StartCoroutine(FloatEffect(textObj));
    }

    /// <summary>
    /// 从对象池获取伤害文本对象，池空则动态创建
    /// </summary>
    /// <returns>伤害文本对象</returns>
    private GameObject GetDamageTextFromPool()
    {
        if (damageTextPool.Count > 0)
        {
            return damageTextPool.Dequeue();
        }
        else
        {
            // 如果池子为空，创建新的对象（动态扩容）
            GameObject newObj = Instantiate(damageTextPrefab, gameCanvas.transform);
            Debug.LogWarning("伤害数字对象池为空，创建新的对象，建议增加对象池大小");
            return newObj;
        }
    }

    /// <summary>
    /// 统计玩家造成的伤害（仅更新【最大单次造伤】，删除冗余）
    /// </summary>
    /// <param name="damage">伤害值</param>
    private void RecordPlayerDealDamage(float damage)
    {
        // 仅更新最大单次伤害，删除总造伤和窗口统计
        if (damage > maxDealDamage)
        {
            maxDealDamage = damage;
            Debug.Log($"更新玩家最大造伤：{maxDealDamage:F0}");
        }
    }

    /// <summary>
    /// 统计玩家受到的伤害（仅更新【累计受伤害】，保留原有逻辑）
    /// </summary>
    /// <param name="damage">伤害值</param>
    private void RecordPlayerTakenDamage(float damage)
    {
        playerTotalTakenDamage += damage;
        Debug.Log($"玩家累计受伤害：{playerTotalTakenDamage:F0}（本次+{damage:F0}）");
    }

    /// <summary>
    /// 统计玩家获得的治疗（仅更新【最大单次治疗】，删除冗余）
    /// </summary>
    /// <param name="heal">治疗值</param>
    private void RecordPlayerHealing(float heal)
    {
        // 仅更新最大单次治疗，删除总治疗和窗口统计
        if (heal > maxHealing)
        {
            maxHealing = heal;
            Debug.Log($"更新最大治疗数：{maxHealing:F0}");
        }
    }

    /// <summary>
    /// 新增：外部调用（EclipseSystem）- 累加星蚀累计层数（释放技能不消耗此值）
    /// </summary>
    /// <param name="addLayer">本次增加的层数</param>
    public void AddEclipseLayerAccumulated(int addLayer)
    {
        if (addLayer <= 0) return;
        totalEclipseLayerAccumulated += addLayer;
        Debug.Log($"星蚀累计层数更新：{totalEclipseLayerAccumulated}（本次+{addLayer}）");
    }
    #endregion

    #region 伤害数字动画+对象池回收
    /// <summary>
    /// 清理过期的活动文本，回收至对象池
    /// </summary>
    void CleanupActiveTexts()
    {
        if (activeDamageTexts.Count == 0) return;

        int count = activeDamageTexts.Count;
        int recycledCount = 0;

        for (int i = 0; i < count; i++)
        {
            GameObject textObj = activeDamageTexts.Dequeue();
            if (textObj != null && textObj.activeSelf)
            {
                activeDamageTexts.Enqueue(textObj);
            }
            else if (textObj != null)
            {
                ReturnDamageTextToPool(textObj);
                recycledCount++;
            }
        }
    }

    /// <summary>
    /// 将伤害文本对象回收至对象池
    /// </summary>
    /// <param name="textObj">伤害文本对象</param>
    private void ReturnDamageTextToPool(GameObject textObj)
    {
        if (textObj == null) return;

        textObj.SetActive(false);
        textObj.transform.SetParent(gameCanvas.transform, false);

        // 重置文本属性
        Text textComponent = textObj.GetComponent<Text>();
        if (textComponent)
        {
            Color color = textComponent.color;
            color.a = 1f;
            textComponent.color = color;
            textObj.transform.localScale = Vector3.one;
        }

        damageTextPool.Enqueue(textObj);
    }

    /// <summary>
    /// 伤害数字跳动效果（修复：非泛型IEnumerator）
    /// </summary>
    /// <param name="textObj">文本对象</param>
    private IEnumerator BounceEffect(GameObject textObj)
    {
        float duration = 0.4f;
        float elapsed = 0f;
        Vector3 originalScale = Vector3.one;
        Vector3 targetScale = originalScale * 1.3f; // 放大到130%

        // 放大阶段
        while (elapsed < duration / 2)
        {
            if (textObj == null || !textObj.activeSelf) yield break;

            elapsed += Time.deltaTime;
            float progress = elapsed / (duration / 2);
            textObj.transform.localScale = Vector3.Lerp(originalScale, targetScale, progress);
            yield return null;
        }

        // 缩小阶段
        elapsed = 0f;
        while (elapsed < duration / 2)
        {
            if (textObj == null || !textObj.activeSelf) yield break;

            elapsed += Time.deltaTime;
            float progress = elapsed / (duration / 2);
            textObj.transform.localScale = Vector3.Lerp(targetScale, originalScale, progress);
            yield return null;
        }

        // 确保回到原始大小
        if (textObj != null)
            textObj.transform.localScale = originalScale;
    }

    /// <summary>
    /// 伤害数字上浮+淡出效果（修复：非泛型IEnumerator）
    /// </summary>
    /// <param name="textObj">文本对象</param>
    private IEnumerator FloatEffect(GameObject textObj)
    {
        float duration = 1.5f;
        float elapsed = 0f;
        Vector3 startPosition = textObj.transform.position;

        while (elapsed < duration)
        {
            if (textObj == null || !textObj.activeSelf) yield break;

            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            // 向上移动
            Vector3 newPosition = startPosition + new Vector3(0, 50f * progress, 0);
            textObj.transform.position = newPosition;

            // 淡出效果
            Text text = textObj.GetComponent<Text>();
            if (text)
            {
                Color color = text.color;
                color.a = 1f - progress;
                text.color = color;
            }

            yield return null;
        }

        // 动画结束后隐藏对象（后续会被回收）
        if (textObj != null && textObj.activeSelf)
        {
            textObj.SetActive(false);
        }
    }
    #endregion

    #region 资源清理
    /// <summary>
    /// 销毁时清理对象池和队列，防止内存泄漏
    /// </summary>
    private void OnDestroy()
    {
        if (damageTextPool != null)
        {
            foreach (var obj in damageTextPool)
            {
                if (obj != null)
                    Destroy(obj);
            }
            damageTextPool.Clear();
        }

        if (activeDamageTexts != null)
        {
            activeDamageTexts.Clear();
        }

        if (lastDamageTime != null)
        {
            lastDamageTime.Clear();
        }
    }
    #endregion
}