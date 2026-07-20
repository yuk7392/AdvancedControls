using System;
using System.Drawing;

namespace AdvancedControls.Theming
{
    /// <summary>
    /// 컨텍스트 색 하나의 색 묶음. 두 갈래를 담는다.
    /// - Solid: 색으로 가득 채우는 형태(버튼·배지·솔리드 알림). 위에 <see cref="OnSolid"/> 글자를 올린다.
    /// - Subtle: 옅은 배경 + 어울리는 테두리·진한 글자(기본 알림·리스트 항목 변형).
    /// struct(값 형식)이므로 <see cref="AdvTheme.Clone"/>의 MemberwiseClone에서 독립 복사된다.
    /// </summary>
    public struct AdvContextPalette
    {
        public Color Solid;
        public Color SolidHover;
        public Color SolidPressed;
        public Color OnSolid;

        public Color SubtleBg;
        public Color SubtleBorder;
        public Color SubtleText;

        /// <summary>
        /// 컨트롤이 지정한 색에서 팔레트를 만든다. 색이 비어 있으면(Color.Empty)
        /// 테마 강조색(<see cref="AdvTheme.AccentContext"/>)을 따르고, 지정돼 있으면 그 색에서 파생한다.
        /// </summary>
        public static AdvContextPalette Resolve(Color context, AdvTheme theme)
        {
            return context.IsEmpty ? theme.AccentContext : FromColor(context, theme);
        }

        /// <summary>
        /// 사용자가 고른 단색 하나에서 팔레트 전체를 유도한다.
        /// hover/pressed는 단색을 어둡게, 글자색은 대비가 큰 검정/흰색, subtle 계열은
        /// 테마의 면·글자색과 섞어 만든다. 컨텍스트 색을 하드코딩하지 않고 이 함수로 파생한다.
        /// </summary>
        public static AdvContextPalette FromColor(Color solid, AdvTheme theme)
        {
            return new AdvContextPalette
            {
                Solid = solid,
                SolidHover = Shade(solid, 0.10f),
                SolidPressed = Shade(solid, 0.18f),
                OnSolid = ReadableOn(solid),
                SubtleBg = Lerp(theme.Surface, solid, 0.16f),
                SubtleBorder = Lerp(theme.Surface, solid, 0.40f),
                SubtleText = Lerp(theme.Text, solid, 0.55f)
            };
        }

        /// <summary>
        /// 배경색 위에 얹을 글자색을 고른다. 밝은 색에는 검정, 어두운 색에는 흰색.
        /// 상대휘도 임계값으로 가르는 이유는, 순수 최대대비를 쓰면 초록·빨강처럼 중간 밝기
        /// 색에 검정이 골라져(대비는 근소하게 높지만) 관례와 어긋나 어색해지기 때문이다.
        /// 임계값 0.4는 노랑·하늘색(검정)과 초록·빨강·회색(흰색)을 여유 있게 갈라 준다.
        /// </summary>
        internal static Color ReadableOn(Color bg)
        {
            return RelativeLuminance(bg) < 0.4 ? Color.White : Color.Black;
        }

        private static double RelativeLuminance(Color c)
        {
            return 0.2126 * LinearChannel(c.R)
                 + 0.7152 * LinearChannel(c.G)
                 + 0.0722 * LinearChannel(c.B);
        }

        private static double LinearChannel(int v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        /// <summary>색을 검정 쪽으로 amt(0~1)만큼 어둡게 한다.</summary>
        internal static Color Shade(Color c, float amt)
        {
            return Color.FromArgb(c.A,
                (int)(c.R * (1f - amt)),
                (int)(c.G * (1f - amt)),
                (int)(c.B * (1f - amt)));
        }

        /// <summary>from에서 to로 t(0~1)만큼 보간한다.</summary>
        internal static Color Lerp(Color from, Color to, float t)
        {
            return Color.FromArgb(
                (int)(from.A + (to.A - from.A) * t),
                (int)(from.R + (to.R - from.R) * t),
                (int)(from.G + (to.G - from.G) * t),
                (int)(from.B + (to.B - from.B) * t));
        }
    }
}
