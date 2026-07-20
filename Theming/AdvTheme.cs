using System.Drawing;

namespace AdvancedControls.Theming
{
    /// <summary>
    /// 라이브러리 전체가 공유하는 색상·형태·효과 값 모음.
    /// 컨트롤은 개별 색을 직접 들고 있지 않고 여기의 의미 단위(Accent, Surface 등)를 참조한다.
    /// </summary>
    public class AdvTheme
    {
        public string Name { get; set; }

        // 중립 배경 — 일반 컨트롤 면
        public Color Surface { get; set; }
        public Color SurfaceHover { get; set; }
        public Color SurfacePressed { get; set; }
        /// <summary>비어 있으면 단색, 값이 있으면 Surface에서 이 색으로 그라데이션.</summary>
        public Color SurfaceGradientEnd { get; set; }

        // 입력 컨트롤 면 — Surface와 다른 배경을 쓰는 테마가 많다
        public Color InputBackground { get; set; }
        public Color InputBackgroundDisabled { get; set; }
        public Color Placeholder { get; set; }

        // 테두리
        public Color Border { get; set; }
        public Color BorderHover { get; set; }
        public Color BorderFocus { get; set; }

        // 텍스트
        public Color Text { get; set; }
        public Color TextMuted { get; set; }
        public Color TextDisabled { get; set; }

        // 강조 — 주 동작 버튼, 선택 표시 등
        public Color Accent { get; set; }
        public Color AccentHover { get; set; }
        public Color AccentPressed { get; set; }
        public Color OnAccent { get; set; }
        /// <summary>비어 있으면 단색, 값이 있으면 Accent에서 이 색으로 그라데이션.</summary>
        public Color AccentGradientEnd { get; set; }

        public Color DisabledFill { get; set; }

        /// <summary>
        /// 포커스 표시용 단색 링 색. 컴팩트·원형·다중 항목 컨트롤(닫기 버튼, 슬라이더, 페이지네이션,
        /// 리스트 그룹, 브레드크럼, 아코디언 머리글)이 항목/요소에 딱 맞는 링을 그릴 때 쓴다.
        /// 테두리를 가진 단일 폼 컨트롤(버튼·입력류)은 대신 <see cref="FocusGlow"/> 글로우를 쓴다 — 의도된 이원 체계.
        /// </summary>
        public Color FocusRing { get; set; }

        // 컨텍스트 색 — Bootstrap의 상황별 색(success/danger/warning/info/secondary).
        // Primary는 위의 Accent 세트에서 합성하므로 별도 필드를 두지 않는다(ResolveContext 참조).
        public AdvContextPalette Secondary { get; set; }
        public AdvContextPalette Success { get; set; }
        public AdvContextPalette Danger { get; set; }
        public AdvContextPalette Warning { get; set; }
        public AdvContextPalette Info { get; set; }

        public AdvCorners Corners { get; set; }
        public int BorderWidth { get; set; }

        /// <summary>그라데이션 각도(도). 90이면 위에서 아래로.</summary>
        public float GradientAngle { get; set; }

        /// <summary>호버 등 상태 전환에 걸리는 시간(ms). 0이면 애니메이션을 끈다.</summary>
        public int TransitionDuration { get; set; }

        /// <summary>포커스 시 컨트롤 바깥에 퍼지는 빛. CSS의 focus ring glow에 해당한다.</summary>
        public AdvShadow FocusGlow { get; set; }

        /// <summary>드롭다운·팝업처럼 떠 있는 면에 쓰는 그림자.</summary>
        public AdvShadow Elevation { get; set; }

        /// <summary>
        /// 컨텍스트 색을 팔레트로 돌려준다. Default/Primary는 Accent 세트에서 합성하고,
        /// 나머지는 저장된 팔레트를 그대로 쓴다.
        /// </summary>
        public AdvContextPalette ResolveContext(AdvContextColor ctx)
        {
            switch (ctx)
            {
                case AdvContextColor.Secondary: return Secondary;
                case AdvContextColor.Success: return Success;
                case AdvContextColor.Danger: return Danger;
                case AdvContextColor.Warning: return Warning;
                case AdvContextColor.Info: return Info;
                default: // Default / Primary — Accent에서 합성. Subtle은 면색/글자색과 섞어 테마를 따른다.
                    return new AdvContextPalette
                    {
                        Solid = Accent,
                        SolidHover = AccentHover,
                        SolidPressed = AccentPressed,
                        OnSolid = OnAccent,
                        SubtleBg = AdvContextPalette.Lerp(Surface, Accent, 0.16f),
                        SubtleBorder = AdvContextPalette.Lerp(Surface, Accent, 0.40f),
                        SubtleText = AdvContextPalette.Lerp(Text, Accent, 0.55f)
                    };
            }
        }

        /// <summary>
        /// 이 테마의 복사본. 컨트롤별 색 재정의(<see cref="AdvColorOverrides"/>)를 입힐 때
        /// 공유 원본을 건드리지 않으려고 쓴다.
        /// MemberwiseClone이라 필드가 늘어도 자동으로 함께 복사된다. Color·AdvCorners는
        /// 값 형식이라 독립 복사되고, AdvShadow는 참조를 공유하지만 병합에서 바꾸지 않으므로 안전하다.
        /// </summary>
        public AdvTheme Clone()
        {
            return (AdvTheme)MemberwiseClone();
        }

        public static AdvTheme CreateLight()
        {
            return new AdvTheme
            {
                Name = "Light",
                Surface = ColorTranslator.FromHtml("#FFFFFF"),
                SurfaceHover = ColorTranslator.FromHtml("#F3F4F6"),
                SurfacePressed = ColorTranslator.FromHtml("#E5E7EB"),
                SurfaceGradientEnd = Color.Empty,
                InputBackground = ColorTranslator.FromHtml("#FFFFFF"),
                InputBackgroundDisabled = ColorTranslator.FromHtml("#F3F4F6"),
                Placeholder = ColorTranslator.FromHtml("#9CA3AF"),
                Border = ColorTranslator.FromHtml("#D1D5DB"),
                BorderHover = ColorTranslator.FromHtml("#9CA3AF"),
                BorderFocus = ColorTranslator.FromHtml("#2563EB"),
                Text = ColorTranslator.FromHtml("#111827"),
                TextMuted = ColorTranslator.FromHtml("#6B7280"),
                TextDisabled = ColorTranslator.FromHtml("#9CA3AF"),
                Accent = ColorTranslator.FromHtml("#2563EB"),
                AccentHover = ColorTranslator.FromHtml("#1D4ED8"),
                AccentPressed = ColorTranslator.FromHtml("#1E40AF"),
                OnAccent = ColorTranslator.FromHtml("#FFFFFF"),
                AccentGradientEnd = Color.Empty,
                DisabledFill = ColorTranslator.FromHtml("#F3F4F6"),
                FocusRing = ColorTranslator.FromHtml("#93C5FD"),
                Corners = new AdvCorners(4),
                BorderWidth = 1,
                GradientAngle = 90f,
                TransitionDuration = 120,
                FocusGlow = new AdvShadow(Color.FromArgb(120, ColorTranslator.FromHtml("#2563EB")), 3, 0, 0),
                Elevation = new AdvShadow(Color.FromArgb(60, 0, 0, 0), 5, 0, 2),
                Secondary = AdvContextPalette.Create(
                    ColorTranslator.FromHtml("#6C757D"), Color.White,
                    ColorTranslator.FromHtml("#F8F9FA"), ColorTranslator.FromHtml("#E9ECEF"), ColorTranslator.FromHtml("#41464B")),
                Success = AdvContextPalette.Create(
                    ColorTranslator.FromHtml("#198754"), Color.White,
                    ColorTranslator.FromHtml("#D1E7DD"), ColorTranslator.FromHtml("#BADBCC"), ColorTranslator.FromHtml("#0F5132")),
                Danger = AdvContextPalette.Create(
                    ColorTranslator.FromHtml("#DC3545"), Color.White,
                    ColorTranslator.FromHtml("#F8D7DA"), ColorTranslator.FromHtml("#F5C2C7"), ColorTranslator.FromHtml("#842029")),
                Warning = AdvContextPalette.Create(
                    ColorTranslator.FromHtml("#FFC107"), Color.Black,
                    ColorTranslator.FromHtml("#FFF3CD"), ColorTranslator.FromHtml("#FFECB5"), ColorTranslator.FromHtml("#664D03")),
                Info = AdvContextPalette.Create(
                    ColorTranslator.FromHtml("#0DCAF0"), Color.Black,
                    ColorTranslator.FromHtml("#CFF4FC"), ColorTranslator.FromHtml("#B6EFFB"), ColorTranslator.FromHtml("#055160"))
            };
        }

        public static AdvTheme CreateDark()
        {
            return new AdvTheme
            {
                Name = "Dark",
                Surface = ColorTranslator.FromHtml("#1F2937"),
                SurfaceHover = ColorTranslator.FromHtml("#374151"),
                SurfacePressed = ColorTranslator.FromHtml("#4B5563"),
                SurfaceGradientEnd = Color.Empty,
                InputBackground = ColorTranslator.FromHtml("#111827"),
                InputBackgroundDisabled = ColorTranslator.FromHtml("#374151"),
                Placeholder = ColorTranslator.FromHtml("#6B7280"),
                Border = ColorTranslator.FromHtml("#4B5563"),
                BorderHover = ColorTranslator.FromHtml("#6B7280"),
                BorderFocus = ColorTranslator.FromHtml("#3B82F6"),
                Text = ColorTranslator.FromHtml("#F9FAFB"),
                TextMuted = ColorTranslator.FromHtml("#9CA3AF"),
                TextDisabled = ColorTranslator.FromHtml("#6B7280"),
                Accent = ColorTranslator.FromHtml("#3B82F6"),
                AccentHover = ColorTranslator.FromHtml("#60A5FA"),
                AccentPressed = ColorTranslator.FromHtml("#2563EB"),
                OnAccent = ColorTranslator.FromHtml("#FFFFFF"),
                AccentGradientEnd = Color.Empty,
                DisabledFill = ColorTranslator.FromHtml("#374151"),
                FocusRing = ColorTranslator.FromHtml("#60A5FA"),
                Corners = new AdvCorners(4),
                BorderWidth = 1,
                GradientAngle = 90f,
                TransitionDuration = 120,
                FocusGlow = new AdvShadow(Color.FromArgb(140, ColorTranslator.FromHtml("#3B82F6")), 3, 0, 0),
                Elevation = new AdvShadow(Color.FromArgb(110, 0, 0, 0), 6, 0, 2),
                Secondary = AdvContextPalette.Create(
                    ColorTranslator.FromHtml("#6C757D"), Color.White,
                    ColorTranslator.FromHtml("#2B3035"), ColorTranslator.FromHtml("#373B3E"), ColorTranslator.FromHtml("#A7ACB1")),
                Success = AdvContextPalette.Create(
                    ColorTranslator.FromHtml("#198754"), Color.White,
                    ColorTranslator.FromHtml("#0A3622"), ColorTranslator.FromHtml("#0F5132"), ColorTranslator.FromHtml("#75B798")),
                Danger = AdvContextPalette.Create(
                    ColorTranslator.FromHtml("#DC3545"), Color.White,
                    ColorTranslator.FromHtml("#2C0B0E"), ColorTranslator.FromHtml("#842029"), ColorTranslator.FromHtml("#EA868F")),
                Warning = AdvContextPalette.Create(
                    ColorTranslator.FromHtml("#FFC107"), Color.Black,
                    ColorTranslator.FromHtml("#332701"), ColorTranslator.FromHtml("#664D03"), ColorTranslator.FromHtml("#FFDA6A")),
                Info = AdvContextPalette.Create(
                    ColorTranslator.FromHtml("#0DCAF0"), Color.Black,
                    ColorTranslator.FromHtml("#032830"), ColorTranslator.FromHtml("#055160"), ColorTranslator.FromHtml("#6EDFF6"))
            };
        }
    }
}
