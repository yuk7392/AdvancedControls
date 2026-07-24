using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls.Internal
{
    /// <summary>
    /// 드롭다운 안에 들어가는 목록. 항목을 직접 그리고 마우스 위치를 추적한다.
    /// 스크롤은 이 컨트롤을 감싼 <see cref="Panel"/>의 AutoScroll이 담당한다.
    /// </summary>
    internal class AdvDropDownList : Control
    {
        private readonly List<object> _items;
        private int _selectedIndex = -1;
        private int _hoverIndex = -1;
        private AdvTheme _theme;

        public event EventHandler<ItemEventArgs> ItemChosen;

        public AdvDropDownList(List<object> items, AdvTheme theme)
        {
            _items = items;
            _theme = theme;

            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw, true);
        }

        public int ItemHeight { get; set; }

        /// <summary>
        /// 항목을 어떤 글자로 보여줄지 정한다. DisplayMember가 걸려 있으면
        /// ToString()이 아니라 그 속성 값을 써야 하므로 바깥에서 받는다.
        /// </summary>
        public Converter<object, string> TextProvider { get; set; }

        /// <summary>
        /// 항목 텍스트에서 강조할 검색어(자동완성의 현재 입력). 대소문자 무시로 첫 일치 구간을
        /// 굵게 + 강조색으로 그린다. 비우면 일반 렌더(콤보 드롭다운 경로).
        /// </summary>
        public string HighlightText { get; set; }

        // 강조 구간용 볼드 폰트는 Font가 바뀔 때만 다시 만든다(매 그리기 생성 방지)
        private Font _boldFont;
        private Font BoldFont { get { return _boldFont ?? (_boldFont = new Font(Font, FontStyle.Bold)); } }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (_boldFont != null) { _boldFont.Dispose(); _boldFont = null; }
        }

        public AdvTheme Theme
        {
            get { return _theme; }
            set { _theme = value; Invalidate(); }
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (_selectedIndex == value) return;
                _selectedIndex = value;
                Invalidate();
            }
        }

        public int HoverIndex
        {
            get { return _hoverIndex; }
            set
            {
                if (_hoverIndex == value) return;
                _hoverIndex = value;
                Invalidate();
            }
        }

        public Rectangle GetItemBounds(int index)
        {
            return new Rectangle(0, index * ItemHeight, Width, ItemHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;

            using (var back = new SolidBrush(_theme.InputBackground))
                g.FillRectangle(back, ClientRectangle);

            // 화면에 보이는 구간만 그린다. 항목이 많아도 그리기 비용이 늘지 않는다
            int first = Math.Max(0, e.ClipRectangle.Top / ItemHeight);
            int last = Math.Min(_items.Count - 1, e.ClipRectangle.Bottom / ItemHeight);

            for (int i = first; i <= last; i++)
            {
                var r = GetItemBounds(i);

                Color fore = _theme.Text;

                if (i == _selectedIndex)
                {
                    using (var b = new SolidBrush(_theme.Accent))
                        g.FillRectangle(b, r);
                    fore = _theme.OnAccent;
                }
                else if (i == _hoverIndex)
                {
                    using (var b = new SolidBrush(_theme.SurfaceHover))
                        g.FillRectangle(b, r);
                }

                var textRect = new Rectangle(r.X + 8, r.Y, r.Width - 16, r.Height);
                string text = ItemText(i);

                int hs = string.IsNullOrEmpty(HighlightText) ? -1
                    : text.IndexOf(HighlightText, StringComparison.CurrentCultureIgnoreCase);
                if (hs >= 0)
                {
                    DrawHighlighted(g, text, hs, HighlightText.Length, textRect, fore, i == _selectedIndex);
                }
                else
                {
                    TextRenderer.DrawText(g, text, Font, textRect, fore,
                        TextFormatFlags.Left
                      | TextFormatFlags.VerticalCenter
                      | TextFormatFlags.EndEllipsis
                      | TextFormatFlags.NoPrefix);
                }
            }

            base.OnPaint(e);
        }

        /// <summary>일치 구간을 굵게 + 강조색으로 세 조각(앞/일치/뒤)으로 나눠 그린다.
        /// 조각 그리기라 말줄임은 없고 넘치는 부분은 잘린다(제안 목록 폭이 입력창과 같아 실사용 무해).</summary>
        private void DrawHighlighted(Graphics g, string text, int start, int len, Rectangle rect, Color fore, bool selected)
        {
            const TextFormatFlags F = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding
                                    | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            var proposed = new Size(int.MaxValue, rect.Height);
            Color midColor = selected ? _theme.OnAccent : _theme.Accent;

            int x = rect.X;
            string[] parts = { text.Substring(0, start), text.Substring(start, len), text.Substring(start + len) };
            for (int p = 0; p < 3 && x < rect.Right; p++)
            {
                if (parts[p].Length == 0) continue;
                var font = p == 1 ? BoldFont : Font;
                var color = p == 1 ? midColor : fore;
                TextRenderer.DrawText(g, parts[p], font, new Rectangle(x, rect.Y, rect.Right - x, rect.Height), color, F);
                x += TextRenderer.MeasureText(g, parts[p], font, proposed, F).Width;
            }
        }

        private string ItemText(int index)
        {
            var item = _items[index];
            if (TextProvider != null) return TextProvider(item) ?? string.Empty;
            return item == null ? string.Empty : item.ToString();
        }

        private int IndexFromPoint(Point p)
        {
            if (ItemHeight <= 0) return -1;
            int i = p.Y / ItemHeight;
            return (i >= 0 && i < _items.Count) ? i : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            HoverIndex = IndexFromPoint(e.Location);
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            HoverIndex = -1;
            base.OnMouseLeave(e);
        }

        /// <summary>휠 스크롤은 목록을 감싼 팝업이 처리한다.</summary>
        public event MouseEventHandler WheelScrolled;

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            var handler = WheelScrolled;
            if (handler != null) handler(this, e);
            base.OnMouseWheel(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            int i = IndexFromPoint(e.Location);
            if (e.Button == MouseButtons.Left && i >= 0)
            {
                var handler = ItemChosen;
                if (handler != null) handler(this, new ItemEventArgs(i));
            }
            base.OnMouseUp(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _boldFont != null) { _boldFont.Dispose(); _boldFont = null; }
            base.Dispose(disposing);
        }

        internal class ItemEventArgs : EventArgs
        {
            public int Index { get; private set; }
            public ItemEventArgs(int index) { Index = index; }
        }
    }

    /// <summary>
    /// 드롭다운 창. <see cref="ToolStripDropDown"/>을 쓰면 바깥 클릭·Esc·포커스 이동으로
    /// 닫히는 처리와 최상위 표시를 직접 만들지 않아도 된다.
    /// </summary>
    internal class AdvComboPopup : ToolStripDropDown
    {
        private readonly Panel _scroller;
        private readonly AdvDropDownList _list;
        private readonly ToolStripControlHost _host;
        private readonly AdvScrollBar _scrollBar;
        private AdvTheme _theme;

        public event EventHandler<AdvDropDownList.ItemEventArgs> ItemChosen;

        public AdvComboPopup(List<object> items, AdvTheme theme, Font font, int itemHeight)
        {
            _theme = theme;

            AutoSize = false;
            Padding = Padding.Empty;
            Margin = Padding.Empty;
            DropShadowEnabled = true;
            BackColor = theme.InputBackground;

            _list = new AdvDropDownList(items, theme);
            _list.Font = font;
            _list.ItemHeight = itemHeight;
            _list.ItemChosen += OnListItemChosen;

            _list.WheelScrolled += ListWheelScrolled;

            // AutoScroll을 쓰면 OS가 시스템 색 스크롤바를 그려 다크 테마에서 흰 띠로 남는다.
            // 뷰포트는 자르는 역할만 하고 스크롤은 직접 그린 막대가 담당한다.
            _scroller = new Panel();
            _scroller.AutoScroll = false;
            _scroller.Padding = Padding.Empty;
            _scroller.Margin = Padding.Empty;
            _scroller.BackColor = theme.InputBackground;
            _scroller.Controls.Add(_list);

            _scrollBar = new AdvScrollBar(theme);
            _scrollBar.ValueChanged += ScrollBarValueChanged;
            _scroller.Controls.Add(_scrollBar);

            _host = new ToolStripControlHost(_scroller);
            _host.Padding = Padding.Empty;
            _host.Margin = Padding.Empty;
            _host.AutoSize = false;

            Items.Add(_host);
        }

        public AdvDropDownList List
        {
            get { return _list; }
        }

        /// <param name="itemCount">전체 항목 수</param>
        /// <param name="maxVisible">한 번에 보여줄 최대 항목 수</param>
        public void SetSize(int width, int itemCount, int maxVisible)
        {
            int itemH = _list.ItemHeight;
            int visible = Math.Max(1, Math.Min(itemCount, maxVisible));
            int height = visible * itemH;
            int contentHeight = Math.Max(itemH, itemCount * itemH);

            bool needsBar = contentHeight > height;
            int barWidth = needsBar ? AdvScrollBar.DefaultWidth : 0;

            _scroller.Size = new Size(width, height);
            _host.Size = _scroller.Size;
            Size = _scroller.Size;

            _scrollBar.Visible = needsBar;
            if (needsBar)
                _scrollBar.Bounds = new Rectangle(width - barWidth, 0, barWidth, height);

            _scrollBar.ViewportHeight = height;
            _scrollBar.ContentHeight = contentHeight;
            _scrollBar.Value = 0;

            _list.Bounds = new Rectangle(0, 0, width - barWidth, contentHeight);
        }

        /// <summary>키보드로 이동한 항목이 화면 밖이면 보이도록 스크롤한다.</summary>
        public void EnsureVisible(int index)
        {
            if (index < 0) return;

            var item = _list.GetItemBounds(index);
            int top = _scrollBar.Value;
            int bottom = top + _scroller.ClientSize.Height;

            if (item.Top < top)
                _scrollBar.Value = item.Top;
            else if (item.Bottom > bottom)
                _scrollBar.Value = item.Bottom - _scroller.ClientSize.Height;
        }

        private void ScrollBarValueChanged(object sender, EventArgs e)
        {
            _list.Top = -_scrollBar.Value;
        }

        private void ListWheelScrolled(object sender, MouseEventArgs e)
        {
            if (!_scrollBar.IsNeeded) return;

            int lines = SystemInformation.MouseWheelScrollLines;
            if (lines <= 0) lines = 3;

            _scrollBar.Value -= Math.Sign(e.Delta) * lines * _list.ItemHeight;
        }

        public void ApplyTheme(AdvTheme theme)
        {
            _theme = theme;
            _list.Theme = theme;
            _scrollBar.Theme = theme;
            _scroller.BackColor = theme.InputBackground;
            BackColor = theme.InputBackground;
            Invalidate();
        }

        /// <summary>
        /// ToolStripDropDown이 그리는 기본 테두리는 테마를 따르지 않으므로 위에 덧그린다.
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using (var pen = new Pen(_theme.Border))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        private void OnListItemChosen(object sender, AdvDropDownList.ItemEventArgs e)
        {
            var handler = ItemChosen;
            if (handler != null) handler(this, e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _list.ItemChosen -= OnListItemChosen;
                _list.WheelScrolled -= ListWheelScrolled;
                _scrollBar.ValueChanged -= ScrollBarValueChanged;
                ItemChosen = null;
            }
            base.Dispose(disposing);
        }
    }
}
