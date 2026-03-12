namespace WheelMenu.Settings.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using WheelMenu.Settings.Models;

/// <summary>
/// 轮盘扇区ViewModel - 轮盘上的单个扇区
/// </summary>
public partial class WheelSectorViewModel : ObservableObject
{
    /// <summary>扇区序号（1-based 全局序号）</summary>
    public int SectorNumber { get; }

    /// <summary>圈层：ring1/ring2/ring3</summary>
    public string Ring { get; }

    /// <summary>方向：N/NE/E/SE/S/SW/W/NW 等</summary>
    public string Direction { get; }

    /// <summary>显示名称（序号+方向）</summary>
    public string DisplayLabel => $"{SectorNumber}";

    [ObservableProperty]
    private bool _isSelected = false;

    [ObservableProperty]
    private bool _isDragTarget = false;

    [ObservableProperty]
    private bool _isBound = false;

    [ObservableProperty]
    private string _boundName = string.Empty;

    [ObservableProperty]
    private string? _boundIconPath;

    /// <summary>绑定源页面ID</summary>
    public string? SourcePageId { get; private set; }

    /// <summary>绑定源格子索引</summary>
    public int? SourceCellIndex { get; private set; }

    public WheelSectorViewModel(int sectorNumber, string ring, string direction)
    {
        SectorNumber = sectorNumber;
        Ring = ring;
        Direction = direction;
    }

    /// <summary>
    /// 应用绑定信息
    /// </summary>
    public void ApplyBinding(WheelSectorBinding? binding)
    {
        if (binding == null || binding.IsEmpty)
        {
            IsBound = false;
            BoundName = string.Empty;
            BoundIconPath = null;
            SourcePageId = null;
            SourceCellIndex = null;
        }
        else
        {
            IsBound = true;
            BoundName = binding.DisplayName;
            BoundIconPath = binding.IconPath;
            SourcePageId = binding.SourcePageId;
            SourceCellIndex = binding.SourceCellIndex;
        }
    }

    /// <summary>
    /// 清除绑定
    /// </summary>
    public void ClearBinding()
    {
        IsBound = false;
        BoundName = string.Empty;
        BoundIconPath = null;
        SourcePageId = null;
        SourceCellIndex = null;
    }

    /// <summary>
    /// 创建绑定
    /// </summary>
    public WheelSectorBinding CreateBinding(ActionPageViewModel page, ActionCellViewModel cell)
    {
        SourcePageId = page.Id;
        SourceCellIndex = cell.CellIndex;
        IsBound = true;
        BoundName = cell.DisplayName;
        BoundIconPath = cell.IconPath;

        return new WheelSectorBinding
        {
            SectorNumber = SectorNumber,
            Ring = Ring,
            Direction = Direction,
            SourcePageId = page.Id,
            SourceCellIndex = cell.CellIndex,
            DisplayName = cell.DisplayName,
            IconPath = cell.IconPath
        };
    }
}
