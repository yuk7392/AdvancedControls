using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>스플리터의 접기 버튼이 접는 대상. None이면 버튼이 없다(속성으로만 접기).</summary>
    public enum AdvSplitCollapseButton
    {
        None,
        Panel1,
        Panel2
    }

    /// <summary>
    /// 드래그로 크기를 조절하는 두 영역짜리 분할 컨테이너. Panel1·Panel2에 각각 컨트롤을 담고,
    /// 사이의 스플리터를 끌어 비율을 바꾼다. 방향(Orientation)으로 좌우/상하를 고른다.
    /// Panel1Collapsed/Panel2Collapsed로 한쪽을 접을 수 있고, CollapseButton을 켜면
    /// 스플리터 가운데 셰브런 클릭 한 번으로 접고 편다(접힌 동안엔 스플리터 전체가 복원 바).
    /// </summary>
    [ToolboxItem(true)]
    [Description("드래그로 크기를 조절하는 분할 컨테이너입니다.")]
    [DefaultProperty("SplitterDistance")]
    [Designer(typeof(Design.AdvSplitContainerDesigner))]
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

        private bool _panel1Collapsed;
        private bool _panel2Collapsed;
        private AdvSplitCollapseButton _collapseButton = AdvSplitCollapseButton.None;
        private bool _hotBtn;   // 접기 버튼 위 호버(스플리터 호버와 별개로 강조)

        private const int CollapseBtnLen = 28;   // 접기 버튼의 스플리터 축 방향 길이(논리 px)

        private AdvSplitContainerOptions _options;

        /// <summary>스플리터를 끌어 위치가 바뀌면 발생한다.</summary>
        [Category("Behavior")]
        [Description("스플리터 위치가 바뀌면 발생합니다.")]
        public event EventHandler SplitterMoved;

        /// <summary>패널이 접히거나 펴지면 발생한다.</summary>
        [Category("Behavior")]
        [Description("패널이 접히거나 펴지면 발생합니다.")]
        public event EventHandler CollapsedChanged;

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
                Invalidate();                   // 막대·그립을 다시 그린다(드래그 외 경로·디자이너에서도 리페인트되게)
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

        /// <summary>Panel1을 접는다. 접힌 동안 SplitterDistance는 보존되고 펴면 그 자리로 돌아온다.
        /// 둘 다 접을 수는 없다 — 한쪽을 접으면 다른 쪽은 자동으로 펴진다.</summary>
        [Browsable(false)]
        [DefaultValue(false)]
        public bool Panel1Collapsed
        {
            get { return _panel1Collapsed; }
            set
            {
                if (_panel1Collapsed == value) return;
                _panel1Collapsed = value;
                if (value) _panel2Collapsed = false;
                LayoutPanels();
                Invalidate();
                OnCollapsedChanged();
            }
        }

        /// <summary>Panel2를 접는다. 나머지는 <see cref="Panel1Collapsed"/>와 같다.</summary>
        [Browsable(false)]
        [DefaultValue(false)]
        public bool Panel2Collapsed
        {
            get { return _panel2Collapsed; }
            set
            {
                if (_panel2Collapsed == value) return;
                _panel2Collapsed = value;
                if (value) _panel1Collapsed = false;
                LayoutPanels();
                Invalidate();
                OnCollapsedChanged();
            }
        }

        /// <summary>스플리터 가운데에 접기 셰브런 버튼을 둘지, 어느 패널을 접을지. None이면 버튼 없음.</summary>
        [Browsable(false)]
        [DefaultValue(AdvSplitCollapseButton.None)]
        public AdvSplitCollapseButton CollapseButton
        {
            get { return _collapseButton; }
            set
            {
                if (_collapseButton == value) return;
                _collapseButton = value;
                LayoutPanels();   // None↔버튼 전환은 접힘 중 스플리터 표시 여부를 바꾼다
                Invalidate();
            }
        }

        private bool AnyCollapsed { get { return _panel1Collapsed || _panel2Collapsed; } }

        private void OnCollapsedChanged()
        {
            var h = CollapsedChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        /// <summary>버튼 클릭·키보드 토글: 접혀 있으면 펴고, 아니면 CollapseButton 대상 패널을 접는다.</summary>
        private void ToggleCollapse()
        {
            if (_panel1Collapsed) Panel1Collapsed = false;
            else if (_panel2Collapsed) Panel2Collapsed = false;
            else if (_collapseButton == AdvSplitCollapseButton.Panel1) Panel1Collapsed = true;
            else if (_collapseButton == AdvSplitCollapseButton.Panel2) Panel2Collapsed = true;
        }

        /// <summary>펼친 상태에서 접기 버튼이 차지하는 영역(스플리터 가운데). 접힌 동안엔 스플리터 전체가 버튼이다.</summary>
        private Rectangle CollapseButtonRect
        {
            get
            {
                if (_collapseButton == AdvSplitCollapseButton.None || _splitterRect.IsEmpty) return Rectangle.Empty;
                if (AnyCollapsed) return _splitterRect;

                int len = AdvGraphics.Scale(this, CollapseBtnLen);
                if (_orientation == Orientation.Vertical)
                {
                    int y = _splitterRect.Top + (_splitterRect.Height - len) / 2;
                    return new Rectangle(_splitterRect.Left, y, _splitterRect.Width, len);
                }
                int x = _splitterRect.Left + (_splitterRect.Width - len) / 2;
                return new Rectangle(x, _splitterRect.Top, len, _splitterRect.Height);
            }
        }

        /// <summary>셰브런 방향: 펼침 상태에선 접을 패널 쪽, 접힘 상태에선 복원(반대) 쪽을 가리킨다.</summary>
        private AdvGraphics.ChevronDirection ChevronDir
        {
            get
            {
                bool towardPanel1 = AnyCollapsed ? _panel2Collapsed : _collapseButton == AdvSplitCollapseButton.Panel1;
                if (_orientation == Orientation.Vertical)
                    return towardPanel1 ? AdvGraphics.ChevronDirection.Left : AdvGraphics.ChevronDirection.Right;
                return towardPanel1 ? AdvGraphics.ChevronDirection.Up : AdvGraphics.ChevronDirection.Down;
            }
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

            _panel1.Visible = !_panel1Collapsed;
            _panel2.Visible = !_panel2Collapsed;

            if (AnyCollapsed)
            {
                // 접힘: _distance는 건드리지 않아 펴면 그 자리로 돌아온다.
                // 접기 버튼 모드면 접힌 쪽 끝에 스플리터를 복원 바로 남기고, 아니면(표준 관례) 스플리터도 숨긴다.
                int sw = _collapseButton != AdvSplitCollapseButton.None ? _splitterWidth : 0;
                if (_orientation == Orientation.Vertical)
                {
                    if (_panel1Collapsed)
                    {
                        _splitterRect = sw > 0 ? new Rectangle(0, 0, sw, Height) : Rectangle.Empty;
                        _panel2.SetBounds(sw, 0, Math.Max(0, Width - sw), Height);
                    }
                    else
                    {
                        _splitterRect = sw > 0 ? new Rectangle(Width - sw, 0, sw, Height) : Rectangle.Empty;
                        _panel1.SetBounds(0, 0, Math.Max(0, Width - sw), Height);
                    }
                }
                else
                {
                    if (_panel1Collapsed)
                    {
                        _splitterRect = sw > 0 ? new Rectangle(0, 0, Width, sw) : Rectangle.Empty;
                        _panel2.SetBounds(0, sw, Width, Math.Max(0, Height - sw));
                    }
                    else
                    {
                        _splitterRect = sw > 0 ? new Rectangle(0, Height - sw, Width, sw) : Rectangle.Empty;
                        _panel1.SetBounds(0, 0, Width, Math.Max(0, Height - sw));
                    }
                }
                return;
            }

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

            if (_splitterRect.IsEmpty) return;   // 표준 관례 접힘(버튼 없음): 스플리터도 안 그린다

            // 스플리터 막대(자식이 안 덮는 부모 영역). 호버·드래그·포커스 중엔 더 진하게.
            Color fill = (_dragging || _hot || Focused) ? theme.SurfacePressed : theme.SurfaceHover;
            using (var b = new SolidBrush(fill)) g.FillRectangle(b, _splitterRect);

            if (_collapseButton != AdvSplitCollapseButton.None)
            {
                // 접기 버튼: 호버 시 강조 배경 + 접기/복원 방향 셰브런(가운데 그립 점 대신)
                var btn = CollapseButtonRect;
                if (_hotBtn)
                    using (var b = new SolidBrush(theme.SurfacePressed))
                        g.FillRectangle(b, btn);
                AdvGraphics.DrawChevron(g, this, btn, ChevronDir,
                                        _hotBtn ? theme.Text : theme.TextMuted, 7, 4, 1.4f, 0);
                return;
            }

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
            if (e.Button != MouseButtons.Left || !_splitterRect.Contains(e.Location)) return;

            // 접힘 중엔 스플리터 전체가 복원 바, 펼침 중엔 접기 버튼 클릭이 접기(둘 다 드래그 안 함).
            // 접힘 중 스플리터가 보인다는 것 자체가 버튼 모드라는 뜻이다(아니면 스플리터가 숨어 여기 못 온다).
            if (AnyCollapsed || CollapseButtonRect.Contains(e.Location))
            {
                ToggleCollapse();
                return;
            }

            _dragging = true;
            Capture = true;
            _dragOffset = (_orientation == Orientation.Vertical ? e.X : e.Y) - _distance;
            Invalidate();
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
            bool hotBtn = hot && !CollapseButtonRect.IsEmpty && CollapseButtonRect.Contains(e.Location);

            // 접기 버튼·복원 바 위에선 손 모양(클릭), 나머지 스플리터는 리사이즈 커서
            Cursor = hotBtn ? Cursors.Hand
                   : hot && !AnyCollapsed ? (_orientation == Orientation.Vertical ? Cursors.VSplit : Cursors.HSplit)
                   : Cursors.Default;

            if (hot != _hot || hotBtn != _hotBtn) { _hot = hot; _hotBtn = hotBtn; Invalidate(); }
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
            if (_hot || _hotBtn) { _hot = false; _hotBtn = false; Invalidate(); }
        }

        // ── 키보드 ────────────────────────────────────────────────────

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down:
                case Keys.Return: case Keys.Space:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            const int step = 10;
            switch (e.KeyCode)
            {
                case Keys.Left: case Keys.Up:
                    if (!AnyCollapsed) SplitterDistance = _distance - step;   // 접힘 중 이동은 보이지 않으므로 무시
                    e.Handled = true; break;
                case Keys.Right: case Keys.Down:
                    if (!AnyCollapsed) SplitterDistance = _distance + step;
                    e.Handled = true; break;
                case Keys.Return: case Keys.Space:
                    // 접기 버튼이 있거나 이미 접혀 있으면 토글(복원은 버튼 없이도 가능해야 함)
                    if (_collapseButton != AdvSplitCollapseButton.None || AnyCollapsed) ToggleCollapse();
                    e.Handled = true; break;
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
        // (AdvDataGrid 관례) 파사드에는 모드성·상태 속성만 둔다.
        [DefaultValue(Orientation.Vertical)]
        [Description("Vertical=세로 스플리터로 좌우 분할, Horizontal=가로 스플리터로 상하 분할.")]
        public Orientation Orientation
        {
            get { return _owner.Orientation; }
            set { _owner.Orientation = value; }
        }

        [DefaultValue(false)]
        [Description("Panel1을 접습니다. 접힌 동안 스플리터 거리는 보존되고, 펴면 그 자리로 돌아옵니다.")]
        public bool Panel1Collapsed
        {
            get { return _owner.Panel1Collapsed; }
            set { _owner.Panel1Collapsed = value; }
        }

        [DefaultValue(false)]
        [Description("Panel2를 접습니다. 접힌 동안 스플리터 거리는 보존되고, 펴면 그 자리로 돌아옵니다.")]
        public bool Panel2Collapsed
        {
            get { return _owner.Panel2Collapsed; }
            set { _owner.Panel2Collapsed = value; }
        }

        [DefaultValue(AdvSplitCollapseButton.None)]
        [Description("스플리터 가운데 접기 셰브런 버튼을 표시하고 어느 패널을 접을지 정합니다. None이면 버튼이 없습니다.")]
        public AdvSplitCollapseButton CollapseButton
        {
            get { return _owner.CollapseButton; }
            set { _owner.CollapseButton = value; }
        }
    }
}
