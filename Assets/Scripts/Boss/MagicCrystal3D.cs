// MagicCrystal3D.cs
using UnityEngine;
using System.Collections;

public class MagicCrystal3D : MonoBehaviour
{
    private Renderer crystalRenderer;
    private Material crystalMaterial;
    private Color[] colors = {
        new Color(0.2f, 0.6f, 1.0f),    // 蓝色
        new Color(0.2f, 0.8f, 0.3f),    // 绿色
        new Color(1.0f, 0.8f, 0.2f),    // 黄色
        new Color(1.0f, 0.3f, 0.8f),    // 粉色
        new Color(0.3f, 0.8f, 1.0f)     // 青色
    };
    private int currentColorIndex = 0;

    [Header("视觉效果")]
    public ParticleSystem colorChangeParticles;
    public Light crystalLight;
    public float rotationSpeed = 30f;
    public float floatAmplitude = 0.5f;
    public float floatFrequency = 1f;

    private Vector3 startPosition;
    private bool isInteractable = true;

    void Start()
    {
        crystalRenderer = GetComponent<Renderer>();
        crystalMaterial = crystalRenderer.material;
        startPosition = transform.position;

        // 启用发光
        crystalMaterial.EnableKeyword("_EMISSION");

        // 设置初始颜色
        ChangeCrystalColor(colors[currentColorIndex]);

        // 开始浮动动画
        StartCoroutine(FloatAnimation());
    }

    void Update()
    {
        // 持续旋转
        transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
    }

    void OnMouseDown()
    {
        if (!isInteractable) return;

        // 循环切换颜色
        currentColorIndex = (currentColorIndex + 1) % colors.Length;
        ChangeCrystalColor(colors[currentColorIndex]);

        // 触发粒子效果
        if (colorChangeParticles != null)
        {
            var main = colorChangeParticles.main;
            main.startColor = colors[currentColorIndex];
            colorChangeParticles.Play();
        }

        // 播放声音
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.pitch = 0.8f + (currentColorIndex * 0.1f);
            audioSource.Play();
        }

        // 短暂禁用交互
        StartCoroutine(InteractionCooldown());
    }

    private void ChangeCrystalColor(Color newColor)
    {
        crystalMaterial.color = newColor;
        crystalMaterial.SetColor("_EmissionColor", newColor * 2f);

        // 更新灯光颜色
        if (crystalLight != null)
        {
            crystalLight.color = newColor;
        }
    }

    private IEnumerator FloatAnimation()
    {
        while (true)
        {
            float newY = startPosition.y + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            yield return null;
        }
    }

    private IEnumerator InteractionCooldown()
    {
        isInteractable = false;
        yield return new WaitForSeconds(0.5f);
        isInteractable = true;
    }
}