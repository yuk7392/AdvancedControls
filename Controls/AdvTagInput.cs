using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Controls.Internal;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>태그 이벤트 인자.</summary>
    public class AdvTagEventArgs : EventArgs
    {
        public string Text { get; private set; }
        public int Index { get; private set; }
        public AdvTagEventArgs(string text, int index) { Text = text; Index = index; }
    }

    /// <summary>태그 추가 직전 취소할 수 있는 이벤트 인자.</summary>
    public class AdvTagAddingEventArgs : EventArgs
    {
        public string Text { get; private set; }
        /// <summary>true로 두면 추가하지 않는다(형식 검증 실패 등).</summary>
        public bool Cancel { get; set; }
        public AdvTagAddingEventArgs(string text) { Text = text; }
    }

    /// <summary>
    /// 태그(칩) 입력창. Enter나 쉼표로 입력을 태그로 확정하고, 태그는 둥근 칩으로
    /// 컨트롤 안에 직접 그린다. 칩의 X나 빈 입력에서 Backspace로 지운다.
    /// 칩이 줄을 넘치면 아래로 감싸며 컨트롤 높이가 내용에 맞게 자란다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("TagAdded")]
    [DefaultProperty("AdvancedControlOptions")]
    [Description("Enter·쉼표로 태그를 추가하는 칩 입력창입니다.")]
    public class AdvTagInput : AdvControlBase
    {
        // 96dpi 논리 치수
        private const int ChipPadX = 10;    // 칩 좌우 안쪽 여백
        private const int ChipPadY = 4;     // 칩 상하 안쪽 여백
        private const int ChipGap = 6;      // 칩 사이·줄 사이 간격
        private const int CloseGap = 6;     // 글자와 X 사이
        private const int MinEditorW = 60;  // 입력창 최소 폭(모자라면 다음 줄)

        private readonly List<string> _tags = new List<string>();
        private readonly AdvInnerTextBox _inner;
        private bool _allowDuplicates;
        private int _maxTags;               // 0 = 제한 없음
        private int _hoverClose = -1;       // X 호버 중인 칩
        private readonly List<Rectangle> _chipRects = new List<Rectangle>();
        private readonly List<Rectangle> _closeRects = new List<Rectangle>();
        private bool _layouting;
        private AdvTagInputOptions _options;

        /// <summary>태그가 추가되기 직전 발생한다. Cancel로 막을 수 있다.</summary>
        [Category("Behavior")]
        [Description("태그가 추가되기 직전 발생합니다. Cancel로 막을 수 있습니다.")]
        public event EventHandler<AdvTagAddingEventArgs> TagAdding;

        /// <summary>태그가 추가된 뒤 발생한다.</summary>
        [Category("Behavior")]
        [Description("태그가 추가된 뒤 발생합니다.")]
        public event EventHandler<AdvTagEventArgs> TagAdded;

        /// <summary>태그가 제거된 뒤 발생한다.</summary>
        [Category("Behavior")]
        [Description("태그가 제거된 뒤 발생합니다.")]
        public event EventHandler<AdvTagEventArgs> TagRemoved;

        public AdvTagInput()
        {
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;

            _inner = new AdvInnerTextBox
            {
                Host = this,
                BorderStyle = BorderStyle.None,
                TabStop = false,
                AutoSize = false
            };
            _inner.KeyDown += InnerKeyDown;
            _inner.KeyPress += InnerKeyPress;
            _inner.GotFocus += InnerFocusChanged;
            _inner.LostFocus += InnerLostFocus;
            Controls.Add(_inner);
        }

        protected override Size DefaultSize
        {
            get { return new Size(260, 36); }
        }

        protected override Padding DefaultPadding
        {
            get { return new Padding(8, 6, 8, 6); }
        }

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvTagInputOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvTagInputOptions(this)); }
        }

        /// <summary>현재 태그들(읽기 전용). 조작은 AddTag·RemoveTagAt·ClearTags로 한다.</summary>
        [Browsable(false)]
        public IList<string> Tags
        {
            get { return _tags.AsReadOnly(); }
        }

        [Browsable(false)]
        public int TagCount { get { return _tags.Count; } }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("같은 태그(대소문자 무시)를 중복으로 허용할지 여부입니다.")]
        public bool AllowDuplicates
        {
            get { return _allowDuplicates; }
            set { _allowDuplicates = value; }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(0)]
        [Description("최대 태그 수입니다. 0이면 제한이 없습니다.")]
        public int MaxTags
        {
            get { return _maxTags; }
            set { _maxTags = value < 0 ? 0 : value; }
        }

        /// <summary>입력 중인(아직 확정 안 된) 텍스트.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string PendingText
        {
            get { return _inner.Text; }
            set { _inner.Text = value ?? string.Empty; }
        }

        // ── 태그 조작 ─────────────────────────────────────────────────

        /// <summary>
        /// 태그를 추가한다. 빈 문자열·중복(허용 안 할 때)·최대 개수 초과·TagAdding 취소면
        /// 추가하지 않고 false를 돌려준다.
        /// </summary>
        public bool AddTag(string text)
        {
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0) return false;
            if (_maxTags > 0 && _tags.Count >= _maxTags) return false;
            if (!_allowDuplicates && ContainsTag(text)) return false;

            var adding = TagAdding;
            if (adding != null)
            {
                var args = new AdvTagAddingEventArgs(text);
                adding(this, args);
                if (args.Cancel) return false;
            }

            _tags.Add(text);
            RelayoutAndGrow();

            var added = TagAdded;
            if (added != null) added(this, new AdvTagEventArgs(text, _tags.Count - 1));
            return true;
        }

        /// <summary>태그 하나를 지운다.</summary>
        public void RemoveTagAt(int index)
        {
            if (index < 0 || index >= _tags.Count) return;
            string text = _tags[index];
            _tags.RemoveAt(index);
            if (_hoverClose == index) _hoverClose = -1;
            RelayoutAndGrow();

            var h = TagRemoved;
            if (h != null) h(this, new AdvTagEventArgs(text, index));
        }

        /// <summary>모든 태그를 지운다(입력 중 텍스트는 그대로).</summary>
        public void ClearTags()
        {
            while (_tags.Count > 0) RemoveTagAt(_tags.Count - 1);
        }

        /// <summary>태그가 이미 있는지(대소문자 무시).</summary>
        public bool ContainsTag(string text)
        {
            foreach (var t in _tags)
                if (string.Equals(t, text, StringComparison.CurrentCultureIgnoreCase)) return true;
            return false;
        }

        // ── 입력 처리 ─────────────────────────────────────────────────

        private void InnerKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommitPending();
                e.Handled = true;
                e.SuppressKeyPress = true;   // 딩 소리 방지
            }
            else if (e.KeyCode == Keys.Back && _inner.TextLength == 0 && _tags.Count > 0)
            {
                RemoveTagAt(_tags.Count - 1);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void InnerKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == ',')
            {
                CommitPending();
                e.Handled = true;   // 쉼표 자체는 입력하지 않는다
            }
        }

        /// <summary>입력 중 텍스트를 태그로 확정한다. 실패(중복 등)해도 텍스트는 남긴다.</summary>
        private void CommitPending()
        {
            string text = _inner.Text.Trim();
            if (text.Length == 0) { _inner.Text = string.Empty; return; }
            if (AddTag(text)) _inner.Text = string.Empty;
        }

        private void InnerFocusChanged(object sender, EventArgs e) { Invalidate(); }

        private void InnerLostFocus(object sender, EventArgs e)
        {
            CommitPending();   // 포커스가 떠날 때 입력 중이던 것을 확정한다
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            _inner.Focus();
            base.OnGotFocus(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            for (int i = 0; i < _closeRects.Count; i++)
            {
                if (_closeRects[i].Contains(e.Location)) { RemoveTagAt(i); return; }
            }
            _inner.Focus();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hover = -1;
            for (int i = 0; i < _closeRects.Count; i++)
                if (_closeRects[i].Contains(e.Location)) { hover = i; break; }
            if (hover != _hoverClose)
            {
                _hoverClose = hover;
                Cursor = hover >= 0 ? Cursors.Hand : Cursors.IBeam;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverClose != -1) { _hoverClose = -1; Cursor = Cursors.Default; Invalidate(); }
        }

        // ── 레이아웃 ──────────────────────────────────────────────────

        private int ChipHeight
        {
            get { return Font.Height + AdvGraphics.Scale(this, ChipPadY) * 2; }
        }

        private Size MeasureChip(string text)
        {
            var ts = TextRenderer.MeasureText(text, Font);
            int padX = AdvGraphics.Scale(this, ChipPadX);
            int closeW = Font.Height;   // X 글리프 자리
            return new Size(ts.Width + padX * 2 + AdvGraphics.Scale(this, CloseGap) + closeW, ChipHeight);
        }

        /// <summary>
        /// 칩들을 흘려 배치하고 입력창을 마지막 칩 뒤(모자라면 다음 줄)에 앉힌다.
        /// 내용에 필요한 높이가 지금과 다르면 컨트롤 높이를 맞춘다(아래로 자람).
        /// </summary>
        private void RelayoutAndGrow()
        {
            if (_layouting) return;
            _layouting = true;
            try
            {
                _chipRects.Clear();
                _closeRects.Clear();

                var c = ContentBounds;
                int gap = AdvGraphics.Scale(this, ChipGap);
                int closeW = Font.Height;
                int padX = AdvGraphics.Scale(this, ChipPadX);
                int chipH = ChipHeight;
                int x = c.Left, y = c.Top;

                foreach (var tag in _tags)
                {
                    var size = MeasureChip(tag);
                    if (size.Width > c.Width) size.Width = c.Width;          // 초장문 칩은 줄임표
                    if (x > c.Left && x + size.Width > c.Right) { x = c.Left; y += chipH + gap; }

                    var rect = new Rectangle(x, y, size.Width, chipH);
                    _chipRects.Add(rect);
                    _closeRects.Add(new Rectangle(rect.Right - padX - closeW + AdvGraphics.Scale(this, 2),
                                                  rect.Top + (chipH - closeW) / 2, closeW, closeW));
                    x = rect.Right + gap;
                }

                // 입력창: 남은 폭이 최소보다 좁으면 다음 줄 전체
                int minW = AdvGraphics.Scale(this, MinEditorW);
                if (x > c.Left && c.Right - x < minW) { x = c.Left; y += chipH + gap; }
                int editH = _inner.PreferredHeight;
                _inner.Bounds = new Rectangle(x, y + Math.Max(0, (chipH - editH) / 2),
                                              Math.Max(minW, c.Right - x), editH);

                // 필요 높이에 맞춰 아래로 자란다(칩 줄 수 변화)
                int needed = y + chipH - c.Top + ChromeSize.Height;
                if (Height != needed && needed >= MinimumContentSize.Height) Height = needed;

                Invalidate();
            }
            finally { _layouting = false; }
        }

        /// <summary>한 줄(칩 1줄)이 눌리지 않을 최소 높이.</summary>
        protected override Size MinimumContentSize
        {
            get { return new Size(0, ChipHeight + ChromeSize.Height); }
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); RelayoutAndGrow(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); RelayoutAndGrow(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); OnThemeChanged(); RelayoutAndGrow(); }

        // ── 그리기 ────────────────────────────────────────────────────

        protected override bool ShowsFocusVisual
        {
            get { return ContainsFocus; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            var bounds = FrameBounds;

            Color border;
            if (!Enabled) border = theme.Border;
            else if (ContainsFocus) border = theme.BorderFocus;
            else border = AdvGraphics.Blend(theme.Border, theme.BorderHover, HoverAmount);

            AdvFrameRenderer.Draw(g, bounds, theme, EffectiveCorners, EffectiveBorderWidth,
                                  Enabled ? theme.InputBackground : theme.InputBackgroundDisabled,
                                  Color.Empty, border, CurrentGlow, CurrentElevation, EffectiveBorderDash);

            var palette = AdvContextPalette.Resolve(Color.Empty, theme);   // 테마 강조색 파생
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int padX = AdvGraphics.Scale(this, ChipPadX);
            for (int i = 0; i < _chipRects.Count; i++)
            {
                var r = _chipRects[i];
                using (var path = AdvGraphics.CreateRoundedRect(r, r.Height / 2))
                {
                    using (var b = new SolidBrush(Enabled ? palette.SubtleBg : theme.DisabledFill))
                        g.FillPath(b, path);
                    using (var pen = new Pen(Enabled ? palette.SubtleBorder : theme.Border))
                        g.DrawPath(pen, path);
                }

                var textRect = Rectangle.FromLTRB(r.Left + padX, r.Top, _closeRects[i].Left
                                                  - AdvGraphics.Scale(this, 2), r.Bottom);
                TextRenderer.DrawText(g, _tags[i], Font, textRect,
                    Enabled ? palette.SubtleText : theme.TextDisabled,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                  | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                // 닫기 X — 호버 시 진하게
                var xr = Rectangle.Inflate(_closeRects[i], -_closeRects[i].Width / 4, -_closeRects[i].Height / 4);
                Color xc = !Enabled ? theme.TextDisabled
                    : (i == _hoverClose ? AdvContextPalette.Shade(palette.SubtleText, 0.3f) : palette.SubtleText);
                using (var pen = new Pen(xc, AdvGraphics.Scale(this, 1.5f))
                { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(pen, xr.Left, xr.Top, xr.Right, xr.Bottom);
                    g.DrawLine(pen, xr.Left, xr.Bottom, xr.Right, xr.Top);
                }
            }

            g.SmoothingMode = old;
            base.OnPaint(e);
        }

        protected override void OnThemeChanged()
        {
            base.OnThemeChanged();
            if (_inner == null) return;
            var theme = EffectiveTheme;
            _inner.BackColor = Enabled ? theme.InputBackground : theme.InputBackgroundDisabled;
            _inner.ForeColor = theme.Text;
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            _inner.Enabled = Enabled;
            OnThemeChanged();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.KeyDown -= InnerKeyDown;
                _inner.KeyPress -= InnerKeyPress;
                _inner.GotFocus -= InnerFocusChanged;
                _inner.LostFocus -= InnerLostFocus;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>AdvTagInput이 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvTagInputOptions : AdvOptions
    {
        private readonly AdvTagInput _owner;

        internal AdvTagInputOptions(AdvTagInput owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(false)]
        [Description("같은 태그(대소문자 무시)를 중복으로 허용할지 여부입니다.")]
        public bool AllowDuplicates
        {
            get { return _owner.AllowDuplicates; }
            set { _owner.AllowDuplicates = value; }
        }

        [DefaultValue(0)]
        [Description("최대 태그 수입니다. 0이면 제한이 없습니다.")]
        public int MaxTags
        {
            get { return _owner.MaxTags; }
            set { _owner.MaxTags = value; }
        }
    }
}
