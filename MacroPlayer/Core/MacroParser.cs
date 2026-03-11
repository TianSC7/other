using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace MacroPlayer.Core;

/// <summary>
/// 宏解析器，负责解析按键序列
/// </summary>
public static class MacroParser
{
    /// <summary>
    /// 解析按键序列为按键列表
    /// </summary>
    /// <param name="sequence">输入序列（如 "DDQQ" 或 "D(30)Q(50)" 或 "18{ENTER}"）</param>
    /// <param name="defaultDelay">默认延迟时间（毫秒）</param>
    /// <returns>按键列表</returns>
    public static List<KeyAction> Parse(string sequence, int defaultDelay = 50)
    {
        var actions = new List<KeyAction>();
        if (string.IsNullOrWhiteSpace(sequence)) return actions;

        int i = 0;
        while (i < sequence.Length)
        {
            // 特殊键：{ENTER} {F1} {SPACE} 等
            if (sequence[i] == '{')
            {
                int end = sequence.IndexOf('}', i);
                if (end < 0) break;
                string key = sequence.Substring(i + 1, end - i - 1).ToUpper();
                i = end + 1;
                int delay = TryReadDelay(sequence, ref i, defaultDelay);
                actions.Add(new KeyAction { Key = key, Delay = delay });
            }
            else
            {
                // 普通单字符键
                string key = sequence[i].ToString().ToUpper();
                i++;
                int delay = TryReadDelay(sequence, ref i, defaultDelay);
                actions.Add(new KeyAction { Key = key, Delay = delay });
            }
        }

        return actions;
    }

    /// <summary>
    /// 尝试读取紧跟的 (数字) 延迟，没有则返回默认值
    /// </summary>
    private static int TryReadDelay(string seq, ref int i, int defaultDelay)
    {
        if (i < seq.Length && seq[i] == '(')
        {
            int end = seq.IndexOf(')', i);
            if (end > i && int.TryParse(seq.Substring(i + 1, end - i - 1), out int ms))
            {
                i = end + 1;
                return ms;
            }
        }
        return defaultDelay;
    }
}

/// <summary>
/// 按键动作
/// </summary>
public class KeyAction
{
    /// <summary>
    /// 按键名称
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// 延迟时间（毫秒）
    /// </summary>
    public int Delay { get; set; }
}
