using UnityEngine;

/// <summary>
/// 玩家子弹
/// - 碰到 Enemy：调用 Die()，自身销毁
/// - 碰到敌方子弹（Tag = "EnemyBullet"）：双方同时销毁
/// - 碰到其他非 Player 物体：自身销毁
/// - 销毁前在当前位置生成特效（特效不挂在子弹上，且延迟自动销毁）
/// </summary>
public class PlayerBullet : MonoBehaviour
{
    public float speed    = 10f;
    public float lifeTime = 3f;

    [Header("特效设置")]
    public GameObject[] effectsOnDestroy;          // 销毁时生成的特效预制体（可多个）
    public bool spawnEffectsAtWorldPosition = true; // 是否在世界空间生成（否则使用子弹局部坐标）
    public float effectAutoDestroyDelay = 2f;      // 特效生成后自动销毁的延迟时间（秒）

    private bool isDestroyed = false;               // 防止重复销毁/重复生成特效

    void Start()
    {
        // 超时销毁
        Invoke(nameof(DestroyBullet), lifeTime);
    }

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDestroyed) return;

        // 击中敌人
        if (other.CompareTag("Enemy"))
        {
            Enemy enemy = other.GetComponent<Enemy>();
            if (enemy != null)
                enemy.Die();
            DestroyBullet();
            return;
        }

        // 与敌方子弹对消
        if (other.CompareTag("EnemyBullet"))
        {
            Destroy(other.gameObject);
            DestroyBullet();
            return;
        }

        // 忽略 Player 自身（通过 Layer 判断）
        if (other.gameObject.layer == LayerMask.NameToLayer("Player")) return;

        // 碰到其他物体（如墙壁）销毁
        DestroyBullet();
    }

    /// <summary>
    /// 销毁子弹，并在销毁前生成特效
    /// </summary>
    private void DestroyBullet()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        // 生成所有特效（不挂载在子弹上）
        SpawnEffects();

        // 销毁子弹本身
        Destroy(gameObject);
    }

    /// <summary>
    /// 在子弹当前位置生成特效预制体，并安排自动销毁
    /// </summary>
    private void SpawnEffects()
    {
        if (effectsOnDestroy == null || effectsOnDestroy.Length == 0)
            return;

        Vector3 spawnPos = spawnEffectsAtWorldPosition ? transform.position : transform.localPosition;
        Quaternion spawnRot = transform.rotation;

        foreach (GameObject effectPrefab in effectsOnDestroy)
        {
            if (effectPrefab != null)
            {
                GameObject effect = Instantiate(effectPrefab, spawnPos, spawnRot);
                // 自动销毁特效，避免堆积
                Destroy(effect, effectAutoDestroyDelay);
            }
        }
    }
}