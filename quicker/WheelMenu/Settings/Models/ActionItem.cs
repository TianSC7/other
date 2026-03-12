namespace WheelMenu.Settings.Models;

/// <summary>
/// 动作项 - 对应轮盘上的一个动作格子
/// 替代旧的 SectorActionConfig
/// </summary>
public class ActionItem
{
    /// <summary>动作唯一ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>动作类型</summary>
    public ActionType Type { get; set; } = ActionType.None;

    /// <summary>动作值（快捷键文本、输入内容、文件路径等）</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>显示名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>图标路径</summary>
    public string? IconPath { get; set; } = null;

    /// <summary>动作引用ID（用于RunAction类型）</summary>
    public string? ActionRefId { get; set; } = null;

    /// <summary>动作参数</summary>
    public string? ActionParam { get; set; } = null;

    /// <summary>
    /// 从旧的 SectorActionConfig 转换
    /// </summary>
    public static ActionItem FromSectorActionConfig(SectorActionConfig? config)
    {
        if (config == null) return new ActionItem();
        
        return new ActionItem
        {
            Id = Guid.NewGuid().ToString(),
            Type = config.Type,
            Value = config.Value,
            Name = config.Label,
            IconPath = config.IconPath,
            ActionRefId = config.ActionRefId,
            ActionParam = config.ActionParam
        };
    }

    /// <summary>
    /// 转换为旧的 SectorActionConfig（用于兼容）
    /// </summary>
    public SectorActionConfig ToSectorActionConfig()
    {
        return new SectorActionConfig
        {
            Type = Type,
            Value = Value,
            Label = Name,
            IconPath = IconPath,
            ActionRefId = ActionRefId,
            ActionParam = ActionParam
        };
    }
}
