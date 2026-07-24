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

        // 달력 주말 글자색 (일요일=빨강, 토요일=파랑)
        public Color SundayText { get; set; }
        public Color SaturdayText { get; set; }

        // 강조 — 주 동작 버튼, 선택 표시 등
        public Color Accent { get; set; }
        public Color AccentHover { get; set; }
        public Color AccentPressed { get; set; }
        public Color OnAccent { get; set; }
        /// <summary>비어 있으면 단색, 값이 있으면 Accent에서 이 색으로 그라데이션.</summary>
        public Color AccentGradientEnd { get; set; }

        public Color DisabledFill { get; set; }

        // 의미색 — 성공·경고·오류. 검증 상태(AdvTextBox)·다이얼로그 아이콘 등에 쓰며 테마별로 대비를 맞춘다.
        public Color Success { get; set; }
        public Color Warning { get; set; }
        public Color Error { get; set; }

        /// <summary>
        /// 포커스 표시용 단색 링 색. 컴팩트·원형·다중 항목 컨트롤(닫기 버튼, 슬라이더, 페이지네이션,
        /// 리스트 그룹, 브레드크럼, 아코디언 머리글)이 항목/요소에 딱 맞는 링을 그릴 때 쓴다.
        /// 테두리를 가진 단일 폼 컨트롤(버튼·입력류)은 대신 <see cref="FocusGlow"/> 글로우를 쓴다 — 의도된 이원 체계.
        /// </summary>
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

        /// <summary>
        /// 테마 강조색(Accent 세트)에서 합성한 컨텍스트 팔레트.
        /// 컨트롤이 색을 따로 지정하지 않았을 때(Context 비움) 쓰는 기본 팔레트다.
        /// Subtle 계열은 면색·글자색과 섞어 테마를 따른다.
        /// </summary>
        public AdvContextPalette AccentContext
        {
            get
            {
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
                SundayText = ColorTranslator.FromHtml("#DC2626"),
                SaturdayText = ColorTranslator.FromHtml("#2563EB"),
                Accent = ColorTranslator.FromHtml("#2563EB"),
                AccentHover = ColorTranslator.FromHtml("#1D4ED8"),
                AccentPressed = ColorTranslator.FromHtml("#1E40AF"),
                OnAccent = ColorTranslator.FromHtml("#FFFFFF"),
                AccentGradientEnd = Color.Empty,
                DisabledFill = ColorTranslator.FromHtml("#F3F4F6"),
                Success = ColorTranslator.FromHtml("#16A34A"),
                Warning = ColorTranslator.FromHtml("#D97706"),
                Error = ColorTranslator.FromHtml("#DC2626"),
                FocusRing = ColorTranslator.FromHtml("#93C5FD"),
                Corners = new AdvCorners(4),
                BorderWidth = 1,
                GradientAngle = 90f,
                TransitionDuration = 120,
                FocusGlow = new AdvShadow(Color.FromArgb(200, ColorTranslator.FromHtml("#2563EB")), 3, 0, 0),
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
                SundayText = ColorTranslator.FromHtml("#F87171"),
                SaturdayText = ColorTranslator.FromHtml("#60A5FA"),
                Accent = ColorTranslator.FromHtml("#3B82F6"),
                AccentHover = ColorTranslator.FromHtml("#60A5FA"),
                AccentPressed = ColorTranslator.FromHtml("#2563EB"),
                OnAccent = ColorTranslator.FromHtml("#FFFFFF"),
                AccentGradientEnd = Color.Empty,
                DisabledFill = ColorTranslator.FromHtml("#374151"),
                Success = ColorTranslator.FromHtml("#22C55E"),
                Warning = ColorTranslator.FromHtml("#F59E0B"),
                Error = ColorTranslator.FromHtml("#EF4444"),
                FocusRing = ColorTranslator.FromHtml("#60A5FA"),
                Corners = new AdvCorners(4),
                BorderWidth = 1,
                GradientAngle = 90f,
                TransitionDuration = 120,
                FocusGlow = new AdvShadow(Color.FromArgb(210, ColorTranslator.FromHtml("#3B82F6")), 3, 0, 0),
                Elevation = new AdvShadow(Color.FromArgb(110, 0, 0, 0), 6, 0, 2)
            };
        }
    }
}
