using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 玩家血量组件（合并自 MechHealth）
/// 挂载在玩家 GameObject 上（Tag = "Player"）。
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("=== 玩家血量 ===")]
    public float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("=== 次数护盾 ===")]
    [SerializeField] private int shieldCount = 0;
    private const int MAX_SHIELD_COUNT = 32;

    [Header("=== UI（可选）===")]
    [Tooltip("显示血量的 Slider，留空则忽略")]
    public Slider healthSlider;
    [Tooltip("显示死亡提示的 GameObject，留空则忽略")]
    public GameObject deathPanel;

    [Header("=== Debug ===")]
    [SerializeField] private bool debugKillWithSpace = true;
    [SerializeField] private bool debugReviveWithF   = true;

    [Header("=== 死亡倒地 ===")]
    [SerializeField] private float fallAngle    = 90f;
    [SerializeField] private float fallDuration = 0.5f;
    [SerializeField] private Vector3 fallAxis   = Vector3.right;
    [SerializeField] private MonoBehaviour[] disableOnDeath; // 死亡时禁用的组件（如驾驶、射击等）

    private bool      isDead = false;
    private Quaternion _aliveRotation;
    private Coroutine  _poseRoutine;

    void Start()
    {
        currentHealth  = maxHealth;
        _aliveRotation = transform.rotation;
        UpdateUI();

        if (deathPanel != null) deathPanel.SetActive(false);
    }

    void Update()
    {
        if (debugReviveWithF && Input.GetKeyDown(KeyCode.F))
        {
            Revive();
            return;
        }
        if (isDead) return;
        if (debugKillWithSpace && Input.GetKeyDown(KeyCode.Space))
        {
            TakeDamage(maxHealth);
        }
    }

    /// <summary>受到伤害（由 Bullet 等系统调用）</summary>
    public void TakeDamage(float damage)
    {
        if (isDead) return;

        // 次数护盾：有护盾则抵扣，不扣血量
        if (shieldCount > 0)
        {
            shieldCount--;
            Debug.Log($"[Player] 护盾抵挡伤害！剩余护盾次数：{shieldCount}");
            UpdateUI();
            return;
        }

        currentHealth -= damage;
        currentHealth  = Mathf.Clamp(currentHealth, 0f, maxHealth);

        Debug.Log($"[Player] 受到伤害 {damage}，当前血量：{currentHealth}/{maxHealth}");
        UpdateUI();

        if (currentHealth <= 0f) Die();
    }

    /// <summary>为护盾添加次数，上限 32。由 DBAC 序列调用。</summary>
    public void AddShield(int count)
    {
        shieldCount = Mathf.Min(shieldCount + count, MAX_SHIELD_COUNT);
        Debug.Log($"[Player] 护盾充能，当前护盾次数：{shieldCount}");
    }

    /// <summary>恢复最大生命值的指定百分比。由 BCADBACD 序列调用。</summary>
    public void HealPercent(float percent)
    {
        Heal(maxHealth * percent);
        Debug.Log($"[Player] 机甲修复，恢复 {percent * 100f:F0}% 生命值");
    }

    /// <summary>
    /// 以指定比例的最大生命值复活玩家。仅在死亡状态下有效。
    /// 由 ABBBBCD 复活序列调用。
    /// </summary>
    public void RevivePartial(float healthFraction)
    {
        if (!isDead) return;

        currentHealth = Mathf.Clamp(maxHealth * healthFraction, 1f, maxHealth);
        isDead = false;

        UpdateUI();
        if (deathPanel != null) deathPanel.SetActive(false);

        foreach (var comp in disableOnDeath)
            if (comp != null) comp.enabled = true;

        if (_poseRoutine != null) StopCoroutine(_poseRoutine);
        _poseRoutine = StartCoroutine(RotateToRoutine(_aliveRotation));

        Debug.Log($"[Player] 玩家以 {healthFraction * 100f:F0}% 生命值复活！");
    }

    /// <summary>回血（可供治疗道具调用）</summary>
    public void Heal(float amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        UpdateUI();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("[Player] 玩家死亡！");

        // 禁用指定组件
        foreach (var comp in disableOnDeath)
            if (comp != null) comp.enabled = false;

        // 显示死亡 UI
        if (deathPanel != null) deathPanel.SetActive(true);

        // 倒地动画：使用 fallAxis 和 fallAngle
        if (_poseRoutine != null) StopCoroutine(_poseRoutine);
        Quaternion target = _aliveRotation * Quaternion.AngleAxis(fallAngle, fallAxis);
        _poseRoutine = StartCoroutine(RotateToRoutine(target));
    }

    public void Revive()
    {
        currentHealth = maxHealth;
        isDead        = false;

        UpdateUI();

        if (deathPanel != null) deathPanel.SetActive(false);

        foreach (var comp in disableOnDeath)
            if (comp != null) comp.enabled = true;

        if (_poseRoutine != null) StopCoroutine(_poseRoutine);
        _poseRoutine = StartCoroutine(RotateToRoutine(_aliveRotation));

        Debug.Log("[Player] 玩家复活！");
    }

    private void UpdateUI()
    {
        if (healthSlider != null)
            healthSlider.value = currentHealth / maxHealth;
    }

    private IEnumerator RotateToRoutine(Quaternion targetRot)
    {
        Quaternion startRot = transform.rotation;
        float t = 0f;
        while (t < fallDuration)
        {
            t += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(startRot, targetRot, Mathf.Clamp01(t / fallDuration));
            yield return null;
        }
        transform.rotation = targetRot;
        _poseRoutine = null;
    }

    public float GetCurrentHealth() => currentHealth;
    public float GetHealthPercent() => currentHealth / maxHealth;
    public bool  IsDead()           => isDead;
}
