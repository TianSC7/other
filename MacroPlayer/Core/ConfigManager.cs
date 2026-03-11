using System.Text.Json;
using MacroPlayer.Models;

namespace MacroPlayer.Core;

/// <summary>
/// 配置管理器，负责加载和保存配置
/// </summary>
public static class ConfigManager
{
    private const string ConfigFileName = "config.json";

    /// <summary>
    /// 当前配置
    /// </summary>
    public static AppSettings Current { get; private set; } = new();

    /// <summary>
    /// 加载配置文件
    /// </summary>
    public static void Load()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                Current = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                Logger.Info($"配置加载成功，共 {Current.Macros.Count} 个宏");
            }
            catch (Exception ex)
            {
                Logger.Error($"配置加载失败：{ex.Message}");
                Current = new();
            }
        }
        else
        {
            Logger.Info("配置文件不存在，使用默认配置");
            Current = new();
        }
    }

    /// <summary>
    /// 保存配置文件
    /// </summary>
    public static void Save()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
        var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(configPath, json);
        Logger.Info("配置已保存");
    }
}
