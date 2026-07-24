using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>상태바의 칸 하나. 텍스트·아이콘·진행률을 담고, 스프링이면 남는 폭을 채운다.</summary>
    public class AdvStatusPanel
    {
        private string _text;
        private int _progress;

        internal AdvStatusBar Owner;
        internal Rectangle Rect;
        internal int TextW;   // LayoutPanels가 한 번 측정해 저장하는 텍스트 폭(재측정 방지)

        public Image Image { get; set; }
        public int Width { get; set; }              // -1 = 내용에 맞춤
        public bool Spring { get; set; }            // 남는 폭을 채움
        public bool ShowProgress { get; set; }      // 진행률 막대 표시
        public HorizontalAlignment Alignment { get; set; }
        public object Tag { get; set; }

        public AdvStatusPanel() { Width = -1; Alignment = HorizontalAlignment.Left; }

        public string Text
        {
            get { return _text; }
            set { if (_text == value) return; _text = value; Repaint(); }
        }

        /// <summary>진행률 0~100. <see cref="ShowProgress"/>가 켜져 있을 때 그려진다.</summary>
        public int Progress
        {
            get { return _progress; }
            set
            {
                value = value < 0 ? 0 : (value > 100 ? 100 : value);
                if (_progress == value) return;
                _progress = value;
                Repaint();
            }
        }

        private void Repaint() { if (Owner != null) Owner.Invalidate(); }
    }

    /// <summary>상태바 칸 클릭 이벤트 인자.</summary>
    public class AdvStatusPanelEventArgs : EventArgs
    {
        public AdvStatusPanel Panel { get; private set; }
        public MouseButtons Button { get; private set; }
        public AdvStatusPanelEventArgs(AdvStatusPanel panel, MouseButtons button) { Panel = panel; Button = button; }
    }

    /// <summary>
    /// 테마를 따르는 커스텀 상태바. 왼쪽 주 상태(스프링)와 오른쪽 칸들(텍스트·아이콘·진행률)을
    /// 가로로 배치하고 칸 사이 구분선을 긋는다. 보통 폼 하단에 Dock한다.
    /// </summary>
    [ToolboxItem(true)]
    [Description("테마를 따르는 커스텀 상태바입니다.")]
    public class AdvStatusBar : AdvControlBase
    {
        private const int PadX = 10;
        private const int IconSize = 14;
        private const int Gap = 6;
        private const int ProgW = 110;
        private const int ProgH = 8;

        private readonly List<AdvStatusPanel> _panels = new List<AdvStatusPanel>();
        private AdvStatusBarOptions _options;

        /// <summary>칸을 클릭하면 발생한다. 어느 칸인지는 인자의 Panel로 알 수 있다.</summary>
        [Category("Action")]
        [Description("칸을 클릭하면 발생합니다.")]
        public event EventHandler<AdvStatusPanelEventArgs> PanelClick;

        public AdvStatusBar()
        {
            Styling.ShowFocusGlow = false;
            Styling.Radius = 0;
            Dock = DockStyle.Bottom;
            Height = 24;
        }

        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvStatusBarOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvStatusBarOptions(this)); }
        }

        [Browsable(false)]
        public IList<AdvStatusPanel> Panels { get { return _panels; } }

        private AdvStatusPanel Add(AdvStatusPanel p) { p.Owner = this; _panels.Add(p); Invalidate(); return p; }

        /// <summary>내용에 맞는 폭의 텍스트 칸을 추가한다.</summary>
        public AdvStatusPanel AddPanel(string text) { return Add(new AdvStatusPanel { Text = text }); }

        /// <summary>고정 폭 텍스트 칸을 추가한다.</summary>
        public AdvStatusPanel AddPanel(string text, int width) { return Add(new AdvStatusPanel { Text = text, Width = width }); }

        /// <summary>남는 폭을 채우는 주 상태 칸을 추가한다(보통 맨 왼쪽).</summary>
        public AdvStatusPanel AddSpring(string text) { return Add(new AdvStatusPanel { Text = text, Spring = true }); }

        /// <summary>진행률 막대 칸을 추가한다.</summary>
        public AdvStatusPanel AddProgress(int value)
        {
            return Add(new AdvStatusPanel { ShowProgress = true, Progress = value, Width = ProgW });
        }

        // ── 레이아웃 ──────────────────────────────────────────────────

        private int AutoWidth(AdvStatusPanel p)
        {
            if (p.ShowProgress) return ProgW;
            int iw = p.Image != null ? AdvGraphics.Scale(this, IconSize) : 0;
            int tw = p.TextW;
            return PadX * 2 + iw + (iw > 0 && tw > 0 ? Gap : 0) + tw;
        }

        private void LayoutPanels(Graphics g)
        {
            // 각 칸 텍스트 폭을 한 번만 측정해 저장(AutoWidth·DrawTextPanel이 재사용 — 예전엔 칸당 최대 3회 측정)
            foreach (var p in _panels)
                p.TextW = (p.ShowProgress || string.IsNullOrEmpty(p.Text)) ? 0 : TextRenderer.MeasureText(g, p.Text, Font).Width;

            int fixedTotal = 0, springs = 0;
            foreach (var p in _panels)
            {
                if (p.Spring) { springs++; continue; }
                fixedTotal += p.Width >= 0 ? p.Width : AutoWidth(p);
            }

            int leftover = Math.Max(0, Width - fixedTotal);
            int springW = springs > 0 ? leftover / springs : 0;
            int springSeen = 0;
            int barH = Math.Max(1, Height - 1);

            int x = 0;
            for (int i = 0; i < _panels.Count; i++)
            {
                var p = _panels[i];
                int w;
                if (p.Spring)
                    // 마지막 스프링에 나머지를 몰아줘 우측 끝까지 채운다(나눗셈 오차 제거)
                    w = springW + (++springSeen == springs ? leftover - springW * springs : 0);
                else
                    w = p.Width >= 0 ? p.Width : AutoWidth(p);
                p.Rect = new Rectangle(x, 1, w, barH);
                x += w;
            }
        }

        // ── 마우스 ────────────────────────────────────────────────────

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            var h = PanelClick;
            if (h == null) return;
            foreach (var p in _panels)
                if (p.Rect.Contains(e.Location)) { h(this, new AdvStatusPanelEventArgs(p, e.Button)); return; }
        }

        // ── 그리기 ────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;

            using (var b = new SolidBrush(theme.Surface)) g.FillRectangle(b, ClientRectangle);
            using (var pen = new Pen(theme.Border)) g.DrawLine(pen, 0, 0, Width, 0);   // 위 구분선

            LayoutPanels(g);

            using (var divider = new Pen(theme.Border))
            {
                for (int i = 0; i < _panels.Count; i++)
                {
                    var p = _panels[i];

                    // 칸 사이 구분선(첫 칸 앞에는 안 그림)
                    if (i > 0)
                        g.DrawLine(divider, p.Rect.Left, p.Rect.Top + 3, p.Rect.Left, p.Rect.Bottom - 3);

                    if (p.ShowProgress) DrawProgress(g, theme, p);
                    else DrawTextPanel(g, theme, p);
                }
            }
        }

        private void DrawTextPanel(Graphics g, AdvTheme theme, AdvStatusPanel p)
        {
            int icon = AdvGraphics.Scale(this, IconSize);
            int iw = p.Image != null ? icon : 0;
            int tw = p.TextW;
            int mid = iw + (iw > 0 && tw > 0 ? Gap : 0) + tw;

            int startX;
            switch (p.Alignment)
            {
                case HorizontalAlignment.Center: startX = p.Rect.Left + (p.Rect.Width - mid) / 2; break;
                case HorizontalAlignment.Right: startX = p.Rect.Right - PadX - mid; break;
                default: startX = p.Rect.Left + PadX; break;
            }
            // 어떤 정렬이든 시작점을 칸 안(왼쪽 여백 확보)으로 클램프해 이웃 칸 침범을 막는다
            if (startX < p.Rect.Left + PadX) startX = p.Rect.Left + PadX;

            if (p.Image != null)
            {
                var ir = new Rectangle(startX, p.Rect.Top + (p.Rect.Height - icon) / 2, icon, icon);
                g.DrawImage(p.Image, ir);
                startX += icon + (tw > 0 ? Gap : 0);
            }

            if (tw > 0)
            {
                int maxW = p.Rect.Right - PadX - startX;   // 오른쪽 여백도 남겨 이웃 칸 침범 방지, 넘치면 말줄임
                if (maxW > 0)
                {
                    var tr = new Rectangle(startX, p.Rect.Top, maxW, p.Rect.Height);
                    TextRenderer.DrawText(g, p.Text, Font, tr, theme.TextMuted,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
            }
        }

        private void DrawProgress(Graphics g, AdvTheme theme, AdvStatusPanel p)
        {
            var track = new Rectangle(p.Rect.Left + PadX, p.Rect.Top + (p.Rect.Height - ProgH) / 2,
                                      Math.Max(0, p.Rect.Width - PadX * 2), ProgH);
            if (track.Width <= 0) return;

            using (var tb = new SolidBrush(theme.SurfaceHover))
            using (var path = AdvGraphics.CreateRoundedRect(track, new AdvCorners(ProgH / 2)))
                g.FillPath(tb, path);

            int fw = (int)((long)track.Width * p.Progress / 100);
            if (fw > 0)
            {
                var fill = new Rectangle(track.Left, track.Top, fw, track.Height);
                using (var fb = new SolidBrush(theme.Accent))
                using (var path = AdvGraphics.CreateRoundedRect(fill, new AdvCorners(ProgH / 2)))
                    g.FillPath(fb, path);
            }
        }

        // ── 접근성(스크린리더/UI Automation) ─────────────────────────

        /// <summary>접근성 Bounds용으로 칸 사각형을 최신화한다(아직 안 그려졌으면 측정만 수행).</summary>
        private void EnsurePanelLayout()
        {
            if (IsHandleCreated)
            {
                using (var g = CreateGraphics()) LayoutPanels(g);
            }
            else
            {
                using (var bmp = new Bitmap(1, 1))
                using (var g = Graphics.FromImage(bmp)) LayoutPanels(g);
            }
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new StatusBarAccessibleObject(this);
        }

        private sealed class StatusBarAccessibleObject : ControlAccessibleObject
        {
            private readonly AdvStatusBar _owner;
            public StatusBarAccessibleObject(AdvStatusBar owner) : base(owner) { _owner = owner; }

            public override AccessibleRole Role { get { return AccessibleRole.StatusBar; } }
            public override int GetChildCount() { return _owner._panels.Count; }
            public override AccessibleObject GetChild(int index)
            {
                return index >= 0 && index < _owner._panels.Count
                    ? new PanelAccessibleObject(_owner, index) : null;
            }

            private sealed class PanelAccessibleObject : AccessibleObject
            {
                private readonly AdvStatusBar _o;
                private readonly int _i;
                public PanelAccessibleObject(AdvStatusBar o, int i) { _o = o; _i = i; }

                private AdvStatusPanel P { get { return _o._panels[_i]; } }

                public override AccessibleObject Parent { get { return _o.AccessibilityObject; } }
                public override AccessibleRole Role
                {
                    get { return P.ShowProgress ? AccessibleRole.ProgressBar : AccessibleRole.StaticText; }
                }

                public override string Name { get { return P.Text; } }

                // 진행률 칸은 값을 백분율로 읽어 준다
                public override string Value
                {
                    get { return P.ShowProgress ? P.Progress + "%" : null; }
                }

                public override AccessibleStates State { get { return AccessibleStates.ReadOnly; } }

                public override Rectangle Bounds
                {
                    get { _o.EnsurePanelLayout(); return _o.RectangleToScreen(P.Rect); }
                }
            }
        }
    }

    /// <summary>AdvStatusBar가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvStatusBarOptions : AdvOptions
    {
        internal AdvStatusBarOptions(AdvStatusBar owner) : base(owner.Styling, owner.Palette) { }
    }
}
