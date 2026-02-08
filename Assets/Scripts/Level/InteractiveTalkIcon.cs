using UnityEngine;

public class InteractiveTalkIcon : MonoBehaviour
{
    [Header("调试模式")]
    public bool debugMode = true;

    [Header("检测设置")]
    public float detectionRadius = 3f;
    public LayerMask playerLayer = 1 << 0;  // 默认检测Default层
    public string playerTag = "Player";
    public bool useSimpleDistanceCheck = true; // 使用简单距离检测，更可靠

    [Header("UI显示对象")]
    public GameObject talkIcon;
    public GameObject targetObject;

    [Header("按键设置")]
    public KeyCode[] triggerKeys = { KeyCode.Q, KeyCode.E, KeyCode.Return, KeyCode.Space };

    [Header("显示设置")]
    public bool hideTalkIconAfterTrigger = true;
    public bool oneTimeTrigger = false;

    private bool isPlayerInRange = false;
    private bool hasTriggered = false;
    private Transform playerTransform;
    private GameObject playerObject;

    void Start()
    {
        // 尝试查找玩家对象
        playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
            if (debugMode) Debug.Log("找到玩家对象: " + playerObject.name);
        }
        else
        {
            Debug.LogWarning("未找到玩家对象，标签: " + playerTag);
        }

        // 初始化隐藏UI
        if (talkIcon != null)
        {
            talkIcon.SetActive(false);
            if (debugMode) Debug.Log("TalkIcon已隐藏");
        }
        else
        {
            Debug.LogError("TalkIcon未赋值！请拖入UI对象");
        }

        if (targetObject != null)
            targetObject.SetActive(false);
    }

    void Update()
    {
        if (oneTimeTrigger && hasTriggered)
            return;

        // 如果玩家对象为空，尝试重新查找
        if (playerObject == null && Time.frameCount % 30 == 0) // 每30帧尝试一次
        {
            playerObject = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObject != null) playerTransform = playerObject.transform;
        }

        // 检测玩家
        CheckPlayerInRange();

        // 更新UI
        UpdateTalkIcon();

        // 检测按键
        if (isPlayerInRange && !hasTriggered && playerTransform != null)
        {
            CheckInput();
        }
    }

    void CheckPlayerInRange()
    {
        bool previousState = isPlayerInRange;

        if (playerTransform == null)
        {
            isPlayerInRange = false;
            return;
        }

        if (useSimpleDistanceCheck)
        {
            // 方法1：简单距离检测（最可靠）
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            isPlayerInRange = distance <= detectionRadius;

            if (debugMode && isPlayerInRange != previousState)
                Debug.Log($"距离检测: {distance}m, 在范围内: {isPlayerInRange}");
        }
        else
        {
            // 方法2：物理检测（兼容旧代码）
            Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, playerLayer);
            isPlayerInRange = false;

            if (debugMode && colliders.Length > 0)
                Debug.Log($"物理检测到 {colliders.Length} 个碰撞器");

            foreach (Collider collider in colliders)
            {
                if (collider.CompareTag(playerTag))
                {
                    isPlayerInRange = true;
                    if (debugMode) Debug.Log("检测到玩家碰撞器: " + collider.name);
                    break;
                }
            }
        }

        // 状态变化处理
        if (previousState != isPlayerInRange)
        {
            if (isPlayerInRange)
                OnPlayerEnter();
            else
                OnPlayerExit();
        }
    }

    void UpdateTalkIcon()
    {
        if (talkIcon == null) return;

        // 只在玩家在范围内且未触发时显示
        bool shouldShow = isPlayerInRange && !hasTriggered && playerTransform != null;

        if (talkIcon.activeSelf != shouldShow)
        {
            talkIcon.SetActive(shouldShow);
            if (debugMode) Debug.Log($"设置TalkIcon显示: {shouldShow}");
        }

        // 让图标面向玩家（如果显示）
        if (shouldShow && playerTransform != null)
        {
            Vector3 directionToPlayer = playerTransform.position - transform.position;
            directionToPlayer.y = 0; // 只水平旋转

            if (directionToPlayer != Vector3.zero)
            {
                talkIcon.transform.rotation = Quaternion.LookRotation(directionToPlayer);
            }
        }
    }

    void CheckInput()
    {
        foreach (KeyCode key in triggerKeys)
        {
            if (Input.GetKeyDown(key))
            {
                if (debugMode) Debug.Log("按下按键: " + key);
                OnTriggerActivated();
                break;
            }
        }
    }

    void OnPlayerEnter()
    {
        if (debugMode) Debug.Log("玩家进入范围");
        // 可以添加进入音效
    }

    void OnPlayerExit()
    {
        if (debugMode) Debug.Log("玩家离开范围");
        // 可以添加离开音效
    }

    void OnTriggerActivated()
    {
        if (debugMode) Debug.Log("触发交互");

        // 标记触发状态
        if (oneTimeTrigger)
            hasTriggered = true;

        // 显示目标对象
        if (targetObject != null)
        {
            targetObject.SetActive(true);
            if (debugMode) Debug.Log("显示目标对象");
        }

        // 隐藏对话图标
        if (hideTalkIconAfterTrigger && talkIcon != null)
        {
            talkIcon.SetActive(false);
        }

        // 可以添加音效或动画
    }

    // 重置状态（可以从其他脚本调用）
    public void ResetTrigger()
    {
        hasTriggered = false;

        if (targetObject != null)
        {
            targetObject.SetActive(false);
            if (debugMode) Debug.Log("重置触发状态");
        }
    }

    // 强制显示/隐藏目标对象
    public void SetTargetObjectActive(bool active)
    {
        if (targetObject != null)
        {
            targetObject.SetActive(active);
            if (debugMode) Debug.Log($"强制设置目标对象: {active}");
        }
    }

    // 设置玩家引用（可以从外部设置）
    public void SetPlayer(GameObject player)
    {
        playerObject = player;
        playerTransform = player != null ? player.transform : null;
        if (debugMode && player != null) Debug.Log("设置玩家引用: " + player.name);
    }

    // 手动触发
    [ContextMenu("手动触发")]
    public void ManualTrigger()
    {
        OnTriggerActivated();
    }

    // 手动重置
    [ContextMenu("重置状态")]
    public void ManualReset()
    {
        ResetTrigger();
        if (debugMode) Debug.Log("手动重置状态");
    }

    // 在编辑器中显示检测范围
    void OnDrawGizmosSelected()
    {
        Gizmos.color = isPlayerInRange ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // 显示检测线（如果玩家存在）
        if (Application.isPlaying && playerTransform != null)
        {
            Gizmos.color = isPlayerInRange ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, playerTransform.position);
        }
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}