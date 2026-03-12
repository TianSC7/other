namespace WheelMenu.Windows;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WheelMenu.Config;
using WheelMenu.Renderer;

/// <summary>
/// 设置窗口 - 轮盘菜单配置界面
/// </summary>
public partial class SettingsWindow : Window
{
    private AppConfig _config = null!;
    private bool _isDirty = false;
    private bool _isInitialized = false;

    // 当前选中
    private string _currentRing = "inner";
    private int _currentSector = 0;
    private string _currentSceneKey = "global";
    
    // 当前编辑的格子配置
    private SlotConfig? _currentSlotConfig = null;

    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
        _isInitialized = true;
        
        // 初始化动作值输入面板
        InitializeActionValuePanel();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadConfig();
        InitializeWheelCanvas();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isDirty)
        {
            var result = MessageBox.Show(
                "有未保存的修改，是否保存？",
                "提示",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                OnSaveSettings(null!, null!);
            }
            else if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
            }
        }
    }

    #region 配置加载

    private void LoadConfig()
    {
        _config = ConfigService.LoadConfig();
        
        // 加载触发设置
        LoadTriggerSettings();
        
        // 加载场景列表
        RefreshSceneList();
    }

    private void LoadTriggerSettings()
    {
        var settings = _config.TriggerSettings;

        // 触发键
        foreach (ComboBoxItem item in TriggerKeyCombo.Items)
        {
            if (item.Tag?.ToString() == settings.TriggerKey)
            {
                TriggerKeyCombo.SelectedItem = item;
                break;
            }
        }

        // 尺寸
        SizeSlider.Value = settings.Size;
        SizeTextBox.Text = settings.Size.ToString();

        // 超时
        TimeoutTextBox.Text = settings.TimeoutMs.ToString();

        // 功能开关
        OuterRing16Check.IsChecked = settings.OuterRing16Mode;
        HideLabelCheck.IsChecked = settings.HideLabelWhenIcon;
        
        // 限定屏幕范围
        foreach (ComboBoxItem item in ConstrainScreenCombo.Items)
        {
            if (item.Tag?.ToString() == settings.EdgeConstrainMode.ToString())
            {
                ConstrainScreenCombo.SelectedItem = item;
                break;
            }
        }
        
        AutoMoveCursorCheck.IsChecked = settings.AutoMoveCursor;
        AutoMoveCursorCheck.IsEnabled = settings.EdgeConstrainMode != EdgeConstrainMode.None;

        // 重复触发键
        RepeatKeyTextBox.Text = settings.RepeatTriggerKey;
    }

    private void RefreshSceneList()
    {
        SceneListBox.Items.Clear();
        
        // 添加全局场景
        var globalItem = new ListBoxItem
        {
            Content = "🌐 全局",
            Tag = "global"
        };
        SceneListBox.Items.Add(globalItem);

        // 添加软件场景
        foreach (var scene in _config.Scenes)
        {
            var item = new ListBoxItem
            {
                Content = $"📄 {scene.Value.Name}",
                Tag = scene.Key
            };
            SceneListBox.Items.Add(item);
        }

        // 默认选中全局
        if (SceneListBox.Items.Count > 0)
            SceneListBox.SelectedIndex = 0;
    }

    private void InitializeWheelCanvas()
    {
        if (WheelPreviewCanvas != null)
        {
            WheelPreviewCanvas.SetCenter(new Point(170, 170));
            // 订阅 DiskUIControl 的扇区点击事件
            WheelPreviewCanvas.SectorClicked += OnDiskUIControlSectorClicked;
            UpdateWheelPreview();
        }
    }

    #endregion

    #region 导航事件

    private void OnNavSettings(object sender, RoutedEventArgs e)
    {
        ShowSettingsPanel();
    }

    private void OnNavActions(object sender, RoutedEventArgs e)
    {
        ShowActionsPanel();
        // 切换到动作管理页时，更新圆盘示意图
        UpdateWheelPreview();
    }

    private void ShowSettingsPanel()
    {
        SettingsPanel.Visibility = Visibility.Visible;
        ActionsPanel.Visibility = Visibility.Collapsed;
    }

    private void ShowActionsPanel()
    {
        SettingsPanel.Visibility = Visibility.Collapsed;
        ActionsPanel.Visibility = Visibility.Visible;
    }

    #endregion

    #region 控件联动

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        
        _isDirty = true;
        
        // 更新自动移动鼠标的启用状态
        if (ConstrainScreenCombo.SelectedItem is ComboBoxItem item && item.Tag is string mode)
        {
            AutoMoveCursorCheck.IsEnabled = mode != "None";
        }
    }

    private void OnSizeSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;
        if (SizeTextBox != null)
        {
            SizeTextBox.Text = ((int)SizeSlider.Value).ToString();
            _isDirty = true;
        }
    }

    private void OnSizeTextBoxChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized) return;
        if (SizeSlider != null && int.TryParse(SizeTextBox.Text, out int size))
        {
            SizeSlider.Value = Math.Clamp(size, 50, 300);
            _isDirty = true;
        }
    }

    #endregion

    #region 动作值输入面板

    private TextBox? _actionValueTextBox = null;

    private void InitializeActionValuePanel()
    {
        // 初始化动作值面板 - 新界面中不需要这行
    }

    private void OnActionTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        // 新界面中不需要这个方法 - 动作通过4x4网格添加
    }

    private void UpdateActionValuePanel(string actionType)
    {
        // 新界面中不需要这个方法 - 动作通过4x4网格添加
    }

    private void OnBrowsePath(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "所有文件|*.*",
            Title = "选择文件"
        };
        if (dialog.ShowDialog() == true)
        {
            _actionValueTextBox!.Text = dialog.FileName;
        }
    }

    private void OnActionNameChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized) return;
        _isDirty = true;
    }

    private void OnChangeIcon(object sender, RoutedEventArgs e)
    {
        // TODO: 实现图标选择功能
        MessageBox.Show("图标选择功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnClearIcon(object sender, RoutedEventArgs e)
    {
        // TODO: 清除图标
        _isDirty = true;
    }

    #endregion

    #region 场景管理

    private void OnAddScene(object sender, RoutedEventArgs e)
    {
        var dialog = new AddSceneDialog();
        dialog.Owner = this;
        
        if (dialog.ShowDialog() == true)
        {
            string processName = dialog.ProcessName.ToLowerInvariant().Trim();
            if (string.IsNullOrEmpty(processName))
            {
                MessageBox.Show("请输入进程名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_config.Scenes.ContainsKey(processName))
            {
                MessageBox.Show("该场景已存在", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 添加新场景
            var newScene = new SceneConfig
            {
                Name = dialog.DisplayName,
                ProcessName = processName,
                InnerRing = new SlotConfig[8],
                OuterRing = new SlotConfig[8],
                ExtendedRing = new SlotConfig[8]
            };
            _config.Scenes[processName] = newScene;
            
            RefreshSceneList();
            _isDirty = true;
        }
    }

    private void OnDeleteScene(object sender, RoutedEventArgs e)
    {
        if (SceneListBox.SelectedItem is ListBoxItem item && item.Tag is string key)
        {
            if (key == "global")
            {
                MessageBox.Show("全局场景不能删除", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"确定删除场景 \"{_config.Scenes[key]?.Name}\"？",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _config.Scenes.Remove(key);
                RefreshSceneList();
                _isDirty = true;
            }
        }
    }

    private void OnSceneSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        // 场景切换时更新圆盘示意图
        UpdateWheelPreview();
    }

    #endregion

    #region 动作页面和网格
    
    // 动作页面列表（最多10页，每页16个动作）
    private List<List<SimpleActionItem>> _actionPages = new List<List<SimpleActionItem>>();
    private List<string> _actionPageTitles = new List<string>();
    private int _currentActionPageIndex = 0;
    private const int MaxActionPages = 10;
    private const int ActionsPerPage = 16; // 4x4
    
    // 简单的动作项类，用于4x4网格显示
    private class SimpleActionItem
    {
        public string Label { get; set; } = "";
        public string ActionType { get; set; } = "none";
        public string ActionValue { get; set; } = "";
    }
    
    // 用于存储所有动作页面的StackPanel
    private StackPanel? _actionPagesPanel;
    
    private void OnAddActionPage(object sender, RoutedEventArgs e)
    {
        // 检查是否已达到最大页数
        if (_actionPages.Count >= MaxActionPages)
        {
            MessageBox.Show($"已达到最大动作页面数量（{MaxActionPages}页）", "提示", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        // 获取页面标题
        string pageTitle = "动作" + (_actionPages.Count + 1);
        
        // 创建新的动作页面（4x4 = 16个动作）
        var newPage = new List<SimpleActionItem>();
        for (int i = 0; i < ActionsPerPage; i++)
        {
            // 空白标签，显示为+
            newPage.Add(new SimpleActionItem { Label = "" });
        }
        
        _actionPages.Add(newPage);
        _actionPageTitles.Add(pageTitle);
        
        // 在UI中添加新的动作页面（垂直排列）
        AddActionPageToPanel(_actionPages.Count - 1, pageTitle);
    }
    
    private void AddActionPageToPanel(int pageIndex, string pageTitle)
    {
        // 获取或创建ActionPagesPanel
        if (_actionPagesPanel == null)
        {
            _actionPagesPanel = this.FindName("ActionPagesPanel") as StackPanel;
        }
        if (_actionPagesPanel == null) return;
        
        // 创建页面容器
        var pagePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
        
        // 页面标题
        var titleBlock = new TextBlock
        {
            Text = $"【{pageTitle}】",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        pagePanel.Children.Add(titleBlock);
        
        // 4x4网格容器 - 使用Grid固定布局
        var gridBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 248, 248)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10)
        };
        
        var grid = new Grid();
        
        // 定义4行4列
        for (int i = 0; i < 4; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        
        // 为每个动作项创建一个Border
        for (int i = 0; i < _actionPages[pageIndex].Count; i++)
        {
            var actionItem = _actionPages[pageIndex][i];
            int row = i / 4;
            int col = i % 4;
            
            var cellBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(3),
                Padding = new Thickness(5),
                AllowDrop = true,
                Tag = actionItem  // 存储动作项数据
            };
            
            var textBlock = new TextBlock
            {
                // 显示Label属性，不是ToString()
                Text = string.IsNullOrEmpty(actionItem.Label) ? "+" : actionItem.Label,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11
            };
            cellBorder.Child = textBlock;
            
            // 绑定右键菜单
            cellBorder.MouseLeftButtonDown += OnActionCellMouseDown;
            cellBorder.MouseRightButtonUp += OnActionCellRightClick;
            
            Grid.SetRow(cellBorder, row);
            Grid.SetColumn(cellBorder, col);
            grid.Children.Add(cellBorder);
        }
        
        gridBorder.Child = grid;
        pagePanel.Children.Add(gridBorder);
        
        _actionPagesPanel.Children.Add(pagePanel);
    }
    
    private void RefreshActionGrid()
    {
        // 刷新操作现在由AddActionPageToPanel处理
        // 多页面垂直显示时不需要刷新单个控件
    }
    
    private void OnPrevActionPage(object sender, RoutedEventArgs e)
    {
        if (_actionPages.Count <= 1) return;
        
        if (_currentActionPageIndex > 0)
        {
            _currentActionPageIndex--;
            RefreshActionGrid();
        }
    }
    
    private void OnNextActionPage(object sender, RoutedEventArgs e)
    {
        if (_actionPages.Count <= 1) return;
        
        if (_currentActionPageIndex < _actionPages.Count - 1)
        {
            _currentActionPageIndex++;
            RefreshActionGrid();
        }
    }
    
    private void OnActionCellMouseDown(object sender, MouseButtonEventArgs e)
    {
        // 开始拖拽 - 将动作格子数据传输到目标扇区
        if (sender is Border border && border.DataContext is SimpleActionItem actionItem)
        {
            var data = new DataObject("ActionItem", actionItem);
            DragDrop.DoDragDrop(border, data, DragDropEffects.Copy);
        }
    }
    
    private void OnActionCellRightClick(object sender, MouseButtonEventArgs e)
    {
        // 右键弹出动作类型选择菜单
        var contextMenu = new ContextMenu();
        
        var actionTypes = new[]
        {
            ("发送快捷键", "hotkey"),
            ("模拟输入", "simulate_input"),
            ("粘贴内容", "paste"),
            ("打开文件", "open"),
            ("运行动作", "run_action"),
            ("发送文本", "send_text"),
            ("日期时间", "datetime"),
            ("（清除）", "none")
        };
        
        foreach (var (name, tag) in actionTypes)
        {
            var menuItem = new MenuItem { Header = name, Tag = tag };
            menuItem.Click += OnActionTypeMenuItemClick;
            contextMenu.Items.Add(menuItem);
        }
        
        contextMenu.IsOpen = true;
    }
    
    private void OnActionTypeMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string actionType)
        {
            if (actionType == "none")
            {
                // 清除动作
                MessageBox.Show("清除动作", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // 弹出动作设置对话框
                ShowActionConfigDialog(actionType);
            }
        }
    }
    
    private void ShowActionConfigDialog(string actionType)
    {
        // TODO: 根据动作类型显示不同的配置对话框
        MessageBox.Show($"配置动作类型: {actionType}", "动作设置", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    
    #endregion
    
    #region 圈层切换

    private void OnRingTabChanged(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        
        // 圈层选项已取消，默认使用外圈（因为这是主要的动作设置区）
        _currentRing = "outer";
        
        UpdateWheelPreview();
        UpdateSlotInfoText();
    }

    private void UpdateWheelPreview()
    {
        if (!_isInitialized || WheelPreviewCanvas == null) return;

        var currentScene = GetCurrentScene();
        if (currentScene == null) return;

        // 转换 SlotConfig 到 WheelSectorData
        var innerData = ConvertToWheelSectorData(currentScene.InnerRing, _currentSceneKey == "global");
        var outerData = ConvertToWheelSectorData(currentScene.OuterRing, _currentSceneKey == "global");

        WheelPreviewCanvas.InnerData = innerData;
        WheelPreviewCanvas.OuterData = outerData;
        WheelPreviewCanvas.SetDisplayOptions(
            _config.TriggerSettings.HideLabelWhenIcon,
            _config.TriggerSettings.OuterRing16Mode);
        
        WheelPreviewCanvas.InvalidateVisual();
    }

    private WheelSectorData[] ConvertToWheelSectorData(SlotConfig[] slots, bool isGlobal)
    {
        var data = new WheelSectorData[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            data[i] = new WheelSectorData
            {
                Label = slot?.Label ?? string.Empty,
                Icon = null, // TODO: 加载图标
                HasAction = slot != null && !string.IsNullOrEmpty(slot?.ActionType) && slot.ActionType != "none"
            };
        }
        return data;
    }

    private void UpdateSlotInfoText()
    {
        // 显示当前动作页面信息
        SlotInfoText.Text = "动作页面";
    }

    #endregion

    #region 圆盘交互

    // DiskUIControl 扇区点击事件处理
    private void OnDiskUIControlSectorClicked(object? sender, SectorClickEventArgs e)
    {
        if (WheelPreviewCanvas == null) return;
        
        _currentRing = e.Ring;
        _currentSector = e.SectorId;
        
        // 更新选中状态
        WheelPreviewCanvas.SetHighlight(e.Ring, e.SectorId);
        
        // 更新UI
        UpdateSlotInfoText();
        LoadSlotConfig();
    }

    private void OnWheelCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (WheelPreviewCanvas == null) return;
        
        var position = e.GetPosition(WheelPreviewCanvas);
        var center = WheelPreviewCanvas.GetCenter();
        
        // 计算缩放比例 - 使用实际可见的轮盘半径
        double scale = Math.Min(WheelPreviewCanvas.ActualWidth, WheelPreviewCanvas.ActualHeight) 
            / (WheelConstants.OuterRingRadius * 2);
        
        // 将鼠标位置转换为逻辑坐标（考虑缩放）
        double logicX = center.X + (position.X - center.X) / scale;
        double logicY = center.Y + (position.Y - center.Y) / scale;
        
        // 使用统一的 WheelGeometry 方法进行点击测试
        bool is16Mode = _config?.TriggerSettings?.OuterRing16Mode ?? false;
        int totalSectors = is16Mode ? 16 : 8;
        
        var (ring, sector) = WheelGeometry.HitTestMainWheel(
            center.X, center.Y, logicX, logicY, totalSectors,
            WheelPreviewCanvas.GetExtendedRingVisible());
        
        // 死区不处理
        if (ring == "dead" || ring == "extended")
        {
            return;
        }
        
        _currentRing = ring;
        _currentSector = sector;
        
        // 更新选中状态
        WheelPreviewCanvas.SetHighlight(ring, sector);
        
        // 更新UI
        UpdateSlotInfoText();
        LoadSlotConfig();
    }
    
    private void OnWheelCanvasDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("ActionItem"))
        {
            e.Effects = DragDropEffects.Copy;
            
            // 计算放置位置对应的扇区
            if (sender is WheelMenu.Renderer.WheelCanvas canvas)
            {
                var position = e.GetPosition(canvas);
                var center = canvas.GetCenter();
                
                double dx = position.X - center.X;
                double dy = position.Y - center.Y; // 屏幕坐标系
                double distance = Math.Sqrt(dx * dx + dy * dy);
                double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
                if (angle < 0) angle += 360;
                
                // 计算缩放比例
                double scale = Math.Min(canvas.ActualWidth, canvas.ActualHeight) 
                    / (WheelConstants.OuterRingRadius * 2);
                double scaledDistance = distance / scale;
                
                string ring = "";
                int sector = 0;
                
                if (scaledDistance >= WheelConstants.DeadZoneRadius && scaledDistance < WheelConstants.InnerRingRadius)
                {
                    ring = "inner";
                    double sectorAngle = 360.0 / 8;
                    sector = (int)((angle - 270 + 360) / sectorAngle) % 8;
                }
                else if (scaledDistance >= WheelConstants.InnerRingRadius && scaledDistance < WheelConstants.OuterRingRadius)
                {
                    ring = "outer";
                    bool is16Mode = _config.TriggerSettings.OuterRing16Mode;
                    int sectors = is16Mode ? 16 : 8;
                    double sectorAngle = 360.0 / sectors;
                    
                    if (is16Mode)
                    {
                        // 16模式：扇区0中心在281.25°(270°+11.25°)
                        double rotation = 11.25;
                        sector = (int)((angle - 270 + 360 + rotation) / sectorAngle) % sectors;
                    }
                    else
                    {
                        // 8模式：扇区0中心在292.5°(270°+22.5°)
                        double rotation = 22.5;
                        sector = (int)((angle - 270 + 360 + rotation) / sectorAngle) % sectors;
                    }
                }
                
                // 高亮目标扇区
                if (!string.IsNullOrEmpty(ring))
                {
                    canvas.SetHighlight(ring, sector);
                }
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }
    
    private void OnWheelCanvasDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("ActionItem")) return;
        if (sender is not WheelMenu.Renderer.WheelCanvas canvas) return;
        
        var position = e.GetPosition(canvas);
        var center = canvas.GetCenter();
        
        double dx = position.X - center.X;
        double dy = position.Y - center.Y; // 屏幕坐标系
        double distance = Math.Sqrt(dx * dx + dy * dy);
        double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
        if (angle < 0) angle += 360;
        
        // 计算缩放比例
        double scale = Math.Min(canvas.ActualWidth, canvas.ActualHeight) 
            / (WheelConstants.OuterRingRadius * 2);
        double scaledDistance = distance / scale;
        
        string ring = "";
        int sector = 0;
        
        if (scaledDistance >= WheelConstants.DeadZoneRadius && scaledDistance < WheelConstants.InnerRingRadius)
        {
            ring = "inner";
            double sectorAngle = 360.0 / 8;
            sector = (int)((angle - 270 + 360) / sectorAngle) % 8;
        }
        else if (scaledDistance >= WheelConstants.InnerRingRadius && scaledDistance < WheelConstants.OuterRingRadius)
        {
            ring = "outer";
            bool is16Mode = _config.TriggerSettings.OuterRing16Mode;
            int sectors = is16Mode ? 16 : 8;
            double sectorAngle = 360.0 / sectors;
            
            if (is16Mode)
            {
                // 16模式：扇区0中心在281.25°(270°+11.25°)
                double rotation = 11.25;
                sector = (int)((angle - 270 + 360 + rotation) / sectorAngle) % sectors;
            }
            else
            {
                // 8模式：扇区0中心在292.5°(270°+22.5°)
                double rotation = 22.5;
                sector = (int)((angle - 270 + 360 + rotation) / sectorAngle) % sectors;
            }
        }
        else
        {
            return;
        }
        
        // 获取拖拽的动作
        if (e.Data.GetData("ActionItem") is SimpleActionItem actionItem)
        {
            var currentScene = GetCurrentScene();
            if (currentScene == null) return;
            
            SlotConfig[]? ringConfig = ring switch
            {
                "inner" => currentScene.InnerRing,
                "outer" => currentScene.OuterRing,
                _ => null
            };
            
            if (ringConfig != null && sector < ringConfig.Length)
            {
                // 创建或更新扇区配置
                ringConfig[sector] = new SlotConfig
                {
                    Label = actionItem.Label,
                    ActionType = actionItem.ActionType,
                    ActionValue = actionItem.ActionValue
                };
                
                _isDirty = true;
                
                // 更新轮盘预览
                UpdateWheelPreview();
                
                MessageBox.Show($"已将动作 \"{actionItem.Label}\" 绑定到 {ring}圈 第{sector + 1}扇区", "提示", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void LoadSlotConfig()
    {
        var currentScene = GetCurrentScene();
        if (currentScene == null) return;

        SlotConfig[]? ringConfig = _currentRing switch
        {
            "inner" => currentScene.InnerRing,
            "outer" => currentScene.OuterRing,
            "extended" => currentScene.ExtendedRing,
            _ => null
        };

        if (ringConfig == null || _currentSector >= ringConfig.Length)
        {
            _currentSlotConfig = null;
            // 新界面中不再使用动作名称和类型输入框
            return;
        }

        _currentSlotConfig = ringConfig[_currentSector];
        
        // 新界面中不再使用动作名称和类型输入框 - 动作通过4x4网格显示
    }

    #endregion

    #region 格子操作

    private void OnSaveSlot(object sender, RoutedEventArgs e)
    {
        // 新界面中不再使用OnSaveSlot - 动作通过4x4网格和拖拽来设置
        MessageBox.Show("请在4x4动作网格中选择动作，拖拽到轮盘扇区进行绑定", "提示", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private string GetActionValueForCurrentType()
    {
        return _actionValueTextBox?.Text ?? string.Empty;
    }

    private void OnClearSlot(object sender, RoutedEventArgs e)
    {
        var currentScene = GetCurrentScene();
        if (currentScene == null) return;

        SlotConfig[]? ringConfig = _currentRing switch
        {
            "inner" => currentScene.InnerRing,
            "outer" => currentScene.OuterRing,
            "extended" => currentScene.ExtendedRing,
            _ => null
        };

        if (ringConfig != null && _currentSector < ringConfig.Length)
        {
            ringConfig[_currentSector] = new SlotConfig { ActionType = "none" };
            _isDirty = true;
        }
    }

    private SceneConfig? GetCurrentScene()
    {
        if (_config == null) return null;
        
        if (SceneListBox.SelectedItem is ListBoxItem item && item.Tag is string key)
        {
            if (key == "global")
                return _config.GlobalScene;
            
            return _config.Scenes.GetValueOrDefault(key);
        }
        return _config.GlobalScene;
    }

    #endregion

    #region 设置保存/取消/重置

    private void OnResetSettings(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "确定要重置为默认配置吗？此操作将清除所有自定义设置。",
            "确认重置",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            // 创建默认配置
            _config = new AppConfig
            {
                ConfigVersion = "1.0",
                TriggerSettings = new TriggerSettings(),
                GlobalScene = new SceneConfig
                {
                    Name = "全局",
                    InnerRing = new SlotConfig[8],
                    OuterRing = new SlotConfig[8],
                    ExtendedRing = new SlotConfig[8]
                },
                Scenes = new Dictionary<string, SceneConfig>()
            };
            
            // 重新加载UI
            LoadTriggerSettings();
            RefreshSceneList();
            UpdateWheelPreview();
            _isDirty = true;
            
            MessageBox.Show("已重置为默认配置，请点击保存按钮保存更改。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnSaveSettings(object sender, RoutedEventArgs e)
    {
        try
        {
            // 保存触发设置
            if (TriggerKeyCombo.SelectedItem is ComboBoxItem triggerItem)
            {
                _config.TriggerSettings.TriggerKey = triggerItem.Tag?.ToString() ?? "middle";
            }

            _config.TriggerSettings.Size = (int)SizeSlider.Value;
            _config.TriggerSettings.TimeoutMs = int.TryParse(TimeoutTextBox.Text, out int timeout) ? Math.Max(0, timeout) : 0;
            _config.TriggerSettings.OuterRing16Mode = OuterRing16Check.IsChecked == true;
            _config.TriggerSettings.HideLabelWhenIcon = HideLabelCheck.IsChecked == true;
            if (ConstrainScreenCombo.SelectedItem is ComboBoxItem constrainItem)
            {
                var modeStr = constrainItem.Tag?.ToString() ?? "Constrain";
                if (Enum.TryParse<EdgeConstrainMode>(modeStr, out var mode))
                {
                    _config.TriggerSettings.EdgeConstrainMode = mode;
                }
            }
            _config.TriggerSettings.AutoMoveCursor = AutoMoveCursorCheck.IsChecked == true;
            _config.TriggerSettings.RepeatTriggerKey = RepeatKeyTextBox.Text;

            // 保存到文件
            ConfigService.SaveConfig();
            
            _isDirty = false;
            MessageBox.Show("设置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnCancelSettings(object sender, RoutedEventArgs e)
    {
        if (_isDirty)
        {
            var result = MessageBox.Show(
                "有未保存的修改，是否保存？",
                "提示",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                OnSaveSettings(sender, e);
                return;
            }
            else if (result == MessageBoxResult.Cancel)
            {
                return;
            }
        }
        
        // 重新加载配置，放弃未保存的修改
        LoadConfig();
        Close();
    }

    #endregion

    #region 重复触发键

    private void OnClearRepeatKey(object sender, RoutedEventArgs e)
    {
        RepeatKeyTextBox.Text = string.Empty;
        _isDirty = true;
    }

    #endregion
}

/// <summary>
/// 添加场景对话框
/// </summary>
public class AddSceneDialog : Window
{
    private TextBox _processNameBox = null!;
    private TextBox _displayNameBox = null!;

    public string ProcessName => _processNameBox.Text;
    public string DisplayName => string.IsNullOrEmpty(_displayNameBox.Text) ? _processNameBox.Text : _displayNameBox.Text;

    public AddSceneDialog()
    {
        Title = "添加软件场景";
        Width = 400;
        Height = 180;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // 进程名
        var processLabel = new TextBlock { Text = "进程名（如 notepad.exe）:", Margin = new Thickness(0, 0, 0, 5) };
        Grid.SetRow(processLabel, 0);
        grid.Children.Add(processLabel);

        _processNameBox = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
        Grid.SetRow(_processNameBox, 1);
        grid.Children.Add(_processNameBox);

        // 显示名称（可选）
        var displayLabel = new TextBlock { Text = "显示名称（可选）:", Margin = new Thickness(0, 0, 0, 5) };
        Grid.SetRow(displayLabel, 2);
        grid.Children.Add(displayLabel);

        _displayNameBox = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
        Grid.SetRow(_displayNameBox, 3);
        grid.Children.Add(_displayNameBox);

        // 按钮
        var buttonPanel = new StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };
        
        var okButton = new Button { Content = "确定", Width = 80, Margin = new Thickness(0, 0, 10, 0) };
        okButton.Click += (s, e) => { DialogResult = true; Close(); };
        
        var cancelButton = new Button { Content = "取消", Width = 80 };
        cancelButton.Click += (s, e) => { DialogResult = false; Close(); };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        
        var mainPanel = new Grid();
        mainPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        
        Grid.SetRow(grid, 0);
        Grid.SetRow(buttonPanel, 1);
        
        mainPanel.Children.Add(grid);
        mainPanel.Children.Add(buttonPanel);
        
        Content = mainPanel;
    }
}
