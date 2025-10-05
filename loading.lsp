;; loading.lsp - 自动加载当前文件夹内所有LSP文件
;; 功能: 自动加载当前文件夹内所有LSP文件，除了loading.lsp自身
;; 作者: CodeBuddy
;; 日期: 2025/9/5

(defun c:LOADALL (/ current-dir file-list file-path)
  (princ "\n=== 自动加载LSP文件系统 ===")
  
  ;; 获取当前LSP文件所在目录
  (setq current-dir (vl-filename-directory (findfile "loading.lsp")))
  (princ (strcat "\n[INFO] 当前目录: " current-dir))
  
  ;; 获取目录中所有文件
  (setq file-list (vl-directory-files current-dir "*.lsp" 1))
  (princ (strcat "\n[INFO] 找到 " (itoa (length file-list)) " 个LSP文件"))
  
  ;; 遍历并加载所有LSP文件（除了loading.lsp自身）
  (foreach file file-list
    (if (/= (strcase file) "LOADING.LSP")
      (progn
        (setq file-path (strcat current-dir "\\" file))
        (princ (strcat "\n[INFO] 正在加载: " file))
        (load file-path)
      )
      (princ (strcat "\n[INFO] 跳过自身: " file))
    )
  )
  
  (princ "\n=== 所有LSP文件加载完成 ===")
  (princ)
)

;; 自动执行加载
(c:LOADALL)

(princ "\n已加载 loading.lsp - 输入 LOADALL 可重新加载所有LSP文件")
(princ)