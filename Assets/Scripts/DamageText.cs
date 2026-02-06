using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class DamageText : MonoBehaviour
{
    [Header("文本效果")]
    public Font damageFont;
    public int baseFontSize = 24;
    public Color outlineColor = Color.black;
    public float outlineWidth = 2f;
    public FontStyle fontStyle = FontStyle.Bold;

    [Header("随机偏移")]
    public float randomOffset = 10f;

    [Header("震动效果")]
    public float shakeIntensity = 2f;
    public float shakeDuration = 0.5f;

    private Vector3 originalPosition;

    void Start()
    {
        Text text = GetComponent<Text>();
        originalPosition = transform.position;

        // 添加随机起始偏移
        Vector3 pos = transform.position;
        pos.x += Random.Range(-randomOffset, randomOffset);
        transform.position = pos;

        // 设置字体样式
        if (damageFont) text.font = damageFont;
        text.fontSize = baseFontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAnchor.MiddleCenter;

        // 添加阴影效果（模拟描边）
        Shadow shadow = gameObject.AddComponent<Shadow>();
        shadow.effectColor = outlineColor;
        shadow.effectDistance = new Vector2(outlineWidth, -outlineWidth);

        // 添加第二个阴影形成更粗的描边
        Shadow shadow2 = gameObject.AddComponent<Shadow>();
        shadow2.effectColor = outlineColor;
        shadow2.effectDistance = new Vector2(-outlineWidth, outlineWidth);

        // 可选：添加轮廓效果
        Outline outline = gameObject.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(outlineWidth, -outlineWidth);

        // 启动震动效果
        StartCoroutine(ShakeEffect());
    }

    // 震动效果协程
    System.Collections.IEnumerator ShakeEffect()
    {
        float elapsed = 0f;
        Vector3 startPosition = transform.position;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;

            // 使用正弦波创建震动效果
            float shakeX = Mathf.Sin(elapsed * 50f) * shakeIntensity * (1f - elapsed / shakeDuration);
            float shakeY = Mathf.Cos(elapsed * 40f) * shakeIntensity * (1f - elapsed / shakeDuration);

            transform.position = startPosition + new Vector3(shakeX, shakeY, 0);

            yield return null;
        }

        // 震动结束后回到原始位置
        //transform.position = startPosition;
    }

    void OnDestroy()
    {
        // 清理组件
        Shadow[] shadows = GetComponents<Shadow>();
        foreach (Shadow shadow in shadows)
        {
            Destroy(shadow);
        }

        Outline outline = GetComponent<Outline>();
        if (outline) Destroy(outline);
    }
}