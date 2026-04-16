using UnityEngine;
using UnityEngine.XR;

public class RTShoot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform bulletOrigin;
    [SerializeField] private GameObject bulletPrefab;

    [Header("Fire Settings")]
    [SerializeField] private float fireInterval = 0.15f;
    [SerializeField] private float bulletSpeed = 25f;

    [Header("Heat Settings")]
    [SerializeField] private float heatPerBullet = 3f;          // 每发子弹增加的热量
    [SerializeField] private float overheatThreshold = 300f;    // 过热阈值
    [SerializeField] private float normalCoolRate = 100f;       // 正常冷却速率（每秒）
    [SerializeField] private float overheatCoolRate = 60f;      // 过热状态冷却速率（每秒）

    private InputDevice rightHandDevice;
    private bool isTrigger;
    private float shootTimer;
    private float barrelHeat;     // 当前枪管热量值
    private bool isOverheated;
    private PlayerHealth _playerHealth;

    [Header("Haptic Feedback")]
    [SerializeField] private float hapticAmplitude = 0.4f;
    [SerializeField] private float hapticDuration  = 0.05f;

    void Start()
    {   
        TryGetRightHandDevice();
        _playerHealth = FindObjectOfType<PlayerHealth>();
    }

    void Update()
    {
    if (!rightHandDevice.isValid)
        TryGetRightHandDevice();

    // 玩家死亡时禁止所有射击逻辑
    if (_playerHealth != null && _playerHealth.IsDead())
    {
        isTrigger = false;
        shootTimer = 0f;
        return;
    }

    bool triggerPressed = false;
    rightHandDevice.TryGetFeatureValue(CommonUsages.triggerButton, out triggerPressed);

    // 冷却（无论是否过热，只要没在射击就降温）
    if (!triggerPressed)
    {
        float coolRate = isOverheated ? overheatCoolRate : normalCoolRate;
        barrelHeat -= coolRate * Time.deltaTime;
        if (barrelHeat < 0f) barrelHeat = 0f;

        // 过热状态：热量归零才解除
        if (isOverheated && barrelHeat <= 0f)
        {
            isOverheated = false;
            Debug.Log("Weapon cooled down");
        }
    }

    // 过热中，禁止射击
    if (isOverheated)
    {
        isTrigger = triggerPressed;
        return;
    }

    // 按下瞬间立即射击一发
    if (triggerPressed && !isTrigger)
    {
        FireOnce();
        shootTimer = 0f;
    }

    // 长按连发
    if (triggerPressed)
    {
        shootTimer += Time.deltaTime;
        if (shootTimer >= fireInterval)
        {
            FireOnce();
            shootTimer = 0f;
        }
    }
    else
    {
        shootTimer = 0f;
    }

    isTrigger = triggerPressed;
}

    private void FireOnce()
    {
        if (bulletPrefab == null || bulletOrigin == null) return;

        GameObject bullet = Instantiate(
            bulletPrefab,
            bulletOrigin.position,
            bulletOrigin.rotation
        );

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.velocity = bulletOrigin.forward * bulletSpeed;
        }

        // 射击震动（屏幕）
        CockpitShake.TriggerShoot();

        // 右手柄震动反馈
        if (rightHandDevice.isValid)
            rightHandDevice.SendHapticImpulse(0, hapticAmplitude, hapticDuration);

        // 每发子弹增加热量
        barrelHeat += heatPerBullet;
        if (barrelHeat >= overheatThreshold)
        {
            barrelHeat = overheatThreshold;
            isOverheated = true;
            Debug.Log("Overheated!");
        }
    }

    /// <summary>
    /// Resets barrel heat to 0 and clears overheat state.
    /// Called by the ABBC weapon-repair button sequence.
    /// </summary>
    public void ResetHeat()
    {
        barrelHeat = 0f;
        isOverheated = false;
        Debug.Log("[RTShoot] Heat reset by repair sequence.");
    }

    // ─── UIManager 读取接口 ───
    public float GetHeat()        => barrelHeat;
    public float GetMaxHeat()     => overheatThreshold;
    public float GetHeatPercent() => overheatThreshold > 0f ? barrelHeat / overheatThreshold : 0f;
    public bool  IsOverheated()   => isOverheated;

    private void TryGetRightHandDevice()
    {
        rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
    }
}