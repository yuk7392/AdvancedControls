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
        /// Solid의 hover/pressed는 기본색을 어둡게 해서 자동으로 만든다. 나머지는 명시로 받는다.
        /// </summary>
        public static AdvContextPalette Create(Color solid, Color onSolid,
                                               Color subtleBg, Color subtleBorder, Color subtleText)
        {
            return new AdvContextPalette
            {
                Solid = solid,
                SolidHover = Shade(solid, 0.10f),
                SolidPressed = Shade(solid, 0.18f),
                OnSolid = onSolid,
                SubtleBg = subtleBg,
                SubtleBorder = subtleBorder,
                SubtleText = subtleText
            };
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
