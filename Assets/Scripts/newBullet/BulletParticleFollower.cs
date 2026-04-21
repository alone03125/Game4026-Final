using UnityEngine;
using System.Collections.Generic;

public class MultiParticleFollower : MonoBehaviour
{
    [Tooltip("如果不手动拖入，会自动获取此物体及其所有子物体中的ParticleSystem")]
    public ParticleSystem[] particleSystems;

    // 为每个粒子系统单独存储其当前活动粒子的局部偏移
    private List<ParticleSystem.Particle[]> particlesList = new List<ParticleSystem.Particle[]>();
    private List<Vector3[]> offsetsList = new List<Vector3[]>();

    void Awake()
    {
        // 如果没有手动指定，自动获取所有子物体及自身的ParticleSystem
        if (particleSystems == null || particleSystems.Length == 0)
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>();
        }

        // 为每个粒子系统准备存放粒子的数组和偏移数组
        foreach (var ps in particleSystems)
        {
            int maxParticles = ps.main.maxParticles;
            particlesList.Add(new ParticleSystem.Particle[maxParticles]);
            offsetsList.Add(null); // 延迟初始化
        }
    }

    void LateUpdate()
    {
        // 当前子弹的位置和旋转
        Vector3 bulletPos = transform.position;
        Quaternion bulletRot = transform.rotation;

        // 遍历每一个粒子系统
        for (int idx = 0; idx < particleSystems.Length; idx++)
        {
            var ps = particleSystems[idx];
            if (ps == null) continue;

            // 获取当前存活的粒子
            var particles = particlesList[idx];
            int numAlive = ps.GetParticles(particles);
            if (numAlive == 0) continue;

            // 确保偏移数组长度足够
            if (offsetsList[idx] == null || offsetsList[idx].Length != numAlive)
            {
                offsetsList[idx] = new Vector3[numAlive];
            }
            var offsets = offsetsList[idx];

            // 遍历每个粒子
            for (int i = 0; i < numAlive; i++)
            {
                // 如果是新粒子（偏移未记录且粒子还有寿命），记录其相对于子弹的局部偏移
                if (offsets[i] == Vector3.zero && particles[i].remainingLifetime > 0)
                {
                    // 计算粒子在子弹局部坐标系下的坐标
                    offsets[i] = Quaternion.Inverse(bulletRot) * (particles[i].position - bulletPos);
                }

                // 根据子弹当前变换重新计算粒子的世界位置
                Vector3 newWorldPos = bulletPos + bulletRot * offsets[i];
                particles[i].position = newWorldPos;
            }

            // 将修改后的粒子数组写回粒子系统
            ps.SetParticles(particles, numAlive);
        }
    }
}