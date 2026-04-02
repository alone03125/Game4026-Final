using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== 3D��ť����ģ�� ====================
/// <summary>
/// ������ÿ��3D�ⷽ�鰴ť�ϣ������Ӿ����������¶�������֪ͨ���й�����
/// ��ť��ɫͨ���������֣�������̣����ű���������ɫ��ֻ����Inspector��ָ����ʶ
/// </summary>
[RequireComponent(typeof(Collider))] // ȷ����ť������ײ�幩���߼��
public class Button3D : MonoBehaviour
{
    [Header("��ť��ʶ")]
    [Tooltip("��Ӧ�����ַ�������Ϊ��д��ĸ A/B/C/D")]
    public char buttonId;

    [Header("��ѹ��������")]
    [SerializeField] private float pressScale = 0.9f;      // ��ѹʱ�����ű���
    [SerializeField] private float animationDuration = 0.1f; // ����ʱ��
    [SerializeField] private AnimationCurve pressCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("VR Touch Settings")]
    [Tooltip("Tag assigned to VR controller objects (create this tag in Unity and assign it to both controller GameObjects)")]
    [SerializeField] private string controllerTag = "VRController";
    [Tooltip("Cooldown in seconds between repeated triggers to prevent double-firing")]
    [SerializeField] private float pressCooldown = 1f;

    private Vector3 originalScale;
    private Coroutine activeAnimation;
    private bool isOnCooldown = false;

    void Start()
    {
        originalScale = transform.localScale;

        // ��У�鰴ťID
        if (buttonId != 'A' && buttonId != 'B' && buttonId != 'C' && buttonId != 'D')
        {
            Debug.LogWarning($"��ť {gameObject.name} �� buttonId ����Ϊ {buttonId}����ֻ֧�� A/B/C/D");
        }
    }

    /// <summary>
    /// �ⲿ���ã�ͨ������������ģ�鴥������ִ�а�ѹЧ����֪ͨ���й�����
    /// </summary>
    public void Press()
    {
        // ���Ű�ѹ����
        if (activeAnimation != null)
            StopCoroutine(activeAnimation);
        activeAnimation = StartCoroutine(AnimatePress());

        // ֪ͨ���Ĺ�����
        SequenceManager.Instance?.OnButtonPressed(buttonId);
    }

    /// <summary>
    /// Called when a VR controller collider enters this button's trigger zone.
    /// The button's Collider must have "Is Trigger" checked.
    /// The controller GameObject must carry the Tag matching controllerTag.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (isOnCooldown) return;
        if (!other.CompareTag(controllerTag)) return;

        Press();
        StartCoroutine(PressCoooldown());
    }

    private IEnumerator PressCoooldown()
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

        // ������С
        while (elapsed < animationDuration)
        {
            float t = elapsed / animationDuration;
            float curveValue = pressCurve.Evaluate(t);
            transform.localScale = Vector3.Lerp(startScale, targetScale, curveValue);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localScale = targetScale;

        // ����ָ�
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

