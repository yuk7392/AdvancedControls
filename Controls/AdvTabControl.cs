using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
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
        [Category("Appearance")]
        [DefaultValue(AdvThemeMode.Inherit)]
        [Description("이 탭 컨트롤이 따를 테마입니다. Inherit이면 전역 테마를 따릅니다.")]
        public AdvThemeMode ThemeMode
        {
            get { return _appearance.ThemeMode; }
            set { _appearance.ThemeMode = value; }
        }

        protected AdvTheme EffectiveTheme
        {
            get { return _theme ?? _appearance.ResolveTheme() ?? AdvThemeManager.Current; }
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

        /// <summary>Win32가 시스템 색으로 칠해 둔 자리(탭 줄 여백, 페이지 둘레)를 덮는다.</summary>
        private void PaintChrome(Graphics g)
        {
            var theme = EffectiveTheme;
            var client = ClientRectangle;
            var last = GetTabRect(TabCount - 1);

            // 1) 마지막 탭 오른쪽 빈 자리. 탭 위쪽에도 여백이 있으므로 클라이언트 위끝부터 채운다.
            //    탭은 TabEdge만큼 넓혀 그리므로 그 끝부터 시작해야 마지막 탭 오른쪽이 잘리지 않는다
            int fillLeft = last.Right + TabEdge;
            if (client.Right > fillLeft)
            {
                using (var brush = new SolidBrush(theme.SurfaceHover))
                    g.FillRectangle(brush, Rectangle.FromLTRB(fillLeft, client.Top,
                                                              client.Right, last.Bottom));
            }

            // 2) 각 탭의 가장자리. OnDrawItem은 탭 사각형 안으로 클리핑되어
            //    Win32가 그린 바깥 1~2px(다크 테마에서 #FFFFFF/#E3E3E3 흰선)을 덮지 못한다.
            //    여기는 클리핑이 없으므로 탭마다 테두리 띠를 자기 채움색으로 덧칠한다
            for (int i = 0; i < TabCount; i++)
            {
                var tab = GetTabRect(i);
                var outer = Rectangle.Inflate(tab, TabEdge, TabEdge);

                // 스트립 위쪽으로는 넘어가지 않게 자른다
                if (outer.Top < client.Top) outer = Rectangle.FromLTRB(
                    outer.Left, client.Top, outer.Right, outer.Bottom);

                using (var brush = new SolidBrush(i == SelectedIndex ? theme.Surface : theme.SurfaceHover))
                {
                    // 위·좌·우 세 변만 덮는다. 아래는 선택 강조선과 페이지가 맞물리는 자리다
                    g.FillRectangle(brush, Rectangle.FromLTRB(
                        outer.Left, outer.Top, outer.Right, tab.Top));
                    g.FillRectangle(brush, Rectangle.FromLTRB(
                        outer.Left, tab.Top, tab.Left, tab.Bottom));
                    g.FillRectangle(brush, Rectangle.FromLTRB(
                        tab.Right, tab.Top, outer.Right, tab.Bottom));
                }

                if (i == SelectedIndex)
                {
                    // 강조선도 넓힌 폭에 맞춰야 오른쪽 끝 2px이 비지 않는다
                    using (var brush = new SolidBrush(theme.Accent))
                        g.FillRectangle(brush, new Rectangle(
                            outer.Left, tab.Bottom - 3, outer.Width, 3));
                }
            }

            // 3) 페이지 둘레의 테두리. 페이지 자체는 TabPage가 칠하므로 바깥 띠만 덮는다
            var page = DisplayRectangle;
            using (var brush = new SolidBrush(theme.Border))
            {
                int stripBottom = last.Bottom;

                if (page.Left > client.Left)
                    g.FillRectangle(brush, Rectangle.FromLTRB(client.Left, stripBottom, page.Left, client.Bottom));

                if (client.Right > page.Right)
                    g.FillRectangle(brush, Rectangle.FromLTRB(page.Right, stripBottom, client.Right, client.Bottom));

                if (client.Bottom > page.Bottom)
                    g.FillRectangle(brush, Rectangle.FromLTRB(client.Left, page.Bottom, client.Right, client.Bottom));

                if (page.Top > stripBottom)
                    g.FillRectangle(brush, Rectangle.FromLTRB(client.Left, stripBottom, client.Right, page.Top));
            }
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;

            if (e.Index < 0 || e.Index >= TabPages.Count) return;

            var page = TabPages[e.Index];
            bool selected = e.Index == SelectedIndex;
            bool hot = (e.State & DrawItemState.HotLight) != 0;

            var r = e.Bounds;
            r.Inflate(TabEdge, TabEdge);

            Color fill = selected ? theme.Surface
                       : hot ? theme.SurfacePressed
                       : theme.SurfaceHover;

            using (var brush = new SolidBrush(fill))
                g.FillRectangle(brush, r);

            if (selected)
            {
                // 선택된 탭 아래쪽에 강조선을 긋는다
                using (var brush = new SolidBrush(theme.Accent))
                    g.FillRectangle(brush, new Rectangle(r.Left, r.Bottom - 3, r.Width, 3));
            }

            TextRenderer.DrawText(g, page.Text, Font, e.Bounds,
                selected ? theme.Text : theme.TextMuted,
                TextFormatFlags.HorizontalCenter
              | TextFormatFlags.VerticalCenter
              | TextFormatFlags.EndEllipsis
              | TextFormatFlags.NoPrefix);

            base.OnDrawItem(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 정적 이벤트라 해제하지 않으면 폼이 닫혀도 컨트롤이 수거되지 않는다
                AdvThemeManager.ThemeChanged -= OnGlobalThemeChanged;
                _appearance.Changed -= OnAppearanceChanged;
            }
            base.Dispose(disposing);
        }
    }
}
