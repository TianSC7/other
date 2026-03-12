namespace WheelMenu.Settings.Models;

/// <summary>
/// 轮盘方向枚举
/// </summary>
public enum WheelDirection
{
    N,    // 北（正上）
    NE,   // 东北
    E,    // 东（正右）
    SE,   // 东南
    S,    // 南（正下）
    SW,   // 西南
    W,    // 西（正左）
    NW    // 西北
}

/// <summary>
/// 轮盘圈层枚举
/// </summary>
public enum WheelRing
{
    /// <summary>内圈</summary>
    Inner,
    /// <summary>外圈</summary>
    Outer,
    /// <summary>扩展圈</summary>
    Extended
}

/// <summary>
/// 轮盘扇区绑定 - 将轮盘上的某个扇区绑定到动作页或单个动作
/// </summary>
public class WheelBinding
{
    /// <summary>方向：N/NE/E/SE/S/SW/W/NW（内圈8格）</summary>
    public WheelDirection Direction { get; set; } = WheelDirection.N;

    /// <summary>圈层：inner / outer / extended</summary>
    public WheelRing Ring { get; set; } = WheelRing.Inner;

    /// <summary>
    /// 绑定目标：
    /// 若 ActionPageId 有值 → 此扇区触发整个动作页（进入子菜单）
    /// 若 ActionItemId 有值 → 此扇区直接执行单个动作
    /// </summary>
    public string? ActionPageId { get; set; } = null;

    /// <summary>绑定的动作项ID</summary>
    public string? ActionItemId { get; set; } = null;

    /// <summary>
    /// 获取绑定键，用于在字典中作为Key
    /// </summary>
    public string GetBindingKey()
    {
        return $"{Ring.ToString().ToLower()}_{Direction.ToString()}";
    }

    /// <summary>
    /// 从绑定键解析WheelBinding
    /// </summary>
    public static WheelBinding? FromBindingKey(string key)
    {
        var parts = key.Split('_');
        if (parts.Length != 2) return null;

        if (!Enum.TryParse<WheelRing>(parts[0], true, out var ring)) return null;
        if (!Enum.TryParse<WheelDirection>(parts[1], true, out var direction)) return null;

        return new WheelBinding
        {
            Ring = ring,
            Direction = direction
        };
    }

    /// <summary>
    /// 创建默认的内圈绑定（8个方向）
    /// </summary>
    public static List<WheelBinding> CreateDefaultInnerRing()
    {
        var bindings = new List<WheelBinding>();
        foreach (WheelDirection dir in Enum.GetValues<WheelDirection>())
        {
            bindings.Add(new WheelBinding
            {
                Ring = WheelRing.Inner,
                Direction = dir,
                ActionPageId = null,
                ActionItemId = null
            });
        }
        return bindings;
    }
}
