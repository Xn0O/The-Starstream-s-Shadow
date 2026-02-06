using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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

    // Z轴偏移计数器
    private float zOffsetCounter = 0f;

    // 防止重复显示的防抖机制
    private Dictionary<string, float> lastDamageTime = new Dictionary<string, float>();
    private float damageCooldown = 0.1f; // 100毫秒内不重复显示同一伤害


    [Header("Boss UI")]
    public GameObject bossHealthBar;          // Boss血条面板
    public Text bossNameText;                 // Boss名称文本
    public Slider bossHealthSlider;           // Boss血条滑动条
    public Image bossHealthFill;              // Boss血条填充图片
    public Text bossHealthText;               // Boss血量文本

    // 显示Boss血条
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

    // 更新Boss血量
    public void UpdateBossHealth(float currentHealth, float maxHealth)
    {
        if (!bossHealthBar || !bossHealthBar.activeSelf) return;

        if (bossHealthSlider)
        {
            bossHealthSlider.value = currentHealth;
        }

        UpdateBossHealthText(currentHealth, maxHealth);
    }

    // 更新血量文本
    void UpdateBossHealthText(float currentHealth, float maxHealth)
    {
        if (bossHealthText)
        {
            bossHealthText.text = $"{Mathf.RoundToInt(currentHealth)}/{Mathf.RoundToInt(maxHealth)}";
        }
    }

    // 隐藏Boss血条
    public void HideBossHealthBar()
    {
        if (bossHealthBar) bossHealthBar.SetActive(false);
    }

    // Boss受到伤害时调用
    public void ShowBossDamageText(Vector3 position, float damage, Color color)
    {
        ShowDamageText(position, damage, color);
    }


    void Start()
    {
        // 初始化对象池
        InitializeObjectPools();

        // 如果没有自动获取引用，则手动查找
        if (!playerController)
            playerController = FindObjectOfType<PlayerController>();
        if (!eclipseSystem)
            eclipseSystem = FindObjectOfType<EclipseSystem>();
        if (!releaseSystem)
            releaseSystem = FindObjectOfType<ReleaseSystem>();
        if (!gameCanvas)
            gameCanvas = FindObjectOfType<Canvas>();

        // 初始化UI
        InitializeHealthSlider();
        UpdateHealthUI();
        UpdateEclipseUI();
    }

    // 初始化对象池
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

    void Update()
    {
        // 实时更新UI
        UpdateHealthUI();
        UpdateEclipseUI();
        UpdateReleaseHint();

        // 清理过期的文本
        CleanupActiveTexts();
    }

    // 初始化生命值滑动条
    void InitializeHealthSlider()
    {
        if (playerController && healthSlider)
        {
            // 设置滑动条的最大值为玩家的最大生命值
            healthSlider.maxValue = playerController.maxHealth;
            healthSlider.minValue = 0f;

            // 如果需要，可以在这里设置滑动条的初始值
            healthSlider.value = playerController.currentHealth;

            Debug.Log($"初始化生命值滑动条: Max={healthSlider.maxValue}, Current={playerController.currentHealth}");
        }
    }

    void UpdateHealthUI()
    {
        if (playerController && healthText && healthSlider)
        {
            // 更新文本
            healthText.text = $"{playerController.currentHealth:F0}/{playerController.maxHealth:F0}";

            // 确保滑动条的最大值与玩家最大生命值同步（以防最大生命值变化）
            if (healthSlider.maxValue != playerController.maxHealth)
            {
                healthSlider.maxValue = playerController.maxHealth;
            }

            // 更新滑动条值
            healthSlider.value = playerController.currentHealth;

            // 调试信息
            if (Input.GetKeyDown(KeyCode.F3)) // 按F3键查看调试信息
            {
                Debug.Log($"生命值更新: {playerController.currentHealth}/{playerController.maxHealth}, Slider Value: {healthSlider.value}");
            }
        }
        else
        {
            // 如果引用丢失，尝试重新获取
            if (!playerController)
                playerController = FindObjectOfType<PlayerController>();
            if (!healthSlider)
            {
                // 尝试查找Slider组件
                healthSlider = GetComponentInChildren<Slider>(true);
                if (healthSlider)
                {
                    Debug.Log("重新获取到Slider组件");
                }
            }
        }
    }

    void UpdateEclipseUI()
    {
        if (eclipseSystem && eclipseText && eclipseBar)
        {
            eclipseText.text = $"*{eclipseSystem.CurrentLayer}";

            // 更新层数条（根据最大层数30计算）
            float fillAmount = (float)eclipseSystem.CurrentLayer / 30f;
            eclipseBar.fillAmount = fillAmount;

            // 根据层数改变颜色
            if (eclipseSystem.CurrentLayer >= 20)
                eclipseBar.color = Color.red;
            else if (eclipseSystem.CurrentLayer >= 6)
                eclipseBar.color = Color.yellow;
            else
                eclipseBar.color = Color.white; // 紫色
        }
    }

    void UpdateReleaseHint()
    {
        if (releaseSystem && releaseHintObject)
        {
            // 有层数时显示提示，无层数时隐藏
            bool shouldShow = eclipseSystem && eclipseSystem.CurrentLayer > 0;
            releaseHintObject.SetActive(shouldShow);

            if (releaseHintText && shouldShow)
            {
                releaseHintText.text = $"按 {releaseSystem.releaseKey} 键释放能量";
            }
        }
    }

    // 从对象池获取伤害文本对象
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
            Debug.LogWarning("对象池为空，创建新的伤害文本对象，建议增加对象池大小");
            return newObj;
        }
    }

    // 回收伤害文本对象到对象池
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

    // 清理过期的活动文本
    private void CleanupActiveTexts()
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

        if (recycledCount > 0)
        {
            Debug.Log($"清理了 {recycledCount} 个过期文本，活动文本数: {activeDamageTexts.Count}");
        }
    }

    // 检查是否重复显示
    private bool IsDuplicateDamage(Vector3 position, float value, string type)
    {
        // 生成唯一标识符
        string key = $"{type}_{position.x:F0}_{position.y:F0}_{position.z:F0}_{value:F0}";

        // 检查最近是否显示过相同的伤害
        if (lastDamageTime.ContainsKey(key))
        {
            float timeSinceLast = Time.time - lastDamageTime[key];
            if (timeSinceLast < damageCooldown)
            {
                Debug.Log($"检测到重复伤害显示，已阻止: {key} ({timeSinceLast:F2}秒前)");
                return true;
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
                if (Time.time - entry.Value > 1.0f) // 1秒前的记录清理
                {
                    keysToRemove.Add(entry.Key);
                }
            }

            foreach (var keyToRemove in keysToRemove)
            {
                lastDamageTime.Remove(keyToRemove);
            }
        }

        return false;
    }

    // 显示伤害数字（可被其他脚本调用）
    public void ShowDamageText(Vector3 worldPosition, float damage, Color color)
    {
        if (!damageTextPrefab || !gameCanvas) return;

        // 检查是否重复显示
        if (IsDuplicateDamage(worldPosition, damage, "damage"))
        {
            return;
        }

        // 清理过期的文本
        CleanupActiveTexts();

        // 世界坐标转屏幕坐标
        Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);

        // 增加更明显的随机偏移防止重叠
        float randomX = Random.Range(-horizontalOffset, horizontalOffset);
        float randomY = Random.Range(-verticalOffset, verticalOffset);

        // 使用Z轴分层防止重叠
        zOffsetCounter -= 0.1f;
        if (zOffsetCounter < -10f) zOffsetCounter = 0f;

        screenPosition += new Vector3(randomX, randomY, zOffsetCounter);

        // 调试信息
        if (Input.GetKeyDown(KeyCode.F2))
        {
            Debug.Log($"生成伤害数字: 位置={screenPosition}, 伤害={damage}, 偏移=({randomX}, {randomY}), Z轴={zOffsetCounter}");
        }

        // 从对象池获取伤害文本
        GameObject damageTextObj = GetDamageTextFromPool();
        if (damageTextObj == null) return;

        // 确保位置设置正确
        damageTextObj.transform.position = screenPosition;

        damageTextObj.SetActive(true);

        // 设置文本内容
        Text damageText = damageTextObj.GetComponent<Text>();
        if (damageText)
        {
            damageText.text = $"-{damage:F0}";
            damageText.color = color;
        }

        // 添加到活动队列
        activeDamageTexts.Enqueue(damageTextObj);

        // 添加跳动效果
        StartCoroutine(BounceEffect(damageTextObj));
        // 添加浮动画效果
        StartCoroutine(FloatDamageText(damageTextObj));
    }

    // 显示治疗数字
    public void ShowHealText(Vector3 worldPosition, float amount, Color color)
    {
        if (!damageTextPrefab || !gameCanvas) return;

        // 检查是否重复显示
        if (IsDuplicateDamage(worldPosition, amount, "heal"))
        {
            return;
        }

        // 清理过期的文本
        CleanupActiveTexts();

        // 世界坐标转屏幕坐标
        Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);

        // 增加更明显的随机偏移防止重叠
        float randomX = Random.Range(-horizontalOffset, horizontalOffset);
        float randomY = Random.Range(-verticalOffset, verticalOffset);

        // 使用Z轴分层防止重叠
        zOffsetCounter -= 0.1f;
        if (zOffsetCounter < -10f) zOffsetCounter = 0f;

        screenPosition += new Vector3(randomX, randomY, zOffsetCounter);

        // 调试信息
        if (Input.GetKeyDown(KeyCode.F2))
        {
            Debug.Log($"生成治疗数字: 位置={screenPosition}, 治疗量={amount}, 偏移=({randomX}, {randomY}), Z轴={zOffsetCounter}");
        }

        // 从对象池获取伤害文本
        GameObject healTextObj = GetDamageTextFromPool();
        if (healTextObj == null) return;

        // 确保位置设置正确
        healTextObj.transform.position = screenPosition;

        healTextObj.SetActive(true);

        Text healText = healTextObj.GetComponent<Text>();
        if (healText)
        {
            healText.text = $"+{amount:F0}";
            healText.color = color;
        }

        // 添加到活动队列
        activeDamageTexts.Enqueue(healTextObj);

        // 添加跳动效果
        StartCoroutine(BounceEffect(healTextObj));
        // 添加浮动画效果
        StartCoroutine(FloatDamageText(healTextObj));
    }

    // 跳动效果协程
    System.Collections.IEnumerator BounceEffect(GameObject textObj)
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

    // 浮动画效果协程
    System.Collections.IEnumerator FloatDamageText(GameObject textObj)
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

        // 动画结束后回收对象
        if (textObj != null && textObj.activeSelf)
        {
            textObj.SetActive(false);
        }
    }

    // 清理对象池（在游戏结束时调用）
    void OnDestroy()
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

        lastDamageTime.Clear();
    }

    // 调试方法：手动测试伤害数字
    public void TestDamageText()
    {
        Vector3 testPosition = new Vector3(Screen.width / 2, Screen.height / 2, 0);
        ShowDamageText(Camera.main.ScreenToWorldPoint(testPosition), 100, Color.red);
        ShowHealText(Camera.main.ScreenToWorldPoint(testPosition), 50, Color.green);
    }

    // 在Unity编辑器中添加测试按钮
    [ContextMenu("测试伤害数字")]
    private void TestDamageNumbers()
    {
        TestDamageText();
    }

    // 清理重复显示记录的调试方法
    [ContextMenu("清理重复显示记录")]
    private void ClearDuplicateRecords()
    {
        lastDamageTime.Clear();
        Debug.Log("已清理重复显示记录");
    }
}