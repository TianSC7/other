namespace WheelMenu.Logic.Context;

using WheelMenu.Config;
using WheelMenu.Renderer;

/// <summary>
/// 场景解析器
/// 负责根据当前活动窗口进程名，解析应使用的场景配置
/// 实现场景优先级逻辑：软件场景覆盖全局场景
/// </summary>
public class SceneResolver
{
    private readonly ForegroundWatcher _watcher;

    /// <summary>当前活动窗口进程名</summary>
    public string? CurrentProcessName => _watcher.CurrentProcessName;

    public SceneResolver(ForegroundWatcher watcher)
    {
        _watcher = watcher;
    }

    /// <summary>
    /// 解析指定扇区的动作配置
    /// 优先级：软件场景 > 全局场景
    /// </summary>
    /// <param name="config">应用配置</param>
    /// <param name="ring">圈层：inner, outer, extended</param>
    /// <param name="sectorIndex">扇区索引（0-7 或 0-15）</param>
    /// <returns>扇区配置，若未设置则返回全局场景的配置</returns>
    public SlotConfig? ResolveSectorConfig(AppConfig config, string ring, int sectorIndex)
    {
        var ringArray = GetRingArray(config, ring);
        if (ringArray == null || sectorIndex < 0 || sectorIndex >= ringArray.Length)
            return null;

        var softwareConfig = ringArray[sectorIndex];

        // 如果软件场景中该扇区未设置动作类型，回退到全局场景
        if (softwareConfig == null || string.IsNullOrEmpty(softwareConfig.ActionType) ||
            softwareConfig.ActionType == "none")
        {
            var globalRingArray = GetRingArray(config.GlobalScene, ring);
            if (globalRingArray != null && sectorIndex < globalRingArray.Length)
            {
                return globalRingArray[sectorIndex];
            }
        }

        return softwareConfig;
    }

    /// <summary>
    /// 构建指定圈的渲染数据数组（已合并场景覆盖）
    /// </summary>
    /// <param name="config">应用配置</param>
    /// <param name="ring">圈层：inner, outer, extended</param>
    /// <param name="count">扇区数量（8 或 16）</param>
    /// <returns>扇区数据数组</returns>
    public WheelSectorData[] BuildSectorDataArray(AppConfig config, string ring, int count)
    {
        var result = new WheelSectorData[count];

        for (int i = 0; i < count; i++)
        {
            var configItem = ResolveSectorConfig(config, ring, i);

            if (configItem != null && !string.IsNullOrEmpty(configItem.ActionType) &&
                configItem.ActionType != "none")
            {
                result[i] = new WheelSectorData
                {
                    HasAction = true,
                    Label = string.IsNullOrEmpty(configItem.Label)
                        ? configItem.ActionValue
                        : configItem.Label,
                    Icon = LoadIcon(configItem.IconPath)
                };
            }
            else
            {
                result[i] = new WheelSectorData { HasAction = false };
            }
        }

        return result;
    }

    /// <summary>
    /// 获取指定场景和圈层的扇区数组
    /// </summary>
    private static SlotConfig[]? GetRingArray(SceneConfig scene, string ring)
    {
        return ring.ToLowerInvariant() switch
        {
            "inner" => scene.InnerRing,
            "outer" => scene.OuterRing,
            "extended" => scene.ExtendedRing,
            _ => null
        };
    }

    /// <summary>
    /// 获取当前场景的指定圈层扇区数组
    /// </summary>
    private SlotConfig[]? GetRingArray(AppConfig config, string ring)
    {
        // 尝试获取软件特定场景
        if (!string.IsNullOrEmpty(CurrentProcessName) &&
            config.Scenes.TryGetValue(CurrentProcessName, out var softwareScene))
        {
            return GetRingArray(softwareScene, ring);
        }

        // 回退到全局场景
        return GetRingArray(config.GlobalScene, ring);
    }

    /// <summary>
    /// 加载图标
    /// </summary>
    private static System.Windows.Media.ImageSource? LoadIcon(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        try
        {
            var img = new System.Windows.Media.Imaging.BitmapImage(new Uri(path));
            img.Freeze();
            return img;
        }
        catch
        {
            return null;
        }
    }
}
