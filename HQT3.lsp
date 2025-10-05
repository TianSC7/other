;; HQT3.lsp - 智能点处理程序 (UTF-8编码)
;; 功能: 读取 wall.txt 文件，提取各类型点的最小最大值，写入 wall2.txt
;; 只处理存在的点类型，跳过不存在的 x 和 x1 点
;; 运行结束后自动启动 HQT2

(defun c:HQT3 (/ cmd_echo lsp_dir wall_filepath wall2_filepath fp lines line
                 parsed_data pt point_name x y z coords_str trimmed_line len
                 start end tag coord_str comma1 comma2
                 x_points x1_points y_points y1_points z_points z1_points
                 x_min_pt x_max_pt x1_min_pt x1_max_pt
                 y_min_pt y_max_pt y1_min_pt y1_max_pt
                 z_min_pt z_max_pt z1_min_pt z1_max_pt
                 fout)
    
    ; 保存当前命令回显状态
    (setq cmd_echo (getvar 'CMDECHO))
    (setvar 'CMDECHO 0)
    
    ;;; 步骤 1: 获取 wall.txt 文件路径
    (setq lsp_dir (vl-filename-directory (findfile "HQT3.lsp")))
    (if lsp_dir
        (progn
            (setq wall_filepath (strcat lsp_dir "\\wall.txt"))
            (setq wall2_filepath (strcat lsp_dir "\\wall2.txt"))
        )
        (progn
            (princ "\n错误: 无法确定 HQT3.lsp 文件目录。")
            (setvar 'CMDECHO cmd_echo)
            (exit)
        )
    )
    
    ;;; 步骤 2: 检查文件是否存在
    (if (findfile wall_filepath)
        (progn
            ; 文件存在，继续
        )
        (progn
            (princ (strcat "\n错误: 找不到文件 " wall_filepath))
            (setvar 'CMDECHO cmd_echo)
            (exit)
        )
    )
    
    ;;; 步骤 3: 读取文件内容到列表
    (setq fp (open wall_filepath "r"))
    (if fp
        (progn
            (setq lines '())
            (while (setq line (read-line fp))
                (if (and line (/= (strlen line) 0) (/= (substr line 1 1) ";")) ; 忽略空行和注释
                    (setq lines (cons line lines))
                )
            )
            (close fp)
            (setq lines (reverse lines)) ; 恢复原始顺序
        )
        (progn
            (princ "\n错误: 无法打开文件进行读取。")
            (setvar 'CMDECHO cmd_echo)
            (exit)
        )
    )
    
    ;;; 步骤 4: 解析每一行的点数据
    (setq parsed_data '())
    (foreach l lines
        (setq point_name nil)
        (setq x nil)
        (setq y nil)
        (setq z nil)
        (setq coords_str nil)
        
        ; 去除行首尾空格
        (setq trimmed_line (vl-string-trim " \t" l))
        (setq len (strlen trimmed_line))
        
        ; 检查基本格式 (以 '(' 开头，以 ')' 结尾)
        (if (and (> len 2) (= (substr trimmed_line 1 1) "(") (= (substr trimmed_line len 1) ")"))
            (progn
                ; 查找点名结束的位置 (遇到空格、制表符或 ')')
                (setq i 2) ; 从 '(' 之后开始
                (while (and (<= i len) 
                           (not (member (substr trimmed_line i 1) '(" " "\t" ")"))))
                    (setq i (1+ i))
                )
                (setq point_name_end (1- i)) ; 点名结束位置
                
                ; 提取点名字符串
                (if (>= point_name_end 2)
                    (progn
                        (setq point_name (substr trimmed_line 2 (- point_name_end 1)))
                        
                        ; 查找坐标开始位置 (跳过空格/制表符)
                        (setq coords_start i)
                        (while (and (<= coords_start len) 
                                   (member (substr trimmed_line coords_start 1) '(" " "\t")))
                            (setq coords_start (1+ coords_start))
                        )
                        
                        ; 提取坐标字符串 (直到 ')' 之前)
                        (if (and (<= coords_start len) (< point_name_end coords_start))
                            (progn
                                (setq coords_str (substr trimmed_line coords_start (- len coords_start)))
                                ; 去掉末尾的 ')'
                                (if (= (substr coords_str (strlen coords_str) 1) ")")
                                    (setq coords_str (substr coords_str 1 (1- (strlen coords_str))))
                                )
                                
                                ; 解析坐标
                                (setq comma1 (vl-string-search "," coords_str))
                                (setq comma2 (vl-string-search "," coords_str (+ comma1 1)))
                                
                                (if (and comma1 comma2)
                                    (progn
                                        (setq x (atof (substr coords_str 1 comma1)))
                                        (setq y (atof (substr coords_str (+ comma1 2) (- comma2 comma1 1))))
                                        (setq z (atof (substr coords_str (+ comma2 2))))
                                        
                                        ; 将解析后的点信息存入列表
                                        (setq parsed_data (cons (list point_name x y z) parsed_data))
                                    )
                                    (princ (strcat "\n警告: 行 \"" l "\" 坐标格式不正确，已跳过。"))
                                )
                            )
                            (princ (strcat "\n警告: 行 \"" l "\" 格式解析失败，已跳过。"))
                        )
                    )
                    (princ (strcat "\n警告: 行 \"" l "\" 未找到有效点名，已跳过。"))
                )
            )
            (princ (strcat "\n警告: 行 \"" l "\" 格式不正确，已跳过。"))
        )
    )
    ; 恢复原始顺序
    (setq parsed_data (reverse parsed_data))
    
    ;;; 步骤 5: 根据标签分组点数据
    (setq x_points '())
    (setq x1_points '())
    (setq y_points '())
    (setq y1_points '())
    (setq z_points '())
    (setq z1_points '())
    
    (foreach pt parsed_data
        (setq point_name (car pt))
        (cond
            ((wcmatch point_name "xpt*") (setq x_points (cons pt x_points)))
            ((wcmatch point_name "x1pt*") (setq x1_points (cons pt x1_points)))
            ((wcmatch point_name "ypt*") (setq y_points (cons pt y_points)))
            ((wcmatch point_name "y1pt*") (setq y1_points (cons pt y1_points)))
            ((wcmatch point_name "zpt*") (setq z_points (cons pt z_points)))
            ((wcmatch point_name "z1pt*") (setq z1_points (cons pt z1_points)))
        )
    )
    
    ;;; 步骤 6: 对每组点进行排序并找到最小和最大点
    ; 对x点按Y坐标排序并提取最小最大点
    (if x_points
        (progn
            (setq x_points (vl-sort x_points '(lambda (a b) (< (caddr a) (caddr b)))))
            (setq x_min_pt (car x_points))
            (setq x_max_pt (last x_points))
        )
    )
    
    ; 对x1点按Y坐标排序并提取最小最大点
    (if x1_points
        (progn
            (setq x1_points (vl-sort x1_points '(lambda (a b) (< (caddr a) (caddr b)))))
            (setq x1_min_pt (car x1_points))
            (setq x1_max_pt (last x1_points))
        )
    )
    
    ; 对y点按X坐标排序并提取最小最大点
    (if y_points
        (progn
            (setq y_points (vl-sort y_points '(lambda (a b) (< (cadr a) (cadr b)))))
            (setq y_min_pt (car y_points))
            (setq y_max_pt (last y_points))
        )
    )
    
    ; 对y1点按X坐标排序并提取最小最大点
    (if y1_points
        (progn
            (setq y1_points (vl-sort y1_points '(lambda (a b) (< (cadr a) (cadr b)))))
            (setq y1_min_pt (car y1_points))
            (setq y1_max_pt (last y1_points))
        )
    )
    
    ; 对z点按Y坐标排序并提取最小最大点
    (if z_points
        (progn
            (setq z_points (vl-sort z_points '(lambda (a b) (< (caddr a) (caddr b)))))
            (setq z_min_pt (car z_points))
            (setq z_max_pt (last z_points))
        )
    )
    
    ; 对z1点按Y坐标排序并提取最小最大点
    (if z1_points
        (progn
            (setq z1_points (vl-sort z1_points '(lambda (a b) (< (caddr a) (caddr b)))))
            (setq z1_min_pt (car z1_points))
            (setq z1_max_pt (last z1_points))
        )
    )
    
    ;;; 步骤 7: 写入结果到 wall2.txt (只写入存在的点类型)
    (setq fout (open wall2_filepath "w"))
    (if fout
        (progn
            (princ "\n[INFO] 开始写入存在的点到 wall2.txt...")
            
            ; 只在有x点时才写入x相关的点
            (if (and x_points x_min_pt x_max_pt)
                (progn
                    (write-line (strcat "(xpt1 " (rtos (cadr x_min_pt) 2 6) "," (rtos (caddr x_min_pt) 2 6) "," (rtos (cadddr x_min_pt) 2 6) ")") fout)
                    (write-line (strcat "(xpt2 " (rtos (cadr x_max_pt) 2 6) "," (rtos (caddr x_max_pt) 2 6) "," (rtos (cadddr x_max_pt) 2 6) ")") fout)
                    (princ "\n[INFO] 已写入 x 相关点")
                )
                (princ "\n[INFO] 跳过 x 相关点（不存在）")
            )
            
            ; 只在有x1点时才写入x1相关的点
            (if (and x1_points x1_min_pt x1_max_pt)
                (progn
                    (write-line (strcat "(x1pt1 " (rtos (cadr x1_min_pt) 2 6) "," (rtos (caddr x1_min_pt) 2 6) "," (rtos (cadddr x1_min_pt) 2 6) ")") fout)
                    (write-line (strcat "(x1pt2 " (rtos (cadr x1_max_pt) 2 6) "," (rtos (caddr x1_max_pt) 2 6) "," (rtos (cadddr x1_max_pt) 2 6) ")") fout)
                    (princ "\n[INFO] 已写入 x1 相关点")
                )
                (princ "\n[INFO] 跳过 x1 相关点（不存在）")
            )
            
            ; y相关的点（通常存在）
            (if (and y_points y_min_pt y_max_pt)
                (progn
                    ; 检查x和z是否都等于0
                    (if (and 
                          (or (null x_points) (equal (cadr x_min_pt) 0.0 1e-6)) ; x=0 或不存在
                          (or (null z_points) (equal (cadr z_min_pt) 0.0 1e-6)) ; z=0 或不存在
                      )
                        (progn
                            ; 当x和z等于0时，输出y线坐标（保持原格式）
                            (write-line (strcat "(ypt1 " (rtos (cadr y_min_pt) 2 6) "," (rtos (caddr y_min_pt) 2 6) "," (rtos (cadddr y_min_pt) 2 6) ")") fout)
                            (write-line (strcat "(ypt2 " (rtos (cadr y_max_pt) 2 6) "," (rtos (caddr y_max_pt) 2 6) "," (rtos (cadddr y_max_pt) 2 6) ")") fout)
                            (princ "\n[INFO] x和z等于0，输出y线坐标")
                        )
                        (progn
                            ; 正常输出x,y,z坐标
                            (write-line (strcat "(ypt1 " (rtos (cadr y_min_pt) 2 6) "," (rtos (caddr y_min_pt) 2 6) "," (rtos (cadddr y_min_pt) 2 6) ")") fout)
                            (write-line (strcat "(ypt2 " (rtos (cadr y_max_pt) 2 6) "," (rtos (caddr y_max_pt) 2 6) "," (rtos (cadddr y_max_pt) 2 6) ")") fout)
                            (princ "\n[INFO] 已写入 y 相关点")
                        )
                    )
                )
            )
            
            ; y1相关的点（通常存在）
            (if (and y1_points y1_min_pt y1_max_pt)
                (progn
                    ; 检查x和z是否都等于0
                    (if (and 
                          (or (null x_points) (equal (cadr x_min_pt) 0.0 1e-6)) ; x=0 或不存在
                          (or (null z_points) (equal (cadr z_min_pt) 0.0 1e-6)) ; z=0 或不存在
                      )
                        (progn
                            ; 当x和z等于0时，输出y1线坐标（保持原格式）
                            (write-line (strcat "(y1pt1 " (rtos (cadr y1_min_pt) 2 6) "," (rtos (caddr y1_min_pt) 2 6) "," (rtos (cadddr y1_min_pt) 2 6) ")") fout)
                            (write-line (strcat "(y1pt2 " (rtos (cadr y1_max_pt) 2 6) "," (rtos (caddr y1_max_pt) 2 6) "," (rtos (cadddr y1_max_pt) 2 6) ")") fout)
                            (princ "\n[INFO] x和z等于0，输出y1线坐标")
                        )
                        (progn
                            ; 正常输出x,y,z坐标
                            (write-line (strcat "(y1pt1 " (rtos (cadr y1_min_pt) 2 6) "," (rtos (caddr y1_min_pt) 2 6) "," (rtos (cadddr y1_min_pt) 2 6) ")") fout)
                            (write-line (strcat "(y1pt2 " (rtos (cadr y1_max_pt) 2 6) "," (rtos (caddr y1_max_pt) 2 6) "," (rtos (cadddr y1_max_pt) 2 6) ")") fout)
                            (princ "\n[INFO] 已写入 y1 相关点")
                        )
                    )
                )
            )
            
            ; z相关的点（通常存在）
            (if (and z_points z_min_pt z_max_pt)
                (progn
                    (write-line (strcat "(zpt1 " (rtos (cadr z_min_pt) 2 6) "," (rtos (caddr z_min_pt) 2 6) "," (rtos (cadddr z_min_pt) 2 6) ")") fout)
                    (write-line (strcat "(zpt2 " (rtos (cadr z_max_pt) 2 6) "," (rtos (caddr z_max_pt) 2 6) "," (rtos (cadddr z_max_pt) 2 6) ")") fout)
                    (princ "\n[INFO] 已写入 z 相关点")
                )
            )
            
            ; z1相关的点（通常存在）
            (if (and z1_points z1_min_pt z1_max_pt)
                (progn
                    (write-line (strcat "(z1pt1 " (rtos (cadr z1_min_pt) 2 6) "," (rtos (caddr z1_min_pt) 2 6) "," (rtos (cadddr z1_min_pt) 2 6) ")") fout)
                    (write-line (strcat "(z1pt2 " (rtos (cadr z1_max_pt) 2 6) "," (rtos (caddr z1_max_pt) 2 6) "," (rtos (cadddr z1_max_pt) 2 6) ")") fout)
                    (princ "\n[INFO] 已写入 z1 相关点")
                )
            )
            
            (close fout)
            (princ (strcat "\n处理完成，结果已保存到: " wall2_filepath))
        )
        (princ (strcat "\n错误: 无法打开输出文件 " wall2_filepath))
    )
    
    ; 恢复命令回显状态
    (setvar 'CMDECHO cmd_echo)
    
    ; 运行结束后启动HQT2
    (c:HQT2)
    
    (princ)
)

(princ "\n已加载 HQT3 - 智能点处理程序 (UTF-8编码)")
(princ "\n符合HQT规范:")
(princ "\n  1. 在没有x和x1点时只输出y、y1、z、z1点")
(princ "\n  2. 当x=0时也调用HQT3")
(princ "\n  3. HQT3运行结束后启动HQT2")
(princ)
