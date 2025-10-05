;;; HQT2.LSP (改用 PLINE 优化版 - 针对 wall2.txt)
;;; 功能: 读取 wall2.txt 文件，按最终确认的顺序自动绘制闭合多段线 (PLINE) 并进行 SOLID 填充 (全自动)。
;;;      别名解析规则:
;;;        对于前缀 x, x1, z, z1: pt1 是 y 轴最小值点, pt2 是 y 轴最大值点。
;;;        对于前缀 y, y1: pt1 是 x 轴最小值点, pt2 是 x 轴最大值点。
;;; 文件格式: 每行一个点，格式为 (点名 x,y,z) ; x,y,z 为小数
;;; 命令: HQT2
;;; 连接顺序: xpt1 -> xpt2 -> ypt2 -> zpt1 -> z1pt1 -> z1pt2 -> y1pt1 -> x1pt1 -> xpt1 (闭合)

(defun c:HQT2 ( / lsp_dir wall_filepath fp lines line parsed_data
                 point_name x y z coords_list coords_str
                 trimmed_line len i char point_name_str
                 ; --- 变量 ---
                 loop_point_aliases loop_point_names loop_coords
                 pt_alias pt_name pt_coords idx all_points_found
                 num_points
                 x1_seq y1_seq z1_seq x_seq y_seq z_seq
                 x1_seq_len y1_seq_len z1_seq_len x_seq_len y_seq_len z_seq_len
                 ; --- 新增用于排序的变量 ---
                 x1_sorted y1_sorted z1_sorted x_sorted y_sorted z_sorted
                 unique_coords last_coord coord
                 pt ; 用于 find-point-coords-corrected
                 p1_coords p2_coords ; 不再需要 line_ent, drawn_lines
                 hatch_point pline_ent ss_for_hatch cmd_echo
                 ; --- 新增自动填充相关变量 ---
                 center_pt min_x max_x min_y max_y center_x center_y
               )

  ; 保存并关闭命令行回显，使过程更干净
  (setq cmd_echo (getvar 'CMDECHO))
  (setvar 'CMDECHO 0)
  
  ; 保存当前图层，确保绘制完成后恢复
  (setq current_layer (getvar 'CLAYER))

  ;;; 步骤 1: 获取 wall2.txt 文件路径 ;;;
  (setq lsp_dir (vl-filename-directory (findfile "HQT2.lsp")))
  (if lsp_dir
    (progn
      (setq wall_filepath (strcat lsp_dir "/wall2.txt"))
    )
    (progn
      (princ "\n错误: 无法确定 HQT2.lsp 文件目录。")
      (setvar 'CMDECHO cmd_echo) ; 恢复回显
      (exit)
    )
  )

  ;;; 步骤 2: 检查文件是否存在 ;;;
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

  ;;; 步骤 3: 读取文件内容到列表 ;;;
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

  ;;; 步骤 4: 解析每一行的点数据 ;;;
  (setq parsed_data '())
  (foreach l lines
    (progn
      (setq point_name nil)
      (setq x nil)
      (setq y nil)
      (setq z nil)
      (setq coords_str nil)

      ; 去除行首尾空格
      (setq trimmed_line (my-trim l))
      (setq len (strlen trimmed_line))

      ; 检查基本格式 (以 '(' 开头，以 ')' 结尾)
      (if (and (> len 2) (= (substr trimmed_line 1 1) "(") (= (substr trimmed_line len 1) ")"))
        (progn
          ; 查找点名结束的位置 (遇到空格、制表符或 ')')
          (setq i 2) ; 从 '(' 之后开始
          (while (and (<= i len) (/= (setq char (substr trimmed_line i 1)) " ") (/= char "\t") (/= char ")"))
            (setq i (1+ i))
          )
          (setq point_name_end (1- i)) ; 点名结束位置

          ; 提取点名字符串
          (if (>= point_name_end 2)
            (progn
              (setq point_name_str (substr trimmed_line 2 (- (1+ point_name_end) 2)))
              ; 去除可能存在的首尾引号
              (setq point_name_str (remove-quotes point_name_str))
              ; 存储为字符串
              (setq point_name point_name_str)

              ; 查找坐标开始位置 (跳过空格/制表符)
              (setq coords_start i)
              (while (and (<= coords_start len) (or (= (substr trimmed_line coords_start 1) " ") (= (substr trimmed_line coords_start 1) "\t")))
                (setq coords_start (1+ coords_start))
              )

              ; 提取坐标字符串 (直到 ')' 之前)
              (if (and (<= coords_start len) (< point_name_end coords_start))
                (progn
                 (setq coords_str (substr trimmed_line coords_start (- len coords_start))) ; 从 coords_start 到行尾
                 ; 去掉末尾的 ')'
                 (if (= (substr coords_str (strlen coords_str) 1) ")")
                   (setq coords_str (substr coords_str 1 (1- (strlen coords_str))))
                 )

                 ; 现在 coords_str 应该是 "x,y,z" 格式的字符串
                 (if (and coords_str (> (strlen coords_str) 0))
                   (progn
                     ; 使用更健壮的分割函数
                     (setq coords_list (split-coord-robust coords_str))
                     (if (and coords_list (>= (length coords_list) 2))
                       (progn
                         (setq x (atof (nth 0 coords_list))) ; 使用 nth 确保索引正确
                         (setq y (atof (nth 1 coords_list)))
                         ; Z 坐标可选
                         (if (>= (length coords_list) 3)
                           (setq z (atof (nth 2 coords_list)))
                           (setq z 0.0) ; 默认 Z 为 0
                         )
                         ; 将解析后的点信息存入列表 (点名 X Y Z)
                         (setq parsed_data (cons (list point_name x y z) parsed_data))
                       )
                       (princ (strcat "\n警告: 行 \"" l "\" 坐标格式不正确 (分割失败)，已跳过。"))
                     )
                   )
                   (princ (strcat "\n警告: 行 \"" l "\" 未找到坐标部分，已跳过。"))
                 )
                )
                (princ (strcat "\n警告: 行 \"" l "\" 格式解析失败 (坐标位置错误)，已跳过。"))
              )
            )
            (princ (strcat "\n警告: 行 \"" l "\" 未找到有效点名，已跳过。"))
          )
        )
        (princ (strcat "\n警告: 行 \"" l "\" 格式不正确 (不以 '(' 开头或不以 ')' 结尾)，已跳过。"))
      )
    ) ; end progn
  ) ; end foreach
  ; 恢复原始顺序
  (setq parsed_data (reverse parsed_data))

  ;;; 步骤 5: 根据序列分组点数据 ;;;
  (setq x1_seq '())
  (setq y1_seq '())
  (setq z1_seq '())
  (setq x_seq '())
  (setq y_seq '())
  (setq z_seq '())
  (foreach pt parsed_data
    (setq point_name (car pt))
    (cond
      ((wcmatch (strcase point_name) "X1PT*")
       (setq x1_seq (cons pt x1_seq)) ; 存储整个点数据 (name x y z)
      )
      ((wcmatch (strcase point_name) "Y1PT*")
       (setq y1_seq (cons pt y1_seq))
      )
      ((wcmatch (strcase point_name) "Z1PT*")
       (setq z1_seq (cons pt z1_seq))
      )
      ((wcmatch (strcase point_name) "XPT*")
       (setq x_seq (cons pt x_seq))
      )
      ((wcmatch (strcase point_name) "YPT*")
       (setq y_seq (cons pt y_seq))
      )
      ((wcmatch (strcase point_name) "ZPT*")
       (setq z_seq (cons pt z_seq))
      )
    )
  )
  ; 恢复原始顺序 (因为是用 cons 添加的)
  (setq x1_seq (reverse x1_seq))
  (setq y1_seq (reverse y1_seq))
  (setq z1_seq (reverse z1_seq))
  (setq x_seq (reverse x_seq))
  (setq y_seq (reverse y_seq))
  (setq z_seq (reverse z_seq))

  ;;; 步骤 6: 根据规则对序列进行排序 ;;;
  ; 对于 x, x1, z, z1: 按 y 值升序排序 (pt1=min y, pt2=max y)
  (setq x_sorted (vl-sort x_seq '(lambda (a b) (< (caddr a) (caddr b)))))
  (setq x1_sorted (vl-sort x1_seq '(lambda (a b) (< (caddr a) (caddr b)))))
  (setq z_sorted (vl-sort z_seq '(lambda (a b) (< (caddr a) (caddr b)))))
  (setq z1_sorted (vl-sort z1_seq '(lambda (a b) (< (caddr a) (caddr b)))))

  ; 对于 y, y1: 按 x 值升序排序 (pt1=min x, pt2=max x)
  (setq y_sorted (vl-sort y_seq '(lambda (a b) (< (cadr a) (cadr b)))))
  (setq y1_sorted (vl-sort y1_seq '(lambda (a b) (< (cadr a) (cadr b)))))

  ; 获取排序后序列的长度
  (setq x1_seq_len (length x1_sorted))
  (setq y1_seq_len (length y1_sorted))
  (setq z1_seq_len (length z1_sorted))
  (setq x_seq_len (length x_sorted))
  (setq y_seq_len (length y_sorted))
  (setq z_seq_len (length z_sorted))

  ;;; 步骤 7: 定义构成闭合环的点别名顺序 (最终确认版) ;;;
  ; 检查 x x1 是否不存在
  (if (and (= x_seq_len 0) (= x1_seq_len 0))
    ; 如果 x x1 不存在，使用替代连接顺序
    (progn
      (princ "\n检测到 x x1 不存在，使用替代连接顺序: ypt1#ypt2#zpt1#z1pt1#z1pt2#y1pt1#ypt1 (闭合)")
      (setq loop_point_aliases '("ypt1" "ypt2" "zpt1" "z1pt1" "z1pt2" "y1pt1" "ypt1"))
    )
    ; 否则使用原始连接顺序
    (progn
      (princ "\n使用原始连接顺序: xpt1 -> xpt2 -> ypt2 -> zpt1 -> z1pt1 -> z1pt2 -> y1pt1 -> x1pt1 -> xpt1 (闭合)")
      (setq loop_point_aliases '("xpt1" "xpt2" "ypt2" "zpt1" "z1pt1" "z1pt2" "y1pt1" "x1pt1"))
    )
  )
  (setq loop_point_names '()) ; 存储解析后的实际点名
  (setq loop_coords '())
  (setq all_points_found T) ; 初始化标志为真

  ; 解析别名并查找坐标
  (foreach pt_alias loop_point_aliases
    (setq pt_name nil) ; 重置 pt_name
    ; --- 别名解析逻辑 (根据排序后的序列) ---
    (cond
      ; --- 处理 x 序列 ---
      ((= (strcase pt_alias) "XPT1")
       (if (>= x_seq_len 1) (setq pt_name (car (nth 0 x_sorted)))) ; y 最小
      )
      ((= (strcase pt_alias) "XPT2")
       (if (> x_seq_len 0) (setq pt_name (car (nth (1- x_seq_len) x_sorted)))) ; y 最大
      )
      ; --- 处理 y 序列 ---
      ((= (strcase pt_alias) "YPT1")
       (if (>= y_seq_len 1) (setq pt_name (car (nth 0 y_sorted)))) ; x 最小
      )
      ((= (strcase pt_alias) "YPT2")
       (if (> y_seq_len 0) (setq pt_name (car (nth (1- y_seq_len) y_sorted)))) ; x 最大
      )
      ; --- 处理 z 序列 ---
      ((= (strcase pt_alias) "ZPT1")
       (if (>= z_seq_len 1) (setq pt_name (car (nth 0 z_sorted)))) ; y 最小
      )
      ((= (strcase pt_alias) "ZPT2")
       (if (> z_seq_len 0) (setq pt_name (car (nth (1- z_seq_len) z_sorted)))) ; y 最大
      )
      ; --- 处理 x1 序列 ---
      ((= (strcase pt_alias) "X1PT1")
       (if (>= x1_seq_len 1) (setq pt_name (car (nth 0 x1_sorted)))) ; y 最小
      )
      ((= (strcase pt_alias) "X1PT2")
       (if (> x1_seq_len 0) (setq pt_name (car (nth (1- x1_seq_len) x1_sorted)))) ; y 最大
      )
      ; --- 处理 y1 序列 ---
      ((= (strcase pt_alias) "Y1PT1")
       (if (>= y1_seq_len 1) (setq pt_name (car (nth 0 y1_sorted)))) ; x 最小
      )
      ((= (strcase pt_alias) "Y1PT2")
       (if (> y1_seq_len 0) (setq pt_name (car (nth (1- y1_seq_len) y1_sorted)))) ; x 最大
      )
      ; --- 处理 z1 序列 ---
      ((= (strcase pt_alias) "Z1PT1")
       (if (>= z1_seq_len 1) (setq pt_name (car (nth 0 z1_sorted)))) ; y 最小
      )
      ((= (strcase pt_alias) "Z1PT2")
       (if (> z1_seq_len 0) (setq pt_name (car (nth (1- z1_seq_len) z1_sorted)))) ; y 最大
      )
    ) ; end cond 别名解析

    ; --- 检查 pt_name 是否找到并获取坐标 ---
    (if pt_name
      (progn
        (setq pt_coords (find-point-coords-corrected pt_name parsed_data)) ; 使用修正后的查找函数
        (if pt_coords
          (progn
            (setq loop_point_names (append loop_point_names (list pt_name))) ; 记录实际点名
            (setq loop_coords (append loop_coords (list pt_coords)))
          )
          (progn
            (princ (strcat "\n错误: 无法找到点 \"" pt_name "\" 的坐标 (由别名 \"" pt_alias "\" 解析而来)。"))
            (setq all_points_found nil)
          )
        )
      )
      (progn
        (princ (strcat "\n错误: 无法解析别名 \"" pt_alias "\" 或找不到对应点。"))
        (setq all_points_found nil)
      )
    )
  ) ; end foreach

  ; 添加闭合点 (第一个点 xpt1) 到坐标列表末尾
  (if (and all_points_found (> (length loop_coords) 0))
    (progn
      ; 闭合点是第一个点的坐标
      (setq loop_coords (append loop_coords (list (nth 0 loop_coords))))
    )
    (progn
     (if (not all_points_found)
       (princ "\n错误: 无法找到构成环的所有点坐标。")
       (princ "\n错误: 环点坐标列表为空，无法闭合。")
     )
     (setvar 'CMDECHO cmd_echo)
     (exit)
    )
  )

  ; 去除连续的重复坐标 (只保留第一个) - 用于最终的线段列表
  (setq unique_coords '())
  (setq last_coord nil)
  (foreach coord loop_coords
    (if (and last_coord
             (equal (nth 0 coord) (nth 0 last_coord) 1e-6)
             (equal (nth 1 coord) (nth 1 last_coord) 1e-6)
            )
      ; 如果当前坐标与上一个坐标相同 (忽略Z)，则跳过
      (progn
        ; 跳过重复点
      )
      (progn
        ; 否则，添加到 unique_coords 并更新 last_coord
        (setq unique_coords (append unique_coords (list coord)))
        (setq last_coord coord)
      )
    )
  )
  ; *注意*: 闭合点已经添加到 loop_coords，去重逻辑会处理它。

  (setq num_points (length unique_coords))

  ; 检查点数是否足够绘制线段
  (if (< num_points 2)
    (progn
      (princ "\n错误: 去重后点数不足 (少于2个)，无法绘制线段。")
      (setvar 'CMDECHO cmd_echo)
      (exit)
    )
  )

  ;;; 步骤 8: 使用 PLINE 命令绘制闭合多段线 (在墙体图层上绘制但不改变当前图层) ;;;
  (princ "
正在绘制闭合多段线...")
  
  ; 检查墙体图层是否存在，不存在则创建
  (if (null (tblsearch "LAYER" "墙体"))
    (progn
      (command "_.LAYER" "_NEW" "墙体" "")
      (princ "
[INFO] 已创建墙体图层")
    )
  )
  
  ; 切换到墙体图层
  (command "_.LAYER" "_SET" "墙体" "")
  
  (command "_.PLINE") ; 启动 PLINE 命令
  (foreach pt unique_coords
    (command (list (car pt) (cadr pt) (if (>= (length pt) 3) (nth 2 pt) 0.0))) ; 传递点坐标 (x y z)
  )
  (command "C") ; 发送 "C" 闭合命令
  (setq pline_ent (entlast)) ; 获取刚创建的多段线实体名

  ; 检查 PLINE 是否成功创建
  (if (and pline_ent (eq (cdr (assoc 0 (entget pline_ent))) "LWPOLYLINE"))
    (progn
      (princ "\n闭合多段线绘制成功。")
    )
    (progn
      (princ "\n错误: 闭合多段线绘制失败。")
      (setvar 'CMDECHO cmd_echo)
      (exit)
    )
  )
  
  
;;; 步骤 9: 对闭合多段线进行 SOLID 填充 (HATCH) - 完全自动版 ;;;
(princ "\n正在进行 SOLID 填充...")

; 使用完全非交互式的 -HATCH 命令
; 参数顺序：Pattern -> Point -> Select objects -> Enter -> Scale -> Angle -> Accept
(command "_.-HATCH" 
         "P" "SOLID" ; 设置图案为 SOLID
         "S" pline_ent "" ; 选择对象方式，选择多段线
         "" ; 结束对象选择
         "" ; 接受默认设置
)

; 等待 HATCH 命令完全结束
(while (> (getvar "CMDACTIVE") 0)
  (princ ".")
  (command "")
)

(princ "\nSOLID 填充完成。")

;;; 步骤 10: 删除边界多段线，只保留填充 ;;;
(princ "\n正在删除边界线...")
(if (and pline_ent (entget pline_ent))
  (progn
    (entdel pline_ent)
    (princ "\n边界线已删除。")
  )
  (princ "\n警告: 无法删除边界线。")
)

; 恢复原始图层
(command "_.LAYER" "_SET" current_layer "")
(princ "
[INFO] 已恢复原始图层")

; 恢复命令行回显
(setvar 'CMDECHO cmd_echo)
(princ "
HQT2 命令执行完毕。")
(c:HQT4) ; 执行完毕后调用HQT4
(princ)
) ; end defun c:HQT2


; 去除字符串首尾空格的函数
(defun my-trim (str / len i char start end)
  (setq len (strlen str))
  (if (= len 0)
    ""
    (progn
      ; 查找第一个非空格字符
      (setq i 1)
      (while (and (<= i len) (or (= (setq char (substr str i 1)) " ") (= char "\t")))
        (setq i (1+ i))
      )
      (setq start i)

      ; 查找最后一个非空格字符
      (setq i len)
      (while (and (>= i 1) (or (= (setq char (substr str i 1)) " ") (= char "\t")))
        (setq i (1- i))
      )
      (setq end i)

      ; 返回子字符串
      (if (> end start)
        (substr str start (- (1+ end) start))
        "" ; 如果全是空格或制表符，返回空字符串
      )
    )
  )
) ; end defun my-trim

; 去除字符串首尾引号的函数
(defun remove-quotes (str / len first_char last_char)
  (setq len (strlen str))
  (if (>= len 2)
    (progn
      (setq first_char (substr str 1 1))
      (setq last_char (substr str len 1))
      (if (and (= first_char "\"") (= last_char "\""))
        (substr str 2 (- len 2)) ; 去掉首尾引号
        str ; 否则返回原字符串
      )
    )
    str ; 长度小于2，直接返回
  )
) ; end defun remove-quotes

; 更健壮的坐标分割函数 (处理逗号和空格)
(defun split-coord-robust (coord-str / parts part-list char-list i char part)
  (setq char-list (vl-string->list coord-str))
  (setq part-list '())
  (setq part "")
  (setq i 0)
  (while (< i (length char-list))
    (setq char (chr (nth i char-list)))
    (if (or (= char ",") (= char " ") (= char "\t"))
      (progn
        (if (> (strlen part) 0)
          (setq part-list (append part-list (list part)))
        )
        (setq part "") ; 重置 part
      )
      (setq part (strcat part char)) ; 添加字符到当前 part
    )
    (setq i (1+ i))
  )
  ; 添加最后一个 part
  (if (> (strlen part) 0)
    (setq part-list (append part-list (list part)))
  )
  ; 返回列表
  part-list
) ; end defun split-coord-robust

; 修正后的查找点坐标函数 (使用 lambda 和 vl-some，不使用 return)
; 这个函数接收一个点名和点数据列表，返回该点的坐标列表 (x y z) 或 nil
(defun find-point-coords-corrected (pt-name data-list)
  (cdr (vl-some '(lambda (pt) (if (= (car pt) pt-name) pt nil)) data-list))
) ; end defun find-point-coords-corrected

(princ "\nHQT2.LSP (改用 PLINE 优化版 - 针对 wall2.txt) 已加载。命令: HQT2")
(princ)
