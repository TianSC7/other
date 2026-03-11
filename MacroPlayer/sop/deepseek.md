# 宏播放器软件 —— 详细可落地的SOP（标准操作流程）

本SOP旨在指导你（或你的GLM4.6助手）使用 **.NET 8** 和 **WinForms** 在 **VSCode** 中开发一个**全局热键宏播放器**。  
所有步骤均按**模块化**设计，每个模块都有明确的输入、处理和输出，确保GLM4.6能准确生成全栈代码。

---

## 1. 开发环境准备
| 步骤 | 操作 | 说明 |
|------|------|------|
| 1.1 | 安装 [.NET 8 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0) | 运行 `dotnet --version` 确认安装成功（应显示 8.x） |
| 1.2 | 安装 [Visual Studio Code](https://code.visualstudio.com/) | 下载并安装 |
| 1.3 | 在VSCode中安装 **C# 扩展**（由Microsoft提供） | 用于代码补全和调试 |
| 1.4 | 创建项目文件夹，例如 `MacroPlayer` | 后续所有操作在此文件夹中进行 |

---

## 2. 创建WinForms项目
在终端中执行以下命令：
```bash
dotnet new winforms -n MacroPlayer -f net8.0
cd MacroPlayer
```
此时项目结构应包含 `MacroPlayer.csproj` 和 `Form1.cs` 等文件。

> **说明**：WinForms自带消息循环，便于处理全局热键消息。

---

## 3. 添加必要的NuGet包
我们尽量使用内置库，但**全局热键**和**按键模拟**需要调用Win32 API，无需额外包。  
为了简化JSON配置读写，可使用 `System.Text.Json`（.NET内置），但为了更好的可读性，我们选用 `Newtonsoft.Json`（可选）。  
添加以下包（如果需要）：
```bash
dotnet add package Newtonsoft.Json
```
> **理由**：Newtonsoft.Json 提供更灵活的序列化控制，尤其在处理枚举和特殊字符时。

---

## 4. 设计宏数据模型
在项目中新建文件夹 `Models`，创建类 `MacroDefinition.cs`：

```csharp
public class MacroDefinition
{
    public string Name { get; set; } = "New Macro";
    public Keys TriggerKey { get; set; }          // 触发热键（如 Keys.F4）
    public bool Alt { get; set; }                  // 是否组合Alt
    public bool Ctrl { get; set; }                 // 是否组合Ctrl
    public bool Shift { get; set; }                 // 是否组合Shift
    public string KeySequence { get; set; } = "";   // 按键序列字符串，如 "DDQQ"
    public List<MacroStep> Steps { get; set; }      // 解析后的步骤列表（供播放器使用）
}

public class MacroStep
{
    public Keys Key { get; set; }        // 要模拟的按键
    public int DelayMs { get; set; } = 50; // 按下后延迟（毫秒）
    public bool IsDown { get; set; } = true;  // true=按下，false=抬起（通常用一对Down/Up表示一次按键）
}
```

> **说明**：`KeySequence` 是用户输入的简易字符串，例如 `"D D Q Q"`（空格分隔）或 `"[F1] hello"`。但为简化，第一步我们只支持**单个字母/数字**的连续输入（如 `DDQQ`）。  
> **解析函数**：需要将 `KeySequence` 转换为 `List<MacroStep>`，每个字符对应一个按键（按下+抬起+延迟）。  
> 特殊键（如F1、Enter）可用 `<F1>` 格式，解析时识别尖括号。

---

## 5. 实现配置读写
在项目中新建文件夹 `Services`，创建类 `ConfigService.cs`。

### 5.1 定义配置根类
```csharp
public class AppConfig
{
    public List<MacroDefinition> Macros { get; set; } = new List<MacroDefinition>();
}
```

### 5.2 实现保存/加载方法
- 配置文件路径：`%APPDATA%\MacroPlayer\config.json`（使用 `Environment.GetFolderPath`）
- 使用 `Newtonsoft.Json` 序列化/反序列化。
- 若文件不存在，创建默认配置（例如预置一个F4->DDQQ的宏）。
- 保存时，自动调用解析函数更新 `MacroDefinition.Steps`。

### 5.3 关键代码提示
```csharp
public static void Save(AppConfig config)
{
    string json = JsonConvert.SerializeObject(config, Formatting.Indented);
    File.WriteAllText(_configPath, json);
}

public static AppConfig Load()
{
    if (!File.Exists(_configPath))
        return new AppConfig(); // 返回默认空配置
    string json = File.ReadAllText(_configPath);
    return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
}
```

---

## 6. 实现全局热键监听
我们需要使用Win32 API `RegisterHotKey` 和 `UnregisterHotKey`。  
在WinForms中，通过重写 `WndProc` 接收 `WM_HOTKEY` 消息。

### 6.1 定义Win32 API委托
新建 `NativeMethods.cs` 文件：
```csharp
using System.Runtime.InteropServices;

public static class NativeMethods
{
    public const int WM_HOTKEY = 0x0312;
    public const int MOD_ALT = 0x0001;
    public const int MOD_CONTROL = 0x0002;
    public const int MOD_SHIFT = 0x0004;
    public const int MOD_WIN = 0x0008;

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
```

### 6.2 在主窗体中管理热键
- 为每个宏分配一个唯一的**热键ID**（例如使用宏在列表中的索引或GUID）。
- 在窗体加载时，遍历所有宏并调用 `RegisterHotKey`。
- 重写 `WndProc`：
```csharp
protected override void WndProc(ref Message m)
{
    if (m.Msg == NativeMethods.WM_HOTKEY)
    {
        int id = m.WParam.ToInt32(); // 热键ID
        // 根据ID找到对应的宏，并触发播放
        // 注意：需要在UI线程外执行播放，避免阻塞消息循环
        Task.Run(() => PlayMacro(id));
    }
    base.WndProc(ref m);
}
```
- 窗体关闭时，务必调用 `UnregisterHotKey` 释放所有热键。

---

## 7. 实现按键模拟
使用 `SendInput` 模拟键盘事件。同样需要P/Invoke。

### 7.1 定义所需结构
在 `NativeMethods.cs` 中添加：
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct INPUT
{
    public uint type; // 1 = INPUT_KEYBOARD
    public KEYBDINPUT ki;
}

[StructLayout(LayoutKind.Sequential)]
public struct KEYBDINPUT
{
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
}

public const uint INPUT_KEYBOARD = 1;
public const uint KEYEVENTF_KEYDOWN = 0x0000;
public const uint KEYEVENTF_KEYUP = 0x0002;
public const uint KEYEVENTF_SCANCODE = 0x0008; // 如果使用扫描码

[DllImport("user32.dll")]
public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
```

### 7.2 封装模拟按键方法
```csharp
public static void SimulateKey(Keys key, bool press)
{
    INPUT input = new INPUT();
    input.type = INPUT_KEYBOARD;
    input.ki.wVk = (ushort)key;
    input.ki.dwFlags = press ? KEYEVENTF_KEYDOWN : KEYEVENTF_KEYUP;
    SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT)));
}
```

> **注意**：`Keys` 枚举来自 `System.Windows.Forms`，需添加 `using System.Windows.Forms;`。  
> 此方法直接发送虚拟键码，适用于大多数标准键。对扩展键（如右Alt）可能需要额外标志，但基础字母数字键工作良好。

---

## 8. 实现宏播放器
宏播放器负责按顺序播放 `MacroStep` 列表，处理按下、抬起和延迟。

### 8.1 创建播放器类 `MacroPlayer`
```csharp
public static class MacroPlayer
{
    public static async Task PlayAsync(List<MacroStep> steps, CancellationToken cancellationToken = default)
    {
        foreach (var step in steps)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // 模拟按键
            SimulateKey(step.Key, step.IsDown);
            // 延迟
            await Task.Delay(step.DelayMs, cancellationToken).ConfigureAwait(false);
        }
    }
}
```

### 8.2 步骤生成器（解析KeySequence）
在 `MacroDefinition` 中添加方法：
```csharp
public void ParseKeySequence()
{
    Steps = new List<MacroStep>();
    // 简单实现：每个字符视为一次按下+抬起，延迟固定50ms
    foreach (char c in KeySequence)
    {
        if (char.IsLetterOrDigit(c))
        {
            Keys key = (Keys)char.ToUpper(c); // 字符转Keys（注意：字母直接对应）
            // 按下
            Steps.Add(new MacroStep { Key = key, IsDown = true, DelayMs = 30 });
            // 抬起
            Steps.Add(new MacroStep { Key = key, IsDown = false, DelayMs = 30 });
        }
        // 扩展：可以处理尖括号如 <F1>
    }
}
```
> **注意**：需要更复杂的映射，例如 `Keys` 枚举中的 `F1` 等。可添加字典映射。

---

## 9. 实现托盘图标和主窗体
主窗体 `Form1` 应该隐藏（`WindowState = Minimized`，`ShowInTaskbar = false`），只显示托盘图标。

### 9.1 添加 `NotifyIcon` 控件
- 在 `Form1` 构造器中初始化 `NotifyIcon`：
```csharp
NotifyIcon trayIcon = new NotifyIcon();
trayIcon.Icon = SystemIcons.Application; // 或自定义图标
trayIcon.Text = "宏播放器";
trayIcon.Visible = true;
```

### 9.2 添加上下文菜单
- 创建 `ContextMenuStrip`，添加菜单项：
  - “打开配置” → 打开配置窗口
  - “退出” → 退出程序
- 关联到 `NotifyIcon.ContextMenuStrip`。

### 9.3 窗体加载和关闭逻辑
- `Load` 事件中：加载配置、注册热键。
- `FormClosing` 事件中：注销热键、保存配置（可选）。

---

## 10. 实现配置窗口
新建窗体 `ConfigForm.cs`，用于编辑宏列表。

### 10.1 界面布局建议
- 使用 `DataGridView` 展示宏列表（列：名称、触发热键、按键序列）。
- 添加“新增”、“删除”、“编辑”按钮。
- 双击某行打开详细编辑对话框。

### 10.2 详细编辑对话框 `MacroEditForm`
- 控件：文本框（名称），组合键选择（Hotkey），文本框（按键序列）。
- 热键选择可使用 `KeyDown` 事件捕获按键和修饰键（Ctrl/Alt/Shift）。
- 确认后更新宏对象。

### 10.3 保存和重新注册热键
- 配置窗口关闭时，重新读取配置并调用主窗体的 `ReRegisterHotKeys` 方法。
- 主窗体公开方法：先注销所有，再遍历新配置注册。

---

## 11. 集成各模块
在 `Form1` 中：
- 持有 `AppConfig` 对象。
- 定义 `Dictionary<int, MacroDefinition>` 映射热键ID到宏。
- 注册热键时，为每个宏生成唯一ID（例如使用 `Guid.NewGuid().GetHashCode()`，或简单用索引）。
- 触发播放时，根据ID获取宏，调用 `MacroPlayer.PlayAsync(宏.Steps)`。

> **注意**：`PlayAsync` 内部已使用 `await`，需要在 `WndProc` 中调用时使用 `Task.Run` 避免阻塞消息。

---

## 12. 运行测试
- 按F5启动程序。
- 托盘图标出现，右键打开配置窗口。
- 新增一个宏：触发键 F4，按键序列 `DDQQ`。
- 保存配置，打开记事本，按下F4，应输出“DDQQ”。
- 测试多个宏、组合键（如Ctrl+F5）等功能。

---

## 13. 发布为单文件EXE
在项目文件中添加：
```xml
<PropertyGroup>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>false</SelfContained> <!-- 或 true 如果目标机器无.NET运行时 -->
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
</PropertyGroup>
```
然后运行：
```bash
dotnet publish -c Release -r win-x64 --self-contained false
```
输出文件在 `bin\Release\net8.0-windows\win-x64\publish\MacroPlayer.exe`，可直接运行。

---

## 附录：关键注意事项
1. **热键冲突**：注册热键时可能失败（如已被其他程序占用），应捕获异常并提示用户更换。
2. **多实例控制**：使用 `Mutex` 确保只运行一个实例，避免热键混乱。
3. **宏播放中的取消**：如果用户再次按同一个热键，应取消当前播放并重新开始，或根据需求选择。可在 `MacroPlayer` 中传入 `CancellationTokenSource` 并管理。
4. **特殊键支持**：完善 `ParseKeySequence`，支持 `<F1>`、`<Enter>`、`<Space>` 等。可定义从字符串到 `Keys` 的映射字典。
5. **延迟配置**：可在宏定义中增加全局延迟或每个步骤单独延迟，以提供灵活性。

---

按照以上步骤，GLM4.6可以逐模块生成完整代码，最终组合成一个可工作的全局热键宏播放器。每一步都给出了明确的目标、所需类和关键代码结构，确保落地时没有歧义。祝开发顺利！