using System;
using System.ComponentModel;
using System.Drawing;

namespace AdvancedControls.Theming
{
    /// <summary>
    /// 컨트롤 하나에만 적용하는 색 재정의.
    /// 각 색은 기본이 <see cref="Color.Empty"/>이고, 그 상태면 테마 값을 그대로 따른다.
    /// 값을 지정한 색만 <see cref="Apply"/>에서 테마 위에 덮어써지므로,
    /// 전역 테마나 다른 컨트롤을 건드리지 않고 이 컨트롤의 색만 바꿀 수 있다.
    /// </summary>
    /// <remarks>
    /// 컨트롤은 원래 색을 전부 <c>EffectiveTheme</c>에서 가져오므로, 여기서 만든
    /// 병합 테마를 <c>EffectiveTheme</c>로 흘려보내면 모든 컨트롤이 재정의를 그대로 존중한다.
    /// 개별 색을 컨트롤마다 속성으로 늘리는 대신 이 객체 하나를 펼쳐 쓰게 한다.
    /// 어떤 색이 화면 어디에 쓰이는지는 컨트롤마다 다르다(예: Filled 버튼의 면은 Accent,
    /// Outline 버튼의 면은 Surface). 해당 컨트롤이 쓰지 않는 색을 바꾸면 아무 효과가 없다.
    /// </remarks>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class AdvColorOverrides
    {
        private Color _surface = Color.Empty;
        private Color _surfaceHover = Color.Empty;
        private Color _surfacePressed = Color.Empty;
        private Color _surfaceGradientEnd = Color.Empty;

        private Color _inputBackground = Color.Empty;
        private Color _inputBackgroundDisabled = Color.Empty;
        private Color _placeholder = Color.Empty;

        private Color _border = Color.Empty;
        private Color _borderHover = Color.Empty;
        private Color _borderFocus = Color.Empty;

        private Color _text = Color.Empty;
        private Color _textMuted = Color.Empty;
        private Color _textDisabled = Color.Empty;

        private Color _accent = Color.Empty;
        private Color _accentHover = Color.Empty;
        private Color _accentPressed = Color.Empty;
        private Color _accentGradientEnd = Color.Empty;
        private Color _onAccent = Color.Empty;

        private Color _disabledFill = Color.Empty;
        private Color _focusRing = Color.Empty;

        /// <summary>어떤 색이든 바뀌어 다시 그려야 할 때 발생한다.</summary>
        internal event EventHandler Changed;

        #region 면(Surface) — 일반 컨트롤 배경, Outline/Ghost 버튼 면

        [Description("일반 컨트롤 면의 배경색입니다. 비워 두면 테마 값을 따릅니다.")]
        public Color Surface
        {
            get { return _surface; }
            set { Set(ref _surface, value); }
        }
        public bool ShouldSerializeSurface() { return !_surface.IsEmpty; }
        public void ResetSurface() { Surface = Color.Empty; }

        [Description("면에 마우스를 올렸을 때의 배경색입니다. 비워 두면 테마 값을 따릅니다.")]
        public Color SurfaceHover
        {
            get { return _surfaceHover; }
            set { Set(ref _surfaceHover, value); }
        }
        public bool ShouldSerializeSurfaceHover() { return !_surfaceHover.IsEmpty; }
        public void ResetSurfaceHover() { SurfaceHover = Color.Empty; }

        [Description("면을 눌렀을 때의 배경색입니다. 비워 두면 테마 값을 따릅니다.")]
        public Color SurfacePressed
        {
            get { return _surfacePressed; }
            set { Set(ref _surfacePressed, value); }
        }
        public bool ShouldSerializeSurfacePressed() { return !_surfacePressed.IsEmpty; }
        public void ResetSurfacePressed() { SurfacePressed = Color.Empty; }

        [Description("면 그라데이션의 끝 색입니다. 지정하면 Surface에서 이 색으로 그라데이션됩니다.")]
        public Color SurfaceGradientEnd
        {
            get { return _surfaceGradientEnd; }
            set { Set(ref _surfaceGradientEnd, value); }
        }
        public bool ShouldSerializeSurfaceGradientEnd() { return !_surfaceGradientEnd.IsEmpty; }
        public void ResetSurfaceGradientEnd() { SurfaceGradientEnd = Color.Empty; }

        #endregion

        #region 입력(Input) — 텍스트·콤보·숫자·날짜 입력의 면

        [Description("입력 컨트롤의 배경색입니다. 비워 두면 테마 값을 따릅니다.")]
        public Color InputBackground
        {
            get { return _inputBackground; }
            set { Set(ref _inputBackground, value); }
        }
        public bool ShouldSerializeInputBackground() { return !_inputBackground.IsEmpty; }
        public void ResetInputBackground() { InputBackground = Color.Empty; }

        [Description("비활성 입력 컨트롤의 배경색입니다. 비워 두면 테마 값을 따릅니다.")]
        public Color InputBackgroundDisabled
        {
            get { return _inputBackgroundDisabled; }
            set { Set(ref _inputBackgroundDisabled, value); }
        }
        public bool ShouldSerializeInputBackgroundDisabled() { return !_inputBackgroundDisabled.IsEmpty; }
        public void ResetInputBackgroundDisabled() { InputBackgroundDisabled = Color.Empty; }

        [Description("플레이스홀더(안내) 글자색입니다. 비워 두면 테마 값을 따릅니다.")]
        public Color Placeholder
        {
            get { return _placeholder; }
            set { Set(ref _placeholder, value); }
        }
        public bool ShouldSerializePlaceholder() { return !_placeholder.IsEmpty; }
        public void ResetPlaceholder() { Placeholder = Color.Empty; }

        #endregion

        #region 테두리(Border)

        [Description("테두리 색입니다. 비워 두면 테마 값을 따릅니다.")]
        public Color Border
        {
            get { return _border; }
            set { Set(ref _border, value); }
        }
        public bool ShouldSerializeBorder() { return !_border.IsEmpty; }
        public void ResetBorder() { Border = Color.Empty; }

        [Description("마우스를 올렸을 때의 테두리 색입니다. 비워 두면 테마 값을 따릅니다.")]
        public Color BorderHover
        {
            get { return _borderHover; }
            set { Set(ref _borderHover, value); }
        }
        public bool ShouldSerializeBorderHover() { return !_borderHover.IsEmpty; }
        public void ResetBorderHover() { BorderHover = Color.Empty; }

        [Description("포커스를 받았을 때의 테두리 색입니다. 비워 두면 테마 값을 따릅니다.")]
        public Color BorderFocus
        {
            get { return _borderFocus; }
            set { Set(ref _borderFocus, value); }
        }
        public bool ShouldSerializeBorderFocus() { return !_borderFocus.IsEmpty; }
        public void ResetBorderFocus() { BorderFocus = Color.Empty; }

        #endregion

        #region 글자(Text)

        [Description("기본 글자색입니다. 비워 두면 테마 값을 따릅니다.")]
        public Color Text
        {
            get { return _text; }
            set { Set(ref _text, value); }
        }
        public bool ShouldSerializeText() { return !_text.IsEmpty; }
        public void ResetText() { Text = Color.Empty; }

        [Description("보조 글자색(설명·단위 등)입니다. 비워 두면 테마 값을 따릅니다.")]
        public Color TextMuted
        {
            get { return _textMuted; }
            set { Set(ref _textMuted, value); }
        }
        public bool ShouldSerializeTextMuted() { return !_textMuted.IsEmpty; }
        public void ResetTextMuted() { TextMuted = Color.Empty; }

        [Description("비활성 상태의 글자색입니다. 비워 두면 테마 값을 따릅니다.")]
        public Color TextDisabled
        {
            get { return _textDisabled; }
            set { Set(ref _textDisabled, value); }
        }
        public bool ShouldSerializeTextDisabled() { return !_textDisabled.IsEmpty; }
        public void ResetTextDisabled() { TextDisabled = Color.Empty; }

        #endregion

        #region 강조(Accent) — 주 동작 버튼 면, 체크·선택 표시

        [Description("강조색입니다. Filled 버튼 면, 체크·선택 표시 등에 쓰입니다. 비워 두면 테마 값을 따릅니다.")]
        public Color Accent
        {
            get { return _accent; }
            set { Set(ref _accent, value); }
        }
        public bool ShouldSerializeAccent() { return !_accent.IsEmpty; }
        public void ResetAccent() { Accent = Color.Empty; }

        [Description("마우스를 올렸을 때의 강조색입니다. 비워 두면 테마 값을 따릅니다.")]
        public Color AccentHover
        {
            get { return _accentHover; }
            set { Set(ref _accentHover, value); }
        }
        public bool ShouldSerializeAccentHover() { return !_accentHover.IsEmpty; }
        public void ResetAccentHover() { AccentHover = Color.Empty; }

        [Description("눌렀을 때의 강조색입니다. 비워 두면 테마 값을 따릅니다.")]
        public Color AccentPressed
        {
            get { return _accentPressed; }
            set { Set(ref _accentPressed, value); }
        }
        public bool ShouldSerializeAccentPressed() { return !_accentPressed.IsEmpty; }
        public void ResetAccentPressed() { AccentPressed = Color.Empty; }

        [Description("강조색 그라데이션의 끝 색입니다. 지정하면 Accent에서 이 색으로 그라데이션됩니다.")]
        public Color AccentGradientEnd
        {
            get { return _accentGradientEnd; }
            set { Set(ref _accentGradientEnd, value); }
        }
        public bool ShouldSerializeAccentGradientEnd() { return !_accentGradientEnd.IsEmpty; }
        public void ResetAccentGradientEnd() { AccentGradientEnd = Color.Empty; }

        [Description("강조색 면 위에 올리는 글자·표시 색입니다. 비워 두면 테마 값을 따릅니다.")]
        public Color OnAccent
        {
            get { return _onAccent; }
            set { Set(ref _onAccent, value); }
        }
        public bool ShouldSerializeOnAccent() { return !_onAccent.IsEmpty; }
        public void ResetOnAccent() { OnAccent = Color.Empty; }

        #endregion

        #region 기타

        [Description("비활성 컨트롤의 채움색입니다. 비워 두면 테마 값을 따릅니다.")]
        public Color DisabledFill
        {
            get { return _disabledFill; }
            set { Set(ref _disabledFill, value); }
        }
        public bool ShouldSerializeDisabledFill() { return !_disabledFill.IsEmpty; }
        public void ResetDisabledFill() { DisabledFill = Color.Empty; }

        [Description("포커스 링(테두리 강조) 색입니다. 비워 두면 테마 값을 따릅니다.")]
        public Color FocusRing
        {
            get { return _focusRing; }
            set { Set(ref _focusRing, value); }
        }
        public bool ShouldSerializeFocusRing() { return !_focusRing.IsEmpty; }
        public void ResetFocusRing() { FocusRing = Color.Empty; }

        #endregion

        /// <summary>지정된 색이 하나라도 있는지. 없으면 테마를 그대로 쓰면 되므로 병합을 건너뛴다.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool HasAny
        {
            get
            {
                return !_surface.IsEmpty || !_surfaceHover.IsEmpty || !_surfacePressed.IsEmpty
                    || !_surfaceGradientEnd.IsEmpty
                    || !_inputBackground.IsEmpty || !_inputBackgroundDisabled.IsEmpty || !_placeholder.IsEmpty
                    || !_border.IsEmpty || !_borderHover.IsEmpty || !_borderFocus.IsEmpty
                    || !_text.IsEmpty || !_textMuted.IsEmpty || !_textDisabled.IsEmpty
                    || !_accent.IsEmpty || !_accentHover.IsEmpty || !_accentPressed.IsEmpty
                    || !_accentGradientEnd.IsEmpty || !_onAccent.IsEmpty
                    || !_disabledFill.IsEmpty || !_focusRing.IsEmpty;
            }
        }

        /// <summary>
        /// 테마 위에 지정된 색만 덮어쓴 새 테마를 돌려준다.
        /// 지정된 색이 없으면 원본 테마를 그대로 돌려준다(복사 없음).
        /// 원본을 건드리지 않도록 반드시 복사본에 쓴다 — 테마는 여러 컨트롤이 공유한다.
        /// </summary>
        public AdvTheme Apply(AdvTheme baseTheme)
        {
            if (baseTheme == null || !HasAny) return baseTheme;

            var t = baseTheme.Clone();

            if (!_surface.IsEmpty) t.Surface = _surface;
            if (!_surfaceHover.IsEmpty) t.SurfaceHover = _surfaceHover;
            if (!_surfacePressed.IsEmpty) t.SurfacePressed = _surfacePressed;
            if (!_surfaceGradientEnd.IsEmpty) t.SurfaceGradientEnd = _surfaceGradientEnd;

            if (!_inputBackground.IsEmpty) t.InputBackground = _inputBackground;
            if (!_inputBackgroundDisabled.IsEmpty) t.InputBackgroundDisabled = _inputBackgroundDisabled;
            if (!_placeholder.IsEmpty) t.Placeholder = _placeholder;

            if (!_border.IsEmpty) t.Border = _border;
            if (!_borderHover.IsEmpty) t.BorderHover = _borderHover;
            if (!_borderFocus.IsEmpty) t.BorderFocus = _borderFocus;

            if (!_text.IsEmpty) t.Text = _text;
            if (!_textMuted.IsEmpty) t.TextMuted = _textMuted;
            if (!_textDisabled.IsEmpty) t.TextDisabled = _textDisabled;

            if (!_accent.IsEmpty) t.Accent = _accent;
            if (!_accentHover.IsEmpty) t.AccentHover = _accentHover;
            if (!_accentPressed.IsEmpty) t.AccentPressed = _accentPressed;
            if (!_accentGradientEnd.IsEmpty) t.AccentGradientEnd = _accentGradientEnd;
            if (!_onAccent.IsEmpty) t.OnAccent = _onAccent;

            if (!_disabledFill.IsEmpty) t.DisabledFill = _disabledFill;
            if (!_focusRing.IsEmpty) t.FocusRing = _focusRing;

            return t;
        }

        /// <summary>펼치기 전 값 칸. 비우지 않으면 타입 이름이 그대로 나온다.</summary>
        public override string ToString()
        {
            return string.Empty;
        }

        private void Set(ref Color field, Color value)
        {
            if (field.Equals(value)) return;
            field = value;
            var handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
