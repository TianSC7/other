namespace WheelMenu.Settings.Models;

/// <summary>
/// 动作页 - 4x4网格（16格/页）
/// 对应轮盘内圈的一个扇区所绑定的页面
/// </summary>
public class ActionPage
{
    /// <summary>动作页唯一ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>动作页显示名称（如 "AutoCAD Application"）</summary>
    public string Name { get; set; } = "新动作页";

    /// <summary>
    /// 固定16格（4x4），索引0~15对应位置从左到右、从上到下
    /// </summary>
    public ActionCell[] Cells { get; set; } =
        Enumerable.Range(0, 16)
                  .Select(i => new ActionCell { CellIndex = i })
                  .ToArray();

    /// <summary>
    /// 默认构造函数，创建一个包含16个空动作格的页面
    /// </summary>
    public ActionPage()
    {
    }

    /// <summary>
    /// 创建指定名称的动作页
    /// </summary>
    public ActionPage(string name) : this()
    {
        Name = name;
    }

    /// <summary>
    /// 从旧的动作页配置创建新格式的动作页（迁移）
    /// </summary>
    public static ActionPage FromOldActionPage(ActionPage oldPage)
    {
        var page = new ActionPage
        {
            Id = oldPage.Id,
            Name = oldPage.Name
        };

        // 将旧的动作项迁移到新的格子
        // 旧格式是List<ActionItem>，最多8个
        // 新格式是ActionCell[]，固定16个
        for (int i = 0; i < 16; i++)
        {
            if (i < oldPage.Cells.Length)
            {
                page.Cells[i] = oldPage.Cells[i];
            }
            else
            {
                page.Cells[i] = new ActionCell { CellIndex = i };
            }
        }

        return page;
    }

    /// <summary>
    /// 从旧的List<ActionItem>格式迁移
    /// </summary>
    public static ActionPage FromActionItems(List<ActionItem> actions, string name = "从配置导入")
    {
        var page = new ActionPage
        {
            Id = Guid.NewGuid().ToString(),
            Name = name
        };

        for (int i = 0; i < 16; i++)
        {
            if (i < actions.Count && actions[i] != null)
            {
                page.Cells[i] = ActionCell.FromActionItem(actions[i], i);
            }
            else
            {
                page.Cells[i] = new ActionCell { CellIndex = i };
            }
        }

        return page;
    }
}
