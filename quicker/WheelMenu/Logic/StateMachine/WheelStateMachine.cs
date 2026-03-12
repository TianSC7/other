using System.Windows;
using System.Windows.Threading;
using WheelMenu.Config;
using WheelMenu.Logic.Actions;
using WheelMenu.Logic.Context;
using WheelMenu.Logic.MouseHook;
using WheelMenu.Logic.Win32;
using WheelMenu.Renderer;
using WheelMenu.Settings.Models;
using WheelMenu.Windows;

namespace WheelMenu.Logic.StateMachine;

public class WheelStateMachine : IDisposable
{
    private readonly LowLevelMouseHook _hook;
    private readonly WheelAnimator _animator;
    private readonly WheelCanvas _canvas;
    private readonly WheelWindow _window;
    private readonly IActionExecutor _executor;
    private readonly SceneResolver _sceneResolver;
    private readonly ConfigService _configService;

    private WheelState _state = WheelState.Idle;
    private System.Windows.Point _wheelCenter = new(0, 0);
    private string _hoverRing = string.Empty;
    private int _hoverSector = -1;
    private bool _triggerKeyDown = false;
    private System.Windows.Point _keyDownPos;
    private const double MoveThreshold = 5.0;
    private readonly DispatcherTimer _timeoutTimer = new();

    public WheelStateMachine(
        LowLevelMouseHook hook, WheelAnimator animator,
        WheelCanvas canvas, WheelWindow window,
        IActionExecutor executor, SceneResolver sceneResolver,
        ConfigService configService)
    {
        _hook = hook;
        _animator = animator;
        _canvas = canvas;
        _window = window;
        _executor = executor;
        _sceneResolver = sceneResolver;
        _configService = configService;

        _hook.MouseEvent += OnMouseEvent;
        _timeoutTimer.Tick += OnTimeout;
        _configService.ConfigSaved += OnConfigSaved;
    }

    private void OnConfigSaved(WheelConfig config)
    {
        if (_state == WheelState.Idle)
            ReloadConfig(config);
    }

    private void ReloadConfig(WheelConfig config)
    {
        var settings = config.Settings;
        _timeoutTimer.Interval = settings.TimeoutMs > 0
            ? TimeSpan.FromMilliseconds(settings.TimeoutMs)
            : TimeSpan.MaxValue;
        _canvas.SetDisplayOptions(settings.HideLabelWhenIcon, settings.OuterRing16Mode);
    }

    private void OnMouseEvent(object? sender, MouseHookEventArgs e)
    {
        var config = _configService.Load();
        var setting = config.Settings;

        bool isTriggerDown = IsTriggerKeyDown(e, setting.TriggerKey);
        bool isTriggerUp = IsTriggerKeyUp(e, setting.TriggerKey);

        switch (_state)
        {
            case WheelState.Idle:
                if (isTriggerDown)
                {
                    _triggerKeyDown = true;
                    _keyDownPos = new System.Windows.Point(e.X, e.Y);
                    e.Handled = true;
                }
                else if (_triggerKeyDown && e.IsMove)
                {
                    double dist = Distance(new System.Windows.Point(e.X, e.Y), _keyDownPos);
                    if (dist >= MoveThreshold)
                        TransitionToShown(e.X, e.Y, config);
                }
                else if (_triggerKeyDown && isTriggerUp)
                {
                    _triggerKeyDown = false;
                    e.Handled = false;
                }
                break;

            case WheelState.WheelShown:
            case WheelState.SectorHighlighted:
                e.Handled = true;

                if (e.IsMove)
                    UpdateHighlight(e.X, e.Y);

                if (isTriggerUp)
                    TriggerAction(config);
                break;
        }
    }

    private void TransitionToShown(int mouseX, int mouseY, WheelConfig config)
    {
        _state = WheelState.WheelShown;

        var rawPos = new System.Windows.Point(mouseX, mouseY);
        var (center, moveTo) = ScreenHelper.CalculateCenter(
            rawPos,
            config.Settings.ConstrainToScreen,
            config.Settings.AutoMoveCursor);
        _wheelCenter = center;

        if (moveTo.HasValue)
            NativeMethods.SetCursorPos((int)moveTo.Value.X, (int)moveTo.Value.Y);

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
            _animator.Open(_wheelCenter));

        if (config.Settings.TimeoutMs > 0)
        {
            _timeoutTimer.Interval = TimeSpan.FromMilliseconds(config.Settings.TimeoutMs);
            _timeoutTimer.Start();
        }
    }

    private void UpdateHighlight(int mouseX, int mouseY)
    {
        var config = _configService.Load();
        int sectors = config.Settings.OuterRing16Mode ? 16 : 8;
        var (ring, sector) = WheelGeometry.HitTestMainWheel(
            _wheelCenter.X, _wheelCenter.Y,
            mouseX, mouseY, sectors,
            false); // 环1使用22.5度，环2使用33.75度

        if (ring == "dead")
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => _canvas.ClearHighlight());
            _state = WheelState.WheelShown;
        }
        else
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => _canvas.SetHighlight(ring, sector));
            _hoverRing = ring;
            _hoverSector = sector;
            _state = WheelState.SectorHighlighted;
        }
    }

    private void TriggerAction(WheelConfig config)
    {
        _timeoutTimer.Stop();
        _triggerKeyDown = false;
        _state = WheelState.Idle;

        System.Windows.Application.Current.Dispatcher.Invoke(() => _animator.Close());

        if (_hoverRing == string.Empty || _hoverSector < 0) return;

        var ring = _hoverRing;
        var sector = _hoverSector;
        _hoverRing = string.Empty;
        _hoverSector = -1;

        Task.Delay(50).ContinueWith(_ =>
        {
            var direction = SectorIndexToDirection(sector,
                ring == "outer" && config.Settings.OuterRing16Mode ? 16 : 8);
            var action = _configService.ResolveSectorAction(
                config, _sceneResolver.CurrentProcessName, ring, direction);
            if (action != null && action.Type != ActionType.None)
                _executor.Execute(action.Type.ToString(), action.Value, action.Label, action.IconPath);
        });
    }

    public void RepeatTrigger(WheelConfig config)
    {
        if (_state != WheelState.SectorHighlighted) return;
        var direction = SectorIndexToDirection(_hoverSector, 8);
        var action = _configService.ResolveSectorAction(
            config, _sceneResolver.CurrentProcessName, _hoverRing, direction);
        if (action != null && action.Type != ActionType.None)
            _executor.Execute(action.Type.ToString(), action.Value, action.Label, action.IconPath);
    }

    private void OnTimeout(object? sender, EventArgs e)
    {
        _timeoutTimer.Stop();
        _state = WheelState.Idle;
        _triggerKeyDown = false;
        _hoverRing = string.Empty;
        _hoverSector = -1;
        System.Windows.Application.Current.Dispatcher.Invoke(() => _animator.Close());
    }

    private static bool IsTriggerKeyDown(MouseHookEventArgs e, string key) => key switch
    {
        "middle" => e.IsMiddleDown,
        "x1" => e.IsX1Down,
        "x2" => e.IsX2Down,
        _ => false
    };

    private static bool IsTriggerKeyUp(MouseHookEventArgs e, string key) => key switch
    {
        "middle" => e.IsMiddleUp,
        "x1" => e.IsX1Up,
        "x2" => e.IsX2Up,
        _ => false
    };

    private static double Distance(System.Windows.Point a, System.Windows.Point b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static string SectorIndexToDirection(int idx, int total)
    {
        var dirs8 = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        var dirs16 = new[] { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
                             "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
        return total == 16 ? dirs16[idx % 16] : dirs8[idx % 8];
    }

    public void Dispose()
    {
        _timeoutTimer.Stop();
        _hook.MouseEvent -= OnMouseEvent;
    }
}
