# AI辅助AutoLISP开发实用手册

## 一、调试工具使用方法

### Visual LISP IDE 操作步骤

#### 断点调试
- **设置断点**：`Debug » Toggle Break Point (F9)`
- **错误自动断点**：`Debug » Break on Error`
- **跳转到错误位置**：`Debug » Last Break Source (Ctrl+F9)`

#### 变量监视
- **打开监视窗口**：`View » Watch Window (Ctrl+Shift+W)`
- **添加变量**：选中变量 → 右键 → Add Watch
- **查看变量值**：双击变量查看数据类型和当前值

#### 逐步执行
- **逐表达式执行**：`Debug » Animate` + `Debug » Continue (Ctrl+F8)`
- **重置环境**：`Debug » Reset to Top Level (Ctrl+R)`

#### 语法检查
- **检查语法**：`Tools » Check` 或直接使用Check命令
- **查看错误**：双击错误信息跳转到对应代码行

### VS Code AutoLISP扩展配置

#### 调试配置
```json
{
    "type": "autolisp",
    "request": "attach",
    "name": "AutoCAD调试附加"
}
```

#### 快速操作
- **加载文件到AutoCAD**：右键LSP文件 → "将文件加载到AutoCAD"
- **项目管理**：创建.prj项目文件管理多个LSP文件

## 二、自动化测试代码

### LISP测试框架
```lisp
(defvar *test-cases* nil)

(defun define-test (name &body body) 
  (push (list name body) *test-cases*))

(defun run-tests () 
  (dolist (test *test-cases*) 
    (let ((name (first test)) (body (second test))) 
      (handler-case 
        (progn (funcall body) 
               (format t "Test ~A passed.~%" name))
        (error (e) 
          (format t "Test ~A failed: ~A~%" name e))))))
```

### Python自动化控制AutoCAD

#### 使用pyautocad
```python
from pyautocad import Autocad, APoint

# 连接到AutoCAD
acad = Autocad(create_if_not_exists=True)

# 添加图元示例
acad.model.AddLine(APoint(0, 0, 0), APoint(100, 100, 0))
acad.model.AddCircle(APoint(50, 50, 0), 25)

# 获取当前文件名
print(acad.doc.Name)
```

#### 使用win32com
```python
import win32com.client

# 获取AutoCAD应用
acad = win32com.client.Dispatch("AutoCAD.Application")

# 发送命令
acad.ActiveDocument.SendCommand("(load \"myscript.lsp\") ")
```

#### 命令行状态处理
```python
import win32com.client
import time

def safe_send_command(acad, command):
    try:
        # 检查AutoCAD状态
        state = acad.GetAcadState()
        if not state.IsQuiescent:
            time.sleep(1)
        
        # 发送命令
        acad.ActiveDocument.SendCommand(command)
    except Exception as e:
        # 清理并重试
        acad.ActiveDocument.SendCommand("^C^C")
        time.sleep(0.5)
        acad.ActiveDocument.SendCommand(command)
```

## 三、AutoLISP自动加载配置

### 方法1：Startup Suite
1. 运行 `APPLOAD` 命令
2. 点击 `Startup Suite` 下的 `Contents` 按钮
3. 添加需要自动加载的LSP文件

### 方法2：acad.lsp文件
在AutoCAD安装目录创建 `acad.lsp` 文件：
```lisp
;; 自动加载的代码
(load "myutils.lsp")
(load "customcommands.lsp")

;; 启动时执行的函数
(defun S::STARTUP ()
    (princ "\n自定义工具已加载")
    (princ)
)
```

### 方法3：acaddoc.lsp文件
```lisp
;; 每次打开图纸时执行
(defun S::STARTUP ()
    ;; 恢复常用设置
    (setvar "CMDECHO" 0)
    (setvar "BLIPMODE" 0)
    (princ)
)
```

## 四、实用的AI协作方法

### "小步闭环"操作步骤

#### 第1步：需求描述
- 单一明确目标，15-30分钟可完成
- 示例：修复选择集获取函数，确保正确返回线段对象

#### 第2步：代码生成
- 要求AI生成最小修复实现
- 明确指定使用的函数和方法

#### 第3步：测试创建
```lisp
;; 测试示例
(defun test-select-lines ()
    (setq ss (ssget "_X" '((0 . "LINE"))))
    (if ss
        (princ (strcat "找到 " (itoa (sslength ss)) " 条线段"))
        (princ "未找到线段"))
)
```

#### 第4步：Bug修复
- 每次只处理一个明确问题
- 使用VLIDE调试工具定位错误

#### 第5步：代码提交
- 提交到版本控制
- 开启新对话窗口处理下一个问题

### 有效提示词模板
```
目标：[具体功能描述]
AutoCAD版本：2024
输入：[参数说明]
输出：[返回值说明]
要求：
1. 使用ssget函数获取选择集
2. 包含错误处理（选择集为空时返回nil）
3. 函数名为[指定函数名]
```

## 五、常见错误解决方案

### 选择集错误
```lisp
;; 错误示例
(setq ss (ssget)) ; 用户可能取消选择

;; 正确示例
(setq ss (ssget '((0 . "LINE"))))
(if (null ss)
    (princ "\n未选择线段")
    (progn
        ;; 处理选择集
    )
)
```

### 括号匹配检查
```lisp
;; 快速检查方法
(defun check-parentheses (str)
    (setq count 0)
    (foreach char (vl-string->list str)
        (if (= char 40) ; 左括号
            (setq count (1+ count))
        )
        (if (= char 41) ; 右括号
            (setq count (1- count))
        )
    )
    (= count 0)
)
```

### 命令行状态清理
```lisp
;; 强制取消当前命令
(command "_.undo" "_end")
(command "_.undo" "_back")
```

## 六、性能优化技巧

### BricsCAD替代方案
- 运行速度：比AutoCAD快2.8倍
- 许可成本：永久许可约为AutoCAD年费的一半
- 兼容性：完全支持AutoLISP和DWG文件

### 批量测试脚本
```python
import os
import win32com.client

def batch_test_lsp_files(folder_path):
    acad = win32com.client.Dispatch("AutoCAD.Application")
    
    for file in os.listdir(folder_path):
        if file.endswith('.lsp'):
            try:
                # 新建图纸
                doc = acad.Documents.Add()
                
                # 加载LSP文件
                lsp_path = os.path.join(folder_path, file)
                doc.SendCommand(f"(load \"{lsp_path}\") ")
                
                # 运行测试
                doc.SendCommand("(test-function) ")
                
                # 关闭图纸
                doc.Close(False)
                
                print(f"✓ {file} 测试通过")
            except Exception as e:
                print(f"✗ {file} 测试失败: {e}")
```

## 七、调试检查清单

### 代码审查要点
- [ ] 所有函数调用参数数量正确
- [ ] 选择集检查是否为空
- [ ] 图形对象操作包含错误处理
- [ ] 系统变量修改后是否恢复
- [ ] 括号完全匹配
- [ ] 变量命名一致

### 测试步骤
1. **语法检查**：VLIDE Check命令
2. **单元测试**：测试框架验证纯函数
3. **集成测试**：AutoCAD环境验证
4. **回归测试**：确保修复不引入新问题

## 八、实用代码片段

### 安全的实体操作
```lisp
(defun safe-entmod (ename elist)
    (if (and ename elist)
        (if (entmod elist)
            T
            (progn
                (princ "\n实体修改失败")
                nil
            )
        )
    )
)
```

### 获取用户输入
```lisp
(defun get-user-point (prompt)
    (setq pt (getpoint prompt))
    (if pt
        pt
        (progn
            (princ "\n用户取消输入")
            nil
        )
    )
)
```

### 图层检查
```lisp
(defun ensure-layer-exists (layername)
    (if (tblsearch "LAYER" layername)
        T
        (progn
            (command "_LAYER" "_N" layername "")
            (tblsearch "LAYER" layername)
        )
    )
)
