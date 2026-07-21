using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>탭 줄의 시각 스타일.</summary>
    public enum AdvTabStyle
    {
        /// <summary>글자만 두고 선택 탭 아래 강조 밑줄(Chrome/Material 스타일).</summary>
        Underline,
        /// <summary>선택 탭을 강조색 알약으로 채운다(세그먼트 컨트롤).</summary>
        Segmented,
        /// <summary>선택 탭이 위가 둥근 카드로 페이지와 연결된다.</summary>
        Card
    }

    /// <summary>
    /// 테마를 따르는 탭 컨트롤. 탭 항목과 페이지 배경은 직접 그리고,
    /// 페이지 관리(추가·삭제·전환)는 표준 <see cref="TabControl"/> 기능을 그대로 쓴다.
    /// 덕분에 디자이너에서 탭을 추가·삭제할 수 있다.
    /// </summary>
    [ToolboxItem(true)]
    [Description("테마를 따르는 탭 컨트롤입니다.")]
    public class AdvTabControl : TabControl
    {
        private AdvTheme _theme;
        private readonly AdvAppearance _appearance = new AdvAppearance();
        private AdvColorOverrides _colors;
        private AdvTheme _mergedTheme;
        private AdvTheme _mergedBase;
        private bool _colorsDirty;
        private AdvTabControlOptions _options;
        private AdvTabStyle _tabStyle = AdvTabStyle.Underline;
        private int _hotIndex = -1;
        private bool _showCloseButtons;
        private int _closeHotIndex = -1;

        private const int CloseAreaWidth = 22;   // 닫기 × 자리로 탭 오른쪽에 비워 두는 폭
        private const int BasePadX = 16;

        /// <summary>탭의 닫기(×) 버튼을 누르기 직전에 발생한다. Cancel로 막을 수 있다.</summary>
        [Category("Behavior")]
        [Description("탭 닫기 버튼을 눌러 탭이 닫히기 직전에 발생합니다. Cancel로 막을 수 있습니다.")]
        public event EventHandler<TabCloseEventArgs> TabClosing;

        /// <summary>탭이 닫힌 뒤에 발생한다.</summary>
        [Category("Behavior")]
        [Description("탭이 닫힌 뒤에 발생합니다.")]
        public event EventHandler<TabCloseEventArgs> TabClosed;

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvTabControlOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvTabControlOptions(this)); }
        }

        public AdvTabControl()
        {
            _appearance.Changed += OnAppearanceChanged;

            // 탭 항목을 직접 그리려면 오너 드로잉이 필요하다
            DrawMode = TabDrawMode.OwnerDrawFixed;
            SizeMode = TabSizeMode.Normal;
            ItemSize = new Size(0, 30);
            Padding = new Point(16, 4);

            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            AdvThemeManager.ThemeChanged += OnGlobalThemeChanged;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvTheme Theme
        {
            get { return _theme; }
            set
            {
                if (ReferenceEquals(_theme, value)) return;
                _theme = value;
                ApplyPageColors();
                Invalidate();
            }
        }

        /// <summary>
        /// 이 탭 컨트롤의 모양 설정. 다른 컨트롤과 달리 속성 창에는 내놓지 않는다 —
        /// 탭 모양과 테두리를 Win32가 그리는 탓에 Corners·BorderWidth·Elevated·
        /// ShowFocusGlow·TransitionDuration이 그리기에 전혀 반영되지 않기 때문이다.
        /// 실제로 먹는 ThemeMode만 <see cref="ThemeMode"/>로 따로 노출한다.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvAppearance Styling
        {
            get { return _appearance; }
        }

        /// <summary>
        /// 이 컨트롤이 따를 테마. Inherit이면 전역 테마를 따른다.
        /// </summary>
        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvThemeMode.Inherit)]
        [Description("이 탭 컨트롤이 따를 테마입니다. Inherit이면 전역 테마를 따릅니다.")]
        public AdvThemeMode ThemeMode
        {
            get { return _appearance.ThemeMode; }
            set { _appearance.ThemeMode = value; }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvTabStyle.Underline)]
        [Description("탭 줄의 시각 스타일입니다. Underline·Segmented·Card 중에서 고릅니다.")]
        public AdvTabStyle TabStyle
        {
            get { return _tabStyle; }
            set { if (_tabStyle == value) return; _tabStyle = value; Invalidate(); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("각 탭에 닫기(×) 버튼을 표시합니다. 누르면 TabClosing→TabClosed가 발생합니다.")]
        public bool ShowTabCloseButtons
        {
            get { return _showCloseButtons; }
            set
            {
                if (_showCloseButtons == value) return;
                _showCloseButtons = value;
                // 닫기 × 자리를 확보하려고 탭 안쪽 여백을 넓힌다
                Padding = new Point(BasePadX + (value ? CloseAreaWidth : 0), Padding.Y);
                Invalidate();
            }
        }

        /// <summary>탭 i의 닫기 × 상자. 그리기와 히트 테스트가 같은 값을 써야 한다.</summary>
        private Rectangle CloseRect(int i)
        {
            var tab = GetTabRect(i);
            int sz = 14;
            return new Rectangle(tab.Right - sz - 6, tab.Top + (tab.Height - sz) / 2, sz, sz);
        }

        /// <summary>탭을 닫는다. TabClosing이 취소하면 그대로 둔다.</summary>
        public void CloseTab(int index)
        {
            if (index < 0 || index >= TabCount) return;

            var page = TabPages[index];
            var closing = new TabCloseEventArgs(index, page);
            var h = TabClosing;
            if (h != null) h(this, closing);
            if (closing.Cancel) return;

            TabPages.Remove(page);
            _closeHotIndex = -1;
            _hotIndex = -1;

            var hc = TabClosed;
            if (hc != null) hc(this, new TabCloseEventArgs(index, page));
            Invalidate();
        }

        /// <summary>
        /// 이 탭 컨트롤에만 적용하는 색 재정의. 비워 둔 색은 테마를 따른다.
        /// 탭 항목 색과 페이지 배경(Surface·Text·Accent 등)에 반영된다.
        /// 속성 창에서는 AdvancedControlOptions 안에서만 보인다.
        /// </summary>
        [Browsable(false)]
        [Description("이 탭 컨트롤에만 적용하는 색입니다. 비워 두면 테마 색을 따릅니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public AdvColorOverrides Palette
        {
            get
            {
                if (_colors == null)
                {
                    _colors = new AdvColorOverrides();
                    _colors.Changed += OnColorsChanged;
                }
                return _colors;
            }
        }

        private void OnColorsChanged(object sender, EventArgs e)
        {
            _colorsDirty = true;
            ApplyPageColors();
            Invalidate();
        }

        protected AdvTheme EffectiveTheme
        {
            get
            {
                var baseTheme = _theme ?? _appearance.ResolveTheme() ?? AdvThemeManager.Current;
                if (_colors == null || !_colors.HasAny) return baseTheme;

                if (_colorsDirty || !ReferenceEquals(_mergedBase, baseTheme))
                {
                    _mergedTheme = _colors.Apply(baseTheme);
                    _mergedBase = baseTheme;
                    _colorsDirty = false;
                }
                return _mergedTheme;
            }
        }

        private void OnAppearanceChanged(object sender, EventArgs e)
        {
            ApplyPageColors();
            Invalidate();
        }

        private void OnGlobalThemeChanged(object sender, EventArgs e)
        {
            if (_theme != null) return;
            ApplyPageColors();
            Invalidate();
        }

        /// <summary>
        /// 페이지 안쪽은 TabPage가 자기 배경색으로 칠하므로, 여기는 직접 그리지 못한다.
        /// 테마가 바뀔 때마다 각 페이지 색을 다시 맞춘다.
        /// </summary>
        private void ApplyPageColors()
        {
            var theme = EffectiveTheme;

            foreach (TabPage page in TabPages)
            {
                page.BackColor = theme.Surface;
                page.ForeColor = theme.Text;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyPageColors();
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);

            var page = e.Control as TabPage;
            if (page != null)
            {
                var theme = EffectiveTheme;
                page.BackColor = theme.Surface;
                page.ForeColor = theme.Text;
            }
        }

        private const int WM_PAINT = 0x000F;
        private const int WM_PRINTCLIENT = 0x0318;

        /// <summary>
        /// GetTabRect는 Win32 테두리에 맞물린 좌표라, 그대로 칠하면 바깥에 시스템 색 틈이 보인다.
        /// 탭을 칠할 때 사방으로 넓히는 폭. 그리는 쪽과 덧칠하는 쪽이 반드시 같은 값을 써야 한다.
        /// </summary>
        private const int TabEdge = 2;

        /// <summary>
        /// 탭 줄에서 마지막 탭 오른쪽 빈 자리는 Win32가 시스템 색으로 칠한다.
        /// OnPaint로는 이 영역을 덮을 수 없어, 기본 그리기가 끝난 뒤 직접 덧칠한다.
        /// 화면 출력은 WM_PAINT, DrawToBitmap·인쇄는 WM_PRINTCLIENT로 들어오므로 둘 다 받는다.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (TabCount == 0 || !IsHandleCreated) return;

            if (m.Msg == WM_PAINT)
            {
                using (var g = Graphics.FromHwnd(Handle))
                    PaintChrome(g);
            }
            else if (m.Msg == WM_PRINTCLIENT && m.WParam != IntPtr.Zero)
            {
                // 이 경우 그릴 대상은 화면이 아니라 wParam으로 넘어온 DC다
                using (var g = Graphics.FromHdc(m.WParam))
                    PaintChrome(g);
            }
        }

        /// <summary>
        /// 탭 줄을 통째로 다시 그린다. Win32가 그린 박스·시스템 색 이음새를 균일하게 덮은 뒤
        /// 선택한 <see cref="TabStyle"/>에 맞춰 밑줄·알약·카드와 글자를 얹는다.
        /// </summary>
        private void PaintChrome(Graphics g)
        {
            var theme = EffectiveTheme;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var client = ClientRectangle;
            var page = DisplayRectangle;
            var last = GetTabRect(TabCount - 1);
            int stripBottom = last.Bottom;
            Color stripBg = StripBackColor(theme);

            // 1) 탭 줄 전체를 균일하게 덮어 Win32 흔적(박스·흰 이음새)을 지운다
            using (var b = new SolidBrush(stripBg))
                g.FillRectangle(b, Rectangle.FromLTRB(client.Left, client.Top, client.Right, stripBottom));

            // 2) 탭 줄 아래(Win32가 그린 굵은 3D 테두리 자리 포함)를 페이지 색으로 덮은 뒤
            //    1px 테두리만 두른다. 라이브러리 다른 컨트롤과 굵기를 맞추고, 페이지에 든
            //    자식 컨트롤의 테두리와 이중선이 되지 않게 한다. Card 선택 탭은 아래 step 3에서
            //    자기 구간의 윗변을 다시 덮어 페이지와 연결한다.
            using (var fill = new SolidBrush(theme.Surface))
                g.FillRectangle(fill, Rectangle.FromLTRB(client.Left, stripBottom, client.Right, client.Bottom));

            using (var pen = new Pen(theme.Border))
                g.DrawRectangle(pen, client.Left, stripBottom - 1, client.Width - 1, client.Bottom - stripBottom);

            // 3) 탭마다 스타일 장식 + 글자
            for (int i = 0; i < TabCount; i++)
            {
                var tab = GetTabRect(i);
                bool sel = i == SelectedIndex;
                bool hot = i == _hotIndex && !sel;

                switch (_tabStyle)
                {
                    case AdvTabStyle.Segmented: DrawPillTab(g, tab, sel, hot, theme); break;
                    case AdvTabStyle.Card: DrawCardTab(g, tab, sel, theme, page.Top); break;
                    default: DrawUnderlineTab(g, tab, sel, hot, theme, stripBottom); break;
                }

                // 닫기 × 자리를 비워 두려고 글자 영역의 오른쪽을 줄인다
                var textRect = _showCloseButtons
                    ? Rectangle.FromLTRB(tab.Left, tab.Top, tab.Right - CloseAreaWidth, tab.Bottom)
                    : tab;

                TextRenderer.DrawText(g, TabPages[i].Text, Font, textRect, TabForeColor(sel, hot, theme),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                  | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                if (_showCloseButtons)
                    DrawTabClose(g, i, i == _closeHotIndex, theme);
            }
        }

        /// <summary>탭의 닫기 × 를 그린다. 호버 시 옅은 원 배경을 깐다.</summary>
        private void DrawTabClose(Graphics g, int index, bool hot, AdvTheme theme)
        {
            var box = CloseRect(index);

            if (hot)
                using (var b = new SolidBrush(theme.SurfaceHover))
                using (var p = AdvGraphics.CreateRoundedRect(box, box.Width / 2))
                    g.FillPath(b, p);

            var inner = Rectangle.Inflate(box, -box.Width / 4, -box.Height / 4);
            using (var pen = new Pen(hot ? theme.Text : theme.TextMuted, 1.4f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pen, inner.Left, inner.Top, inner.Right, inner.Bottom);
                g.DrawLine(pen, inner.Left, inner.Bottom, inner.Right, inner.Top);
            }
        }

        /// <summary>탭 줄 바탕색. Card는 살짝 눌린 느낌으로 SurfaceHover, 나머지는 페이지와 이어지는 Surface.</summary>
        private Color StripBackColor(AdvTheme theme)
        {
            return _tabStyle == AdvTabStyle.Card ? theme.SurfaceHover : theme.Surface;
        }

        private Color TabForeColor(bool selected, bool hot, AdvTheme theme)
        {
            if (_tabStyle == AdvTabStyle.Segmented && selected) return theme.OnAccent;
            if (selected) return theme.Text;
            return hot ? theme.Text : theme.TextMuted;
        }

        private void DrawUnderlineTab(Graphics g, Rectangle tab, bool sel, bool hot, AdvTheme theme, int stripBottom)
        {
            int left = tab.Left - TabEdge, right = tab.Right + TabEdge;
            Color? bar = sel ? theme.Accent : hot ? (Color?)theme.BorderHover : null;
            if (bar == null) return;
            using (var b = new SolidBrush(bar.Value))
                g.FillRectangle(b, Rectangle.FromLTRB(left, stripBottom - 2, right, stripBottom));
        }

        private void DrawPillTab(Graphics g, Rectangle tab, bool sel, bool hot, AdvTheme theme)
        {
            if (!sel && !hot) return;

            var pill = Rectangle.Inflate(tab, TabEdge - 1, -3);
            using (var path = AdvGraphics.CreateRoundedRect(pill, new AdvCorners(Math.Max(1, pill.Height / 2))))
            using (var b = new SolidBrush(sel ? theme.Accent : theme.SurfaceHover))
                g.FillPath(b, path);
        }

        private void DrawCardTab(Graphics g, Rectangle tab, bool sel, AdvTheme theme, int pageTop)
        {
            if (!sel) return;

            const int rad = 6;
            // 위가 둥근 카드. 아래는 페이지 안까지 1px 겹쳐 채워 구분선을 덮고 페이지와 잇는다
            var card = Rectangle.FromLTRB(tab.Left - TabEdge, tab.Top, tab.Right + TabEdge, pageTop + 1);

            using (var fill = AdvGraphics.CreateRoundedRect(card, new AdvCorners(rad, rad, 0, 0)))
            using (var b = new SolidBrush(theme.Surface))
                g.FillPath(b, fill);

            // 위+좌+우만 테두리(아래는 페이지로 열어 둔다)
            using (var path = new GraphicsPath())
            {
                float d = rad * 2;
                path.StartFigure();
                path.AddLine(card.Left, card.Bottom, card.Left, card.Top + rad);
                path.AddArc(card.Left, card.Top, d, d, 180, 90);
                path.AddArc(card.Right - d, card.Top, d, d, 270, 90);
                path.AddLine(card.Right, card.Top + rad, card.Right, card.Bottom);
                using (var pen = new Pen(theme.Border))
                    g.DrawPath(pen, path);
            }
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            // 실제 탭 그리기는 PaintChrome(클리핑 없음)에서 스타일별로 한다.
            // 여기서는 Win32 기본 그리기가 남지 않도록 탭 배경만 균일하게 덮는다.
            if (e.Index < 0 || e.Index >= TabPages.Count) return;

            var r = e.Bounds;
            r.Inflate(TabEdge, TabEdge);
            using (var brush = new SolidBrush(StripBackColor(EffectiveTheme)))
                e.Graphics.FillRectangle(brush, r);

            base.OnDrawItem(e);
        }

        // 호버 탭을 직접 추적한다(그리기를 PaintChrome로 모았으므로 Win32 HotLight를 쓰지 않는다)
        protected override void OnMouseMove(MouseEventArgs e)
        {
            int hot = -1;
            for (int i = 0; i < TabCount; i++)
                if (GetTabRect(i).Contains(e.Location)) { hot = i; break; }

            if (hot != _hotIndex) { _hotIndex = hot; Invalidate(); }

            int closeHot = -1;
            if (_showCloseButtons)
                for (int i = 0; i < TabCount; i++)
                    if (CloseRect(i).Contains(e.Location)) { closeHot = i; break; }

            if (closeHot != _closeHotIndex) { _closeHotIndex = closeHot; Invalidate(); }

            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hotIndex != -1) { _hotIndex = -1; Invalidate(); }
            if (_closeHotIndex != -1) { _closeHotIndex = -1; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            // 닫기 × 를 누르면 탭 선택 대신 그 탭을 닫는다(base를 부르지 않아 선택이 바뀌지 않는다)
            if (_showCloseButtons && e.Button == MouseButtons.Left)
            {
                for (int i = 0; i < TabCount; i++)
                    if (CloseRect(i).Contains(e.Location)) { CloseTab(i); return; }
            }
            base.OnMouseDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 정적 이벤트라 해제하지 않으면 폼이 닫혀도 컨트롤이 수거되지 않는다
                AdvThemeManager.ThemeChanged -= OnGlobalThemeChanged;
                _appearance.Changed -= OnAppearanceChanged;

                if (_colors != null) _colors.Changed -= OnColorsChanged;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// AdvTabControl이 추가한 속성.
    /// 다른 컨트롤과 달리 Styling을 내놓지 않는다 — 탭 모양과 테두리를 Win32가 그려
    /// Corners·BorderWidth·Elevated 등이 그리기에 전혀 반영되지 않기 때문이다.
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvTabControlOptions
    {
        private readonly AdvTabControl _owner;

        internal AdvTabControlOptions(AdvTabControl owner)
        {
            _owner = owner;
        }

        [DefaultValue(AdvTabStyle.Underline)]
        [Description("탭 줄의 시각 스타일입니다. Underline·Segmented·Card 중에서 고릅니다.")]
        public AdvTabStyle TabStyle
        {
            get { return _owner.TabStyle; }
            set { _owner.TabStyle = value; }
        }

        [DefaultValue(false)]
        [Description("각 탭에 닫기(×) 버튼을 표시합니다. 누르면 TabClosing→TabClosed가 발생합니다.")]
        public bool ShowTabCloseButtons
        {
            get { return _owner.ShowTabCloseButtons; }
            set { _owner.ShowTabCloseButtons = value; }
        }

        [DefaultValue(AdvThemeMode.Inherit)]
        [Description("이 탭 컨트롤이 따를 테마입니다. Inherit이면 전역 테마를 따릅니다.")]
        public AdvThemeMode ThemeMode
        {
            get { return _owner.ThemeMode; }
            set { _owner.ThemeMode = value; }
        }

        /// <summary>이 탭 컨트롤에만 적용하는 색. 비워 둔 색은 테마를 따른다.</summary>
        [Description("이 탭 컨트롤에만 적용하는 색입니다. 펼쳐서 색을 지정하면 그 색만 테마 대신 쓰입니다.")]
        public AdvColorOverrides Palette
        {
            get { return _owner.Palette; }
        }

        public override string ToString()
        {
            return string.Empty;
        }
    }

    /// <summary>탭 닫기 이벤트 인자. Cancel로 닫기를 막을 수 있다.</summary>
    public class TabCloseEventArgs : CancelEventArgs
    {
        public int TabIndex { get; private set; }
        public TabPage TabPage { get; private set; }

        public TabCloseEventArgs(int index, TabPage page)
        {
            TabIndex = index;
            TabPage = page;
        }
    }
}
