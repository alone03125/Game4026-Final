using UnityEngine;

/// <summary>
/// 玩家测试控制器（第三人称）
/// 
/// 使用说明：
///   1. 在场景中创建一个 Capsule 作为玩家，Tag 设为 "Player"。
///   2. 挂载此脚本、PlayerHealth.cs 和 CharacterController 组件。
///   3. 创建一个子弹预制体挂载 DamageSource.cs，Tag = "PlayerBullet"，
///      Collider(IsTrigger=true)，Rigidbody(IsKinematic=true)，
///      再挂载 PlayerBullet.cs（或复用 Bullet1.cs 并设置好伤害）。
///   4. 将相机设为玩家子物体，或使用 Cinemachine。
///
/// 控制：
///   WASD      —— 移动
///   鼠标移动  —— 转向（Cursor.lockState = Locked）
///   鼠标左键  —— 射击（朝摄像机准星方向发射子弹）
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerHealth))]
public class PlayerController : MonoBehaviour
{
    [Header("=== 移动 ===")]
    public float moveSpeed = 6f;
    public float gravity   = -20f;
    public float jumpHeight = 1.5f;

    [Header("=== 摄像机旋转 ===")]
    [Tooltip("鼠标灵敏度")]
    public float mouseSensitivity = 2f;
    [Tooltip("摄像机挂载的 Transform（通常是玩家的子物体 CameraRoot）")]
    public Transform cameraTransform;

    [Header("=== 攻击 ===")]
    [Tooltip("玩家子弹预制体（需挂载 DamageSource.cs，Tag=PlayerBullet）")]
    public GameObject playerBullet1Prefab;
    [Tooltip("子弹发射速度")]
    public float bullet1Speed = 20f;
    [Tooltip("子弹伤害值（会写入 DamageSource.damage）")]
    public float bullet1Damage = 10f;

    // ─────────────────────────────────────────────
    // 私有
    // ─────────────────────────────────────────────
    private CharacterController cc;
    private Vector3             velocity;
    private float               xRotation  = 0f;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandleShooting();
    }

    // ─────────────────────────────────────────────
    // 鼠标视角
    // ─────────────────────────────────────────────

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // 左右旋转玩家整体
        transform.Rotate(Vector3.up * mouseX);

        // 上下旋转摄像机（限制仰角）
        if (cameraTransform != null)
        {
            xRotation -= mouseY;
            xRotation  = Mathf.Clamp(xRotation, -80f, 80f);
            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }
    }

    // ─────────────────────────────────────────────
    // 移动
    // ─────────────────────────────────────────────

    void HandleMovement()
    {
        // 地面检测
        if (cc.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        // WASD 输入
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = transform.right * h + transform.forward * v;
        cc.Move(move * moveSpeed * Time.deltaTime);

        // 跳跃（空格）
        if (Input.GetButtonDown("Jump") && cc.isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        // 重力
        velocity.y += gravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);
    }

    // ─────────────────────────────────────────────
    // 射击
    // ─────────────────────────────────────────────

    void HandleShooting()
    {
        // 按一下鼠标左键只发射一颗子弹
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("[PlayerController] 检测到鼠标左键按下，准备射击");
            Shoot();
        }
    }

    void Shoot()
    {
        if (playerBullet1Prefab == null)
        {
            Debug.LogWarning("[PlayerController] 未设置 playerBullet1Prefab！");
            return;
        }

        // 以摄像机正前方为射击方向；若没有摄像机则用玩家正前方
        Transform origin = (cameraTransform != null) ? cameraTransform : transform;
        Vector3 spawnPos = origin.position + origin.forward * 0.5f;
        Vector3 dir      = origin.forward;

        Debug.Log($"[PlayerController] 生成子弹，位置：{spawnPos}，方向：{dir}");
        GameObject bullet1Obj = Instantiate(playerBullet1Prefab, spawnPos, Quaternion.LookRotation(dir));
        bullet1Obj.tag = "PlayerBullet";

        // 写入伤害值
        DamageSource ds = bullet1Obj.GetComponent<DamageSource>();
        if (ds == null) ds = bullet1Obj.AddComponent<DamageSource>();
        ds.damage = bullet1Damage;

        // 获取或添加子弹移动组件
        PlayerBulletMover mover = bullet1Obj.GetComponent<PlayerBulletMover>();
        if (mover == null) mover = bullet1Obj.AddComponent<PlayerBulletMover>();
        mover.Initialize(dir, bullet1Speed);
    }
}
