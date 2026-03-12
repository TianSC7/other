namespace WheelMenu.Logic.Actions;

using WheelMenu.Logic.Actions.Executors;

/// <summary>
/// 动作执行器分派器
/// 根据动作类型分派到对应的执行器
/// 使用策略模式和工厂模式实现
/// </summary>
public class ActionExecutor
{
    // 动作类型常量
    public const string ActionTypeNone = "none";
    public const string ActionTypeHotkey = "hotkey";
    public const string ActionTypeSimulateInput = "simulate_input";
    public const string ActionTypePaste = "paste";
    public const string ActionTypeOpen = "open";
    public const string ActionTypeRunAction = "run_action";
    public const string ActionTypeSendText = "send_text";
    public const string ActionTypeDateTime = "datetime";

    private readonly Dictionary<string, IActionExecutor> _executors;

    /// <summary>动作执行完成事件</summary>
    public event EventHandler<ActionExecutedEventArgs>? ActionExecuted;

    /// <summary>动作执行失败事件</summary>
    public event EventHandler<ActionFailedEventArgs>? ActionFailed;

    public ActionExecutor()
    {
        _executors = new Dictionary<string, IActionExecutor>(StringComparer.OrdinalIgnoreCase)
        {
            [ActionTypeHotkey] = new HotkeyExecutor(),
            [ActionTypeSimulateInput] = new SimulateInputExecutor(),
            [ActionTypePaste] = new PasteExecutor(),
            [ActionTypeOpen] = new OpenExecutor(),
            [ActionTypeRunAction] = new RunActionExecutor(),
            [ActionTypeSendText] = new SendTextExecutor(),
            [ActionTypeDateTime] = new DateTimeExecutor(),
        };
    }

    /// <summary>
    /// 执行动作
    /// </summary>
    /// <param name="actionType">动作类型</param>
    /// <param name="actionValue">动作值</param>
    /// <param name="label">显示名称（可选）</param>
    /// <param name="iconPath">图标路径（可选）</param>
    /// <returns>执行结果</returns>
    public ActionExecutionResult Execute(string actionType, string actionValue, string? label = null, string? iconPath = null)
    {
        // none 类型不执行任何动作
        if (string.IsNullOrEmpty(actionType) || actionType == ActionTypeNone)
        {
            return ActionExecutionResult.Skipped("动作类型为空或为 none");
        }

        // 验证动作类型
        if (!_executors.TryGetValue(actionType, out var executor))
        {
            string error = $"未知的动作类型: {actionType}";
            System.Diagnostics.Debug.WriteLine($"[ActionExecutor] {error}");
            ActionFailed?.Invoke(this, new ActionFailedEventArgs(actionType, actionValue, error));
            return ActionExecutionResult.Failed(error);
        }

        // 验证动作值
        if (string.IsNullOrEmpty(actionValue) && actionType != ActionTypeNone)
        {
            string error = $"动作类型 {actionType} 的动作值为空";
            System.Diagnostics.Debug.WriteLine($"[ActionExecutor] {error}");
            ActionFailed?.Invoke(this, new ActionFailedEventArgs(actionType, actionValue, error));
            return ActionExecutionResult.Failed(error);
        }

        try
        {
            executor.Execute(actionType, actionValue, label, iconPath);
            var result = ActionExecutionResult.Success();
            ActionExecuted?.Invoke(this, new ActionExecutedEventArgs(actionType, actionValue, label));
            System.Diagnostics.Debug.WriteLine($"[ActionExecutor] 执行成功: {actionType} - {label ?? actionValue}");
            return result;
        }
        catch (Exception ex)
        {
            string error = $"执行动作失败: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[ActionExecutor] {error}\n{ex.StackTrace}");
            ActionFailed?.Invoke(this, new ActionFailedEventArgs(actionType, actionValue, error, ex));
            return ActionExecutionResult.Failed(error);
        }
    }

    /// <summary>
    /// 检查动作类型是否有效
    /// </summary>
    public static bool IsValidActionType(string actionType)
    {
        if (string.IsNullOrEmpty(actionType))
            return false;

        return actionType.Equals(ActionTypeNone, StringComparison.OrdinalIgnoreCase) ||
               actionType.Equals(ActionTypeHotkey, StringComparison.OrdinalIgnoreCase) ||
               actionType.Equals(ActionTypeSimulateInput, StringComparison.OrdinalIgnoreCase) ||
               actionType.Equals(ActionTypePaste, StringComparison.OrdinalIgnoreCase) ||
               actionType.Equals(ActionTypeOpen, StringComparison.OrdinalIgnoreCase) ||
               actionType.Equals(ActionTypeRunAction, StringComparison.OrdinalIgnoreCase) ||
               actionType.Equals(ActionTypeSendText, StringComparison.OrdinalIgnoreCase) ||
               actionType.Equals(ActionTypeDateTime, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取所有支持的动作类型
    /// </summary>
    public static string[] GetSupportedActionTypes()
    {
        return new[]
        {
            ActionTypeNone,
            ActionTypeHotkey,
            ActionTypeSimulateInput,
            ActionTypePaste,
            ActionTypeOpen,
            ActionTypeRunAction,
            ActionTypeSendText,
            ActionTypeDateTime
        };
    }
}

/// <summary>
/// 动作执行结果
/// </summary>
public class ActionExecutionResult
{
    public bool IsSuccess { get; private set; }
    public string Message { get; private set; } = string.Empty;

    private ActionExecutionResult(bool isSuccess, string message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    public static ActionExecutionResult Success() => new ActionExecutionResult(true, string.Empty);
    public static ActionExecutionResult Failed(string message) => new ActionExecutionResult(false, message);
    public static ActionExecutionResult Skipped(string message) => new ActionExecutionResult(false, message);
}

/// <summary>
/// 动作执行完成事件参数
/// </summary>
public class ActionExecutedEventArgs : EventArgs
{
    public string ActionType { get; }
    public string ActionValue { get; }
    public string? Label { get; }

    public ActionExecutedEventArgs(string actionType, string actionValue, string? label)
    {
        ActionType = actionType;
        ActionValue = actionValue;
        Label = label;
    }
}

/// <summary>
/// 动作执行失败事件参数
/// </summary>
public class ActionFailedEventArgs : EventArgs
{
    public string ActionType { get; }
    public string ActionValue { get; }
    public string ErrorMessage { get; }
    public Exception? Exception { get; }

    public ActionFailedEventArgs(string actionType, string actionValue, string errorMessage, Exception? exception = null)
    {
        ActionType = actionType;
        ActionValue = actionValue;
        ErrorMessage = errorMessage;
        Exception = exception;
    }
}
