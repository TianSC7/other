using System.Collections.ObjectModel;
using System.Windows;
using WheelMenu.Settings.Models;
using WheelMenu.Settings.ViewModels;

namespace WheelMenu.Settings.Controls;

public partial class SectorEditDialog : Window
{
    public ObservableCollection<ActionTypeOption> ActionTypeOptions { get; } = new()
    {
        new ActionTypeOption(ActionType.None, "无"),
        new ActionTypeOption(ActionType.Hotkey, "发送快捷键"),
        new ActionTypeOption(ActionType.SimulateInput, "模拟输入"),
        new ActionTypeOption(ActionType.Paste, "粘贴内容"),
        new ActionTypeOption(ActionType.Open, "打开文件/URL"),
        new ActionTypeOption(ActionType.RunAction, "运行动作引用"),
        new ActionTypeOption(ActionType.SendText, "发送文本"),
        new ActionTypeOption(ActionType.DateTime, "插入日期时间")
    };

    public SectorEditViewModel ViewModel { get; set; } = new();

    public SectorEditDialog()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    public bool ShowDialog(string ring, int sector, SectorActionConfig? existingAction)
    {
        ViewModel.PositionLabel = $"位置: {ring} - {sector}";
        ViewModel.LoadFrom(existingAction);
        return ShowDialog() == true;
    }
}

public class ActionTypeOption(ActionType type, string displayName)
{
    public ActionType Type { get; } = type;
    public string DisplayName { get; } = displayName;
}
