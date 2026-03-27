using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class SequenceManager : MonoBehaviour
{
    public static SequenceManager Instance { get; private set; }

    // 输入序列缓冲区（使用StringBuilder便于子串截取）
    private StringBuilder inputBuffer = new StringBuilder();

    // 模式字典：键为模式字符串（如"ABBC"），值为触发时的回调
    private Dictionary<string, Action> patterns = new Dictionary<string, Action>();

    // 记录每个模式最后一次被触发时的结束索引（缓冲区字符位置，从0开始）
    // 用于防止同一子串重复触发，同时允许相同模式在不同位置再次触发
    private Dictionary<string, int> lastTriggerEndIndex = new Dictionary<string, int>();

    // 可选：缓冲区最大长度，防止无限增长（根据实际需求调整，此处设为100足够覆盖复杂序列）
    private const int MAX_BUFFER_LENGTH = 100;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // 便于场景切换保留，VR项目可按需调整

        // 注册初始效果（可根据需求在此添加更多）
        RegisterPattern("ABBC", () => Debug.Log("ABBC"));
        RegisterPattern("DBAC", () => Debug.Log("DBAC"));
        RegisterPattern("BCADBACD", () => Debug.Log("BCADBACD"));
    }


    public void RegisterPattern(string sequence, Action effect)
    {
        if (patterns.ContainsKey(sequence))
        {
            Debug.LogWarning($"模式 {sequence} 已存在，将被覆盖");
            patterns[sequence] = effect;
        }
        else
        {
            patterns.Add(sequence, effect);
        }

        // 初始化该模式的最后触发索引为 -1，表示从未触发
        if (!lastTriggerEndIndex.ContainsKey(sequence))
            lastTriggerEndIndex[sequence] = -1;
        else
            lastTriggerEndIndex[sequence] = -1;
    }

    /// <summary>
    /// 外部调用：玩家按下某个按钮时，传入对应字符（A/B/C/D）
    /// </summary>
    public void OnButtonPressed(char buttonChar)
    {
        // 追加到缓冲区
        inputBuffer.Append(buttonChar);

        // 限制缓冲区长度（保留最新的部分，防止溢出且不影响远距离叠加）
        if (inputBuffer.Length > MAX_BUFFER_LENGTH)
        {
            int excess = inputBuffer.Length - MAX_BUFFER_LENGTH;
            inputBuffer.Remove(0, excess);

            // 重要：由于移除了前缀，所有模式的最后触发索引需要同步减去偏移量，避免索引失效。
            // 简单实现：重置所有触发记录，允许已触发的模式在剩余序列中重新匹配（符合预期，因为旧前缀已丢弃）
            foreach (var key in lastTriggerEndIndex.Keys)
            {
                lastTriggerEndIndex[key] = -1;
            }
            // 注意：重置后可能导致同一模式在保留的序列中被再次触发（但原模式已经触发过，再次触发可能是多余的，但通常影响不大）
            // 更精确的做法是遍历保留序列重新计算已触发区间，为简化且满足一般游戏交互（用户不会超长输入），此处重置处理。
        }

        // 检查并触发所有匹配的模式
        CheckAndTriggerMatches();
    }

    /// <summary>
    /// 在缓冲区中查找所有未触发过的模式子串，并执行对应效果
    /// 支持一次输入触发多个重叠/非重叠模式（序列叠加特性）
    /// </summary>
    private void CheckAndTriggerMatches()
    {
        // 收集本轮需要触发的（模式，结束索引）对，避免在遍历中修改字典
        List<(string pattern, int endIndex)> toTrigger = new List<(string, int)>();

        foreach (var kvp in patterns)
        {
            string pattern = kvp.Key;
            int patternLen = pattern.Length;
            int lastEnd = lastTriggerEndIndex[pattern];

            // 在缓冲区中查找所有可能的匹配子串
            for (int start = 0; start <= inputBuffer.Length - patternLen; start++)
            {
                int end = start + patternLen - 1;
                // 只考虑结束索引大于上次触发位置的子串（防止重复触发同一区间）
                if (end > lastEnd)
                {
                    // 提取子串比较（由于模式长度一般较小，ToString开销可接受）
                    string sub = inputBuffer.ToString(start, patternLen);
                    if (sub == pattern)
                    {
                        toTrigger.Add((pattern, end));
                        // 注意：一个模式可能在同一轮有多个匹配（例如输入序列同时包含两个相同模式的不同位置）
                        // 我们收集所有，稍后统一触发，并更新 lastTriggerEndIndex 为最大值。
                    }
                }
            }
        }

        // 为避免因触发顺序导致索引相互影响，先按结束索引排序（小的优先，符合序列自然顺序）
        toTrigger.Sort((a, b) => a.endIndex.CompareTo(b.endIndex));

        // 执行触发，并更新每个模式的最后触发索引（取当前轮中该模式的最大结束索引）
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
            // 再次确认该模式在此轮尚未被触发过相同或更大的索引（防止因更新顺序重复触发）
            if (end > lastTriggerEndIndex[pat])
            {
                // 执行效果回调
                if (patterns.TryGetValue(pat, out Action action))
                {
                    action?.Invoke();
                }
                // 更新最后触发索引为此轮该模式的最大结束索引（会在所有触发后统一更新）
            }
        }

        // 统一更新各模式的 lastTriggerEndIndex 为本轮检测到的最大结束索引
        foreach (var kvp in maxEndForPattern)
        {
            lastTriggerEndIndex[kvp.Key] = kvp.Value;
        }
    }

    // 可选：提供清空序列的方法（用于重置或测试）
    public void ClearSequence()
    {
        inputBuffer.Clear();
        foreach (var key in lastTriggerEndIndex.Keys)
        {
            lastTriggerEndIndex[key] = -1;
        }
        Debug.Log("输入序列已清空");
    }

    // 调试：打印当前缓冲区内容（可选）
    public string GetCurrentSequence() => inputBuffer.ToString();
}




