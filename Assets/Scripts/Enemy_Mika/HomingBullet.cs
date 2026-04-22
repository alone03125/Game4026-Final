using UnityEngine;


[DisallowMultipleComponent]
public class HomingBullet : MonoBehaviour
{
    [Header("锁定参数")]
    [Tooltip("生成时扫描敌人的锥形半角（度）。仅在此范围内的敌人才会被锁定。")]
    [Range(5f, 60f)]
    public float searchAngle = 20f;

    [Tooltip("每秒最大转向角度（度/秒）。值越大追踪越激进，建议 60–180。")]
    [Range(10f, 360f)]
    public float turnSpeed = 90f;

    [Tooltip("锁定目标的最大距离（米）。0 = 不限距离。")]
    public float maxLockDistance = 0f;

    // ── 运行时 ──────────────────────────────────────────────
    private Transform _target;
    private Rigidbody _rb;
    private float     _speed;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();

        // 读取当前速度大小（兼容两种模式）
        if (_rb != null && !_rb.isKinematic)
            _speed = _rb.velocity.magnitude;

        // 在 searchAngle 锥内锁定最近角度的敌人
        _target = FindTarget();
    }

    void FixedUpdate()
    {
        if (_target == null || !_target.gameObject.activeInHierarchy)
        {
            _target = null;
            return; // 无目标，保持直线
        }

        if (_rb != null && !_rb.isKinematic)
            SteerRigidbody();
    }

    void Update()
    {
        // Transform 模式（无 Rigidbody / kinematic）
        if (_rb == null || _rb.isKinematic)
            SteerTransform();
    }

    // ── 转向辅助 ────────────────────────────────────────────

    /// <summary>Rigidbody 模式：旋转速度向量方向。</summary>
    private void SteerRigidbody()
    {
        Vector3 toTarget = (_target.position - transform.position).normalized;
        Vector3 currentDir = _rb.velocity.normalized;

        float maxStep = turnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime;
        Vector3 newDir = Vector3.RotateTowards(currentDir, toTarget, maxStep, 0f);

        _rb.velocity = newDir * _speed;
        // 让子弹外观朝向飞行方向
        transform.rotation = Quaternion.LookRotation(newDir);
    }

    /// <summary>Transform 模式：旋转 forward 方向，再沿 forward 移动。</summary>
    private void SteerTransform()
    {
        if (_target == null || !_target.gameObject.activeInHierarchy) return;

        Vector3 toTarget = (_target.position - transform.position).normalized;
        float maxStep = turnSpeed * Mathf.Deg2Rad * Time.deltaTime;
        Vector3 newDir = Vector3.RotateTowards(transform.forward, toTarget, maxStep, 0f);
        transform.rotation = Quaternion.LookRotation(newDir);

        // 速度由外部脚本（PlayerBullet/Bullet）驱动，此处只管方向
    }

    // ── 目标搜索 ────────────────────────────────────────────

    private Transform FindTarget()
    {
        Transform best = null;
        float bestAngle = searchAngle; // 仅接受 < searchAngle 的

        foreach (Enemy enemy in Enemy.ActiveEnemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            Vector3 toEnemy = enemy.transform.position - transform.position;

            // 距离过滤
            if (maxLockDistance > 0f && toEnemy.magnitude > maxLockDistance)
                continue;

            float angle = Vector3.Angle(transform.forward, toEnemy);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = enemy.transform;
            }
        }

        return best;
    }
}
