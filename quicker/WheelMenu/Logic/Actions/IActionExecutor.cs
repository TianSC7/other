namespace WheelMenu.Logic.Actions;

/// <summary>
/// 动作执行器接口
/// </summary>
public interface IActionExecutor
{
    /// <summary>
    /// 执行动作
    /// </summary>
    /// <param name="actionType">动作类型</param>
    /// <param name="actionValue">动作值</param>
    /// <param name="label">显示名称（可选）</param>
    /// <param name="iconPath">图标路径（可选）</param>
    void Execute(string actionType, string actionValue, string? label = null, string? iconPath = null);
}
