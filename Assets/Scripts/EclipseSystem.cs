using UnityEngine;

public class EclipseSystem : MonoBehaviour
{
    [Header("星蚀设置")]
    public int currentLayer = 0;
    public int maxLayer = 30;

    [Header("DOT设置")]
    public float dotInterval = 1f; // 每秒触发一次
    public float dotDamagePerLayer = 0.5f; // 每层0.5%伤害

    private float dotTimer = 0f;
    private PlayerController player;

    void Start()
    {
        // 【修改点1】修复PlayerController获取失败的潜在BUG：改用全局查找，加空值判断
        player = FindObjectOfType<PlayerController>();
        if (player == null)
        {
            Debug.LogError("EclipseSystem未找到PlayerController脚本，请检查场景中是否有该脚本！");
        }
    }

    void Update()
    {
        // DOT计时器
        dotTimer += Time.deltaTime;
        if (dotTimer >= dotInterval && currentLayer > 0 && player != null) // 加player非空判断，防止空指针
        {
            ApplyDOT();
            dotTimer = 0f;
        }
    }

    // 应用DOT伤害
    void ApplyDOT()
    {
        float damage = player.maxHealth * (dotDamagePerLayer / 100f) * currentLayer;
        player.TakeDamage(damage);

        // 显示DOT伤害数字（紫色）
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui)
        {
            // 【修改点2】和PlayerController受伤害保持一致，改用统计玩家受伤害的方法，DOT伤害计入玩家受伤害统计
            ui.ShowPlayerTakenDamageText(transform.position, damage, Color.magenta);
        }
    }

    // 增加层数
    public void AddLayer(int amount)
    {
        currentLayer = Mathf.Min(maxLayer, currentLayer + amount);
        Debug.Log($"增加星蚀层数: +{amount}, 当前: {currentLayer}");

        // 【修改点3】添加UI层数统计：通知UIManager累计星蚀层数，解决层数统计为0的问题
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui != null)
        {
            ui.AddEclipseLayerAccumulated(amount);
        }
    }

    // 减少层数
    public void RemoveLayer(int amount)
    {
        currentLayer = Mathf.Max(0, currentLayer - amount);
        Debug.Log($"减少星蚀层数: -{amount}, 当前: {currentLayer}");
    }

    // 清空层数
    public void ClearAllLayers()
    {
        Debug.Log($"清空星蚀层数: {currentLayer} -> 0");
        currentLayer = 0;
    }

    // 属性访问器
    public int CurrentLayer => currentLayer;
}