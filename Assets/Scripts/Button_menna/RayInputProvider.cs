using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== ��������㣨������ߣ�====================
/// <summary>
/// ���������ת��Ϊ3D��ť��Press���á�
/// ��VR��Ŀ�У��������滻�˽ű�ΪVR��������ű���ֻ����� Button3D.Press ���ɣ��������޸İ�ť�͹������߼���
/// </summary>
public class RayInputProvider : MonoBehaviour
{
    [Header("��������")]
    [SerializeField] private Camera raycastCamera; // ����ָ�����Զ���ȡ�����
    [SerializeField] private LayerMask buttonLayer = -1; // Ĭ�����в�

    void Start()
    {
        if (raycastCamera == null)
            raycastCamera = Camera.main;
        if (raycastCamera == null)
            Debug.LogError("δ�ҵ�����������ֶ�ָ�� RayInputProvider �� raycastCamera");
    }

    void Update()
    {
        // �����������PC/�༭�����ԣ�
        // Buttons are now triggered by VR controller physical touch via Button3D.OnTriggerEnter.
        // This RayInputProvider component is no longer needed in VR builds and can be removed from the scene.
#if UNITY_EDITOR
        // Editor-only mouse fallback for testing without a headset:
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = raycastCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, buttonLayer))
            {
                Button3D btn = hit.collider.GetComponent<Button3D>();
                if (btn != null)
                    btn.Press();
            }
        }
#endif
    }

    // VR��Ŀ�У��ɽ��ô˽ű�������VR�ֱ����߽ű���ͬ������ hit.collider.GetComponent<Button3D>()?.Press();
}