using UnityEngine;

public class GroundDetector : MonoBehaviour
{
    [Header("检测参数")]
    public float checkDistance = 0.2f;
    public LayerMask groundLayer = 1;

    [Header("运行状态（只读）")]
    public bool isGrounded = false;

    [Header("可视化设置")]
    public bool showGizmo = true;

    private PlayerController player;

    void Start()
    {
        player = GetComponentInParent<PlayerController>();
    }

    public bool CheckGrounded()
    {
        Vector2 origin = transform.position;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, checkDistance, groundLayer);
        isGrounded = hit.collider != null;
        return isGrounded;
    }

    void OnDrawGizmos()
    {
        if (!showGizmo) return;

        Vector2 origin = transform.position;
        Vector2 end = origin + Vector2.down * checkDistance;

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(origin, end);
        Gizmos.DrawWireSphere(end, 0.05f);
        Gizmos.DrawWireSphere(origin, 0.03f);
    }
}
