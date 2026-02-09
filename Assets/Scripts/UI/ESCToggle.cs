using UnityEngine;

public class ESCToggle : MonoBehaviour
{
    public GameObject targetObject;  // 拖入你要显示/隐藏的GameObject

    void Update()
    {
        if (Input.GetKey(KeyCode.Escape))
        {
            targetObject.SetActive(true);
        }
        else
        {
            targetObject.SetActive(false);
        }
    }
}
