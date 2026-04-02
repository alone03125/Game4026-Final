using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class SequenceManager : MonoBehaviour
{
    public static SequenceManager Instance { get; private set; }

    // �������л�������ʹ��StringBuilder�����Ӵ���ȡ��
    private StringBuilder inputBuffer = new StringBuilder();

    // ģʽ�ֵ䣺��Ϊģʽ�ַ�������"ABBC"����ֵΪ����ʱ�Ļص�
    private Dictionary<string, Action> patterns = new Dictionary<string, Action>();

    // ��¼ÿ��ģʽ���һ�α�����ʱ�Ľ����������������ַ�λ�ã���0��ʼ��
    // ���ڷ�ֹͬһ�Ӵ��ظ�������ͬʱ������ͬģʽ�ڲ�ͬλ���ٴδ���
    private Dictionary<string, int> lastTriggerEndIndex = new Dictionary<string, int>();

    // ��ѡ����������󳤶ȣ���ֹ��������������ʵ������������˴���Ϊ100�㹻���Ǹ������У�
    private const int MAX_BUFFER_LENGTH = 100;

    // 缓存场景中的目标组件引用
    private RTShoot rtShoot;
    private PlayerHealth playerHealth;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // ���ڳ����л�������VR��Ŀ�ɰ������

        rtShoot      = FindObjectOfType<RTShoot>();
        playerHealth = FindObjectOfType<PlayerHealth>();

        // ABBC：武器快速修复——枪管热量归零，立即解除过热
        RegisterPattern("ABBC", () =>
        {
            if (rtShoot != null) rtShoot.ResetHeat();
            else Debug.LogWarning("[SequenceManager] RTShoot not found for ABBC");
        });

        // DBAC：护盾充能——为次数盾添加 8 次（上限 32）
        RegisterPattern("DBAC", () =>
        {
            if (playerHealth != null) playerHealth.AddShield(8);
            else Debug.LogWarning("[SequenceManager] PlayerHealth not found for DBAC");
        });

        // BCADBACD：机甲修复——恢复最大生命值的 10%
        RegisterPattern("BCADBACD", () =>
        {
            if (playerHealth != null) playerHealth.HealPercent(0.1f);
            else Debug.LogWarning("[SequenceManager] PlayerHealth not found for BCADBACD");
        });

        // ABBBBCD：复活——以 30% 最大生命值复活（仅死亡状态有效）
        RegisterPattern("ABBBBCD", () =>
        {
            if (playerHealth != null) playerHealth.RevivePartial(0.3f);
            else Debug.LogWarning("[SequenceManager] PlayerHealth not found for ABBBBCD");
        });
    }


    public void RegisterPattern(string sequence, Action effect)
    {
        if (patterns.ContainsKey(sequence))
        {
            // Chain callbacks so multiple systems can respond to the same sequence.
            patterns[sequence] += effect;
        }
        else
        {
            patterns.Add(sequence, effect);
        }

        // ��ʼ����ģʽ����󴥷�����Ϊ -1����ʾ��δ����
        if (!lastTriggerEndIndex.ContainsKey(sequence))
            lastTriggerEndIndex[sequence] = -1;
        else
            lastTriggerEndIndex[sequence] = -1;
    }

    /// <summary>
    /// �ⲿ���ã���Ұ���ĳ����ťʱ�������Ӧ�ַ���A/B/C/D��
    /// </summary>
    public void OnButtonPressed(char buttonChar)
    {
        // ׷�ӵ�������
        inputBuffer.Append(buttonChar);

        // ���ƻ��������ȣ��������µĲ��֣���ֹ����Ҳ�Ӱ��Զ������ӣ�
        if (inputBuffer.Length > MAX_BUFFER_LENGTH)
        {
            int excess = inputBuffer.Length - MAX_BUFFER_LENGTH;
            inputBuffer.Remove(0, excess);

            // ��Ҫ�������Ƴ���ǰ׺������ģʽ����󴥷�������Ҫͬ����ȥƫ��������������ʧЧ��
            // ��ʵ�֣��������д�����¼�������Ѵ�����ģʽ��ʣ������������ƥ�䣨����Ԥ�ڣ���Ϊ��ǰ׺�Ѷ�����
            foreach (var key in lastTriggerEndIndex.Keys)
            {
                lastTriggerEndIndex[key] = -1;
            }
            // ע�⣺���ú���ܵ���ͬһģʽ�ڱ����������б��ٴδ�������ԭģʽ�Ѿ����������ٴδ��������Ƕ���ģ���ͨ��Ӱ�첻��
            // ����ȷ�������Ǳ��������������¼����Ѵ������䣬Ϊ��������һ����Ϸ�������û����ᳬ�����룩���˴����ô�����
        }

        // ��鲢��������ƥ���ģʽ
        CheckAndTriggerMatches();
    }

    /// <summary>
    /// �ڻ������в�������δ��������ģʽ�Ӵ�����ִ�ж�ӦЧ��
    /// ֧��һ�����봥������ص�/���ص�ģʽ�����е������ԣ�
    /// </summary>
    private void CheckAndTriggerMatches()
    {
        // �ռ�������Ҫ�����ģ�ģʽ�������������ԣ������ڱ������޸��ֵ�
        List<(string pattern, int endIndex)> toTrigger = new List<(string, int)>();

        foreach (var kvp in patterns)
        {
            string pattern = kvp.Key;
            int patternLen = pattern.Length;
            int lastEnd = lastTriggerEndIndex[pattern];

            // �ڻ������в������п��ܵ�ƥ���Ӵ�
            for (int start = 0; start <= inputBuffer.Length - patternLen; start++)
            {
                int end = start + patternLen - 1;
                // ֻ���ǽ������������ϴδ���λ�õ��Ӵ�����ֹ�ظ�����ͬһ���䣩
                if (end > lastEnd)
                {
                    // ��ȡ�Ӵ��Ƚϣ�����ģʽ����һ���С��ToString�����ɽ��ܣ�
                    string sub = inputBuffer.ToString(start, patternLen);
                    if (sub == pattern)
                    {
                        toTrigger.Add((pattern, end));
                        // ע�⣺һ��ģʽ������ͬһ���ж��ƥ�䣨������������ͬʱ����������ͬģʽ�Ĳ�ͬλ�ã�
                        // �����ռ����У��Ժ�ͳһ������������ lastTriggerEndIndex Ϊ���ֵ��
                    }
                }
            }
        }

        // Ϊ�����򴥷�˳���������໥Ӱ�죬�Ȱ�������������С�����ȣ�����������Ȼ˳��
        toTrigger.Sort((a, b) => a.endIndex.CompareTo(b.endIndex));

        // ִ�д�����������ÿ��ģʽ����󴥷�������ȡ��ǰ���и�ģʽ��������������
        Dictionary<string, int> maxEndForPattern = new Dictionary<string, int>();
        foreach (var item in toTrigger)
        {
            string pat = item.pattern;
            int end = item.endIndex;
            if (!maxEndForPattern.ContainsKey(pat) || end > maxEndForPattern[pat])
                maxEndForPattern[pat] = end;
        }

        foreach (var item in toTrigger)
        {
            string pat = item.pattern;
            int end = item.endIndex;
            // �ٴ�ȷ�ϸ�ģʽ�ڴ�����δ����������ͬ��������������ֹ�����˳���ظ�������
            if (end > lastTriggerEndIndex[pat])
            {
                // ִ��Ч���ص�
                if (patterns.TryGetValue(pat, out Action action))
                {
                    action?.Invoke();
                }
                // ������󴥷�����Ϊ���ָ�ģʽ���������������������д�����ͳһ���£�
            }
        }

        // ͳһ���¸�ģʽ�� lastTriggerEndIndex Ϊ���ּ�⵽������������
        foreach (var kvp in maxEndForPattern)
        {
            lastTriggerEndIndex[kvp.Key] = kvp.Value;
        }
    }

    // ��ѡ���ṩ������еķ������������û���ԣ�
    public void ClearSequence()
    {
        inputBuffer.Clear();
        foreach (var key in lastTriggerEndIndex.Keys)
        {
            lastTriggerEndIndex[key] = -1;
        }
        Debug.Log("�������������");
    }

    // ���ԣ���ӡ��ǰ���������ݣ���ѡ��
    public string GetCurrentSequence() => inputBuffer.ToString();
}




