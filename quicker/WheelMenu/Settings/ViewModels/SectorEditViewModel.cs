using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WheelMenu.Settings.Models;

namespace WheelMenu.Settings.ViewModels;

public partial class SectorEditViewModel : ObservableObject
{
    [ObservableProperty]
    private ActionType _selectedActionType = ActionType.None;

    [ObservableProperty]
    private string _hotkeyValue = string.Empty;

    [ObservableProperty]
    private string _textValue = string.Empty;

    [ObservableProperty]
    private string _openValue = string.Empty;

    [ObservableProperty]
    private string _actionRefId = string.Empty;

    [ObservableProperty]
    private string _actionParam = string.Empty;

    [ObservableProperty]
    private string _dateTimeFormat = "{0:yyyy-MM-dd}";

    [ObservableProperty]
    private string _customLabel = string.Empty;

    [ObservableProperty]
    private string _positionLabel = string.Empty;

    [ObservableProperty]
    private bool _isInherited = false;

    [ObservableProperty]
    private System.Windows.Media.ImageSource? _iconPreview = null;

    public string DateTimePreview =>
        string.Format(DateTimeFormat.Replace("{0:", "{0:"), DateTime.Now);

    public bool IsHotkeyType => SelectedActionType == ActionType.Hotkey;
    public bool IsTextType => SelectedActionType is ActionType.SimulateInput
                            or ActionType.Paste or ActionType.SendText;
    public bool IsOpenType => SelectedActionType == ActionType.Open;
    public bool IsRunActionType => SelectedActionType == ActionType.RunAction;
    public bool IsDateTimeType => SelectedActionType == ActionType.DateTime;

    public string SelectedActionDisplay => SelectedActionType switch
    {
        ActionType.None => "无",
        ActionType.Hotkey => "发送快捷键",
        ActionType.SimulateInput => "模拟输入",
        ActionType.Paste => "粘贴内容",
        ActionType.Open => "打开文件/URL",
        ActionType.RunAction => "运行动作引用",
        ActionType.SendText => "发送文本",
        ActionType.DateTime => "插入日期时间",
        _ => ""
    };

    partial void OnSelectedActionTypeChanged(ActionType value)
    {
        OnPropertyChanged(nameof(IsHotkeyType));
        OnPropertyChanged(nameof(IsTextType));
        OnPropertyChanged(nameof(IsOpenType));
        OnPropertyChanged(nameof(IsRunActionType));
        OnPropertyChanged(nameof(IsDateTimeType));
        OnPropertyChanged(nameof(SelectedActionDisplay));
    }

    partial void OnDateTimeFormatChanged(string value) =>
        OnPropertyChanged(nameof(DateTimePreview));

    [RelayCommand]
    private void Browse()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog();
        if (dlg.ShowDialog() == true)
            OpenValue = dlg.FileName;
    }

    [RelayCommand]
    private void ChangeIcon()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog();
        dlg.Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*";
        if (dlg.ShowDialog() == true)
        {
            IconPreview = new System.Windows.Media.Imaging.BitmapImage(new Uri(dlg.FileName));
        }
    }

    [RelayCommand]
    private void ClearIcon()
    {
        IconPreview = null;
    }

    [RelayCommand]
    private void InsertFormat(string format)
    {
        DateTimeFormat = format;
    }

    [RelayCommand]
    private void SelectAction()
    {
        // TODO: 实现动作选择对话框
    }

    [RelayCommand]
    private void Ok()
    {
        // 由对话框处理关闭
    }

    [RelayCommand]
    private void Cancel()
    {
        // 由对话框处理关闭
    }

    public SectorActionConfig ToConfig() => new()
    {
        Type = SelectedActionType,
        Value = SelectedActionType switch
        {
            ActionType.Hotkey => HotkeyValue,
            ActionType.SimulateInput => TextValue,
            ActionType.Paste => TextValue,
            ActionType.Open => OpenValue,
            ActionType.SendText => TextValue,
            ActionType.DateTime => DateTimeFormat,
            _ => string.Empty
        },
        Label = CustomLabel,
        ActionRefId = ActionRefId,
        ActionParam = ActionParam
    };

    public void LoadFrom(SectorActionConfig? config)
    {
        if (config == null) return;
        SelectedActionType = config.Type;
        CustomLabel = config.Label;
        ActionRefId = config.ActionRefId ?? string.Empty;
        ActionParam = config.ActionParam ?? string.Empty;
        switch (config.Type)
        {
            case ActionType.Hotkey:
                HotkeyValue = config.Value;
                break;
            case ActionType.SimulateInput:
            case ActionType.Paste:
            case ActionType.SendText:
                TextValue = config.Value;
                break;
            case ActionType.Open:
                OpenValue = config.Value;
                break;
            case ActionType.DateTime:
                DateTimeFormat = config.Value;
                break;
        }
    }

    [RelayCommand]
    public void Clear()
    {
        SelectedActionType = ActionType.None;
        HotkeyValue = string.Empty;
        TextValue = string.Empty;
        OpenValue = string.Empty;
        ActionRefId = string.Empty;
        ActionParam = string.Empty;
        DateTimeFormat = "{0:yyyy-MM-dd}";
        CustomLabel = string.Empty;
        PositionLabel = string.Empty;
        IsInherited = false;
        IconPreview = null;
    }
}
