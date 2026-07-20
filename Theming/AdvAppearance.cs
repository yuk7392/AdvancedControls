using System;
using System.ComponentModel;
using System.Drawing;
using AdvancedControls.Animation;

namespace AdvancedControls.Theming
{
    /// <summary>CSS의 border-style에 대응한다.</summary>
    public enum AdvBorderDash
    {
        Solid,
        Dash,
        Dot,
        DashDot
    }

    /// <summary>컨트롤이 어떤 테마를 따를지.</summary>
    public enum AdvThemeMode
    {
        /// <summary>전역 테마(<see cref="AdvThemeManager.Current"/>)를 따른다.</summary>
        Inherit,
        Light,
        Dark
    }

    /// <summary>
    /// 컨트롤 하나의 모양 설정. 값이 -1이면 테마 값을 따른다는 뜻이다.
    /// 컨트롤마다 속성을 여러 개 늘리는 대신 이 객체 하나를 펼쳐 쓰게 한다.
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class AdvAppearance
    {
        private AdvThemeMode _themeMode = AdvThemeMode.Inherit;
        private AdvCorners _corners = new AdvCorners(-1);
        private int _borderWidth = -1;
        private int _transitionDuration = -1;
        private AdvEasing _easing = AdvEasing.Smooth;
        private float _gradientAngle = -1f;
        private bool _showFocusGlow = true;
        private bool _elevated;
        private AdvBorderDash _borderDash = AdvBorderDash.Solid;

        // 그림자·글로우 커스터마이즈. Custom을 켜야 적용되고, 끄면 테마 값을 따른다.
        private readonly AdvShadowSettings _shadow = new AdvShadowSettings(Color.FromArgb(60, 0, 0, 0), 6, 0, 3);
        private readonly AdvShadowSettings _glow = new AdvShadowSettings(Color.FromArgb(120, 37, 99, 235), 4, 0, 0);

        /// <summary>값이 바뀌어 다시 그려야 할 때 발생한다.</summary>
        internal event EventHandler Changed;

        /// <summary>값이 바뀌어 자식 배치까지 다시 계산해야 할 때 발생한다.</summary>
        internal event EventHandler LayoutChanged;

        public AdvAppearance()
        {
            // 번짐·오프셋이 바뀌면 여백(크기)도 달라지므로 레이아웃 갱신으로 취급한다.
            _shadow.Changed += (s, e) => RaiseLayout();
            _glow.Changed += (s, e) => RaiseLayout();
        }

        [DefaultValue(AdvThemeMode.Inherit)]
        [Description("이 컨트롤이 따를 테마입니다. Inherit이면 전역 테마를 따릅니다.")]
        public AdvThemeMode ThemeMode
        {
            get { return _themeMode; }
            set
            {
                if (_themeMode == value) return;
                _themeMode = value;
                RaiseLayout();
            }
        }

        [Description("모서리별 반경입니다. -1이면 테마 값을 따릅니다. 0이면 각진 모서리가 됩니다.")]
        public AdvCorners Corners
        {
            get { return _corners; }
            set
            {
                if (_corners == value) return;
                _corners = value;
                RaiseChanged();
            }
        }

        /// <summary>네 모서리를 한꺼번에 맞추는 지름길. 실제 값은 <see cref="Corners"/>에 저장된다.</summary>
        [DefaultValue(-1)]
        [RefreshProperties(RefreshProperties.All)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Description("네 모서리를 한꺼번에 맞추는 반경입니다. -1이면 테마 값을 따릅니다. 모서리마다 다르면 -1로 보입니다.")]
        public int Radius
        {
            get
            {
                var c = _corners;
                return c.TopLeft == c.TopRight && c.TopRight == c.BottomRight && c.BottomRight == c.BottomLeft
                    ? c.TopLeft : -1;
            }
            set
            {
                if (value < -1) value = -1;
                Corners = new AdvCorners(value);
            }
        }

        [DefaultValue(-1)]
        [Description("테두리 두께입니다. -1이면 테마 값을 따릅니다. 0이면 테두리를 그리지 않습니다.")]
        public int BorderWidth
        {
            get { return _borderWidth; }
            set
            {
                if (value < -1) value = -1;
                if (_borderWidth == value) return;
                _borderWidth = value;
                RaiseLayout();
            }
        }

        [DefaultValue(-1)]
        [Description("호버·포커스 전환에 걸리는 시간(ms)입니다. -1이면 테마 값, 0이면 애니메이션 없음입니다.")]
        public int TransitionDuration
        {
            get { return _transitionDuration; }
            set
            {
                if (value < -1) value = -1;
                if (_transitionDuration == value) return;
                _transitionDuration = value;
                RaiseChanged();
            }
        }

        [DefaultValue(AdvEasing.Smooth)]
        [Description("호버·포커스 전환의 가감속 곡선입니다. CSS transition-timing-function에 대응합니다.")]
        public AdvEasing Easing
        {
            get { return _easing; }
            set
            {
                if (_easing == value) return;
                _easing = value;
                RaiseChanged();
            }
        }

        [DefaultValue(-1f)]
        [Description("채움 그라데이션의 각도(도)입니다. -1이면 테마 값을 따릅니다. 90이면 위에서 아래로 흐릅니다. Palette에서 그라데이션 끝색을 지정했을 때 보입니다.")]
        public float GradientAngle
        {
            get { return _gradientAngle; }
            set
            {
                if (_gradientAngle == value) return;
                _gradientAngle = value;
                RaiseChanged();
            }
        }

        [DefaultValue(true)]
        [Description("포커스를 받았을 때 바깥으로 퍼지는 빛을 그릴지 여부입니다.")]
        public bool ShowFocusGlow
        {
            get { return _showFocusGlow; }
            set
            {
                if (_showFocusGlow == value) return;
                _showFocusGlow = value;
                // 글로우 여백이 사라지면 내용 영역 크기가 달라진다
                RaiseLayout();
            }
        }

        [DefaultValue(false)]
        [Description("떠 있는 카드처럼 그림자를 그릴지 여부입니다. 그림자만큼 각 변에 여백을 확보합니다.")]
        public bool Elevated
        {
            get { return _elevated; }
            set
            {
                if (_elevated == value) return;
                _elevated = value;
                // 그림자 여백이 생기거나 사라지면 내용 영역 크기가 달라진다
                RaiseLayout();
            }
        }

        [DefaultValue(AdvBorderDash.Solid)]
        [Description("테두리 선 모양입니다.")]
        public AdvBorderDash BorderDash
        {
            get { return _borderDash; }
            set
            {
                if (_borderDash == value) return;
                _borderDash = value;
                RaiseChanged();
            }
        }

        /// <summary>그림자를 테마 대신 직접 지정한다. Custom을 켜야 적용된다.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Description("그림자 색·번짐·오프셋을 직접 지정합니다. 펼쳐서 Custom을 켜면 Elevated일 때 적용됩니다.")]
        public AdvShadowSettings Shadow
        {
            get { return _shadow; }
        }

        /// <summary>포커스 글로우를 테마 대신 직접 지정한다. Custom을 켜야 적용된다.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Description("포커스 글로우 색·번짐·오프셋을 직접 지정합니다. 펼쳐서 Custom을 켜면 포커스 시 적용됩니다.")]
        public AdvShadowSettings Glow
        {
            get { return _glow; }
        }

        /// <summary>그림자가 잘리지 않도록 각 변에 비워 둘 여백.</summary>
        public int ResolveShadowPadding(AdvTheme theme)
        {
            if (!_elevated) return 0;

            var s = ResolveElevation(theme);
            return s != null && s.IsVisible ? s.Blur + Math.Abs(s.OffsetY) : 0;
        }

        /// <summary>Custom이 켜져 있으면 컨트롤 지정 그림자를, 아니면 테마 그림자를 돌려준다.</summary>
        public AdvShadow ResolveElevation(AdvTheme theme)
        {
            return _shadow.Resolve(theme.Elevation);
        }

        /// <summary>Custom이 켜져 있으면 컨트롤 지정 글로우를, 아니면 테마 글로우를 돌려준다.</summary>
        public AdvShadow ResolveGlow(AdvTheme theme)
        {
            return _glow.Resolve(theme.FocusGlow);
        }

        /// <summary>그라데이션 각도. -1(미지정)이면 테마 값을 따른다.</summary>
        public float ResolveGradientAngle(AdvTheme theme)
        {
            return _gradientAngle < 0f ? theme.GradientAngle : _gradientAngle;
        }

        /// <summary>디자이너가 기본값일 때 코드를 생성하지 않도록 알려준다.</summary>
        public bool ShouldSerializeCorners()
        {
            return !_corners.FollowsTheme;
        }

        public void ResetCorners()
        {
            Corners = new AdvCorners(-1);
        }

        /// <summary>테마를 따르는 자리를 실제 값으로 채운 최종 모서리 반경.</summary>
        public AdvCorners ResolveCorners(AdvTheme theme)
        {
            return _corners.ResolveAgainst(theme.Corners);
        }

        public int ResolveBorderWidth(AdvTheme theme)
        {
            return _borderWidth < 0 ? theme.BorderWidth : _borderWidth;
        }

        public int ResolveTransitionDuration(AdvTheme theme)
        {
            return _transitionDuration < 0 ? theme.TransitionDuration : _transitionDuration;
        }

        /// <summary>ThemeMode에 해당하는 테마. Inherit이면 null을 돌려준다.</summary>
        public AdvTheme ResolveTheme()
        {
            switch (_themeMode)
            {
                case AdvThemeMode.Light: return AdvThemeManager.Light;
                case AdvThemeMode.Dark: return AdvThemeManager.Dark;
                default: return null;
            }
        }

        private void RaiseChanged()
        {
            var handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void RaiseLayout()
        {
            var handler = LayoutChanged;
            if (handler != null) handler(this, EventArgs.Empty);
            RaiseChanged();
        }
    }
}
