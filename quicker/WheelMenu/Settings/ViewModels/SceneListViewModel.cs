using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WheelMenu.Config;
using WheelMenu.Settings.Models;
using WheelMenu.Windows;

namespace WheelMenu.Settings.ViewModels;

public partial class SceneListViewModel : ObservableObject
{
    private readonly WheelConfig _config;

    public ObservableCollection<SceneItemViewModel> Scenes { get; } = new();

    [ObservableProperty]
    private SceneItemViewModel? _selectedScene;

    public SceneListViewModel(WheelConfig config)
    {
        _config = config;
        RefreshScenes();
    }

    private void RefreshScenes()
    {
        Scenes.Clear();
        Scenes.Add(new SceneItemViewModel("global", _config.Scenes["global"], isGlobal: true));
        foreach (var (key, scene) in _config.Scenes)
        {
            if (key == "global") continue;
            Scenes.Add(new SceneItemViewModel(key, scene, isGlobal: false));
        }
        SelectedScene = Scenes.FirstOrDefault();
    }

    [RelayCommand]
    private void AddScene()
    {
        var dlg = new AddSceneDialog();
        if (dlg.ShowDialog() != true) return;
        var processName = dlg.ProcessName.ToLowerInvariant().Trim();
        if (string.IsNullOrEmpty(processName) || _config.Scenes.ContainsKey(processName))
            return;
        _config.Scenes[processName] = new WheelMenu.Settings.Models.SceneConfig
        {
            Name = dlg.DisplayName,
            Process = processName
        };
        RefreshScenes();
        SelectedScene = Scenes.FirstOrDefault(s => s.Key == processName);
    }

    [RelayCommand]
    private void DeleteScene(SceneItemViewModel item)
    {
        if (item.IsGlobal) return;
        var result = System.Windows.MessageBox.Show(
            $"确定删除场景\"{item.Scene.Name}\"？", "确认删除",
            System.Windows.MessageBoxButton.YesNo);
        if (result != System.Windows.MessageBoxResult.Yes) return;
        _config.Scenes.Remove(item.Key);
        RefreshScenes();
    }
}

public class SceneItemViewModel(string key, WheelMenu.Settings.Models.SceneConfig scene, bool isGlobal)
{
    public string Key { get; } = key;
    public WheelMenu.Settings.Models.SceneConfig Scene { get; } = scene;
    public bool IsGlobal { get; } = isGlobal;
    public string DisplayName => IsGlobal ? "🌐 全局" : $"📄 {Scene.Name}";
}
