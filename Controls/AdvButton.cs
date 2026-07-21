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
    /// <summary>버튼의 시각적 강조 단계.</summary>
    public enum AdvButtonKind
    {
        /// <summary>강조색으로 채운다. 화면의 주 동작에 쓴다.</summary>
        Filled,
        /// <summary>테두리와 텍스트만 표시한다.</summary>
        Outline,
        /// <summary>평소에는 텍스트만 보이고 호버 시에만 배경이 생긴다.</summary>
        Ghost
    }

    /// <summary>버튼 크기 단계. 글자 크기와 높이를 함께 키우거나 줄인다.</summary>
    public enum AdvButtonSize
    {
        /// <summary>작게(글꼴 0.85배).</summary>
        Small,
        /// <summary>보통(기본 글꼴).</summary>
        Medium,
        /// <summary>크게(글꼴 1.2배).</summary>
        Large
    }

    [ToolboxItem(true)]
    [DefaultEvent("Click")]
    [Description("테마를 따르는 커스텀 그리기 버튼입니다.")]
    public class AdvButton : AdvControlBase, IButtonControl
    {
        private const int ImageTextGap = 6;

        private const int SplitWidth = 22;

        private AdvButtonKind _kind = AdvButtonKind.Filled;
        private Color _context = Color.Empty;
        private DialogResult _dialogResult = DialogResult.None;
        private bool _isDefault;
        private Image _image;
        private TextImageRelation _textImageRelation = TextImageRelation.ImageBeforeText;
        private AdvButtonOptions _options;

        // 버튼/상태: 로딩 스피너·크기 단계·분할 드롭다운
        private readonly AdvAnimator _loadingAnim;
        private bool _isLoading;
        private AdvButtonSize _size = AdvButtonSize.Medium;
        private Font _sizeFont;                 // Medium이 아닐 때만 만든다(파생 글꼴)
        private bool _splitDropDown;
        private ContextMenuStrip _splitMenu;
        private Rectangle _splitRect = Rectangle.Empty;
        private bool _swallowClick;             // 분할 영역 클릭이면 본문 Click을 삼킨다

        /// <summary>분할 버튼의 드롭다운(오른쪽 화살표) 영역을 눌렀을 때 발생한다.</summary>
        [Category("Action")]
        [Description("분할 버튼의 드롭다운 영역을 눌렀을 때 발생합니다.")]
        public event EventHandler DropDownClick;

        public AdvButton()
        {
            TabStop = true;
            _loadingAnim = new AdvAnimator(0);
            _loadingAnim.ValueChanged += OnLoadingTick;
        }

        /// <summary>크기 단계가 적용된 글꼴. 측정·그리기는 모두 이 글꼴을 쓴다.</summary>
        private Font EffectiveFont
        {
            get { return _size == AdvButtonSize.Medium ? Font : (_sizeFont ?? Font); }
        }

        private void RebuildSizeFont()
        {
            if (_sizeFont != null) { _sizeFont.Dispose(); _sizeFont = null; }
            if (_size == AdvButtonSize.Medium) return;

            float scale = _size == AdvButtonSize.Small ? 0.85f : 1.20f;
            _sizeFont = new Font(Font.FontFamily, Font.Size * scale, Font.Style);
        }

        /// <summary>
        /// 크기 단계가 더하는 여백. 글꼴 배율만으로는 높이 차이가 작아 사다리가 흐릿하므로,
        /// 사용자 Padding은 건드리지 않고 내용 영역에만 더해 준다.
        /// </summary>
        private Padding SizeInset
        {
            get
            {
                switch (_size)
                {
                    case AdvButtonSize.Small: return new Padding(2, 0, 2, 0);
                    case AdvButtonSize.Large: return new Padding(8, 5, 8, 5);
                    default: return Padding.Empty;
                }
            }
        }

        private void OnLoadingTick(object sender, EventArgs e)
        {
            if (!IsDisposed && IsHandleCreated) Invalidate();
        }

        private void UpdateLoadingSpin()
        {
            bool run = _isLoading && !DesignMode && IsHandleCreated && Visible;
            if (run && !_loadingAnim.IsLooping) _loadingAnim.StartLoop(700);
            else if (!run && _loadingAnim.IsLooping) _loadingAnim.StopLoop();
        }

        protected override Size DefaultSize
        {
            // 글로우 여백(각 변 3px)을 감안한 크기
            get { return new Size(100, 34); }
        }

        protected override Padding DefaultPadding
        {
            get { return new Padding(14, 4, 14, 4); }
        }

        #region IButtonControl — 폼의 AcceptButton / CancelButton 지원

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(DialogResult.None)]
        [Description("이 버튼을 눌렀을 때 대화 상자가 돌려줄 결과입니다.")]
        public DialogResult DialogResult
        {
            get { return _dialogResult; }
            set { _dialogResult = value; }
        }

        /// <summary>폼이 이 버튼을 기본 버튼으로 지정하거나 해제할 때 호출한다.</summary>
        public void NotifyDefault(bool value)
        {
            if (_isDefault == value) return;
            _isDefault = value;
            Invalidate();
        }

        /// <summary>이 버튼이 폼의 AcceptButton인지 여부. 기본 버튼은 테두리를 한 겹 더 그린다.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsDefault
        {
            get { return _isDefault; }
        }

        /// <summary>코드에서 클릭과 같은 동작을 일으킨다. 표준 Button과 같다.</summary>
        public void PerformClick()
        {
            if (!Enabled || !Visible) return;
            OnClick(EventArgs.Empty);
        }

        /// <summary>
        /// 대화 상자 안의 버튼이면 누를 때 폼의 DialogResult를 설정한다.
        /// 표준 Button과 같은 동작이며, 이게 없으면 ShowDialog가 닫히지 않는다.
        /// </summary>
        protected override void OnClick(EventArgs e)
        {
            if (_isLoading) return;                                   // 로딩 중엔 클릭 무시
            if (_swallowClick) { _swallowClick = false; return; }      // 분할 영역 클릭은 본문 Click을 삼킨다

            if (_dialogResult != DialogResult.None)
            {
                var form = FindForm();
                if (form != null) form.DialogResult = _dialogResult;
            }
            base.OnClick(e);
        }

        #endregion

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvButtonKind.Filled)]
        [Description("버튼의 시각적 강조 단계입니다.")]
        public AdvButtonKind Kind
        {
            get { return _kind; }
            set
            {
                if (_kind == value) return;
                _kind = value;
                Invalidate();
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [Description("버튼의 강조 색입니다. 비워 두면 테마 강조색(Accent)을 따릅니다.")]
        public Color Context
        {
            get { return _context; }
            set
            {
                if (_context == value) return;
                _context = value;
                Invalidate();
            }
        }
        public bool ShouldSerializeContext() { return !_context.IsEmpty; }
        public void ResetContext() { Context = Color.Empty; }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(null)]
        [Description("버튼에 표시할 이미지입니다.")]
        public Image Image
        {
            get { return _image; }
            set
            {
                if (ReferenceEquals(_image, value)) return;
                _image = value;
                AdjustSize();
                Invalidate();
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(TextImageRelation.ImageBeforeText)]
        [Description("이미지와 글자를 어떻게 배치할지 정합니다.")]
        public TextImageRelation TextImageRelation
        {
            get { return _textImageRelation; }
            set
            {
                if (_textImageRelation == value) return;
                _textImageRelation = value;
                AdjustSize();
                Invalidate();
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("로딩 중임을 표시합니다. 스피너가 돌고 클릭이 막힙니다.")]
        public bool IsLoading
        {
            get { return _isLoading; }
            set
            {
                if (_isLoading == value) return;
                _isLoading = value;
                UpdateLoadingSpin();
                Cursor = _isLoading ? Cursors.Default : (UseHandCursor ? Cursors.Hand : Cursors.Default);
                Invalidate();
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvButtonSize.Medium)]
        [Description("버튼 크기 단계입니다. 글자 크기와 높이를 함께 조정합니다.")]
        public AdvButtonSize ButtonSize
        {
            get { return _size; }
            set
            {
                if (_size == value) return;
                _size = value;
                RebuildSizeFont();
                AdjustSize();
                ReapplyMinimumSize();
                Invalidate();
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("오른쪽에 드롭다운(화살표) 영역을 둘지 여부입니다. 그 영역을 누르면 DropDownClick이 발생합니다.")]
        public bool SplitDropDown
        {
            get { return _splitDropDown; }
            set
            {
                if (_splitDropDown == value) return;
                _splitDropDown = value;
                AdjustSize();
                ReapplyMinimumSize();
                Invalidate();
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(null)]
        [Description("분할 드롭다운 영역을 눌렀을 때 펼칠 메뉴입니다. 비워 두면 DropDownClick만 발생합니다.")]
        public ContextMenuStrip SplitMenu
        {
            get { return _splitMenu; }
            set { _splitMenu = value; }
        }

        /// <summary>
        /// 내용(이미지+글자)에 여백·테두리·글로우를 더한 크기.
        /// </summary>
        protected override bool IsClickable
        {
            get { return true; }
        }

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvButtonOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvButtonOptions(this)); }
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var content = MeasureContent();
            var chrome = ChromeSize;

            var inset = SizeInset;
            return new Size(
                content.Width + chrome.Width + inset.Horizontal + (_splitDropDown ? SplitWidth : 0),
                content.Height + chrome.Height + inset.Vertical);
        }

        /// <summary>
        /// 글자가 통째로 사라지지 않을 만큼의 높이는 지킨다.
        /// 폭은 줄임표가 처리하므로 제한하지 않는다(분할 화살표 자리만 확보).
        /// </summary>
        protected override Size MinimumContentSize
        {
            get { return new Size(_splitDropDown ? SplitWidth * 2 : 0,
                                  ChromeSize.Height + EffectiveFont.Height + SizeInset.Vertical); }
        }

        private Size MeasureContent()
        {
            var text = string.IsNullOrEmpty(Text)
                     ? Size.Empty
                     : TextRenderer.MeasureText(Text, EffectiveFont, new Size(int.MaxValue, int.MaxValue),
                                                TextFormatFlags.NoPrefix);

            var img = _image == null ? Size.Empty : _image.Size;

            if (img.IsEmpty) return text;
            if (text.IsEmpty) return img;

            switch (_textImageRelation)
            {
                case TextImageRelation.ImageAboveText:
                case TextImageRelation.TextAboveImage:
                    return new Size(Math.Max(img.Width, text.Width),
                                    img.Height + ImageTextGap + text.Height);

                case TextImageRelation.Overlay:
                    return new Size(Math.Max(img.Width, text.Width),
                                    Math.Max(img.Height, text.Height));

                default: // ImageBeforeText / TextBeforeImage
                    return new Size(img.Width + ImageTextGap + text.Width,
                                    Math.Max(img.Height, text.Height));
            }
        }

        /// <summary>내용 영역 안에서 이미지와 글자가 놓일 자리를 함께 계산한다.</summary>
        private void LayoutContent(Rectangle content, out Rectangle imageRect, out Rectangle textRect)
        {
            imageRect = Rectangle.Empty;
            textRect = content;

            if (_image == null) return;

            var img = _image.Size;

            if (string.IsNullOrEmpty(Text))
            {
                imageRect = Center(content, img);
                textRect = Rectangle.Empty;
                return;
            }

            var text = TextRenderer.MeasureText(Text, EffectiveFont, new Size(int.MaxValue, int.MaxValue),
                                                TextFormatFlags.NoPrefix);

            switch (_textImageRelation)
            {
                case TextImageRelation.ImageAboveText:
                {
                    int total = img.Height + ImageTextGap + text.Height;
                    int top = content.Top + (content.Height - total) / 2;
                    imageRect = new Rectangle(content.Left + (content.Width - img.Width) / 2, top,
                                              img.Width, img.Height);
                    textRect = new Rectangle(content.Left, top + img.Height + ImageTextGap,
                                             content.Width, text.Height);
                    break;
                }

                case TextImageRelation.TextAboveImage:
                {
                    int total = text.Height + ImageTextGap + img.Height;
                    int top = content.Top + (content.Height - total) / 2;
                    textRect = new Rectangle(content.Left, top, content.Width, text.Height);
                    imageRect = new Rectangle(content.Left + (content.Width - img.Width) / 2,
                                              top + text.Height + ImageTextGap, img.Width, img.Height);
                    break;
                }

                case TextImageRelation.TextBeforeImage:
                {
                    int total = text.Width + ImageTextGap + img.Width;
                    int left = content.Left + (content.Width - total) / 2;
                    textRect = new Rectangle(left, content.Top, text.Width, content.Height);
                    imageRect = Center(new Rectangle(left + text.Width + ImageTextGap, content.Top,
                                                     img.Width, content.Height), img);
                    break;
                }

                case TextImageRelation.Overlay:
                    imageRect = Center(content, img);
                    textRect = content;
                    break;

                default: // ImageBeforeText
                {
                    int total = img.Width + ImageTextGap + text.Width;
                    int left = content.Left + (content.Width - total) / 2;
                    imageRect = Center(new Rectangle(left, content.Top, img.Width, content.Height), img);
                    textRect = new Rectangle(left + img.Width + ImageTextGap, content.Top,
                                             text.Width, content.Height);
                    break;
                }
            }
        }

        private static Rectangle Center(Rectangle area, Size size)
        {
            return new Rectangle(
                area.Left + (area.Width - size.Width) / 2,
                area.Top + (area.Height - size.Height) / 2,
                size.Width, size.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = FrameBounds;
            var corners = EffectiveCorners;

            Color fill, fillEnd, border, foreColor;
            ResolveColors(theme, out fill, out fillEnd, out border, out foreColor);

            int bw = EffectiveBorderWidth;
            AdvFrameRenderer.Draw(g, bounds, theme, corners, bw, fill, fillEnd, border,
                                  CurrentGlow, CurrentElevation, EffectiveBorderDash, EffectiveGradientAngle);

            // 기본 버튼은 별도 링을 그리지 않는다 — 포커스 표시는 글로우로 일원화한다.
            // (예전 인셋 링은 포커스 시 자동으로 임시 기본버튼이 되며 흰색 링 아티팩트를 만들었다.)

            // 분할 드롭다운: 오른쪽 화살표 영역을 떼어 내고 구분선과 셰브런을 그린다
            int rightInset = 0;
            if (_splitDropDown)
            {
                _splitRect = new Rectangle(bounds.Right - bw - SplitWidth, bounds.Top + bw,
                                           SplitWidth, Math.Max(0, bounds.Height - bw * 2));
                using (var pen = new Pen(Color.FromArgb(90, foreColor)))
                    g.DrawLine(pen, _splitRect.Left, _splitRect.Top + 4, _splitRect.Left, _splitRect.Bottom - 4);
                AdvGraphics.DrawChevron(g, _splitRect, AdvGraphics.ChevronDirection.Down, foreColor, 8, 4, 1.5f, 0);
                rightInset = SplitWidth;
            }
            else
            {
                _splitRect = Rectangle.Empty;
            }

            var inset = SizeInset;
            var content = new Rectangle(
                bounds.Left + bw + Padding.Left + inset.Left,
                bounds.Top + bw + Padding.Top + inset.Top,
                Math.Max(0, bounds.Width - bw * 2 - Padding.Horizontal - inset.Horizontal - rightInset),
                Math.Max(0, bounds.Height - bw * 2 - Padding.Vertical - inset.Vertical));

            // 로딩 중이면 스피너로 대체한다
            if (_isLoading)
            {
                DrawLoading(g, content, foreColor);
                base.OnPaint(e);
                return;
            }

            Rectangle imageRect, textRect;
            LayoutContent(content, out imageRect, out textRect);

            if (_image != null && imageRect.Width > 0)
            {
                if (Enabled) g.DrawImage(_image, imageRect);
                else ControlPaint.DrawImageDisabled(g, _image, imageRect.X, imageRect.Y, Color.Transparent);
            }

            if (!string.IsNullOrEmpty(Text) && textRect.Width > 0)
            {
                // 이미지가 없으면 내용 영역 전체에 가운데 정렬, 있으면 계산된 자리에 그린다
                var flags = TextFormatFlags.VerticalCenter
                          | TextFormatFlags.EndEllipsis
                          | TextFormatFlags.NoPrefix;

                if (_image == null || _textImageRelation == TextImageRelation.Overlay
                                   || _textImageRelation == TextImageRelation.ImageAboveText
                                   || _textImageRelation == TextImageRelation.TextAboveImage)
                    flags |= TextFormatFlags.HorizontalCenter;

                TextRenderer.DrawText(g, Text, EffectiveFont, textRect, foreColor, flags);
            }

            // 소비자가 붙인 Paint 핸들러가 위에 덧그릴 수 있도록 마지막에 호출한다
            base.OnPaint(e);
        }

        /// <summary>로딩 스피너(회전 원호)와, 글자가 있으면 그 오른쪽에 글자를 함께 그린다.</summary>
        private void DrawLoading(Graphics g, Rectangle content, Color color)
        {
            if (content.Width <= 0 || content.Height <= 0) return;

            int sz = Math.Max(6, Math.Min(content.Height, EffectiveFont.Height));
            bool hasText = !string.IsNullOrEmpty(Text);

            var text = hasText
                ? TextRenderer.MeasureText(Text, EffectiveFont, new Size(int.MaxValue, int.MaxValue),
                                           TextFormatFlags.NoPrefix)
                : Size.Empty;

            int total = hasText ? sz + ImageTextGap + text.Width : sz;
            int left = content.Left + Math.Max(0, (content.Width - total) / 2);

            var box = new Rectangle(left, content.Top + (content.Height - sz) / 2, sz, sz);
            int th = Math.Max(2, sz / 7);
            var arc = Rectangle.Inflate(box, -th, -th);

            float phase = DesignMode ? 0f : _loadingAnim.Value;
            if (arc.Width > 0 && arc.Height > 0)
                using (var pen = new Pen(color, th) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawArc(pen, arc.X, arc.Y, arc.Width, arc.Height, phase * 360f, 270f);

            if (hasText)
            {
                var tr = new Rectangle(left + sz + ImageTextGap, content.Top,
                                       content.Right - (left + sz + ImageTextGap), content.Height);
                TextRenderer.DrawText(g, Text, EffectiveFont, tr, color,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left
                    | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        private void ResolveColors(AdvTheme theme, out Color fill, out Color fillEnd,
                                   out Color border, out Color foreColor)
        {
            fillEnd = Color.Empty;

            if (!Enabled)
            {
                // Ghost는 활성 상태에서도 면·테두리가 없으므로 비활성이라고 생기면 안 된다
                fill = _kind == AdvButtonKind.Ghost ? Color.Transparent : theme.DisabledFill;
                border = _kind == AdvButtonKind.Outline ? theme.Border : fill;
                foreColor = theme.TextDisabled;
                return;
            }

            var p = AdvContextPalette.Resolve(_context, theme);
            // 색을 비우면 테마 강조색(Accent 세트)을 그대로 쓴다.
            bool neutral = _context.IsEmpty;
            float t = HoverAmount;

            switch (_kind)
            {
                case AdvButtonKind.Filled:
                    fill = IsPressed
                         ? p.SolidPressed
                         : AdvGraphics.Blend(p.Solid, p.SolidHover, t);
                    // 그라데이션 끝색은 테마 강조색을 따를 때(색 비움)만 쓴다.
                    fillEnd = neutral ? theme.AccentGradientEnd : Color.Empty;
                    border = fill;
                    foreColor = p.OnSolid;
                    break;

                case AdvButtonKind.Outline:
                    if (neutral)
                    {
                        // 기존 중립 아웃라인 동작 유지(회색 테두리 + 본문색).
                        fill = IsPressed
                             ? theme.SurfacePressed
                             : AdvGraphics.Blend(theme.Surface, theme.SurfaceHover, t);
                        fillEnd = theme.SurfaceGradientEnd;
                        border = IsPressed
                               ? theme.BorderHover
                               : AdvGraphics.Blend(theme.Border, theme.BorderHover, t);
                        foreColor = theme.Text;
                    }
                    else
                    {
                        // 컨텍스트 아웃라인 — 평소엔 색 테두리+글자, 호버/누름에 채운다.
                        fill = IsPressed ? p.SolidPressed : AdvGraphics.Blend(theme.Surface, p.Solid, t);
                        fillEnd = Color.Empty;
                        border = p.Solid;
                        foreColor = (IsPressed || t >= 0.5f) ? p.OnSolid : p.Solid;
                    }
                    break;

                default: // Ghost
                    fill = IsPressed
                         ? theme.SurfacePressed
                         : AdvGraphics.Blend(Color.Transparent, theme.SurfaceHover, t);
                    border = Color.Transparent;
                    foreColor = neutral ? theme.Text : p.Solid;
                    break;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (_isLoading) return;   // 로딩 중엔 눌림·클릭 없음

            // 분할 드롭다운 영역을 누르면 본문 대신 드롭다운을 연다
            if (_splitDropDown && e.Button == MouseButtons.Left && _splitRect.Contains(e.Location))
            {
                _swallowClick = true;
                if (!Focused) Focus();
                OnDropDownClick();
                return;
            }

            _swallowClick = false;
            if (e.Button == MouseButtons.Left && !Focused) Focus();
            base.OnMouseDown(e);
        }

        /// <summary>분할 드롭다운을 연다. DropDownClick을 올리고, 메뉴가 지정돼 있으면 버튼 아래에 펼친다.</summary>
        protected virtual void OnDropDownClick()
        {
            var h = DropDownClick;
            if (h != null) h(this, EventArgs.Empty);

            if (_splitMenu != null && !_splitMenu.IsDisposed)
                _splitMenu.Show(this, new Point(0, Height));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && !_isLoading) IsPressed = true;
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && IsPressed)
            {
                IsPressed = false;
                OnClick(EventArgs.Empty);
            }
            base.OnKeyUp(e);
        }

        /// <summary>
        /// 포커스가 다른 곳으로 넘어가면 KeyUp이 오지 않아 눌림 상태가 남는다.
        /// </summary>
        protected override void OnLostFocus(EventArgs e)
        {
            IsPressed = false;
            base.OnLostFocus(e);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            RebuildSizeFont();      // 크기 단계 글꼴을 새 기준 글꼴에서 다시 만든다
            base.OnFontChanged(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateLoadingSpin();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdateLoadingSpin();    // 숨겨지면 스피너 루프를 멈춘다
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _loadingAnim.ValueChanged -= OnLoadingTick;
                _loadingAnim.Dispose();
                if (_sizeFont != null) { _sizeFont.Dispose(); _sizeFont = null; }
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>AdvButton이 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvButtonOptions : AdvOptions
    {
        private readonly AdvButton _owner;

        internal AdvButtonOptions(AdvButton owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(AdvButtonKind.Filled)]
        [Description("버튼의 시각적 강조 단계입니다.")]
        public AdvButtonKind Kind
        {
            get { return _owner.Kind; }
            set { _owner.Kind = value; }
        }

        [Description("버튼의 강조 색입니다. 비워 두면 테마 강조색(Accent)을 따릅니다.")]
        public Color Context
        {
            get { return _owner.Context; }
            set { _owner.Context = value; }
        }
        public bool ShouldSerializeContext() { return _owner.ShouldSerializeContext(); }
        public void ResetContext() { _owner.ResetContext(); }

        [DefaultValue(true)]
        [Description("이 버튼 위에서 손 모양 커서를 보일지 여부입니다.")]
        public bool UseHandCursor
        {
            get { return _owner.UseHandCursor; }
            set { _owner.UseHandCursor = value; }
        }

        [DefaultValue(null)]
        [Description("버튼에 표시할 이미지입니다.")]
        public Image Image
        {
            get { return _owner.Image; }
            set { _owner.Image = value; }
        }

        [DefaultValue(TextImageRelation.ImageBeforeText)]
        [Description("이미지와 글자를 어떻게 배치할지 정합니다.")]
        public TextImageRelation TextImageRelation
        {
            get { return _owner.TextImageRelation; }
            set { _owner.TextImageRelation = value; }
        }

        [DefaultValue(DialogResult.None)]
        [Description("이 버튼을 눌렀을 때 대화 상자가 돌려줄 결과입니다.")]
        public DialogResult DialogResult
        {
            get { return _owner.DialogResult; }
            set { _owner.DialogResult = value; }
        }

        [DefaultValue(false)]
        [Description("로딩 중임을 표시합니다. 스피너가 돌고 클릭이 막힙니다.")]
        public bool IsLoading
        {
            get { return _owner.IsLoading; }
            set { _owner.IsLoading = value; }
        }

        [DefaultValue(AdvButtonSize.Medium)]
        [Description("버튼 크기 단계입니다. 글자 크기와 높이를 함께 조정합니다.")]
        public AdvButtonSize ButtonSize
        {
            get { return _owner.ButtonSize; }
            set { _owner.ButtonSize = value; }
        }

        [DefaultValue(false)]
        [Description("오른쪽에 드롭다운(화살표) 영역을 둘지 여부입니다. 그 영역을 누르면 DropDownClick이 발생합니다.")]
        public bool SplitDropDown
        {
            get { return _owner.SplitDropDown; }
            set { _owner.SplitDropDown = value; }
        }

        [DefaultValue(null)]
        [Description("분할 드롭다운 영역을 눌렀을 때 펼칠 메뉴입니다. 비워 두면 DropDownClick만 발생합니다.")]
        public ContextMenuStrip SplitMenu
        {
            get { return _owner.SplitMenu; }
            set { _owner.SplitMenu = value; }
        }
    }
}
