using UnityEngine;

/// <summary>
/// 全局游戏计时器。
/// 从教程"射击"完成（第一阶段正式开始）时开始计时，
/// 到 Boss 被击倒时停止，并保存最终秒数供 YouWinText 读取。
/// 纯静态工具类，无需挂载到 GameObject。
/// </summary>
public static class GameTimer
{
    private static float _startTime;
    private static float _endTime;
    private static bool  _running;

    /// <summary>是否正在计时。</summary>
    public static bool IsRunning => _running;

    /// <summary>
    /// 已计时的总秒数。
    /// 计时中返回实时值；停止后返回记录的最终值。
    /// </summary>
    public static float ElapsedSeconds =>
        _running ? Time.realtimeSinceStartup - _startTime
                 : _endTime - _startTime;

    /// <summary>开始（或重置）计时。</summary>
    public static void StartTimer()
    {
        _startTime = Time.realtimeSinceStartup;
        _endTime   = _startTime;
        _running   = true;
        Debug.Log("[GameTimer] 计时开始");
    }

    /// <summary>停止计时并冻结最终时间。</summary>
    public static void StopTimer()
    {
        if (!_running) return;
        _endTime = Time.realtimeSinceStartup;
        _running = false;
        Debug.Log($"[GameTimer] 计时结束：{FormatTime(ElapsedSeconds)}（{ElapsedSeconds:F3} 秒）");
    }

    /// <summary>将秒数格式化为 MM:SS.mmm。</summary>
    public static string FormatTime(float seconds)
    {
        int totalMs  = Mathf.RoundToInt(seconds * 1000f);
        int ms       = totalMs % 1000;
        int totalSec = totalMs / 1000;
        int sec      = totalSec % 60;
        int min      = totalSec / 60;
        return $"{min:D2}:{sec:D2}.{ms:D3}";
    }
}
