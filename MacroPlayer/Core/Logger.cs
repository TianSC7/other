using System.IO;

namespace MacroPlayer.Core;

/// <summary>
/// 日志记录器
/// </summary>
public static class Logger
{
    private const string LogFileName = "MacroPlayer.log";
    private static readonly object _lock = new();

    /// <summary>
    /// 清空日志文件
    /// </summary>
    public static void Clear()
    {
        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogFileName);
        if (File.Exists(logPath))
        {
            File.WriteAllText(logPath, string.Empty);
        }
    }

    /// <summary>
    /// 记录信息
    /// </summary>
    public static void Info(string message)
    {
        WriteLog("INFO", message);
    }

    /// <summary>
    /// 记录警告
    /// </summary>
    public static void Warning(string message)
    {
        WriteLog("WARN", message);
    }

    /// <summary>
    /// 记录错误
    /// </summary>
    public static void Error(string message)
    {
        WriteLog("ERROR", message);
    }

    /// <summary>
    /// 写入日志
    /// </summary>
    private static void WriteLog(string level, string message)
    {
        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogFileName);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var logLine = $"[{timestamp}] [{level}] {message}";

        lock (_lock)
        {
            File.AppendAllText(logPath, logLine + Environment.NewLine);
        }
    }
}
