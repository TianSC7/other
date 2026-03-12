namespace WheelMenu.Logic.Actions.Executors;

using WheelMenu.Config;

/// <summary>
/// 运行动作执行器 - 运行已配置的动作引用
/// 支持参数传递：同一动作通过不同扇区传入不同参数
/// 注意：Quicker 动作系统需要更复杂的集成，当前为占位实现
/// </summary>
public class RunActionExecutor : IActionExecutor
{
    private readonly ActionExecutor _executor;

    public RunActionExecutor()
    {
        _executor = new ActionExecutor();
    }

    public void Execute(string actionType, string actionValue, string? label = null, string? iconPath = null)
    {
        if (string.IsNullOrEmpty(actionValue))
        {
            ShowWarning("动作引用为空，请重新绑定动作。");
            return;
        }

        // actionValue 格式: "动作ID|参数" 或仅 "动作ID"
        string[] parts = actionValue.Split('|', 2);
        string actionId = parts[0];
        string? param = parts.Length > 1 ? parts[1] : null;

        // 加载配置
        var config = ConfigService.LoadConfig();
        var targetAction = FindActionById(config, actionId);

        if (targetAction == null)
        {
            ShowWarning($"找不到引用的动作（ID: {actionId}），原始动作可能已被删除。\n请重新绑定。");
            return;
        }

        // 参数传递：如果有参数，覆盖目标动作的值
        SlotConfig execAction;
        if (!string.IsNullOrEmpty(param))
        {
            execAction = new SlotConfig
            {
                ActionType = targetAction.ActionType,
                ActionValue = param,
                Label = targetAction.Label,
                IconPath = targetAction.IconPath
            };
        }
        else
        {
            execAction = targetAction;
        }

        System.Diagnostics.Debug.WriteLine($"[RunActionExecutor] 执行动作引用: {actionId} - {label}, 参数: {param}");

        // 使用主执行器执行动作
        _executor.Execute(execAction.ActionType, execAction.ActionValue, label, iconPath);
    }

    /// <summary>
    /// 在配置中查找目标动作（通过 ID 或 Label）
    /// </summary>
    private static SlotConfig? FindActionById(AppConfig config, string id)
    {
        // 搜索全局场景
        var action = FindActionInScene(config.GlobalScene, id);
        if (action != null) return action;

        // 搜索所有软件场景
        foreach (var scene in config.Scenes.Values)
        {
            action = FindActionInScene(scene, id);
            if (action != null) return action;
        }

        return null;
    }

    /// <summary>
    /// 在单个场景中查找动作
    /// </summary>
    private static SlotConfig? FindActionInScene(SceneConfig scene, string id)
    {
        foreach (var ring in new[] { scene.InnerRing, scene.OuterRing, scene.ExtendedRing })
        {
            if (ring == null) continue;
            foreach (var slot in ring)
            {
                if (slot != null && !string.IsNullOrEmpty(slot.Label) && slot.Label == id)
                    return slot;
            }
        }
        return null;
    }

    private static void ShowWarning(string message)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            System.Windows.MessageBox.Show(message, "动作执行",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        });
    }
}
