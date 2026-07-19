using System.Drawing;
using System.Drawing.Drawing2D;
using AdvancedControls.Theming;

namespace AdvancedControls.Rendering
{
    /// <summary>
    /// 컨트롤의 바깥 테두리 한 겹(그림자 → 배경 → 테두리)을 그린다.
    /// 버튼·입력창·드롭다운이 모두 같은 모양을 갖도록 여기 한 곳에서 처리한다.
    /// </summary>
    public static class AdvFrameRenderer
    {
        /// <param name="bounds">테두리를 그릴 영역. 그림자는 이 영역 바깥으로 퍼진다.</param>
        /// <param name="borderWidth">테두리 두께. 컨트롤마다 테마를 덮어쓸 수 있어 따로 받는다.</param>
        /// <param name="fillEnd">비어 있으면 단색, 값이 있으면 fill에서 이 색으로 그라데이션.</param>
        /// <param name="glow">포커스 글로우. null이면 그리지 않는다.</param>
        /// <param name="elevation">떠 있는 느낌을 주는 그림자. 글로우보다 아래에 깔린다.</param>
        /// <param name="dash">테두리 선 모양(CSS border-style).</param>
        public static void Draw(Graphics g, Rectangle bounds, AdvTheme theme, AdvCorners corners,
                                int borderWidth, Color fill, Color fillEnd, Color border,
                                AdvShadow glow, AdvShadow elevation = null,
                                AdvBorderDash dash = AdvBorderDash.Solid)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 그림자를 먼저 깔고 그 위에 글로우를 올린다. 순서가 바뀌면 글로우가 묻힌다
            if (elevation != null && elevation.IsVisible)
                AdvGraphics.DrawShadow(g, bounds, corners, elevation);

            if (glow != null && glow.IsVisible)
                AdvGraphics.DrawShadow(g, bounds, corners, glow);

            var inner = AdvGraphics.Deflate(bounds, borderWidth);

            using (var path = AdvGraphics.CreateRoundedRect(inner, corners))
            {
                if (fill.A > 0)
                {
                    using (var brush = AdvGraphics.CreateFillBrush(inner, fill, fillEnd, theme.GradientAngle))
                        g.FillPath(brush, path);
                }

                if (border.A > 0 && borderWidth > 0)
                {
                    using (var pen = new Pen(border, borderWidth))
                    {
                        pen.DashStyle = ToDashStyle(dash);
                        g.DrawPath(pen, path);
                    }
                }
            }
        }

        private static DashStyle ToDashStyle(AdvBorderDash dash)
        {
            switch (dash)
            {
                case AdvBorderDash.Dash: return DashStyle.Dash;
                case AdvBorderDash.Dot: return DashStyle.Dot;
                case AdvBorderDash.DashDot: return DashStyle.DashDot;
                default: return DashStyle.Solid;
            }
        }
    }
}
