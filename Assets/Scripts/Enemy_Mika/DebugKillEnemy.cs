using UnityEngine;

public class DebugKillEnemy : MonoBehaviour
{
    public KeyCode killKey = KeyCode.K; // 可自定义按键

    void Update()
    {
        if (Input.GetKeyDown(killKey))
        {
            // 查找所有带 "Enemy" 标签的物体
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            if (enemies.Length > 0)
            {
                // 击杀第一个找到的敌人（你也可以改成随机选择或全部击杀）
                Enemy enemy = enemies[0].GetComponent<Enemy>();
                if (enemy != null)
                {
                    enemy.Die();
                    Debug.Log($"【调试】击杀敌人：{enemies[0].name}");
                }
                else
                {
                    Debug.LogWarning("【调试】找到的物体上没有 Enemy 脚本，请检查标签是否正确。");
                }
            }
            else
            {
                Debug.Log("【调试】当前没有敌人可以击杀。");
            }
        }
    }
}