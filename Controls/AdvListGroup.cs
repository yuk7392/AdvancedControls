using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>선택된 목록 항목 정보.</summary>
    public class AdvListGroupItemEventArgs : EventArgs
    {
        public int Index { get; private set; }
        public string Text { get; private set; }

        public AdvListGroupItemEventArgs(int index, string text)
        {
            Index = index;
            Text = text;
        }
    }

    /// <summary>
    /// 선택 가능한 항목들의 세로 목록. 항목 우측에 배지를 달 수 있고, 바깥 테두리 없는
    /// flush 모드를 지원한다.
    /// (스크롤은 지원하지 않는다 — 항목이 넘치면 잘린다. 긴 목록은 <see cref="AdvListBox"/>를 쓴다.)
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("ItemClicked")]
    [DefaultProperty("AdvancedControlOptions")]
    [Description("선택 가능한 항목 목록입니다.")]
    public class AdvListGroup : AdvControlBase
    {
        private const int RowPadH = 12;
        private const int RowPadV = 8;

        private string[] _items = new string[0];
        private string[] _badges = new string[0];
        private Size[] _badgeSizes = new Size[0];   // 배지 텍스트 측정 결과 캐시(_badges와 같은 길이)
        private bool _flush;
        private bool _selectionEnabled = true;
        private int _selectedIndex = -1;
        private int _hover = -1;
        private int _focusRow = -1;
        private Color _context = Color.Empty;
        private AdvListGroupOptions _options;

        public event EventHandler<AdvListGroupItemEventArgs> ItemClicked;
        public event EventHandler SelectedIndexChanged;

        public AdvListGroup()
        {
            // 행 단위로 키보드 포커스를 옮기므로 컨트롤을 포커스 가능하게 한다.
            // 포커스 표시는 행별 링으로 직접 그리므로 전체 글로우 여백은 예약하지 않는다(레이아웃 밀림 방지).
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
            Styling.ShowFocusGlow = false;
        }

        protected override Size DefaultSize
        {
            get { return new Size(220, 160); }
        }

        protected override bool IsClickable
        {
            get { return true; }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [Description("목록 항목의 글자들입니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string[] Items
        {
            get { return (string[])_items.Clone(); }
            set
            {
                _items = value ?? new string[0];
                if (_selectedIndex >= _items.Length) SelectedIndex = -1;   // setter 경유로 이벤트가 발생하게 한다
                if (_focusRow >= _items.Length) _focusRow = -1;
                Invalidate();
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [Description("각 항목 우측에 표시할 배지 글자입니다(항목과 같은 인덱스). 비우면 배지가 없습니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string[] Badges
        {
            get { return (string[])_badges.Clone(); }
            set { _badges = value ?? new string[0]; MeasureBadges(); Invalidate(); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("바깥 테두리를 없애고 항목 사이만 구분선으로 나눕니다.")]
        public bool Flush
        {
            get { return _flush; }
            set { if (_flush == value) return; _flush = value; Invalidate(); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [Description("선택된 항목의 강조 색입니다. 비워 두면 테마 강조색(Accent)을 따릅니다.")]
        public Color Context
        {
            get { return _context; }
            set { if (_context == value) return; _context = value; Invalidate(); }
        }
        public bool ShouldSerializeContext() { return !_context.IsEmpty; }
        public void ResetContext() { Context = Color.Empty; }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(true)]
        [Description("항목을 클릭했을 때 선택 상태로 유지할지 여부입니다. 끄면 링크처럼 동작합니다.")]
        public bool SelectionEnabled
        {
            get { return _selectionEnabled; }
            set
            {
                if (_selectionEnabled == value) return;
                _selectionEnabled = value;
                if (!value) SelectedIndex = -1;   // setter 경유로 이벤트가 발생하게 한다
                Invalidate();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                value = value < 0 || value >= _items.Length ? -1 : value;
                if (_selectedIndex == value) return;
                _selectedIndex = value;
                Invalidate();
                var h = SelectedIndexChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvListGroupOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvListGroupOptions(this)); }
        }

        private int RowHeight
        {
            get { return Font.Height + RowPadV * 2; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var palette = AdvContextPalette.Resolve(_context, theme);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var frame = FrameBounds;
            if (frame.Width <= 0 || frame.Height <= 0) return;

            int bw = EffectiveBorderWidth;

            // 바깥 프레임: flush면 배경만, 아니면 테두리까지.
            AdvFrameRenderer.Draw(g, frame, theme, EffectiveCorners, _flush ? 0 : bw,
                                  theme.Surface, theme.SurfaceGradientEnd,
                                  _flush ? Color.Empty : theme.Border,
                                  null, CurrentElevation, EffectiveBorderDash, EffectiveGradientAngle);

            int inset = _flush ? 0 : bw;
            int rowH = RowHeight;
            int top = frame.Top + inset;
            int left = frame.Left + inset;
            int width = frame.Width - inset * 2;

            for (int i = 0; i < _items.Length; i++)
            {
                var row = new Rectangle(left, top + i * rowH, width, rowH);
                if (row.Top >= frame.Bottom - inset) break;   // 넘치면 그만

                bool selected = _selectionEnabled && i == _selectedIndex;
                bool hovered = i == _hover && !selected;

                if (selected)
                {
                    using (var b = new SolidBrush(palette.Solid)) g.FillRectangle(b, row);
                }
                else if (hovered)
                {
                    using (var b = new SolidBrush(theme.SurfaceHover)) g.FillRectangle(b, row);
                }

                Color fore = selected ? palette.OnSolid : (Enabled ? theme.Text : theme.TextDisabled);

                // 배지 먼저 배치해 글자 폭을 그만큼 줄인다.
                int textRight = row.Right - RowPadH;
                string badge = i < _badges.Length ? _badges[i] : null;
                if (!string.IsNullOrEmpty(badge))
                    textRight = DrawBadge(g, palette, row, badge, _badgeSizes[i], selected) - 6;

                var textRect = new Rectangle(row.Left + RowPadH, row.Top,
                                             Math.Max(0, textRight - (row.Left + RowPadH)), row.Height);
                TextRenderer.DrawText(g, _items[i] ?? string.Empty, Font, textRect, fore,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                // 항목 사이 구분선.
                if (i < _items.Length - 1)
                {
                    using (var pen = new Pen(theme.Border, 1))
                        g.DrawLine(pen, row.Left + RowPadH, row.Bottom, row.Right - RowPadH, row.Bottom);
                }

                // 키보드 포커스가 놓인 행에 포커스 링을 그린다.
                if (Focused && i == _focusRow)
                {
                    var fr = Rectangle.Inflate(row, -2, -2);
                    using (var pen = new Pen(theme.FocusRing, 1.5f))
                        g.DrawRectangle(pen, fr.Left, fr.Top, fr.Width - 1, fr.Height - 1);
                }
            }

            base.OnPaint(e);
        }

        /// <summary>배지 텍스트 크기를 미리 재서 캐싱한다. Badges/Font가 바뀔 때만 호출한다.</summary>
        private void MeasureBadges()
        {
            _badgeSizes = new Size[_badges.Length];
            for (int i = 0; i < _badges.Length; i++)
                _badgeSizes[i] = string.IsNullOrEmpty(_badges[i])
                    ? Size.Empty
                    : TextRenderer.MeasureText(_badges[i], Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix);
        }

        /// <summary>미리 잰 크기로 배지 알약을 그리고 그 왼쪽 x를 돌려준다.</summary>
        private int DrawBadge(Graphics g, AdvContextPalette palette, Rectangle row, string text, Size size, bool onSelectedRow)
        {
            int h = size.Height + 2;
            int w = Math.Max(h, size.Width + 12);
            int x = row.Right - RowPadH - w;
            int y = row.Top + (row.Height - h) / 2;
            var pill = new Rectangle(x, y, w, h);

            Color bg = onSelectedRow ? palette.OnSolid : palette.Solid;
            Color fg = onSelectedRow ? palette.Solid : palette.OnSolid;

            using (var path = AdvGraphics.CreateRoundedRect(pill, h / 2))
            using (var brush = new SolidBrush(bg))
                g.FillPath(brush, path);

            TextRenderer.DrawText(g, text, Font, pill, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            return x;
        }

        private int HitTest(Point p)
        {
            var frame = FrameBounds;
            int inset = _flush ? 0 : EffectiveBorderWidth;
            int rowH = RowHeight;
            if (rowH <= 0) return -1;

            int rel = p.Y - (frame.Top + inset);
            if (rel < 0) return -1;
            int idx = rel / rowH;
            return idx >= 0 && idx < _items.Length ? idx : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int hit = HitTest(e.Location);
            if (hit != _hover)
            {
                _hover = hit;
                // 행은 여러 개의 독립 클릭 영역이므로 항목 위에서만 손 커서를 켠다.
                Cursor = (hit >= 0 && UseHandCursor) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hover != -1) { _hover = -1; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            if (!Focused) Focus();

            int hit = HitTest(e.Location);
            if (hit < 0) return;

            SetFocusRow(hit);
            ActivateRow(hit);
        }

        private void ActivateRow(int i)
        {
            if (i < 0 || i >= _items.Length) return;

            if (_selectionEnabled) SelectedIndex = i;

            var h = ItemClicked;
            if (h != null) h(this, new AdvListGroupItemEventArgs(i, _items[i]));
        }

        private int DefaultFocusRow()
        {
            return _selectedIndex >= 0 ? _selectedIndex : 0;
        }

        private void SetFocusRow(int i)
        {
            if (_items.Length == 0) i = -1;
            else if (i < 0) i = 0;
            else if (i > _items.Length - 1) i = _items.Length - 1;
            if (_focusRow == i) return;
            _focusRow = i;
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Home:
                case Keys.End:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up:
                    SetFocusRow(_focusRow < 0 ? DefaultFocusRow() : _focusRow - 1);
                    e.Handled = true;
                    break;
                case Keys.Down:
                    SetFocusRow(_focusRow < 0 ? DefaultFocusRow() : _focusRow + 1);
                    e.Handled = true;
                    break;
                case Keys.Home: SetFocusRow(0); e.Handled = true; break;
                case Keys.End: SetFocusRow(_items.Length - 1); e.Handled = true; break;
                case Keys.Enter:
                case Keys.Space:
                    if (_focusRow >= 0) ActivateRow(_focusRow);
                    e.Handled = true;
                    break;
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            if (_focusRow < 0 && _items.Length > 0) _focusRow = DefaultFocusRow();
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            MeasureBadges();
            Invalidate();
            base.OnFontChanged(e);
        }

        protected override void OnThemeChanged()
        {
            Invalidate();
            base.OnThemeChanged();
        }
    }

    /// <summary>AdvListGroup이 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvListGroupOptions : AdvOptions
    {
        private readonly AdvListGroup _owner;

        internal AdvListGroupOptions(AdvListGroup owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [Description("선택된 항목의 강조 색입니다. 비워 두면 테마 강조색(Accent)을 따릅니다.")]
        public Color Context
        {
            get { return _owner.Context; }
            set { _owner.Context = value; }
        }
        public bool ShouldSerializeContext() { return _owner.ShouldSerializeContext(); }
        public void ResetContext() { _owner.ResetContext(); }

        [Description("목록 항목의 글자들입니다.")]
        public string[] Items
        {
            get { return _owner.Items; }
            set { _owner.Items = value; }
        }

        [Description("각 항목 우측에 표시할 배지 글자입니다(항목과 같은 인덱스). 비우면 배지가 없습니다.")]
        public string[] Badges
        {
            get { return _owner.Badges; }
            set { _owner.Badges = value; }
        }

        [DefaultValue(false)]
        [Description("바깥 테두리를 없애고 항목 사이만 구분선으로 나눕니다.")]
        public bool Flush
        {
            get { return _owner.Flush; }
            set { _owner.Flush = value; }
        }

        [DefaultValue(true)]
        [Description("항목을 클릭했을 때 선택 상태로 유지할지 여부입니다. 끄면 링크처럼 동작합니다.")]
        public bool SelectionEnabled
        {
            get { return _owner.SelectionEnabled; }
            set { _owner.SelectionEnabled = value; }
        }
    }
}
