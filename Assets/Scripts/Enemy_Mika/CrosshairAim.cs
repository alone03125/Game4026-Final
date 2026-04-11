using UnityEngine;

/// <summary>
/// 挂载在 BulletSP 上，每帧将自身 forward 朝向 AttackWarningUI 画布上的准星红点。
/// 子弹从 BulletSP.position 沿 BulletSP.forward 发射，即飞向准星方向。
/// </summary>
public class CrosshairAim : MonoBehaviour
{
    [Tooltip("场景中的 AttackWarningUI 组件（WarningCanvas 上）")]
    public AttackWarningUI warningUI;

    void LateUpdate()
    {
        if (warningUI == null) return;

        Vector3 crosshair = warningUI.CrosshairWorldPos;
        Vector3 dir = transform.position - crosshair;  // 从红点穿过 BulletSP 向外
        if (dir.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(dir);
    }
}
