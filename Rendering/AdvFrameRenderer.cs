using System;
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
        // 프레임은 매 그리기(호버·포커스 전환 중 최대 66fps)마다 다시 그려진다.
        // 경로·펜·단색 브러시를 프레임마다 새로 만들지 않고 스레드마다 하나씩 재사용한다.
        // WinForms 그리기는 UI 스레드 전용이라 [ThreadStatic]이면 충돌이 없다.
        [ThreadStatic] private static GraphicsPath _path;
        [ThreadStatic] private static Pen _pen;
        [ThreadStatic] private static SolidBrush _fill;

        /// <param name="bounds">테두리를 그릴 영역. 그림자는 이 영역 바깥으로 퍼진다.</param>
        /// <param name="borderWidth">테두리 두께. 컨트롤마다 테마를 덮어쓸 수 있어 따로 받는다.</param>
        /// <param name="fillEnd">비어 있으면 단색, 값이 있으면 fill에서 이 색으로 그라데이션.</param>
        /// <param name="glow">포커스 글로우. null이면 그리지 않는다.</param>
        /// <param name="elevation">떠 있는 느낌을 주는 그림자. 글로우보다 아래에 깔린다.</param>
        /// <param name="dash">테두리 선 모양(CSS border-style).</param>
        /// <param name="gradientAngle">채움 그라데이션 각도. NaN이면 테마 값을 쓴다.</param>
        public static void Draw(Graphics g, Rectangle bounds, AdvTheme theme, AdvCorners corners,
                                int borderWidth, Color fill, Color fillEnd, Color border,
                                AdvShadow glow, AdvShadow elevation = null,
                                AdvBorderDash dash = AdvBorderDash.Solid,
                                float gradientAngle = float.NaN)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 그림자를 먼저 깔고 그 위에 글로우를 올린다. 순서가 바뀌면 글로우가 묻힌다
            if (elevation != null && elevation.IsVisible)
                AdvGraphics.DrawShadow(g, bounds, corners, elevation);

            if (glow != null && glow.IsVisible)
                AdvGraphics.DrawShadow(g, bounds, corners, glow);

            bool hasFill = fill.A > 0;
            bool hasBorder = border.A > 0 && borderWidth > 0;
            var path = _path ?? (_path = new GraphicsPath());

            // 실선 테두리 + 채움이 함께면 펜 스트로크 대신 '링(두 채움의 차집합)'으로 그린다.
            // 좌상단 기준 경로를 중심 정렬 펜으로 스트로크하면 GDI+ AA의 좌상단 편향 때문에
            // 우·하단 코너의 커버리지가 줄어 테두리가 상하좌우로 비대칭이 된다(우·하단이 흐려짐).
            // 바깥을 테두리색으로, 안쪽을 카드색으로 채우면 두 채움이 같은 방식으로 래스터돼
            // 네 변·네 코너가 균일하다. PixelOffsetMode.Half로 정수 경로 가장자리를 픽셀에
            // 정렬해 직선 변까지 또렷하게 남긴다. (점선은 링으로 표현 못하므로 아래 스트로크로 뺀다.)
            if (hasFill && hasBorder && dash == AdvBorderDash.Solid)
            {
                var prevOffset = g.PixelOffsetMode;
                g.PixelOffsetMode = PixelOffsetMode.Half;

                path.Reset();
                AdvGraphics.BuildRoundedRect(path, bounds, corners);
                var bb = _fill ?? (_fill = new SolidBrush(Color.Black));
                bb.Color = border;
                g.FillPath(bb, path);                       // 바깥 = 테두리색

                var innerRect = Rectangle.Inflate(bounds, -borderWidth, -borderWidth);
                if (innerRect.Width > 0 && innerRect.Height > 0)
                {
                    var innerCorners = new AdvCorners(
                        corners.TopLeft - borderWidth, corners.TopRight - borderWidth,
                        corners.BottomRight - borderWidth, corners.BottomLeft - borderWidth);
                    path.Reset();
                    AdvGraphics.BuildRoundedRect(path, innerRect, innerCorners);
                    FillShape(g, path, innerRect, theme, fill, fillEnd, gradientAngle); // 안쪽 = 카드색
                }

                g.PixelOffsetMode = prevOffset;
                return;
            }

            // 그 외(채움만·테두리만·점선): 기존 채움(inner) + 스트로크(inner) 경로.
            var inner = AdvGraphics.Deflate(bounds, borderWidth);
            path.Reset();
            AdvGraphics.BuildRoundedRect(path, inner, corners);

            if (hasFill)
                FillShape(g, path, inner, theme, fill, fillEnd, gradientAngle);

            if (hasBorder)
            {
                var pen = _pen ?? (_pen = new Pen(Color.Black));
                pen.Color = border;
                pen.Width = borderWidth;
                pen.DashStyle = ToDashStyle(dash);
                g.DrawPath(pen, path);
            }
        }

        // 경로를 단색 또는 그라데이션으로 채운다. 단색은 재사용 브러시로 색만 바꾸고,
        // 그라데이션은 영역에 종속돼 매번 새로 만든다.
        private static void FillShape(Graphics g, GraphicsPath path, Rectangle rect, AdvTheme theme,
                                      Color fill, Color fillEnd, float gradientAngle)
        {
            if (fillEnd.IsEmpty || fillEnd == fill)
            {
                var fb = _fill ?? (_fill = new SolidBrush(Color.Black));
                fb.Color = fill;
                g.FillPath(fb, path);
            }
            else
            {
                float angle = float.IsNaN(gradientAngle) ? theme.GradientAngle : gradientAngle;
                using (var brush = AdvGraphics.CreateFillBrush(rect, fill, fillEnd, angle))
                    g.FillPath(brush, path);
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
