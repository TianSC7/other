;; HQT6.lsp - 楼层高度绘制系统 (UTF-8编码)
;; 功能: 读取CSV文件和wall2.txt，根据楼层高度数据自动绘制闭合多边形
;; 表头为楼层高度，支持乱序表头识别
;; 从choose.txt读取行数，无需用户选择
;; 命令: HQT6

;; 精确匹配表头名称获取数据
(defun get-value-by-name (target_name data / result)
    (setq result nil)
    (foreach pair data
        (if (= (car pair) target_name)
            (if (and (cdr pair) (/= (cdr pair) ""))
                (setq result (cdr pair))
            )
        )
    )
    result
)

;; 创建或切换到指定图层
(defun create-or-set-layer (layer_name / )
    ;; 检查图层是否存在，如果不存在则创建
    (if (null (tblsearch "LAYER" layer_name))
        (progn
            (command "_.LAYER" "_NEW" layer_name "")
            (princ (strcat "\n[INFO] 已创建图层: " layer_name))
        )
        (princ (strcat "\n[INFO] 图层已存在: " layer_name))
    )
    ;; 设置当前图层
    (command "_.LAYER" "_SET" layer_name "")
    (princ (strcat "\n[INFO] 已切换到图层: " layer_name))
)

;; 解析楼层高度数据
(defun parse-height-data (data_str / )
    (if (and data_str (/= data_str ""))
        (atof data_str)
        0.0
    )
)

;; 读取choose.txt文件获取行数
(defun read-choose-file (choose_file / fp line_num)
    (setq fp (open choose_file "r"))
    (if fp
        (progn
            (setq line_num (read-line fp))
            (close fp)
            (if line_num (atoi line_num) nil)
        )
        nil
    )
)

;; 绘制楼层高度闭合多边形
(defun draw-floor-height-polygon (height_value ypt1 ypt2 lsp_dir / a p1 p2 p3 p4)
    (if (and height_value (> height_value 0))
        (progn
            ;; 设置图层为"墙体"
            (create-or-set-layer "墙体")
            
            (setq a height_value)
            (princ (strcat "\n[DEBUG] 楼层高度 a=" (rtos a)))
            
            ;; 计算四个点
            ;; p1: ypt1的y轴+650
            (setq p1 (list (car ypt1) (+ (cadr ypt1) 650) (caddr ypt1)))
            ;; p2: ypt2的y轴+650
            (setq p2 (list (car ypt2) (+ (cadr ypt2) 650) (caddr ypt2)))
            ;; p3: p1的y轴+a
            (setq p3 (list (car p1) (+ (cadr p1) a) (caddr p1)))
            ;; p4: p2的y轴+a
            (setq p4 (list (car p2) (+ (cadr p2) a) (caddr p2)))
            
            (princ (strcat "\n[DEBUG] p1=" (point-to-string p1)))
            (princ (strcat "\n[DEBUG] p2=" (point-to-string p2)))
            (princ (strcat "\n[DEBUG] p3=" (point-to-string p3)))
            (princ (strcat "\n[DEBUG] p4=" (point-to-string p4)))
            
            ;; 在"墙体"图层上绘制p1到p2的连线
            (command "_.LINE" p1 p2 "")
            (princ "\n[DEBUG] 已在墙体图层绘制p1到p2连线")
            
            ;; 在"墙体"图层上绘制闭合多边形 p1-p2-p4-p3-p1
            (command "_.PLINE" p1 p2 p4 p3 "C")
            (princ "\n[DEBUG] 已在墙体图层绘制闭合多边形 p1-p2-p4-p3-p1")
            
            ;; 生成wall3.txt文件（在计算完四个点后调用）
            (create-wall3-file lsp_dir p1 p2 p3 p4)
        )
        (princ "\n[INFO] 楼层高度数据无效，跳过绘制")
    )
)

;; 读取CSV文件
(defun read-csv-file (filename / file-handle line-data all-data headers first-line)
    (setq all-data '())
    (setq file-handle (open filename "r"))
    (if file-handle
        (progn
            ;; 读取第一行作为表头
            (setq first-line (read-line file-handle))
            (if first-line
                (progn
                    ;; 移除BOM字符（UTF-8编码）
                    (if (and (>= (strlen first-line) 3)
                             (= (ascii (substr first-line 1 1)) 239)
                             (= (ascii (substr first-line 2 1)) 187)
                             (= (ascii (substr first-line 3 1)) 191))
                        (setq first-line (substr first-line 4))
                    )
                    (setq headers (parse-csv-line first-line))
                    (princ "\n[DEBUG] CSV表头:")
                    (foreach h headers
                        (princ (strcat "\n  " h))
                    )
                    
                    ;; 读取数据行
                    (while (setq line-data (read-line file-handle))
                        (if (and line-data (> (strlen line-data) 0))
                            (progn
                                (setq parsed-line (parse-csv-line line-data))
                                (if parsed-line
                                    (setq all-data (append all-data (list (combine-header-data headers parsed-line))))
                                )
                            )
                        )
                    )
                )
            )
            (close file-handle)
            all-data
        )
        nil
    )
)

;; 解析CSV行（完全重写）
(defun parse-csv-line (line / result current-field i char)
    ;; 检查输入类型
    (if (not (= (type line) 'STR))
        '() ;; 返回空列表
        (progn
            (setq result '())
            (setq current-field "")
            (setq i 1) ;; AutoLISP 中 substr 的索引从1开始
            
            (while (<= i (strlen line))
                (setq char (substr line i 1))
                (if (= char ",")
                    (progn
                        (setq result (append result (list current-field)))
                        (setq current-field "")
                    )
                    (setq current-field (strcat current-field char))
                )
                (setq i (+ i 1))
            )
            
            ;; 添加最后一个字段
            (setq result (append result (list current-field)))
            result
        )
    )
)

;; 组合表头和数据
(defun combine-header-data (headers data / result i)
    (setq result '())
    (setq i 0)
    (while (< i (length headers))
        (setq result (append result (list (cons (nth i headers) (nth i data)))))
        (setq i (+ i 1))
    )
    result
)

;; 从关联列表获取值
(defun get-value (key data / pair)
    (setq pair (assoc key data))
    (if pair (cdr pair) "")
)

;; 读取wall2.txt文件
(defun read-wall2-file (filename / fp line parsed_data)
    (setq parsed_data '())
    (setq fp (open filename "r"))
    (if fp
        (progn
            (while (setq line (read-line fp))
                (if (and line (/= (strlen line) 0) (/= (substr line 1 1) ";"))
                    (setq parsed_data (cons (parse-wall2-line line) parsed_data))
                )
            )
            (close fp)
            (reverse parsed_data)
        )
        nil
    )
)

;; 解析wall2.txt行
(defun parse-wall2-line (line / trimmed point_name coords_str comma1 comma2 x y z space_pos)
    (setq trimmed (vl-string-trim " \t" line))
    (if (and (> (strlen trimmed) 2) 
             (= (substr trimmed 1 1) "(") 
             (= (substr trimmed (strlen trimmed) 1) ")"))
        (progn
            (setq space_pos (vl-string-search " " trimmed))
            (if space_pos
                (progn
                    (setq point_name (substr trimmed 2 (- space_pos 1)))
                    (setq coords_str (substr trimmed (+ space_pos 2) (- (strlen trimmed) space_pos 2)))
                    (if (= (substr coords_str (strlen coords_str) 1) ")")
                        (setq coords_str (substr coords_str 1 (- (strlen coords_str) 1)))
                    )
                    (setq comma1 (vl-string-search "," coords_str))
                    (setq comma2 (vl-string-search "," coords_str (+ comma1 1)))
                    (if (and comma1 comma2)
                        (progn
                            (setq x (atof (substr coords_str 1 comma1)))
                            (setq y (atof (substr coords_str (+ comma1 2) (- comma2 comma1 1))))
                            (setq z (atof (substr coords_str (+ comma2 2))))
                            (list point_name x y z)
                        )
                        nil
                    )
                )
                nil
            )
        )
        nil
    )
)

;; 获取指定点的坐标
(defun get-point-coord (point_name data / result)
    (setq result nil)
    (foreach pt data
        (if (and pt (= (car pt) point_name))
            (setq result (list (cadr pt) (caddr pt) (cadddr pt)))
        )
    )
    result
)

;; 点坐标转字符串
(defun point-to-string (pt)
    (if pt
        (strcat "(" (rtos (car pt)) ", " (rtos (cadr pt)) ", " (rtos (caddr pt)) ")")
        "(nil)"
    )
)

;; 生成wall3.txt文件
(defun create-wall3-file (lsp_dir p1 p2 p3 p4 / wall3_file fp)
    (setq wall3_file (strcat lsp_dir "\\wall3.txt"))
    (setq fp (open wall3_file "w"))
    (if fp
        (progn
            (write-line (strcat "(p1 " (rtos (car p1)) "," (rtos (cadr p1)) "," (rtos (caddr p1)) ")") fp)
            (write-line (strcat "(p2 " (rtos (car p2)) "," (rtos (cadr p2)) "," (rtos (caddr p2)) ")") fp)
            (write-line (strcat "(p3 " (rtos (car p3)) "," (rtos (cadr p3)) "," (rtos (caddr p3)) ")") fp)
            (write-line (strcat "(p4 " (rtos (car p4)) "," (rtos (cadr p4)) "," (rtos (caddr p4)) ")") fp)
            (close fp)
            (princ (strcat "
[INFO] 已更新wall3.txt文件: " wall3_file))
        )
        (princ "\n[ERROR] 无法创建wall3.txt文件")
    )
)

;; 主函数
(defun c:HQT6 (/ cmd_echo old_osnap lsp_dir csv_file wall2_file choose_file csv_data selected_row
                 wall2_data ypt1_coord ypt2_coord choice_line
                 floor_height)
                 
    (setq cmd_echo (getvar 'CMDECHO))
    (if cmd_echo (setvar 'CMDECHO 0))
    
    ;; 保存并关闭对象捕捉，确保程序不受干扰
    (setq old_osnap (getvar 'OSMODE))
    (setvar 'OSMODE 0)
    
    (princ "\n=== HQT6 楼层高度绘制系统 ===")
    
    ;; 获取文件路径
    (setq lsp_dir (vl-filename-directory (findfile "HQT6.lsp")))
    (if lsp_dir
        (progn
            (setq csv_file (strcat lsp_dir "\\data.csv"))
            (setq wall2_file (strcat lsp_dir "\\wall2.txt"))
            (setq choose_file (strcat lsp_dir "\\choose.txt"))
        )
        (progn
            (princ "\n错误: 无法确定文件目录")
            (setvar 'OSMODE old_osnap)
            (if cmd_echo (setvar 'CMDECHO cmd_echo))
            (exit)
        )
    )
    
    ;; 读取choose.txt获取行数
    (setq choice_line (read-choose-file choose_file))
    (if (null choice_line)
        (progn
            (princ "\n错误: 无法读取choose.txt文件或行数无效")
            (setvar 'OSMODE old_osnap)
            (if cmd_echo (setvar 'CMDECHO cmd_echo))
            (exit)
        )
    )
    (princ (strcat "\n[INFO] 从choose.txt读取行数: " (itoa choice_line)))
    
    ;; 读取CSV数据
    (setq csv_data (read-csv-file csv_file))
    (if (null csv_data)
        (progn
            (princ "\n错误: 无法读取CSV数据")
            (setvar 'OSMODE old_osnap)
            (if cmd_echo (setvar 'CMDECHO cmd_echo))
            (exit)
        )
    )
    
    (princ "\n[DEBUG] CSV数据行数:")
    (princ (strcat "\n  总行数: " (itoa (length csv_data))))
    (princ (strcat "\n  选择行数: " (itoa choice_line)))
    
    ;; 获取数据列表（choose.txt中的1对应CSV文件的第2行，即第一行数据）
    (if (and (>= choice_line 1) (<= choice_line (length csv_data)))
        (setq selected_row (nth (- choice_line 1) csv_data))
        (progn
            (princ "\n错误: 行数超出范围")
            (setvar 'OSMODE old_osnap)
            (if cmd_echo (setvar 'CMDECHO cmd_echo))
            (exit)
        )
    )
    
    (princ "\n[DEBUG] 选中行数据类型:")
    (if (listp selected_row)
        (princ "\n  数据类型: 列表")
        (princ "\n  数据类型: 非列表")
    )
    
    (princ "\n[DEBUG] 表头列表:")
    (foreach pair selected_row
        (princ (strcat "\n  " (car pair) " = " (cdr pair)))
    )
    
    ;; 读取wall2.txt文件
    (setq wall2_data (read-wall2-file wall2_file))
    (if (null wall2_data)
        (progn
            (princ "\n错误: 无法读取wall2.txt文件")
            (setvar 'OSMODE old_osnap)
            (if cmd_echo (setvar 'CMDECHO cmd_echo))
            (exit)
        )
    )
    
    ;; 获取ypt1和ypt2坐标
    (setq ypt1_coord (get-point-coord "ypt1" wall2_data))
    (setq ypt2_coord (get-point-coord "ypt2" wall2_data))
    
    (if (or (null ypt1_coord) (null ypt2_coord))
        (progn
            (princ "\n错误: 无法找到ypt1或ypt2坐标")
            (setvar 'OSMODE old_osnap)
            (if cmd_echo (setvar 'CMDECHO cmd_echo))
            (exit)
        )
    )
    
    (princ (strcat "\n[DEBUG] ypt1坐标: " (point-to-string ypt1_coord)))
    (princ (strcat "\n[DEBUG] ypt2坐标: " (point-to-string ypt2_coord)))
    
    ;; 获取楼层高度数据（支持乱序表头识别）
    (if (listp selected_row)
        (setq floor_height (get-value-by-name "楼层高度" selected_row))
        (progn
            (princ "\n错误: 选中行数据不是列表格式")
            (setvar 'OSMODE old_osnap)
            (if cmd_echo (setvar 'CMDECHO cmd_echo))
            (exit)
        )
    )
    
    (if (and floor_height (/= floor_height ""))
        (progn
            (princ (strcat "\n[DEBUG] 楼层高度数据: " floor_height))
            
            ;; 解析楼层高度数据
            (setq floor_height (parse-height-data floor_height))
            
            ;; 绘制楼层高度多边形
            (princ "\n--- 绘制楼层高度多边形 ---")
            (draw-floor-height-polygon floor_height ypt1_coord ypt2_coord lsp_dir)
        )
        (princ "\n[INFO] 没有找到楼层高度数据")
    )
    
    ;; 恢复原始设置
    (setvar 'OSMODE old_osnap)
    (if cmd_echo (setvar 'CMDECHO cmd_echo))
    (princ "\n楼层高度绘制完成!")
    
    ;; 自动启动HQT7插座坐标计算系统
    (princ "\n正在启动HQT7插座坐标计算系统...")
    (c:HQT7)
    
    (princ)
)

(princ "\n已加载 HQT6 - 楼层高度绘制系统")
(princ)