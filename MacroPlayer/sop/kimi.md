# 键盘宏播放器 - 详细开发SOP
## 项目代号：MacroPlayer v1.0

---

## 📋 项目概述

**目标**：开发一个基于.NET 8的Windows桌面键盘宏播放器，支持全局热键触发、按键序列模拟、配置持久化。

**交付物**：单EXE文件，无需安装，托盘运行

**技术栈**：
- 运行时：.NET 8
- UI框架：WPF (Windows Presentation Foundation)
- 核心API：Windows API (user32.dll, kernel32.dll)
- 配置格式：JSON
- 开发环境：VS Code + C# Dev Kit

---

## 🏗️ 第一阶段：项目初始化

### 1.1 环境准备

**操作步骤**：
1. 安装 .NET 8 SDK
   - 下载地址：https://dotnet.microsoft.com/download/dotnet/8.0
   - 验证安装：`dotnet --version` 应显示 8.0.x

2. 安装 VS Code 及插件
   - 安装 C# Dev Kit (Microsoft官方)
   - 安装 .NET Extension Pack
   - 安装 NuGet Package Manager GUI

3. 创建项目目录结构
   ```
   MacroPlayer/
   ├── src/
   │   ├── MacroPlayer.Core/          # 核心逻辑类库
   │   ├── MacroPlayer.UI/            # WPF界面
   │   └── MacroPlayer.sln
   ├── docs/                          # 文档
   └── publish/                       # 发布输出
   ```

### 1.2 创建解决方案

**VS Code终端操作**：
```bash
# 创建根目录
mkdir MacroPlayer && cd MacroPlayer

# 创建解决方案
dotnet new sln -n MacroPlayer

# 创建核心类库
dotnet new classlib -n MacroPlayer.Core -o src/MacroPlayer.Core

# 创建WPF应用
dotnet new wpf -n MacroPlayer.UI -o src/MacroPlayer.UI

# 添加到解决方案
dotnet sln add src/MacroPlayer.Core/MacroPlayer.Core.csproj
dotnet sln add src/MacroPlayer.UI/MacroPlayer.UI.csproj

# 添加项目引用 (UI引用Core)
cd src/MacroPlayer.UI
dotnet add reference ../MacroPlayer.Core/MacroPlayer.Core.csproj
```

### 1.3 项目文件配置

**MacroPlayer.Core.csproj 配置**：
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UseWindowsForms>true</UseWindowsForms>  <!-- 用于SendKeys -->
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.Text.Json" Version="8.0.0" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />  <!-- 备选 -->
  </ItemGroup>
</Project>
```

**MacroPlayer.UI.csproj 配置**：
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <PublishSingleFile>true</PublishSingleFile>  <!-- 单文件发布 -->
    <SelfContained>true</SelfContained>          <!-- 独立运行时 -->
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\MacroPlayer.Core\MacroPlayer.Core.csproj" />
    <PackageReference Include="Hardcodet.NotifyIcon.Wpf" Version="1.1.0" />  <!-- 托盘图标 -->
  </ItemGroup>
</Project>
```

---

## 🧩 第二阶段：核心模块开发

### 2.1 宏规则模块 (MacroPlayer.Core)

**文件位置**：`src/MacroPlayer.Core/Models/MacroDefinition.cs`

**数据结构定义**：

```csharp
namespace MacroPlayer.Core.Models;

/// <summary>
/// 宏定义 - 包含触发键和按键序列
/// </summary>
public class MacroDefinition
{
    /// <summary>宏名称（如"技能连招1"）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>触发热键（如"F4"）</summary>
    public string Hotkey { get; set; } = string.Empty;

    /// <summary>按键序列</summary>
    public List<KeyAction> KeySequence { get; set; } = new();

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>循环模式：0=单次，1=按住循环，2=开关切换</summary>
    public int LoopMode { get; set; } = 0;
}

/// <summary>
/// 单个按键动作
/// </summary>
public class KeyAction
{
    /// <summary>虚拟键码 (Virtual Key Code)</summary>
    public int KeyCode { get; set; }

    /// <summary>按键显示名称（如"D"）</summary>
    public string KeyName { get; set; } = string.Empty;

    /// <summary>按下持续时间(ms)</summary>
    public int PressDuration { get; set; } = 50;

    /// <summary>抬起后延迟(ms)</summary>
    public int DelayAfter { get; set; } = 50;

    /// <summary>动作类型：0=按下并抬起，1=仅按下，2=仅抬起</summary>
    public int ActionType { get; set; } = 0;
}
```

**文件位置**：`src/MacroPlayer.Core/Services/MacroParser.cs`

**功能**：字符串宏解析器
- 输入：`"D(50ms) D(50ms) Q(50ms) Q(50ms)"`
- 输出：`List<KeyAction>`

**解析规则**：
1. 基础格式：`KEY` 或 `KEY(duration)` 或 `KEY(duration,delay)`
2. 示例：
   - `D` → D键，默认50ms按下，50ms延迟
   - `D(100)` → D键，100ms按下，默认延迟
   - `D(100,200)` → D键，100ms按下，200ms延迟
3. 支持组合键：`Ctrl+C` 解析为 Ctrl按下→C按下→C抬起→Ctrl抬起

### 2.2 热键监听模块

**文件位置**：`src/MacroPlayer.Core/Services/HotkeyManager.cs`

**技术方案**：
- 使用 `RegisterHotKey` / `UnregisterHotKey` (user32.dll)
- 使用 `WM_HOTKEY` 消息循环
- 需要窗口句柄接收消息

**关键常量定义**：
```csharp
// 修饰键
public const int MOD_NONE = 0x0000;
public const int MOD_ALT = 0x0001;
public const int MOD_CONTROL = 0x0002;
public const int MOD_SHIFT = 0x0004;
public const int MOD_WIN = 0x0008;

// Windows消息
public const int WM_HOTKEY = 0x0312;
```

**类设计**：
```csharp
public class HotkeyManager : IDisposable
{
    // 事件：热键触发时通知
    public event EventHandler<string>? HotkeyTriggered;

    // 注册热键
    public bool RegisterHotkey(IntPtr hWnd, string hotkeyId, uint modifiers, uint vkCode);

    // 注销热键
    public bool UnregisterHotkey(IntPtr hWnd, string hotkeyId);

    // 处理Windows消息
    public void ProcessHotkeyMessage(Message msg);
}
```

**常见虚拟键码对照表**（需硬编码）：
| 按键 | VK Code | 按键 | VK Code |
|------|---------|------|---------|
| F1-F12 | 0x70-0x7B | 0-9 | 0x30-0x39 |
| A-Z | 0x41-0x5A | Space | 0x20 |
| Ctrl | 0x11 | Shift | 0x10 |
| Alt | 0x12 | Tab | 0x09 |

### 2.3 按键模拟模块

**文件位置**：`src/MacroPlayer.Core/Services/InputSimulator.cs`

**技术方案选择**：

**方案A：SendInput API (推荐)**
- 优点：最稳定，支持硬件级模拟
- 缺点：需要P/Invoke声明
- 适用：游戏、专业软件

**方案B：SendKeys**
- 优点：.NET内置，简单
- 缺点：部分游戏不支持
- 适用：普通办公软件

**方案C：keybd_event (已弃用但兼容性好)**
- 优点：兼容旧版Windows
- 缺点：官方不推荐

**实现选择**：优先使用SendInput，备选keybd_event

**SendInput P/Invoke声明**：
```csharp
[DllImport("user32.dll", SetLastError = true)]
static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

[StructLayout(LayoutKind.Sequential)]
struct INPUT
{
    public uint type;  // 1 = INPUT_KEYBOARD
    public KEYBDINPUT ki;
}

[StructLayout(LayoutKind.Sequential)]
struct KEYBDINPUT
{
    public ushort wVk;         // 虚拟键码
    public ushort wScan;       // 扫描码
    public uint dwFlags;       // KEYEVENTF_EXTENDEDKEY, KEYEVENTF_KEYUP
    public uint time;
    public IntPtr dwExtraInfo;
}
```

**核心方法**：
```csharp
public class InputSimulator
{
    /// <summary>模拟按键（按下+抬起）</summary>
    public void SimulateKeyPress(int keyCode, int pressDurationMs);

    /// <summary>模拟按键按下</summary>
    public void SimulateKeyDown(int keyCode);

    /// <summary>模拟按键抬起</summary>
    public void SimulateKeyUp(int keyCode);

    /// <summary>按序列播放宏</summary>
    public async Task PlayMacroAsync(List<KeyAction> sequence, CancellationToken ct);
}
```

**稳定性控制参数**：
- 默认按下时间：50ms
- 默认间隔延迟：50ms
- 最小精度：Windows定时器约15ms
- 高精度方案：使用 `Stopwatch` + `SpinWait` 实现亚毫秒级延迟

### 2.4 配置存储模块

**文件位置**：`src/MacroPlayer.Core/Services/ConfigManager.cs`

**存储路径**：
- 开发模式：`./config.json` (项目根目录)
- 发布模式：`%AppData%\MacroPlayer\config.json`

**配置文件结构**：
```json
{
  "version": "1.0",
  "settings": {
    "startMinimized": true,
    "defaultPressDuration": 50,
    "defaultDelay": 50,
    "enableSound": false
  },
  "macros": [
    {
      "name": "连招1",
      "hotkey": "F4",
      "isEnabled": true,
      "loopMode": 0,
      "keySequence": [
        {"keyCode": 68, "keyName": "D", "pressDuration": 50, "delayAfter": 50},
        {"keyCode": 68, "keyName": "D", "pressDuration": 50, "delayAfter": 50},
        {"keyCode": 81, "keyName": "Q", "pressDuration": 50, "delayAfter": 50},
        {"keyCode": 81, "keyName": "Q", "pressDuration": 50, "delayAfter": 50}
      ]
    }
  ]
}
```

**核心方法**：
```csharp
public class ConfigManager
{
    private readonly string _configPath;

    /// <summary>加载配置</summary>
    public AppConfig LoadConfig();

    /// <summary>保存配置</summary>
    public void SaveConfig(AppConfig config);

    /// <summary>导出宏</summary>
    public void ExportMacro(string macroName, string filePath);

    /// <summary>导入宏</summary>
    public MacroDefinition ImportMacro(string filePath);
}
```

---

## 🎨 第三阶段：UI界面开发

### 3.1 主窗口设计

**文件位置**：`src/MacroPlayer.UI/MainWindow.xaml`

**窗口属性**：
```xml
<Window x:Class="MacroPlayer.UI.MainWindow"
        Title="MacroPlayer" 
        Height="600" Width="900"
        WindowStartupLocation="CenterScreen"
        Closing="Window_Closing">
    <!-- 内容 -->
</Window>
```

**界面布局**：
```
┌─────────────────────────────────────────────┐
│  MacroPlayer v1.0                    [_][X] │
├─────────────────────────────────────────────┤
│  ┌──────────────┐  ┌─────────────────────┐  │
│  │ 宏列表        │  │ 宏编辑器             │  │
│  │              │  │                     │  │
│  │ • 连招1 [F4] │  │ 名称: [________]    │  │
│  │ • 连招2 [F5] │  │ 热键: [F4 ▼]        │  │
│  │ • 连招3 [F6] │  │                     │  │
│  │              │  │ 按键序列:           │  │
│  │ [+添加]      │  │ ┌───────────────┐   │  │
│  │ [-删除]      │  │ │ D(50) D(50)   │   │  │
│  │              │  │ │ Q(50) Q(50)   │   │  │
│  └──────────────┘  │ └───────────────┘   │  │
│                    │ [测试] [保存]        │  │
│                    └─────────────────────┘  │
├─────────────────────────────────────────────┤
│ 状态: 就绪 | 热键监听: 开启 | [最小化到托盘]  │
└─────────────────────────────────────────────┘
```

### 3.2 托盘图标实现

**使用库**：Hardcodet.NotifyIcon.Wpf

**XAML定义**：`src/MacroPlayer.UI/NotifyIconResources.xaml`
```xml
<tb:TaskbarIcon x:Key="TrayIcon"
                IconSource="/Resources/app.ico"
                ToolTipText="MacroPlayer - 运行中"
                LeftClickCommand="{Binding ShowWindowCommand}">
    <tb:TaskbarIcon.ContextMenu>
        <ContextMenu>
            <MenuItem Header="显示主窗口" Command="{Binding ShowWindowCommand}"/>
            <MenuItem Header="暂停/恢复" Command="{Binding ToggleEnabledCommand}"/>
            <Separator/>
            <MenuItem Header="退出" Command="{Binding ExitCommand}"/>
        </ContextMenu>
    </tb:TaskbarIcon.ContextMenu>
</tb:TaskbarIcon>
```

**行为逻辑**：
- 关闭窗口 → 最小化到托盘（不退出）
- 双击托盘 → 显示主窗口
- 右键托盘 → 显示菜单

### 3.3 热键录制控件

**自定义控件**：`HotkeyRecorder`

**交互逻辑**：
1. 点击"录制热键"按钮
2. 控件进入录制状态（红色边框闪烁）
3. 用户按下目标组合键
4. 捕获并显示（如 "Ctrl+F4"）
5. 验证是否与其他宏冲突

**技术实现**：
- 使用 `PreviewKeyDown` 事件
- 记录 `Keyboard.Modifiers` + `e.Key`
- 转换为字符串显示

---

## ⚙️ 第四阶段：系统集成

### 4.1 服务注册与生命周期

**文件位置**：`src/MacroPlayer.UI/App.xaml.cs`

**依赖注入配置**：
```csharp
public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        // 初始化服务
        var hotkeyManager = ServiceProvider.GetRequiredService<HotkeyManager>();
        var configManager = ServiceProvider.GetRequiredService<ConfigManager>();

        // 加载配置并注册热键
        var config = configManager.LoadConfig();
        InitializeHotkeys(hotkeyManager, config);

        base.OnStartup(e);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ConfigManager>();
        services.AddSingleton<HotkeyManager>();
        services.AddSingleton<InputSimulator>();
        services.AddSingleton<MacroEngine>();
        services.AddTransient<MainWindow>();
    }
}
```

### 4.2 消息循环集成

**问题**：WPF的 `HwndSource` 需要添加Hook接收 `WM_HOTKEY`

**解决方案**：
```csharp
public partial class MainWindow : Window
{
    private HwndSource? _hwndSource;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _hwndSource?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312) // WM_HOTKEY
        {
            var hotkeyId = wParam.ToInt32();
            // 触发对应宏
            HandleHotkeyTriggered(hotkeyId);
            handled = true;
        }
        return IntPtr.Zero;
    }
}
```

### 4.3 宏引擎（协调器）

**文件位置**：`src/MacroPlayer.Core/Services/MacroEngine.cs`

**职责**：
- 管理宏执行状态
- 处理循环模式逻辑
- 防止宏重叠执行

**状态机**：
```
Idle (空闲)
  ↓ 热键触发
Executing (执行中)
  ↓ 完成
Idle

LoopMode=1 (按住循环):
Executing ↔ 按住时循环执行
```

**线程安全**：
- 使用 `SemaphoreSlim` 防止并发执行
- 使用 `CancellationTokenSource` 取消循环

---

## 📦 第五阶段：构建与发布

### 5.1 调试配置

**launch.json 配置**：
```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": "Debug MacroPlayer",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build",
            "program": "${workspaceFolder}/src/MacroPlayer.UI/bin/Debug/net8.0-windows/MacroPlayer.UI.exe",
            "args": [],
            "cwd": "${workspaceFolder}/src/MacroPlayer.UI",
            "console": "internalConsole",
            "stopAtEntry": false
        }
    ]
}
```

### 5.2 发布命令

**VS Code终端操作**：
```bash
# 进入UI项目目录
cd src/MacroPlayer.UI

# 单文件发布（独立运行时）
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true

# 输出位置
# bin/Release/net8.0-windows/win-x64/publish/MacroPlayer.UI.exe

# 可选：压缩体积
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=true /p:TrimMode=partial
```

### 5.3 发布文件清单

**输出目录内容**：
```
publish/
├── MacroPlayer.UI.exe      # 主程序（约 10-50MB，取决于裁剪）
└── config.json             # 配置文件（首次运行自动生成）
```

**体积优化建议**：
- 不裁剪：约 150MB（包含完整.NET运行时）
- 部分裁剪：约 50MB
- 框架依赖：约 1MB（需目标机器安装.NET 8运行时）

---

## 🧪 第六阶段：测试验证

### 6.1 功能测试清单

| 测试项 | 操作步骤 | 预期结果 |
|--------|----------|----------|
| 热键注册 | 添加F4触发宏 | 注册成功，托盘显示状态 |
| 宏执行 | 按下F4 | 正确输出DDQQ序列 |
| 延迟精度 | 设置10ms间隔 | 实际间隔10ms±5ms |
| 循环模式 | 设置按住循环 | 按住时持续触发，松开停止 |
| 配置保存 | 修改后重启 | 配置持久化，宏保留 |
| 托盘功能 | 点击关闭再双击托盘 | 窗口恢复，热键仍有效 |
| 多宏冲突 | 设置相同热键 | 提示冲突，禁止保存 |

### 6.2 性能测试

**测试工具**：使用 `Stopwatch` 记录

**指标**：
- 热键响应延迟：< 50ms
- 宏播放精度：±5ms
- CPU占用：< 1%（空闲时）
- 内存占用：< 100MB

---

## 🚀 第七阶段：交付与部署

### 7.1 用户文档

**README.md 内容**：
```markdown
# MacroPlayer 使用说明

## 快速开始
1. 运行 MacroPlayer.exe
2. 点击"添加宏"
3. 设置热键（如F4）
4. 输入按键序列（如 DDQQ）
5. 保存并最小化
6. 在游戏中按下F4测试

## 按键语法
- 基础：D → 按下D键50ms
- 带延迟：D(100) → 按下100ms
- 完整：D(100,200) → 按下100ms，延迟200ms
- 组合：Ctrl+C → 组合键

## 注意事项
- 需要管理员权限（部分游戏需要）
- 关闭前请保存配置
- 热键全局生效，注意冲突
```

### 7.2 部署检查表

- [ ] 单EXE可运行
- [ ] 配置自动创建
- [ ] 托盘图标正常
- [ ] 热键全局有效
- [ ] 退出时注销热键（防止残留）

---

## 🔧 技术要点补充

### 8.1 管理员权限获取

**方案**：创建 `app.manifest` 文件
```xml
<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
```

### 8.2 防误触设计

- 添加"启用/禁用"开关（全局暂停）
- 特定进程白名单（只在指定游戏窗口生效）
- 执行前倒计时（3,2,1提示）

### 8.3 常见问题

**Q: 游戏不响应模拟按键？**
A: 尝试以管理员运行，或改用SendInput API

**Q: 热键在游戏中无效？**
A: 部分游戏使用DirectInput，需要更底层的Hook

**Q: 宏执行顺序错乱？**
A: 增加延迟间隔，避免系统缓冲区溢出

---

## 📎 附录：GLM4.6编码提示

### 代码规范
- 使用C# 12新特性（主构造函数、集合表达式等）
- 遵循Microsoft命名规范
- 关键方法添加XML注释
- 使用 `async/await` 处理异步操作

### 关键NuGet包
```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />  <!-- MVVM工具 -->
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
```

### 需要P/Invoke的API列表
- `RegisterHotKey` / `UnregisterHotKey`
- `SendInput`
- `GetAsyncKeyState`（检测按键状态）
- `GetForegroundWindow`（获取当前窗口）

### 调试技巧
- 使用 `Debug.WriteLine` 输出关键流程
- 使用 `Spy++` 查看窗口消息
- 使用 `PostMessage` 测试消息接收

---

**文档版本**：v1.0  
**最后更新**：2026-03-05  
**编写**：Kimi  
**执行**：GLM4.6 全栈开发
