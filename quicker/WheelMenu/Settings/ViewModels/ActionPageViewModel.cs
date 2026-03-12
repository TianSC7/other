namespace WheelMenu.Settings.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WheelMenu.Settings.Models;

/// <summary>
/// 动作页ViewModel - 对应4x4网格（16格）
/// </summary>
public partial class ActionPageViewModel : ObservableObject
{
    public ActionPage Model { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>16个格子</summary>
    public ObservableCollection<ActionCellViewModel> Cells { get; } = new();

    public string Id => Model.Id;

    public ActionPageViewModel(ActionPage model)
    {
        Model = model;
        _name = model.Name;

        // 初始化16个格子
        foreach (var cell in model.Cells)
        {
            Cells.Add(new ActionCellViewModel(cell));
        }
    }

    partial void OnNameChanged(string value)
    {
        Model.Name = value;
    }

    /// <summary>
    /// 刷新所有格子
    /// </summary>
    public void RefreshAll()
    {
        foreach (var cell in Cells)
        {
            cell.Refresh();
        }
    }

    /// <summary>
    /// 获取指定索引的格子
    /// </summary>
    public ActionCellViewModel? GetCell(int index)
    {
        if (index >= 0 && index < Cells.Count)
            return Cells[index];
        return null;
    }
}
