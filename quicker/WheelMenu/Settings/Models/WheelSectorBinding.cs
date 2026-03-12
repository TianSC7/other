namespace WheelMenu.Settings.Models;

/// <summary>
/// 轮盘扇区绑定信息
/// Key格式："{ring}_{sectorNumber}"，如 "ring1_1"、"ring2_9"
/// </summary>
public class WheelSectorBinding
{
    /// <summary>扇区序号（1-based 全局序号）</summary>
    public int SectorNumber { get; set; }

    /// <summary>圈层：ring1/ring2/ring3</summary>
    public string Ring { get; set; } = string.Empty;

    /// <summary>方向：N/NE/E/SE/S/SW/W/NW 等</summary>
    public string Direction { get; set; } = string.Empty;

    /// <summary>绑定来源：动作页ID</summary>
    public string? SourcePageId { get; set; }

    /// <summary>绑定来源：动作格索引（0~15）</summary>
    public int? SourceCellIndex { get; set; }

    /// <summary>绑定后缓存的显示名称</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>绑定后缓存的图标路径</summary>
    public string? IconPath { get; set; }

    /// <summary>判断是否为空绑定</summary>
    public bool IsEmpty => string.IsNullOrEmpty(SourcePageId);

    /// <summary>
    /// 获取绑定键
    /// </summary>
    public string GetBindingKey()
    {
        return $"{Ring}_{SectorNumber}";
    }

    /// <summary>
    /// 从绑定键解析
    /// </summary>
    public static WheelSectorBinding? FromBindingKey(string key)
    {
        var parts = key.Split('_');
        if (parts.Length != 2) return null;

        if (!int.TryParse(parts[1], out var sectorNum)) return null;

        return new WheelSectorBinding
        {
            Ring = parts[0],
            SectorNumber = sectorNum
        };
    }
}
