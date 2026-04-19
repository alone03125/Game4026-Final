using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class SequenceManager : MonoBehaviour
{
    public static SequenceManager Instance { get; private set; }

    [SerializeField] private TMP_Text sequenceDisplayText;

    private const int MAX_DISPLAY_LENGTH = 15;
    private Queue<char> recentInputs = new Queue<char>();

    // 输入缓冲区，用于存储玩家的按键序列
    private StringBuilder inputBuffer = new StringBuilder();

    private Dictionary<string, Action> patterns = new Dictionary<string, Action>();

    private Dictionary<string, int> lastTriggerEndIndex = new Dictionary<string, int>();

    private const int MAX_BUFFER_LENGTH = 1000;

    // 缓存场景中的目标组件引用
    private RTShoot rtShoot;
    private PlayerHealth playerHealth;

    // 玩家死亡标记：死亡时屏蔽复活以外的所有序列
    private bool _playerDead = false;
    private const string REVIVE_PATTERN = "ABBCD";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        rtShoot      = FindObjectOfType<RTShoot>();
        playerHealth = FindObjectOfType<PlayerHealth>();

        // ABBC：武器快速修复——枪管热量归零，立即解除过热
        RegisterPattern("ABBC", () =>
        {
            if (rtShoot == null) rtShoot = FindObjectOfType<RTShoot>();
            if (rtShoot != null) rtShoot.ResetHeat();
            else Debug.LogWarning("[SequenceManager] RTShoot not found for ABBC");
        });

        // DBAC：护盾充能——为次数盾添加 4 次（上限 16）
        RegisterPattern("DBAC", () =>
        {
            if (playerHealth == null) playerHealth = FindObjectOfType<PlayerHealth>();
            if (playerHealth != null) playerHealth.AddShield(4);
            else Debug.LogWarning("[SequenceManager] PlayerHealth not found for DBAC");
        });

        // CABDBAC：机甲修复——恢复最大生命值的 20%
        RegisterPattern("CABDBAC", () =>
        {
            if (playerHealth == null) playerHealth = FindObjectOfType<PlayerHealth>();
            if (playerHealth != null) playerHealth.HealPercent(0.2f);
            else Debug.LogWarning("[SequenceManager] PlayerHealth not found for CABDBAC");
        });

        // ABBCD：复活——以 70% 最大生命值复活（仅死亡状态有效）
        RegisterPattern("ABBCD", () =>
        {
            if (playerHealth == null) playerHealth = FindObjectOfType<PlayerHealth>();
            if (playerHealth != null) playerHealth.RevivePartial(0.7f);
            else Debug.LogWarning("[SequenceManager] PlayerHealth not found for ABBCD");
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

        if (!lastTriggerEndIndex.ContainsKey(sequence))
            lastTriggerEndIndex[sequence] = -1;
        else
            lastTriggerEndIndex[sequence] = -1;
    }

    private void UpdateSequenceDisplay()
    {
        if (sequenceDisplayText != null)
        {
            sequenceDisplayText.text = "Current Command Sequence: " + new string(recentInputs.ToArray());
        }
    }

    public void OnButtonPressed(char buttonChar)
    {
        // 更新最近15个输入的显示队列
        recentInputs.Enqueue(buttonChar);
        while (recentInputs.Count > MAX_DISPLAY_LENGTH)
            recentInputs.Dequeue();
        UpdateSequenceDisplay();

        inputBuffer.Append(buttonChar);

        if (inputBuffer.Length > MAX_BUFFER_LENGTH)
        {
            int excess = inputBuffer.Length - MAX_BUFFER_LENGTH;
            inputBuffer.Remove(0, excess);

            var keys = new List<string>(lastTriggerEndIndex.Keys);
            foreach (var key in keys)
            {
                lastTriggerEndIndex[key] = -1;
            }
        }

        CheckAndTriggerMatches();
    }

    private void CheckAndTriggerMatches()
    {
        List<(string pattern, int endIndex)> toTrigger = new List<(string, int)>();

        foreach (var kvp in patterns)
        {
            string pattern = kvp.Key;
            int patternLen = pattern.Length;
            int lastEnd = lastTriggerEndIndex[pattern];

            for (int start = 0; start <= inputBuffer.Length - patternLen; start++)
            {
                int end = start + patternLen - 1;
                if (end > lastEnd)
                {
                    string sub = inputBuffer.ToString(start, patternLen);
                    if (sub == pattern)
                    {
                        toTrigger.Add((pattern, end));
                    }
                }
            }
        }

        toTrigger.Sort((a, b) => a.endIndex.CompareTo(b.endIndex));

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
            if (end > lastTriggerEndIndex[pat])
            {
                if (patterns.TryGetValue(pat, out Action action))
                {
                    // 玩家死亡时，只允许执行复活序列
                    if (_playerDead && pat != REVIVE_PATTERN) continue;
                    action?.Invoke();
                }
            }
        }

        foreach (var kvp in maxEndForPattern)
        {
            lastTriggerEndIndex[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// 设置玩家死亡状态。死亡时除复活序列（ABBBBCD）外所有序列均被屏蔽。
    /// 由 PlayerHealth 在死亡/复活时调用。
    /// </summary>
    public static void SetPlayerDead(bool dead)
    {
        if (Instance != null) Instance._playerDead = dead;
    }

    // 清空当前输入缓冲区和所有模式的触发状态
    public void ClearSequence()
    {
        inputBuffer.Clear();
        foreach (var key in lastTriggerEndIndex.Keys)
        {
            lastTriggerEndIndex[key] = -1;
        }
        Debug.Log("已清空当前输入缓冲区和所有模式的触发状态");
    }

    // 获取当前输入缓冲区的内容
    public string GetCurrentSequence() => inputBuffer.ToString();
}




