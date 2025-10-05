;; HQT8.lsp - 插座图块自动放置系统

;; 获取LSP文件所在目录
(defun get-lsp-directory ()
  (setq script-path (getvar "MENUNAME"))
  (if script-path
    (vl-filename-directory script-path)
    "c:\\Users\\TIAN\\Documents\\2025\\September\\lsp"
  )
)

;; 解析坐标字符串
(defun parse-coords (coord-str)
  (setq coord-list '())
  (setq parts (vl-string->list coord-str))
  (setq current-num "")
  (setq char-list parts)
  
  (while char-list
    (setq char (car char-list))
    (cond
      ((= char 44) ;; 逗号
       (if (> (strlen current-num) 0)
         (progn
           (setq coord-list (append coord-list (list (atof current-num))))
           (setq current-num "")
         )
       )
      )
      ((or (and (>= char 48) (<= char 57)) (= char 46) (= char 45)) ;; 数字、小数点、负号
       (setq current-num (strcat current-num (chr char)))
      )
    )
    (setq char-list (cdr char-list))
  )
  
  ;; 处理最后一个数字
  (if (> (strlen current-num) 0)
    (setq coord-list (append coord-list (list (atof current-num))))
  )
  
  coord-list
)

;; 读取wall4.txt文件获取插座坐标数据
(defun read-wall4-data ()
  (setq lsp-dir (get-lsp-directory))
  (setq wall4-file (strcat lsp-dir "\\wall4.txt"))
  (if (not (findfile wall4-file))
    (setq wall4-file "c:\\Users\\TIAN\\Documents\\2025\\September\\lsp\\wall4.txt")
  )
  
  (if (findfile wall4-file)
    (progn
      (setq file (open wall4-file "r"))
      (setq coords-list '())
      (while (setq line (read-line file))
        (if (and line (> (strlen line) 0) (vl-string-search "p" line))
          (progn
            ;; 提取坐标数据
            (setq start-pos (vl-string-search " " line))
            (if start-pos
              (progn
                (setq coords-str (substr line (+ start-pos 2)))
                (setq coords-str (vl-string-subst "" ")" coords-str))
                (setq coord-data (parse-coords coords-str))
                (if (and coord-data (>= (length coord-data) 3))
                  (setq coords-list (append coords-list (list coord-data)))
                )
              )
            )
          )
        )
      )
      (close file)
      coords-list
    )
    (progn
      (princ "\n错误：找不到wall4.txt文件")
      nil
    )
  )
)

;; 加载图块定义（如果不存在）
(defun load-block-if-not-exists (blockname filepath)
  (if (not (tblsearch "BLOCK" blockname))
    (progn
      (princ (strcat "\n加载图块: " blockname))
      (command "_-INSERT" (strcat filepath "," blockname) "0,0" "" "" "")
      (command "_.erase" "last" "") ;; 删除临时块插入
      (if (tblsearch "BLOCK" blockname)
        (princ (strcat "\n图块 " blockname " 加载成功"))
        (princ (strcat "\n警告：图块 " blockname " 加载失败"))
      )
    )
    (princ (strcat "\n图块 " blockname " 已存在"))
  )
  (tblsearch "BLOCK" blockname)
)

;; 在指定坐标插入图块
(defun insert-block-at-point (blockname point)
  (if point
    (progn
      (command "_INSERT" blockname point "" "" "")
      (if (entlast)
        (progn
          (princ (strcat "\n插入图块 " blockname " 在点 (" 
                        (rtos (car point) 2 4) "," 
                        (rtos (cadr point) 2 4) "," 
                        (rtos (caddr point) 2 4) ")"))
          T
        )
        (progn
          (princ (strcat "\n错误：无法在点 (" 
                        (rtos (car point) 2 4) "," 
                        (rtos (cadr point) 2 4) ") 插入图块 " blockname))
          nil
        )
      )
    )
    (progn
      (princ "\n错误：插入点坐标为空")
      nil
    )
  )
)

;; 主函数HQT8 - 直接在所有坐标点放置插座图块
(defun C:HQT8 ()
  (princ "\n=== HQT8 插座图块自动放置 ===\n")
  
  ;; 关闭对象捕捉
  (setq old-osnap (getvar "OSMODE"))
  (setvar "OSMODE" 0)
  
  ;; 读取wall4.txt坐标数据
  (setq coords-data (read-wall4-data))
  
  (if coords-data
    (progn
      (princ (strcat "找到 " (itoa (length coords-data)) " 个坐标点，开始放置插座..."))
      
      ;; 设置图块文件路径
      (setq lsp-dir (get-lsp-directory))
      (setq block-file (strcat lsp-dir "\\chazuo.dwg"))
      (if (not (findfile block-file))
        (setq block-file "c:\\Users\\TIAN\\Documents\\2025\\September\\lsp\\chazuo.dwg")
      )
      
      ;; 加载图块（如果需要）
      (if (findfile block-file)
        (progn
          (if (not (tblsearch "BLOCK" "chazuo"))
            (progn
              (princ "\n加载图块...")
              (command "_-INSERT" (strcat block-file ",chazuo") "0,0" "" "" "")
              (command "_.erase" "last" "")
            )
          )
          
          ;; 直接在每个点放置图块
          (setq count 0)
          (foreach coord coords-data
            (setq count (+ count 1))
            (princ (strcat "\n放置第 " (itoa count) " 个插座..."))
            (command "_INSERT" "chazuo" coord "" "" "")
          )
          
          (princ (strcat "\n\n✅ 成功放置 " (itoa count) " 个插座图块！"))
        )
        (princ "\n⚠️ 错误：找不到chazuo.dwg文件")
      )
    )
    (princ "\n⚠️ 没有找到坐标数据")
  )
  
  ;; 恢复设置
  (setvar "OSMODE" old-osnap)
  
  ;; 提示完成信息，可以手动运行HQT9
  (princ "\n\n===== HQT8处理完成 =====")
  (princ "\n可以输入 HQT9 继续下一步")
  ;; (command "HQT9") ; 注释掉自动跳转，避免流程控制问题
  
  (princ)
)

(princ "\nHQT8插座图块放置系统已加载，输入HQT8开始执行")
(princ)