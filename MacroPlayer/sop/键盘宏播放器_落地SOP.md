# 键盘宏播放器 — 全栈落地 SOP（给 AI 编码助手使用）

**文档版本：** v2.0  
**日期：** 2026-03-05  
**开发工具：** VS Code  
**技术栈：** .NET 8 WinForms + WinAPI  
**目标产物：** 单 EXE，无需安装，支持 Windows 10/11 x64  

> **阅读说明：** 本文档为逐步执行的编码指令，按顺序完成每一步即可得到完整可运行项目。每步均含：要做什么、在哪个文件操作、写什么代码。

---

## 第一步：创建项目

### 1.1 在 VS Code 终端执行

```bash
# 创建 WinForms 项目（支持托盘、窗口、WinAPI）
dotnet new winforms -n MacroPlayer -f net8.0-windows
cd MacroPlayer

# 安装 JSON 序列化库
dotnet add package System.Text.Json
```

### 1.2 项目最终文件结构

```
MacroPlayer/
├── MacroPlayer.csproj
├── Program.cs               ← 入口，启动托盘
├── AppContext.cs            ← 托盘图标 + 生命周期管理
├── Models/
│   ├── MacroEntry.cs        ← 单条宏数据结构
│   └── AppSettings.cs       ← 全局配置数据结构
├── Core/
│   ├── ConfigManager.cs     ← 读写 config.json
│   ├── MacroParser.cs       ← 字符串 → 按键序列
│   ├── MacroPlayer.cs       ← 执行按键模拟
│   └── HotkeyManager.cs     ← 注册/监听全局热键
├── UI/
│   └── MainForm.cs          ← 宏编辑器主窗口
└── config.json              ← 用户配置（运行时生成）
```

---

## 第二步：编写数据模型

### 文件：`Models/MacroEntry.cs`

**要求：** 定义单条宏的数据结构，用于 JSON 序列化和内存传递。

```csharp
namespace MacroPlayer.Models;

public class MacroEntry
{
    // 触发热键，例如 "F4"、"F6"
    public string Hotkey { get; set; } = "";

    // 宏名称，用于 UI 显示
    public string Name { get; set; } = "未命名宏";

    // 按键序列字符串，例如 "DDQQ" 或 "D(30)D(30)Q(50)"
    public string Sequence { get; set; } = "";

    // 全局默认按键间隔（毫秒），可被 Sequence 内的单独延迟覆盖
    public int DelayMs { get; set; } = 50;

    // 是否启用该宏
    public bool Enabled { get; set; } = true;
}
```

---

### 文件：`Models/AppSettings.cs`

**要求：** 定义全局配置结构，包含宏列表和全局参数。

```csharp
namespace MacroPlayer.Models;

public class AppSettings
{
    // 所有宏条目
    public List<MacroEntry> Macros { get; set; } = new();

    // 每次按键按住的持续时间（毫秒）
    public int KeyDownDuration { get; set; } = 20;

    // 启动时最小化到托盘
    public bool StartMinimized { get; set; } = true;

    // 开机自启
    public bool AutoStart { get; set; } = false;
}
```

---

## 第三步：编写配置管理器

### 文件：`Core/ConfigManager.cs`

**要求：**
- 读取 / 写入 `config.json`（与 EXE 同目录）
- 文件不存在时自动创建含示例数据的默认配置
- 提供静态单例访问

```csharp
using System.Text.Json;
using MacroPlayer.Models;

namespace MacroPlayer.Core;

public static class ConfigManager
{
    // config.json 存放在 EXE 同目录
    private static readonly string ConfigPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    // 内存中的当前配置
    public static AppSettings Current { get; private set; } = new();

    // 从磁盘加载，失败则使用默认值
    public static void Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                Current = CreateDefault();
                Save(); // 写出默认配置供用户参考
                return;
            }

            var json = File.ReadAllText(ConfigPath);
            Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? CreateDefault();
        }
        catch
        {
            Current = CreateDefault();
        }
    }

    // 将当前配置写回磁盘
    public static void Save()
    {
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    // 生成出厂默认配置（含两条示例宏）
    private static AppSettings CreateDefault() => new()
    {
        KeyDownDuration = 20,
        StartMinimized = true,
        Macros = new List<MacroEntry>
        {
            new() { Hotkey = "F4", Name = "示例宏1", Sequence = "DDQQ", DelayMs = 50 },
            new() { Hotkey = "F6", Name = "示例宏2", Sequence = "WASD", DelayMs = 30 }
        }
    };
}
```

---

## 第四步：编写宏解析器

### 文件：`Core/MacroParser.cs`

**要求：**
- 将 `Sequence` 字符串解析为按键单元列表
- 支持两种语法：
  - 简单语法：`DDQQ`（每个字符一个按键，使用全局 DelayMs）
  - 带延迟语法：`D(30)D(30)Q(50)`（括号内为该键的自定义延迟毫秒）
- 支持特殊键名：`{F1}` `{ENTER}` `{SPACE}` `{TAB}` 等

```csharp
using System.Text.RegularExpressions;

namespace MacroPlayer.Core;

// 单个按键执行单元
public record KeyUnit(string Key, int DelayMs);

public static class MacroParser
{
    // 解析序列字符串为 KeyUnit 列表
    // defaultDelay: 未指定延迟时使用的默认值
    public static List<KeyUnit> Parse(string sequence, int defaultDelay = 50)
    {
        var result = new List<KeyUnit>();
        if (string.IsNullOrWhiteSpace(sequence)) return result;

        int i = 0;
        while (i < sequence.Length)
        {
            // 特殊键：{ENTER} {F1} {SPACE} 等
            if (sequence[i] == '{')
            {
                int end = sequence.IndexOf('}', i);
                if (end < 0) break;
                string key = sequence.Substring(i + 1, end - i - 1).ToUpper();
                i = end + 1;
                int delay = TryReadDelay(sequence, ref i, defaultDelay);
                result.Add(new KeyUnit(key, delay));
            }
            else
            {
                // 普通单字符键
                string key = sequence[i].ToString().ToUpper();
                i++;
                int delay = TryReadDelay(sequence, ref i, defaultDelay);
                result.Add(new KeyUnit(key, delay));
            }
        }

        return result;
    }

    // 尝试读取紧跟的 (数字) 延迟，没有则返回默认值
    private static int TryReadDelay(string seq, ref int i, int defaultDelay)
    {
        if (i < seq.Length && seq[i] == '(')
        {
            int end = seq.IndexOf(')', i);
            if (end > i && int.TryParse(seq.Substring(i + 1, end - i - 1), out int ms))
            {
                i = end + 1;
                return ms;
            }
        }
        return defaultDelay;
    }
}
```

---

## 第五步：编写宏播放器（按键模拟核心）

### 文件：`Core/MacroPlayer.cs`

**要求：**
- 调用 WinAPI `SendInput` 模拟按键按下/抬起
- 每个按键：按下 → 等待 KeyDownDuration → 抬起 → 等待 DelayMs
- 在独立线程执行，不阻塞 UI
- 支持中途取消（`CancellationToken`）
- 内置虚拟键码映射表（A-Z、0-9、F1-F12、常用特殊键）

```csharp
using System.Runtime.InteropServices;
using MacroPlayer.Models;

namespace MacroPlayer.Core;

public static class MacroPlayer
{
    // ── WinAPI 结构 ──────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public INPUTUNION u; }

    [StructLayout(LayoutKind.Explicit)]
    struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT ki; }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    const uint INPUT_KEYBOARD = 1;
    const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    // ── 虚拟键码映射表 ────────────────────────────────────────

    private static readonly Dictionary<string, ushort> VkMap = new()
    {
        // 字母
        ["A"]=0x41,["B"]=0x42,["C"]=0x43,["D"]=0x44,["E"]=0x45,
        ["F"]=0x46,["G"]=0x47,["H"]=0x48,["I"]=0x49,["J"]=0x4A,
        ["K"]=0x4B,["L"]=0x4C,["M"]=0x4D,["N"]=0x4E,["O"]=0x4F,
        ["P"]=0x50,["Q"]=0x51,["R"]=0x52,["S"]=0x53,["T"]=0x54,
        ["U"]=0x55,["V"]=0x56,["W"]=0x57,["X"]=0x58,["Y"]=0x59,["Z"]=0x5A,
        // 数字
        ["0"]=0x30,["1"]=0x31,["2"]=0x32,["3"]=0x33,["4"]=0x34,
        ["5"]=0x35,["6"]=0x36,["7"]=0x37,["8"]=0x38,["9"]=0x39,
        // F键
        ["F1"]=0x70,["F2"]=0x71,["F3"]=0x72,["F4"]=0x73,["F5"]=0x74,
        ["F6"]=0x75,["F7"]=0x76,["F8"]=0x77,["F9"]=0x78,["F10"]=0x79,
        ["F11"]=0x7A,["F12"]=0x7B,
        // 特殊键
        ["ENTER"]=0x0D,["SPACE"]=0x20,["TAB"]=0x09,["ESC"]=0x1B,
        ["BACKSPACE"]=0x08,["DELETE"]=0x2E,["INSERT"]=0x2D,
        ["HOME"]=0x24,["END"]=0x23,["PAGEUP"]=0x21,["PAGEDOWN"]=0x22,
        ["LEFT"]=0x25,["UP"]=0x26,["RIGHT"]=0x27,["DOWN"]=0x28,
        ["LSHIFT"]=0xA0,["RSHIFT"]=0xA1,["LCTRL"]=0xA2,["RCTRL"]=0xA3,
        ["LALT"]=0xA4,["RALT"]=0xA5,
    };

    // ── 公开方法：异步播放一条宏 ──────────────────────────────

    public static Task PlayAsync(MacroEntry entry, int keyDownDuration,
                                  CancellationToken ct = default)
    {
        var units = MacroParser.Parse(entry.Sequence, entry.DelayMs);
        return Task.Run(() =>
        {
            foreach (var unit in units)
            {
                if (ct.IsCancellationRequested) break;

                if (!VkMap.TryGetValue(unit.Key, out ushort vk)) continue;

                PressKey(vk);
                Thread.Sleep(keyDownDuration);
                ReleaseKey(vk);
                Thread.Sleep(unit.DelayMs);
            }
        }, ct);
    }

    // ── 内部辅助：按下 / 抬起单个键 ──────────────────────────

    private static void PressKey(ushort vk) => SendInput(1, new[]
    {
        new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION
            { ki = new KEYBDINPUT { wVk = vk } } }
    }, Marshal.SizeOf<INPUT>());

    private static void ReleaseKey(ushort vk) => SendInput(1, new[]
    {
        new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION
            { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } } }
    }, Marshal.SizeOf<INPUT>());
}
```

---

## 第六步：编写热键监听器

### 文件：`Core/HotkeyManager.cs`

**要求：**
- 调用 WinAPI `RegisterHotKey` 注册全局热键
- 通过隐藏窗口的消息循环接收 `WM_HOTKEY`
- 触发时回调 `OnHotkeyTriggered` 事件
- 支持批量注册 / 注销
- 热键冲突时给出错误提示

```csharp
using System.Runtime.InteropServices;

namespace MacroPlayer.Core;

public class HotkeyManager : IDisposable
{
    [DllImport("user32.dll")] static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(nint hWnd, int id);

    const int WM_HOTKEY = 0x0312;

    // 热键触发事件，参数为热键字符串（如 "F4"）
    public event Action<string>? OnHotkeyTriggered;

    private readonly HotkeyWindow _window;
    private readonly Dictionary<int, string> _idToKey = new();
    private int _nextId = 1;

    public HotkeyManager()
    {
        _window = new HotkeyWindow();
        _window.WmHotkeyReceived += id =>
        {
            if (_idToKey.TryGetValue(id, out var key))
                OnHotkeyTriggered?.Invoke(key);
        };
    }

    // 注册一个热键，失败返回 false
    public bool Register(string hotkeyStr)
    {
        if (!TryParseHotkey(hotkeyStr, out uint vk, out uint mod)) return false;

        int id = _nextId++;
        if (!RegisterHotKey(_window.Handle, id, mod, vk))
        {
            MessageBox.Show($"热键 {hotkeyStr} 注册失败，可能已被其他程序占用。",
                "热键冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        _idToKey[id] = hotkeyStr;
        return true;
    }

    // 注销所有已注册热键
    public void UnregisterAll()
    {
        foreach (var id in _idToKey.Keys)
            UnregisterHotKey(_window.Handle, id);
        _idToKey.Clear();
        _nextId = 1;
    }

    public void Dispose()
    {
        UnregisterAll();
        _window.DestroyHandle();
    }

    // 解析热键字符串为 vk + modifier
    // 支持格式：F4 / Ctrl+F4 / Alt+F4 / Ctrl+Shift+A
    private static bool TryParseHotkey(string input, out uint vk, out uint mod)
    {
        vk = 0; mod = 0;
        var parts = input.ToUpper().Split('+');
        string keyPart = parts[^1].Trim();

        uint modifiers = 0;
        foreach (var p in parts[..^1])
        {
            modifiers |= p.Trim() switch
            {
                "ALT" => 0x0001,
                "CTRL" or "CONTROL" => 0x0002,
                "SHIFT" => 0x0004,
                "WIN" => 0x0008,
                _ => 0
            };
        }

        var vkMap = new Dictionary<string, uint>
        {
            ["F1"]=0x70,["F2"]=0x71,["F3"]=0x72,["F4"]=0x73,["F5"]=0x74,
            ["F6"]=0x75,["F7"]=0x76,["F8"]=0x77,["F9"]=0x78,["F10"]=0x79,
            ["F11"]=0x7A,["F12"]=0x7B,
        };
        for (char c = 'A'; c <= 'Z'; c++) vkMap[c.ToString()] = (uint)c;
        for (char c = '0'; c <= '9'; c++) vkMap[c.ToString()] = (uint)c;

        if (!vkMap.TryGetValue(keyPart, out uint code)) return false;
        vk = code; mod = modifiers;
        return true;
    }

    // 隐藏消息窗口，专门用于接收 WM_HOTKEY
    private class HotkeyWindow : NativeWindow
    {
        public event Action<int>? WmHotkeyReceived;
        public HotkeyWindow() => CreateHandle(new CreateParams());
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
                WmHotkeyReceived?.Invoke((int)m.WParam);
            base.WndProc(ref m);
        }
    }
}
```

---

## 第七步：编写主窗口（宏编辑器 UI）

### 文件：`UI/MainForm.cs`

**要求：**
- 使用 WinForms 标准控件
- 左侧：宏列表（DataGridView），显示热键、名称、序列、是否启用
- 右侧：编辑面板，可修改选中宏的所有字段
- 底部按钮：新增、删除、保存、测试播放
- 标题栏显示软件名称和版本

```csharp
using MacroPlayer.Core;
using MacroPlayer.Models;

namespace MacroPlayer.UI;

public class MainForm : Form
{
    private DataGridView gridMacros = null!;
    private TextBox txtName = null!, txtHotkey = null!, txtSequence = null!;
    private NumericUpDown numDelay = null!;
    private CheckBox chkEnabled = null!;
    private Button btnAdd = null!, btnDelete = null!, btnSave = null!, btnTest = null!;
    private Label lblStatus = null!;

    public MainForm()
    {
        InitUI();
        LoadGrid();
    }

    private void InitUI()
    {
        Text = "键盘宏播放器 v1.0";
        Size = new Size(800, 500);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        // ── 宏列表 ──
        gridMacros = new DataGridView
        {
            Location = new Point(10, 10), Size = new Size(480, 400),
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false, ReadOnly = true, AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        gridMacros.Columns.Add("Hotkey", "热键");
        gridMacros.Columns.Add("Name", "名称");
        gridMacros.Columns.Add("Sequence", "序列");
        gridMacros.Columns.Add("Enabled", "启用");
        gridMacros.SelectionChanged += GridMacros_SelectionChanged;
        Controls.Add(gridMacros);

        // ── 编辑面板 ──
        int ex = 510, ey = 10;
        AddLabel("热键：", ex, ey);
        txtHotkey = AddTextBox(ex, ey + 20, 250);
        AddLabel("名称：", ex, ey + 55);
        txtName = AddTextBox(ex, ey + 75, 250);
        AddLabel("序列（例：DDQQ 或 D(30)Q(50)）：", ex, ey + 110);
        txtSequence = AddTextBox(ex, ey + 130, 250);
        AddLabel("按键间隔 ms：", ex, ey + 165);
        numDelay = new NumericUpDown { Location = new Point(ex, ey + 185), Size = new Size(100, 25), Minimum = 10, Maximum = 2000, Value = 50 };
        Controls.Add(numDelay);
        chkEnabled = new CheckBox { Text = "启用此宏", Location = new Point(ex, ey + 220), Checked = true };
        Controls.Add(chkEnabled);

        // ── 按钮 ──
        btnAdd = AddButton("新增", ex, ey + 260, Btn_Add);
        btnDelete = AddButton("删除", ex + 90, ey + 260, Btn_Delete);
        btnSave = AddButton("保存", ex + 180, ey + 260, Btn_Save);
        btnTest = AddButton("测试", ex, ey + 300, Btn_Test);

        // ── 状态栏 ──
        lblStatus = new Label { Location = new Point(10, 420), Size = new Size(760, 20), ForeColor = Color.Gray, Text = "就绪" };
        Controls.Add(lblStatus);
    }

    private void LoadGrid()
    {
        gridMacros.Rows.Clear();
        foreach (var m in ConfigManager.Current.Macros)
            gridMacros.Rows.Add(m.Hotkey, m.Name, m.Sequence, m.Enabled ? "✓" : "");
    }

    private void GridMacros_SelectionChanged(object? s, EventArgs e)
    {
        if (gridMacros.SelectedRows.Count == 0) return;
        int idx = gridMacros.SelectedRows[0].Index;
        var m = ConfigManager.Current.Macros[idx];
        txtHotkey.Text = m.Hotkey;
        txtName.Text = m.Name;
        txtSequence.Text = m.Sequence;
        numDelay.Value = m.DelayMs;
        chkEnabled.Checked = m.Enabled;
    }

    private void Btn_Add(object? s, EventArgs e)
    {
        ConfigManager.Current.Macros.Add(new MacroEntry());
        LoadGrid();
        gridMacros.Rows[^1].Selected = true;
        SetStatus("已新增一条空宏，请填写后保存。");
    }

    private void Btn_Delete(object? s, EventArgs e)
    {
        if (gridMacros.SelectedRows.Count == 0) return;
        int idx = gridMacros.SelectedRows[0].Index;
        ConfigManager.Current.Macros.RemoveAt(idx);
        LoadGrid();
        SetStatus("已删除。");
    }

    private void Btn_Save(object? s, EventArgs e)
    {
        if (gridMacros.SelectedRows.Count == 0) return;
        int idx = gridMacros.SelectedRows[0].Index;
        var m = ConfigManager.Current.Macros[idx];
        m.Hotkey = txtHotkey.Text.Trim().ToUpper();
        m.Name = txtName.Text.Trim();
        m.Sequence = txtSequence.Text.Trim();
        m.DelayMs = (int)numDelay.Value;
        m.Enabled = chkEnabled.Checked;
        ConfigManager.Save();
        LoadGrid();
        SetStatus("已保存。热键变更需重启生效。");
    }

    private async void Btn_Test(object? s, EventArgs e)
    {
        if (gridMacros.SelectedRows.Count == 0) return;
        int idx = gridMacros.SelectedRows[0].Index;
        var m = ConfigManager.Current.Macros[idx];
        SetStatus($"3 秒后播放宏：{m.Name} ...");
        await Task.Delay(3000);
        await MacroPlayer.Core.MacroPlayer.PlayAsync(m, ConfigManager.Current.KeyDownDuration);
        SetStatus("测试播放完毕。");
    }

    private void SetStatus(string msg) => lblStatus.Text = msg;

    // ── UI 辅助方法 ──
    private Label AddLabel(string text, int x, int y)
    {
        var lbl = new Label { Text = text, Location = new Point(x, y), AutoSize = true };
        Controls.Add(lbl); return lbl;
    }
    private TextBox AddTextBox(int x, int y, int w)
    {
        var tb = new TextBox { Location = new Point(x, y), Size = new Size(w, 25) };
        Controls.Add(tb); return tb;
    }
    private Button AddButton(string text, int x, int y, EventHandler onClick)
    {
        var btn = new Button { Text = text, Location = new Point(x, y), Size = new Size(80, 30) };
        btn.Click += onClick; Controls.Add(btn); return btn;
    }
}
```

---

## 第八步：编写应用上下文（托盘管理）

### 文件：`AppContext.cs`

**要求：**
- 继承 `ApplicationContext`，管理托盘图标
- 启动时初始化所有模块
- 托盘右键菜单：显示窗口 / 重载配置 / 退出
- 捕获热键事件 → 调用宏播放器

```csharp
using MacroPlayer.Core;
using MacroPlayer.Models;
using MacroPlayer.UI;

namespace MacroPlayer;

public class AppContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly HotkeyManager _hotkeyManager;
    private MainForm? _mainForm;
    private CancellationTokenSource _cts = new();

    public AppContext()
    {
        // 1. 加载配置
        ConfigManager.Load();

        // 2. 初始化托盘图标
        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "键盘宏播放器",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        _tray.DoubleClick += (_, _) => ShowMainForm();

        // 3. 注册热键
        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.OnHotkeyTriggered += OnHotkeyTriggered;
        RegisterAllHotkeys();

        // 4. 按配置决定是否显示窗口
        if (!ConfigManager.Current.StartMinimized)
            ShowMainForm();
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开编辑器", null, (_, _) => ShowMainForm());
        menu.Items.Add("重载配置", null, (_, _) => ReloadConfig());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApplication());
        return menu;
    }

    private void ShowMainForm()
    {
        if (_mainForm == null || _mainForm.IsDisposed)
            _mainForm = new MainForm();
        _mainForm.Show();
        _mainForm.BringToFront();
    }

    private void ReloadConfig()
    {
        _hotkeyManager.UnregisterAll();
        ConfigManager.Load();
        RegisterAllHotkeys();
        _tray.ShowBalloonTip(2000, "宏播放器", "配置已重载。", ToolTipIcon.Info);
    }

    private void RegisterAllHotkeys()
    {
        foreach (var macro in ConfigManager.Current.Macros)
            if (macro.Enabled && !string.IsNullOrEmpty(macro.Hotkey))
                _hotkeyManager.Register(macro.Hotkey);
    }

    private void OnHotkeyTriggered(string hotkey)
    {
        var macro = ConfigManager.Current.Macros
            .FirstOrDefault(m => m.Hotkey.Equals(hotkey, StringComparison.OrdinalIgnoreCase)
                                 && m.Enabled);
        if (macro == null) return;

        // 取消上一次未完成的播放，开始新的
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        MacroPlayer.Core.MacroPlayer.PlayAsync(macro,
            ConfigManager.Current.KeyDownDuration, _cts.Token);
    }

    private void ExitApplication()
    {
        _cts.Cancel();
        _hotkeyManager.Dispose();
        _tray.Visible = false;
        Application.Exit();
    }
}
```

---

## 第九步：编写程序入口

### 文件：`Program.cs`

```csharp
namespace MacroPlayer;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new AppContext());
    }
}
```

---

## 第十步：配置项目文件

### 文件：`MacroPlayer.csproj`

**将原有内容替换为：**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
    <AssemblyName>MacroPlayer</AssemblyName>
    <RootNamespace>MacroPlayer</RootNamespace>
    <!-- 发布为单文件 EXE -->
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>false</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.Text.Json" Version="8.0.0" />
  </ItemGroup>
</Project>
```

---

## 第十一步：发布单 EXE

在 VS Code 终端执行：

```bash
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
```

输出位置：

```
bin/Release/net8.0-windows/win-x64/publish/MacroPlayer.exe
```

---

## 第十二步：验证清单（逐项确认）

| # | 验证项 | 通过标准 |
|---|--------|---------|
| 1 | 启动后托盘出现图标 | 右键菜单可见 |
| 2 | 首次启动生成 config.json | 文件内含示例宏 |
| 3 | 按 F4 触发宏 DDQQ | 记事本内出现 ddqq 字符 |
| 4 | 打开编辑器可新增宏 | 保存后 config.json 更新 |
| 5 | 测试按钮延迟 3 秒后执行 | 字符正常输入 |
| 6 | 重载配置不重启生效 | 托盘菜单操作后热键更新 |
| 7 | 退出后托盘消失 | 进程不残留 |

---

## 附录：序列语法速查

| 语法 | 含义 |
|------|------|
| `DDQQ` | 连按 D D Q Q，使用全局 DelayMs |
| `D(30)D(30)Q(50)` | D 后等 30ms，Q 后等 50ms |
| `{ENTER}` | 回车键 |
| `{F1}` | F1 功能键 |
| `{SPACE}` | 空格 |
| `{TAB}` | Tab |
| `{ESC}` | Esc |
| `{LEFT}{RIGHT}` | 左右方向键 |
| `Ctrl+C` | 热键格式（修饰键+主键） |

---

*文档结束 — 按步骤完整执行后即可得到可运行的键盘宏播放器。*
