using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WheelMenu.Config;
using WheelMenu.Settings.Models;

namespace WheelMenu.Settings.ViewModels;

public partial class SettingsWindowViewModel : ObservableObject
{
    private readonly ConfigService _configService = ConfigService.Instance;
    private WheelConfig _workingCopy;

    [ObservableProperty]
    private bool _isDirty = false;

    public WheelSettingsPageViewModel WheelSettingsVm { get; }
    public ActionManagerPageViewModel ActionManagerVm { get; }

    [ObservableProperty]
    private object? _currentPage;

    public SettingsWindowViewModel()
    {
        var original = _configService.Load();
        _workingCopy = DeepCopy(original);

        WheelSettingsVm = new WheelSettingsPageViewModel(_workingCopy.Settings);
        ActionManagerVm = new ActionManagerPageViewModel(_workingCopy);

        // 默认显示全局参数页面
        _currentPage = WheelSettingsVm;

        WheelSettingsVm.PropertyChanged += (_, _) => IsDirty = true;
        ActionManagerVm.PropertyChanged += (_, _) => IsDirty = true;
    }

    [RelayCommand]
    private void ShowWheelSettings()
    {
        CurrentPage = WheelSettingsVm;
    }

    [RelayCommand]
    private void ShowActionManager()
    {
        CurrentPage = ActionManagerVm;
    }

    [RelayCommand]
    private void Save()
    {
        _configService.Save(_workingCopy);
        IsDirty = false;
    }

    [RelayCommand]
    private void Cancel()
    {
        _workingCopy = DeepCopy(_configService.Load());
        IsDirty = false;
    }

    public bool CanClose()
    {
        if (!IsDirty) return true;
        var result = System.Windows.MessageBox.Show(
            "有未保存的修改，是否保存？",
            "提示",
            System.Windows.MessageBoxButton.YesNoCancel);
        if (result == System.Windows.MessageBoxResult.Yes)
        {
            Save();
            return true;
        }
        return result == System.Windows.MessageBoxResult.No;
    }

    private static WheelConfig DeepCopy(WheelConfig src)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(src);
        return System.Text.Json.JsonSerializer.Deserialize<WheelConfig>(json)!;
    }
}
