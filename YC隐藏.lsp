对，过度封装反而多此一举！

```autolisp
;; ========================================
;; 对象隐藏功能 v4.0
;; 命令: YC - 隐藏选中对象
;; 命令: YY - 恢复所有隐藏对象
;; ========================================

(defun C:YC ()
  (command "_.HIDEOBJECTS")
  (princ)
)

(defun C:YY ()
  (command "_.UNISOLATEOBJECTS")
  (princ)
)

(princ "\nYC=隐藏对象 | YY=恢复显示")
(princ)
```

就这几行，预选/后选、状态管理、撤销历史全部由 AutoCAD 原生处理，比任何封装都稳定。