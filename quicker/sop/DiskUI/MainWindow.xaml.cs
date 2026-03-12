using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DiskUI
{
    public partial class MainWindow : Window
    {
        // ── 几何参数（与HTML版完全一致）──────────────────────
        const double Rc   = 20;    // 圆1半径
        const double R1   = 100;   // 环1外径
        const double R2   = 200;   // 环2外径
        const double R3   = 300;   // 环3外径
        const double ROT  = 22.5;  // 整盘旋转角度
        const double START = -90;  // 几何起始角
        const double S8   = 45;
        const double S16  = 22.5;

        // 编号偏移（已校正：12点为扇区1起点）
        const int OFF_R1    = 1;
        const int OFF_R2_8  = 1;
        const int OFF_R2_16 = 1;
        const int OFF_R3    = 1;

        // 画布中心
        const double CX = 350, CY = 350;

        // 颜色
        static readonly Color C_R1_Even   = ParseHex("#0c1e12");
        static readonly Color C_R1_Odd    = ParseHex("#0f2015");
        static readonly Color C_R1_Div    = ParseHex("#2a7a4a");
        static readonly Color C_R1_Border = ParseHex("#1a5a2a");
        static readonly Color C_R2_Even   = ParseHex("#140f24");
        static readonly Color C_R2_Odd    = ParseHex("#17122a");
        static readonly Color C_R2_Div    = ParseHex("#5a3a8a");
        static readonly Color C_R2_Div2   = ParseHex("#4a2a6a");
        static readonly Color C_R2_Border = ParseHex("#2a1a4a");
        static readonly Color C_R3_Even   = ParseHex("#1f1018");
        static readonly Color C_R3_Odd    = ParseHex("#231220");
        static readonly Color C_R3_Border = ParseHex("#3a1535");
        static readonly Color C_C1_Fill   = ParseHex("#0a1828");
        static readonly Color C_C1_Border = ParseHex("#1a5a8a");
        static readonly Color C_Label     = Color.FromArgb(140, 255, 255, 255);
        static readonly Color C_Sel       = ParseHex("#4aaa88");
        static readonly Color C_SelBorder = ParseHex("#88ffcc");

        bool showCircle1 = true, showRing1 = true, showRing2 = true, showRing3 = false;
        bool outer16 = false;

        // 选中状态
        System.Windows.Shapes.Path? selectedPath = null;
        Brush? selectedOrigFill = null;
        Brush? selectedOrigStroke = null;

        // 日志文件路径
        private readonly string logFilePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "DiskUI_ClickLog.txt");

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => Render();
        }

        void OnToggle(object sender, RoutedEventArgs e)
        {
            showCircle1 = BtnCircle1?.IsChecked == true;
            showRing1   = BtnRing1?.IsChecked   == true;
            showRing2   = BtnRing2?.IsChecked   == true;
            showRing3   = BtnRing3?.IsChecked   == true;
            outer16     = BtnOuter16?.IsChecked  == true;
            selectedPath = null;
            if (DiskCanvas != null)
            {
                Render();
            }
        }

        void Render()
        {
            DiskCanvas.Children.Clear();

            // 整盘旋转变换（以画布中心为原点）
            var diskRotate = new RotateTransform(ROT, CX, CY);

            // ── 环3（R200~R300）8扇区 33-40，无分割线无标签 ──
            if (showRing3)
            {
                for (int i = 0; i < 8; i++)
                {
                    int sid = ((i + OFF_R3) % 8) + 33;
                    double sA = START + i * S8;
                    var fill = i % 2 == 0 ? C_R3_Even : C_R3_Odd;
                    var p = MakeSector(R2, R3, sA, S8, fill, ParseHex("#160b16"), 0.5, diskRotate);
                    p.Tag = new SectorInfo(sid, "环3", sA, S8);
                    p.PreviewMouseLeftButtonDown += OnSectorPreviewClick;
                    p.Cursor = Cursors.Hand;
                    DiskCanvas.Children.Add(p);
                }
                DiskCanvas.Children.Add(MakeCircle(R3, C_R3_Border, 1.0, diskRotate));
            }

            // ── 环2（R100~R200）8或16扇区，9-16 / 9-24 ──
            if (showRing2)
            {
                int cnt   = outer16 ? 16 : 8;
                double stp = outer16 ? S16 : S8;
                int off   = outer16 ? OFF_R2_16 : OFF_R2_8;

                for (int i = 0; i < cnt; i++)
                {
                    int sid = ((i + off) % cnt) + 9;
                    double sA = START + i * stp;
                    var fill = i % 2 == 0 ? C_R2_Even : C_R2_Odd;
                    var p = MakeSector(R1, R2, sA, stp, fill, ParseHex("#0e0a18"), 0.4, diskRotate);
                    p.Tag = new SectorInfo(sid, "环2", sA, stp);
                    p.PreviewMouseLeftButtonDown += OnSectorPreviewClick;
                    p.Cursor = Cursors.Hand;
                    DiskCanvas.Children.Add(p);

                    // 标签（反旋转保持直立）
                    AddLabel(sid.ToString(), R1, R2, sA, stp, diskRotate);
                }

                // 分割线
                for (int i = 0; i < 8; i++)
                {
                    double a = START + i * S8;
                    DiskCanvas.Children.Add(MakeDivLine(R1, R2, a, C_R2_Div, 1.2, diskRotate));
                }
                if (outer16)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        double a = START + i * S8 + S16;
                        DiskCanvas.Children.Add(MakeDivLine(R1, R2, a, C_R2_Div2, 0.8, diskRotate));
                    }
                }
                DiskCanvas.Children.Add(MakeCircle(R2, C_R2_Border, 1.0, diskRotate));
            }

            // ── 环1（R20~R100）8扇区，1-8 ──
            if (showRing1)
            {
                for (int i = 0; i < 8; i++)
                {
                    int sid = ((i + OFF_R1) % 8) + 1;
                    double sA = START + i * S8;
                    var fill = i % 2 == 0 ? C_R1_Even : C_R1_Odd;
                    var p = MakeSector(Rc, R1, sA, S8, fill, ParseHex("#091510"), 0.4, diskRotate);
                    p.Tag = new SectorInfo(sid, "环1", sA, S8);
                    p.PreviewMouseLeftButtonDown += OnSectorPreviewClick;
                    p.Cursor = Cursors.Hand;
                    DiskCanvas.Children.Add(p);

                    AddLabel(sid.ToString(), Rc, R1, sA, S8, diskRotate);
                }
                for (int i = 0; i < 8; i++)
                {
                    double a = START + i * S8;
                    DiskCanvas.Children.Add(MakeDivLine(Rc, R1, a, C_R1_Div, 1.2, diskRotate));
                }
                DiskCanvas.Children.Add(MakeCircle(R1, C_R1_Border, 1.0, diskRotate));
            }

            // ── 圆1（整圆R20）无分割线 ──
            if (showCircle1)
            {
                var c = new Ellipse
                {
                    Width  = Rc * 2,
                    Height = Rc * 2,
                    Fill   = new SolidColorBrush(C_C1_Fill),
                    Stroke = new SolidColorBrush(C_C1_Border),
                    StrokeThickness = 1.2,
                    Cursor = Cursors.Hand,
                    Tag = new SectorInfo(0, "圆1", 0, 360)
                };
                Canvas.SetLeft(c, CX - Rc);
                Canvas.SetTop(c,  CY - Rc);
                c.MouseLeftButtonDown += (s, _) => {
                    ResetSelection();
                    InfoText.Text = "圆1  ·  中心圆  ·  R=20  ·  无分割";
                    InfoText.Foreground = new SolidColorBrush(C_Sel);
                    // 记录圆1点击
                    LogCircleClick();
                };
                DiskCanvas.Children.Add(c);
            }

            // 装饰外圆
            DiskCanvas.Children.Add(MakeCircleRaw(R3 + 3, Color.FromRgb(0x1a,0x1a,0x1a), 1.5));
            // 圆心点
            var dot = new Ellipse { Width=5, Height=5, Fill=new SolidColorBrush(C_Sel) };
            Canvas.SetLeft(dot, CX - 2.5); Canvas.SetTop(dot, CY - 2.5);
            DiskCanvas.Children.Add(dot);
        }

        // ── 几何工具 ────────────────────────────────────────────

        Point Pt(double r, double deg)
        {
            double rad = deg * Math.PI / 180;
            return new Point(CX + r * Math.Cos(rad), CY + r * Math.Sin(rad));
        }

        // 旋转后角度
        Point PtRot(double r, double deg) => Pt(r, deg + ROT);

        System.Windows.Shapes.Path MakeSector(double r1, double r2, double startDeg, double span,
                        Color fill, Color stroke, double sw, RotateTransform rot)
        {
            double endDeg = startDeg + span;
            bool largeArc = span > 180;

            var p1s = Pt(r2, startDeg); var p1e = Pt(r2, endDeg);
            var p2s = Pt(r1, startDeg); var p2e = Pt(r1, endDeg);

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(p1s, true, true);
                ctx.ArcTo(p1e, new Size(r2, r2), 0, largeArc, SweepDirection.Clockwise, true, false);
                if (r1 > 0)
                {
                    ctx.LineTo(p2e, true, false);
                    ctx.ArcTo(p2s, new Size(r1, r1), 0, largeArc, SweepDirection.Counterclockwise, true, false);
                }
                else
                {
                    ctx.LineTo(new Point(CX, CY), true, false);
                }
            }
            geo.Transform = rot;
            geo.Freeze();

            return new System.Windows.Shapes.Path
            {
                Data = geo,
                Fill = new SolidColorBrush(fill),
                Stroke = new SolidColorBrush(stroke),
                StrokeThickness = sw
            };
        }

        Line MakeDivLine(double r1, double r2, double deg, Color stroke, double sw, RotateTransform rot)
        {
            var p1 = Pt(r1, deg); var p2 = Pt(r2, deg);
            var tg = new TransformGroup();
            tg.Children.Add(rot);
            var l = new Line
            {
                X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y,
                Stroke = new SolidColorBrush(stroke),
                StrokeThickness = sw,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap   = PenLineCap.Round,
                Opacity = 0.85,
                RenderTransform = rot,
                RenderTransformOrigin = new Point(0, 0)
            };
            // 分割线用相同旋转变换（通过几何坐标计算旋转后点位置）
            // 重新用旋转后坐标直接算
            double rad = (deg + ROT) * Math.PI / 180;
            l.X1 = CX + r1 * Math.Cos(rad); l.Y1 = CY + r1 * Math.Sin(rad);
            l.X2 = CX + r2 * Math.Cos(rad); l.Y2 = CY + r2 * Math.Sin(rad);
            l.RenderTransform = null;
            return l;
        }

        Ellipse MakeCircle(double r, Color stroke, double sw, RotateTransform rot)
        {
            var e = new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Fill = Brushes.Transparent,
                Stroke = new SolidColorBrush(stroke),
                StrokeThickness = sw,
                Opacity = 0.5
            };
            Canvas.SetLeft(e, CX - r); Canvas.SetTop(e, CY - r);
            return e;
        }

        Ellipse MakeCircleRaw(double r, Color stroke, double sw)
        {
            var e = new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Fill = Brushes.Transparent,
                Stroke = new SolidColorBrush(stroke),
                StrokeThickness = sw
            };
            Canvas.SetLeft(e, CX - r); Canvas.SetTop(e, CY - r);
            return e;
        }

        void AddLabel(string text, double r1, double r2, double startDeg, double span, RotateTransform rot)
        {
            double midDeg = startDeg + span / 2 + ROT; // 旋转后的中点角
            double midR   = (r1 + r2) / 2;
            double rad    = midDeg * Math.PI / 180;
            double x = CX + midR * Math.Cos(rad);
            double y = CY + midR * Math.Sin(rad);

            var tb = new TextBlock
            {
                Text       = text,
                Foreground = new SolidColorBrush(C_Label),
                FontFamily = new FontFamily("Courier New"),
                FontSize   = 9.5,
                Background = Brushes.Transparent,
                IsHitTestVisible = false
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double w = tb.DesiredSize.Width, h = tb.DesiredSize.Height;
            Canvas.SetLeft(tb, x - w / 2);
            Canvas.SetTop(tb,  y - h / 2);
            DiskCanvas.Children.Add(tb);
        }

        void OnSectorClick(object sender, MouseButtonEventArgs e)
        {
            ResetSelection();
            if (sender is System.Windows.Shapes.Path p && p.Tag is SectorInfo info)
            {
                selectedPath      = p;
                selectedOrigFill  = p.Fill;
                selectedOrigStroke = p.Stroke;
                p.Fill   = new SolidColorBrush(Color.FromArgb(200, 74, 170, 136));
                p.Stroke = new SolidColorBrush(C_SelBorder);
                p.StrokeThickness = 1.5;

                double sA = ((info.StartDeg + ROT) % 360 + 360) % 360;
                double eA = ((info.StartDeg + info.Span + ROT) % 360 + 360) % 360;
                double mA = ((info.StartDeg + info.Span / 2 + ROT) % 360 + 360) % 360;

                InfoText.Text = $"扇区 #{info.Sid}  ·  {info.Ring}  ·  起 {sA:F1}°  →  止 {eA:F1}°  ·  中线 {mA:F1}°";
                InfoText.Foreground = new SolidColorBrush(C_Sel);
            }
            e.Handled = true;
        }

        void OnSectorPreviewClick(object sender, MouseButtonEventArgs e)
        {
            Console.WriteLine($"[点击事件] Sender: {sender?.GetType().Name}");
            if (sender is System.Windows.Shapes.Path p && p.Tag is SectorInfo info)
            {
                Console.WriteLine($"[点击扇区] 扇区编号: {info.Sid}, 环: {info.Ring}, 起始角度: {info.StartDeg}, 跨度: {info.Span}");
                // 记录到日志文件
                LogSectorClick(info);
            }
            OnSectorClick(sender, e);
            e.Handled = true;
        }

        // 记录扇区点击到日志文件
        void LogSectorClick(SectorInfo info)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string logEntry = $"[{timestamp}] 点击扇区 #{info.Sid} | 环: {info.Ring} | 起始角度: {info.StartDeg}° | 跨度: {info.Span}°";

                // 确保目录存在
                string? directory = System.IO.Path.GetDirectoryName(logFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 追加写入日志文件
                File.AppendAllText(logFilePath, logEntry + Environment.NewLine, System.Text.Encoding.UTF8);
                Console.WriteLine($"[日志] 已记录: {logEntry}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[日志错误] 无法写入日志文件: {ex.Message}");
            }
        }

        // 记录圆1点击到日志文件
        void LogCircleClick()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string logEntry = $"[{timestamp}] 点击圆1 | 中心圆 | 半径: {Rc} | 无分割";

                // 确保目录存在
                string? directory = System.IO.Path.GetDirectoryName(logFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 追加写入日志文件
                File.AppendAllText(logFilePath, logEntry + Environment.NewLine, System.Text.Encoding.UTF8);
                Console.WriteLine($"[日志] 已记录: {logEntry}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[日志错误] 无法写入日志文件: {ex.Message}");
            }
        }

        void ResetSelection()
        {
            if (selectedPath != null && selectedOrigFill != null)
            {
                selectedPath.Fill   = selectedOrigFill;
                selectedPath.Stroke = selectedOrigStroke;
                selectedPath.StrokeThickness = 0.4;
                selectedPath = null;
            }
            InfoText.Text = "点击扇区查看信息";
            InfoText.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
        }

        static Color ParseHex(string hex)
        {
            hex = hex.TrimStart('#');
            return Color.FromRgb(
                Convert.ToByte(hex[0..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16));
        }
    }

    record SectorInfo(int Sid, string Ring, double StartDeg, double Span);
}
