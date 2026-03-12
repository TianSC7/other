namespace WheelMenu.Settings.Models;

/// <summary>
/// 动作格子 - 4x4网格中的单格
/// </summary>
public class ActionCell
{
    /// <summary>格子在4x4网格中的位置（0-based，0~15）</summary>
    public int CellIndex { get; set; }

    /// <summary>动作类型</summary>
    public ActionType Type { get; set; } = ActionType.None;

    /// <summary>显示名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>动作值（快捷键文本、输入内容、文件路径等）</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>图标路径</summary>
    public string? IconPath { get; set; }

    /// <summary>RunAction专用：引用的动作ID</summary>
    public string? ActionRefId { get; set; }

    /// <summary>RunAction专用：动作参数</summary>
    public string? ActionParam { get; set; }

    /// <summary>判断格子是否为空</summary>
    public bool IsEmpty => Type == ActionType.None;

    /// <summary>
    /// 从ActionItem转换（用于迁移旧数据）
    /// </summary>
    public static ActionCell FromActionItem(ActionItem item, int cellIndex)
    {
        return new ActionCell
        {
            CellIndex = cellIndex,
            Type = item.Type,
            Name = item.Name,
            Value = item.Value,
            IconPath = item.IconPath,
            ActionRefId = item.ActionRefId,
            ActionParam = item.ActionParam
        };
    }

    /// <summary>
    /// 转换为ActionItem（用于兼容旧逻辑）
    /// </summary>
    public ActionItem ToActionItem()
    {
        return new ActionItem
        {
            Id = Guid.NewGuid().ToString(),
            Type = Type,
            Name = Name,
            Value = Value,
            IconPath = IconPath,
            ActionRefId = ActionRefId,
            ActionParam = ActionParam
        };
    }
}
