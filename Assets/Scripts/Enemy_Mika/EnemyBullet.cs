using UnityEngine;

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

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 与玩家子弹对消
        if (other.CompareTag("PlayerBullet"))
        {
            Destroy(other.gameObject);
            Destroy(gameObject);
            return;
        }

        // 击中玩家（通过 Layer 判断）
        if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null)
                playerHealth.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // 忽略 Enemy 自身
        if (other.CompareTag("Enemy")) return;

        // 碰到其他物体（如墙壁）销毁
        Destroy(gameObject);
    }
}
