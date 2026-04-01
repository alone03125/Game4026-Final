using System;
using UnityEngine;

/// <summary>
/// 挂载在信标 GameObject 上，当带有 "Player" Tag 的物体进入 Trigger 碰撞体时触发回调。
/// </summary>
[RequireComponent(typeof(Collider))]
public class BeaconTrigger : MonoBehaviour
{
    /// <summary>玩家进入信标范围时的回调，由 GameFlowManager 赋值。</summary>
    public Action OnPlayerEntered;

    void Awake()
    {
        // 确保碰撞体为 Trigger
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning("[BeaconTrigger] 碰撞体已自动设为 Trigger。请在 Prefab 上手动勾选 Is Trigger。");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Debug.Log("[BeaconTrigger] 玩家触碰信标！");
        OnPlayerEntered?.Invoke();
    }
}
