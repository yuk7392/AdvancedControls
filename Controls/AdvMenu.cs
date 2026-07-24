using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>메뉴 항목 하나. 구분선이거나, 텍스트·단축키·활성 여부를 갖는 클릭 항목이다. 자식을 담으면 서브메뉴가 된다.</summary>
    public class AdvMenuItem
    {
        private List<AdvMenuItem> _children;
        private string _shortcut;
        private Keys _shortcutKeys;

        public string Text { get; set; }

        /// <summary>
        /// 단축키 표기("Ctrl+N" 등). 대입하면 <see cref="ShortcutKeys"/>도 함께 파싱된다
        /// — 파싱 가능한 표기면 메뉴바가 폼 전역에서 실제로 바인딩한다.
        /// </summary>
        public string Shortcut
        {
            get { return _shortcut; }
            set
            {
                _shortcut = value;
                _shortcutKeys = ParseShortcut(value);
            }
        }

        /// <summary>
        /// 실제 바인딩되는 단축키. 대입하면 표기(<see cref="Shortcut"/>)도 서식대로 맞춰진다.
        /// </summary>
        public Keys ShortcutKeys
        {
            get { return _shortcutKeys; }
            set
            {
                _shortcutKeys = value;
                _shortcut = FormatShortcut(value);
            }
        }

        public bool Enabled { get; set; }

        /// <summary>체크 항목이면 왼쪽 거터에 체크 표시(✓)가 그려진다. 토글은 호출자가 Click에서 뒤집는다.</summary>
        public bool Checked { get; set; }

        /// <summary>왼쪽 거터에 그릴 아이콘(16px 논리). <see cref="Checked"/>면 체크가 우선한다.</summary>
        public Image Image { get; set; }

        public object Tag { get; set; }
        public bool IsSeparator { get; internal set; }

        /// <summary>항목을 클릭(또는 Enter)하면 발생한다. 서브메뉴 부모 항목은 발생하지 않는다.</summary>
        public event EventHandler Click;

        public AdvMenuItem() { Enabled = true; }
        public AdvMenuItem(string text) { Text = text; Enabled = true; }

        internal bool HasChildren { get { return _children != null && _children.Count > 0; } }
        internal List<AdvMenuItem> ChildList { get { return _children; } }

        /// <summary>이 항목의 서브메뉴 항목들. 처음 접근할 때 만들어진다.</summary>
        [Browsable(false)]
        public IList<AdvMenuItem> Children { get { return _children ?? (_children = new List<AdvMenuItem>()); } }

        /// <summary>서브메뉴 항목을 추가하고 돌려준다(이 항목이 서브메뉴 부모가 된다).</summary>
        public AdvMenuItem Add(string text)
        {
            var it = new AdvMenuItem(text);
            Children.Add(it);
            return it;
        }

        /// <summary>서브메뉴 텍스트·단축키 항목을 추가하고 돌려준다.</summary>
        public AdvMenuItem Add(string text, string shortcut)
        {
            var it = new AdvMenuItem(text) { Shortcut = shortcut };
            Children.Add(it);
            return it;
        }

        /// <summary>서브메뉴 텍스트·단축키(Keys) 항목을 추가하고 돌려준다.</summary>
        public AdvMenuItem Add(string text, Keys shortcutKeys)
        {
            var it = new AdvMenuItem(text) { ShortcutKeys = shortcutKeys };
            Children.Add(it);
            return it;
        }

        /// <summary>서브메뉴에 구분선을 추가한다.</summary>
        public void AddSeparator() { Children.Add(Separator()); }

        internal void PerformClick()
        {
            var h = Click;
            if (h != null) h(this, EventArgs.Empty);
        }

        /// <summary>구분선 항목을 만든다.</summary>
        public static AdvMenuItem Separator() { return new AdvMenuItem { IsSeparator = true, Enabled = false }; }

        // ── 단축키 표기 ↔ Keys 변환 ──────────────────────────────────

        /// <summary>
        /// "Ctrl+N" 같은 표기를 Keys로 푼다. 대소문자·공백에 관대하다.
        /// 미지원 토큰이 하나라도 있거나 키가 없으면 <see cref="Keys.None"/>이다(부분 해석 안 함).
        /// </summary>
        internal static Keys ParseShortcut(string text)
        {
            if (text == null) return Keys.None;
            text = text.Trim();
            if (text.Length == 0) return Keys.None;

            Keys mods = Keys.None;
            Keys key = Keys.None;

            foreach (string raw in text.Split('+'))
            {
                string t = raw.Trim().ToUpperInvariant();
                if (t.Length == 0) return Keys.None;

                if (t == "CTRL" || t == "CONTROL") { mods |= Keys.Control; continue; }
                if (t == "SHIFT") { mods |= Keys.Shift; continue; }
                if (t == "ALT") { mods |= Keys.Alt; continue; }

                Keys k = ParseKeyToken(t);
                if (k == Keys.None || key != Keys.None) return Keys.None;   // 미지원 토큰·키 두 개
                key = k;
            }

            return key == Keys.None ? Keys.None : mods | key;
        }

        private static Keys ParseKeyToken(string t)
        {
            if (t.Length == 1)
            {
                char c = t[0];
                if (c >= 'A' && c <= 'Z') return Keys.A + (c - 'A');
                if (c >= '0' && c <= '9') return Keys.D0 + (c - '0');
                return Keys.None;
            }

            if (t[0] == 'F' && t.Length <= 3)
            {
                int n;
                if (int.TryParse(t.Substring(1), out n) && n >= 1 && n <= 24)
                    return Keys.F1 + (n - 1);
                return Keys.None;
            }

            switch (t)
            {
                case "DELETE": case "DEL": return Keys.Delete;
                case "INSERT": case "INS": return Keys.Insert;
                case "HOME": return Keys.Home;
                case "END": return Keys.End;
                case "PAGEUP": case "PGUP": return Keys.PageUp;
                case "PAGEDOWN": case "PGDN": return Keys.PageDown;
                case "LEFT": return Keys.Left;
                case "RIGHT": return Keys.Right;
                case "UP": return Keys.Up;
                case "DOWN": return Keys.Down;
                case "TAB": return Keys.Tab;
                case "SPACE": return Keys.Space;
                case "ENTER": case "RETURN": return Keys.Enter;
                case "ESC": case "ESCAPE": return Keys.Escape;
                case "BACK": case "BACKSPACE": return Keys.Back;
            }
            return Keys.None;
        }

        /// <summary>Keys를 표준 표기("Ctrl+Shift+S")로 만든다. None이면 빈 문자열.</summary>
        internal static string FormatShortcut(Keys keys)
        {
            Keys code = keys & Keys.KeyCode;
            if (code == Keys.None) return string.Empty;

            var sb = new System.Text.StringBuilder();
            if ((keys & Keys.Control) != 0) sb.Append("Ctrl+");
            if ((keys & Keys.Shift) != 0) sb.Append("Shift+");
            if ((keys & Keys.Alt) != 0) sb.Append("Alt+");
            sb.Append(FormatKeyToken(code));
            return sb.ToString();
        }

        private static string FormatKeyToken(Keys k)
        {
            if (k >= Keys.D0 && k <= Keys.D9) return ((char)('0' + (k - Keys.D0))).ToString();
            switch (k)
            {
                case Keys.Delete: return "Del";
                case Keys.Insert: return "Ins";
                case Keys.Escape: return "Esc";
                case Keys.PageUp: return "PgUp";
                case Keys.PageDown: return "PgDn";
            }
            return k.ToString();
        }
    }

    /// <summary>
    /// 팝업 안에서 메뉴를 직접 그리는 뷰. 서브메뉴를 별도 창이 아니라 이 한 표면 위에
    /// 오른쪽 '칸(column)'으로 펼쳐 그린다(경량화 — 창·메시지 필터 추가 없음).
    /// 항목 선택 시 <see cref="ItemChosen"/>, 닫아야 하면 <see cref="CloseRequested"/>,
    /// 칸이 열리고 닫혀 크기가 바뀌면 <see cref="LayoutResized"/>를 알린다.
    /// </summary>
    internal sealed class AdvMenuView : Control
    {
        private const int ItemH = 26;
        private const int SepH = 9;
        private const int TextLeft = 28;
        private const int IconSize = 16;   // 거터 아이콘(논리 px)
        private const int RightPad = 26;
        private const int ShortcutGap = 28;
        private const int ChevW = 14;      // 서브메뉴 셰브런 자리
        private const int ColPad = 5;      // 칸 위아래 여백
        private const int ColOverlap = 4;  // 서브메뉴 칸이 부모 칸 오른쪽에 겹치는 정도

        private sealed class Col
        {
            public readonly List<AdvMenuItem> Items;
            public int Hover = -1;
            public int OpenChild = -1;
            public Rectangle Bounds;   // 뷰 좌표
            public Col(List<AdvMenuItem> items) { Items = items; }
        }

        private readonly List<Col> _cols = new List<Col>();
        private int _lastFlatCount = -1;   // 접근성: 평면 자식 수 변화 감지(Reorder 통지용)

        public event Action<AdvMenuItem> ItemChosen;
        public event Action CloseRequested;
        public event Action LayoutResized;

        public AdvMenuView(List<AdvMenuItem> rootItems)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            _cols.Add(new Col(rootItems));
            LayoutColumns();
            AdvThemeManager.ThemeChanged += OnGlobalThemeChanged;
        }

        private void OnGlobalThemeChanged(object sender, EventArgs e) { if (!IsDisposed) Invalidate(); }

        /// <summary>각 칸의 뷰 좌표 사각형(호스트가 창 Region을 이 합집합으로 잡는다).</summary>
        public Rectangle[] ColumnBounds
        {
            get { var arr = new Rectangle[_cols.Count]; for (int i = 0; i < _cols.Count; i++) arr[i] = _cols[i].Bounds; return arr; }
        }

        // ── 측정·레이아웃 ─────────────────────────────────────────────

        private int ColWidth(List<AdvMenuItem> items)
        {
            int w = 120;
            using (var sf = new Font(Font, FontStyle.Regular))
                foreach (var it in items)
                {
                    if (it.IsSeparator) continue;
                    int tw = TextRenderer.MeasureText(it.Text ?? "", sf).Width;
                    int extra = it.HasChildren
                        ? AdvGraphics.Scale(this, ChevW)
                        : (string.IsNullOrEmpty(it.Shortcut) ? 0 : ShortcutGap + TextRenderer.MeasureText(it.Shortcut, sf).Width);
                    w = Math.Max(w, TextLeft + tw + extra + RightPad);
                }
            return w;
        }

        private static int ColHeight(List<AdvMenuItem> items)
        {
            int h = ColPad * 2;
            foreach (var it in items) h += it.IsSeparator ? SepH : ItemH;
            return h;
        }

        private static int ItemTop(Col col, int index)
        {
            int y = col.Bounds.Top + ColPad;
            for (int i = 0; i < index; i++) y += col.Items[i].IsSeparator ? SepH : ItemH;
            return y;
        }

        private static Rectangle ItemRect(Col col, int index)
        {
            int h = col.Items[index].IsSeparator ? SepH : ItemH;
            return new Rectangle(col.Bounds.Left, ItemTop(col, index), col.Bounds.Width, h);
        }

        private void LayoutColumns()
        {
            int maxRight = 0, maxBottom = 0, minTop = 0;
            for (int c = 0; c < _cols.Count; c++)
            {
                var col = _cols[c];
                int w = ColWidth(col.Items), h = ColHeight(col.Items);
                int cx, cy;
                if (c == 0) { cx = 0; cy = 0; }
                else
                {
                    var parent = _cols[c - 1];
                    cx = parent.Bounds.Right - ColOverlap;
                    cy = ItemTop(parent, parent.OpenChild) - ColPad;   // 부모 항목 높이에 맞춤
                }
                col.Bounds = new Rectangle(cx, cy, w, h);
                maxRight = Math.Max(maxRight, col.Bounds.Right);
                maxBottom = Math.Max(maxBottom, col.Bounds.Bottom);
                minTop = Math.Min(minTop, col.Bounds.Top);
            }

            // 서브 칸이 위로 넘치면(부모 항목이 아래쪽) 전체를 아래로 밀어 음수 좌표를 없앤다
            if (minTop < 0)
            {
                for (int c = 0; c < _cols.Count; c++)
                {
                    var b = _cols[c].Bounds;
                    _cols[c].Bounds = new Rectangle(b.X, b.Y - minTop, b.Width, b.Height);
                }
                maxBottom -= minTop;
            }

            var newSize = new Size(Math.Max(1, maxRight), Math.Max(1, maxBottom));
            bool changed = Size != newSize;
            if (changed) Size = newSize;
            var h2 = LayoutResized;
            if (h2 != null) h2();
            Invalidate();

            // 열린 칸 수가 바뀌어 평면 자식 집합이 달라지면 스크린리더에 재구성을 알린다
            if (IsHandleCreated)
            {
                int fc = FlatItemCount();
                if (fc != _lastFlatCount) { _lastFlatCount = fc; AccessibilityNotifyClients(AccessibleEvents.Reorder, -1); }
            }
        }

        // ── 그리기 ────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = AdvThemeManager.Current;
            var g = e.Graphics;
            for (int c = 0; c < _cols.Count; c++) DrawColumn(g, theme, _cols[c]);
        }

        private void DrawColumn(Graphics g, AdvTheme theme, Col col)
        {
            var b = col.Bounds;
            var frame = new Rectangle(b.Left, b.Top, b.Width - 1, b.Height - 1);
            AdvFrameRenderer.Draw(g, frame, theme, new AdvCorners(8), 1,
                                  theme.Surface, Color.Empty, theme.Border, null);

            for (int i = 0; i < col.Items.Count; i++)
            {
                var it = col.Items[i];
                var r = ItemRect(col, i);

                if (it.IsSeparator)
                {
                    using (var pen = new Pen(theme.Border))
                        g.DrawLine(pen, b.Left + 8, r.Top + SepH / 2, b.Right - 8, r.Top + SepH / 2);
                    continue;
                }

                bool hot = (i == col.Hover || i == col.OpenChild) && it.Enabled;
                var row = new Rectangle(b.Left + 4, r.Top, b.Width - 8, ItemH);
                if (hot)
                    using (var br = new SolidBrush(theme.Accent))
                    using (var path = AdvGraphics.CreateRoundedRect(row, new AdvCorners(5)))
                        g.FillPath(br, path);

                Color fg = !it.Enabled ? theme.TextDisabled : hot ? theme.OnAccent : theme.Text;

                // 왼쪽 거터(행 안쪽~TextLeft): 체크 표시가 아이콘보다 우선
                if (it.Checked)
                {
                    int cs = AdvGraphics.Scale(this, 10);
                    var gutter = new Rectangle(b.Left + 4, r.Top, TextLeft - 8, ItemH);
                    var chk = new Rectangle(gutter.Left + (gutter.Width - cs) / 2,
                                            gutter.Top + (gutter.Height - cs) / 2, cs, cs);
                    AdvGraphics.DrawCheckMark(g, this, chk, !it.Enabled ? theme.TextDisabled : hot ? theme.OnAccent : theme.Accent);
                }
                else if (it.Image != null)
                {
                    int isz = AdvGraphics.Scale(this, IconSize);
                    var gutter = new Rectangle(b.Left + 4, r.Top, TextLeft - 8, ItemH);
                    var ir = new Rectangle(gutter.Left + (gutter.Width - isz) / 2,
                                           gutter.Top + (gutter.Height - isz) / 2, isz, isz);
                    if (it.Enabled) g.DrawImage(it.Image, ir);
                    else AdvGraphics.DrawImageDisabled(g, it.Image, ir);
                }

                var textRect = Rectangle.FromLTRB(b.Left + TextLeft, r.Top, b.Right - RightPad, r.Bottom);
                TextRenderer.DrawText(g, it.Text ?? "", Font, textRect, fg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                if (it.HasChildren)
                {
                    var chev = new Rectangle(b.Right - RightPad + 4, r.Top, AdvGraphics.Scale(this, ChevW), ItemH);
                    Color cc = !it.Enabled ? theme.TextDisabled : hot ? theme.OnAccent : theme.TextMuted;
                    AdvGraphics.DrawChevron(g, this, chev, AdvGraphics.ChevronDirection.Right, cc, 7, 4, 1.4f, 0);
                }
                else if (!string.IsNullOrEmpty(it.Shortcut))
                {
                    Color sc = !it.Enabled ? theme.TextDisabled : hot ? theme.OnAccent : theme.TextMuted;
                    TextRenderer.DrawText(g, it.Shortcut, Font, textRect, sc,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            }
        }

        // ── 히트테스트 ────────────────────────────────────────────────

        /// <summary>칸·항목 히트테스트. 겹친 영역은 더 깊은 칸이 위에 있으므로 뒤에서부터 본다.</summary>
        private void HitTest(Point p, out int colIndex, out int itemIndex)
        {
            colIndex = itemIndex = -1;
            for (int c = _cols.Count - 1; c >= 0; c--)
            {
                var col = _cols[c];
                if (!col.Bounds.Contains(p)) continue;
                colIndex = c;
                int top = col.Bounds.Top + ColPad;
                for (int i = 0; i < col.Items.Count; i++)
                {
                    int h = col.Items[i].IsSeparator ? SepH : ItemH;
                    if (p.Y >= top && p.Y < top + h) { itemIndex = i; return; }
                    top += h;
                }
                return;
            }
        }

        private void TruncateTo(int colIndex)
        {
            for (int c = _cols.Count - 1; c > colIndex; c--) _cols.RemoveAt(c);
        }

        // ── 마우스 ────────────────────────────────────────────────────

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int ci, ii;
            HitTest(e.Location, out ci, out ii);
            if (ci < 0) return;   // 칸 사이 빈 곳: 그대로 둔다(마우스가 서브 칸으로 가는 중일 수 있음)

            var col = _cols[ci];
            int hi = (ii >= 0 && !col.Items[ii].IsSeparator && col.Items[ii].Enabled) ? ii : -1;

            bool alreadyDeepest = ci == _cols.Count - 1;
            if (col.Hover == hi && (alreadyDeepest || col.OpenChild == hi)) return;

            TruncateTo(ci);
            col.Hover = hi;
            col.OpenChild = -1;
            if (hi >= 0 && col.Items[hi].HasChildren)
            {
                col.OpenChild = hi;
                _cols.Add(new Col(col.Items[hi].ChildList));
            }
            LayoutColumns();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int ci, ii;
            HitTest(e.Location, out ci, out ii);
            if (ci < 0 || ii < 0) return;
            var it = _cols[ci].Items[ii];
            if (it.IsSeparator || !it.Enabled) return;

            if (it.HasChildren)
            {
                if (_cols[ci].OpenChild != ii)
                {
                    TruncateTo(ci);
                    _cols[ci].Hover = ii;
                    _cols[ci].OpenChild = ii;
                    _cols.Add(new Col(it.ChildList));
                    LayoutColumns();
                }
                return;
            }
            Choose(it);
        }

        // ── 키보드 ────────────────────────────────────────────────────

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up: case Keys.Down: case Keys.Left: case Keys.Right:
                case Keys.Return: case Keys.Escape:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            var deep = _cols[_cols.Count - 1];
            switch (e.KeyCode)
            {
                case Keys.Down: MoveHover(deep, +1); e.Handled = true; break;
                case Keys.Up: MoveHover(deep, -1); e.Handled = true; break;
                case Keys.Right:
                    if (deep.Hover >= 0 && deep.Items[deep.Hover].HasChildren) OpenChildKeyboard(deep);
                    e.Handled = true; break;
                case Keys.Left:
                    if (_cols.Count > 1)
                    {
                        _cols.RemoveAt(_cols.Count - 1);
                        _cols[_cols.Count - 1].OpenChild = -1;
                        LayoutColumns();
                    }
                    e.Handled = true; break;
                case Keys.Return:
                    if (deep.Hover >= 0)
                    {
                        var it = deep.Items[deep.Hover];
                        if (it.HasChildren) OpenChildKeyboard(deep); else Choose(it);
                    }
                    e.Handled = true; break;
                case Keys.Escape:
                    var c = CloseRequested; if (c != null) c();
                    e.Handled = true; break;
            }
        }

        private void OpenChildKeyboard(Col deep)
        {
            int hi = deep.Hover;
            deep.OpenChild = hi;
            var child = new Col(deep.Items[hi].ChildList);
            _cols.Add(child);
            LayoutColumns();
            MoveHover(child, +1);   // 첫 선택 가능 항목
        }

        /// <summary>구분선·비활성을 건너뛰며 그 칸의 hover를 이동한다.</summary>
        private void MoveHover(Col col, int dir)
        {
            if (col.Items.Count == 0) return;
            int i = col.Hover;
            for (int n = 0; n < col.Items.Count; n++)
            {
                i += dir;
                if (i < 0) i = col.Items.Count - 1;
                if (i >= col.Items.Count) i = 0;
                if (!col.Items[i].IsSeparator && col.Items[i].Enabled)
                {
                    col.Hover = i; Invalidate();
                    NotifyMenuAccFocus(col);   // 호버 이동을 스크린리더에 라이브로 알린다
                    return;
                }
            }
        }

        private void Choose(AdvMenuItem it)
        {
            var h = ItemChosen;
            if (h != null) h(it);
        }

        // ── 접근성(스크린리더/UI Automation) ─────────────────────────
        // 보이는 항목 평면 모델: 팝업(MenuPopup)의 직접 자식 = 현재 열린 모든 칸의 항목을 칸 순서대로
        // 이어붙인 평면 리스트. 이래야 MSAA 단일 childID(= 평면 인덱스)로 호버 이동을 라이브로 정확히
        // 지정할 수 있다. 서브메뉴 항목은 그 칸이 열려야 리스트에 나타난다(네이티브 메뉴와 동일).

        private int FlatItemCount()
        {
            int n = 0;
            foreach (var col in _cols) n += col.Items.Count;
            return n;
        }

        private bool LocateFlat(int flat, out int colIndex, out int itemIndex)
        {
            colIndex = itemIndex = -1;
            if (flat < 0) return false;
            for (int c = 0; c < _cols.Count; c++)
            {
                int n = _cols[c].Items.Count;
                if (flat < n) { colIndex = c; itemIndex = flat; return true; }
                flat -= n;
            }
            return false;
        }

        private int FlatIndexOf(int colIndex, int itemIndex)
        {
            int baseIdx = 0;
            for (int c = 0; c < colIndex && c < _cols.Count; c++) baseIdx += _cols[c].Items.Count;
            return baseIdx + itemIndex;
        }

        private Rectangle ItemScreenRectAt(int colIndex, int itemIndex)
        {
            if (colIndex < 0 || colIndex >= _cols.Count) return Rectangle.Empty;
            return RectangleToScreen(ItemRect(_cols[colIndex], itemIndex));
        }

        /// <summary>부모 항목의 서브메뉴 칸을 연다(접근성 기본 동작·키보드 공통 경로).</summary>
        private void OpenSubmenuFor(int colIndex, int itemIndex)
        {
            if (colIndex < 0 || colIndex >= _cols.Count) return;
            var col = _cols[colIndex];
            if (itemIndex < 0 || itemIndex >= col.Items.Count) return;
            var it = col.Items[itemIndex];
            if (!it.HasChildren || col.OpenChild == itemIndex) return;
            TruncateTo(colIndex);
            col.Hover = itemIndex;
            col.OpenChild = itemIndex;
            _cols.Add(new Col(it.ChildList));
            LayoutColumns();
        }

        /// <summary>호버(키보드 선택) 이동을 스크린리더에 라이브로 알린다. childID = 평면 인덱스.</summary>
        private void NotifyMenuAccFocus(Col col)
        {
            if (!Focused || col.Hover < 0) return;
            int colIndex = _cols.IndexOf(col);
            if (colIndex < 0) return;
            AccessibilityNotifyClients(AccessibleEvents.Focus, FlatIndexOf(colIndex, col.Hover));
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new MenuAccessibleObject(this);
        }

        private sealed class MenuAccessibleObject : ControlAccessibleObject
        {
            private readonly AdvMenuView _view;
            public MenuAccessibleObject(AdvMenuView view) : base(view) { _view = view; }

            public override AccessibleRole Role { get { return AccessibleRole.MenuPopup; } }
            public override int GetChildCount() { return _view.FlatItemCount(); }
            public override AccessibleObject GetChild(int index)
            {
                int c, i;
                return _view.LocateFlat(index, out c, out i) ? new MenuItemAccessibleObject(_view, c, i) : null;
            }

            public override AccessibleObject GetSelected()
            {
                // 가장 깊은(활성) 칸의 호버 항목이 현재 선택이다
                for (int c = _view._cols.Count - 1; c >= 0; c--)
                    if (_view._cols[c].Hover >= 0) return new MenuItemAccessibleObject(_view, c, _view._cols[c].Hover);
                return null;
            }

            public override AccessibleObject GetFocused() { return GetSelected(); }
        }

        private sealed class MenuItemAccessibleObject : AccessibleObject
        {
            private readonly AdvMenuView _view;
            private readonly int _c, _i;
            public MenuItemAccessibleObject(AdvMenuView view, int c, int i) { _view = view; _c = c; _i = i; }

            private bool Valid { get { return _c >= 0 && _c < _view._cols.Count && _i >= 0 && _i < _view._cols[_c].Items.Count; } }
            private AdvMenuItem It { get { return _view._cols[_c].Items[_i]; } }

            public override AccessibleObject Parent { get { return _view.AccessibilityObject; } }
            public override AccessibleRole Role
            {
                get { return (!Valid || It.IsSeparator) ? AccessibleRole.Separator : AccessibleRole.MenuItem; }
            }

            public override string Name { get { return (Valid && !It.IsSeparator) ? It.Text : null; } }
            public override string KeyboardShortcut { get { return Valid ? It.Shortcut : null; } }

            public override AccessibleStates State
            {
                get
                {
                    if (!Valid) return AccessibleStates.None;
                    var it = It;
                    if (it.IsSeparator) return AccessibleStates.None;
                    var s = AccessibleStates.Focusable | AccessibleStates.Selectable;
                    if (!it.Enabled) s |= AccessibleStates.Unavailable;
                    if (it.Checked) s |= AccessibleStates.Checked;
                    if (it.HasChildren) s |= AccessibleStates.HasPopup;
                    var col = _view._cols[_c];
                    if (col.Hover == _i) s |= AccessibleStates.Focused | AccessibleStates.Selected | AccessibleStates.HotTracked;
                    if (col.OpenChild == _i) s |= AccessibleStates.Expanded;
                    return s;
                }
            }

            public override Rectangle Bounds { get { return Valid ? _view.ItemScreenRectAt(_c, _i) : Rectangle.Empty; } }

            public override string DefaultAction
            {
                get
                {
                    if (!Valid) return null;
                    var it = It;
                    if (it.IsSeparator || !it.Enabled) return null;
                    return it.HasChildren ? "펼치기" : "실행";
                }
            }

            public override void DoDefaultAction()
            {
                if (!Valid) return;
                var it = It;
                if (it.IsSeparator || !it.Enabled) return;
                if (it.HasChildren) _view.OpenSubmenuFor(_c, _i);
                else _view.Choose(it);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) AdvThemeManager.ThemeChanged -= OnGlobalThemeChanged;
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// 테마 컨텍스트(우클릭) 메뉴. 항목을 담아 두고 <see cref="Show(Control, Point)"/>로 띄운다.
    /// 서브메뉴는 별도 창이 아니라 같은 팝업 안의 오른쪽 칸으로 펼쳐진다(경량).
    /// 부유·바깥 클릭 자동닫힘은 ToolStripDropDown 껍데기가 맡고, 그리기는 전부 커스텀이다.
    /// </summary>
    [ToolboxItem(true)]
    [Description("테마를 따르는 커스텀 컨텍스트 메뉴입니다(서브메뉴 지원).")]
    public class AdvContextMenu : Component
    {
        private readonly List<AdvMenuItem> _items = new List<AdvMenuItem>();
        private ToolStripDropDown _dd;
        private ToolStripControlHost _host;
        private AdvMenuView _view;

        /// <summary>메뉴가 닫힌 뒤 발생한다.</summary>
        public event EventHandler Closed;

        /// <summary>메뉴 항목들. 구분선은 <see cref="AdvMenuItem.Separator"/>로 넣는다.</summary>
        [Browsable(false)]
        public IList<AdvMenuItem> Items { get { return _items; } }

        /// <summary>텍스트 항목을 추가하고 돌려준다. 반환된 항목에 Add로 서브메뉴를 붙일 수 있다.</summary>
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

        /// <summary>텍스트·단축키(Keys) 항목을 추가하고 돌려준다.</summary>
        public AdvMenuItem Add(string text, Keys shortcutKeys)
        {
            var it = new AdvMenuItem(text) { ShortcutKeys = shortcutKeys };
            _items.Add(it);
            return it;
        }

        /// <summary>구분선을 추가한다.</summary>
        public void AddSeparator() { _items.Add(AdvMenuItem.Separator()); }

        /// <summary>화면 좌표에 연다.</summary>
        public void Show(Point screenLocation) { ShowInternal(screenLocation); }

        /// <summary>owner 기준 좌표에 연다.</summary>
        public void Show(Control owner, Point ownerLocation)
        {
            ShowInternal(owner != null ? owner.PointToScreen(ownerLocation) : ownerLocation);
        }

        private void ShowInternal(Point screen)
        {
            Close();
            if (_items.Count == 0) return;

            _view = new AdvMenuView(_items);
            _host = new ToolStripControlHost(_view)
            { AutoSize = false, Margin = Padding.Empty, Padding = Padding.Empty, Size = _view.Size };

            _dd = new ToolStripDropDown
            { Padding = Padding.Empty, AutoClose = true, AutoSize = false, DropShadowEnabled = false };
            _dd.Renderer = BlankRenderer.Instance;
            _dd.Items.Add(_host);

            _view.ItemChosen += it => { Close(); it.PerformClick(); };
            _view.CloseRequested += Close;
            _view.LayoutResized += SyncSizeToView;
            _dd.Closed += (s, e) =>
            {
                var h = Closed;
                if (h != null) h(this, EventArgs.Empty);
                DisposeDropDown();
            };

            SyncSizeToView();      // 크기·Region은 Show 전에 잡는다(원본 관례 — Show 후 리사이즈는 dd를 불안정하게 함)
            _dd.Show(screen);
            _view.Focus();
        }

        /// <summary>서브 칸이 열리고 닫혀 뷰 크기가 바뀌면 팝업 크기·Region·위치를 맞춘다.</summary>
        private void SyncSizeToView()
        {
            if (_dd == null || _view == null) return;
            var size = _view.Size;
            _host.Size = size;
            _dd.Size = size;

            // 창 Region = 각 칸의 둥근 사각형 합집합. 원본과 같은 방식(GraphicsPath 하나로 모아 new Region)으로
            // 만들어 Union/MakeEmpty 경로를 피한다. 이전 Region은 dd가 정리하므로 직접 해제하지 않는다.
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                foreach (var cb in _view.ColumnBounds)
                    using (var cp = AdvGraphics.CreateRoundedRect(cb, new AdvCorners(8)))
                        path.AddPath(cp, false);
                _dd.Region = new Region(path);
            }

            // 서브 칸이 오른쪽/아래로 자라 화면을 벗어나면 전체를 밀어 넣는다(가장자리 근처 메뉴)
            if (_dd.IsHandleCreated)
            {
                var wa = Screen.FromPoint(_dd.Location).WorkingArea;
                int x = _dd.Left, y = _dd.Top;
                if (x + size.Width > wa.Right) x = Math.Max(wa.Left, wa.Right - size.Width);
                if (y + size.Height > wa.Bottom) y = Math.Max(wa.Top, wa.Bottom - size.Height);
                if (x != _dd.Left || y != _dd.Top) _dd.Location = new Point(x, y);
            }
        }

        /// <summary>열려 있으면 닫는다.</summary>
        public void Close()
        {
            if (_dd == null) return;
            try
            {
                if (!_dd.IsDisposed && _dd.Visible) { _dd.Close(); return; }   // 정상 경로: Closed→DisposeDropDown
            }
            catch (ObjectDisposedException) { }   // dd 내부 핸들 상태가 어긋난 드문 경우는 폐기로 넘어간다
            DisposeDropDown();
        }

        private void DisposeDropDown()
        {
            if (_dd == null) return;
            var d = _dd; _dd = null; _host = null; _view = null;
            d.Dispose();   // 창 Region은 dd.Dispose가 정리한다(직접 Region.Dispose하면 이중 해제로 힙 손상)
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

        // ── 접근성(스크린리더/UI Automation) ─────────────────────────

        /// <summary>접근성 Bounds용으로 타이틀 사각형을 최신화한다(아직 안 그려졌으면 측정만 수행).</summary>
        private void EnsureTitleLayout()
        {
            if (!_titlesDirty) return;
            if (IsHandleCreated)
            {
                using (var g = CreateGraphics()) LayoutTitles(g);
            }
            else
            {
                using (var bmp = new Bitmap(1, 1))
                using (var g = Graphics.FromImage(bmp)) LayoutTitles(g);
            }
            _titlesDirty = false;
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new MenuBarAccessibleObject(this);
        }

        private sealed class MenuBarAccessibleObject : ControlAccessibleObject
        {
            private readonly AdvMenuBar _owner;
            public MenuBarAccessibleObject(AdvMenuBar owner) : base(owner) { _owner = owner; }

            public override AccessibleRole Role { get { return AccessibleRole.MenuBar; } }
            public override int GetChildCount() { return _owner._menus.Count; }
            public override AccessibleObject GetChild(int index)
            {
                return index >= 0 && index < _owner._menus.Count
                    ? new TitleAccessibleObject(_owner, index) : null;
            }

            private sealed class TitleAccessibleObject : AccessibleObject
            {
                private readonly AdvMenuBar _o;
                private readonly int _i;
                public TitleAccessibleObject(AdvMenuBar o, int i) { _o = o; _i = i; }

                public override AccessibleObject Parent { get { return _o.AccessibilityObject; } }
                public override AccessibleRole Role { get { return AccessibleRole.MenuItem; } }
                public override string Name { get { return _o._menus[_i].Title; } }

                public override AccessibleStates State
                {
                    get
                    {
                        var s = AccessibleStates.Focusable | AccessibleStates.HasPopup;
                        if (_i == _o._open)
                            s |= AccessibleStates.Expanded | AccessibleStates.Selected | AccessibleStates.HotTracked;
                        else if (_i == _o._hover)
                            s |= AccessibleStates.HotTracked;
                        return s;
                    }
                }

                public override Rectangle Bounds
                {
                    get { _o.EnsureTitleLayout(); return _o.RectangleToScreen(_o._menus[_i].Rect); }
                }

                public override string DefaultAction { get { return _i == _o._open ? "닫기" : "열기"; } }
                public override void DoDefaultAction()
                {
                    if (_i == _o._open) _o.CloseOpen();
                    else _o.OpenMenu(_i);
                }
            }
        }

        // ── 단축키 폼 바인딩 ─────────────────────────────────────────
        // 메뉴바가 폼에 붙으면 폼 KeyPreview를 켜고 KeyDown을 받아, 등록된 단축키와
        // 일치하는 항목을 폼 전역에서 실행한다. 떨어지거나 파기되면 원래 상태로 되돌린다.

        private Form _hookedForm;
        private bool _prevKeyPreview;

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            HookForm(FindForm());
        }

        private void HookForm(Form form)
        {
            if (ReferenceEquals(_hookedForm, form)) return;
            UnhookForm();
            if (form == null) return;

            _hookedForm = form;
            _prevKeyPreview = form.KeyPreview;
            form.KeyPreview = true;
            form.KeyDown += OnFormKeyDown;
        }

        private void UnhookForm()
        {
            if (_hookedForm == null) return;
            _hookedForm.KeyDown -= OnFormKeyDown;
            _hookedForm.KeyPreview = _prevKeyPreview;
            _hookedForm = null;
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (!Enabled) return;

            var item = FindShortcut(e.KeyData);
            if (item == null || !item.Enabled) return;

            item.PerformClick();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        /// <summary>등록된 모든 메뉴(서브메뉴 포함)에서 단축키가 일치하는 항목을 찾는다.</summary>
        private AdvMenuItem FindShortcut(Keys keys)
        {
            if ((keys & Keys.KeyCode) == Keys.None) return null;
            foreach (var m in _menus)
            {
                var found = FindShortcutIn(m.Menu.Items, keys);
                if (found != null) return found;
            }
            return null;
        }

        private static AdvMenuItem FindShortcutIn(IList<AdvMenuItem> items, Keys keys)
        {
            foreach (var it in items)
            {
                if (it.IsSeparator) continue;
                if (it.ShortcutKeys == keys) return it;
                if (it.HasChildren)
                {
                    var found = FindShortcutIn(it.ChildList, keys);
                    if (found != null) return found;
                }
            }
            return null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnhookForm();
                foreach (var m in _menus) m.Menu.Dispose();
            }
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
