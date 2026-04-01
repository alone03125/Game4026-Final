using UnityEngine;

/// <summary>
/// 玩家子弹
/// - 碰到 Enemy：调用 Die()，自身销毁
/// - 碰到敌方子弹（Tag = "EnemyBullet"）：双方同时销毁
/// - 碰到其他非 Player 物体：自身销毁
/// </summary>
public class PlayerBullet : MonoBehaviour
{
    public float speed    = 10f;
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
        // 击中敌人
        if (other.CompareTag("Enemy"))
        {
            Enemy enemy = other.GetComponent<Enemy>();
            if (enemy != null)
                enemy.Die();
            Destroy(gameObject);
            return;
        }

        // 与敌方子弹对消
        if (other.CompareTag("EnemyBullet"))
        {
            Destroy(other.gameObject);
            Destroy(gameObject);
            return;
        }

        // 忽略 Player 自身（通过 Layer 判断）
        if (other.gameObject.layer == LayerMask.NameToLayer("Player")) return;

        // 碰到其他物体（如墙壁）销毁
        Destroy(gameObject);
    }
}
