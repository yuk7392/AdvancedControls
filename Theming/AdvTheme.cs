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
        public Color FocusRing { get; set; }

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
                Elevation = new AdvShadow(Color.FromArgb(60, 0, 0, 0), 5, 0, 2)
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
                Elevation = new AdvShadow(Color.FromArgb(110, 0, 0, 0), 6, 0, 2)
            };
        }
    }
}
