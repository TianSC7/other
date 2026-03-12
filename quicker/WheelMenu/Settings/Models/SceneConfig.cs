namespace WheelMenu.Settings.Models;

/// <summary>
/// 场景配置 - 对应一个软件或全局设置
/// 根据SOP终版重写，支持4x4动作页和轮盘绑定
/// </summary>
public class SceneConfig
{
    /// <summary>场景名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>进程名，null表示全局场景</summary>
    public string? Process { get; set; } = null;

    /// <summary>该场景下的所有动作页</summary>
    public List<ActionPage> ActionPages { get; set; } = new();

    /// <summary>
    /// 扇区绑定表。Key = "ring1_1"..."ring3_24"
    /// </summary>
    public Dictionary<string, WheelSectorBinding> Bindings { get; set; } = new();

    /// <summary>是否在此软件下禁用轮盘菜单</summary>
    public bool DisableWheel { get; set; } = false;

    /// <summary>附加通用动作页ID列表（引用global场景中的动作页）</summary>
    public List<string> AttachedCommonPageIds { get; set; } = new();

    /// <summary>是否自动返回第一页</summary>
    public bool AutoReturnToFirstPage { get; set; } = true;

    // ========== 旧模型属性 (向后兼容) ==========

    /// <summary>旧版：内圈配置（保留兼容）</summary>
    public Dictionary<string, SectorActionConfig?> InnerRing { get; set; } = new();

    /// <summary>旧版：外圈配置（保留兼容）</summary>
    public Dictionary<string, SectorActionConfig?> OuterRing { get; set; } = new();

    /// <summary>旧版：扩展圈配置（保留兼容）</summary>
    public Dictionary<string, SectorActionConfig?> ExtendedRing { get; set; } = new();

    /// <summary>
    /// 创建默认场景配置
    /// </summary>
    public static SceneConfig CreateDefault(string name, string? process = null)
    {
        var scene = new SceneConfig
        {
            Name = name,
            Process = process
        };

        // 创建默认动作页
        var defaultPage = new ActionPage("默认动作页");
        scene.ActionPages.Add(defaultPage);

        // 创建默认内圈绑定（8格）
        var dirs8 = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        for (int i = 0; i < 8; i++)
        {
            var key = $"ring1_{i + 1}";
            scene.Bindings[key] = new WheelSectorBinding
            {
                SectorNumber = i + 1,
                Ring = "ring1",
                Direction = dirs8[i],
                SourcePageId = defaultPage.Id,
                SourceCellIndex = i,
                DisplayName = $"动作{i + 1}"
            };
        }

        // 初始化旧版配置
        scene.InnerRing = new Dictionary<string, SectorActionConfig?>();
        scene.OuterRing = new Dictionary<string, SectorActionConfig?>();
        scene.ExtendedRing = new Dictionary<string, SectorActionConfig?>();

        return scene;
    }

    /// <summary>
    /// 判断是否为全局场景
    /// </summary>
    public bool IsGlobal => string.IsNullOrEmpty(Process);

    /// <summary>
    /// 获取显示名称
    /// </summary>
    public string DisplayName => IsGlobal ? "全局" : Name;

    /// <summary>
    /// 从旧配置迁移到新模型
    /// </summary>
    public void MigrateToNewModel()
    {
        if (ActionPages.Count > 0) return; // 已经迁移过

        // 创建默认动作页
        var defaultPage = new ActionPage("从配置迁移");
        ActionPages.Add(defaultPage);

        // 从InnerRing迁移
        var directionMap = new Dictionary<string, int>
        {
            { "N", 0 }, { "NE", 1 }, { "E", 2 }, { "SE", 3 },
            { "S", 4 }, { "SW", 5 }, { "W", 6 }, { "NW", 7 }
        };

        foreach (var kvp in InnerRing)
        {
            if (kvp.Value != null && directionMap.TryGetValue(kvp.Key, out var cellIndex))
            {
                var cell = ActionCell.FromActionItem(
                    ActionItem.FromSectorActionConfig(kvp.Value), cellIndex);
                defaultPage.Cells[cellIndex] = cell;

                // 创建绑定
                var bindingKey = $"ring1_{cellIndex + 1}";
                Bindings[bindingKey] = new WheelSectorBinding
                {
                    SectorNumber = cellIndex + 1,
                    Ring = "ring1",
                    Direction = kvp.Key,
                    SourcePageId = defaultPage.Id,
                    SourceCellIndex = cellIndex,
                    DisplayName = cell.Name,
                    IconPath = cell.IconPath
                };
            }
        }
    }
}
