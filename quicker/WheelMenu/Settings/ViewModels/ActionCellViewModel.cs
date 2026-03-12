namespace WheelMenu.Settings.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using WheelMenu.Settings.Models;

/// <summary>
/// 动作格子ViewModel - 对应4x4网格中的单格
/// </summary>
public partial class ActionCellViewModel : ObservableObject
{
    public ActionCell Model { get; }

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string? _iconPath;

    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>格子序号（0-15）</summary>
    public int CellIndex => Model.CellIndex;

    /// <summary>格子显示序号（1-16）</summary>
    public string DisplayIndex => (CellIndex + 1).ToString();

    // 拖拽状态
    [ObservableProperty]
    private bool _isDragging = false;

    public ActionCellViewModel(ActionCell model)
    {
        Model = model;
        Refresh();
    }

    public void Refresh()
    {
        IsEmpty = Model.IsEmpty;
        DisplayName = Model.IsEmpty ? string.Empty : Model.Name;
        IconPath = Model.IconPath;
    }

    /// <summary>
    /// 更新格子数据
    /// </summary>
    public void Update(ActionType type, string name, string value, string? iconPath = null)
    {
        Model.Type = type;
        Model.Name = name;
        Model.Value = value;
        Model.IconPath = iconPath;
        Refresh();
    }

    /// <summary>
    /// 清空格子
    /// </summary>
    public void Clear()
    {
        Model.Type = ActionType.None;
        Model.Name = string.Empty;
        Model.Value = string.Empty;
        Model.IconPath = null;
        Model.ActionRefId = null;
        Model.ActionParam = null;
        Refresh();
    }
}
