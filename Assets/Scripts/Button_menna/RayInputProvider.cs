using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== 输入适配层（鼠标射线）====================
/// <summary>
/// 负责将鼠标点击转换为3D按钮的Press调用。
/// 在VR项目中，可整体替换此脚本为VR射线输入脚本（只需调用 Button3D.Press 即可），无需修改按钮和管理器逻辑。
/// </summary>
public class RayInputProvider : MonoBehaviour
{
    [Header("射线设置")]
    [SerializeField] private Camera raycastCamera; // 若不指定则自动获取主相机
    [SerializeField] private LayerMask buttonLayer = -1; // 默认所有层

    void Start()
    {
        if (raycastCamera == null)
            raycastCamera = Camera.main;
        if (raycastCamera == null)
            Debug.LogError("未找到主相机，请手动指定 RayInputProvider 的 raycastCamera");
    }

    void Update()
    {
        // 鼠标左键点击（PC/编辑器调试）
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = raycastCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, buttonLayer))
            {
                Button3D btn = hit.collider.GetComponent<Button3D>();
                if (btn != null)
                {
                    btn.Press();
                }
            }
        }
    }

    // VR项目中，可禁用此脚本，改用VR手柄射线脚本，同样调用 hit.collider.GetComponent<Button3D>()?.Press();
}