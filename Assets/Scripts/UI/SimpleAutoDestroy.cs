using UnityEngine;

public class SimpleAutoDestroy : MonoBehaviour
{
    
    // ªÚ π”√Invoke
    void Start()
    {
        Invoke("DestroySelf", 1f);
    }

    void DestroySelf()
    {
        Destroy(gameObject);
    }
}