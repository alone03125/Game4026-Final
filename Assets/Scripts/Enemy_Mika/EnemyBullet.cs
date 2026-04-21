using UnityEngine;
using System.Collections;

/// <summary>
/// 敌方子弹
/// - 碰到玩家子弹（Tag = "PlayerBullet"）：双方同时销毁
/// - 碰到玩家（Tag = "Player"）：调用 TakeDamage，自身销毁
/// - 忽略 Enemy 自身
/// - 碰到其他物体：自身销毁
/// </summary>
public class EnemyBullet : MonoBehaviour
{
    public float speed    = 8f;
    public float damage   = 1f;
    public float lifeTime = 3f;

    [Header("命中/销毁特效")]
    [Tooltip("销毁时是否触发特效")]
    public bool enableHitVFX = false;
    [Tooltip("销毁时在当前位置生成的特效预制体（支持多个，独立实例，不挂载在子弹上）")]
    public GameObject[] hitVFXPrefabs;
    [Tooltip("特效自动销毁延迟（秒）；为 0 则由粒子系统自身时长决定")]
    public float hitVFXLifetime = 0f;

    private bool _hasHit = false;

    void Start()
    {
        StartCoroutine(LifetimeDestroy());
    }

    System.Collections.IEnumerator LifetimeDestroy()
    {
        yield return new WaitForSeconds(lifeTime);
        if (!_hasHit)
        {
            _hasHit = true;
            SpawnHitVFX();
            Destroy(gameObject);
        }
    }

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;

        // 与玩家子弹对消
        if (other.CompareTag("PlayerBullet"))
        {
            _hasHit = true;
            SpawnHitVFX();
            Destroy(other.gameObject);
            Destroy(gameObject);
            return;
        }

        // 命中任意玩家子碰撞体：只要能解析到 PlayerHealth 就判定为击中玩家
        PlayerHealth playerHealth = ResolvePlayerHealth(other);
        if (playerHealth != null)
        {
            _hasHit = true;
            playerHealth.TakeDamage(damage);
            SpawnHitVFX();
            Destroy(gameObject);
            return;
        }

        // 忽略 Enemy和 Boss 自身
        if (other.CompareTag("Enemy") || other.CompareTag("Boss")) return;

        _hasHit = true;
        SpawnHitVFX();
        Destroy(gameObject);
    }

    private void SpawnHitVFX()
    {
        if (!enableHitVFX || hitVFXPrefabs == null) return;

        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;

        foreach (GameObject prefab in hitVFXPrefabs)
        {
            if (prefab == null) continue;

            GameObject fx = Instantiate(prefab, pos, rot, null);

            ParticleSystem[] allPs = fx.GetComponentsInChildren<ParticleSystem>(true);
            float maxDuration = hitVFXLifetime > 0f ? hitVFXLifetime : 2f;

            foreach (ParticleSystem p in allPs)
            {
                p.gameObject.SetActive(true);
                p.Play(true);

                if (hitVFXLifetime <= 0f)
                {
                    var lt = p.main.startLifetime;
                    float lifetime = lt.mode == ParticleSystemCurveMode.Constant
                        ? lt.constant
                        : Mathf.Max(lt.constantMin, lt.constantMax);
                    float totalDur = p.main.duration + lifetime;
                    if (totalDur > maxDuration) maxDuration = totalDur;
                }
            }

            if (allPs.Length == 0 && hitVFXLifetime <= 0f)
                maxDuration = 5f;

            Destroy(fx, maxDuration + 0.5f);
        }
    }

    private PlayerHealth ResolvePlayerHealth(Collider other)
    {
        PlayerHealth ph = other.GetComponent<PlayerHealth>();
        if (ph != null) return ph;

        ph = other.GetComponentInChildren<PlayerHealth>(true);
        if (ph != null) return ph;

        ph = other.GetComponentInParent<PlayerHealth>();
        if (ph != null) return ph;

        Transform root = other.transform.root;
        if (root != null)
        {
            ph = root.GetComponent<PlayerHealth>();
            if (ph != null) return ph;

            ph = root.GetComponentInChildren<PlayerHealth>(true);
            if (ph != null) return ph;
        }

        if (other.attachedRigidbody != null)
        {
            ph = other.attachedRigidbody.GetComponent<PlayerHealth>();
            if (ph != null) return ph;

            ph = other.attachedRigidbody.GetComponentInParent<PlayerHealth>();
            if (ph != null) return ph;
        }

        return null;
    }
}
