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
    /// flush 모드를 지원한다. Bootstrap의 <c>.list-group</c>에 대응한다.
    /// (스크롤은 지원하지 않는다 — 항목이 넘치면 잘린다. 긴 목록은 <see cref="AdvListBox"/>를 쓴다.)
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("ItemClicked")]
    [Description("선택 가능한 항목 목록입니다.")]
    public class AdvListGroup : AdvControlBase
    {
        private const int RowPadH = 12;
        private const int RowPadV = 8;

        private string[] _items = new string[0];
        private string[] _badges = new string[0];
        private bool _flush;
        private bool _selectionEnabled = true;
        private int _selectedIndex = -1;
        private int _hover = -1;
        private AdvContextColor _context = AdvContextColor.Default;
        private AdvListGroupOptions _options;

        public event EventHandler<AdvListGroupItemEventArgs> ItemClicked;
        public event EventHandler SelectedIndexChanged;

        public AdvListGroup()
        {
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }

        protected override Size DefaultSize
        {
            get { return new Size(220, 160); }
        }

        protected override bool IsClickable
        {
            get { return true; }
        }

        [Category("Behavior")]
        [Description("목록 항목의 글자들입니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string[] Items
        {
            get { return (string[])_items.Clone(); }
            set
            {
                _items = value ?? new string[0];
                if (_selectedIndex >= _items.Length) _selectedIndex = -1;
                Invalidate();
            }
        }

        [Category("Behavior")]
        [Description("각 항목 우측에 표시할 배지 글자입니다(항목과 같은 인덱스). 비우면 배지가 없습니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string[] Badges
        {
            get { return (string[])_badges.Clone(); }
            set { _badges = value ?? new string[0]; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        [Description("바깥 테두리를 없애고 항목 사이만 구분선으로 나눕니다.")]
        public bool Flush
        {
            get { return _flush; }
            set { if (_flush == value) return; _flush = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(AdvContextColor.Default)]
        [Description("선택된 항목의 컨텍스트 색입니다. Default는 테마 강조색(Accent)을 따릅니다.")]
        public AdvContextColor Context
        {
            get { return _context; }
            set { if (_context == value) return; _context = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("항목을 클릭했을 때 선택 상태로 유지할지 여부입니다. 끄면 링크처럼 동작합니다.")]
        public bool SelectionEnabled
        {
            get { return _selectionEnabled; }
            set
            {
                if (_selectionEnabled == value) return;
                _selectionEnabled = value;
                if (!value) _selectedIndex = -1;
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
            var palette = theme.ResolveContext(_context);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var frame = FrameBounds;
            if (frame.Width <= 0 || frame.Height <= 0) return;

            int bw = EffectiveBorderWidth;

            // 바깥 프레임: flush면 배경만, 아니면 테두리까지.
            AdvFrameRenderer.Draw(g, frame, theme, EffectiveCorners, _flush ? 0 : bw,
                                  theme.Surface, theme.SurfaceGradientEnd,
                                  _flush ? Color.Empty : theme.Border,
                                  null, CurrentElevation, EffectiveBorderDash);

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
                    textRight = DrawBadge(g, palette, row, badge, selected) - 6;

                var textRect = new Rectangle(row.Left + RowPadH, row.Top,
                                             Math.Max(0, textRight - (row.Left + RowPadH)), row.Height);
                TextRenderer.DrawText(g, _items[i], Font, textRect, fore,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                // 항목 사이 구분선.
                if (i < _items.Length - 1)
                {
                    using (var pen = new Pen(theme.Border, 1))
                        g.DrawLine(pen, row.Left + RowPadH, row.Bottom, row.Right - RowPadH, row.Bottom);
                }
            }

            base.OnPaint(e);
        }

        /// <summary>배지 알약을 그리고 그 왼쪽 x를 돌려준다.</summary>
        private int DrawBadge(Graphics g, AdvContextPalette palette, Rectangle row, string text, bool onSelectedRow)
        {
            var size = TextRenderer.MeasureText(text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix);
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

            int hit = HitTest(e.Location);
            if (hit < 0) return;

            if (_selectionEnabled) SelectedIndex = hit;

            var h = ItemClicked;
            if (h != null) h(this, new AdvListGroupItemEventArgs(hit, _items[hit]));
        }

        protected override void OnFontChanged(EventArgs e)
        {
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

        [DefaultValue(AdvContextColor.Default)]
        [Description("선택된 항목의 컨텍스트 색입니다.")]
        public AdvContextColor Context
        {
            get { return _owner.Context; }
            set { _owner.Context = value; }
        }
    }
}
