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
    private const int MAX_SHIELD_COUNT = 16;

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

    // 死亡时需要禁用的组件，运行时自动查找，无需 Inspector 配置
    private CockpitThrottle  _throttle;
    private CockpitSimpleTurn _turn;
    private RTShoot           _rtShoot;

    private bool      isDead = false;
    private bool      _shieldDisabled = false;  // 难度设置：禁用护盾
    private Quaternion _aliveRotation;
    private Coroutine  _poseRoutine;

    void Start()
    {
        currentHealth  = maxHealth;
        _aliveRotation = transform.rotation;
        UpdateUI();

        if (deathPanel != null) deathPanel.SetActive(false);

        // 自动查找需要禁用的组件
        _throttle = FindObjectOfType<CockpitThrottle>();
        _turn     = FindObjectOfType<CockpitSimpleTurn>();
        _rtShoot  = FindObjectOfType<RTShoot>();
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

            //play SFX
             AudioManager.Instance?.PlaySfxAttachedOnce(SfxId.PlayerShieldBlock, transform, 0.9f);

            if (shieldCount == 0)
             AudioManager.Instance?.PlaySfxAttachedOnce(SfxId.PlayerShieldBreak, transform, 1f);

            Debug.Log($"[Player] 护盾抵挡伤害！剩余护盾次数：{shieldCount}");
            UpdateUI();
            return;
        }

        currentHealth -= damage;
        currentHealth  = Mathf.Clamp(currentHealth, 0f, maxHealth);

        // 受击震动
        CockpitShake.TriggerHit();

        //play SFX
        AudioManager.Instance?.PlaySfxAttachedOnce(SfxId.PlayerHurt, transform, 0.95f);

        Debug.Log($"[Player] 受到伤害 {damage}，当前血量：{currentHealth}/{maxHealth}");
        UpdateUI();

        if (currentHealth <= 0f) Die();
    }

    /// <summary>为护盾添加次数，上限 32。由 DBAC 序列调用。</summary>
    public void AddShield(int count)
    {
        if (_shieldDisabled) return;  // 难度设置：护盾已禁用
        shieldCount = Mathf.Min(shieldCount + count, MAX_SHIELD_COUNT);
        // play SFX
        AudioManager.Instance?.PlaySfxAttachedOnce(SfxId.PlayerShieldAdd, transform, 1f);

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

        // 恢复所有按钮序列功能
        SequenceManager.SetPlayerDead(false);

        UpdateUI();
        if (deathPanel != null) deathPanel.SetActive(false);

        // 恢复移动 / 视角旋转 / 射击
        SetMovementEnabled(true);

        if (_poseRoutine != null) StopCoroutine(_poseRoutine);
        _poseRoutine = StartCoroutine(RotateToRoutine(_aliveRotation));

        // play SFX
        AudioManager.Instance?.PlaySfxAttachedOnce(SfxId.PlayerRespawn, transform, 1f);

        Debug.Log($"[Player] 玩家以 {healthFraction * 100f:F0}% 生命值复活！");
    }

    /// <summary>回血（可供治疗道具调用）</summary>
    public void Heal(float amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        UpdateUI();
        // play SFX
        AudioManager.Instance?.PlaySfxAttachedOnce(SfxId.PlayerHeal, transform, 0.9f);
    }

    private void SetMovementEnabled(bool enabled)
    {
        if (_throttle != null) _throttle.enabled = enabled;
        if (_turn     != null) _turn.enabled     = enabled;
        if (_rtShoot  != null) _rtShoot.enabled  = enabled;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // play SFX
        AudioManager.Instance?.PlaySfxAttachedOnce(SfxId.PlayerFall, transform, 1f);

        // 屏蔽除复活以外的所有按钮序列
        SequenceManager.SetPlayerDead(true);

        Debug.Log("[Player] 玩家死亡！");

        // 禁用移动 / 视角旋转 / 射击
        SetMovementEnabled(false);

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

        // 恢复所有按钮序列功能
        SequenceManager.SetPlayerDead(false);

        UpdateUI();

        if (deathPanel != null) deathPanel.SetActive(false);

        // 恢复移动 / 视角旋转 / 射击
        SetMovementEnabled(true);

        if (_poseRoutine != null) StopCoroutine(_poseRoutine);
        _poseRoutine = StartCoroutine(RotateToRoutine(_aliveRotation));

        // play SFX
        AudioManager.Instance?.PlaySfxAttachedOnce(SfxId.PlayerRespawn, transform, 1f);

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

    public int GetShieldCount()    => _shieldDisabled ? 0 : shieldCount;
    public int GetMaxShieldCount() => _shieldDisabled ? 0 : MAX_SHIELD_COUNT;

    /// <summary>禁用护盾（难度 C/D 调用）。将护盾次数和上限均设为 0。</summary>
    public void DisableShield()
    {
        _shieldDisabled = true;
        shieldCount = 0;
        UpdateUI();
        Debug.Log("[Player] 护盾已禁用（难度设置）");
    }

    /// <summary>将当前血量设为 maxHealth。难度应用后调用。</summary>
    public void SetCurrentHealthToMax()
    {
        currentHealth = maxHealth;
        UpdateUI();
    }
}
