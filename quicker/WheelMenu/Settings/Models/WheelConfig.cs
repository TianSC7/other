using System.Text.Json.Serialization;

namespace WheelMenu.Settings.Models;

public class WheelConfig
{
    public int Version { get; set; } = 1;
    public WheelSettings Settings { get; set; } = new();
    public Dictionary<string, SceneConfig> Scenes { get; set; } = new()
    {
        ["global"] = new SceneConfig { Name = "全局", Process = null }
    };
}
