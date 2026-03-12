using CommunityToolkit.Mvvm.ComponentModel;
using WheelMenu.Settings.Models;

namespace WheelMenu.Settings.ViewModels;

public partial class WheelSettingsPageViewModel : ObservableObject
{
    private readonly WheelSettings _model;

    public WheelSettingsPageViewModel(WheelSettings model)
    {
        _model = model;
    }

    public string TriggerKey
    {
        get => _model.TriggerKey;
        set { _model.TriggerKey = value; OnPropertyChanged(); }
    }

    public double Size
    {
        get => _model.Size;
        set { _model.Size = Math.Clamp(value, 50, 300); OnPropertyChanged(); }
    }

    public int TimeoutMs
    {
        get => _model.TimeoutMs;
        set { _model.TimeoutMs = Math.Max(0, value); OnPropertyChanged(); }
    }

    public bool OuterRing16Mode
    {
        get => _model.OuterRing16Mode;
        set { _model.OuterRing16Mode = value; OnPropertyChanged(); }
    }

    public bool HideLabelWhenIcon
    {
        get => _model.HideLabelWhenIcon;
        set { _model.HideLabelWhenIcon = value; OnPropertyChanged(); }
    }

    public bool ConstrainToScreen
    {
        get => _model.ConstrainToScreen;
        set
        {
            _model.ConstrainToScreen = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AutoMoveCursorEnabled));
            if (!value) _model.AutoMoveCursor = false;
            OnPropertyChanged(nameof(AutoMoveCursor));
        }
    }

    public bool AutoMoveCursor
    {
        get => _model.AutoMoveCursor;
        set { _model.AutoMoveCursor = value; OnPropertyChanged(); }
    }

    public bool AutoMoveCursorEnabled => _model.ConstrainToScreen;

    public string RepeatTriggerKey
    {
        get => _model.RepeatTriggerKey;
        set { _model.RepeatTriggerKey = value; OnPropertyChanged(); }
    }
    
    // ===== 圆盘尺寸自定义 =====
    public int DeadZoneRadius
    {
        get => _model.DeadZoneRadius;
        set 
        { 
            // 验证：DeadZoneRadius < Ring1Radius
            if (value < _model.Ring1Radius)
            {
                _model.DeadZoneRadius = value; 
                OnPropertyChanged(); 
            }
        }
    }
    
    public int Ring1Radius
    {
        get => _model.Ring1Radius;
        set 
        { 
            // 验证：Ring1Radius > DeadZoneRadius 且 Ring1Radius < Ring2Radius
            int newVal = Math.Max(_model.DeadZoneRadius + 1, Math.Min(value, _model.Ring2Radius - 1));
            _model.Ring1Radius = newVal; 
            OnPropertyChanged(); 
        }
    }
    
    public int Ring2Radius
    {
        get => _model.Ring2Radius;
        set 
        { 
            // 验证：Ring2Radius > Ring1Radius 且 Ring2Radius < Ring3Radius
            int newVal = Math.Max(_model.Ring1Radius + 1, Math.Min(value, _model.Ring3Radius - 1));
            _model.Ring2Radius = newVal; 
            OnPropertyChanged(); 
        }
    }
    
    public int Ring3Radius
    {
        get => _model.Ring3Radius;
        set 
        { 
            // 验证：Ring3Radius > Ring2Radius
            if (value > _model.Ring2Radius)
            {
                _model.Ring3Radius = value; 
                OnPropertyChanged(); 
            }
        }
    }
}
