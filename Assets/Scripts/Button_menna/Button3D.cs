using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== 3D按钮交互模块 ====================
/// <summary>
/// 挂载在每个3D扁方块按钮上，处理视觉反馈（按下动画）并通知序列管理器
/// 按钮颜色通过材质区分（红黄蓝绿），脚本不负责颜色，只需在Inspector中指定标识
/// </summary>
[RequireComponent(typeof(Collider))] // 确保按钮具有碰撞体供射线检测
public class Button3D : MonoBehaviour
{
    [Header("按钮标识")]
    [Tooltip("对应序列字符，必须为大写字母 A/B/C/D")]
    public char buttonId;

    [Header("按压动画参数")]
    [SerializeField] private float pressScale = 0.9f;      // 按压时的缩放比例
    [SerializeField] private float animationDuration = 0.1f; // 动画时长
    [SerializeField] private AnimationCurve pressCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Vector3 originalScale;
    private Coroutine activeAnimation;

    void Start()
    {
        originalScale = transform.localScale;

        // 简单校验按钮ID
        if (buttonId != 'A' && buttonId != 'B' && buttonId != 'C' && buttonId != 'D')
        {
            Debug.LogWarning($"按钮 {gameObject.name} 的 buttonId 设置为 {buttonId}，但只支持 A/B/C/D");
        }
    }

    /// <summary>
    /// 外部调用（通常由射线输入模块触发），执行按压效果并通知序列管理器
    /// </summary>
    public void Press()
    {
        // 播放按压动画
        if (activeAnimation != null)
            StopCoroutine(activeAnimation);
        activeAnimation = StartCoroutine(AnimatePress());

        // 通知核心管理器
        SequenceManager.Instance?.OnButtonPressed(buttonId);
    }

    private IEnumerator AnimatePress()
    {
        float elapsed = 0f;
        Vector3 startScale = originalScale;
        Vector3 targetScale = originalScale * pressScale;

        // 按下缩小
        while (elapsed < animationDuration)
        {
            float t = elapsed / animationDuration;
            float curveValue = pressCurve.Evaluate(t);
            transform.localScale = Vector3.Lerp(startScale, targetScale, curveValue);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localScale = targetScale;

        // 弹起恢复
        elapsed = 0f;
        while (elapsed < animationDuration)
        {
            float t = elapsed / animationDuration;
            float curveValue = pressCurve.Evaluate(t);
            transform.localScale = Vector3.Lerp(targetScale, startScale, curveValue);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localScale = startScale;
        activeAnimation = null;
    }
}

