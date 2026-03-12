using System.Text.Json.Serialization;

namespace WheelMenu.Settings.Models;

public enum ActionType
{
    None,
    Hotkey,
    SimulateInput,
    Paste,
    Open,
    RunAction,
    SendText,
    DateTime
}

public class SectorActionConfig
{
    public ActionType Type { get; set; } = ActionType.None;
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? IconPath { get; set; } = null;
    public string? ActionRefId { get; set; } = null;
    public string? ActionParam { get; set; } = null;
}
