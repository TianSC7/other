using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;

namespace MacroPlayer.Core;

/// <summary>
/// 热键管理器，负责注册和监听全局热键
/// </summary>
public class HotkeyManager : IDisposable
{
    [DllImport("user32.dll")] static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(nint hWnd, int id);

    const int WM_HOTKEY = 0x0312;

    /// <summary>
    /// 热键触发事件，参数为热键字符串（如 "F4"）
    /// </summary>
    public event Action<string>? OnHotkeyTriggered;

    private readonly MessageWindow _window;
    private readonly Dictionary<int, string> _idToKey = new();
    private int _nextId = 1;

    public HotkeyManager()
    {
        Logger.Info("初始化热键管理器");
        _window = new MessageWindow();
        _window.WmHotkeyReceived += id =>
        {
            if (_idToKey.TryGetValue(id, out var key))
            {
                Logger.Info($"热键触发: {key} (ID: {id})");
                OnHotkeyTriggered?.Invoke(key);
            }
            else
            {
                Logger.Warning($"未知热键ID: {id}");
            }
        };
    }

    /// <summary>
    /// 注册一个热键，失败返回 false
    /// </summary>
    /// <param name="hotkeyStr">热键字符串</param>
    /// <returns>注册是否成功</returns>
    public bool Register(string hotkeyStr)
    {
        Logger.Info($"尝试注册热键: {hotkeyStr}");
        if (!TryParseHotkey(hotkeyStr, out uint vk, out uint mod))
        {
            Logger.Error($"热键解析失败: {hotkeyStr}");
            return false;
        }

        int id = _nextId++;
        if (!RegisterHotKey(_window.Handle, id, mod, vk))
        {
            Logger.Error($"热键注册失败: {hotkeyStr} (VK: 0x{vk:X2}, Mod: 0x{mod:X2})");
            return false;
        }
        _idToKey[id] = hotkeyStr;
        Logger.Info($"热键注册成功: {hotkeyStr} (ID: {id})");
        return true;
    }

    /// <summary>
    /// 注销所有已注册热键
    /// </summary>
    public void UnregisterAll()
    {
        Logger.Info($"注销所有热键 (共 {_idToKey.Count} 个)");
        foreach (var id in _idToKey.Keys)
        {
            if (_idToKey.TryGetValue(id, out var key))
                Logger.Info($"注销热键: {key} (ID: {id})");
                UnregisterHotKey(_window.Handle, id);
        }
        _idToKey.Clear();
        _nextId = 1;
    }

    public void Dispose()
    {
        Logger.Info("释放热键管理器");
        UnregisterAll();
        _window.Dispose();
    }

    /// <summary>
    /// 解析热键字符串，返回虚拟键码和修饰符
    /// 支持格式：F4 / Ctrl+F4 / Alt+F4 / Ctrl+Shift+A
    /// </summary>
    private static bool TryParseHotkey(string input, out uint vk, out uint mod)
    {
        vk = 0;
        mod = 0;
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
            // F键
            ["F1"] = 0x70, ["F2"] = 0x71, ["F3"] = 0x72, ["F4"] = 0x73,
            ["F5"] = 0x74, ["F6"] = 0x75, ["F7"] = 0x76, ["F8"] = 0x77,
            ["F9"] = 0x78, ["F10"] = 0x79, ["F11"] = 0x7A, ["F12"] = 0x7B,
        };

        // 字母
        for (char c = 'A'; c <= 'Z'; c++)
        {
            vkMap[c.ToString()] = (uint)c;
        }

        // 数字
        for (char c = '0'; c <= '9'; c++)
        {
            vkMap[c.ToString()] = (uint)c;
        }

        // 特殊键
        var specialKeys = new Dictionary<string, uint>
        {
            ["SPACE"] = 0x20, ["ENTER"] = 0x0D, ["RETURN"] = 0x0D,
            ["TAB"] = 0x09, ["ESC"] = 0x1B, ["ESCAPE"] = 0x1B,
            ["BACKSPACE"] = 0x08, ["DELETE"] = 0x2E, ["INSERT"] = 0x2D,
            ["HOME"] = 0x24, ["END"] = 0x23, ["PAGEUP"] = 0x21, ["PAGEDOWN"] = 0x22,
            ["LEFT"] = 0x25, ["UP"] = 0x26, ["RIGHT"] = 0x27, ["DOWN"] = 0x28
        };

        foreach (var kvp in specialKeys)
        {
            vkMap[kvp.Key] = kvp.Value;
        }

        if (!vkMap.TryGetValue(keyPart, out uint code)) return false;
        vk = code;
        mod = modifiers;
        return true;
    }

    /// <summary>
    /// 用于接收热键消息的隐藏窗口
    /// </summary>
    private class MessageWindow : Form
    {
        public event Action<int>? WmHotkeyReceived;

        public MessageWindow()
        {
            // 创建隐藏窗口
            Text = "MacroPlayerHotkeyWindow";
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            FormBorderStyle = FormBorderStyle.None;
            Size = new System.Drawing.Size(0, 0);
            Location = new System.Drawing.Point(-10000, -10000);
            Visible = false;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_HOTKEY)
            {
                Logger.Info($"收到WM_HOTKEY消息: {m.WParam.ToInt32()}");
                WmHotkeyReceived?.Invoke(m.WParam.ToInt32());
            }
        }
    }
}
