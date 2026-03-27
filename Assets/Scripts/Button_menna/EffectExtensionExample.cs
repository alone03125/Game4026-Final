using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== 扩展示例：如何动态添加新效果 ====================

public class EffectExtensionExample : MonoBehaviour
{
    void Start()
    {
        // 假设我们要添加效果X: "ABC" 触发 嘲讽；效果Y: "BCD" 触发 充能；效果Z: "CDA" 触发 电磁脉冲
        // 只需在任意地方（例如游戏开始后）调用 RegisterPattern 即可
        SequenceManager.Instance?.RegisterPattern("ABC", () => Debug.Log("[自定义] 嘲讽效果：敌人注意力被吸引"));
        SequenceManager.Instance?.RegisterPattern("BCD", () => Debug.Log("[自定义] 充能效果：能量恢复30%"));
        SequenceManager.Instance?.RegisterPattern("CDA", () => Debug.Log("[自定义] 电磁脉冲：范围干扰"));

        // 演示序列叠加测试（根据题目：输入 ABCDA 应连续触发 X Y Z）
        // 可在控制台手动测试或写自动化测试
        Debug.Log("已注册 ABC / BCD / CDA 效果，测试序列叠加：ABCDA 应触发三次效果");
    }
}