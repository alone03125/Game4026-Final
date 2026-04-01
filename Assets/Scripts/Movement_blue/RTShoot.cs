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
    [SerializeField] private float overheatHoldSeconds = 5f;
    [SerializeField] private float cooldownSeconds = 2f;     

    private InputDevice rightHandDevice;
    private bool isTrigger;
    private float shootTimer;
    private float holdTime;
    private float cooldownTimer;
    private bool isOverheated;

    void Start()
    {   
        TryGetRightHandDevice();
    }

    void Update()
    {
    Debug.Log("rightHandDevice.isValid: " + rightHandDevice.isValid);

    if (!rightHandDevice.isValid)
        TryGetRightHandDevice();

    bool triggerPressed = false;
    rightHandDevice.TryGetFeatureValue(CommonUsages.triggerButton, out triggerPressed);

    // 過熱冷卻中
    if (isOverheated)
    {
        if (!triggerPressed)
        {
            cooldownTimer += Time.deltaTime;
            if (cooldownTimer >= cooldownSeconds)
            {
                isOverheated = false;
                cooldownTimer = 0f;
                holdTime = 0f;
                Debug.Log("Weapon cooled down");
            }
        }
        else
        {
            // 持續按著就不讓它冷卻
            cooldownTimer = 0f;
        }
        isTrigger = triggerPressed;
        return;
    }

    // 按下當下先發一顆
    if (triggerPressed && !isTrigger)
    {
        FireOnce();
        shootTimer = 0f;
    }

    // 長按連發 + 過熱計時
    if (triggerPressed)
    {
        holdTime += Time.deltaTime;
        
        Debug.Log($"Has been shooting for {shootTimer:F2} seconds");

        shootTimer += Time.deltaTime;

        if (shootTimer >= fireInterval)
        {
            FireOnce();
            shootTimer = 0f;
        }
        if (holdTime >= overheatHoldSeconds)
        {
            isOverheated = true;
            shootTimer = 0f;
            cooldownTimer = 0f;
            Debug.Log("Overheated!");
        }
    }
    else
    {
        shootTimer = 0f;
        holdTime = 0f;
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
            rb.useGravity = true;
            rb.velocity = bulletOrigin.forward * bulletSpeed; // 初速
        }
    }

    private void TryGetRightHandDevice()
    {
        rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
    }
}