using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>
    /// 테마를 따르는 입력창. 테두리·배경·포커스 링만 직접 그리고
    /// 글자 입력부는 표준 <see cref="TextBox"/>를 안에 올려 그대로 쓴다.
    /// 한글 IME 조합, 클립보드, 실행 취소, 캐럿, 드래그 선택이 모두 표준 동작 그대로다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("TextChanged")]
    [DefaultProperty("Text")]
    [Description("테마를 따르는 입력창입니다.")]
    public class AdvTextBox : AdvControlBase
    {
        /// <summary>
        /// 안내 문구를 직접 그리는 입력창. Win32 큐 배너(EM_SETCUEBANNER)는 색을 OS가 정해
        /// 다크 테마에서 대비가 맞지 않으므로, 기본 그리기가 끝난 뒤 위에 덧그린다.
        /// 글자 입력·IME는 그대로 표준 TextBox가 맡는다.
        /// </summary>
        private class PlaceholderTextBox : TextBox
        {
            private const int WM_PAINT = 0x000F;
            private const int WM_PRINTCLIENT = 0x0318;

            public string Placeholder = string.Empty;
            public Color PlaceholderColor = SystemColors.GrayText;

            /// <summary>
            /// 화면 출력은 WM_PAINT로, DrawToBitmap·인쇄는 WM_PRINTCLIENT로 들어온다.
            /// 둘 다 받지 않으면 화면에는 보이는데 캡처에는 안 찍힌다.
            /// </summary>
            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                if (m.Msg != WM_PAINT && m.Msg != WM_PRINTCLIENT) return;
                if (Placeholder.Length == 0 || TextLength > 0) return;

                if (m.Msg == WM_PRINTCLIENT && m.WParam != IntPtr.Zero)
                {
                    using (var g = Graphics.FromHdc(m.WParam))
                        DrawPlaceholder(g);
                }
                else if (m.Msg == WM_PAINT)
                {
                    using (var g = Graphics.FromHwnd(Handle))
                        DrawPlaceholder(g);
                }
            }

            private void DrawPlaceholder(Graphics g)
            {
                var flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

                if (Multiline) flags |= TextFormatFlags.Top | TextFormatFlags.WordBreak;
                else flags |= TextFormatFlags.VerticalCenter
                            | TextFormatFlags.SingleLine
                            | TextFormatFlags.EndEllipsis;

                switch (TextAlign)
                {
                    case HorizontalAlignment.Center: flags |= TextFormatFlags.HorizontalCenter; break;
                    case HorizontalAlignment.Right: flags |= TextFormatFlags.Right; break;
                    default: flags |= TextFormatFlags.Left; break;
                }

                TextRenderer.DrawText(g, Placeholder, Font, ClientRectangle, PlaceholderColor, flags);
            }
        }

        private readonly PlaceholderTextBox _inner;
        private string _placeholder = string.Empty;

        public AdvTextBox()
        {
            _inner = new PlaceholderTextBox();
            _inner.BorderStyle = BorderStyle.None;
            _inner.TabStop = false;          // 탭 순서는 바깥 컨트롤이 갖는다
            _inner.AutoSize = false;

            // 안쪽 핸들은 바깥보다 늦게 만들어지므로, 그 시점에 안내 문구를 다시 넣는다
            _inner.HandleCreated += InnerHandleCreated;
            _inner.TextChanged += InnerTextChanged;
            _inner.GotFocus += InnerFocusChanged;
            _inner.LostFocus += InnerFocusChanged;
            _inner.MouseEnter += InnerMouseEnter;
            _inner.MouseLeave += InnerMouseLeave;
            _inner.KeyDown += InnerKeyDown;
            _inner.KeyPress += InnerKeyPress;
            _inner.KeyUp += InnerKeyUp;

            Controls.Add(_inner);
        }

        protected override Size DefaultSize
        {
            get { return new Size(180, 34); }
        }

        protected override Padding DefaultPadding
        {
            get { return new Padding(8, 4, 8, 4); }
        }

        /// <summary>안에 올라간 표준 TextBox. 기본 속성으로 부족할 때 직접 접근한다.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TextBox InnerTextBox
        {
            get { return _inner; }
        }

        protected override bool ShowsFocusVisual
        {
            get { return _inner != null && _inner.Focused; }
        }

        #region 표준 TextBox 속성 전달

        private AdvTextBoxOptions _options;

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvTextBoxOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvTextBoxOptions(this)); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue("")]
        [Description("내용이 비어 있을 때 흐리게 표시할 안내 문구입니다. 색은 테마를 따릅니다.")]
        public string Placeholder
        {
            get { return _placeholder; }
            set
            {
                value = value ?? string.Empty;
                if (_placeholder == value) return;
                _placeholder = value;
                ApplyPlaceholder();
            }
        }

        [Browsable(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public override string Text
        {
            get { return _inner == null ? string.Empty : _inner.Text; }
            set { if (_inner != null) _inner.Text = value ?? string.Empty; }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("입력을 막고 읽기 전용으로 만들지 여부입니다.")]
        public bool ReadOnly
        {
            get { return _inner.ReadOnly; }
            set
            {
                if (_inner.ReadOnly == value) return;
                _inner.ReadOnly = value;
                ApplyInnerAppearance();
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("여러 줄을 입력받을지 여부입니다. 켜면 높이를 직접 지정할 수 있습니다.")]
        public bool Multiline
        {
            get { return _inner.Multiline; }
            set
            {
                if (_inner.Multiline == value) return;
                _inner.Multiline = value;
                LayoutInner();
                AdjustHeight();
            }
        }

        [Category("Behavior")]
        [DefaultValue(32767)]
        [Description("입력할 수 있는 최대 글자 수입니다.")]
        public int MaxLength
        {
            get { return _inner.MaxLength; }
            set { _inner.MaxLength = value; }
        }

        [Category("Behavior")]
        [DefaultValue('\0')]
        [Description("입력한 글자 대신 표시할 문자입니다. 비워 두면 글자가 그대로 보입니다.")]
        public char PasswordChar
        {
            get { return _inner.PasswordChar; }
            set { _inner.PasswordChar = value; }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("OS가 정한 암호 문자를 쓸지 여부입니다. 켜면 PasswordChar보다 우선합니다.")]
        public bool UseSystemPasswordChar
        {
            get { return _inner.UseSystemPasswordChar; }
            set { _inner.UseSystemPasswordChar = value; }
        }

        [Category("Appearance")]
        [DefaultValue(HorizontalAlignment.Left)]
        [Description("글자를 왼쪽·가운데·오른쪽 중 어디에 맞출지입니다.")]
        public HorizontalAlignment TextAlign
        {
            get { return _inner.TextAlign; }
            set { _inner.TextAlign = value; }
        }

        [Category("Behavior")]
        [DefaultValue(ScrollBars.None)]
        [Description("여러 줄일 때 표시할 스크롤 막대입니다. Multiline이 꺼져 있으면 무시됩니다.")]
        public ScrollBars ScrollBars
        {
            get { return _inner.ScrollBars; }
            set { _inner.ScrollBars = value; }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectionStart
        {
            get { return _inner.SelectionStart; }
            set { _inner.SelectionStart = value; }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectionLength
        {
            get { return _inner.SelectionLength; }
            set { _inner.SelectionLength = value; }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SelectedText
        {
            get { return _inner.SelectedText; }
            set { _inner.SelectedText = value; }
        }

        public void SelectAll() { _inner.SelectAll(); }
        public void Clear() { _inner.Clear(); }
        public void Undo() { _inner.Undo(); }
        public void Copy() { _inner.Copy(); }
        public void Cut() { _inner.Cut(); }
        public void Paste() { _inner.Paste(); }

        #endregion

        #region 내부 TextBox 이벤트 중계

        private void InnerHandleCreated(object sender, EventArgs e) { ApplyPlaceholder(); }
        private void InnerTextChanged(object sender, EventArgs e) { OnTextChanged(EventArgs.Empty); }
        private void InnerKeyDown(object sender, KeyEventArgs e) { OnKeyDown(e); }
        private void InnerKeyPress(object sender, KeyPressEventArgs e) { OnKeyPress(e); }
        private void InnerKeyUp(object sender, KeyEventArgs e) { OnKeyUp(e); }

        private void InnerFocusChanged(object sender, EventArgs e)
        {
            SetFocusVisual(_inner.Focused);
        }

        private void InnerMouseEnter(object sender, EventArgs e) { SetHovered(true); }

        /// <summary>
        /// 안쪽 TextBox와 바깥 테두리 사이를 오갈 때 MouseLeave가 먼저 오므로,
        /// 커서가 정말 컨트롤 밖으로 나갔는지 좌표로 확인한다.
        /// </summary>
        private void InnerMouseLeave(object sender, EventArgs e)
        {
            SetHovered(MouseStillInside);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            // 커서가 안쪽 TextBox로 들어갔을 뿐이면 컨트롤을 벗어난 것이 아니므로
            // 호버를 유지하고 MouseLeave도 올리지 않는다
            if (MouseStillInside) return;

            base.OnMouseLeave(e);
        }

        #endregion

        protected override void OnMouseDown(MouseEventArgs e)
        {
            // 테두리 여백을 눌러도 입력이 시작되게 한다
            if (e.Button == MouseButtons.Left && !_inner.Focused) _inner.Focus();
            base.OnMouseDown(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            _inner.Focus();
            base.OnGotFocus(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var bounds = FrameBounds;

            Color fill = !Enabled ? theme.InputBackgroundDisabled
                       : ReadOnly ? theme.InputBackgroundDisabled
                       : theme.InputBackground;

            Color border;
            if (!Enabled) border = theme.Border;
            else if (ShowsFocusVisual) border = theme.BorderFocus;
            else border = AdvGraphics.Blend(theme.Border, theme.BorderHover, HoverAmount);

            AdvFrameRenderer.Draw(e.Graphics, bounds, theme, EffectiveCorners, EffectiveBorderWidth,
                                  fill, Color.Empty, border, CurrentGlow, CurrentElevation,
                                  EffectiveBorderDash);

            base.OnPaint(e);
        }

        protected override void OnResize(EventArgs e)
        {
            LayoutInner();
            base.OnResize(e);
        }

        protected override void OnPaddingChanged(EventArgs e)
        {
            LayoutInner();
            AdjustHeight();
            base.OnPaddingChanged(e);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            _inner.Font = Font;
            LayoutInner();
            AdjustHeight();
            base.OnFontChanged(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            _inner.Enabled = Enabled;
            ApplyInnerAppearance();
            base.OnEnabledChanged(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _inner.Font = Font;
            ApplyInnerAppearance();
            ApplyPlaceholder();
            LayoutInner();
            AdjustHeight();
        }

        protected override void OnThemeChanged()
        {
            ApplyInnerAppearance();
            ApplyPlaceholder();      // 안내 문구 색도 테마를 따른다
            LayoutInner();
            base.OnThemeChanged();
        }

        /// <summary>
        /// 테마 색은 안쪽 TextBox에도 반영해야 한다. 여기는 직접 그리지 못하는 영역이다.
        /// </summary>
        private void ApplyInnerAppearance()
        {
            var theme = EffectiveTheme;

            _inner.BackColor = !Enabled || ReadOnly
                             ? theme.InputBackgroundDisabled
                             : theme.InputBackground;

            _inner.ForeColor = Enabled ? theme.Text : theme.TextDisabled;
        }

        private void ApplyPlaceholder()
        {
            _inner.Placeholder = _placeholder;
            _inner.PlaceholderColor = EffectiveTheme.Placeholder;
            _inner.Invalidate();
        }

        private void LayoutInner()
        {
            var frame = FrameBounds;
            int bw = EffectiveBorderWidth;

            var area = new Rectangle(
                frame.Left + bw + Padding.Left,
                frame.Top + bw + Padding.Top,
                frame.Width - bw * 2 - Padding.Horizontal,
                frame.Height - bw * 2 - Padding.Vertical);

            if (area.Width < 1) area.Width = 1;
            if (area.Height < 1) area.Height = 1;

            // 단일 행 TextBox는 높이를 스스로 정하므로 세로 중앙에 맞춘다
            if (!_inner.Multiline)
            {
                int h = _inner.PreferredHeight;
                if (h < area.Height) area = new Rectangle(area.X, area.Y + (area.Height - h) / 2, area.Width, h);
            }

            _inner.Bounds = area;
        }

        /// <summary>단일 행일 때는 글꼴에 맞는 높이로 고정한다. 표준 TextBox와 같은 동작이다.</summary>
        private void AdjustHeight()
        {
            if (_inner.Multiline || !IsHandleCreated) return;

            int h = _inner.PreferredHeight
                  + Padding.Vertical
                  + EffectiveBorderWidth * 2
                  + FramePadding * 2;

            if (Height != h) Height = h;
        }

        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            if (!Multiline && IsHandleCreated)
            {
                height = _inner.PreferredHeight
                       + Padding.Vertical
                       + EffectiveBorderWidth * 2
                       + FramePadding * 2;
            }
            base.SetBoundsCore(x, y, width, height, specified);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _inner != null)
            {
                _inner.HandleCreated -= InnerHandleCreated;
                _inner.TextChanged -= InnerTextChanged;
                _inner.GotFocus -= InnerFocusChanged;
                _inner.LostFocus -= InnerFocusChanged;
                _inner.MouseEnter -= InnerMouseEnter;
                _inner.MouseLeave -= InnerMouseLeave;
                _inner.KeyDown -= InnerKeyDown;
                _inner.KeyPress -= InnerKeyPress;
                _inner.KeyUp -= InnerKeyUp;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>AdvTextBox가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvTextBoxOptions : AdvOptions
    {
        private readonly AdvTextBox _owner;

        internal AdvTextBoxOptions(AdvTextBox owner) : base(owner.Styling)
        {
            _owner = owner;
        }

        [DefaultValue("")]
        [Description("내용이 비어 있을 때 흐리게 표시할 안내 문구입니다. 색은 테마를 따릅니다.")]
        public string Placeholder
        {
            get { return _owner.Placeholder; }
            set { _owner.Placeholder = value; }
        }
    }
}
