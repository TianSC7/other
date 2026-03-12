# Phase 2 — 动作管理页  完整重写 SOP（.NET 8.0 / WPF）

> **本文档基于真实 Quicker 截图重写，与旧版 SOP 存在根本性差异，以本文档为准。**
> 旧文件 `Phase2_动作管理页_详细SOP_dotnet8.md` 作废。

---

## 一、真实 UI 结构总览（基于截图）

```
┌──────────────────────────────────────────────────────────────────────┐
│  场景与动作管理                                             [最大化] [×] │
├────────────────┬─────────────────────────────────────────────────────┤
│  左侧场景列表  │  右侧内容区                                            │
│  [~170px]      │                                                      │
│                │  ┌── 动作页 Tab 区（顶部，横向滚动）──────────────┐  │
│  全局          │  │ [AutoCAD App] [AutoCAD App #2] [AutoCAD App #4]│  │
│  global        │  └────────────────────────────────────────────────┘  │
│                │                                                      │
│  通用          │  ┌── 动作网格区（图标+名称，可拖拽排序）────────────┐  │
│  common        │  │  [FB][F2][HD][BPT]  [BO][FR0][FR20][FR]  ...   │  │
│                │  │  [77.25][99650]...  每格：图标+名称              │  │
│  任务栏        │  └────────────────────────────────────────────────┘  │
│  taskbar       │                                                      │
│                │  [+添加动作页] [附加通用动作页(0)] [自动返回第一页]    │
│  桌面          │                                                      │
│  desktop       │  ┌── 功能 Tab（底部）──────────────────────────────┐  │
│                │  │  [轮盘菜单] [手势操作] [左键辅助]                 │  │
│  AutoCAD App   │  └────────────────────────────────────────────────┘  │
│  acad.exe      │                                                      │
│                │  ── 轮盘菜单 Tab 内容 ─────────────────────────────   │
│                │                                                      │
│                │  □ 在此软件下禁用轮盘菜单                             │
│                │                                                      │
│  [+][编辑][删] │  ┌── 轮盘圆盘预览区 ─────┐  ┌── 右侧操作区 ──────┐  │
│                │  │  （只读圆盘示意图）    │  │ [复制当前轮盘设置] │  │
│                │  │  每个扇区显示对应     │  │ [清空轮盘]        │  │
│                │  │  动作页的动作名+图标  │  │ [轮盘设置]        │  │
│                │  └──────────────────────┘  └───────────────────┘  │
└────────────────┴─────────────────────────────────────────────────────┘
```

---

## 二、核心概念澄清（与旧 SOP 的根本差异）

### 2.1 动作页（ActionPage）概念

Quicker 的轮盘菜单是**多页结构**，不是单一的内圈/外圈/扩展圈：

```
轮盘菜单
  └── 动作页 1（默认页，始终显示在内圈第1位）
  └── 动作页 2
  └── 动作页 3（...最多可有多个）

每个动作页：
  └── 最多 8 个动作格子（一行一格，图标 + 名称）

轮盘弹出时：
  - 内圈每个扇区 = 对应动作页的第 1 个动作（或动作页本身）
  - 旋转到某扇区后进入该动作页，看到该页全部动作
  - 外圈/扩展圈 = 更多动作页或第二层级动作
```

### 2.2 扇区与动作页的对应关系

```
内圈 8 个扇区（N/NE/E/SE/S/SW/W/NW）
  每个扇区绑定一个"动作页"（不是单个动作）

动作页内容：
  ┌─────────────────────────────────┐
  │  动作页名称：AutoCAD Application │
  │  ┌────┬────┬────┬────┐         │
  │  │ FB │ F2 │ HD │BPT │  行1    │
  │  ├────┼────┼────┼────┤         │
  │  │ BO │FR0 │FR20│ FR │  行2    │
  │  ├────┼────┼────┼────┤         │
  │  │BCZ │ 2X │图库│SBZ │  行3    │
  │  └────┴────┴────┴────┘         │
  │  （每页最多 N 个动作格，4列排列）│
  └─────────────────────────────────┘

扇区与动作页绑定：
  扇区 NE（右上）→ 动作页 "AutoCAD Application"
  扇区 E（右）  → 动作页 "AutoCAD Application #2"
  扇区 SE（右下）→ 动作页 "AutoCAD Application #4"
  ...
```

### 2.3 拖拽绑定（核心交互）

动作网格区的动作可以**拖拽到轮盘圆盘的扇区上**，完成绑定：

```
拖拽操作：
  源：动作网格中的某个动作格子
  目标：圆盘预览区的某个扇区

  拖拽到内圈扇区 → 该动作绑定为该扇区的直接触发动作
  拖拽到外圈扇区 → 绑定为外圈该方向的动作

也可以：
  圆盘扇区之间互相拖拽 → 交换/移动绑定
```

---

## 三、数据模型重设计

旧模型的 `InnerRing/OuterRing/ExtendedRing` 不够用，需要支持动作页概念：

### ActionPage.cs（新增）

```csharp
// Settings/Models/ActionPage.cs
namespace WheelMenu.Settings.Models;

public class ActionPage
{
    /// <summary>动作页唯一ID（GUID）</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>动作页显示名称（如 "AutoCAD Application"）</summary>
    public string Name { get; set; } = "新动作页";

    /// <summary>该页中的所有动作，有序列表，显示顺序 = 列表顺序</summary>
    public List<ActionItem> Actions { get; set; } = new();
}
```

### ActionItem.cs（新增，替代 SectorActionConfig）

```csharp
// Settings/Models/ActionItem.cs
namespace WheelMenu.Settings.Models;

public class ActionItem
{
    public string     Id        { get; set; } = Guid.NewGuid().ToString();
    public ActionType Type      { get; set; } = ActionType.None;
    public string     Value     { get; set; } = string.Empty;
    public string     Name      { get; set; } = string.Empty;   // 显示名称
    public string?    IconPath  { get; set; } = null;
    public string?    ActionRefId  { get; set; } = null;
    public string?    ActionParam  { get; set; } = null;
}
```

### WheelBinding.cs（新增，扇区绑定）

```csharp
// Settings/Models/WheelBinding.cs
namespace WheelMenu.Settings.Models;

/// <summary>
/// 轮盘某个扇区绑定到某个 ActionPage（或单个 ActionItem）
/// </summary>
public class WheelBinding
{
    /// <summary>方向：N/NE/E/SE/S/SW/W/NW（内圈8格）</summary>
    public string Direction { get; set; } = string.Empty;

    /// <summary>圈层：inner / outer / extended</summary>
    public string Ring { get; set; } = "inner";

    /// <summary>
    /// 绑定目标：
    ///   若 ActionPageId 有值 → 此扇区触发整个动作页（进入子菜单）
    ///   若 ActionItemId 有值 → 此扇区直接执行单个动作
    /// </summary>
    public string? ActionPageId  { get; set; } = null;
    public string? ActionItemId  { get; set; } = null;
}
```

### SceneConfig.cs（重写）

```csharp
// Settings/Models/SceneConfig.cs
namespace WheelMenu.Settings.Models;

public class SceneConfig
{
    public string  Name    { get; set; } = string.Empty;
    public string? Process { get; set; } = null;   // null = 全局场景

    /// <summary>该场景下的所有动作页</summary>
    public List<ActionPage> ActionPages { get; set; } = new();

    /// <summary>
    /// 轮盘扇区绑定表：
    /// Key = "inner_N" / "inner_NE" / "outer_N" / "extended_N" ...
    /// Value = WheelBinding
    /// </summary>
    public Dictionary<string, WheelBinding> WheelBindings { get; set; } = new();

    /// <summary>是否在此软件下禁用轮盘菜单</summary>
    public bool DisableWheel { get; set; } = false;

    /// <summary>附加通用动作页（引用 global 场景中的动作页 ID 列表）</summary>
    public List<string> AttachedCommonPageIds { get; set; } = new();
}
```

---

## 四、UI 层次与页面结构（完整 XAML 骨架）

```xml
<!-- Settings/Views/Pages/ActionManagerPage.xaml -->
<Page x:Class="WheelMenu.Settings.Views.Pages.ActionManagerPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:vm="clr-namespace:WheelMenu.Settings.ViewModels"
      xmlns:ctrl="clr-namespace:WheelMenu.Settings.Controls">

  <Grid>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="170"/>   <!-- 左侧场景列表 -->
      <ColumnDefinition Width="1"/>     <!-- 分隔线 -->
      <ColumnDefinition Width="*"/>     <!-- 右侧内容区 -->
    </Grid.ColumnDefinitions>

    <!-- ════════════ 左侧：场景列表 ════════════ -->
    <Grid Grid.Column="0" Background="#F5F5F5">
      <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>  <!-- 顶部标题 -->
        <RowDefinition Height="*"/>     <!-- 场景 ListBox -->
        <RowDefinition Height="Auto"/>  <!-- 底部工具栏 -->
      </Grid.RowDefinitions>

      <TextBlock Grid.Row="0" Text="选择—" FontSize="11"
                 Foreground="#757575" Margin="8,6,0,4"/>

      <ListBox Grid.Row="1"
               x:Name="SceneListBox"
               ItemsSource="{Binding Scenes}"
               SelectedItem="{Binding SelectedScene}"
               BorderThickness="0" Background="Transparent"
               ScrollViewer.HorizontalScrollBarVisibility="Disabled">
        <ListBox.ItemTemplate>
          <DataTemplate DataType="{x:Type vm:SceneItemViewModel}">
            <Grid Height="42" Margin="0,1">
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width="28"/>
                <ColumnDefinition Width="*"/>
              </Grid.ColumnDefinitions>
              <!-- 场景图标（软件图标或默认图标）-->
              <Image Grid.Column="0" Width="16" Height="16"
                     Source="{Binding ProcessIcon}"
                     HorizontalAlignment="Center"
                     VerticalAlignment="Top" Margin="0,4,0,0"/>
              <StackPanel Grid.Column="1" VerticalAlignment="Center">
                <TextBlock Text="{Binding DisplayName}"
                           FontSize="13" FontWeight="SemiBold"
                           TextTrimming="CharacterEllipsis"/>
                <TextBlock Text="{Binding ProcessKey}"
                           FontSize="11" Foreground="#9E9E9E"
                           TextTrimming="CharacterEllipsis"/>
              </StackPanel>
            </Grid>
          </DataTemplate>
        </ListBox.ItemTemplate>
        <ListBox.ItemContainerStyle>
          <Style TargetType="ListBoxItem">
            <Setter Property="Padding" Value="4,0"/>
            <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
            <!-- 右键菜单 -->
            <Setter Property="ContextMenu">
              <Setter.Value>
                <ContextMenu>
                  <MenuItem Header="重命名"
                            Command="{Binding DataContext.RenameSceneCommand,
                                Source={x:Reference SceneListBox}}"
                            CommandParameter="{Binding}"/>
                  <Separator/>
                  <MenuItem Header="删除场景"
                            Command="{Binding DataContext.DeleteSceneCommand,
                                Source={x:Reference SceneListBox}}"
                            CommandParameter="{Binding}"/>
                </ContextMenu>
              </Setter.Value>
            </Setter>
          </Style>
        </ListBox.ItemContainerStyle>
      </ListBox>

      <!-- 底部：添加/编辑/删除 工具栏 -->
      <StackPanel Grid.Row="2" Orientation="Horizontal" Margin="4,4">
        <Button x:Name="AddSceneBtn" Width="28" Height="28"
                Content="+" FontSize="16"
                Command="{Binding AddSceneCommand}"
                ToolTip="添加场景"/>
        <Button x:Name="EditSceneBtn" Width="28" Height="28"
                Content="✎" Margin="2,0"
                Command="{Binding EditSceneCommand}"
                ToolTip="编辑场景"/>
        <Button x:Name="DeleteSceneBtn" Width="28" Height="28"
                Content="✕" Foreground="#F44336"
                Command="{Binding DeleteSceneCommand}"
                CommandParameter="{Binding SelectedScene}"
                ToolTip="删除场景"/>
        <Button x:Name="MoveUpBtn" Width="28" Height="28"
                Content="↑" Margin="2,0"
                Command="{Binding MoveSceneUpCommand}"
                ToolTip="上移"/>
      </StackPanel>
    </Grid>

    <!-- 分隔线 -->
    <Rectangle Grid.Column="1" Fill="#E0E0E0"/>

    <!-- ════════════ 右侧：内容区 ════════════ -->
    <Grid Grid.Column="2">
      <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>  <!-- 动作页 Tab 区 -->
        <RowDefinition Height="*"/>     <!-- 动作网格区 -->
        <RowDefinition Height="Auto"/>  <!-- 底部工具栏：添加动作页等 -->
        <RowDefinition Height="Auto"/>  <!-- 功能 Tab 区 -->
        <RowDefinition Height="Auto"/>  <!-- 禁用开关 -->
        <RowDefinition Height="280"/>   <!-- 轮盘预览 + 操作区 -->
      </Grid.RowDefinitions>

      <!-- ── 动作页 Tab（顶部横向滚动） ── -->
      <ScrollViewer Grid.Row="0"
                    HorizontalScrollBarVisibility="Auto"
                    VerticalScrollBarVisibility="Hidden">
        <ctrl:ActionPageTabBar
            ActionPages="{Binding ActionPages}"
            SelectedPage="{Binding SelectedActionPage, Mode=TwoWay}"
            PageReorderRequested="OnPageReorder"/>
      </ScrollViewer>

      <!-- ── 动作网格区（图标+名称，可拖拽） ── -->
      <ctrl:ActionGrid
          Grid.Row="1"
          x:Name="ActionGridCtrl"
          Actions="{Binding SelectedPageActions}"
          AllowDrop="True"
          Drop="OnActionGridDrop"
          DragItemRequested="OnActionDragStart"
          ActionDoubleClicked="OnActionDoubleClicked"
          ActionRightClicked="OnActionRightClicked"
          Margin="8,4"/>

      <!-- ── 底部工具栏 ── -->
      <StackPanel Grid.Row="2" Orientation="Horizontal" Margin="8,4">
        <Button Content="＋ 添加动作页"
                Command="{Binding AddActionPageCommand}"
                Padding="10,4" Margin="0,0,8,0"/>
        <Button Content="{Binding AttachCommonPageLabel}"
                Command="{Binding AttachCommonPageCommand}"
                Padding="10,4" Margin="0,0,8,0"/>
        <CheckBox Content="自动返回第一页"
                  IsChecked="{Binding AutoReturnToFirstPage}"
                  VerticalAlignment="Center"/>
      </StackPanel>

      <!-- ── 功能 Tab ── -->
      <TabControl Grid.Row="3" BorderThickness="0,1,0,0"
                  Padding="0" Margin="0,4,0,0">
        <TabItem Header="轮盘菜单" IsSelected="True">
          <!-- 内容在下方行，Tab 切换用 Visibility 控制 -->
        </TabItem>
        <TabItem Header="手势操作"/>
        <TabItem Header="左键辅助"/>
      </TabControl>

      <!-- ── 禁用开关 ── -->
      <CheckBox Grid.Row="4"
                Content="在此软件下禁用轮盘菜单"
                IsChecked="{Binding DisableWheelForScene}"
                Margin="12,6"/>

      <!-- ── 轮盘预览 + 右侧操作区 ── -->
      <Grid Grid.Row="5" Margin="8">
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width="*"/>    <!-- 圆盘预览 -->
          <ColumnDefinition Width="140"/>  <!-- 操作按钮区 -->
        </Grid.ColumnDefinitions>

        <!-- 圆盘预览（只读，但支持拖拽目标） -->
        <ctrl:WheelBindingCanvas
            Grid.Column="0"
            x:Name="WheelCanvas"
            Bindings="{Binding WheelBindings}"
            ActionPages="{Binding AllActionPagesForPreview}"
            AllowDrop="True"
            Drop="OnWheelSectorDrop"
            DragOver="OnWheelSectorDragOver"
            SectorClicked="OnWheelSectorClicked"
            Width="260" Height="260"
            HorizontalAlignment="Center"/>

        <!-- 右侧操作按钮 -->
        <StackPanel Grid.Column="1" VerticalAlignment="Center" Margin="8,0">
          <Button Content="复制当前轮盘设置"
                  Command="{Binding CopyWheelBindingsCommand}"
                  Height="32" Margin="0,0,0,6"/>
          <Button Content="清空轮盘"
                  Command="{Binding ClearWheelBindingsCommand}"
                  Height="32" Foreground="#F44336"
                  Margin="0,0,0,6"/>
          <Button Content="轮盘设置"
                  Command="{Binding OpenWheelSettingsCommand}"
                  Height="32"/>
        </StackPanel>
      </Grid>

    </Grid>
  </Grid>
</Page>
```

---

## 五、ActionManagerPageViewModel.cs（完整重写）

```csharp
// Settings/ViewModels/ActionManagerPageViewModel.cs
namespace WheelMenu.Settings.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WheelMenu.Settings.Models;

public partial class ActionManagerPageViewModel : ObservableObject
{
    private readonly WheelConfig _config;

    // ══════════════════════════════════════
    // 场景列表
    // ══════════════════════════════════════
    public ObservableCollection<SceneItemViewModel> Scenes { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionPages))]
    [NotifyPropertyChangedFor(nameof(WheelBindings))]
    [NotifyPropertyChangedFor(nameof(DisableWheelForScene))]
    [NotifyPropertyChangedFor(nameof(AttachCommonPageLabel))]
    [NotifyPropertyChangedFor(nameof(AllActionPagesForPreview))]
    private SceneItemViewModel? _selectedScene;

    partial void OnSelectedSceneChanged(SceneItemViewModel? value)
    {
        RefreshActionPages();
        SelectedActionPage = ActionPages.FirstOrDefault();
    }

    // ══════════════════════════════════════
    // 动作页列表（顶部 Tab）
    // ══════════════════════════════════════
    public ObservableCollection<ActionPageViewModel> ActionPages { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPageActions))]
    private ActionPageViewModel? _selectedActionPage;

    partial void OnSelectedActionPageChanged(ActionPageViewModel? value) =>
        RefreshSelectedPageActions();

    // ══════════════════════════════════════
    // 动作网格（当前动作页的所有动作）
    // ══════════════════════════════════════
    public ObservableCollection<ActionItemViewModel> SelectedPageActions { get; } = new();

    // ══════════════════════════════════════
    // 轮盘绑定（圆盘预览数据）
    // ══════════════════════════════════════
    /// <summary>
    /// Key = "inner_N" 等，Value = 显示在扇区中的内容
    /// </summary>
    public Dictionary<string, WheelSectorDisplayData> WheelBindings =>
        BuildWheelDisplayData();

    /// <summary>圆盘预览所需的所有动作页数据（含全局兜底）</summary>
    public List<ActionPage> AllActionPagesForPreview =>
        SelectedScene?.Scene.ActionPages ?? new();

    // ══════════════════════════════════════
    // 开关：禁用轮盘 / 附加通用页 / 自动返回
    // ══════════════════════════════════════
    public bool DisableWheelForScene
    {
        get => SelectedScene?.Scene.DisableWheel ?? false;
        set
        {
            if (SelectedScene == null) return;
            SelectedScene.Scene.DisableWheel = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    [ObservableProperty] private bool _autoReturnToFirstPage = false;

    public string AttachCommonPageLabel
    {
        get
        {
            int count = SelectedScene?.Scene.AttachedCommonPageIds.Count ?? 0;
            return $"附加通用动作页（{count}）";
        }
    }

    // ══════════════════════════════════════
    // 构造
    // ══════════════════════════════════════
    public ActionManagerPageViewModel(WheelConfig config)
    {
        _config = config;
        RefreshScenes();
    }

    // ══════════════════════════════════════
    // 场景 CRUD
    // ══════════════════════════════════════
    private void RefreshScenes()
    {
        Scenes.Clear();
        // 固定顺序：全局、通用、任务栏、桌面、软件场景
        var ordered = _config.Scenes
            .OrderBy(kv => kv.Key == "global"  ? 0 :
                           kv.Key == "common"  ? 1 :
                           kv.Key == "taskbar" ? 2 :
                           kv.Key == "desktop" ? 3 : 4)
            .ThenBy(kv => kv.Value.Name);

        foreach (var (key, scene) in ordered)
            Scenes.Add(new SceneItemViewModel(key, scene));

        SelectedScene = Scenes.FirstOrDefault();
    }

    [RelayCommand]
    private void AddScene()
    {
        var dlg = new Views.AddSceneDialog { Owner = GetOwner() };
        if (dlg.ShowDialog() != true) return;
        string key = dlg.ProcessName.ToLowerInvariant().Trim();
        if (string.IsNullOrEmpty(key) || _config.Scenes.ContainsKey(key)) return;
        _config.Scenes[key] = new SceneConfig
            { Name = dlg.DisplayName, Process = key };
        RefreshScenes();
        SelectedScene = Scenes.FirstOrDefault(s => s.Key == key);
        MarkDirty();
    }

    [RelayCommand]
    private void DeleteScene(SceneItemViewModel? item)
    {
        if (item == null || IsSystemScene(item.Key)) return;
        if (System.Windows.MessageBox.Show(
            $"确定删除场景 "{item.Scene.Name}"？",
            "确认", System.Windows.MessageBoxButton.YesNo)
            != System.Windows.MessageBoxResult.Yes) return;
        _config.Scenes.Remove(item.Key);
        RefreshScenes();
        MarkDirty();
    }

    [RelayCommand]
    private void RenameScene()
    {
        if (SelectedScene == null || IsSystemScene(SelectedScene.Key)) return;
        var dlg = new Views.InputDialog("重命名", "名称：", SelectedScene.Scene.Name)
            { Owner = GetOwner() };
        if (dlg.ShowDialog() != true) return;
        SelectedScene.Scene.Name = dlg.Result!;
        RefreshScenes();
        MarkDirty();
    }

    [RelayCommand]
    private void EditScene() => RenameScene();

    [RelayCommand]
    private void MoveSceneUp()
    {
        // 系统场景不参与排序
        // 软件场景可上移，影响显示顺序（存储到 SceneConfig.SortOrder）
    }

    private static bool IsSystemScene(string key) =>
        key is "global" or "common" or "taskbar" or "desktop";

    // ══════════════════════════════════════
    // 动作页 CRUD
    // ══════════════════════════════════════
    private void RefreshActionPages()
    {
        ActionPages.Clear();
        if (SelectedScene == null) return;
        foreach (var page in SelectedScene.Scene.ActionPages)
            ActionPages.Add(new ActionPageViewModel(page));
    }

    private void RefreshSelectedPageActions()
    {
        SelectedPageActions.Clear();
        if (SelectedActionPage == null) return;
        foreach (var action in SelectedActionPage.Page.Actions)
            SelectedPageActions.Add(new ActionItemViewModel(action));
    }

    [RelayCommand]
    private void AddActionPage()
    {
        if (SelectedScene == null) return;
        var dlg = new Views.InputDialog("添加动作页", "动作页名称：", "新动作页")
            { Owner = GetOwner() };
        if (dlg.ShowDialog() != true) return;
        var page = new ActionPage { Name = dlg.Result! };
        SelectedScene.Scene.ActionPages.Add(page);
        ActionPages.Add(new ActionPageViewModel(page));
        SelectedActionPage = ActionPages.Last();
        MarkDirty();
    }

    /// <summary>动作页 Tab 拖拽排序</summary>
    public void OnPageReorder(int fromIndex, int toIndex)
    {
        if (SelectedScene == null) return;
        var pages = SelectedScene.Scene.ActionPages;
        if (fromIndex < 0 || toIndex < 0 ||
            fromIndex >= pages.Count || toIndex >= pages.Count) return;
        var page = pages[fromIndex];
        pages.RemoveAt(fromIndex);
        pages.Insert(toIndex, page);
        RefreshActionPages();
        MarkDirty();
    }

    [RelayCommand]
    private void AttachCommonPage()
    {
        // 弹出通用场景动作页选择器
        // 选中后加入 AttachedCommonPageIds
        OnPropertyChanged(nameof(AttachCommonPageLabel));
        MarkDirty();
    }

    // ══════════════════════════════════════
    // 动作 CRUD（在动作网格中）
    // ══════════════════════════════════════

    /// <summary>双击动作格子 → 编辑该动作</summary>
    public void OnActionDoubleClicked(ActionItemViewModel item)
    {
        var dlg = new Controls.ActionEditDialog(item.Action)
            { Owner = GetOwner() };
        if (dlg.ShowDialog() != true) return;
        item.RefreshFromModel();
        MarkDirty();
    }

    /// <summary>右键动作格子 → 上下文菜单</summary>
    public void OnActionRightClicked(ActionItemViewModel item,
        System.Windows.Point pos)
    {
        // 由 Code-Behind 构建 ContextMenu（见 ActionManagerPage.xaml.cs）
        _lastRightClickedAction = item;
    }

    private ActionItemViewModel? _lastRightClickedAction;

    [RelayCommand]
    private void DeleteAction()
    {
        if (_lastRightClickedAction == null || SelectedActionPage == null) return;
        SelectedActionPage.Page.Actions.Remove(_lastRightClickedAction.Action);
        SelectedPageActions.Remove(_lastRightClickedAction);
        OnPropertyChanged(nameof(WheelBindings));
        MarkDirty();
    }

    [RelayCommand]
    private void EditAction()
    {
        if (_lastRightClickedAction != null)
            OnActionDoubleClicked(_lastRightClickedAction);
    }

    // ══════════════════════════════════════
    // 拖拽：动作网格 → 轮盘扇区
    // ══════════════════════════════════════

    /// <summary>
    /// 动作被拖拽到圆盘某扇区时调用。
    /// ring    = "inner" / "outer" / "extended"
    /// direction = "N" / "NE" / "E" / "SE" / "S" / "SW" / "W" / "NW"
    /// draggedItem = 来自动作网格的 ActionItemViewModel
    /// </summary>
    public void BindActionToSector(
        string ring, string direction, ActionItemViewModel draggedItem)
    {
        if (SelectedScene == null) return;

        string key = $"{ring}_{direction}";
        SelectedScene.Scene.WheelBindings[key] = new WheelBinding
        {
            Direction   = direction,
            Ring        = ring,
            ActionItemId = draggedItem.Action.Id,
            ActionPageId = null   // 直接绑定单个动作，不绑定整页
        };

        // 刷新圆盘预览
        OnPropertyChanged(nameof(WheelBindings));
        MarkDirty();
    }

    /// <summary>
    /// 动作页 Tab 被拖拽到圆盘某扇区 → 整个动作页绑定到该扇区
    /// </summary>
    public void BindPageToSector(
        string ring, string direction, ActionPageViewModel draggedPage)
    {
        if (SelectedScene == null) return;

        string key = $"{ring}_{direction}";
        SelectedScene.Scene.WheelBindings[key] = new WheelBinding
        {
            Direction    = direction,
            Ring         = ring,
            ActionPageId = draggedPage.Page.Id,
            ActionItemId = null
        };

        OnPropertyChanged(nameof(WheelBindings));
        MarkDirty();
    }

    /// <summary>
    /// 圆盘扇区之间互相拖拽（交换绑定）
    /// </summary>
    public void SwapSectorBindings(
        string ringA, string dirA, string ringB, string dirB)
    {
        if (SelectedScene == null) return;
        var bindings = SelectedScene.Scene.WheelBindings;
        string keyA  = $"{ringA}_{dirA}";
        string keyB  = $"{ringB}_{dirB}";

        bindings.TryGetValue(keyA, out var bindA);
        bindings.TryGetValue(keyB, out var bindB);

        if (bindA != null) { bindA.Direction = dirB; bindA.Ring = ringB; bindings[keyB] = bindA; }
        else               bindings.Remove(keyB);

        if (bindB != null) { bindB.Direction = dirA; bindB.Ring = ringA; bindings[keyA] = bindB; }
        else               bindings.Remove(keyA);

        OnPropertyChanged(nameof(WheelBindings));
        MarkDirty();
    }

    // ══════════════════════════════════════
    // 轮盘操作命令
    // ══════════════════════════════════════
    [RelayCommand]
    private void CopyWheelBindings()
    {
        if (SelectedScene == null) return;
        var json = System.Text.Json.JsonSerializer.Serialize(
            SelectedScene.Scene.WheelBindings);
        System.Windows.Clipboard.SetText(json);
        System.Windows.MessageBox.Show("轮盘设置已复制到剪贴板。", "复制成功");
    }

    [RelayCommand]
    private void ClearWheelBindings()
    {
        if (SelectedScene == null) return;
        if (System.Windows.MessageBox.Show(
            "确定清空当前场景的所有轮盘绑定？",
            "确认清空",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning)
            != System.Windows.MessageBoxResult.Yes) return;
        SelectedScene.Scene.WheelBindings.Clear();
        OnPropertyChanged(nameof(WheelBindings));
        MarkDirty();
    }

    [RelayCommand]
    private void OpenWheelSettings()
    {
        // 跳转到全局参数页（轮盘设置）
        // 由外层 SettingsWindow 处理导航
        NavigateToSettingsRequested?.Invoke();
    }

    public event Action? NavigateToSettingsRequested;

    // ══════════════════════════════════════
    // 圆盘预览数据构建
    // ══════════════════════════════════════
    private Dictionary<string, WheelSectorDisplayData> BuildWheelDisplayData()
    {
        var result  = new Dictionary<string, WheelSectorDisplayData>();
        if (SelectedScene == null) return result;

        var bindings   = SelectedScene.Scene.WheelBindings;
        var allPages   = SelectedScene.Scene.ActionPages;
        var globalPages = _config.Scenes.GetValueOrDefault("global")?.ActionPages
                          ?? new List<ActionPage>();

        var dirs = new[] { "N","NE","E","SE","S","SW","W","NW" };
        foreach (string ring in new[] { "inner", "outer", "extended" })
        foreach (string dir  in dirs)
        {
            string key = $"{ring}_{dir}";
            if (!bindings.TryGetValue(key, out var binding))
            {
                result[key] = WheelSectorDisplayData.Empty;
                continue;
            }

            // 找到绑定的动作页或单个动作
            if (!string.IsNullOrEmpty(binding.ActionPageId))
            {
                var page = allPages.FirstOrDefault(p => p.Id == binding.ActionPageId)
                        ?? globalPages.FirstOrDefault(p => p.Id == binding.ActionPageId);
                result[key] = page != null
                    ? new WheelSectorDisplayData
                    {
                        Label      = page.Name,
                        Icon       = null,  // 动作页无图标（或取第一个动作的图标）
                        IsPageLink = true
                    }
                    : WheelSectorDisplayData.Unlinked;
            }
            else if (!string.IsNullOrEmpty(binding.ActionItemId))
            {
                ActionItem? action = null;
                foreach (var page in allPages)
                {
                    action = page.Actions.FirstOrDefault(
                        a => a.Id == binding.ActionItemId);
                    if (action != null) break;
                }
                result[key] = action != null
                    ? new WheelSectorDisplayData
                    {
                        Label      = action.Name,
                        IconPath   = action.IconPath,
                        IsPageLink = false
                    }
                    : WheelSectorDisplayData.Unlinked;
            }
        }
        return result;
    }

    // ══════════════════════════════════════
    // 工具
    // ══════════════════════════════════════
    private void MarkDirty() => OnPropertyChanged(string.Empty);

    private static System.Windows.Window? GetOwner() =>
        System.Windows.Application.Current.Windows
            .OfType<System.Windows.Window>()
            .FirstOrDefault(w => w.IsActive);
}

// ══════════════════════════════════════
// 辅助 ViewModel
// ══════════════════════════════════════

public class ActionPageViewModel(ActionPage page)
{
    public ActionPage Page        { get; } = page;
    public string     DisplayName => Page.Name;
}

public partial class ActionItemViewModel : ObservableObject
{
    public ActionItem Action { get; }

    [ObservableProperty] private string  _displayName = string.Empty;
    [ObservableProperty] private string? _iconPath;

    public ActionItemViewModel(ActionItem action)
    {
        Action = action;
        RefreshFromModel();
    }

    public void RefreshFromModel()
    {
        DisplayName = Action.Name;
        IconPath    = Action.IconPath;
    }
}

/// <summary>圆盘扇区显示数据（传给 WheelBindingCanvas）</summary>
public class WheelSectorDisplayData
{
    public string  Label      { get; set; } = string.Empty;
    public string? IconPath   { get; set; }
    public System.Windows.Media.ImageSource? Icon { get; set; }
    public bool    IsPageLink { get; set; }   // true=指向动作页，false=直接动作
    public bool    IsEmpty    { get; set; }
    public bool    IsUnlinked { get; set; }   // 目标已被删除

    public static WheelSectorDisplayData Empty    =>
        new() { IsEmpty = true };
    public static WheelSectorDisplayData Unlinked =>
        new() { IsUnlinked = true, Label = "⚠ 失联" };
}
```

---

## 六、WheelBindingCanvas.cs（支持拖拽的圆盘预览控件）

```csharp
// Settings/Controls/WheelBindingCanvas.cs
namespace WheelMenu.Settings.Controls;

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WheelMenu.Renderer;
using WheelMenu.Settings.ViewModels;

/// <summary>
/// 动作管理页中的圆盘预览控件。
/// 职责：
///   1. 绘制每个扇区的绑定状态（名称/图标/空/失联）
///   2. 作为拖拽目标，接收动作网格或动作页 Tab 的拖拽
///   3. 扇区之间支持互相拖拽交换
///   4. 点击扇区选中并高亮
/// </summary>
public class WheelBindingCanvas : FrameworkElement
{
    // ══ 依赖属性 ══

    public static readonly DependencyProperty BindingsProperty =
        DependencyProperty.Register(nameof(Bindings),
            typeof(Dictionary<string, WheelSectorDisplayData>),
            typeof(WheelBindingCanvas),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public Dictionary<string, WheelSectorDisplayData>? Bindings
    {
        get => (Dictionary<string, WheelSectorDisplayData>?)GetValue(BindingsProperty);
        set => SetValue(BindingsProperty, value);
    }

    // ══ 拖拽目标事件 ══
    /// <summary>
    /// 有动作/动作页被拖拽到某扇区上时触发。
    /// 参数：(ring, direction, dragData)
    /// dragData 可能是 ActionItemViewModel 或 ActionPageViewModel
    /// </summary>
    public event Action<string, string, object>? DropOnSector;

    /// <summary>扇区之间互相拖拽交换时触发</summary>
    public event Action<string, string, string, string>? SectorSwap;

    public event EventHandler<(string Ring, string Direction)>? SectorClicked;

    // ══ 内部状态 ══
    private string _hoverRing      = string.Empty;
    private string _hoverDirection = string.Empty;
    private string _selectedRing   = string.Empty;
    private string _selectedDir    = string.Empty;
    private string _dragSourceRing = string.Empty;
    private string _dragSourceDir  = string.Empty;

    private static readonly string[] Dirs8 =
        { "N","NE","E","SE","S","SW","W","NW" };

    // ══ 绘制 ══
    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth < 1) return;
        double cx = ActualWidth  / 2;
        double cy = ActualHeight / 2;
        double scale = Math.Min(ActualWidth, ActualHeight) / WheelConstants.WheelDiameter;
        double deadR  = WheelConstants.DeadZoneRadius  * scale;
        double innerR = WheelConstants.InnerRingRadius * scale;
        double outerR = WheelConstants.OuterRingRadius * scale;

        DrawRingSectors(dc, cx, cy, deadR, innerR, outerR, "inner");
        DrawRingSectors(dc, cx, cy, innerR, outerR, outerR * 1.4, "outer");
        DrawDividerLines(dc, cx, cy, deadR, outerR * 1.4);
        DrawSectorLabels(dc, cx, cy, deadR, innerR, outerR);
        DrawDeadZone(dc, cx, cy, deadR);
        DrawDragHighlight(dc, cx, cy, deadR, innerR, outerR * 1.4);
    }

    private void DrawRingSectors(DrawingContext dc,
        double cx, double cy, double rIn, double rOut, double rOutActual,
        string ring)
    {
        for (int i = 0; i < 8; i++)
        {
            string dir = Dirs8[i];
            string key = $"{ring}_{dir}";
            var    data = Bindings?.GetValueOrDefault(key);

            double step       = 360.0 / 8;
            double startAngle = i * step - step / 2.0;
            double endAngle   = startAngle + step;
            var    geo        = WheelGeometry.CreateSectorRing(
                cx, cy, rIn, rOut, startAngle, endAngle);

            // 背景色
            Color fill = data switch
            {
                { IsEmpty: true }    => Color.FromArgb(0,   255, 255, 255),  // 透明
                { IsUnlinked: true } => Color.FromRgb(255, 235, 238),        // 淡红=失联
                { IsPageLink: true } => Color.FromRgb(232, 245, 233),        // 淡绿=动作页
                _                    => Color.FromRgb(255, 253, 231)         // 淡黄=单动作
            };

            // 悬停/选中高亮
            bool hover = _hoverRing == ring && _hoverDirection == dir;
            bool sel   = _selectedRing == ring && _selectedDir == dir;
            if (hover) fill = Lighten(fill, -15);
            if (sel)   fill = Color.FromArgb(80, 25, 118, 210);

            dc.DrawGeometry(new SolidColorBrush(fill), null, geo);

            // 选中边框
            if (sel)
            {
                var pen = new Pen(new SolidColorBrush(Color.FromRgb(25, 118, 210)), 2);
                dc.DrawGeometry(null, pen, geo);
            }
        }
    }

    private void DrawDividerLines(DrawingContext dc,
        double cx, double cy, double innerR, double outerR)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)), 1);
        for (int i = 0; i < 8; i++)
        {
            double angle = i * 45.0 - 90.0;
            var p1 = WheelGeometry.PolarToPoint(cx, cy, innerR, angle);
            var p2 = WheelGeometry.PolarToPoint(cx, cy, outerR, angle);
            dc.DrawLine(pen, p1, p2);
        }
        // 圆环线
        var ringPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), 1);
        dc.DrawEllipse(null, ringPen, new Point(cx, cy),
            WheelConstants.InnerRingRadius * (Math.Min(ActualWidth, ActualHeight) / WheelConstants.WheelDiameter),
            WheelConstants.InnerRingRadius * (Math.Min(ActualWidth, ActualHeight) / WheelConstants.WheelDiameter));
    }

    private void DrawSectorLabels(DrawingContext dc,
        double cx, double cy, double deadR, double innerR, double outerR)
    {
        if (Bindings == null) return;
        double scale = Math.Min(ActualWidth, ActualHeight) / WheelConstants.WheelDiameter;
        double step  = 360.0 / 8;

        for (int i = 0; i < 8; i++)
        {
            string dir  = Dirs8[i];
            string key  = $"inner_{dir}";
            var    data = Bindings.GetValueOrDefault(key);
            if (data == null || data.IsEmpty) continue;

            double angle  = i * step;
            var    center = WheelGeometry.SectorCenterPoint(cx, cy, deadR, innerR, angle);

            string text  = data.IsUnlinked ? "⚠ 失联"
                         : data.Label.Length > 6
                           ? data.Label[..6] + "…" : data.Label;

            var ft = new FormattedText(text,
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei UI"),
                9.5 * scale,
                data.IsUnlinked
                    ? new SolidColorBrush(Color.FromRgb(183, 28, 28))
                    : new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                MaxTextWidth = (innerR - deadR) * 0.85,
                MaxLineCount = 2,
                Trimming     = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center
            };

            dc.DrawText(ft, new Point(center.X - ft.Width / 2,
                                      center.Y - ft.Height / 2));
        }
    }

    private void DrawDeadZone(DrawingContext dc,
        double cx, double cy, double deadR)
    {
        double scale = Math.Min(ActualWidth, ActualHeight) / WheelConstants.WheelDiameter;
        dc.DrawEllipse(
            new SolidColorBrush(Color.FromRgb(245, 245, 245)),
            new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), 1),
            new Point(cx, cy), deadR, deadR);
    }

    private void DrawDragHighlight(DrawingContext dc,
        double cx, double cy, double deadR, double innerR, double outerR)
    {
        if (string.IsNullOrEmpty(_hoverRing)) return;
        // 拖拽悬停时：在圆盘外圈上方显示扇区名提示（如 "内圈 · 右上方向"）
    }

    // ══ 鼠标交互 ══
    protected override void OnMouseMove(MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        HitSector(pos.X, pos.Y, out string ring, out string dir);
        if (ring != _hoverRing || dir != _hoverDirection)
        {
            _hoverRing      = ring;
            _hoverDirection = dir;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _hoverRing = _hoverDirection = string.Empty;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        HitSector(pos.X, pos.Y, out string ring, out string dir);
        if (!string.IsNullOrEmpty(ring))
        {
            _selectedRing = ring;
            _selectedDir  = dir;
            InvalidateVisual();
            SectorClicked?.Invoke(this, (ring, dir));

            // 开始扇区→扇区拖拽
            _dragSourceRing = ring;
            _dragSourceDir  = dir;
            DragDrop.DoDragDrop(this,
                new SectorDragData { Ring = ring, Direction = dir },
                DragDropEffects.Move);
        }
    }

    // ══ 拖拽目标处理 ══
    protected override void OnDragOver(DragEventArgs e)
    {
        var pos = e.GetPosition(this);
        HitSector(pos.X, pos.Y, out string ring, out string dir);
        _hoverRing      = ring;
        _hoverDirection = dir;
        InvalidateVisual();
        e.Effects = string.IsNullOrEmpty(ring)
            ? DragDropEffects.None : DragDropEffects.Move;
        e.Handled = true;
    }

    protected override void OnDrop(DragEventArgs e)
    {
        var pos = e.GetPosition(this);
        HitSector(pos.X, pos.Y, out string ring, out string dir);
        if (string.IsNullOrEmpty(ring)) return;

        var data = e.Data;

        // 情况1：动作网格拖来的 ActionItemViewModel
        if (data.GetDataPresent(typeof(ActionItemViewModel)))
        {
            var item = (ActionItemViewModel)data.GetData(typeof(ActionItemViewModel))!;
            DropOnSector?.Invoke(ring, dir, item);
        }
        // 情况2：动作页 Tab 拖来的 ActionPageViewModel
        else if (data.GetDataPresent(typeof(ActionPageViewModel)))
        {
            var page = (ActionPageViewModel)data.GetData(typeof(ActionPageViewModel))!;
            DropOnSector?.Invoke(ring, dir, page);
        }
        // 情况3：扇区之间互换
        else if (data.GetDataPresent(typeof(SectorDragData)))
        {
            var src = (SectorDragData)data.GetData(typeof(SectorDragData))!;
            if (src.Ring != ring || src.Direction != dir)
                SectorSwap?.Invoke(src.Ring, src.Direction, ring, dir);
        }

        _hoverRing = _hoverDirection = string.Empty;
        InvalidateVisual();
    }

    // ══ Hit Test ══
    private void HitSector(double mx, double my,
        out string ring, out string direction)
    {
        double cx    = ActualWidth  / 2;
        double cy    = ActualHeight / 2;
        double scale = Math.Min(ActualWidth, ActualHeight) / WheelConstants.WheelDiameter;
        double deadR  = WheelConstants.DeadZoneRadius  * scale;
        double innerR = WheelConstants.InnerRingRadius * scale;
        double outerR = WheelConstants.OuterRingRadius * scale;

        double dx   = mx - cx;
        double dy   = my - cy;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        if (dist <= deadR)  { ring = direction = string.Empty; return; }

        ring = dist <= innerR ? "inner"
             : dist <= outerR ? "outer"
             : string.Empty;

        if (string.IsNullOrEmpty(ring)) { direction = string.Empty; return; }

        double angle = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
        if (angle < 0) angle += 360.0;
        int idx = (int)Math.Floor((angle + 22.5) / 45.0) % 8;
        direction = Dirs8[idx];
    }

    private static Color Lighten(Color c, int amount)
    {
        return Color.FromArgb(c.A,
            (byte)Math.Clamp(c.R + amount, 0, 255),
            (byte)Math.Clamp(c.G + amount, 0, 255),
            (byte)Math.Clamp(c.B + amount, 0, 255));
    }

    public class SectorDragData
    {
        public string Ring      { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
    }
}
```

---

## 七、ActionGrid.cs（动作网格控件，支持拖拽源）

```csharp
// Settings/Controls/ActionGrid.cs
namespace WheelMenu.Settings.Controls;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WheelMenu.Settings.ViewModels;

/// <summary>
/// 动作网格：显示当前动作页的所有动作，4列图标+名称。
/// 支持：
///   - 拖拽到 WheelBindingCanvas 上绑定到扇区
///   - 格子内部拖拽排序
///   - 双击编辑
///   - 右键菜单
/// </summary>
public class ActionGrid : ItemsControl
{
    public event EventHandler<ActionItemViewModel>? ActionDoubleClicked;
    public event EventHandler<(ActionItemViewModel Item, Point Pos)>? ActionRightClicked;
    public event EventHandler<ActionItemViewModel>? DragItemRequested;

    public ActionGrid()
    {
        AllowDrop = true;
        Drop  += OnSelfDrop;  // 内部排序
    }

    protected override DependencyObject GetContainerForItemOverride() =>
        new ActionGridCell();

    protected override bool IsItemItsOwnContainerOverride(object item) =>
        item is ActionGridCell;

    protected override void PrepareContainerForItemOverride(
        DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is ActionGridCell cell && item is ActionItemViewModel vm)
        {
            cell.ViewModel = vm;
            cell.PreviewMouseLeftButtonDown += (s, e) =>
                OnCellMouseDown(cell, vm, e);
            cell.MouseDoubleClick += (s, e) =>
                ActionDoubleClicked?.Invoke(this, vm);
            cell.MouseRightButtonDown += (s, e) =>
                ActionRightClicked?.Invoke(this, (vm, e.GetPosition(this)));
        }
    }

    private Point _dragStartPos;

    private void OnCellMouseDown(ActionGridCell cell,
        ActionItemViewModel vm, MouseButtonEventArgs e)
    {
        _dragStartPos = e.GetPosition(this);

        // 等待 MouseMove 才开始拖拽（防止误触）
        cell.PreviewMouseMove += OnCellMouseMove;
        cell.PreviewMouseLeftButtonUp += (s, _) =>
            cell.PreviewMouseMove -= OnCellMouseMove;

        void OnCellMouseMove(object s, MouseEventArgs me)
        {
            var cur  = me.GetPosition(this);
            var diff = cur - _dragStartPos;
            if (Math.Abs(diff.X) < 5 && Math.Abs(diff.Y) < 5) return;

            cell.PreviewMouseMove -= OnCellMouseMove;
            DragItemRequested?.Invoke(this, vm);

            // 开始 WPF DragDrop
            var data = new DataObject(typeof(ActionItemViewModel), vm);
            DragDrop.DoDragDrop(cell, data, DragDropEffects.Move | DragDropEffects.Copy);
        }
    }

    private void OnSelfDrop(object sender, DragEventArgs e)
    {
        // 动作格子内部排序（拖拽到不同位置）
        if (!e.Data.GetDataPresent(typeof(ActionItemViewModel))) return;
        var vm = (ActionItemViewModel)e.Data.GetData(typeof(ActionItemViewModel))!;
        var targetCell = FindCell(e.GetPosition(this));
        if (targetCell?.ViewModel == null || targetCell.ViewModel == vm) return;

        var items = (ObservableCollection<ActionItemViewModel>)ItemsSource;
        int from  = items.IndexOf(vm);
        int to    = items.IndexOf(targetCell.ViewModel);
        if (from < 0 || to < 0 || from == to) return;

        items.Move(from, to);
        // 同步到 Model
        var page = vm.Action; // 找到 page 并更新顺序（由 VM 处理）
        e.Handled = true;
    }

    private ActionGridCell? FindCell(Point pos)
    {
        var hit = VisualTreeHelper.HitTest(this, pos)?.VisualHit;
        while (hit != null && hit != this)
        {
            if (hit is ActionGridCell cell) return cell;
            hit = VisualTreeHelper.GetParent(hit);
        }
        return null;
    }
}

/// <summary>动作网格中的单个格子</summary>
public class ActionGridCell : ContentControl
{
    public ActionItemViewModel? ViewModel { get; set; }

    static ActionGridCell()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ActionGridCell),
            new FrameworkPropertyMetadata(typeof(ActionGridCell)));
    }
}
```

---

## 八、ActionManagerPage.xaml.cs（Code-Behind）

```csharp
// Settings/Views/Pages/ActionManagerPage.xaml.cs
namespace WheelMenu.Settings.Views.Pages;

using System.Windows;
using System.Windows.Controls;
using WheelMenu.Settings.ViewModels;

public partial class ActionManagerPage : Page
{
    private ActionManagerPageViewModel Vm =>
        (ActionManagerPageViewModel)DataContext;

    public ActionManagerPage() => InitializeComponent();

    // ── 动作页 Tab 排序 ──
    private void OnPageReorder(int from, int to) =>
        Vm.OnPageReorder(from, to);

    // ── 动作网格拖拽源 ──
    private void OnActionDragStart(object sender, ActionItemViewModel item)
    {
        // DragDrop 已在 ActionGrid 内部启动，此处可记录日志
    }

    // ── 圆盘扇区接收拖拽 ──
    private void OnWheelSectorDrop(object sender,
        System.Windows.DragEventArgs e)
    {
        // 取拖拽位置对应扇区（由 WheelBindingCanvas 内部处理）
    }

    private void OnWheelSectorDragOver(object sender,
        System.Windows.DragEventArgs e)
    {
        e.Effects = System.Windows.DragDropEffects.Move;
        e.Handled = true;
    }

    // ── 圆盘控件事件转发 ──
    private void OnWheelBindingCanvasDrop(string ring, string direction, object dragData)
    {
        switch (dragData)
        {
            case ActionItemViewModel actionVm:
                Vm.BindActionToSector(ring, direction, actionVm);
                break;
            case ActionPageViewModel pageVm:
                Vm.BindPageToSector(ring, direction, pageVm);
                break;
        }
    }

    private void OnWheelSectorSwap(string ringA, string dirA,
        string ringB, string dirB) =>
        Vm.SwapSectorBindings(ringA, dirA, ringB, dirB);

    private void OnWheelSectorClicked(object sender,
        (string Ring, string Direction) e)
    {
        // 选中扇区后可在右侧显示详情（可选）
    }

    // ── 动作双击/右键 ──
    private void OnActionDoubleClicked(object sender, ActionItemViewModel item) =>
        Vm.OnActionDoubleClicked(item);

    private void OnActionRightClicked(object sender,
        (ActionItemViewModel Item, Point Pos) e)
    {
        var menu = new ContextMenu();
        var edit = new MenuItem { Header = "编辑动作" };
        edit.Click += (_, _) => Vm.EditActionCommand.Execute(null);
        var del = new MenuItem { Header = "删除动作", Foreground =
            System.Windows.Media.Brushes.Red };
        del.Click += (_, _) => Vm.DeleteActionCommand.Execute(null);
        menu.Items.Add(edit);
        menu.Items.Add(new Separator());
        menu.Items.Add(del);
        menu.IsOpen = true;
    }
}
```

---

## 九、验收清单（基于真实截图）

**场景列表：**
- [ ] 左侧显示全局/通用/任务栏/桌面及软件场景，每行双行文字（名称+进程名）
- [ ] 底部工具栏：`+` / 编辑 / 删除 / 上移四个按钮
- [ ] 全局/通用/任务栏/桌面四个系统场景不可删除

**动作页 Tab：**
- [ ] 顶部横向滚动 Tab，每个 Tab 对应一个动作页
- [ ] Tab 可拖拽排序（左右交换位置）
- [ ] `+添加动作页` 按钮点击后弹出命名对话框

**动作网格：**
- [ ] 网格显示 4 列，每格显示图标+名称
- [ ] 格子可在网格内拖拽排序
- [ ] 格子拖拽到圆盘扇区后，扇区显示该动作名称
- [ ] 双击格子弹出动作编辑弹窗
- [ ] 右键格子出现"编辑/删除"菜单

**圆盘预览：**
- [ ] 显示内圈 8 个扇区，每个扇区显示绑定的动作名称
- [ ] 空扇区显示透明/白色
- [ ] 失联绑定显示红色"⚠ 失联"
- [ ] 动作页绑定显示淡绿色背景
- [ ] 扇区悬停时边框高亮
- [ ] 拖拽悬停到扇区上时扇区蓝色高亮提示
- [ ] 扇区之间可互相拖拽交换绑定

**右侧操作区：**
- [ ] "复制当前轮盘设置"将绑定序列化到剪贴板
- [ ] "清空轮盘"弹出确认后清除所有绑定
- [ ] "轮盘设置"跳转到全局参数页

**开关：**
- [ ] "在此软件下禁用轮盘菜单"勾选后存入配置
- [ ] "自动返回第一页"勾选状态正确持久化
