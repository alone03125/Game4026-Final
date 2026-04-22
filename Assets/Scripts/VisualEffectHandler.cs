using System.Collections;
using UnityEngine;

public class VisualEffectHandler : MonoBehaviour
{
    [Header("物体引用 (Inspector中拖入)")]
    [SerializeField] private GameObject healEffectObject;   // 回复技能时显示的物体
    [SerializeField] private GameObject shieldEffectObject; // 护盾技能时显示的物体

    [Header("设置")]
    [SerializeField] private float displayDuration = 2f;    // 显示持续时间

    // 协程引用，用于重置计时器
    private Coroutine healCoroutine;
    private Coroutine shieldCoroutine;

    /// <summary>
    /// 显示回复效果物体，重置计时器
    /// </summary>
    public void ShowHealEffect()
    {
        if (healEffectObject == null) return;

        // 如果已有协程在运行，先停止它（重置计时器）
        if (healCoroutine != null)
            StopCoroutine(healCoroutine);

        // 显示物体
        healEffectObject.SetActive(true);

        // 启动新协程，延迟后隐藏
        healCoroutine = StartCoroutine(HideAfterDelay(healEffectObject, displayDuration, () => healCoroutine = null));
    }

    /// <summary>
    /// 显示护盾效果物体，重置计时器
    /// </summary>
    public void ShowShieldEffect()
    {
        if (shieldEffectObject == null) return;

        if (shieldCoroutine != null)
            StopCoroutine(shieldCoroutine);

        shieldEffectObject.SetActive(true);
        shieldCoroutine = StartCoroutine(HideAfterDelay(shieldEffectObject, displayDuration, () => shieldCoroutine = null));
    }

    private IEnumerator HideAfterDelay(GameObject obj, float delay, System.Action onComplete)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null)
            obj.SetActive(false);
        onComplete?.Invoke();
    }
}