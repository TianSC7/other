;; HQT7.lsp - 插座坐标计算系统 (完全重写版)
;; 功能：从CSV数据和wall3.txt基准点计算插座坐标，保存到wall4.txt
;; 只处理插座1到插座6，不插入图块，仅计算坐标

;; 安全数字转换函数
(defun safe-atof (str)
  (if (and str (not (= str "")) (or (wcmatch str "*[0-9]*") (wcmatch str "*.*")))
    (atof str)
    0.0
  )
)

(defun safe-atoi (str)
  (if (and str (not (= str "")) (wcmatch str "*[0-9]*"))
    (atoi str)
    0
  )
)

;; 检查字符串是否为有效数字
(defun is-valid-number (str)
  (and str 
       (not (= str ""))
       (or (wcmatch str "*[0-9]*")
           (wcmatch str "*.*"))
  )
)

;; 获取LSP文件所在目录
(defun get-lsp-directory ()
  (setq script-path (vl-filename-directory (findfile "HQT7.lsp")))
  (if script-path
    script-path
    "c:\\Users\\TIAN\\Documents\\2025\\September\\lsp"
  )
)

;; 解析CSV行
(defun parse-csv-line (line)
  (setq result '())
  (setq current "")
  (setq i 0)
  (while (< i (strlen line))
    (setq char (substr line (+ i 1) 1))
    (if (= char ",")
      (progn
        (setq result (append result (list current)))
        (setq current "")
      )
      (setq current (strcat current char))
    )
    (setq i (+ i 1))
  )
  (setq result (append result (list current)))
  result
)

;; 读取CSV文件并解析
(defun read-csv-data (csv-file)
  (setq file (open csv-file "r"))
  (if file
    (progn
      ;; 读取表头
      (setq headers-line (read-line file))
      (princ (strcat "\n[DEBUG] 原始表头行长度: " (if headers-line (itoa (strlen headers-line)) "nil")))
      
      ;; 移除BOM字符 (UTF-8 BOM: 239 187 191)
      (if (and headers-line (>= (strlen headers-line) 3))
        (progn
          (setq first-char (substr headers-line 1 1))
          (setq first-three (substr headers-line 1 3))
          (if (= first-three "﻿")
            (progn
              (setq headers-line (substr headers-line 4))
              (princ "\n[DEBUG] 检测到并移除了BOM字符")
            )
            (princ "\n[DEBUG] 未检测到BOM字符")
          )
        )
      )
      
      (princ (strcat "\n[DEBUG] 处理后表头行长度: " (if headers-line (itoa (strlen headers-line)) "nil")))
      
      (if (not headers-line)
        (progn
          (close file)
          (princ "\n[ERROR] CSV文件为空或表头行为空")
          nil
        )
        (progn
          (setq headers (parse-csv-line headers-line))
          (princ (strcat "\n[DEBUG] 解析到表头列数: " (itoa (length headers))))
          
          ;; 读取数据行
          (setq data-lines '())
          (setq line-count 0)
          (while (setq line (read-line file))
            (if (> (strlen line) 0)
              (progn
                (setq line-count (+ line-count 1))
                (setq parsed-line (parse-csv-line line))
                (setq data-lines (append data-lines (list parsed-line)))
                (if (<= line-count 3) ;; 只显示前3行的调试信息
                  (princ (strcat "\n[DEBUG] 第" (itoa line-count) "行数据列数: " (itoa (length parsed-line))))
                )
              )
            )
          )
          (close file)
          (princ (strcat "\n[DEBUG] 总共读取 " (itoa line-count) " 行数据"))
          (list headers data-lines)
        )
      )
    )
    (progn
      (princ "\n[ERROR] 无法打开CSV文件")
      nil
    )
  )
)

;; 解析坐标字符串 "x,y,z"
(defun parse-coords (coord-str)
  (setq coord-list '())
  (setq parts (parse-csv-line coord-str))
  (if (>= (length parts) 3)
    (list (atof (nth 0 parts)) (atof (nth 1 parts)) (atof (nth 2 parts)))
    nil
  )
)

;; 读取wall3.txt获取基准点
(defun read-wall3-data ()
  (setq lsp-dir (get-lsp-directory))
  (setq wall3-file (strcat lsp-dir "\\wall3.txt"))
  (princ (strcat "\n[DEBUG] 尝试读取wall3.txt: " wall3-file))
  
  ;; 尝试多个可能的路径
  (if (not (findfile wall3-file))
    (progn
      (setq wall3-file "c:\\Users\\TIAN\\Documents\\2025\\September\\lsp\\wall3.txt")
      (princ (strcat "\n[DEBUG] 备用路径1: " wall3-file))
      (if (not (findfile wall3-file))
        (progn
          (setq wall3-file "c:\\Users\\TIAN\\Documents\\2025\\September\\OK\\wall3.txt")
          (princ (strcat "\n[DEBUG] 备用路径2: " wall3-file))
        )
      )
    )
  )
  
  (if (findfile wall3-file)
    (progn
      (princ (strcat "\n[DEBUG] 找到wall3.txt文件: " wall3-file))
      (setq file (open wall3-file "r"))
      (setq p1 nil p2 nil p3 nil p4 nil)
      (while (setq line (read-line file))
        (if (and line (> (strlen line) 0))
          (cond
            ((vl-string-search "p1" line)
             (setq coords (vl-string-subst "" "(p1 " line))
             (setq coords (vl-string-subst "" ")" coords))
             (setq p1 (parse-coords coords))
             (princ (strcat "\n[DEBUG] 解析p1: " (if p1 (vl-princ-to-string p1) "nil")))
            )
            ((vl-string-search "p2" line)
             (setq coords (vl-string-subst "" "(p2 " line))
             (setq coords (vl-string-subst "" ")" coords))
             (setq p2 (parse-coords coords))
             (princ (strcat "\n[DEBUG] 解析p2: " (if p2 (vl-princ-to-string p2) "nil")))
            )
            ((vl-string-search "p3" line)
             (setq coords (vl-string-subst "" "(p3 " line))
             (setq coords (vl-string-subst "" ")" coords))
             (setq p3 (parse-coords coords))
            )
            ((vl-string-search "p4" line)
             (setq coords (vl-string-subst "" "(p4 " line))
             (setq coords (vl-string-subst "" ")" coords))
             (setq p4 (parse-coords coords))
            )
          )
        )
      )
      (close file)
      (list p1 p2 p3 p4)
    )
    (progn
      (princ "\n错误：在所有路径中都找不到wall3.txt文件")
      nil
    )
  )
)

;; 读取choose.txt获取选择的行号
(defun read-choose-data ()
  (setq lsp-dir (get-lsp-directory))
  (setq choose-file (strcat lsp-dir "\\choose.txt"))
  (princ (strcat "\n[DEBUG] 尝试读取choose.txt: " choose-file))
  
  ;; 尝试多个可能的路径
  (if (not (findfile choose-file))
    (progn
      (setq choose-file "c:\\Users\\TIAN\\Documents\\2025\\September\\lsp\\choose.txt")
      (princ (strcat "\n[DEBUG] 备用路径1: " choose-file))
      (if (not (findfile choose-file))
        (progn
          (setq choose-file "c:\\Users\\TIAN\\Documents\\2025\\September\\OK\\choose.txt")
          (princ (strcat "\n[DEBUG] 备用路径2: " choose-file))
        )
      )
    )
  )
  
  (if (findfile choose-file)
    (progn
      (princ (strcat "\n[DEBUG] 找到choose.txt文件: " choose-file))
      (setq file (open choose-file "r"))
      (setq row-num (read-line file))
      (close file)
      (princ (strcat "\n[DEBUG] 读取的行号: " row-num))
      (atoi row-num)
    )
    (progn
      (princ "\n[WARNING] 在所有路径中都找不到choose.txt文件")
      (princ "\n[INFO] 使用默认行号: 1")
      1  ;; 默认使用第1行
    )
  )
)

;; 计算插座坐标 - 核心功能
(defun calculate-socket-coords (p1 p2 socket-data)
  (setq result-coords '())
  (setq conflict-count 0)
  
  ;; 处理插座1到插座6
  (setq socket-num 1)
  (while (<= socket-num 6)
    (setq left-wall-col (strcat "插座" (itoa socket-num) "离左墙体"))
    (setq right-wall-col (strcat "插座" (itoa socket-num) "离右墙体"))
    (setq height-col (strcat "插座" (itoa socket-num) "离地高度"))
    (setq count-col (strcat "插座" (itoa socket-num) "个数"))
    
    ;; 获取数据值
    (setq left-val (cdr (assoc left-wall-col socket-data)))
    (setq right-val (cdr (assoc right-wall-col socket-data)))
    (setq height-val (cdr (assoc height-col socket-data)))
    (setq count-val (cdr (assoc count-col socket-data)))
    
    ;; 处理空值
    (if (not left-val) (setq left-val ""))
    (if (not right-val) (setq right-val ""))
    (if (not height-val) (setq height-val ""))
    (if (not count-val) (setq count-val ""))
    
    ;; 检查冲突（左右墙体都有数据）
    (if (and (not (= left-val "")) (not (= right-val "")))
      (progn
        (setq conflict-count (+ conflict-count 1))
        (setq right-val "") ;; 冲突时选择左墙体
        (princ (strcat "\n警告：插座" (itoa socket-num) "左右墙体数据冲突，选择左墙体"))
      )
    )
    
    ;; 智能默认值处理：当有距离和高度但个数为空时，设为1
    (if (and (or (not (= left-val "")) (not (= right-val "")))
             (not (= height-val ""))
             (= count-val ""))
      (setq count-val "1")
    )
    
    ;; 处理左墙体插座
    (if (and (not (= left-val "")) (not (= height-val "")) (not (= count-val ""))
             (is-valid-number left-val) (is-valid-number height-val))
      (progn
        (setq wall-distance (safe-atof left-val))
        (setq socket-height (safe-atof height-val))
        (setq socket-count (if (is-valid-number count-val) (safe-atoi count-val) 1))
        
        (if (and (> wall-distance 0) (> socket-height 0) (> socket-count 0))
          (progn
            ;; 计算坐标：P1的Y轴+高度→pc1，pc1的X轴+距离→pc2
            (setq pc1 (list (car p1) (+ (cadr p1) socket-height) (caddr p1)))
            (setq pc2 (list (+ (car pc1) wall-distance) (cadr pc1) (caddr pc1)))
            
            ;; 根据个数生成坐标，依次向右偏移86单位
            (setq i 0)
            (while (< i socket-count)
              (setq current-coord (list (+ (car pc2) (* i 86)) (cadr pc2) (caddr pc2)))
              (setq result-coords (append result-coords (list current-coord)))
              (princ (strcat "\n左墙体插座" (itoa socket-num) "-" (itoa (+ i 1)) "：" 
                           (rtos (car current-coord) 2 4) "," 
                           (rtos (cadr current-coord) 2 4) "," 
                           (rtos (caddr current-coord) 2 4)))
              (setq i (+ i 1))
            )
          )
        )
      )
    )
    
    ;; 处理右墙体插座
    (if (and (not (= right-val "")) (not (= height-val "")) (not (= count-val ""))
             (is-valid-number right-val) (is-valid-number height-val))
      (progn
        (setq wall-distance (safe-atof right-val))
        (setq socket-height (safe-atof height-val))
        (setq socket-count (if (is-valid-number count-val) (safe-atoi count-val) 1))
        
        (if (and (> wall-distance 0) (> socket-height 0) (> socket-count 0))
          (progn
            ;; 计算坐标：P2的Y轴+高度→pc3，pc3的X轴-距离→pc4，pc4的X轴-86→pc5
            (setq pc3 (list (car p2) (+ (cadr p2) socket-height) (caddr p2)))
            (setq pc4 (list (- (car pc3) wall-distance) (cadr pc3) (caddr pc3)))
            (setq pc5 (list (- (car pc4) 86) (cadr pc4) (caddr pc4)))
            
            ;; 根据个数生成坐标，依次向左偏移86单位
            (setq i 0)
            (while (< i socket-count)
              (setq current-coord (list (- (car pc5) (* i 86)) (cadr pc5) (caddr pc5)))
              (setq result-coords (append result-coords (list current-coord)))
              (princ (strcat "\n右墙体插座" (itoa socket-num) "-" (itoa (+ i 1)) "：" 
                           (rtos (car current-coord) 2 4) "," 
                           (rtos (cadr current-coord) 2 4) "," 
                           (rtos (caddr current-coord) 2 4)))
              (setq i (+ i 1))
            )
          )
        )
      )
    )
    
    (setq socket-num (+ socket-num 1))
  )
  
  (if (> conflict-count 0)
    (princ (strcat "\n注意：发现 " (itoa conflict-count) " 组冲突数据，已自动选择左墙体数据"))
  )
  
  result-coords
)

;; 将坐标写入wall4.txt
(defun write-wall4-data (coord-list)
  (setq lsp-dir (get-lsp-directory))
  (setq wall4-file (strcat lsp-dir "\\wall4.txt"))
  (setq file (open wall4-file "w"))
  (if file
    (progn
      (setq point-num 1)
      (foreach coord coord-list
        (write-line (strcat "(p" (itoa point-num) " " 
                           (rtos (car coord) 2 4) "," 
                           (rtos (cadr coord) 2 4) "," 
                           (rtos (caddr coord) 2 4) ")") file)
        (setq point-num (+ point-num 1))
      )
      (close file)
      (princ (strcat "\n成功写入 " (itoa (length coord-list)) " 个坐标到wall4.txt"))
      T
    )
    (progn
      (princ "\n错误：无法创建wall4.txt文件")
      nil
    )
  )
)

;; 主函数HQT7
(defun C:HQT7 ()
  (princ "\n=== HQT7 插座坐标计算系统 - 重写版 ===")
  
  ;; 读取wall3.txt获取基准点
  (setq wall-data (read-wall3-data))
  (if (not wall-data)
    (progn
      (princ "\n错误：无法读取wall3.txt数据")
      (princ "\n请确保在运行HQT7前先运行HQT生成wall3.txt文件")
      (princ)
      (exit)
    )
  )
  (setq p1 (car wall-data))
  (setq p2 (cadr wall-data))
  
  ;; 验证基准点数据
  (if (or (not p1) (not p2))
    (progn
      (princ "\n错误：基准点P1或P2数据为空")
      (exit)
    )
  )
  
  (princ (strcat "\n基准点P1: (" (rtos (car p1) 2 4) "," (rtos (cadr p1) 2 4) "," (rtos (caddr p1) 2 4) ")"))
  (princ (strcat "\n基准点P2: (" (rtos (car p2) 2 4) "," (rtos (cadr p2) 2 4) "," (rtos (caddr p2) 2 4) ")"))
  
  ;; 读取choose.txt获取行号
  (setq row-num (read-choose-data))
  (princ (strcat "\n选择的数据行: " (itoa row-num)))
  
  ;; 读取CSV数据
  (setq lsp-dir (get-lsp-directory))
  (setq csv-file (strcat lsp-dir "\\data.csv"))
  (princ (strcat "\n[DEBUG] 尝试读取data.csv: " csv-file))
  
  ;; 尝试多个可能的路径
  (if (not (findfile csv-file))
    (progn
      (setq csv-file "c:\\Users\\TIAN\\Documents\\2025\\September\\lsp\\data.csv")
      (princ (strcat "\n[DEBUG] 备用路径1: " csv-file))
      (if (not (findfile csv-file))
        (progn
          (setq csv-file "c:\\Users\\TIAN\\Documents\\2025\\September\\OK\\data.csv")
          (princ (strcat "\n[DEBUG] 备用路径2: " csv-file))
        )
      )
    )
  )
  
  (if (not (findfile csv-file))
    (progn
      (princ "\n错误：在所有路径中都找不到data.csv文件")
      (princ "\n请确保 data.csv 文件存在于以下任一路径：")
      (princ (strcat "\n  1. " (strcat (get-lsp-directory) "\\data.csv")))
      (princ "\n  2. c:\\Users\\TIAN\\Documents\\2025\\September\\lsp\\data.csv")
      (princ "\n  3. c:\\Users\\TIAN\\Documents\\2025\\September\\OK\\data.csv")
      (princ)
      (exit)
    )
  )
  
  (princ (strcat "\n[DEBUG] 找到data.csv文件: " csv-file))
  
  (setq csv-result (read-csv-data csv-file))
  (if (not csv-result)
    (progn
      (princ "\n错误：无法读取CSV数据")
      (princ "\n请检查data.csv文件是否损坏或格式不正确")
      (princ)
      (exit)
    )
  )
  
  (setq headers (car csv-result))
  (setq data-lines (cadr csv-result))
  (princ (strcat "\n[DEBUG] CSV表头数量: " (itoa (length headers))))
  (princ (strcat "\n[DEBUG] CSV数据行数: " (itoa (length data-lines))))
  
  ;; 检查行号是否有效
  (if (or (<= row-num 0) (> row-num (length data-lines)))
    (progn
      (princ (strcat "\n错误：行号 " (itoa row-num) " 超出范围（1-" (itoa (length data-lines)) "）"))
      (exit)
    )
  )
  
  ;; 获取选中的数据行
  (setq selected-data (nth (- row-num 1) data-lines))
  
  ;; 将表头和数据组合成关联列表
  (setq socket-data '())
  (setq i 0)
  (while (< i (length headers))
    (setq socket-data (append socket-data (list (cons (nth i headers) (nth i selected-data)))))
    (setq i (+ i 1))
  )
  
  ;; 计算插座坐标
  (princ "\n开始计算插座坐标...")
  (setq result-coords (calculate-socket-coords p1 p2 socket-data))
  
  ;; 写入wall4.txt
  (if result-coords
    (progn
      (write-wall4-data result-coords)
      (princ (strcat "\n\n=== 插座坐标计算完成 ==="))
      (princ (strcat "\n共生成 " (itoa (length result-coords)) " 个插座坐标"))
      (princ "\n坐标已保存到wall4.txt文件")
      (princ "\n正在启动HQT8插座图块放置系统...")
      ;; 自动启动HQT8
      (c:HQT8)
    )
    (princ "\n没有找到有效的插座数据")
  )
  
  (princ)
)

(princ "\nHQT7插座坐标计算系统已加载（重写版），输入HQT7开始执行")
(princ)