using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Theming;

namespace AdvancedControls.Rendering
{
    /// <summary>
    /// 컨트롤 그리기에 공통으로 쓰이는 도형·색 헬퍼.
    /// </summary>
    public static class AdvGraphics
    {
        /// <summary>
        /// 네 모서리 반경이 각각 다른 사각형 경로.
        /// 호출자가 경로를 Dispose 해야 한다.
        /// </summary>
        public static GraphicsPath CreateRoundedRect(Rectangle bounds, AdvCorners corners)
        {
            var path = new GraphicsPath();
            BuildRoundedRect(path, bounds, corners);
            return path;
        }

        /// <summary>
        /// 기존 <see cref="GraphicsPath"/>에 둥근 사각형을 채워 넣는다.
        /// 경로를 재사용해 프레임마다 새로 할당하지 않으려는 그리기 경로에서 쓴다.
        /// 호출 전에 필요하면 <see cref="GraphicsPath.Reset"/>을 부른다.
        /// </summary>
        internal static void BuildRoundedRect(GraphicsPath path, Rectangle bounds, AdvCorners corners)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            // 반경이 변의 절반을 넘으면 호가 서로 겹쳐 도형이 뒤집힌다
            int limit = Math.Min(bounds.Width, bounds.Height) / 2;
            var c = corners.Clamp(0, limit);

            if (c.IsZero)
            {
                path.AddRectangle(bounds);
                return;
            }

            int l = bounds.Left, t = bounds.Top, r = bounds.Right, b = bounds.Bottom;

            // 변과 호를 번갈아 명시적으로 잇는다. 반경이 0인 모서리는 호를 건너뛰면
            // 앞뒤 변이 그대로 이어져 직각이 된다.
            path.StartFigure();

            path.AddLine(l + c.TopLeft, t, r - c.TopRight, t);
            if (c.TopRight > 0)
                path.AddArc(r - c.TopRight * 2, t, c.TopRight * 2, c.TopRight * 2, 270, 90);

            path.AddLine(r, t + c.TopRight, r, b - c.BottomRight);
            if (c.BottomRight > 0)
                path.AddArc(r - c.BottomRight * 2, b - c.BottomRight * 2,
                            c.BottomRight * 2, c.BottomRight * 2, 0, 90);

            path.AddLine(r - c.BottomRight, b, l + c.BottomLeft, b);
            if (c.BottomLeft > 0)
                path.AddArc(l, b - c.BottomLeft * 2, c.BottomLeft * 2, c.BottomLeft * 2, 90, 90);

            path.AddLine(l, b - c.BottomLeft, l, t + c.TopLeft);
            if (c.TopLeft > 0)
                path.AddArc(l, t, c.TopLeft * 2, c.TopLeft * 2, 180, 90);

            path.CloseFigure();
        }

        public static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
        {
            return CreateRoundedRect(bounds, new AdvCorners(radius));
        }

        /// <summary>
        /// 테두리를 안쪽으로 그리기 위한 보정. 펜은 선 중심을 기준으로 그려지므로
        /// 보정하지 않으면 두께의 절반이 컨트롤 밖으로 잘려 나간다.
        /// </summary>
        public static Rectangle Deflate(Rectangle bounds, int borderWidth)
        {
            if (borderWidth <= 0) return bounds;

            int half = borderWidth / 2;
            return new Rectangle(
                bounds.X + half,
                bounds.Y + half,
                Math.Max(0, bounds.Width - borderWidth),
                Math.Max(0, bounds.Height - borderWidth));
        }

        // ── DPI 스케일 ────────────────────────────────────────────────
        // 글리프·아이콘 치수는 96dpi 기준 '논리 픽셀'로 두고, 그릴 때 컨트롤이 놓인 화면의
        // DeviceDpi로 스케일한다. 고DPI에서 글자는 폰트 스케일로 커지는데 글리프가 고정이면
        // 상대적으로 작아지는 문제(D2)를 없앤다. DeviceDpi를 페인트 시점에 읽으므로 모니터 간
        // 이동(per-monitor DPI)에도 재페인트로 자기보정된다.

        /// <summary>96dpi 논리 픽셀 → 컨트롤 화면의 실제 픽셀(정수).</summary>
        public static int Scale(Control c, int logicalPx)
        {
            return ScaleDpi(logicalPx, c != null ? c.DeviceDpi : 96);
        }

        /// <summary>96dpi 논리 픽셀 → 실제 픽셀(실수, 펜 두께 등).</summary>
        public static float Scale(Control c, float logicalPx)
        {
            int dpi = c != null ? c.DeviceDpi : 96;
            return dpi == 96 ? logicalPx : logicalPx * dpi / 96f;
        }

        /// <summary>96dpi 논리 픽셀을 지정한 dpi로 스케일한다(스케일 계산 핵심).</summary>
        public static int ScaleDpi(int logicalPx, int dpi)
        {
            return dpi == 96 ? logicalPx : (int)Math.Round(logicalPx * dpi / 96.0);
        }

        /// <summary>셰브런이 향하는 쪽.</summary>
        public enum ChevronDirection { Up, Down, Left, Right }

        /// <summary>
        /// DPI 대응 셰브런. span·depth·thickness·offset을 컨트롤 DeviceDpi로 스케일한 뒤 그린다.
        /// area(중심 기준 배치)는 호출자가 이미 스케일해 넘긴다.
        /// </summary>
        public static void DrawChevron(Graphics g, Control c, Rectangle area, ChevronDirection dir,
                                       Color color, int span, int depth, float thickness, int offset)
        {
            DrawChevron(g, area, dir, color,
                        Scale(c, span), Scale(c, depth), Scale(c, thickness), Scale(c, offset));
        }

        /// <summary>
        /// 꺾인 화살표(셰브런)를 area의 가운데에 그린다.
        /// 콤보 화살표·숫자 증감·달력 이전다음이 같은 도형을 각자 그리고 있어 여기로 모았다.
        /// </summary>
        /// <param name="span">꺾이는 방향의 길이. 반대 축 길이는 그 절반보다 조금 크게 잡는다.</param>
        /// <param name="offset">가운데에서 밀어낼 픽셀. 두 개를 맞물려 놓을 때 벌리는 데 쓴다.</param>
        public static void DrawChevron(Graphics g, Rectangle area, ChevronDirection dir,
                                       Color color, int span, int depth,
                                       float thickness, int offset)
        {
            if (area.Width <= 0 || area.Height <= 0) return;

            bool vertical = dir == ChevronDirection.Up || dir == ChevronDirection.Down;
            int w = vertical ? span : depth;
            int h = vertical ? depth : span;

            int cx = area.Left + (area.Width - w) / 2;
            int cy = area.Top + (area.Height - h) / 2;

            if (vertical) cy += offset; else cx += offset;

            Point[] pts;
            switch (dir)
            {
                case ChevronDirection.Up:
                    pts = new[] { new Point(cx, cy + h), new Point(cx + w / 2, cy), new Point(cx + w, cy + h) };
                    break;
                case ChevronDirection.Down:
                    pts = new[] { new Point(cx, cy), new Point(cx + w / 2, cy + h), new Point(cx + w, cy) };
                    break;
                case ChevronDirection.Left:
                    pts = new[] { new Point(cx + w, cy), new Point(cx, cy + h / 2), new Point(cx + w, cy + h) };
                    break;
                default:
                    pts = new[] { new Point(cx, cy), new Point(cx + w, cy + h / 2), new Point(cx, cy + h) };
                    break;
            }

            using (var pen = new Pen(color, thickness))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, pts);
            }
        }

        /// <summary>
        /// 채우기 브러시. end가 비어 있으면 단색, 아니면 선형 그라데이션이다.
        /// 호출자가 Dispose 해야 한다.
        /// </summary>
        public static Brush CreateFillBrush(Rectangle bounds, Color start, Color end, float angleDegrees)
        {
            if (end.IsEmpty || end == start || bounds.Width <= 0 || bounds.Height <= 0)
                return new SolidBrush(start);

            // LinearGradientBrush는 경계에서 반대편 색이 1픽셀 새는 문제가 있어 살짝 넓혀 만든다
            var r = Rectangle.Inflate(bounds, 1, 1);
            return new LinearGradientBrush(r, start, end, angleDegrees);
        }

        // 그림자는 매 프레임(호버·글로우 전환 중 최대 66fps) 다시 그려지므로,
        // 블러 레이어마다 경로·브러시를 새로 만들지 않고 스레드마다 하나씩 재사용한다.
        // WinForms 그리기는 UI 스레드 전용이라 [ThreadStatic]이면 충돌이 없다.
        [ThreadStatic] private static GraphicsPath _shadowPath;
        [ThreadStatic] private static SolidBrush _shadowBrush;

        /// <summary>
        /// 도형 바깥에 그림자를 근사한다. 경로를 조금씩 넓히며 반투명하게 겹친다.
        /// </summary>
        public static void DrawShadow(Graphics g, Rectangle bounds, AdvCorners corners, AdvShadow shadow)
        {
            if (shadow == null || !shadow.IsVisible) return;

            var baseRect = new Rectangle(
                bounds.X + shadow.OffsetX,
                bounds.Y + shadow.OffsetY,
                bounds.Width,
                bounds.Height);

            var path = _shadowPath ?? (_shadowPath = new GraphicsPath());
            var brush = _shadowBrush ?? (_shadowBrush = new SolidBrush(Color.Black));

            for (int i = shadow.Blur; i >= 1; i--)
            {
                // 바깥쪽 레이어일수록 옅게. 겹쳐 쌓이면서 블러처럼 보인다
                int alpha = (int)(shadow.Color.A * (1f - (float)(i - 1) / shadow.Blur) / shadow.Blur);
                if (alpha <= 0) continue;

                var layer = Rectangle.Inflate(baseRect, i, i);
                if (layer.Width <= 0 || layer.Height <= 0) continue;

                var layerCorners = new AdvCorners(
                    corners.TopLeft + i, corners.TopRight + i,
                    corners.BottomRight + i, corners.BottomLeft + i);

                path.Reset();
                BuildRoundedRect(path, layer, layerCorners);
                brush.Color = Color.FromArgb(alpha, shadow.Color);
                g.FillPath(brush, path);
            }
        }

        /// <summary>
        /// 컨트롤에 둥근 모서리 <see cref="Region"/> 클립을 적용·갱신한다. 사각 자식이 둥근 코너 밖으로
        /// 튀지 않게 컨테이너·목록·그리드가 공통으로 쓴다(예전엔 컨트롤마다 복제돼 캐시 가드가 제각각이었다).
        /// clip이 직전과 같으면 GDI Region을 다시 만들지 않고, skip이면 클립을 없앤다.
        /// 이전 Region은 반드시 Dispose한다(GDI 누수 방지).
        /// </summary>
        /// <param name="skip">클립하지 않을 조건. Elevated(그림자가 잘림)·사각 도킹(반경 0) 등은 호출자가 판단한다.</param>
        /// <param name="cache">직전에 Region을 만든 clip. 모서리·테마가 바뀌어 크기가 같아도 다시 만들어야 하면
        /// 호출자가 <see cref="Rectangle.Empty"/>로 초기화해 넘긴다.</param>
        public static void UpdateRoundedRegion(Control control, Rectangle clip, AdvCorners corners,
                                               bool skip, ref Rectangle cache)
        {
            if (skip)
            {
                if (control.Region != null)
                {
                    var prev = control.Region;
                    control.Region = null;
                    prev.Dispose();
                }
                cache = Rectangle.Empty;
                return;
            }

            if (control.Region != null && clip == cache) return;   // 크기·위치가 그대로면 재생성하지 않는다

            cache = clip;
            var old = control.Region;
            using (var path = CreateRoundedRect(clip, corners))
                control.Region = new Region(path);
            if (old != null) old.Dispose();
        }

        /// <summary>
        /// 목록·트리 항목용 체크박스를 box에 그린다. 상자는 항상 입력 배경으로 채우고
        /// 체크는 강조색으로 — 선택된(강조색) 행 위에서도 대비가 유지된다.
        /// (AdvListBox·AdvTreeView가 같은 도형을 각자 그리고 있어 여기로 모았다.)
        /// </summary>
        public static void DrawItemCheckBox(Graphics g, Control c, Rectangle box,
                                            bool isChecked, bool enabled, AdvTheme theme)
        {
            var oldSmooth = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var path = CreateRoundedRect(box, Scale(c, 3)))
            {
                using (var b = new SolidBrush(enabled ? theme.InputBackground : theme.InputBackgroundDisabled))
                    g.FillPath(b, path);
                Color line = !enabled ? theme.TextDisabled : (isChecked ? theme.Accent : theme.Border);
                using (var pen = new Pen(line, Scale(c, isChecked ? 1.4f : 1f)))
                    g.DrawPath(pen, path);
            }

            if (isChecked)
            {
                int ins = Scale(c, 4);
                DrawCheckMark(g, c, Rectangle.Inflate(box, -ins, -ins), enabled ? theme.Accent : theme.TextDisabled);
            }

            g.SmoothingMode = oldSmooth;
        }

        /// <summary>
        /// 체크 표시(✓)만 area에 그린다. 상자 없는 체크가 필요한 곳(메뉴 체크 항목)과
        /// <see cref="DrawItemCheckBox"/>가 같은 도형을 공유한다.
        /// </summary>
        public static void DrawCheckMark(Graphics g, Control c, Rectangle area, Color color)
        {
            var oldSmooth = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var pts = new[]
            {
                new Point(area.Left, area.Top + area.Height / 2),
                new Point(area.Left + area.Width * 2 / 5, area.Bottom),
                new Point(area.Right, area.Top)
            };
            using (var pen = new Pen(color, Scale(c, 1.8f))
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, pts);
            g.SmoothingMode = oldSmooth;
        }

        // 비활성 아이콘용 반투명 매트릭스는 항상 같은 값이라 한 번만 만들어 재사용한다(매 프레임 할당 방지)
        private static readonly System.Drawing.Imaging.ImageAttributes _disabledImageAttr = CreateDisabledImageAttr();
        private static System.Drawing.Imaging.ImageAttributes CreateDisabledImageAttr()
        {
            var ia = new System.Drawing.Imaging.ImageAttributes();
            ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.4f });
            return ia;
        }

        /// <summary>비활성 상태의 아이콘을 반투명으로 그린다(툴바·메뉴 공용).</summary>
        public static void DrawImageDisabled(Graphics g, Image img, Rectangle r)
        {
            g.DrawImage(img, r, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, _disabledImageAttr);
        }

        /// <summary>두 색 사이를 t(0~1)로 보간한다. 호버 전환 애니메이션에 쓴다.</summary>
        public static Color Blend(Color from, Color to, float t)
        {
            if (t <= 0f) return from;
            if (t >= 1f) return to;

            return Color.FromArgb(
                (int)(from.A + (to.A - from.A) * t),
                (int)(from.R + (to.R - from.R) * t),
                (int)(from.G + (to.G - from.G) * t),
                (int)(from.B + (to.B - from.B) * t));
        }
    }
}
