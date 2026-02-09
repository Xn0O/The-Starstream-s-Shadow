using UnityEngine;

public class ShowOnPlayerApproach : MonoBehaviour
{
    [Header("显示设置")]
    [SerializeField] private GameObject objectToShow; // 要显示的对象
    [SerializeField] private float detectionDistance = 5f; // 检测距离

    [Header("玩家设置")]
    [SerializeField] private string playerTag = "Player"; // 玩家标签
    [SerializeField] private Transform playerTransform; // 玩家Transform（可选）

    private void Start()
    {
        // 如果未指定对象，默认使用挂载脚本的GameObject
        if (objectToShow == null)
            objectToShow = gameObject;

        // 初始化时隐藏对象
        objectToShow.SetActive(false);

        // 自动查找玩家（如果未指定）
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
                playerTransform = player.transform;
        }
    }

    private void Update()
    {
        if (playerTransform == null)
            return;

        // 计算与玩家的距离
        float distance = Vector3.Distance(transform.position, playerTransform.position);

        // 根据距离显示/隐藏对象
        if (distance <= detectionDistance)
        {
            if (!objectToShow.activeSelf)
                objectToShow.SetActive(true);
        }
        else
        {
            if (objectToShow.activeSelf)
                objectToShow.SetActive(false);
        }
    }

    // 在Scene视图中显示检测范围（调试用）
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionDistance);
    }
}