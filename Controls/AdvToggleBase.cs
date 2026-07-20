using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Animation;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>
    /// 체크박스·라디오버튼처럼 "표시 도형 + 라벨" 구조를 갖는 컨트롤의 공통 부분.
    /// 도형 자체는 파생 클래스가 그린다.
    /// </summary>
    [ToolboxItem(false)]
    public abstract class AdvToggleBase : AdvControlBase
    {
        private bool _checked;
        private bool _buttonStyle;
        private readonly AdvAnimator _checkAnim;
        private readonly AdvGlyphSettings _glyph = new AdvGlyphSettings();
        private AdvToggleOptions _options;

        /// <summary>버튼형으로 그릴 때 글자 좌우·상하에 두는 안쪽 여백.</summary>
        private const int ButtonPadX = 14;
        private const int ButtonPadY = 5;

        [Category("Behavior")]
        [Description("체크 상태가 바뀔 때 발생합니다.")]
        public event EventHandler CheckedChanged;

        protected AdvToggleBase()
        {
            TabStop = true;
            _checkAnim = new AdvAnimator(0);
            _checkAnim.ValueChanged += OnCheckAnimTick;
            _glyph.LayoutChanged += OnGlyphLayoutChanged;
        }

        /// <summary>표시 도형의 크기·간격·좌우 배치.</summary>
        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [Description("표시 도형의 크기와 배치입니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public AdvGlyphSettings Glyph
        {
            get { return _glyph; }
        }

        protected int GlyphSize
        {
            get { return _glyph.Size; }
        }

        protected int GlyphGap
        {
            get { return _glyph.Gap; }
        }

        /// <summary>
        /// 도형의 가로 길이. 기본은 정사각형이고, 스위치처럼 가로로 긴 도형은 재정의한다.
        /// </summary>
        protected virtual int GlyphWidth
        {
            get { return _glyph.Size; }
        }

        private void OnGlyphLayoutChanged(object sender, EventArgs e)
        {
            AdjustSize();
            Invalidate();
        }

        protected override Size DefaultSize
        {
            get { return new Size(140, 24); }
        }

        protected override bool IsClickable
        {
            get { return true; }
        }

        /// <summary>
        /// 도형(체크 상자·원) 대신 눌리는 버튼 모양으로 그릴지 여부.
        /// 켜짐이면 강조색으로 채운 버튼, 꺼짐이면 테두리만 있는 버튼으로 보인다.
        /// </summary>
        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("버튼처럼 그릴지 여부입니다. 켜면 도형 대신 눌리는 버튼 모양이 됩니다.")]
        public bool ButtonStyle
        {
            get { return _buttonStyle; }
            set
            {
                if (_buttonStyle == value) return;
                _buttonStyle = value;
                AdjustSize();
                ReapplyMinimumSize();
                Invalidate();
            }
        }

        /// <summary>버튼형 렌더링을 지원하는지. 스위치처럼 도형이 핵심인 컨트롤은 끈다.</summary>
        protected virtual bool SupportsButtonStyle
        {
            get { return true; }
        }

        /// <summary>실제로 버튼 모양으로 그릴지. 지원하지 않으면 켜도 무시한다.</summary>
        protected bool RenderAsButton
        {
            get { return _buttonStyle && SupportsButtonStyle; }
        }

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvToggleOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvToggleOptions(this)); }
        }

        /// <summary>도형 + 간격 + 글자에 글로우 여백을 더한 크기. 버튼형은 버튼 크기로 잡는다.</summary>
        public override Size GetPreferredSize(Size proposedSize)
        {
            var text = string.IsNullOrEmpty(Text)
                     ? Size.Empty
                     : TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue),
                                                TextFormatFlags.NoPrefix);

            if (RenderAsButton)
            {
                int edge = (FramePadding + EffectiveBorderWidth) * 2;
                return new Size(text.Width + edge + ButtonPadX * 2,
                                Math.Max(text.Height, Font.Height) + edge + ButtonPadY * 2);
            }

            int pad = FramePadding * 2;
            int width = GlyphWidth + pad;
            if (!text.IsEmpty) width += GlyphGap + text.Width;

            return new Size(width, Math.Max(GlyphSize, text.Height) + pad);
        }

        /// <summary>
        /// 버튼형은 글자가 눌리지 않을 최소 높이를 지킨다.
        /// AutoSize가 꺼져 있어도 도형형보다 세로 여백이 커서 눌릴 수 있어 따로 잡는다.
        /// </summary>
        protected override Size MinimumContentSize
        {
            get
            {
                if (!RenderAsButton) return base.MinimumContentSize;
                int edge = (FramePadding + EffectiveBorderWidth + ButtonPadY) * 2;
                return new Size(0, edge + Font.Height);
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("체크 여부입니다.")]
        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                SetCheckedCore(value);
                OnCheckedChanged(EventArgs.Empty);
            }
        }

        /// <summary>이벤트를 내지 않고 상태만 바꾼다. 라디오 그룹에서 형제를 끌 때 쓴다.</summary>
        protected void SetCheckedCore(bool value)
        {
            _checked = value;
            _checkAnim.Duration = EffectiveTransitionDuration;
            _checkAnim.Easing = EffectiveEasing;
            _checkAnim.AnimateTo(value ? 1f : 0f);
            Invalidate();
        }

        /// <summary>체크 전환 진행도(0~1).</summary>
        protected float CheckAmount
        {
            get { return _checkAnim.Eased; }
        }

        protected virtual void OnCheckedChanged(EventArgs e)
        {
            var handler = CheckedChanged;
            if (handler != null) handler(this, e);
        }

        /// <summary>코드에서 클릭과 같은 동작을 일으킨다. 표준 CheckBox와 같다.</summary>
        public void PerformClick()
        {
            if (!Enabled) return;
            OnClick(EventArgs.Empty);
        }

        /// <summary>클릭·스페이스로 눌렸을 때의 동작. 체크박스는 토글, 라디오는 켜기만 한다.</summary>
        protected abstract void Toggle();

        /// <summary>표시 도형을 그린다.</summary>
        protected abstract void DrawGlyph(Graphics g, Rectangle glyph, AdvTheme theme,
                                          Color fill, Color border, Color mark);

        /// <summary>도형이 놓이는 자리. 세로는 항상 중앙, 가로는 Glyph.Alignment를 따른다.</summary>
        protected Rectangle GlyphBounds
        {
            get
            {
                var f = FrameBounds;
                int w = GlyphWidth, h = GlyphSize;
                int x = _glyph.Alignment == LeftRightAlignment.Right ? f.Right - w : f.Left;

                return new Rectangle(x, f.Top + (f.Height - h) / 2, w, h);
            }
        }

        protected Rectangle LabelBounds
        {
            get
            {
                var f = FrameBounds;
                int taken = GlyphWidth + GlyphGap;

                if (_glyph.Alignment == LeftRightAlignment.Right)
                    return new Rectangle(f.Left, f.Top, Math.Max(0, f.Width - taken), f.Height);

                return new Rectangle(f.Left + taken, f.Top, Math.Max(0, f.Width - taken), f.Height);
            }
        }

        private void OnCheckAnimTick(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (RenderAsButton)
            {
                PaintButton(g, theme);
                base.OnPaint(e);
                return;
            }

            Color fill, border, mark, text;
            ResolveColors(theme, out fill, out border, out mark, out text);

            var glyph = GlyphBounds;

            var glow = CurrentGlow;
            if (glow != null)
                AdvGraphics.DrawShadow(g, glyph, GlyphCorners(theme), glow);

            DrawGlyph(g, glyph, theme, fill, border, mark);

            if (!string.IsNullOrEmpty(Text))
            {
                // 도형이 오른쪽에 있으면 글자는 반대쪽 끝에 붙어야 사이 간격이 유지된다
                var align = _glyph.Alignment == LeftRightAlignment.Right
                          ? TextFormatFlags.Right
                          : TextFormatFlags.Left;

                TextRenderer.DrawText(g, Text, Font, LabelBounds, text,
                    align
                  | TextFormatFlags.VerticalCenter
                  | TextFormatFlags.EndEllipsis
                  | TextFormatFlags.NoPrefix);
            }

            base.OnPaint(e);
        }

        /// <summary>글로우를 도형 모양에 맞춰 그리기 위한 반경. 라디오는 원이라 재정의한다.</summary>
        protected virtual AdvCorners GlyphCorners(AdvTheme theme)
        {
            // 도형이 16px로 작아 컨트롤 반경을 그대로 쓰면 원처럼 뭉개진다
            return new AdvCorners(Math.Min(EffectiveCorners.Max, GlyphSize / 4));
        }

        private void ResolveColors(AdvTheme theme, out Color fill, out Color border,
                                   out Color mark, out Color text)
        {
            if (!Enabled)
            {
                fill = _checked ? theme.DisabledFill : theme.InputBackgroundDisabled;
                border = theme.Border;
                mark = theme.TextDisabled;
                text = theme.TextDisabled;
                return;
            }

            float t = CheckAmount;
            float h = HoverAmount;

            // 꺼짐 → 켜짐으로 갈수록 배경이 강조색으로 차오른다
            Color offFill = AdvGraphics.Blend(theme.InputBackground, theme.SurfaceHover, h);
            Color onFill = IsPressed ? theme.AccentPressed
                         : AdvGraphics.Blend(theme.Accent, theme.AccentHover, h);

            Color offBorder = AdvGraphics.Blend(theme.Border, theme.BorderHover, h);

            fill = AdvGraphics.Blend(offFill, onFill, t);
            border = AdvGraphics.Blend(offBorder, onFill, t);
            mark = theme.OnAccent;
            text = theme.Text;
        }

        /// <summary>버튼형일 때: 버튼 프레임을 그리고 글자를 가운데 놓는다.</summary>
        private void PaintButton(Graphics g, AdvTheme theme)
        {
            var bounds = FrameBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            int bw = EffectiveBorderWidth;

            Color fill, fillEnd, border, fore;
            ResolveButtonColors(theme, out fill, out fillEnd, out border, out fore);

            AdvFrameRenderer.Draw(g, bounds, theme, EffectiveCorners, bw, fill, fillEnd, border,
                                  CurrentGlow, CurrentElevation, EffectiveBorderDash);

            if (string.IsNullOrEmpty(Text)) return;

            var content = new Rectangle(
                bounds.Left + bw + ButtonPadX,
                bounds.Top + bw + ButtonPadY,
                Math.Max(0, bounds.Width - bw * 2 - ButtonPadX * 2),
                Math.Max(0, bounds.Height - bw * 2 - ButtonPadY * 2));

            TextRenderer.DrawText(g, Text, Font, content, fore,
                TextFormatFlags.HorizontalCenter
              | TextFormatFlags.VerticalCenter
              | TextFormatFlags.EndEllipsis
              | TextFormatFlags.NoPrefix);
        }

        /// <summary>
        /// 버튼형 색. 켜짐이면 강조색으로 채운 버튼, 꺼짐이면 테두리만 있는 버튼이다.
        /// 체크 전환(CheckAmount)에 맞춰 두 상태 사이를 보간한다.
        /// </summary>
        private void ResolveButtonColors(AdvTheme theme, out Color fill, out Color fillEnd,
                                         out Color border, out Color fore)
        {
            if (!Enabled)
            {
                fill = _checked ? theme.DisabledFill : theme.Surface;
                fillEnd = Color.Empty;
                border = theme.Border;
                fore = theme.TextDisabled;
                return;
            }

            float t = CheckAmount;
            float h = HoverAmount;

            Color offFill = IsPressed ? theme.SurfacePressed
                          : AdvGraphics.Blend(theme.Surface, theme.SurfaceHover, h);
            Color onFill = IsPressed ? theme.AccentPressed
                         : AdvGraphics.Blend(theme.Accent, theme.AccentHover, h);

            fill = AdvGraphics.Blend(offFill, onFill, t);
            fillEnd = _checked ? theme.AccentGradientEnd : theme.SurfaceGradientEnd;

            Color offBorder = AdvGraphics.Blend(theme.Border, theme.BorderHover, h);
            border = AdvGraphics.Blend(offBorder, onFill, t);

            fore = AdvGraphics.Blend(theme.Text, theme.OnAccent, t);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !Focused) Focus();
            base.OnMouseDown(e);
        }

        protected override void OnClick(EventArgs e)
        {
            Toggle();
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) IsPressed = true;
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && IsPressed)
            {
                IsPressed = false;
                // OnClick이 Toggle을 부르므로 여기서 직접 토글하지 않는다
                OnClick(EventArgs.Empty);
            }
            base.OnKeyUp(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            IsPressed = false;
            base.OnLostFocus(e);
        }

        protected override void OnThemeChanged()
        {
            _checkAnim.Duration = EffectiveTransitionDuration;
            _checkAnim.Easing = EffectiveEasing;
            base.OnThemeChanged();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _checkAnim.Duration = EffectiveTransitionDuration;
            _checkAnim.Easing = EffectiveEasing;
            _checkAnim.SetImmediate(_checked ? 1f : 0f);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _checkAnim.ValueChanged -= OnCheckAnimTick;
                _checkAnim.Dispose();
                _glyph.LayoutChanged -= OnGlyphLayoutChanged;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// 체크박스·라디오·스위치가 공통으로 추가한 속성.
    /// 파생 컨트롤이 고유 속성을 더 내놓을 때는(예: AdvCheckBoxOptions) 이 클래스를 상속해 늘린다.
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class AdvToggleOptions : AdvOptions
    {
        private readonly AdvToggleBase _owner;

        internal AdvToggleOptions(AdvToggleBase owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [Description("표시 도형의 크기와 배치입니다.")]
        public AdvGlyphSettings Glyph
        {
            get { return _owner.Glyph; }
        }

        [DefaultValue(false)]
        [Description("버튼처럼 그릴지 여부입니다. 켜면 도형 대신 눌리는 버튼 모양이 됩니다.")]
        public bool ButtonStyle
        {
            get { return _owner.ButtonStyle; }
            set { _owner.ButtonStyle = value; }
        }

        [DefaultValue(false)]
        [Description("체크 여부입니다.")]
        public bool Checked
        {
            get { return _owner.Checked; }
            set { _owner.Checked = value; }
        }

        [DefaultValue(true)]
        [Description("이 컨트롤 위에서 손 모양 커서를 보일지 여부입니다.")]
        public bool UseHandCursor
        {
            get { return _owner.UseHandCursor; }
            set { _owner.UseHandCursor = value; }
        }
    }
}
