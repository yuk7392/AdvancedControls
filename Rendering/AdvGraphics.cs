using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return path;

            // 반경이 변의 절반을 넘으면 호가 서로 겹쳐 도형이 뒤집힌다
            int limit = Math.Min(bounds.Width, bounds.Height) / 2;
            var c = corners.Clamp(0, limit);

            if (c.IsZero)
            {
                path.AddRectangle(bounds);
                return path;
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
            return path;
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

        /// <summary>셰브런이 향하는 쪽.</summary>
        public enum ChevronDirection { Up, Down, Left, Right }

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

                using (var path = CreateRoundedRect(layer, layerCorners))
                using (var brush = new SolidBrush(Color.FromArgb(alpha, shadow.Color)))
                    g.FillPath(brush, path);
            }
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
