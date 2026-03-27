using UnityEngine;

public class GravityManager : MonoBehaviour
{
    public static GravityManager Instance;
    public float currentGravity = 1f;   // 当前重力系数，范围0~2等

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // 可选：提供一个方法供外部修改重力
    public void SetGravity(float value)
    {
        currentGravity = Mathf.Clamp(value, 0f, 3f);
    }
}