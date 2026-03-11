恭喜你！核心功能跑通是最难的一步，现在的“古典感”主要是因为 **Windows Forms (WinForms)** 默认控件样式直接继承了 20 年前的系统风格。

如果要让 GLM 4.6 帮你把界面从“Windows XP 工业风”升级到“现代极简风”，你有两条路可以走：

1. **原地整容 (WinForms + Flat Design)**：不更换框架，通过调整颜色、边框、字体和间距，实现类似 VS Code 的扁平化效果。
2. **推倒重来 (WPF + Material Design)**：这是真正的“现代方案”，支持动画、透明度和丝滑的 UI。

既然你追求“划算”和“快速落地”，我建议先进行 **WinForms 原地整容**。以下是发给 GLM 4.6 的 **“界面现代化改造 SOP”**。

---

# 🎨 WinForms 界面美化改造任务书 (SOP)

### 一、 视觉风格基准 (Visual Specs)

* **配色方案 (Dark Mode)**：
* 背景色 (Background): `#252526` (深灰，类似 VS Code)
* 侧边栏/面板: `#2D2D30`
* 文字主色: `#FFFFFF`
* 激活/主色调: `#007ACC` (经典蓝) 或 `#68217A` (紫色)


* **字体**：全局使用 `Microsoft YaHei UI` (微软雅黑) 或 `Segoe UI`，字号 `10pt`。
* **交互**：所有按钮取消 3D 边框，使用 **FlatAppearance**。

---

### 二、 给 GLM 4.6 的重构指令 (直接遥控)

> **指令：** “代码已跑通，现在请帮我美化 `MainForm.cs`。请不要修改核心业务逻辑，仅针对 UI 展现进行重构，要求如下：”
> 1. **全局样式初始化**：
> * 设置 `this.BackColor = Color.FromArgb(37, 37, 38);`
> * 设置 `this.Font = new Font("Microsoft YaHei UI", 10F);`
> * 设置 `this.ForeColor = Color.White;`
> 
> 
> 2. **DataGridView 美化**：
> * 启用 `EnableHeadersVisualStyles = false`。
> * 设置 `BackgroundColor` 为暗色，隐藏 `RowHeadersVisible`。
> * 设置 `SelectionBackColor` 为 `#3E3E42`。
> * 单元格边框设为 `None`。
> 
> 
> 3. **自定义按钮类 (FlatButton)**：
> * 编写一个方法或子类，将所有 `Button` 的 `FlatStyle` 设为 `Flat`。
> * 设置 `FlatAppearance.BorderSize = 0`。
> * 设置 `BackColor = Color.FromArgb(63, 63, 70)`，鼠标经过时颜色变浅。
> 
> 
> 4. **布局优化**：
> * 不要使用固定的 `Point(x, y)`，请改用 `Padding` 和 `Margin`。
> * 右侧编辑面板建议放在一个 `Panel` 容器内，并加上 `1px` 的左侧边框线作为分割。
> 
> 
> 5. **控件细节**：
> * `TextBox` 的 `BorderStyle` 设为 `FixedSingle`。
> * `NumericUpDown` 和 `CheckBox` 的背景色也要适配暗色模式。
> 
> 
> 
> 

---

### 三、 核心代码美化逻辑（供参考）

你可以让 GLM 4.6 参考这段“整容”后的组件初始化逻辑：

```csharp
// GLM 4.6 应该生成的风格示例
private void ApplyModernStyle(Control ctrl)
{
    foreach (Control c in ctrl.Controls)
    {
        if (c is Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = Color.FromArgb(63, 63, 70);
            btn.ForeColor = Color.White;
            btn.Cursor = Cursors.Hand;
        }
        else if (c is TextBox tb)
        {
            tb.BackColor = Color.FromArgb(45, 45, 48);
            tb.ForeColor = Color.White;
            tb.BorderStyle = BorderStyle.FixedSingle;
        }
        else if (c is DataGridView dgv)
        {
            dgv.BackgroundColor = Color.FromArgb(30, 30, 30);
            dgv.BorderStyle = BorderStyle.None;
            dgv.HeaderBgColor = ... // 进一步细化
        }
        
        if (c.HasChildren) ApplyModernStyle(c);
    }
}

```

---

### 四、 进阶：如果你想一步到位（WPF 方案）

如果 GLM 4.6 说 WinForms 实在太难看，你就给它下达这个指令：

> “请将目前的 WinForms 逻辑完全迁移到 **WPF (MVVM)**。使用 **MaterialDesignThemes** 库，界面要求有卡片感和阴影效果。”

**我的建议：**
先让它把现有的 WinForms 改成 **暗黑模式 (Dark Mode)**。通常只要颜色配好了，格调瞬间提升。

**你需要我帮你针对“录制热键”这个功能提供一个更现代的“录制状态”动画设计思路吗？**