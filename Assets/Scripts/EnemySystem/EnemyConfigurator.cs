using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyConfig", menuName = "Enemy System/Enemy Configuration")]
public class EnemyConfig : ScriptableObject
{
    [Header("基础属性")]
    public string enemyName = "New Enemy";
    public float maxHealth = 100f;
    public int layerAddAmount = 3;
    public float collisionDamage = 10f;

    [Header("移动设置")]
    public float moveSpeed = 3f;
    public float patrolRadius = 5f;
    public float chaseSpeed = 5f;
    public float chaseRange = 8f;

    [Header("攻击设置")]
    public float attackDamage = 15f;
    public float attackRange = 2f;
    public float attackCooldown = 1.5f;

    [Header("视觉设置")]
    public GameObject prefab;
    public Sprite sprite;
    public Color color = Color.white;

    [Header("效果设置")]
    public GameObject hitAnimationPrefab;
    public GameObject deathAnimationPrefab;
    public ParticleSystem trailParticlePrefab;

    [Header("掉落设置")]
    public GameObject[] dropItems;
    public float dropChance = 0.3f;
}

[System.Serializable]
public class EnemySpawnData
{
    public EnemyConfig config;
    public Vector2 position;
    public Quaternion rotation;
    public int spawnCount = 1;
}