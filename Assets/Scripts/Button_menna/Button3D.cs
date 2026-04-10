using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;


[RequireComponent(typeof(XRSimpleInteractable))]
public class Button3D : MonoBehaviour
{
    [Header("按钮标识")]
    [Tooltip("对应的字符，建议为大写字母 A/B/C/D")]
    public char buttonId;

    [Header("按压动画参数")]
    [SerializeField] private float pressScale = 0.9f;      // 按压时缩放比例
    [SerializeField] private float animationDuration = 0.1f; // 动画时长
    [SerializeField] private AnimationCurve pressCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("XR Interactable")]
    [SerializeField] private XRSimpleInteractable interactable;

    [Header("Cooldown")]
    [Tooltip("Cooldown in seconds between repeated triggers to prevent double-firing")]
    [SerializeField] private float pressCooldown = 1f;

    private Vector3 originalScale;
    private Coroutine activeAnimation;
    private bool isOnCooldown = false;

    void Reset()
    {
        interactable = GetComponent<XRSimpleInteractable>();
    }

    void Start()
    {
        originalScale = transform.localScale;

        if (interactable == null)
            interactable = GetComponent<XRSimpleInteractable>();

        // 校验按钮ID
        if (buttonId != 'A' && buttonId != 'B' && buttonId != 'C' && buttonId != 'D')
        {
            Debug.LogWarning($"按钮 {gameObject.name} 的 buttonId 被设为 {buttonId}，建议只支持 A/B/C/D");
        }
    }

    void OnEnable()
    {
        if (interactable == null)
            interactable = GetComponent<XRSimpleInteractable>();
        if (interactable != null)
            interactable.selectEntered.AddListener(OnSelectEntered);
    }

    void OnDisable()
    {
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnSelectEntered);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (isOnCooldown) return;

        Debug.Log($"Button {gameObject.name} poke select from {args.interactorObject}");
        Press();
        StartCoroutine(PressCooldown());
    }

    /// <summary>
    /// 外部调用：通过交互模块触发按钮，执行按压效果并通知相关模块
    /// </summary>
    public void Press()
    {
        // 执行按压动画
        if (activeAnimation != null)
            StopCoroutine(activeAnimation);
        activeAnimation = StartCoroutine(AnimatePress());

        // 通知核心模块
        SequenceManager.Instance?.OnButtonPressed(buttonId);
    }

    private IEnumerator PressCooldown()
    {
        isOnCooldown = true;
        yield return new WaitForSeconds(pressCooldown);
        isOnCooldown = false;
    }

    private IEnumerator AnimatePress()
    {
        float elapsed = 0f;
        Vector3 startScale = originalScale;
        Vector3 targetScale = originalScale * pressScale;

        // 压下去
        while (elapsed < animationDuration)
        {
            float t = elapsed / animationDuration;
            float curveValue = pressCurve.Evaluate(t);
            transform.localScale = Vector3.Lerp(startScale, targetScale, curveValue);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localScale = targetScale;

        // 弹回来
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