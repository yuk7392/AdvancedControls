using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>
    /// 드래그로 크기를 조절하는 두 영역짜리 분할 컨테이너. Panel1·Panel2에 각각 컨트롤을 담고,
    /// 사이의 스플리터를 끌어 비율을 바꾼다. 방향(Orientation)으로 좌우/상하를 고른다.
    /// </summary>
    [ToolboxItem(true)]
    [Description("드래그로 크기를 조절하는 분할 컨테이너입니다.")]
    [DefaultProperty("SplitterDistance")]
    public class AdvSplitContainer : AdvContainerBase
    {
        private readonly Panel _panel1 = new Panel();
        private readonly Panel _panel2 = new Panel();

        private Orientation _orientation = Orientation.Vertical;   // Vertical = 세로 스플리터(좌|우)
        private int _distance = 120;
        private int _splitterWidth = 6;
        private int _panel1Min = 25;
        private int _panel2Min = 25;

        private Rectangle _splitterRect;
        private bool _dragging;
        private int _dragOffset;
        private bool _hot;

        private AdvSplitContainerOptions _options;

        /// <summary>스플리터를 끌어 위치가 바뀌면 발생한다.</summary>
        [Category("Behavior")]
        [Description("스플리터 위치가 바뀌면 발생합니다.")]
        public event EventHandler SplitterMoved;

        public AdvSplitContainer()
        {
            Styling.Radius = 0;
            SetStyle(ControlStyles.Selectable, true);   // 키보드로 스플리터 조작 가능하게
            TabStop = true;
            _panel1.Margin = Padding.Empty;
            _panel2.Margin = Padding.Empty;
            Controls.Add(_panel1);
            Controls.Add(_panel2);
            Size = new Size(300, 200);
        }

        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvSplitContainerOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvSplitContainerOptions(this)); }
        }

        /// <summary>첫 번째 영역. 여기에 컨트롤을 담는다.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public Panel Panel1 { get { return _panel1; } }

        /// <summary>두 번째 영역. 여기에 컨트롤을 담는다.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public Panel Panel2 { get { return _panel2; } }

        [Category("Layout")]
        [DefaultValue(Orientation.Vertical)]
        [Description("Vertical=세로 스플리터로 좌우 분할, Horizontal=가로 스플리터로 상하 분할.")]
        public Orientation Orientation
        {
            get { return _orientation; }
            set { if (_orientation == value) return; _orientation = value; LayoutPanels(); Invalidate(); }
        }

        [Category("Layout")]
        [DefaultValue(120)]
        [Description("Panel1 쪽 끝에서 스플리터까지의 거리(px)입니다.")]
        public int SplitterDistance
        {
            get { return _distance; }
            set
            {
                if (_distance == value) return;
                int before = _distance;
                _distance = value;
                LayoutPanels();                 // _distance를 유효 범위로 클램프
                if (_distance != before) OnSplitterMoved();   // 클램프 후 실제로 움직였을 때만 통지
            }
        }

        [Category("Layout")]
        [DefaultValue(6)]
        [Description("스플리터 두께(px)입니다.")]
        public int SplitterWidth
        {
            get { return _splitterWidth; }
            set { value = value < 2 ? 2 : value; if (_splitterWidth == value) return; _splitterWidth = value; LayoutPanels(); Invalidate(); }
        }

        [Category("Layout")]
        [DefaultValue(25)]
        [Description("Panel1의 최소 크기(px)입니다.")]
        public int Panel1MinSize
        {
            get { return _panel1Min; }
            set { value = value < 0 ? 0 : value; if (_panel1Min == value) return; _panel1Min = value; LayoutPanels(); }
        }

        [Category("Layout")]
        [DefaultValue(25)]
        [Description("Panel2의 최소 크기(px)입니다.")]
        public int Panel2MinSize
        {
            get { return _panel2Min; }
            set { value = value < 0 ? 0 : value; if (_panel2Min == value) return; _panel2Min = value; LayoutPanels(); }
        }

        private int TotalLength { get { return _orientation == Orientation.Vertical ? Width : Height; } }

        /// <summary>최소 크기와 두께를 반영해 스플리터 거리를 유효 범위로 자른다.</summary>
        private int ClampedDistance()
        {
            int max = TotalLength - _splitterWidth - _panel2Min;   // Panel2Min을 지키는 상한
            if (max < 0) max = 0;
            int min = _panel1Min;
            // 컨테이너가 두 최소값+스플리터보다 작아 둘 다 만족 못 하면, Panel2Min을 지키고 Panel1이 양보한다.
            // (예전에는 min 클램프가 max 클램프를 덮어써 Panel2Min이 조용히 깨졌다.)
            if (min > max) min = max;
            int d = _distance;
            if (d < min) d = min;
            if (d > max) d = max;
            return d;
        }

        private void LayoutPanels()
        {
            if (Width <= 0 || Height <= 0) return;

            int d = ClampedDistance();
            _distance = d;

            if (_orientation == Orientation.Vertical)
            {
                _panel1.SetBounds(0, 0, d, Height);
                _splitterRect = new Rectangle(d, 0, _splitterWidth, Height);
                _panel2.SetBounds(d + _splitterWidth, 0, Math.Max(0, Width - d - _splitterWidth), Height);
            }
            else
            {
                _panel1.SetBounds(0, 0, Width, d);
                _splitterRect = new Rectangle(0, d, Width, _splitterWidth);
                _panel2.SetBounds(0, d + _splitterWidth, Width, Math.Max(0, Height - d - _splitterWidth));
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutPanels();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            var theme = EffectiveTheme;
            _panel1.BackColor = theme.Surface;
            _panel2.BackColor = theme.Surface;
            LayoutPanels();
        }

        protected override void OnThemeChanged()
        {
            base.OnThemeChanged();
            var theme = EffectiveTheme;
            _panel1.BackColor = theme.Surface;
            _panel2.BackColor = theme.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var theme = EffectiveTheme;
            var g = e.Graphics;

            // 스플리터 막대(자식이 안 덮는 부모 영역). 호버·드래그·포커스 중엔 더 진하게.
            Color fill = (_dragging || _hot || Focused) ? theme.SurfacePressed : theme.SurfaceHover;
            using (var b = new SolidBrush(fill)) g.FillRectangle(b, _splitterRect);

            // 가운데 그립 점 3개
            DrawGrip(g, theme);
        }

        private void DrawGrip(Graphics g, AdvTheme theme)
        {
            int cx = _splitterRect.Left + _splitterRect.Width / 2;
            int cy = _splitterRect.Top + _splitterRect.Height / 2;
            const int dot = 2, gap = 4;

            using (var b = new SolidBrush(_hot || _dragging ? theme.Text : theme.TextMuted))
            {
                for (int i = -1; i <= 1; i++)
                {
                    int x, y;
                    if (_orientation == Orientation.Vertical) { x = cx - dot / 2; y = cy + i * gap - dot / 2; }
                    else { x = cx + i * gap - dot / 2; y = cy - dot / 2; }
                    g.FillEllipse(b, x, y, dot, dot);
                }
            }
        }

        // ── 드래그 ────────────────────────────────────────────────────

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && _splitterRect.Contains(e.Location))
            {
                _dragging = true;
                Capture = true;
                _dragOffset = (_orientation == Orientation.Vertical ? e.X : e.Y) - _distance;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // 드래그는 왼쪽 버튼이 실제로 눌려 있을 때만(캡처가 풀린 뒤 잔상 이동 방지)
            if (_dragging && (e.Button & MouseButtons.Left) != 0)
            {
                int pos = (_orientation == Orientation.Vertical ? e.X : e.Y) - _dragOffset;
                int before = _distance;
                _distance = pos;
                LayoutPanels();
                Invalidate();
                if (_distance != before) OnSplitterMoved();   // 끝에 닿아 값이 안 바뀌면 통지 생략
                return;
            }

            bool hot = _splitterRect.Contains(e.Location);
            Cursor = hot ? (_orientation == Orientation.Vertical ? Cursors.VSplit : Cursors.HSplit) : Cursors.Default;
            if (hot != _hot) { _hot = hot; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_dragging)
            {
                _dragging = false;
                Capture = false;
                Invalidate();
            }
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            // 드래그 중 캡처가 강제로 풀리면(모달 대화상자·Alt+Tab·Enabled=false 등) 드래그 상태를 확실히 내린다
            if (_dragging) { _dragging = false; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Cursor = Cursors.Default;
            if (_hot) { _hot = false; Invalidate(); }
        }

        // ── 키보드 ────────────────────────────────────────────────────

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down: return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            const int step = 10;
            switch (e.KeyCode)
            {
                case Keys.Left: case Keys.Up: SplitterDistance = _distance - step; e.Handled = true; break;
                case Keys.Right: case Keys.Down: SplitterDistance = _distance + step; e.Handled = true; break;
            }
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        private void OnSplitterMoved()
        {
            var h = SplitterMoved;
            if (h != null) h(this, EventArgs.Empty);
        }
    }

    /// <summary>AdvSplitContainer가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvSplitContainerOptions : AdvOptions
    {
        private readonly AdvSplitContainer _owner;

        internal AdvSplitContainerOptions(AdvSplitContainer owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        // SplitterWidth·SplitterDistance·Panel1/2MinSize는 Layout 카테고리에 직접 노출되므로
        // (AdvDataGrid 관례) 파사드에는 모드성 속성인 Orientation만 둔다.
        [DefaultValue(Orientation.Vertical)]
        [Description("Vertical=세로 스플리터로 좌우 분할, Horizontal=가로 스플리터로 상하 분할.")]
        public Orientation Orientation
        {
            get { return _owner.Orientation; }
            set { _owner.Orientation = value; }
        }
    }
}
