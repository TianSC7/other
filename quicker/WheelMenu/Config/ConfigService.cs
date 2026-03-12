namespace WheelMenu.Config;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using WheelMenu.Settings.Models;

public class ConfigService
{
    private const string ConfigFileName = "wheel_config.json";
    private static readonly string ConfigFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WheelMenu",
        ConfigFileName);

    private static ConfigService? _instance;
    private static AppConfig? _config;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>单例实例</summary>
    public static ConfigService Instance => _instance ??= new ConfigService();

    /// <summary>配置保存事件</summary>
    public event Action<WheelConfig>? ConfigSaved;

    /// <summary>加载配置</summary>
    public WheelConfig Load()
    {
        return ConvertToWheelConfig(LoadConfig());
    }

    /// <summary>保存配置</summary>
    public void Save(WheelConfig config)
    {
        var appConfig = ConvertFromWheelConfig(config);
        _config = appConfig;
        SaveConfig();
        ConfigSaved?.Invoke(config);
    }

    /// <summary>获取触发设置</summary>
    public TriggerSettings GetTriggerSettings()
    {
        var config = LoadConfig();
        return new TriggerSettings
        {
            TriggerKey = config.TriggerSettings?.TriggerKey ?? "middle",
            Size = config.TriggerSettings?.Size ?? 120,
            TimeoutMs = config.TriggerSettings?.TimeoutMs ?? 0,
            OuterRing16Mode = config.TriggerSettings?.OuterRing16Mode ?? false,
            HideLabelWhenIcon = config.TriggerSettings?.HideLabelWhenIcon ?? true,
            EdgeConstrainMode = config.TriggerSettings?.EdgeConstrainMode ?? EdgeConstrainMode.Constrain,
            RepeatTriggerKey = config.TriggerSettings?.RepeatTriggerKey ?? "F1",
            // 圆盘尺寸自定义
            DeadZoneRadius = config.TriggerSettings?.DeadZoneRadius ?? 20,
            Ring1Radius = config.TriggerSettings?.Ring1Radius ?? 100,
            Ring2Radius = config.TriggerSettings?.Ring2Radius ?? 200,
            Ring3Radius = config.TriggerSettings?.Ring3Radius ?? 300
        };
    }

    /// <summary>解析扇区动作</summary>
    public SectorActionConfig? ResolveSectorAction(
        WheelConfig config, string? processName, string ring, string direction)
    {
        var appConfig = LoadConfig();
        
        // 优先查找软件特定场景
        SceneConfig? scene = null;
        if (!string.IsNullOrEmpty(processName) && appConfig.Scenes.TryGetValue(processName, out var appScene))
        {
            scene = appScene;
        }
        
        // 如果没找到软件特定场景，使用全局场景
        scene ??= appConfig.GlobalScene;

        var ringConfig = ring switch
        {
            "inner" => scene?.InnerRing,
            "outer" => scene?.OuterRing,
            _ => scene?.ExtendedRing
        };

        if (ringConfig == null) return null;

        var dirs8 = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        int idx = Array.IndexOf(dirs8, direction);
        if (idx < 0 || idx >= ringConfig.Length) return null;

        var slot = ringConfig[idx];
        if (slot == null || string.IsNullOrEmpty(slot.ActionType)) return null;

        return new SectorActionConfig
        {
            Type = ParseActionType(slot.ActionType),
            Value = slot.ActionValue,
            Label = slot.Label,
            IconPath = slot.IconPath
        };
    }

    private static ActionType ParseActionType(string type) => type switch
    {
        "hotkey" => ActionType.Hotkey,
        "simulate_input" => ActionType.SimulateInput,
        "paste" => ActionType.Paste,
        "open" => ActionType.Open,
        "run_action" => ActionType.RunAction,
        "send_text" => ActionType.SendText,
        "datetime" => ActionType.DateTime,
        _ => ActionType.None
    };

    private WheelConfig ConvertToWheelConfig(AppConfig appConfig)
    {
        return new WheelConfig
        {
            Settings = new WheelSettings
            {
                TriggerKey = appConfig.TriggerSettings?.TriggerKey ?? "middle",
                Size = appConfig.TriggerSettings?.Size ?? 120,
                TimeoutMs = appConfig.TriggerSettings?.TimeoutMs ?? 0,
                OuterRing16Mode = appConfig.TriggerSettings?.OuterRing16Mode ?? false,
                HideLabelWhenIcon = appConfig.TriggerSettings?.HideLabelWhenIcon ?? true,
                ConstrainToScreen = appConfig.TriggerSettings?.ConstrainToScreen ?? true,
                AutoMoveCursor = appConfig.TriggerSettings?.AutoMoveCursor ?? false
            }
        };
    }

    private AppConfig ConvertFromWheelConfig(WheelConfig wheelConfig)
    {
        return new AppConfig
        {
            ConfigVersion = "1.0",
            TriggerSettings = new TriggerSettings
            {
                TriggerKey = wheelConfig.Settings.TriggerKey,
                Size = (int)wheelConfig.Settings.Size,
                TimeoutMs = wheelConfig.Settings.TimeoutMs,
                OuterRing16Mode = wheelConfig.Settings.OuterRing16Mode,
                HideLabelWhenIcon = wheelConfig.Settings.HideLabelWhenIcon,
                ConstrainToScreen = wheelConfig.Settings.ConstrainToScreen,
                AutoMoveCursor = wheelConfig.Settings.AutoMoveCursor
            },
            GlobalScene = new SceneConfig
            {
                Name = "全局"
            },
            Scenes = new Dictionary<string, SceneConfig>()
        };
    }

    /// <summary>加载配置（内部方法）</summary>
    public static AppConfig LoadConfig()
    {
        if (!File.Exists(ConfigFilePath))
        {
            // 创建默认配置
            _config = CreateDefaultConfig();
            SaveConfig();
            return _config;
        }

        try
        {
            string json = File.ReadAllText(ConfigFilePath);
            _config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);
            
            // 执行配置迁移
            if (_config != null)
            {
                MigrateConfig(_config);
            }
            
            return _config ?? CreateDefaultConfig();
        }
        catch
        {
            // 读取失败时返回默认配置
            _config = CreateDefaultConfig();
            return _config;
        }
    }

    /// <summary>配置迁移</summary>
    private static void MigrateConfig(AppConfig config)
    {
        string? version = config.ConfigVersion;
        
        if (string.IsNullOrEmpty(version))
        {
            // 旧版本：添加版本字段
            config.ConfigVersion = "1.0";
        }
        
        // 后续版本迁移可在此添加...
        // if (config.ConfigVersion == "1.0") { ... 迁移到 1.1 }
    }

    /// <summary>保存配置</summary>
    public static void SaveConfig()
    {
        if (_config == null) return;

        try
        {
            string json = JsonSerializer.Serialize(_config, _jsonOptions);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch
        {
            // TODO: 记录日志
        }
    }

    /// <summary>创建默认配置</summary>
    private static AppConfig CreateDefaultConfig()
    {
        return new AppConfig
        {
            TriggerSettings = new TriggerSettings(),
            GlobalScene = new SceneConfig
            {
                Name = "全局",
                InnerRing = new SlotConfig[8],
                OuterRing = new SlotConfig[8],
                ExtendedRing = new SlotConfig[8]
            },
            Scenes = new Dictionary<string, SceneConfig>()
        };
    }
}
