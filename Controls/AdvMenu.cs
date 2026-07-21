using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>메뉴 항목 하나. 구분선이거나, 텍스트·단축키·활성 여부를 갖는 클릭 항목이다.</summary>
    public class AdvMenuItem
    {
        public string Text { get; set; }
        public string Shortcut { get; set; }
        public bool Enabled { get; set; }
        public object Tag { get; set; }
        public bool IsSeparator { get; internal set; }

        /// <summary>항목을 클릭(또는 Enter)하면 발생한다.</summary>
        public event EventHandler Click;

        public AdvMenuItem() { Enabled = true; }
        public AdvMenuItem(string text) { Text = text; Enabled = true; }

        internal void PerformClick()
        {
            var h = Click;
            if (h != null) h(this, EventArgs.Empty);
        }

        /// <summary>구분선 항목을 만든다.</summary>
        public static AdvMenuItem Separator() { return new AdvMenuItem { IsSeparator = true, Enabled = false }; }
    }

    /// <summary>
    /// 팝업 안에서 항목 목록을 직접 그리는 뷰. 호버·클릭·화살표 키를 처리하고,
    /// 항목이 선택되면 <see cref="ItemChosen"/>, 닫아야 하면 <see cref="CloseRequested"/>를 알린다.
    /// </summary>
    internal sealed class AdvMenuView : Control
    {
        private const int ItemH = 26;
        private const int SepH = 9;
        private const int TextLeft = 28;
        private const int RightPad = 18;
        private const int ShortcutGap = 28;

        private readonly List<AdvMenuItem> _items;
        private int _hover = -1;

        public event Action<AdvMenuItem> ItemChosen;
        public event Action CloseRequested;

        public AdvMenuView(List<AdvMenuItem> items)
        {
            _items = items;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Size = Measure();
            // 팝업이 열린 채 앱이 테마를 바꾸면 즉시 다시 그린다(Control 직접 상속이라 베이스의 자동 구독이 없음)
            AdvThemeManager.ThemeChanged += OnGlobalThemeChanged;
        }

        private void OnGlobalThemeChanged(object sender, EventArgs e) { if (!IsDisposed) Invalidate(); }

        private Size Measure()
        {
            // CreateGraphics()는 부모 편입 전에 네이티브 핸들을 강제 생성하므로, Graphics 없는
            // TextRenderer.MeasureText 오버로드로 측정한다(핸들 생성·측정 비용 제거).
            int h = 5, w = 150;
            using (var sf = new Font(Font, FontStyle.Regular))
            {
                foreach (var it in _items)
                {
                    if (it.IsSeparator) { h += SepH; continue; }
                    int tw = TextRenderer.MeasureText(it.Text ?? "", sf).Width;
                    int sw = string.IsNullOrEmpty(it.Shortcut) ? 0 : ShortcutGap + TextRenderer.MeasureText(it.Shortcut, sf).Width;
                    w = Math.Max(w, TextLeft + tw + sw + RightPad);
                    h += ItemH;
                }
            }
            return new Size(w, h + 5);
        }

        private int ItemTop(int index)
        {
            int y = 5;
            for (int i = 0; i < index; i++) y += _items[i].IsSeparator ? SepH : ItemH;
            return y;
        }

        private int ItemAt(int y)
        {
            int top = 5;
            for (int i = 0; i < _items.Count; i++)
            {
                int hgt = _items[i].IsSeparator ? SepH : ItemH;
                if (y >= top && y < top + hgt) return i;
                top += hgt;
            }
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = AdvThemeManager.Current;
            var g = e.Graphics;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            AdvFrameRenderer.Draw(g, bounds, theme, new AdvCorners(8), 1,
                                  theme.Surface, Color.Empty, theme.Border, null);

            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                int y = ItemTop(i);

                if (it.IsSeparator)
                {
                    using (var pen = new Pen(theme.Border))
                        g.DrawLine(pen, 8, y + SepH / 2, Width - 8, y + SepH / 2);
                    continue;
                }

                var row = new Rectangle(4, y, Width - 8, ItemH);
                bool hot = i == _hover && it.Enabled;
                if (hot)
                    using (var b = new SolidBrush(theme.Accent))
                    using (var path = AdvGraphics.CreateRoundedRect(row, new AdvCorners(5)))
                        g.FillPath(b, path);

                Color fg = !it.Enabled ? theme.TextDisabled : hot ? theme.OnAccent : theme.Text;
                var textRect = Rectangle.FromLTRB(TextLeft, y, Width - RightPad, y + ItemH);
                TextRenderer.DrawText(g, it.Text ?? "", Font, textRect, fg,   // 측정(Measure)과 동일하게 null 방어
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                if (!string.IsNullOrEmpty(it.Shortcut))
                {
                    Color sc = !it.Enabled ? theme.TextDisabled : hot ? theme.OnAccent : theme.TextMuted;
                    TextRenderer.DrawText(g, it.Shortcut, Font, textRect, sc,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = ItemAt(e.Y);
            if (i >= 0 && (_items[i].IsSeparator || !_items[i].Enabled)) i = -1;
            if (i != _hover) { _hover = i; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover != -1) { _hover = -1; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int i = ItemAt(e.Y);
            if (i >= 0 && !_items[i].IsSeparator && _items[i].Enabled) Choose(_items[i]);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up: case Keys.Down: case Keys.Return: case Keys.Escape: return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Down: Step(+1); e.Handled = true; break;
                case Keys.Up: Step(-1); e.Handled = true; break;
                case Keys.Return:
                    if (_hover >= 0) Choose(_items[_hover]);
                    e.Handled = true; break;
                case Keys.Escape:
                    var c = CloseRequested; if (c != null) c();
                    e.Handled = true; break;
            }
        }

        /// <summary>구분선·비활성을 건너뛰며 다음 선택 가능한 항목으로 이동한다.</summary>
        private void Step(int dir)
        {
            if (_items.Count == 0) return;
            int i = _hover;
            for (int n = 0; n < _items.Count; n++)
            {
                i += dir;
                if (i < 0) i = _items.Count - 1;
                if (i >= _items.Count) i = 0;
                if (!_items[i].IsSeparator && _items[i].Enabled) { _hover = i; Invalidate(); return; }
            }
        }

        private void Choose(AdvMenuItem it)
        {
            var h = ItemChosen;
            if (h != null) h(it);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) AdvThemeManager.ThemeChanged -= OnGlobalThemeChanged;
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// 테마 컨텍스트(우클릭) 메뉴. 항목을 담아 두고 <see cref="Show(Control, Point)"/>로 띄운다.
    /// 부유·바깥 클릭 자동닫힘은 ToolStripDropDown 껍데기가 맡고, 항목 그리기는 전부 커스텀이다.
    /// </summary>
    [ToolboxItem(true)]
    [Description("테마를 따르는 커스텀 컨텍스트 메뉴입니다.")]
    public class AdvContextMenu : Component
    {
        private readonly List<AdvMenuItem> _items = new List<AdvMenuItem>();
        private ToolStripDropDown _dd;

        /// <summary>메뉴가 닫힌 뒤 발생한다.</summary>
        public event EventHandler Closed;

        /// <summary>메뉴 항목들. 구분선은 <see cref="AdvMenuItem.Separator"/>로 넣는다.</summary>
        [Browsable(false)]
        public IList<AdvMenuItem> Items { get { return _items; } }

        /// <summary>텍스트 항목을 추가하고 돌려준다.</summary>
        public AdvMenuItem Add(string text)
        {
            var it = new AdvMenuItem(text);
            _items.Add(it);
            return it;
        }

        /// <summary>텍스트·단축키 항목을 추가하고 돌려준다.</summary>
        public AdvMenuItem Add(string text, string shortcut)
        {
            var it = new AdvMenuItem(text) { Shortcut = shortcut };
            _items.Add(it);
            return it;
        }

        /// <summary>구분선을 추가한다.</summary>
        public void AddSeparator() { _items.Add(AdvMenuItem.Separator()); }

        /// <summary>화면 좌표에 연다.</summary>
        public void Show(Point screenLocation) { ShowInternal(null, screenLocation); }

        /// <summary>owner 기준 좌표에 연다.</summary>
        public void Show(Control owner, Point ownerLocation)
        {
            ShowInternal(owner, owner != null ? owner.PointToScreen(ownerLocation) : ownerLocation);
        }

        private void ShowInternal(Control owner, Point screen)
        {
            Close();
            if (_items.Count == 0) return;

            var view = new AdvMenuView(_items);
            var host = new ToolStripControlHost(view)
            { AutoSize = false, Margin = Padding.Empty, Padding = Padding.Empty, Size = view.Size };

            _dd = new ToolStripDropDown
            { Padding = Padding.Empty, AutoClose = true, AutoSize = false, DropShadowEnabled = false };
            _dd.Renderer = BlankRenderer.Instance;
            _dd.Items.Add(host);
            _dd.Size = view.Size;

            using (var path = AdvGraphics.CreateRoundedRect(new Rectangle(Point.Empty, view.Size), new AdvCorners(8)))
                _dd.Region = new Region(path);

            view.ItemChosen += it => { Close(); it.PerformClick(); };
            view.CloseRequested += Close;
            _dd.Closed += (s, e) =>
            {
                var h = Closed;
                if (h != null) h(this, EventArgs.Empty);
                DisposeDropDown();
            };

            _dd.Show(screen);
            view.Focus();
        }

        /// <summary>열려 있으면 닫는다.</summary>
        public void Close()
        {
            if (_dd != null && _dd.Visible) _dd.Close();
            else DisposeDropDown();
        }

        private void DisposeDropDown()
        {
            if (_dd == null) return;
            var d = _dd; _dd = null;
            if (d.Region != null) d.Region.Dispose();   // 대입한 둥근 Region을 명시적으로 해제(반복 개폐 시 HRGN 누수 방지)
            d.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeDropDown();
            base.Dispose(disposing);
        }

        /// <summary>ToolStripDropDown의 기본 테두리·배경을 지워 우리 그림만 보이게 한다.</summary>
        private sealed class BlankRenderer : ToolStripRenderer
        {
            public static readonly BlankRenderer Instance = new BlankRenderer();
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
        }
    }

    /// <summary>
    /// 상단 앱 메뉴바. 최상위 타이틀을 가로로 늘어놓고, 클릭하면 그 타이틀의
    /// <see cref="AdvContextMenu"/>를 아래로 연다.
    /// </summary>
    [ToolboxItem(true)]
    [Description("테마를 따르는 커스텀 메뉴바입니다.")]
    public class AdvMenuBar : AdvControlBase
    {
        private sealed class TopMenu { public string Title; public AdvContextMenu Menu; public Rectangle Rect; }

        private readonly List<TopMenu> _menus = new List<TopMenu>();
        private int _hover = -1;
        private int _open = -1;
        private int _justClosedIndex = -1;   // 방금 닫힌 메뉴(타이틀 재클릭 토글 처리용)
        private int _justClosedAt;
        private bool _titlesDirty = true;    // 타이틀 추가·폰트·크기 변경 시에만 레이아웃 재계산

        private const int ItemPadX = 14;

        private AdvMenuBarOptions _options;

        public AdvMenuBar()
        {
            SetStyle(ControlStyles.Selectable, true);
            Styling.ShowFocusGlow = false;
            Styling.Radius = 0;
            Dock = DockStyle.Top;
            Height = 30;
        }

        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvMenuBarOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvMenuBarOptions(this)); }
        }

        /// <summary>최상위 메뉴를 추가하고 그 항목을 담을 <see cref="AdvContextMenu"/>를 돌려준다.</summary>
        public AdvContextMenu AddMenu(string title)
        {
            var menu = new AdvContextMenu();
            _menus.Add(new TopMenu { Title = title, Menu = menu });
            _titlesDirty = true;
            Invalidate();
            return menu;
        }

        [Browsable(false)]
        public int MenuCount { get { return _menus.Count; } }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            _titlesDirty = true;   // 폰트가 바뀌면 타이틀 폭 재측정
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _titlesDirty = true;   // 높이가 바뀌면 타이틀 사각형 높이 재계산
        }

        private void LayoutTitles(Graphics g)
        {
            int x = 6;
            foreach (var m in _menus)
            {
                int w = TextRenderer.MeasureText(g, m.Title ?? "", Font).Width + ItemPadX * 2;
                m.Rect = new Rectangle(x, 0, w, Height);
                x += w;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;

            using (var b = new SolidBrush(theme.Surface)) g.FillRectangle(b, ClientRectangle);
            using (var pen = new Pen(theme.Border))
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);

            if (_titlesDirty) { LayoutTitles(g); _titlesDirty = false; }   // 매 페인트 재측정 방지
            for (int i = 0; i < _menus.Count; i++)
            {
                var m = _menus[i];
                bool active = i == _open || i == _hover;
                if (active)
                    using (var b = new SolidBrush(i == _open ? theme.SurfacePressed : theme.SurfaceHover))
                        g.FillRectangle(b, m.Rect);

                TextRenderer.DrawText(g, m.Title ?? "", Font, m.Rect, theme.Text,   // LayoutTitles와 동일하게 null 방어
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }

        private int TitleAt(Point p)
        {
            for (int i = 0; i < _menus.Count; i++)
                if (_menus[i].Rect.Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = TitleAt(e.Location);
            if (i != _hover) { _hover = i; Invalidate(); }
            // 이미 메뉴가 열린 상태에서 다른 타이틀로 옮기면 그쪽으로 전환
            if (_open >= 0 && i >= 0 && i != _open) OpenMenu(i);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover != -1) { _hover = -1; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int i = TitleAt(e.Location);
            if (i < 0) return;
            if (i == _open) { CloseOpen(); return; }   // 열린 걸 다시 누르면 닫기
            // 바깥 클릭으로 팝업이 방금 닫힌 직후 같은 타이틀을 누른 것이면 다시 열지 않는다(토글)
            if (i == _justClosedIndex && unchecked(Environment.TickCount - _justClosedAt) < 300)
            { _justClosedIndex = -1; return; }
            OpenMenu(i);
        }

        private void OpenMenu(int index)
        {
            CloseOpen();
            _open = index;
            Invalidate();

            var m = _menus[index];
            m.Menu.Closed += OnMenuClosed;
            m.Menu.Show(this, new Point(m.Rect.Left, Height));
        }

        private void CloseOpen()
        {
            if (_open < 0) return;
            var m = _menus[_open];
            m.Menu.Closed -= OnMenuClosed;
            m.Menu.Close();
            _open = -1;
            Invalidate();
        }

        private void OnMenuClosed(object sender, EventArgs e)
        {
            var cm = sender as AdvContextMenu;
            if (cm != null) cm.Closed -= OnMenuClosed;
            _justClosedIndex = _open;
            _justClosedAt = Environment.TickCount;
            _open = -1;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                foreach (var m in _menus) m.Menu.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>AdvMenuBar가 추가한 속성. 테두리·효과·전환·테마 그룹을 속성 창에 노출한다.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvMenuBarOptions : AdvOptions
    {
        internal AdvMenuBarOptions(AdvMenuBar owner) : base(owner.Styling, owner.Palette) { }
    }
}
