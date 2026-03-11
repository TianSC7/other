using System.Collections.Generic;
using System.Drawing;
using System.Text.Json;
using MacroPlayer.Core;
using MacroPlayer.Models;

namespace MacroPlayer.UI;

/// <summary>
/// 主窗体，用于编辑宏配置
/// </summary>
public class MainForm : Form
{
    private DataGridView gridMacros = null!;
    private TextBox txtName = null!, txtHotkey = null!, txtSequence = null!;
    private NumericUpDown numDelay = null!;
    private CheckBox chkEnabled = null!;
    private Button btnAdd = null!, btnDelete = null!, btnSave = null!, btnTest = null!;
    private Button btnToggle = null!;
    private Button btnRecordHotkey = null!, btnAddSpace = null!, btnAddEnter = null!;
    private Button btnExport = null!, btnImport = null!;
    private Label lblStatus = null!;
    private int _macroCounter = 1;
    private bool _isRecordingHotkey = false;

    public MainForm()
    {
        InitUI();
        LoadGrid();
    }

    /// <summary>
    /// 初始化UI界面
    /// </summary>
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
        numDelay = new NumericUpDown { Location = new Point(ex, ey + 185), Size = new Size(100, 25), Minimum = 10, Maximum = 2000, Value = 10 };
        Controls.Add(numDelay);
        chkEnabled = new CheckBox { Text = "启用此宏", Location = new Point(ex, ey + 220), Checked = true };
        Controls.Add(chkEnabled);

        // ── 热键录制 ──
        btnRecordHotkey = new Button
        {
            Text = "录制热键",
            Location = new Point(ex, ey + 260),
            Size = new Size(80, 30)
        };
        btnRecordHotkey.Click += Btn_RecordHotkey;
        btnRecordHotkey.KeyDown += BtnRecordHotkey_KeyDown;
        Controls.Add(btnRecordHotkey);

        // ── 特殊键按钮 ──
        btnAddSpace = new Button
        {
            Text = "添加空格",
            Location = new Point(ex + 90, ey + 260),
            Size = new Size(80, 30)
        };
        btnAddSpace.Click += (s, e) => txtSequence.AppendText("{SPACE}");
        Controls.Add(btnAddSpace);

        btnAddEnter = new Button
        {
            Text = "添加回车",
            Location = new Point(ex + 180, ey + 260),
            Size = new Size(80, 30)
        };
        btnAddEnter.Click += (s, e) => txtSequence.AppendText("{ENTER}");
        Controls.Add(btnAddEnter);

        // ── 操作按钮 ──
        btnAdd = AddButton("新增", ex, ey + 300, Btn_Add);
        btnDelete = AddButton("删除", ex + 90, ey + 300, Btn_Delete);
        btnSave = AddButton("保存", ex + 180, ey + 300, Btn_Save);
        btnToggle = AddButton("启用/禁用", ex, ey + 340, Btn_Toggle);
        btnTest = AddButton("测试", ex + 90, ey + 340, Btn_Test);

        // ── 导入导出按钮 ──
        btnExport = AddButton("导出配置", ex, ey + 390, Btn_Export);
        btnImport = AddButton("导入配置", ex + 90, ey + 390, Btn_Import);

        // ── 状态栏 ──
        lblStatus = new Label { Location = new Point(10, 420), Size = new Size(760, 20), ForeColor = Color.Gray, Text = "就绪" };
        Controls.Add(lblStatus);
    }

    /// <summary>
    /// 加载宏列表到网格
    /// </summary>
    public void LoadGrid()
    {
        gridMacros.Rows.Clear();
        foreach (var m in ConfigManager.Current.Macros)
            gridMacros.Rows.Add(m.Hotkey, m.Name, m.Sequence, m.Enabled ? "✓" : "");
    }

    /// <summary>
    /// 网格选择改变事件
    /// </summary>
    private void GridMacros_SelectionChanged(object? s, EventArgs e)
    {
        if (gridMacros.SelectedRows.Count > 0)
        {
            int idx = gridMacros.SelectedRows[0].Index;
            var m = ConfigManager.Current.Macros[idx];
            txtName.Text = m.Name;
            txtHotkey.Text = m.Hotkey;
            txtSequence.Text = m.Sequence;
            numDelay.Value = m.DelayMs;
            chkEnabled.Checked = m.Enabled;
        }
    }

    /// <summary>
    /// 新增按钮点击事件
    /// </summary>
    private void Btn_Add(object? s, EventArgs e)
    {
        var newMacro = new MacroEntry
        {
            Name = $"宏{_macroCounter++}",
            Hotkey = "",
            Sequence = "",
            DelayMs = 10,
            Enabled = true
        };
        ConfigManager.Current.Macros.Add(newMacro);
        LoadGrid();
        gridMacros.Rows[^1].Selected = true;
        SetStatus($"已新增宏：{newMacro.Name}，请填写后保存。");
    }

    /// <summary>
    /// 删除按钮点击事件
    /// </summary>
    private void Btn_Delete(object? s, EventArgs e)
    {
        if (gridMacros.SelectedRows.Count == 0) return;
        int idx = gridMacros.SelectedRows[0].Index;
        var m = ConfigManager.Current.Macros[idx];
        
        var result = MessageBox.Show($"确定要删除宏 {m.Name} 吗？", "确认删除",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            ConfigManager.Current.Macros.RemoveAt(idx);
            ConfigManager.Save();
            LoadGrid();
            SetStatus($"已删除宏：{m.Name}");
        }
    }

    /// <summary>
    /// 保存按钮点击事件
    /// </summary>
    private void Btn_Save(object? s, EventArgs e)
    {
        if (gridMacros.SelectedRows.Count == 0) return;
        int idx = gridMacros.SelectedRows[0].Index;
        var m = ConfigManager.Current.Macros[idx];
        
        if (string.IsNullOrWhiteSpace(m.Name))
        {
            MessageBox.Show("请输入宏名称", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        
        m.Name = txtName.Text;
        m.Hotkey = txtHotkey.Text;
        m.Sequence = txtSequence.Text;
        m.DelayMs = (int)numDelay.Value;
        m.Enabled = chkEnabled.Checked;
        
        ConfigManager.Save();
        LoadGrid();
        gridMacros.Rows[idx].Selected = true;
        SetStatus($"已保存宏：{m.Name}");
    }

    /// <summary>
    /// 测试按钮点击事件
    /// </summary>
    private async void Btn_Test(object? s, EventArgs e)
    {
        if (gridMacros.SelectedRows.Count == 0) return;
        int idx = gridMacros.SelectedRows[0].Index;
        var m = ConfigManager.Current.Macros[idx];
        SetStatus($"3 秒后播放宏：{m.Name} ...");
        await Task.Delay(3000);
        await Core.MacroPlayer.PlayAsync(m, ConfigManager.Current.KeyDownDuration);
        SetStatus("测试播放完毕。");
    }

    /// <summary>
    /// 启用/禁用按钮点击事件
    /// </summary>
    private void Btn_Toggle(object? s, EventArgs e)
    {
        if (gridMacros.SelectedRows.Count == 0) return;
        int idx = gridMacros.SelectedRows[0].Index;
        var m = ConfigManager.Current.Macros[idx];
        m.Enabled = !m.Enabled;
        ConfigManager.Save();
        LoadGrid();
        gridMacros.Rows[idx].Selected = true;
        SetStatus($"宏 {m.Name} 已{(m.Enabled ? "启用" : "禁用")}");
    }

    /// <summary>
    /// 导出配置按钮点击事件
    /// </summary>
    private void Btn_Export(object? s, EventArgs e)
    {
        try
        {
            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "JSON文件|*.json",
                Title = "导出配置",
                FileName = "macros.json"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                string json = System.Text.Json.JsonSerializer.Serialize(ConfigManager.Current, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(saveDialog.FileName, json);
                SetStatus($"配置已导出到：{saveDialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"导出失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 导入配置按钮点击事件
    /// </summary>
    private void Btn_Import(object? s, EventArgs e)
    {
        try
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "JSON文件|*.json",
                Title = "导入配置"
            };

            if (openDialog.ShowDialog() == DialogResult.OK)
            {
                string json = File.ReadAllText(openDialog.FileName);
                var importedConfig = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                // 收集重复的热键
                var duplicateHotkeys = new List<string>();
                int addedCount = 0;
                
                // 合并导入的宏到现有配置
                foreach (var macro in importedConfig.Macros)
                {
                    // 检查热键是否重复
                    if (!ConfigManager.Current.Macros.Any(m => m.Hotkey.Equals(macro.Hotkey, StringComparison.OrdinalIgnoreCase)))
                    {
                        ConfigManager.Current.Macros.Add(macro);
                        addedCount++;
                    }
                    else
                    {
                        duplicateHotkeys.Add(macro.Hotkey);
                    }
                }
                
                ConfigManager.Save();
                LoadGrid();
                
                // 显示导入结果
                string message = $"成功导入 {addedCount} 个宏。";
                if (duplicateHotkeys.Count > 0)
                {
                    message += $"\n\n以下热键已存在，已跳过：\n{string.Join(", ", duplicateHotkeys)}";
                    MessageBox.Show(message, "导入完成", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show(message, "导入成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                
                SetStatus($"配置已从 {openDialog.FileName} 导入");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"导入失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 热键录制按钮点击事件
    /// </summary>
    private void Btn_RecordHotkey(object? s, EventArgs e)
    {
        if (_isRecordingHotkey)
        {
            btnRecordHotkey.Text = "停止录制...";
            _isRecordingHotkey = false;
        }
        else
        {
            btnRecordHotkey.Text = "停止录制...";
            _isRecordingHotkey = true;
            txtHotkey.Text = "";
            txtHotkey.Focus();
        }
    }

    /// <summary>
    /// 热键录制键盘按下事件
    /// </summary>
    private void BtnRecordHotkey_KeyDown(object? s, KeyEventArgs e)
    {
        if (!_isRecordingHotkey) return;

        e.Handled = true;
        string hotkey = GetHotkeyString(e);
        txtHotkey.Text = hotkey;
        _isRecordingHotkey = false;
        btnRecordHotkey.Text = "录制热键";
        SetStatus($"已录制热键：{hotkey}");
    }

    /// <summary>
    /// 获取热键字符串
    /// </summary>
    /// <param name="e">按键事件参数</param>
    /// <returns>热键字符串</returns>
    private string GetHotkeyString(KeyEventArgs e)
    {
        var modifiers = new List<string>();
        if (e.Control) modifiers.Add("Ctrl");
        if (e.Alt) modifiers.Add("Alt");
        if (e.Shift) modifiers.Add("Shift");

        string key = "";
        if (e.KeyCode >= Keys.F1 && e.KeyCode <= Keys.F12)
        {
            key = $"F{e.KeyCode - Keys.F1 + 1}";
        }
        else if (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9)
        {
            key = e.KeyCode.ToString();
        }
        else if (char.IsLetterOrDigit((char)e.KeyCode))
        {
            key = char.ToUpper((char)e.KeyCode).ToString();
        }

        if (modifiers.Count > 0)
        {
            return $"{string.Join("+", modifiers)}+{key}";
        }
        return key;
    }

    /// <summary>
    /// 设置状态栏文本
    /// </summary>
    /// <param name="text">状态文本</param>
    private void SetStatus(string text)
    {
        lblStatus.Text = text;
    }

    /// <summary>
    /// 添加标签
    /// </summary>
    private Label AddLabel(string text, int x, int y)
    {
        var lbl = new Label { Text = text, Location = new Point(x, y), AutoSize = true };
        Controls.Add(lbl);
        return lbl;
    }

    /// <summary>
    /// 添加文本框
    /// </summary>
    private TextBox AddTextBox(int x, int y, int w)
    {
        var txt = new TextBox { Location = new Point(x, y), Size = new Size(w, 25) };
        Controls.Add(txt);
        return txt;
    }

    /// <summary>
    /// 添加按钮
    /// </summary>
    private Button AddButton(string text, int x, int y, EventHandler onClick)
    {
        var btn = new Button { Text = text, Location = new Point(x, y), Size = new Size(80, 30) };
        btn.Click += onClick;
        Controls.Add(btn);
        return btn;
    }
}
