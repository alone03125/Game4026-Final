using UnityEngine;

/// <summary>
/// 伤害来源组件
/// 
/// 挂载在任何可造成伤害的物体上（玩家子弹、近战武器碰撞体等），
/// 用于携带伤害数值，供 BossController 和 Crystal 的碰撞检测读取。
///
/// 玩家子弹使用方式：
///   1. 在玩家子弹预制体上挂载此组件，设置 damage 值。
///   2. 设置 Tag = "PlayerBullet"。
///   3. 添加 Collider（Is Trigger = true）。
///   Crystal 和 Boss 检测到该 Tag 后会读取此组件的 damage 值。
///
/// 玩家近战使用方式：
///   1. 在近战武器碰撞体 GameObject 上挂载此组件，设置 damage 值。
///   2. 设置 Tag = "PlayerAttack"。
///   3. 攻击时启用 Collider，攻击结束时禁用 Collider。
/// </summary>
public class DamageSource : MonoBehaviour
{
    [Tooltip("此攻击造成的伤害值")]
    public float damage = 1f;
}
