using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    [DefaultProperty("AdvancedControlOptions")]
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

            /// <summary>바깥 AdvTextBox. 포커스가 이 내부 편집창에 오므로 접근성 이름을 여기서 가져온다.</summary>
            public AdvTextBox Host;

            protected override AccessibleObject CreateAccessibilityInstance()
            {
                return new PlaceholderAccessibleObject(this);
            }

            private sealed class PlaceholderAccessibleObject : ControlAccessibleObject
            {
                private readonly PlaceholderTextBox _tb;
                public PlaceholderAccessibleObject(PlaceholderTextBox tb) : base(tb) { _tb = tb; }

                public override string Name
                {
                    get
                    {
                        var host = _tb.Host;
                        if (host != null && !string.IsNullOrEmpty(host.AccessibleName)) return host.AccessibleName;
                        return _tb.Placeholder;   // 이름이 없으면 안내 문구라도 읽어 준다
                    }
                    set { base.Name = value; }
                }
            }
        }

        private readonly PlaceholderTextBox _inner;
        private string _placeholder = string.Empty;

        // 입력 강화: 아이콘·접두/접미 애드온·지우기 버튼·검증 상태
        private const int AddonGap = 6;
        private Image _leadingIcon;
        private Image _trailingIcon;
        private string _prefix = string.Empty;
        private string _suffix = string.Empty;
        private bool _showClearButton;
        private AdvValidationState _validation = AdvValidationState.None;
        private bool _clearHover;
        private Rectangle _leadingRect, _prefixRect, _suffixRect, _trailingRect, _clearRect, _validationRect;

        public AdvTextBox()
        {
            _inner = new PlaceholderTextBox();
            _inner.Host = this;
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

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
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
                LayoutInner();      // 읽기 전용이면 지우기 버튼 자리를 예약하지 않는다
                Invalidate();
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
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

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(32767)]
        [Description("입력할 수 있는 최대 글자 수입니다.")]
        public int MaxLength
        {
            get { return _inner.MaxLength; }
            set { _inner.MaxLength = value; }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue('\0')]
        [Description("입력한 글자 대신 표시할 문자입니다. 비워 두면 글자가 그대로 보입니다.")]
        public char PasswordChar
        {
            get { return _inner.PasswordChar; }
            set { _inner.PasswordChar = value; }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("OS가 정한 암호 문자를 쓸지 여부입니다. 켜면 PasswordChar보다 우선합니다.")]
        public bool UseSystemPasswordChar
        {
            get { return _inner.UseSystemPasswordChar; }
            set { _inner.UseSystemPasswordChar = value; }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(HorizontalAlignment.Left)]
        [Description("글자를 왼쪽·가운데·오른쪽 중 어디에 맞출지입니다.")]
        public HorizontalAlignment TextAlign
        {
            get { return _inner.TextAlign; }
            set { _inner.TextAlign = value; }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
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

        #region 입력 강화 (아이콘·애드온·지우기·검증)

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(null)]
        [Description("입력창 왼쪽 안에 표시할 아이콘입니다. 비율을 유지하며 맞춰집니다.")]
        public Image LeadingIcon
        {
            get { return _leadingIcon; }
            set { if (_leadingIcon == value) return; _leadingIcon = value; LayoutInner(); Invalidate(); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(null)]
        [Description("입력창 오른쪽 안에 표시할 아이콘입니다. 비율을 유지하며 맞춰집니다.")]
        public Image TrailingIcon
        {
            get { return _trailingIcon; }
            set { if (_trailingIcon == value) return; _trailingIcon = value; LayoutInner(); Invalidate(); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue("")]
        [Description("글자 앞에 흐리게 붙이는 고정 문구입니다. 예: \"https://\", \"$\".")]
        public string Prefix
        {
            get { return _prefix; }
            set
            {
                value = value ?? string.Empty;
                if (_prefix == value) return;
                _prefix = value;
                LayoutInner();
                Invalidate();
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue("")]
        [Description("글자 뒤에 흐리게 붙이는 고정 문구입니다. 예: \".com\", \"kg\".")]
        public string Suffix
        {
            get { return _suffix; }
            set
            {
                value = value ?? string.Empty;
                if (_suffix == value) return;
                _suffix = value;
                LayoutInner();
                Invalidate();
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("글자가 있을 때 오른쪽에 지우기(×) 버튼을 표시할지 여부입니다.")]
        public bool ShowClearButton
        {
            get { return _showClearButton; }
            set { if (_showClearButton == value) return; _showClearButton = value; LayoutInner(); Invalidate(); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvValidationState.None)]
        [Description("검증 상태입니다. 테두리 색과 오른쪽 상태 아이콘으로 표시됩니다.")]
        public AdvValidationState ValidationState
        {
            get { return _validation; }
            set { if (_validation == value) return; _validation = value; LayoutInner(); Invalidate(); }
        }

        /// <summary>검증 상태별 색. 테마의 의미색(성공·경고·오류)을 따라 라이트/다크에서 대비가 맞는다.</summary>
        private static Color ValidationColor(AdvValidationState s, AdvTheme theme)
        {
            switch (s)
            {
                case AdvValidationState.Success: return theme.Success;
                case AdvValidationState.Warning: return theme.Warning;
                case AdvValidationState.Error:   return theme.Error;
                default: return Color.Empty;
            }
        }

        /// <summary>
        /// 애드온(아이콘·접두/접미·지우기·검증) 자리를 좌우 끝에서 잘라내고,
        /// 남은 가운데를 안쪽 입력창에 준다. 각 요소의 사각형은 필드에 저장해 그리기·히트에 쓴다.
        /// </summary>
        private void PerformAddonLayout()
        {
            var content = ContentBounds;
            int glyph = Math.Min(16, Math.Max(1, content.Height));
            int gy = content.Top + (content.Height - glyph) / 2;

            _leadingRect = _prefixRect = _suffixRect = Rectangle.Empty;
            _trailingRect = _clearRect = _validationRect = Rectangle.Empty;

            // 왼쪽: 아이콘 → 접두 문구
            int left = content.Left;
            if (_leadingIcon != null)
            {
                _leadingRect = new Rectangle(left, gy, glyph, glyph);
                left += glyph + AddonGap;
            }
            if (_prefix.Length > 0)
            {
                int w = TextRenderer.MeasureText(_prefix, Font).Width;
                _prefixRect = new Rectangle(left, content.Top, w, content.Height);
                left += w + AddonGap;
            }

            // 오른쪽: (오른→왼) 검증 → 지우기 → 후행 아이콘 → 접미 문구.
            // 지우기 자리는 글자 유무와 상관없이 예약해 입력 중 폭이 흔들리지 않게 한다.
            int right = content.Right;
            if (_validation != AdvValidationState.None)
            {
                _validationRect = new Rectangle(right - glyph, gy, glyph, glyph);
                right -= glyph + AddonGap;
            }
            if (_showClearButton && !ReadOnly)
            {
                _clearRect = new Rectangle(right - glyph, gy, glyph, glyph);
                right -= glyph + AddonGap;
            }
            if (_trailingIcon != null)
            {
                _trailingRect = new Rectangle(right - glyph, gy, glyph, glyph);
                right -= glyph + AddonGap;
            }
            if (_suffix.Length > 0)
            {
                int w = TextRenderer.MeasureText(_suffix, Font).Width;
                _suffixRect = new Rectangle(right - w, content.Top, w, content.Height);
                right -= w + AddonGap;
            }

            var area = new Rectangle(left, content.Top, Math.Max(1, right - left), content.Height);
            if (!_inner.Multiline)
            {
                int h = _inner.PreferredHeight;
                if (h < area.Height) area = new Rectangle(area.X, area.Y + (area.Height - h) / 2, area.Width, h);
            }
            _inner.Bounds = area;
        }

        private bool ClearVisible
        {
            get { return _showClearButton && !ReadOnly && Enabled && _inner.TextLength > 0 && !_clearRect.IsEmpty; }
        }

        private void DrawAddons(Graphics g, AdvTheme theme)
        {
            var oldSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color muted = Enabled ? theme.TextMuted : theme.TextDisabled;

            if (_leadingIcon != null && !_leadingRect.IsEmpty) DrawIconImage(g, _leadingIcon, _leadingRect);
            if (_trailingIcon != null && !_trailingRect.IsEmpty) DrawIconImage(g, _trailingIcon, _trailingRect);

            const TextFormatFlags baseFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
                                            | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            if (_prefix.Length > 0 && !_prefixRect.IsEmpty)
                TextRenderer.DrawText(g, _prefix, Font, _prefixRect, muted, baseFlags | TextFormatFlags.Left);
            if (_suffix.Length > 0 && !_suffixRect.IsEmpty)
                TextRenderer.DrawText(g, _suffix, Font, _suffixRect, muted, baseFlags | TextFormatFlags.Right);

            if (ClearVisible) DrawClear(g, theme);
            if (!_validationRect.IsEmpty) DrawValidationGlyph(g, _validation);

            g.SmoothingMode = oldSmoothing;
        }

        /// <summary>아이콘을 비율을 지키며 rect 안에 가운데 맞춰 그린다.</summary>
        private static void DrawIconImage(Graphics g, Image img, Rectangle rect)
        {
            if (img.Width <= 0 || img.Height <= 0) return;

            float scale = Math.Min((float)rect.Width / img.Width, (float)rect.Height / img.Height);
            int w = Math.Max(1, (int)(img.Width * scale));
            int h = Math.Max(1, (int)(img.Height * scale));
            var dest = new Rectangle(rect.Left + (rect.Width - w) / 2, rect.Top + (rect.Height - h) / 2, w, h);

            var old = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(img, dest);
            g.InterpolationMode = old;
        }

        private void DrawClear(Graphics g, AdvTheme theme)
        {
            var r = _clearRect;
            if (_clearHover)
                using (var b = new SolidBrush(theme.SurfaceHover))
                using (var path = AdvGraphics.CreateRoundedRect(r, r.Width / 2))
                    g.FillPath(b, path);

            var box = Rectangle.Inflate(r, -r.Width / 4, -r.Height / 4);
            Color c = _clearHover ? theme.Text : theme.TextMuted;
            using (var pen = new Pen(c, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pen, box.Left, box.Top, box.Right, box.Bottom);
                g.DrawLine(pen, box.Left, box.Bottom, box.Right, box.Top);
            }
        }

        private void DrawValidationGlyph(Graphics g, AdvValidationState state)
        {
            Color c = ValidationColor(state, EffectiveTheme);
            var r = _validationRect;
            var box = Rectangle.Inflate(r, -r.Width / 5, -r.Height / 5);
            using (var pen = new Pen(c, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (state == AdvValidationState.Success)
                {
                    // 체크 표시
                    var pts = new[]
                    {
                        new Point(box.Left, box.Top + box.Height * 3 / 5),
                        new Point(box.Left + box.Width * 2 / 5, box.Bottom),
                        new Point(box.Right, box.Top)
                    };
                    g.DrawLines(pen, pts);
                }
                else
                {
                    // 느낌표 (경고·오류는 색으로 구분한다)
                    int cx = r.Left + r.Width / 2;
                    g.DrawLine(pen, cx, box.Top, cx, box.Top + box.Height * 3 / 5);
                    using (var b = new SolidBrush(c))
                        g.FillEllipse(b, cx - 1, box.Bottom - 2, 3, 3);
                }
            }
        }

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
            if (_clearHover) { _clearHover = false; Cursor = Cursors.Default; Invalidate(); }

            // 커서가 안쪽 TextBox로 들어갔을 뿐이면 컨트롤을 벗어난 것이 아니므로
            // 호버를 유지하고 MouseLeave도 올리지 않는다
            if (MouseStillInside) return;

            base.OnMouseLeave(e);
        }

        #endregion

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool over = ClearVisible && _clearRect.Contains(e.Location);
            if (over != _clearHover)
            {
                _clearHover = over;
                Cursor = over ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            // 지우기(×) 클릭은 캐럿을 옮기지 않고 내용만 비운다
            if (e.Button == MouseButtons.Left && ClearVisible && _clearRect.Contains(e.Location))
            {
                _inner.Clear();
                _inner.Focus();
                _clearHover = false;
                Invalidate();
                return;
            }

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
            else if (_validation != AdvValidationState.None) border = ValidationColor(_validation, theme);
            else if (ShowsFocusVisual) border = theme.BorderFocus;
            else border = AdvGraphics.Blend(theme.Border, theme.BorderHover, HoverAmount);

            AdvFrameRenderer.Draw(e.Graphics, bounds, theme, EffectiveCorners, EffectiveBorderWidth,
                                  fill, Color.Empty, border, CurrentGlow, CurrentElevation,
                                  EffectiveBorderDash);

            DrawAddons(e.Graphics, theme);

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
            // 애드온(아이콘·접두/접미·지우기·검증) 자리를 잘라내고 남은 가운데를 입력창에 준다.
            PerformAddonLayout();
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

    /// <summary>입력창의 검증 상태. 테두리 색과 오른쪽 상태 아이콘으로 표시된다.</summary>
    public enum AdvValidationState
    {
        /// <summary>표시 없음.</summary>
        None,
        /// <summary>성공(초록·체크).</summary>
        Success,
        /// <summary>경고(주황·느낌표).</summary>
        Warning,
        /// <summary>오류(빨강·느낌표).</summary>
        Error
    }

    /// <summary>AdvTextBox가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class AdvTextBoxOptions : AdvOptions   // 파생 입력 컨트롤(마스크·자동완성)이 파사드를 확장한다
    {
        private readonly AdvTextBox _owner;

        internal AdvTextBoxOptions(AdvTextBox owner) : base(owner.Styling, owner.Palette)
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

        [DefaultValue(false)]
        [Description("입력을 막고 읽기 전용으로 만들지 여부입니다.")]
        public bool ReadOnly
        {
            get { return _owner.ReadOnly; }
            set { _owner.ReadOnly = value; }
        }

        [DefaultValue(false)]
        [Description("여러 줄을 입력받을지 여부입니다. 켜면 높이를 직접 지정할 수 있습니다.")]
        public bool Multiline
        {
            get { return _owner.Multiline; }
            set { _owner.Multiline = value; }
        }

        [DefaultValue(32767)]
        [Description("입력할 수 있는 최대 글자 수입니다.")]
        public int MaxLength
        {
            get { return _owner.MaxLength; }
            set { _owner.MaxLength = value; }
        }

        [DefaultValue('\0')]
        [Description("입력한 글자 대신 표시할 문자입니다. 비워 두면 글자가 그대로 보입니다.")]
        public char PasswordChar
        {
            get { return _owner.PasswordChar; }
            set { _owner.PasswordChar = value; }
        }

        [DefaultValue(false)]
        [Description("OS가 정한 암호 문자를 쓸지 여부입니다. 켜면 PasswordChar보다 우선합니다.")]
        public bool UseSystemPasswordChar
        {
            get { return _owner.UseSystemPasswordChar; }
            set { _owner.UseSystemPasswordChar = value; }
        }

        [DefaultValue(HorizontalAlignment.Left)]
        [Description("글자를 왼쪽·가운데·오른쪽 중 어디에 맞출지입니다.")]
        public HorizontalAlignment TextAlign
        {
            get { return _owner.TextAlign; }
            set { _owner.TextAlign = value; }
        }

        [DefaultValue(ScrollBars.None)]
        [Description("여러 줄일 때 표시할 스크롤 막대입니다. Multiline이 꺼져 있으면 무시됩니다.")]
        public ScrollBars ScrollBars
        {
            get { return _owner.ScrollBars; }
            set { _owner.ScrollBars = value; }
        }

        [DefaultValue(null)]
        [Description("입력창 왼쪽 안에 표시할 아이콘입니다. 비율을 유지하며 맞춰집니다.")]
        public Image LeadingIcon
        {
            get { return _owner.LeadingIcon; }
            set { _owner.LeadingIcon = value; }
        }

        [DefaultValue(null)]
        [Description("입력창 오른쪽 안에 표시할 아이콘입니다. 비율을 유지하며 맞춰집니다.")]
        public Image TrailingIcon
        {
            get { return _owner.TrailingIcon; }
            set { _owner.TrailingIcon = value; }
        }

        [DefaultValue("")]
        [Description("글자 앞에 흐리게 붙이는 고정 문구입니다. 예: \"https://\", \"$\".")]
        public string Prefix
        {
            get { return _owner.Prefix; }
            set { _owner.Prefix = value; }
        }

        [DefaultValue("")]
        [Description("글자 뒤에 흐리게 붙이는 고정 문구입니다. 예: \".com\", \"kg\".")]
        public string Suffix
        {
            get { return _owner.Suffix; }
            set { _owner.Suffix = value; }
        }

        [DefaultValue(false)]
        [Description("글자가 있을 때 오른쪽에 지우기(×) 버튼을 표시할지 여부입니다.")]
        public bool ShowClearButton
        {
            get { return _owner.ShowClearButton; }
            set { _owner.ShowClearButton = value; }
        }

        [DefaultValue(AdvValidationState.None)]
        [Description("검증 상태입니다. 테두리 색과 오른쪽 상태 아이콘으로 표시됩니다.")]
        public AdvValidationState ValidationState
        {
            get { return _owner.ValidationState; }
            set { _owner.ValidationState = value; }
        }
    }
}
