;; HQT墙体绘制系统 AutoLISP程序 - 完整修正版 (序号从1开始)
;; 作者: Claude AI Assistant (核心逻辑), Tongyi AI Assistant (点位输出功能)
;; 功能: 从data.csv文件读取数据并绘制平面墙体（包含详细调试输出）
;;       并将所有关键点坐标按 (标签 x,y,z) 格式输出到wall.txt文件
;;       所有点均使用从1开始的连续序号标签 (e.g., xpt1, xpt2, x1pt1, x1pt2, ...)
;;       修正了标签计数逻辑，确保每个基标签独立递增

;; 全局变量，用于存储所有墙体的关键点 (格式: (label x y z))
(setq *all-wall-points* nil)
;; 全局计数器，用于跟踪每个基础标签的序号
;; 格式: ((base-label1 . current_counter1) (base-label2 . current_counter2) ...)
;; 例如: (("xpt" . 3) ("x1pt" . 4) ("ypt" . 2) ...)
(setq *label-counters* nil)

(defun c:HQT (/ csv-file csv-data selected-row pt1)
  (princ "\n=== HQT墙体绘制系统 - 完整修正版 (序号从1开始) ===")
  
  ;; 重置全局点列表和计数器
  (setq *all-wall-points* nil)
  (setq *label-counters* nil) ; 重置所有标签计数器

  ;; 获取当前LSP文件所在目录
  (setq csv-file (strcat (vl-filename-directory (findfile "HQT.lsp")) "\\data.csv"))
  (princ (strcat "\n[DEBUG] CSV文件路径: " csv-file))
  
  ;; 检查CSV文件是否存在
  (if (not (findfile csv-file))
    (progn
      (princ (strcat "\n错误: 找不到文件 " csv-file))
      (exit)
    )
  )
  
  ;; 读取CSV数据
  (princ "\n[DEBUG] 开始读取CSV数据...")
  (setq csv-data (read-csv-file csv-file))
  (if (null csv-data)
    (progn
      (princ "\n错误: 无法读取CSV数据")
      (exit)
    )
  )
  (princ (strcat "\n[DEBUG] 成功读取 " (itoa (length csv-data)) " 行数据"))
  
  ;; 显示可用的行并让用户选择
  (setq selected-row (select-csv-row csv-data))
  (if (null selected-row)
    (progn
      (princ "\n取消操作")
      (exit)
    )
  )
  
  ;; 获取起点
  (setq pt1 (getpoint "\n请点击起点: "))
  (if (null pt1)
    (progn
      (princ "\n取消操作")
      (exit)
    )
  )
  ;; 确保起点是3D点
  (if (null (caddr pt1)) (setq pt1 (list (car pt1) (cadr pt1) 0.0)))
  (princ (strcat "\n[DEBUG] 起点坐标: " (point-to-string pt1)))
  
  ;; 调用主绘制函数
  (draw-wall-system pt1 selected-row)
  
  ;; 将所有收集到的点输出到文件
  (write-points-to-file *all-wall-points*)
  
  (princ "\n墙体绘制完成! 所有点位已按 (标签 x,y,z) 格式输出到 wall.txt")
  (princ)
)

;; 读取CSV文件函数
(defun read-csv-file (filename / file-handle line-data all-data headers line-count)
  (setq all-data '())
  (setq line-count 0)
  (setq file-handle (open filename "r"))
  
  (if file-handle
    (progn
      ;; 读取表头
      (setq headers (parse-csv-line (read-line file-handle)))
      (princ (strcat "\n[DEBUG] 表头: " (apply 'strcat (mapcar '(lambda (x) (strcat x " ")) headers))))
      
      ;; 读取数据行
      (while (setq line-data (read-line file-handle))
        (if (> (strlen line-data) 0)
          (progn
            (setq line-count (+ line-count 1))
            (setq all-data (append all-data (list (combine-header-data headers (parse-csv-line line-data)))))
            (princ (strcat "\n[DEBUG] 读取第" (itoa line-count) "行数据"))
          )
        )
      )
      (close file-handle)
      all-data
    )
    nil
  )
)

;; 解析CSV行数据
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
  
  ;; 添加最后一个字段
  (setq result (append result (list (substr line (+ start-pos 1)))))
  result
)

;; 将表头和数据组合成关联列表
(defun combine-header-data (headers data / result i)
  (setq result '())
  (setq i 0)
  
  (while (< i (length headers))
    (setq result (append result (list (cons (nth i headers) (nth i data)))))
    (setq i (+ i 1))
  )
  result
)

;; 从关联列表中获取值
(defun get-value (key data / pair)
  (setq pair (assoc key data))
  (if pair (cdr pair) "")
)

;; 显示CSV行并让用户选择
(defun select-csv-row (csv-data / i choice choose-file choose-file-path)
  (princ "
可用的墙体数据:")
  (setq i 0)
  
  (foreach row csv-data
    (setq i (+ i 1))
    (princ (strcat "
" (itoa i) ". " (get-value "名称" row)))
  )
  
  (setq choice (getint "
请选择要绘制的墙体 (输入序号): "))
  (princ (strcat "
[DEBUG] 用户选择: " (itoa choice)))
  
  ;; 将用户选择的行数保存到 choose.txt
  (setq choose-file-path (strcat (vl-filename-directory (findfile "HQT.lsp")) "\\choose.txt"))
  (setq choose-file (open choose-file-path "w"))
  (if choose-file
    (progn
      (write-line (itoa choice) choose-file)
      (close choose-file)
      (princ (strcat "
[INFO] 用户选择的行数已保存到: " choose-file-path))
    )
    (princ (strcat "
[ERROR] 无法创建/写入 choose.txt 文件: " choose-file-path))
  )
  
  (if (and choice (> choice 0) (<= choice (length csv-data)))
    (nth (- choice 1) csv-data)
    nil
  )
)

;; 主绘制函数
(defun draw-wall-system (start-pt row-data / left-wall middle-wall right-wall x-pts x1-pts y-start y-pts y1-pts z-start z-pts z1-pts temp-pt)
  
  ;; 从选中行获取墙体数据
  (setq left-wall (get-value "左墙体" row-data))
  (setq middle-wall (get-value "中间墙体" row-data))
  (setq right-wall (get-value "右墙体" row-data))
  
  (princ (strcat "\n=== 开始绘制墙体: " (get-value "名称" row-data) " ==="))
  ;; 当x=0时，不显示X相关的调试信息
  (if (/= (atof left-wall) 0)
    (princ (strcat "\n[DEBUG] 左墙体数据: " left-wall))
  )
  (princ (strcat "\n[DEBUG] 中间墙体数据: " middle-wall))
  (princ (strcat "\n[DEBUG] 右墙体数据: " right-wall))
  
  ;; 检查是否为特殊情况：x=0 (左墙体为0)
  (if (= (atof left-wall) 0)
    ;; 特殊情况：x=0，pt1作为y的起点
    (progn
      (princ "\n=== 特殊模式：X=0，直接从Y开始绘制 ===")
      
      ;; 绘制Y轴墙体 (pt1作为起点)
      (princ "\n--- 步骤1: 绘制Y轴墙体 (ypt) ---")
      (princ (strcat "\n[DEBUG] Y轴起点: " (point-to-string start-pt)))
      (setq y-pts (draw-wall-segments-labeled start-pt middle-wall "ypt" 0))
      (princ "\n[DEBUG] Y轴墙体关键点 (ypt):")
      (print-points-labeled y-pts)
      (setq *all-wall-points* (append *all-wall-points* y-pts))

      ;; 创建Y1偏移墙体 (向Y轴+120偏移)
      (princ "\n--- 步骤2: 创建Y1偏移墙体 (y1pt) ---")
      (setq y1-pts (offset-points-labeled y-pts '(0 120 0) "y1pt"))
      (princ "\n[DEBUG] Y1偏移关键点 (y1pt):")
      (print-points-labeled y1-pts)
      (setq *all-wall-points* (append *all-wall-points* y1-pts))

      ;; Y1起点和终点延伸
      (princ "\n--- 步骤3: Y1起点和终点延伸 ---")
      ;; 连接 y1pt1 与 ypt1
      (princ (strcat "\n[DEBUG] 连接Y1起点与Y起点: " (point-to-string (first-y1-coord y1-pts)) " -> " (point-to-string (first-y-coord y-pts))))
      (command "_.LINE" (first-y1-coord y1-pts) (first-y-coord y-pts) "")
      
      
      ;; 当x和z等于0时，取消y1pt-1往x轴延伸120
      (if (and (= (atof left-wall) 0) (= (atof right-wall) 0))
        (progn
          (princ "\n[DEBUG] X=0且Z=0，取消y1pt-1往x轴延伸120")
        )
        (progn
          ;; 正常情况：y1pt-1往x轴+120延申
          (setq temp-pt (polar (last-y1-coord y1-pts) 0 120))
          (princ (strcat "\n[DEBUG] y1终点延伸: " (point-to-string (last-y1-coord y1-pts)) " -> " (point-to-string temp-pt)))
          (command "_.LINE" (last-y1-coord y1-pts) temp-pt "")
          (setq *all-wall-points* (append *all-wall-points* (list (get-next-labeled-point temp-pt "y1pt"))))
        )
      )
      

      ;; 当x和z等于0时，取消步骤4以后的东西
      (if (and (= (atof left-wall) 0) (= (atof right-wall) 0))
        (progn
          (princ "\n[DEBUG] X=0且Z=0，取消Z轴相关绘制")
          ;; 单独增加：链接ypt-1#y1pt-1
          (princ (strcat "\n[DEBUG] 链接ypt-1#y1pt-1: " (point-to-string (last-y-coord y-pts)) " -> " (point-to-string (last-y1-coord y1-pts))))
          (command "_.LINE" (last-y-coord y-pts) (last-y1-coord y1-pts) "")
        )
        (progn
          ;; 绘制Z轴墙体 (Y轴终点作为Z轴起点)
          (princ "\n--- 步骤4: 绘制Z轴墙体 (zpt) ---")
          (setq z-start (last-y-coord y-pts))
          (princ (strcat "\n[DEBUG] Z轴起点: " (point-to-string z-start)))
          (setq z-pts (draw-wall-segments-labeled z-start right-wall "zpt" (* 3 (/ pi 2))))
          (princ "\n[DEBUG] Z轴墙体关键点 (zpt):")
          (print-points-labeled z-pts)
          (setq *all-wall-points* (append *all-wall-points* z-pts))

          ;; 创建Z1偏移墙体 (向X轴+120偏移)
          (princ "\n--- 步骤5: 创建Z1偏移墙体 (z1pt) ---")
          (setq z1-pts (offset-points-labeled z-pts '(120 0 0) "z1pt"))
          (princ "\n[DEBUG] Z1偏移关键点 (z1pt):")
          (print-points-labeled z1-pts)
          (setq *all-wall-points* (append *all-wall-points* z1-pts))

          ;; z1pt1往y轴+120进行延伸
          (princ "\n--- 步骤6: z1pt1往y轴+120进行延伸 ---")
          (setq temp-pt (polar (first-z1-coord z1-pts) (/ pi 2) 120))
          (princ (strcat "\n[DEBUG] z1pt1延伸: " (point-to-string (first-z1-coord z1-pts)) " -> " (point-to-string temp-pt)))
          (command "_.LINE" (first-z1-coord z1-pts) temp-pt "")
          (setq *all-wall-points* (append *all-wall-points* (list (get-next-labeled-point temp-pt "z1pt"))))

          ;; 连接关键线段
          (princ "\n--- 步骤7: 连接关键线段 ---")
          ;; 只连接Z终点与Z1终点，取消Y终点与Y1终点的连接
          (princ (strcat "\n[DEBUG] 连接Z终点与Z1终点: " (point-to-string (last-z-coord z-pts)) " -> " (point-to-string (last-z1-coord z1-pts))))
          (command "_.LINE" (last-z-coord z-pts) (last-z1-coord z1-pts) "")
        )
      )
    )
    ;; 原有逻辑：正常的X-Y-Z绘制流程
    (progn
      (princ "\n--- 步骤1: 绘制左墙体 (xpt) ---")
      (princ (strcat "\n[DEBUG] 起点: " (point-to-string start-pt)))
      ;; 使用带标签的绘制函数
      (setq x-pts (draw-wall-segments-labeled start-pt left-wall "xpt" (/ pi 2)))
      (princ "\n[DEBUG] 左墙体关键点 (xpt):")
      (print-points-labeled x-pts)
      ;; 收集点位
      (setq *all-wall-points* (append *all-wall-points* x-pts))

      ;; 创建偏移墙体x1 (向X轴-120偏移)
      (princ "\n--- 步骤2: 创建左墙体偏移 (x1pt) ---")
      ;; 使用带标签的偏移函数
      (setq x1-pts (offset-points-labeled x-pts '(-120 0 0) "x1pt"))
      (princ "\n[DEBUG] 偏移后的x1关键点 (x1pt):")
      (print-points-labeled x1-pts)
      ;; 收集点位
      (setq *all-wall-points* (append *all-wall-points* x1-pts))

      ;; 将x1的最后一点向Y轴延伸+120
      (princ "\n--- 步骤3: x1终点向Y轴延伸+120 ---")
      (setq temp-pt (polar (last-x1-coord x1-pts) (/ pi 2) 120))
      (princ (strcat "\n[DEBUG] x1终点: " (point-to-string (last-x1-coord x1-pts))))
      (princ (strcat "\n[DEBUG] x1延伸终点: " (point-to-string temp-pt)))
      (command "_.LINE" (last-x1-coord x1-pts) temp-pt "")
      ;; 收集延伸终点 (使用连续序号)
      (setq *all-wall-points* (append *all-wall-points* (list (get-next-labeled-point temp-pt "x1pt"))))

      ;; 连接x起点与x1起点
      (princ "\n--- 步骤4: 连接x起点与x1起点 ---")
      (princ (strcat "\n[DEBUG] 连接: " (point-to-string (first-x-coord x-pts)) " -> " (point-to-string (first-x1-coord x1-pts))))
      (command "_.LINE" (first-x-coord x-pts) (first-x1-coord x1-pts) "")
      ;; 起点已在x-pts和x1-pts中，无需重复添加

      ;; 绘制中间墙体(Y方向 - X轴增加)
      (princ "\n--- 步骤5: 绘制中间墙体 (ypt) ---")
      (setq y-start (last-x-coord x-pts))
      (princ (strcat "\n[DEBUG] 中间墙体起点: " (point-to-string y-start)))
      ;; 使用带标签的绘制函数
      (setq y-pts (draw-wall-segments-labeled y-start middle-wall "ypt" 0))
      (princ "\n[DEBUG] 中间墙体关键点 (ypt):")
      (print-points-labeled y-pts)
      ;; 收集点位
      (setq *all-wall-points* (append *all-wall-points* y-pts))

      ;; 创建偏移墙体y1 (向Y轴+120偏移)
      (princ "\n--- 步骤6: 创建中间墙体偏移 (y1pt) ---")
      ;; 使用带标签的偏移函数
      (setq y1-pts (offset-points-labeled y-pts '(0 120 0) "y1pt"))
      (princ "\n[DEBUG] 偏移后的y1关键点 (y1pt):")
      (print-points-labeled y1-pts)
      ;; 收集点位
      (setq *all-wall-points* (append *all-wall-points* y1-pts))

      ;; y1起点向X轴-120偏移，终点向X轴+120延伸
      (princ "\n--- 步骤7: y1起点和终点延伸 ---")
      ;; 当x和z等于0时，y的x轴取消+120
      (if (and (= (atof left-wall) 0) (= (atof right-wall) 0))
        (progn
          (princ "\n[DEBUG] X=0且Z=0，取消Y轴X方向+120延伸")
          ;; 只保留y1起点向X轴-120偏移
          (setq temp-pt (polar (first-y1-coord y1-pts) pi 120))
          (princ (strcat "\n[DEBUG] y1起点延伸: " (point-to-string (first-y1-coord y1-pts)) " -> " (point-to-string temp-pt)))
          (command "_.LINE" (first-y1-coord y1-pts) temp-pt "")
          ;; 收集延伸点 (使用连续序号)
          (setq *all-wall-points* (append *all-wall-points* (list (get-next-labeled-point temp-pt "y1pt"))))
        )
        (progn
          ;; 正常情况：y1起点向X轴-120偏移，终点向X轴+120延伸
          (setq temp-pt (polar (first-y1-coord y1-pts) pi 120))
          (princ (strcat "\n[DEBUG] y1起点延伸: " (point-to-string (first-y1-coord y1-pts)) " -> " (point-to-string temp-pt)))
          (command "_.LINE" (first-y1-coord y1-pts) temp-pt "")
          ;; 收集延伸点 (使用连续序号)
          (setq *all-wall-points* (append *all-wall-points* (list (get-next-labeled-point temp-pt "y1pt"))))
          
          (setq temp-pt (polar (last-y1-coord y1-pts) 0 120))
          (princ (strcat "\n[DEBUG] y1终点延伸: " (point-to-string (last-y1-coord y1-pts)) " -> " (point-to-string temp-pt)))
          (command "_.LINE" (last-y1-coord y1-pts) temp-pt "")
          ;; 收集延伸点 (使用连续序号)
          (setq *all-wall-points* (append *all-wall-points* (list (get-next-labeled-point temp-pt "y1pt"))))
        )
      )

      ;; 绘制右墙体(Z方向 - Y轴减少)
      (princ "\n--- 步骤8: 绘制右墙体 (zpt) ---")
      (setq z-start (last-y-coord y-pts))
      (princ (strcat "\n[DEBUG] 右墙体起点: " (point-to-string z-start)))
      ;; 使用带标签的绘制函数
      (setq z-pts (draw-wall-segments-labeled z-start right-wall "zpt" (* 3 (/ pi 2))))
      (princ "\n[DEBUG] 右墙体关键点 (zpt):")
      (print-points-labeled z-pts)
      ;; 收集点位
      (setq *all-wall-points* (append *all-wall-points* z-pts))

      ;; 创建偏移墙体z1 (向X轴+120偏移)
      (princ "\n--- 步骤9: 创建右墙体偏移 (z1pt) ---")
      ;; 使用带标签的偏移函数
      (setq z1-pts (offset-points-labeled z-pts '(120 0 0) "z1pt"))
      (princ "\n[DEBUG] 偏移后的z1关键点 (z1pt):")
      (print-points-labeled z1-pts)
      ;; 收集点位
      (setq *all-wall-points* (append *all-wall-points* z1-pts))

      ;; z1起点向Y轴+120延伸
      (princ "\n--- 步骤10: z1起点向Y轴延伸+120 ---")
      (setq temp-pt (polar (first-z1-coord z1-pts) (/ pi 2) 120))
      (princ (strcat "\n[DEBUG] z1起点延伸: " (point-to-string (first-z1-coord z1-pts)) " -> " (point-to-string temp-pt)))
      (command "_.LINE" (first-z1-coord z1-pts) temp-pt "")
      ;; 收集延伸终点 (使用连续序号)
      (setq *all-wall-points* (append *all-wall-points* (list (get-next-labeled-point temp-pt "z1pt"))))

      ;; 连接z终点与z1终点
      (princ "\n--- 步骤11: 连接z终点与z1终点 ---")
      (princ (strcat "\n[DEBUG] 连接: " (point-to-string (last-z-coord z-pts)) " -> " (point-to-string (last-z1-coord z1-pts))))
      (command "_.LINE" (last-z-coord z-pts) (last-z1-coord z1-pts) "")
      ;; 终点已在z-pts和z1-pts中，无需重复添加
    )
  )
)


;; --- 新增/修改的函数用于处理带标签的点 ---

;; 绘制墙体段函数 (支持分段和标签)
(defun draw-wall-segments-labeled (start-pt wall-data-str base-label angle / segments current-pt points segment-length next-pt i pt-label)
  (setq segments (parse-segments wall-data-str))
  (setq current-pt start-pt)
  (setq points '())

  ;; 获取该标签的当前计数器并递增
  (setq i (get-label-counter base-label))
  (setq *label-counters* (set-label-counter base-label (+ i 1)))

  (princ (strcat "\n[DEBUG] 分段数据: " wall-data-str))
  
  ;; 第一个点
  (setq pt-label (strcat base-label (itoa i)))
  (setq points (append points (list (list pt-label (car start-pt) (cadr start-pt) (caddr start-pt)))))
  (princ (strcat "\n[DEBUG] 生成点: " pt-label " = " (point-to-string start-pt)))
  (setq i (+ i 1))
  (setq *label-counters* (set-label-counter base-label i))

  (foreach segment segments
    (setq segment-length (atof segment))
    (if (> segment-length 0) ; 只绘制长度大于0的段
     (progn
       (setq next-pt (polar current-pt angle segment-length))
       ;; 确保是3D点
       (if (null (caddr next-pt)) (setq next-pt (list (car next-pt) (cadr next-pt) 0.0)))
       (princ (strcat "\n[DEBUG] 绘制段: 长度=" (rtos segment-length) " 从 " (point-to-string current-pt) " 到 " (point-to-string next-pt)))
       (command "_.LINE" current-pt next-pt "")
       ;; 为下一个点生成带序号的标签 (xpt2, xpt3, ...)
       (setq pt-label (strcat base-label (itoa i)))
       (setq points (append points (list (list pt-label (car next-pt) (cadr next-pt) (caddr next-pt)))))
       (princ (strcat "\n[DEBUG] 生成点: " pt-label " = " (point-to-string next-pt)))
       (setq current-pt next-pt)
       (setq i (+ i 1))
       ;; 更新计数器
       (setq *label-counters* (set-label-counter base-label i))
     )
    )
  )
  points ; 返回包含 (label x y z) 的点列表
)

;; 偏移点集合并绘制线段 (支持标签)
(defun offset-points-labeled (points-data offset-vector base-label / result offset-pt prev-offset-pt i pt-data pt pt-label)
  (setq result '())
  (setq prev-offset-pt nil)
  
  ;; 获取该标签的当前计数器并递增
  (setq i (get-label-counter base-label))
  (setq *label-counters* (set-label-counter base-label (+ i 1)))

  (foreach pt-data points-data
    ;; pt-data 是 (label x y z) 的列表
    (setq pt (list (cadr pt-data) (caddr pt-data) (cadddr pt-data))) ; 提取坐标 (x y z)
    (setq offset-pt (mapcar '+ pt offset-vector))
    ;; 确保是3D点
    (if (null (caddr offset-pt)) (setq offset-pt (list (car offset-pt) (cadr offset-pt) 0.0)))
    
    ;; 为偏移点生成带序号的标签 (x1pt1, x1pt2, ...)
    (setq pt-label (strcat base-label (itoa i)))
    (setq result (append result (list (list pt-label (car offset-pt) (cadr offset-pt) (caddr offset-pt)))))
    (princ (strcat "\n[DEBUG] 偏移点: " (point-to-string pt) " -> " (point-to-string offset-pt) " -> " pt-label))
    
    ;; 如果有前一个偏移点，则绘制线段
    (if prev-offset-pt
      (progn
        (princ (strcat "\n[DEBUG] 绘制偏移线段: " (point-to-string prev-offset-pt) " -> " (point-to-string offset-pt)))
        (command "_.LINE" prev-offset-pt offset-pt "")
      )
    )
    (setq prev-offset-pt offset-pt)
    (setq i (+ i 1))
    ;; 更新计数器
    (setq *label-counters* (set-label-counter base-label i))
  )
  result ; 返回包含 (label x y z) 的偏移点列表
)

;; 辅助函数：获取下一个带序号标签的点 (用于延伸点)
(defun get-next-labeled-point (pt base-label / counter new-label)
  ;; 获取该标签的当前计数器
  (setq counter (get-label-counter base-label))
  ;; 生成标签
  (setq new-label (strcat base-label (itoa counter)))
  ;; 递增并更新计数器
  (setq *label-counters* (set-label-counter base-label (+ counter 1)))
  (princ (strcat "\n[DEBUG] 生成延伸点: " new-label " = " (point-to-string pt)))
  (list new-label (car pt) (cadr pt) (caddr pt))
)

;; --- 辅助函数：管理全局计数器 ---
;; 获取标签的当前计数 (如果不存在则返回1)
(defun get-label-counter (label / pair)
  (setq pair (assoc label *label-counters*))
  (if pair (cdr pair) 1)
)

;; 设置标签的计数 (返回更新后的 *label-counters* 列表)
(defun set-label-counter (label value / pair)
  (setq pair (assoc label *label-counters*))
  (if pair
    (subst (cons label value) pair *label-counters*) ; 更新现有项
    (append *label-counters* (list (cons label value))) ; 添加新项
  )
)

;; --- 辅助函数：从带标签的点列表中提取第一个点的坐标 ---
(defun first-x-coord (pts) (list (cadr (car pts)) (caddr (car pts)) (cadddr (car pts))))
(defun first-x1-coord (pts) (list (cadr (car pts)) (caddr (car pts)) (cadddr (car pts))))
(defun first-y-coord (pts) (list (cadr (car pts)) (caddr (car pts)) (cadddr (car pts))))
(defun first-y1-coord (pts) (list (cadr (car pts)) (caddr (car pts)) (cadddr (car pts))))
(defun first-z1-coord (pts) (list (cadr (car pts)) (caddr (car pts)) (cadddr (car pts))))

;; --- 辅助函数：从带标签的点列表中提取最后一个点的坐标 ---
(defun last-x-coord (pts) (list (cadr (last pts)) (caddr (last pts)) (cadddr (last pts))))
(defun last-x1-coord (pts) (list (cadr (last pts)) (caddr (last pts)) (cadddr (last pts))))
(defun last-y-coord (pts) (list (cadr (last pts)) (caddr (last pts)) (cadddr (last pts))))
(defun last-y1-coord (pts) (list (cadr (last pts)) (caddr (last pts)) (cadddr (last pts))))
(defun last-z-coord (pts) (list (cadr (last pts)) (caddr (last pts)) (cadddr (last pts))))
(defun last-z1-coord (pts) (list (cadr (last pts)) (caddr (last pts)) (cadddr (last pts))))

;; --- 保持不变的原有函数 ---

;; 解析带+号的段数据
(defun parse-segments (data-str / segments pos start-pos)
  (setq segments '())
  (setq start-pos 0)
  (setq pos 0)
  
  (while (< pos (strlen data-str))
    (if (= (substr data-str (+ pos 1) 1) "+")
      (progn
        (setq segments (append segments (list (substr data-str (+ start-pos 1) (- pos start-pos)))))
        (setq start-pos (+ pos 1))
      )
    )
    (setq pos (+ pos 1))
  )
  
  ;; 添加最后一个段
  (setq segments (append segments (list (substr data-str (+ start-pos 1)))))
  segments
)

;; 辅助函数：点坐标转字符串 (处理3D点)
(defun point-to-string (pt)
  (if pt
    (strcat "(" (rtos (car pt)) ", " (rtos (cadr pt)) ", " (rtos (caddr pt)) ")")
    "(nil)"
  )
)

;; 辅助函数：打印点集合 (适应 (label x y z) 格式)
(defun print-points-labeled (pts-data / i pt-data)
  (setq i 0)
  (foreach pt-data pts-data
    ;; pt-data 是 (label x y z) 的列表
    (setq i (+ i 1))
    (princ (strcat "\n[DEBUG]   点" (itoa i) " (" (car pt-data) "): " (point-to-string (cdr pt-data))))
  )
)

;; --- 新增的函数：写入点位到文件 ---
;; 写入点位到文件的函数 (按 (标签 x,y,z) 格式)
(defun write-points-to-file (points-list / file-path file-handle pt-data label x y z)
  (if points-list
    (progn
      ;; 构造文件路径 (与LSP文件同目录)
      (setq file-path (strcat (vl-filename-directory (findfile "HQT.lsp")) "\\wall.txt"))
      (princ (strcat "\n[INFO] 正在写入点位到文件: " file-path))

      ;; 以写入模式打开文件，如果不存在则创建，存在则覆盖
      (setq file-handle (open file-path "w"))
      (if file-handle
        (progn
          ;; 遍历点列表并写入 (格式: (label x,y,z))
          (foreach pt-data points-list
            (setq label (car pt-data))
            (setq x (cadr pt-data))
            (setq y (caddr pt-data))
            (setq z (cadddr pt-data))
            ;; 使用2号格式(小数点),精度6位
            (write-line (strcat "(" label " " (rtos x 2 6) "," (rtos y 2 6) "," (rtos z 2 6) ")") file-handle)
          )
          (close file-handle)
          (princ "\n[INFO] 点位写入完成。")
          
          ;; 始终调用HQT3，无论X是否为0
          (c:HQT3)
        )
        (princ (strcat "\n[ERROR] 无法打开文件进行写入: " file-path))
      )
    )
    (princ "\n[WARN] 没有点位数据需要写入。")
  )
)
(princ "\n已加载HQT墙体绘制系统 - 完整修正版 (序号从1开始)")
(princ "\n输入 HQT 开始绘制")
(princ)
