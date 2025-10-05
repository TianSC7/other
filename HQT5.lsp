;; HQT5.lsp - 排水绘制系统 (UTF-8编码)
;; 功能: 读取CSV文件和wall2.txt，根据排水位置数据自动绘制排水圆
;; 支持排水1、排水2、排水3，支持离左墙体、离右墙体、离中墙体的定位
;; 从choose.txt读取行数，无需用户选择
;; 所有生成的排水圆将放置在"墙体"图层上
;; 命令: HQT5

;; 创建或切换到指定图层
(defun create-or-set-layer (layer_name / layer_table layer_obj)
    (setq layer_table (tblsearch "LAYER" layer_name))
    (if (null layer_table)
        (progn
            (entmake (list '(0 . "LAYER")
                          '(100 . "AcDbSymbolTableRecord")
                          '(100 . "AcDbLayerTableRecord")
                          (cons 2 layer_name)
                          '(70 . 0)
                          '(62 . 7)
                          '(6 . "Continuous")))
            (princ (strcat "\n[INFO] 已创建图层: " layer_name))
        )
        (princ (strcat "\n[INFO] 图层已存在: " layer_name))
    )
    (setvar "CLAYER" layer_name)
    (princ (strcat "\n[INFO] 已切换到图层: " layer_name))
)

;; 精确匹配表头名称获取数据
(defun get-drain-value-by-name (target_name data / result)
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

;; 解析a+b格式数据，返回第一个数值a或第二个数值b
(defun parse-ab-data (data_str get_a / plus_pos)
    (if (and data_str (/= data_str ""))
        (progn
            (setq plus_pos (vl-string-search "+" data_str))
            (if plus_pos
                (progn
                    (if get_a
                        (atof (substr data_str 1 plus_pos))
                        (atof (substr data_str (+ plus_pos 2)))
                    )
                )
                (atof data_str)
            )
        )
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

;; 绘制排水圆
(defun draw-drainage-circle (drain_name left_data right_data middle_data ypt1 ypt2 / 
                             a c p1 p2 p3 p4 center_pt use_left)
    (setq use_left (and left_data (/= left_data "")))
    
    (if use_left
        (progn
            ;; 使用左墙体数据
            (setq a (parse-ab-data left_data T))
            (setq p1 (list (+ (car ypt1) a) (cadr ypt1) (caddr ypt1)))
            (princ (strcat "\n[DEBUG] " drain_name " 使用左墙体数据 a=" (rtos a)))
            (princ (strcat "\n[DEBUG] p1=" (point-to-string p1)))
            
            ;; 处理中墙体数据
            (if (and middle_data (/= middle_data ""))
                (progn
                    (setq c (parse-ab-data middle_data T))
                    (setq p3 (list (car p1) (- (cadr p1) c) (caddr p1)))
                    (princ (strcat "\n[DEBUG] 中墙体数据 c=" (rtos c)))
                    (princ (strcat "\n[DEBUG] p3=" (point-to-string p3)))
                    
                    ;; 计算交点（p1与p3的中点）
                    (setq center_pt (list 
                        (/ (+ (car p1) (car p3)) 2)
                        (/ (+ (cadr p1) (cadr p3)) 2)
                        (/ (+ (caddr p1) (caddr p3)) 2)
                    ))
                    (princ (strcat "\n[DEBUG] " drain_name " 圆心=" (point-to-string center_pt)))
                    
                    ;; 使用entmake绘制直径50的圆（半径25）
                    (entmake (list '(0 . "CIRCLE")
                                  '(100 . "AcDbEntity")
                                  '(8 . "墙体")
                                  '(100 . "AcDbCircle")
                                  (cons 10 center_pt)
                                  '(40 . 25)))
                    (princ (strcat "\n[DEBUG] " drain_name " 直径50的圆已绘制到墙体图层"))
                )
                (princ (strcat "\n[INFO] " drain_name " 没有中墙体数据，跳过绘制"))
            )
        )
        (if (and right_data (/= right_data ""))
            (progn
                ;; 使用右墙体数据
                (setq a (parse-ab-data right_data T))
                (setq p2 (list (- (car ypt2) a) (cadr ypt2) (caddr ypt2)))
                (princ (strcat "\n[DEBUG] " drain_name " 使用右墙体数据 a=" (rtos a)))
                (princ (strcat "\n[DEBUG] p2=" (point-to-string p2)))
                
                ;; 处理中墙体数据
                (if (and middle_data (/= middle_data ""))
                    (progn
                        (setq c (parse-ab-data middle_data T))
                        (setq p4 (list (car p2) (- (cadr p2) c) (caddr p2)))
                        (princ (strcat "\n[DEBUG] 中墙体数据 c=" (rtos c)))
                        (princ (strcat "\n[DEBUG] p4=" (point-to-string p4)))
                        
                        ;; 计算交点（p2与p4的中点）
                        (setq center_pt (list 
                            (/ (+ (car p2) (car p4)) 2)
                            (/ (+ (cadr p2) (cadr p4)) 2)
                            (/ (+ (caddr p2) (caddr p4)) 2)
                        ))
                        (princ (strcat "\n[DEBUG] " drain_name " 圆心=" (point-to-string center_pt)))
                        
                        ;; 使用entmake绘制直径50的圆（半径25）
                        (entmake (list '(0 . "CIRCLE")
                                      '(100 . "AcDbEntity")
                                      '(8 . "墙体")
                                      '(100 . "AcDbCircle")
                                      (cons 10 center_pt)
                                      '(40 . 25)))
                        (princ (strcat "\n[DEBUG] " drain_name " 直径50的圆已绘制到墙体图层"))
                    )
                    (princ (strcat "\n[INFO] " drain_name " 没有中墙体数据，跳过绘制"))
                )
            )
            (princ (strcat "\n[INFO] " drain_name " 没有左墙体或右墙体数据，跳过绘制"))
        )
    )
)

;; 读取CSV文件
(defun read-csv-file (filename / file-handle line-data all-data headers)
    (setq all-data '())
    (setq file-handle (open filename "r"))
    (if file-handle
        (progn
            (setq headers (parse-csv-line (read-line file-handle)))
            (while (setq line-data (read-line file-handle))
                (if (> (strlen line-data) 0)
                    (setq all-data (append all-data (list (combine-header-data headers (parse-csv-line line-data)))))
                )
            )
            (close file-handle)
            all-data
        )
        nil
    )
)

;; 解析CSV行
(defun parse-csv-line (line / result pos start-pos)
    (setq result '())
    (setq start-pos 0)
    (setq pos 0)
    (while (< pos (strlen line))
        (if (= (substr line (+ pos 1) 1) ",")
            (progn
                (setq result (append result (list (substr line (+ start-pos 1) (- pos start-pos)))))
                (setq start-pos (+ pos 1))
            )
        )
        (setq pos (+ pos 1))
    )
    (setq result (append result (list (substr line (+ start-pos 1)))))
    result
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

;; 主函数
(defun c:HQT5 (/ cmd_echo old_osnap lsp_dir csv_file wall2_file choose_file csv_data selected_row
                 wall2_data ypt1_coord ypt2_coord choice_line
                 drain1_left drain1_right drain1_middle
                 drain2_left drain2_right drain2_middle
                 drain3_left drain3_right drain3_middle)
                 
    (setq cmd_echo (getvar 'CMDECHO))
    (setq old_osnap (getvar 'OSMODE))
    (if cmd_echo (setvar 'CMDECHO 0))
    (setvar 'OSMODE 0)  ; 关闭对象捕捉
    
    ;; 创建或切换到墙体图层
    (create-or-set-layer "墙体")
    
    (princ "\n=== HQT5 排水绘制系统 ===")
    
    ;; 获取文件路径
    (setq lsp_dir (vl-filename-directory (findfile "HQT5.lsp")))
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
    
    ;; 根据行数获取选中的行
    (if (and (> choice_line 0) (<= choice_line (length csv_data)))
        (setq selected_row (nth (- choice_line 1) csv_data))
        (progn
            (princ "\n错误: 行数超出范围")
            (setvar 'OSMODE old_osnap)
            (if cmd_echo (setvar 'CMDECHO cmd_echo))
            (exit)
        )
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
    
    ;; 获取排水数据
    (setq drain1_left (get-drain-value-by-name "排水1离左墙体" selected_row))
    (setq drain1_right (get-drain-value-by-name "排水1离右墙体" selected_row))
    (setq drain1_middle (get-drain-value-by-name "排水1离中墙体" selected_row))
    (setq drain2_left (get-drain-value-by-name "排水2离左墙体" selected_row))
    (setq drain2_right (get-drain-value-by-name "排水2离右墙体" selected_row))
    (setq drain2_middle (get-drain-value-by-name "排水2离中墙体" selected_row))
    (setq drain3_left (get-drain-value-by-name "排水3离左墙体" selected_row))
    (setq drain3_right (get-drain-value-by-name "排水3离右墙体" selected_row))
    (setq drain3_middle (get-drain-value-by-name "排水3离中墙体" selected_row))
    
    (princ (strcat "\n[DEBUG] 排水1数据 - 左:" (if drain1_left drain1_left "nil") " 右:" (if drain1_right drain1_right "nil") " 中:" (if drain1_middle drain1_middle "nil")))
    (princ (strcat "\n[DEBUG] 排水2数据 - 左:" (if drain2_left drain2_left "nil") " 右:" (if drain2_right drain2_right "nil") " 中:" (if drain2_middle drain2_middle "nil")))
    (princ (strcat "\n[DEBUG] 排水3数据 - 左:" (if drain3_left drain3_left "nil") " 右:" (if drain3_right drain3_right "nil") " 中:" (if drain3_middle drain3_middle "nil")))
    
    ;; 绘制排水1
    (if (or (and drain1_left (/= drain1_left "")) (and drain1_right (/= drain1_right "")))
        (progn
            (princ "\n--- 绘制排水1 ---")
            (draw-drainage-circle "排水1" drain1_left drain1_right drain1_middle ypt1_coord ypt2_coord)
        )
        (princ "\n[INFO] 跳过排水1（无数据）")
    )
    
    ;; 绘制排水2
    (if (or (and drain2_left (/= drain2_left "")) (and drain2_right (/= drain2_right "")))
        (progn
            (princ "\n--- 绘制排水2 ---")
            (draw-drainage-circle "排水2" drain2_left drain2_right drain2_middle ypt1_coord ypt2_coord)
        )
        (princ "\n[INFO] 跳过排水2（无数据）")
    )
    
    ;; 绘制排水3
    (if (or (and drain3_left (/= drain3_left "")) (and drain3_right (/= drain3_right "")))
        (progn
            (princ "\n--- 绘制排水3 ---")
            (draw-drainage-circle "排水3" drain3_left drain3_right drain3_middle ypt1_coord ypt2_coord)
        )
        (princ "\n[INFO] 跳过排水3（无数据）")
    )
    
    (setvar 'OSMODE old_osnap)  ; 恢复对象捕捉设置
    (if cmd_echo (setvar 'CMDECHO cmd_echo))
    (princ "\n排水绘制完成!")
    
    ;; 运行结束后启动HQT6
    (princ "\n正在启动HQT6楼层高度绘制系统...")
    (c:HQT6)
    
    (princ)
)

(princ "\n已加载 HQT5 - 排水绘制系统")
(princ)