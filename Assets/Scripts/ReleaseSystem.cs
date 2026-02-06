using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ReleaseSystem : MonoBehaviour
{
    [Header("释放设置")]
    public KeyCode releaseKey = KeyCode.Space;
    public float releaseRange = 3f;
    public float baseDamage = 50f;
    public float damagePerLayer = 10f;
    public float healPerLayer = 3f;
    public float healAuto = 1f;

    [Header("释放效果")]
    public GameObject releaseEffectPrefab;
    public GameObject explosionAnimationPrefab;
    public float effectDuration = 1f;

    [Header("屏幕效果设置")]
    public float shakeDuration = 0.3f;
    public float shakeMagnitude = 0.15f;
    public Color flashColor = new Color(0.8f, 0.3f, 1f, 0.8f);
    public float timeSlowFactor = 0.3f;
    public float timeSlowDuration = 0.4f;

    [Header("敌人检测图层")]
    public LayerMask enemyLayerMask;

    private EclipseSystem eclipseSystem;
    private PlayerController playerController;
    private float dotTimer = 0f;
    private float dotInterval = 1f;
    private ScreenEffectManager screenEffectManager;

    void Start()
    {
        eclipseSystem = GetComponent<EclipseSystem>();
        playerController = GetComponent<PlayerController>();

        screenEffectManager = FindObjectOfType<ScreenEffectManager>();
        if (screenEffectManager == null)
        {
            GameObject effectManagerObj = new GameObject("ScreenEffectManager");
            screenEffectManager = effectManagerObj.AddComponent<ScreenEffectManager>();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(releaseKey))
        {
            ReleaseEnergy();
        }

        dotTimer += Time.deltaTime;
        if (dotTimer >= dotInterval && eclipseSystem.CurrentLayer > 0)
        {
            AutoHealth();
            dotTimer = 0f;
        }
    }

    void ReleaseEnergy()
    {
        if (eclipseSystem.CurrentLayer <= 0)
        {
            Debug.Log("没有星蚀能量可释放");
            return;
        }

        int layers = eclipseSystem.CurrentLayer;

        if (layers >= 5) ApplyTimeSlow(layers);
        DamageEnemiesInRange(layers);
        HealPlayer(layers);
        eclipseSystem.ClearAllLayers();
        PlayReleaseEffect(layers);
        ApplyScreenEffects(layers);

        Debug.Log($"能量释放！层数: {layers}");
    }

    void DamageEnemiesInRange(int layers)
    {
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, releaseRange, enemyLayerMask);
        float totalDamage = CalculateDamage(layers);

        Debug.Log($"释放范围伤害: {totalDamage}, 检测到 {hitColliders.Length} 个碰撞体");

        int enemiesDamaged = 0;
        List<string> enemyTypes = new List<string>();

        foreach (Collider2D collider in hitColliders)
        {
            GameObject enemyObj = collider.gameObject;

            // 方法1：检测BaseEnemy（推荐）
            BaseEnemy baseEnemy = enemyObj.GetComponent<BaseEnemy>();
            if (baseEnemy != null)
            {
                baseEnemy.TakeDamage(totalDamage);
                enemiesDamaged++;
                enemyTypes.Add("BaseEnemy");
                ShowDamageNumber(enemyObj.transform.position, totalDamage);
                continue;
            }

            

            // 方法3：检测EnemyShadow（旧脚本，保持兼容）
            EnemyShadow enemyShadow = enemyObj.GetComponent<EnemyShadow>();
            if (enemyShadow != null)
            {
                enemyShadow.TakeDamage(totalDamage);
                enemiesDamaged++;
                enemyTypes.Add("EnemyShadow");
                ShowDamageNumber(enemyObj.transform.position, totalDamage);
                continue;
            }

            // 方法4：检测通用的TakeDamage方法（更通用的兼容）
            MonoBehaviour[] scripts = enemyObj.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour script in scripts)
            {
                System.Type scriptType = script.GetType();
                System.Reflection.MethodInfo takeDamageMethod = scriptType.GetMethod("TakeDamage",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                    null,
                    new System.Type[] { typeof(float) },
                    null);

                if (takeDamageMethod != null)
                {
                    try
                    {
                        takeDamageMethod.Invoke(script, new object[] { totalDamage });
                        enemiesDamaged++;
                        enemyTypes.Add(scriptType.Name);
                        ShowDamageNumber(enemyObj.transform.position, totalDamage);
                        break;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"调用TakeDamage方法失败: {e.Message}");
                    }
                }
            }
        }

        if (enemiesDamaged > 0)
        {
            Debug.Log($"成功伤害 {enemiesDamaged} 个敌人，类型: {string.Join(", ", enemyTypes)}");
        }
        else
        {
            Debug.Log("范围内没有找到可伤害的敌人");
        }
    }

    float CalculateDamage(int layers)
    {
        if (layers >= 6)
        {
            if (layers > 6 && layers <= 10)
            {
                return baseDamage * 2 + (damagePerLayer * layers);
            }
            else if (layers > 10)
            {
                float multiplier = 1f + ((layers - 10) * 0.05f);
                return (baseDamage + (damagePerLayer * layers)) * multiplier;
            }
            else
            {
                return baseDamage + (damagePerLayer * layers);
            }
        }
        else
        {
            return 3 * layers;
        }
    }

    void HealPlayer(int layers)
    {
        float healAmount = playerController.maxHealth * (healPerLayer / 100f) * layers;

        if (layers >= 20) healAmount *= 2f;

        playerController.Heal(healAmount);
        Debug.Log($"释放治疗: {healAmount}");
        ShowHealNumber(transform.position, healAmount);
    }

    void AutoHealth()
    {
        int layers = eclipseSystem.CurrentLayer;
        float healAmount = playerController.maxHealth * (healAuto / 100f);
        playerController.Heal(healAmount);

        Debug.Log($"自动治疗: {healAmount}");
        ShowHealNumber(transform.position, healAmount, new Color(0.5f, 1f, 0.5f));
    }

    void PlayReleaseEffect(int layers)
    {
        if (explosionAnimationPrefab != null)
        {
            PlayExplosionAnimation(layers);
        }

        if (releaseEffectPrefab != null)
        {
            PlayParticleEffect(layers);
        }
    }

    void PlayExplosionAnimation(int layers)
    {
        GameObject explosion = Instantiate(explosionAnimationPrefab, transform.position, Quaternion.identity);

        float scaleMultiplier = 1f + (layers * 0.05f);
        explosion.transform.localScale = Vector3.one * scaleMultiplier;

        Animator animator = explosion.GetComponent<Animator>();
        if (animator != null)
        {
            float speedMultiplier = Mathf.Clamp(1f + (layers * 0.02f), 0.5f, 2f);
            animator.speed = speedMultiplier;
            animator.Play("Explosion", 0, 0f);

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            if (clips.Length > 0)
            {
                Destroy(explosion, clips[0].length + 0.5f);
            }
            else
            {
                Destroy(explosion, effectDuration);
            }
        }
        else
        {
            Destroy(explosion, effectDuration);
        }
    }

    void PlayParticleEffect(int layers)
    {
        GameObject effect = Instantiate(releaseEffectPrefab, transform.position, Quaternion.identity);

        float scaleMultiplier = 1f + (layers * 0.05f);
        effect.transform.localScale = Vector3.one * scaleMultiplier;

        ParticleSystem ps = effect.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            main.startSpeed = 5f + (layers * 0.2f);

            if (layers >= 20)
            {
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.2f, 0.8f, 1f),
                    new Color(0.3f, 0.8f, 1f, 1f)
                );
            }
        }

        Destroy(effect, effectDuration);
    }

    void ApplyScreenEffects(int layers)
    {
        float intensity = Mathf.Clamp01(layers / 30f);

        if (screenEffectManager != null)
        {
            float shakeIntensity = shakeMagnitude * (1f + intensity);
            float actualShakeDuration = shakeDuration * (1f + intensity * 0.5f);
            screenEffectManager.ShakeCamera(actualShakeDuration, shakeIntensity);
        }

        if (screenEffectManager != null)
        {
            Color adjustedFlashColor = flashColor;
            adjustedFlashColor.a *= (0.5f + intensity * 0.5f);
            float flashDuration = 0.2f + (intensity * 0.3f);
            screenEffectManager.FlashScreen(adjustedFlashColor, flashDuration);
        }

        if (layers >= 10 && screenEffectManager != null)
        {
            Color edgeColor = new Color(1f, 0.6f, 0.2f, 0.8f);
            screenEffectManager.EdgeFlash(edgeColor, 0.3f + (intensity * 0.2f));
        }
    }

    void ApplyTimeSlow(int layers)
    {
        if (screenEffectManager != null)
        {
            float slowFactor = Mathf.Lerp(0.5f, timeSlowFactor, Mathf.Clamp01((layers - 5) / 25f));
            float duration = timeSlowDuration * (1f + (layers * 0.02f));
            screenEffectManager.TimeSlow(slowFactor, duration);
        }
    }

    void ShowDamageNumber(Vector3 position, float damage)
    {
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui)
        {
            ui.ShowDamageText(position, Mathf.RoundToInt(damage), Color.red);
        }
    }

    void ShowHealNumber(Vector3 position, float healAmount, Color? color = null)
    {
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui)
        {
            Color healColor = color ?? Color.green;
            ui.ShowHealText(position, Mathf.RoundToInt(healAmount), healColor);
        }
    }

    // 调试方法：显示范围内所有敌人
    [ContextMenu("测试检测范围内敌人")]
    void TestDetectEnemies()
    {
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, releaseRange, enemyLayerMask);
        Debug.Log($"范围内检测到 {hitColliders.Length} 个碰撞体:");

        foreach (Collider2D collider in hitColliders)
        {
            GameObject obj = collider.gameObject;
            Debug.Log($"- {obj.name} (位置: {obj.transform.position})");

            // 检查组件
            if (obj.GetComponent<BaseEnemy>() != null) Debug.Log($"  包含: BaseEnemy");
            //if (obj.GetComponent<EnmShadow>() != null) Debug.Log($"  包含: EnmShadow");
            if (obj.GetComponent<EnemyShadow>() != null) Debug.Log($"  包含: EnemyShadow");
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, releaseRange);
    }

    void OnValidate()
    {
        releaseRange = Mathf.Max(releaseRange, 1f);
        baseDamage = Mathf.Max(baseDamage, 1f);
        damagePerLayer = Mathf.Max(damagePerLayer, 1f);
        healPerLayer = Mathf.Max(healPerLayer, 0.1f);
        healAuto = Mathf.Max(healAuto, 0.1f);
    }
}