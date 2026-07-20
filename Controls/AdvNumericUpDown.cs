using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>
    /// 테마를 따르는 숫자 입력창. 글자 입력부는 표준 <see cref="TextBox"/>를 안에 올리고
    /// 테두리와 증감 버튼만 직접 그린다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("ValueChanged")]
    [DefaultProperty("Value")]
    [Description("테마를 따르는 숫자 입력창입니다.")]
    public class AdvNumericUpDown : AdvControlBase
    {
        private const int SpinWidth = 18;

        private AdvOptions _options;

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvOptions(Styling, Palette)); }
        }

        private readonly TextBox _editor;
        private decimal _minimum;
        private decimal _maximum = 100m;
        private decimal _value;
        private decimal _increment = 1m;
        private int _decimalPlaces;
        private bool _thousandsSeparator;
        private bool _syncing;

        /// <summary>0 = 없음, 1 = 위쪽, 2 = 아래쪽.</summary>
        private int _hotSpin;

        [Category("Behavior")]
        [Description("값이 바뀔 때 발생합니다.")]
        public event EventHandler ValueChanged;

        public AdvNumericUpDown()
        {
            _editor = new TextBox();
            _editor.BorderStyle = BorderStyle.None;
            _editor.TabStop = false;
            _editor.AutoSize = false;
            _editor.TextAlign = HorizontalAlignment.Right;

            _editor.GotFocus += EditorFocusChanged;
            _editor.LostFocus += EditorLostFocus;
            _editor.MouseEnter += EditorMouseEnter;
            _editor.MouseLeave += EditorMouseLeave;
            _editor.KeyDown += EditorKeyDown;

            Controls.Add(_editor);
        }

        protected override Size DefaultSize
        {
            get { return new Size(140, 34); }
        }

        protected override Padding DefaultPadding
        {
            get { return new Padding(8, 4, 8, 4); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TextBox Editor
        {
            get { return _editor; }
        }

        protected override bool ShowsFocusVisual
        {
            get { return _editor != null && _editor.Focused; }
        }

        #region 값

        [Category("Behavior")]
        [DefaultValue(typeof(decimal), "0")]
        [Description("입력할 수 있는 최솟값입니다.")]
        public decimal Minimum
        {
            get { return _minimum; }
            set
            {
                if (_minimum == value) return;
                _minimum = value;

                // 최솟값이 최댓값을 넘으면 이후 범위 검사가 항상 실패한다
                if (_maximum < _minimum) _maximum = _minimum;
                Value = _value;
                ReapplyMinimumSize();
            }
        }

        [Category("Behavior")]
        [DefaultValue(typeof(decimal), "100")]
        [Description("입력할 수 있는 최댓값입니다.")]
        public decimal Maximum
        {
            get { return _maximum; }
            set
            {
                if (_maximum == value) return;
                _maximum = value;

                if (_minimum > _maximum) _minimum = _maximum;
                Value = _value;
                ReapplyMinimumSize();
            }
        }

        [Category("Behavior")]
        [DefaultValue(typeof(decimal), "0")]
        [Description("현재 값입니다. 최솟값~최댓값 범위로 잘립니다.")]
        public decimal Value
        {
            get { return _value; }
            set
            {
                value = Clamp(value);
                if (_value == value)
                {
                    // 사용자가 친 글자가 정규화되지 않은 채 남지 않게 항상 다시 표시한다
                    SyncEditorFromValue();
                    return;
                }

                _value = value;
                SyncEditorFromValue();
                Invalidate();

                var handler = ValueChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        [Category("Behavior")]
        [DefaultValue(typeof(decimal), "1")]
        [Description("증감 버튼과 방향키가 한 번에 더하거나 빼는 값입니다.")]
        public decimal Increment
        {
            get { return _increment; }
            set { _increment = value <= 0m ? 1m : value; }
        }

        [Category("Appearance")]
        [DefaultValue(0)]
        [Description("소수점 아래 자릿수입니다.")]
        public int DecimalPlaces
        {
            get { return _decimalPlaces; }
            set
            {
                if (value < 0) value = 0;
                if (value > 15) value = 15;
                if (_decimalPlaces == value) return;
                _decimalPlaces = value;
                SyncEditorFromValue();
                ReapplyMinimumSize();
            }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        [Description("천 단위 구분 기호를 표시할지 여부입니다.")]
        public bool ThousandsSeparator
        {
            get { return _thousandsSeparator; }
            set
            {
                if (_thousandsSeparator == value) return;
                _thousandsSeparator = value;
                SyncEditorFromValue();
                ReapplyMinimumSize();
            }
        }

        private decimal Clamp(decimal v)
        {
            if (v < _minimum) return _minimum;
            if (v > _maximum) return _maximum;
            return v;
        }

        public void UpButton() { Value = Clamp(_value + _increment); }
        public void DownButton() { Value = Clamp(_value - _increment); }

        private string NumberFormat
        {
            get { return (_thousandsSeparator ? "N" : "F") + _decimalPlaces; }
        }

        private string FormatValue()
        {
            return _value.ToString(NumberFormat, CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// 값이 잘리지 않고 들어가는 가장 좁은 폭. 현재 값이 아니라 Minimum·Maximum
        /// 기준이라, 값을 입력한다고 컨트롤 폭이 흔들리지 않는다.
        /// </summary>
        private int MinimumValueWidth
        {
            get
            {
                int lo = TextRenderer.MeasureText(
                    _minimum.ToString(NumberFormat, CultureInfo.CurrentCulture), Font).Width;
                int hi = TextRenderer.MeasureText(
                    _maximum.ToString(NumberFormat, CultureInfo.CurrentCulture), Font).Width;

                return Math.Max(lo, hi)
                     + 4 + SpinWidth                    // TextBounds가 증감 영역에 내주는 몫
                     + ChromeSize.Width;
            }
        }

        /// <summary>값이 잘리지 않는 폭과, 글자가 눌리지 않는 높이를 함께 지킨다.</summary>
        protected override Size MinimumContentSize
        {
            get
            {
                int inner = _editor != null ? _editor.PreferredHeight : Font.Height;
                return new Size(MinimumValueWidth, ChromeSize.Height + inner);
            }
        }

        private void SyncEditorFromValue()
        {
            if (_editor == null) return;

            string text = FormatValue();
            if (_editor.Text == text) return;

            _syncing = true;
            try { _editor.Text = text; }
            finally { _syncing = false; }
        }

        /// <summary>
        /// 입력창의 글자를 값으로 되돌린다. 숫자가 아니면 마지막 값으로 되돌린다 —
        /// 잘못된 글자를 그대로 두면 화면과 Value가 달라진다.
        /// </summary>
        private void CommitEditorText()
        {
            if (_syncing || _editor == null) return;

            decimal parsed;
            var styles = NumberStyles.Number;

            if (decimal.TryParse(_editor.Text, styles, CultureInfo.CurrentCulture, out parsed))
                Value = Clamp(parsed);
            else
                SyncEditorFromValue();
        }

        #endregion

        #region 레이아웃

        private Rectangle SpinBounds
        {
            get
            {
                var c = ContentBounds;
                return new Rectangle(c.Right - SpinWidth, c.Top, SpinWidth, c.Height);
            }
        }

        private Rectangle UpBounds
        {
            get
            {
                var s = SpinBounds;
                return new Rectangle(s.Left, s.Top, s.Width, s.Height / 2);
            }
        }

        private Rectangle DownBounds
        {
            get
            {
                var s = SpinBounds;
                int half = s.Height / 2;
                return new Rectangle(s.Left, s.Top + half, s.Width, s.Height - half);
            }
        }

        private Rectangle TextBounds
        {
            get
            {
                var c = ContentBounds;
                return new Rectangle(c.Left, c.Top,
                                     Math.Max(0, c.Width - SpinWidth - 4), c.Height);
            }
        }

        private void LayoutEditor()
        {
            if (_editor == null) return;

            var area = TextBounds;
            if (area.Width < 1) area.Width = 1;
            if (area.Height < 1) area.Height = 1;

            int h = _editor.PreferredHeight;
            if (h < area.Height)
                area = new Rectangle(area.X, area.Y + (area.Height - h) / 2, area.Width, h);

            _editor.Bounds = area;
        }

        #endregion

        private void ApplyEditorAppearance()
        {
            if (_editor == null) return;

            var theme = EffectiveTheme;
            _editor.Font = Font;
            _editor.BackColor = Enabled ? theme.InputBackground : theme.InputBackgroundDisabled;
            _editor.ForeColor = Enabled ? theme.Text : theme.TextDisabled;
            _editor.Enabled = Enabled;
        }

        private void EditorFocusChanged(object sender, EventArgs e) { SetFocusVisual(true); }

        private void EditorLostFocus(object sender, EventArgs e)
        {
            SetFocusVisual(false);
            CommitEditorText();
        }

        private void EditorMouseEnter(object sender, EventArgs e) { SetHovered(true); }

        private void EditorMouseLeave(object sender, EventArgs e)
        {
            SetHovered(MouseStillInside);
        }

        private void EditorKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up:
                    UpButton();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;

                case Keys.Down:
                    DownButton();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;

                case Keys.Enter:
                    CommitEditorText();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = FrameBounds;

            Color fill = Enabled ? theme.InputBackground : theme.InputBackgroundDisabled;

            Color border;
            if (!Enabled) border = theme.Border;
            else if (ShowsFocusVisual) border = theme.BorderFocus;
            else border = AdvGraphics.Blend(theme.Border, theme.BorderHover, HoverAmount);

            AdvFrameRenderer.Draw(g, bounds, theme, EffectiveCorners, EffectiveBorderWidth,
                                  fill, Color.Empty, border, CurrentGlow, CurrentElevation,
                                  EffectiveBorderDash);

            bool atMax = _value >= _maximum;
            bool atMin = _value <= _minimum;

            DrawSpin(g, UpBounds, true, _hotSpin == 1, atMax, theme);
            DrawSpin(g, DownBounds, false, _hotSpin == 2, atMin, theme);

            base.OnPaint(e);
        }

        /// <summary>한계에 도달한 방향은 흐리게 그려 더 못 간다는 걸 보여준다.</summary>
        private void DrawSpin(Graphics g, Rectangle area, bool up, bool hot, bool atLimit, AdvTheme theme)
        {
            if (area.Width <= 0 || area.Height <= 0) return;

            if (hot && Enabled && !atLimit)
            {
                using (var brush = new SolidBrush(theme.SurfaceHover))
                using (var path = AdvGraphics.CreateRoundedRect(area, new AdvCorners(3)))
                    g.FillPath(brush, path);
            }

            Color color = (!Enabled || atLimit) ? theme.TextDisabled : theme.TextMuted;

            // 두 셰브런을 각자 절반의 정중앙에 두면 맞닿아 닫힌 마름모(◇) 하나로 읽힌다.
            // 높이를 줄이고 맞물리는 쪽에서 서로 1px씩 밀어 두 개의 화살표로 보이게 한다
            const int seam = 1;

            AdvGraphics.DrawChevron(g, area,
                up ? AdvGraphics.ChevronDirection.Up : AdvGraphics.ChevronDirection.Down,
                color, 7, 3, 1.5f, up ? -seam : seam);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int hot = UpBounds.Contains(e.Location) ? 1
                    : DownBounds.Contains(e.Location) ? 2
                    : 0;

            if (hot != _hotSpin)
            {
                _hotSpin = hot;
                Invalidate();
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hotSpin != 0) { _hotSpin = 0; Invalidate(); }

            // 커서가 안쪽 입력창으로 들어갔을 뿐이면 컨트롤을 벗어난 것이 아니다
            if (MouseStillInside) return;

            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (UpBounds.Contains(e.Location)) { _editor.Focus(); UpButton(); }
                else if (DownBounds.Contains(e.Location)) { _editor.Focus(); DownButton(); }
                else if (!_editor.Focused) _editor.Focus();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (Enabled)
            {
                if (e.Delta > 0) UpButton();
                else if (e.Delta < 0) DownButton();
            }
            base.OnMouseWheel(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            _editor.Focus();
            base.OnGotFocus(e);
        }

        protected override void OnThemeChanged()
        {
            ApplyEditorAppearance();
            LayoutEditor();
            base.OnThemeChanged();
        }

        protected override void OnResize(EventArgs e) { LayoutEditor(); base.OnResize(e); }

        protected override void OnPaddingChanged(EventArgs e)
        {
            ReapplyMinimumSize();
            LayoutEditor();
            base.OnPaddingChanged(e);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            ApplyEditorAppearance();
            ReapplyMinimumSize();
            LayoutEditor();
            base.OnFontChanged(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            ApplyEditorAppearance();
            base.OnEnabledChanged(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyEditorAppearance();
            SyncEditorFromValue();
            ReapplyMinimumSize();
            LayoutEditor();
        }

        /// <summary>
        /// 단일 행이므로 글꼴에 맞는 높이로 고정한다.
        /// 최소 폭(값이 잘리지 않는 폭)은 베이스가 MinimumContentSize로 지킨다.
        /// </summary>
        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            if (IsHandleCreated && _editor != null)
                height = _editor.PreferredHeight + ChromeSize.Height;

            base.SetBoundsCore(x, y, width, height, specified);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _editor != null)
            {
                _editor.GotFocus -= EditorFocusChanged;
                _editor.LostFocus -= EditorLostFocus;
                _editor.MouseEnter -= EditorMouseEnter;
                _editor.MouseLeave -= EditorMouseLeave;
                _editor.KeyDown -= EditorKeyDown;
            }
            base.Dispose(disposing);
        }
    }
}
