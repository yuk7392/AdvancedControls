using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>툴바의 항목 하나. 버튼(텍스트·아이콘·토글) 또는 구분선.</summary>
    public class AdvToolBarItem
    {
        public string Text { get; set; }
        public Image Image { get; set; }
        public bool Enabled { get; set; }
        public bool IsToggle { get; set; }
        public bool Checked { get; set; }
        public string ToolTipText { get; set; }
        public object Tag { get; set; }
        public bool IsSeparator { get; internal set; }

        internal Rectangle Rect;
        internal int TextW;        // LayoutItems가 한 번 측정해 저장(그리기 루프가 재사용)
        internal bool Overflowed;  // 폭 부족으로 셰브런(») 메뉴로 접힌 항목

        /// <summary>버튼을 누르면 발생한다. 토글이면 Checked가 먼저 뒤집힌다.</summary>
        public event EventHandler Click;

        public AdvToolBarItem() { Enabled = true; }
        public AdvToolBarItem(string text) { Text = text; Enabled = true; }

        internal void PerformClick()
        {
            var h = Click;
            if (h != null) h(this, EventArgs.Empty);
        }

        public static AdvToolBarItem Separator() { return new AdvToolBarItem { IsSeparator = true, Enabled = false }; }
    }

    /// <summary>툴바 항목 이벤트 인자.</summary>
    public class AdvToolBarItemEventArgs : EventArgs
    {
        public AdvToolBarItem Item { get; private set; }
        public AdvToolBarItemEventArgs(AdvToolBarItem item) { Item = item; }
    }

    /// <summary>
    /// 테마를 따르는 커스텀 툴바. 텍스트·아이콘·토글 버튼과 구분선을 가로로 늘어놓고
    /// 호버·눌림·체크 상태를 직접 그린다.
    /// </summary>
    [ToolboxItem(true)]
    [Description("테마를 따르는 커스텀 툴바입니다.")]
    [DefaultEvent("ItemClicked")]
    public class AdvToolBar : AdvControlBase
    {
        private const int IconSize = 16;
        private const int PadX = 10;
        private const int Gap = 6;
        private const int SepWidth = 9;
        private const int OverflowW = 22;   // 오버플로 셰브런(») 버튼 폭(논리 px)

        private readonly List<AdvToolBarItem> _items = new List<AdvToolBarItem>();
        private int _hover = -1;
        private int _pressed = -1;
        private int _focused = -1;   // 키보드 포커스 항목
        private bool _layoutDirty = true;   // 항목·글꼴이 바뀔 때만 레이아웃 재계산
        private int _lastItemCount = -1;    // Items를 외부에서 직접 변형(RemoveAt/Clear)한 경우 감지용
        private ToolTip _tip;               // 아이콘 버튼 등의 툴팁(지연 생성)

        // 오버플로: 폭이 모자라면 뒤쪽 항목을 셰브런(») 드롭다운 메뉴로 접는다
        private Rectangle _chevRect = Rectangle.Empty;   // 셰브런 버튼 자리(비면 오버플로 없음)
        private bool _chevHover;
        private AdvContextMenu _overflow;                // 열려 있는 오버플로 메뉴
        private bool _overflowOpen;
        private int _justClosedAt;                       // 셰브런 재클릭 토글 처리용(메뉴바와 동일 패턴)

        /// <summary>좌·우 끝 항목이 둥근 모서리에 걸리지 않도록 두는 여백. 반경에 맞춰 커진다.</summary>
        private int SideInset
        {
            get { int r = EffectiveCorners.TopLeft; return r > 0 ? Math.Max(6, r) : 4; }
        }

        private AdvToolBarOptions _options;

        /// <summary>아무 버튼이나 눌리면 발생한다.</summary>
        [Category("Action")]
        [Description("툴바의 버튼이 눌리면 발생합니다.")]
        public event EventHandler<AdvToolBarItemEventArgs> ItemClicked;

        public AdvToolBar()
        {
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
            Styling.ShowFocusGlow = false;
            Styling.Radius = 0;
            Dock = DockStyle.Top;
            Height = 38;
        }

        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvToolBarOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvToolBarOptions(this)); }
        }

        [Browsable(false)]
        public IList<AdvToolBarItem> Items { get { return _items; } }

        private AdvToolBarItem Add(AdvToolBarItem it)
        {
            _items.Add(it); _layoutDirty = true; Invalidate(); return it;
        }

        /// <summary>텍스트 버튼을 추가한다.</summary>
        public AdvToolBarItem AddButton(string text) { return Add(new AdvToolBarItem(text)); }

        /// <summary>아이콘(+텍스트) 버튼을 추가한다.</summary>
        public AdvToolBarItem AddButton(string text, Image image) { return Add(new AdvToolBarItem(text) { Image = image }); }

        /// <summary>토글 버튼을 추가한다.</summary>
        public AdvToolBarItem AddToggle(string text) { return Add(new AdvToolBarItem(text) { IsToggle = true }); }

        public void AddSeparator() { Add(AdvToolBarItem.Separator()); }

        // ── 레이아웃 ──────────────────────────────────────────────────

        private void LayoutItems(Graphics g)
        {
            // 1차: 각 항목의 자연 폭 측정(폭은 Rect.Width에 임시 저장)
            int total = 0;
            foreach (var it in _items)
            {
                int w;
                if (it.IsSeparator) { it.TextW = 0; w = SepWidth; }
                else
                {
                    int tw = string.IsNullOrEmpty(it.Text) ? 0 : TextRenderer.MeasureText(g, it.Text, Font).Width;
                    it.TextW = tw;   // 그리기 루프가 재사용(매 페인트 재측정 방지)
                    int iw = it.Image != null ? AdvGraphics.Scale(this, IconSize) : 0;
                    int mid = iw + (iw > 0 && tw > 0 ? Gap : 0) + tw;
                    w = PadX * 2 + mid;
                }
                it.Rect = new Rectangle(0, 0, w, 0);
                total += w;
            }

            // 2차: 배치. 다 안 들어가면 셰브런 자리를 빼고, 처음 넘치는 항목부터 순서대로 접는다.
            int right = Width - SideInset;
            bool overflow = SideInset + total > right;
            int chevW = AdvGraphics.Scale(this, OverflowW);
            int limit = overflow ? right - chevW : right;

            int x = SideInset;
            bool spill = false;
            foreach (var it in _items)
            {
                int w = it.Rect.Width;
                if (spill || (overflow && x + w > limit))
                {
                    spill = true;
                    it.Overflowed = true;
                    it.Rect = Rectangle.Empty;
                    continue;
                }
                it.Overflowed = false;
                it.Rect = new Rectangle(x, 3, w, Height - 6);
                x += w;
            }

            _chevRect = spill ? new Rectangle(right - chevW, 3, chevW, Height - 6) : Rectangle.Empty;
        }

        // ── 그리기 ────────────────────────────────────────────────────

        /// <summary>
        /// 모서리 반경(Styling.Radius)이 0이면 풀폭 도킹 사각 바(아래 구분선만),
        /// 0보다 크면 둥근 '플로팅 필'(전체 둥근 테두리)로 그린다. 둥글면 아래 Region으로 코너를 자른다.
        /// </summary>
        private Rectangle _regionClip = Rectangle.Empty;   // 마지막으로 Region을 만든 클립(리사이즈 중 재생성 방지)

        private void ApplyRoundedRegion()
        {
            if (!IsHandleCreated) return;
            var clip = Rectangle.Inflate(FrameBounds, 1, 1);
            // 반경 0(사각 도킹 바)이면 클립하지 않는다 — 아래 구분선만 그린다.
            AdvGraphics.UpdateRoundedRegion(this, clip, EffectiveCorners, EffectiveCorners.IsZero, ref _regionClip);
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyRoundedRegion(); }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRoundedRegion();
            _layoutDirty = true;   // 폭에 따라 오버플로 컷오프가 달라진다
            Invalidate();
        }
        protected override void OnThemeChanged() { base.OnThemeChanged(); _regionClip = Rectangle.Empty; ApplyRoundedRegion(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;

            var corners = EffectiveCorners;
            if (corners.IsZero)
            {
                // 사각 도킹 바: 면색 + 아래 구분선
                using (var b = new SolidBrush(theme.Surface)) g.FillRectangle(b, ClientRectangle);
                using (var pen = new Pen(theme.Border)) g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }
            else
            {
                // 둥근 플로팅 필: 전체 둥근 테두리
                AdvFrameRenderer.Draw(g, FrameBounds, theme, corners, EffectiveBorderWidth,
                                      theme.Surface, Color.Empty, theme.Border, null, CurrentElevation, EffectiveBorderDash);
            }

            // Items를 외부에서 직접 추가/제거하면(내부 Add 외 경로) 개수 변화로 감지해
            // 레이아웃을 다시 잡고, 범위를 벗어난 호버·눌림·포커스 인덱스를 정리한다.
            if (_lastItemCount != _items.Count)
            {
                _lastItemCount = _items.Count;
                _layoutDirty = true;
                if (_hover >= _items.Count) _hover = -1;
                if (_pressed >= _items.Count) _pressed = -1;
                if (_focused >= _items.Count) _focused = -1;
            }

            if (_layoutDirty) { LayoutItems(g); _layoutDirty = false; }

            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                if (it.Overflowed) continue;   // 셰브런 메뉴로 접힌 항목
                if (it.IsSeparator)
                {
                    int cx = it.Rect.Left + SepWidth / 2;
                    using (var pen = new Pen(theme.Border))
                        g.DrawLine(pen, cx, it.Rect.Top + 4, cx, it.Rect.Bottom - 4);
                    continue;
                }

                bool pressed = i == _pressed && i == _hover && it.Enabled;
                bool hot = i == _hover && it.Enabled && !pressed;
                bool active = it.IsToggle && it.Checked;

                Color? bg = null;
                if (pressed) bg = theme.SurfacePressed;
                else if (active) bg = theme.Accent;
                else if (hot) bg = theme.SurfaceHover;

                if (bg != null)
                    using (var br = new SolidBrush(bg.Value))
                    using (var path = AdvGraphics.CreateRoundedRect(it.Rect, new AdvCorners(6)))
                        g.FillPath(br, path);

                // 키보드 포커스 표시(포커스가 이 컨트롤에 있을 때만)
                if (Focused && i == _focused && it.Enabled)
                    using (var pen = new Pen(active ? theme.OnAccent : theme.Accent))
                    using (var path = AdvGraphics.CreateRoundedRect(Rectangle.Inflate(it.Rect, -1, -1), new AdvCorners(5)))
                        g.DrawPath(pen, path);

                Color fg = !it.Enabled ? theme.TextDisabled : active ? theme.OnAccent : theme.Text;

                int icon = AdvGraphics.Scale(this, IconSize);
                int iw = it.Image != null ? icon : 0;
                int tw = it.TextW;   // LayoutItems에서 이미 측정
                int mid = iw + (iw > 0 && tw > 0 ? Gap : 0) + tw;
                int startX = it.Rect.Left + (it.Rect.Width - mid) / 2;

                if (it.Image != null)
                {
                    var ir = new Rectangle(startX, it.Rect.Top + (it.Rect.Height - icon) / 2, icon, icon);
                    if (it.Enabled) g.DrawImage(it.Image, ir);
                    else AdvGraphics.DrawImageDisabled(g, it.Image, ir);
                    startX += icon + (tw > 0 ? Gap : 0);
                }

                if (tw > 0)
                {
                    var tr = new Rectangle(startX, it.Rect.Top, tw + 4, it.Rect.Height);
                    TextRenderer.DrawText(g, it.Text, Font, tr, fg,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            }

            // 오버플로 셰브런(») 버튼
            if (!_chevRect.IsEmpty)
            {
                Color? cbg = _overflowOpen ? theme.SurfacePressed
                           : _chevHover ? (Color?)theme.SurfaceHover : null;
                if (cbg != null)
                    using (var br = new SolidBrush(cbg.Value))
                    using (var path = AdvGraphics.CreateRoundedRect(_chevRect, new AdvCorners(6)))
                        g.FillPath(br, path);

                if (Focused && _focused == _items.Count)
                    using (var pen = new Pen(theme.Accent))
                    using (var path = AdvGraphics.CreateRoundedRect(Rectangle.Inflate(_chevRect, -1, -1), new AdvCorners(5)))
                        g.DrawPath(pen, path);

                AdvGraphics.DrawChevron(g, this, _chevRect, AdvGraphics.ChevronDirection.Right, theme.Text, 7, 4, 1.4f, -3);
                AdvGraphics.DrawChevron(g, this, _chevRect, AdvGraphics.ChevronDirection.Right, theme.Text, 7, 4, 1.4f, +3);
            }
        }

        // ── 마우스 ────────────────────────────────────────────────────

        private int ItemAt(Point p)
        {
            for (int i = 0; i < _items.Count; i++)
                if (!_items[i].IsSeparator && _items[i].Rect.Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = ItemAt(e.Location);
            if (i >= 0 && !_items[i].Enabled) i = -1;
            if (i != _hover) { _hover = i; UpdateTip(i); Invalidate(); }

            bool ch = !_chevRect.IsEmpty && _chevRect.Contains(e.Location);
            if (ch != _chevHover) { _chevHover = ch; Invalidate(); }
        }

        /// <summary>호버 항목이 바뀌면 그 항목의 ToolTipText를 툴팁으로 보여준다(있을 때만).</summary>
        private void UpdateTip(int i)
        {
            string text = (i >= 0 && !string.IsNullOrEmpty(_items[i].ToolTipText)) ? _items[i].ToolTipText : null;
            if (text == null) { if (_tip != null) _tip.Hide(this); return; }
            if (_tip == null) _tip = new ToolTip();
            _tip.SetToolTip(this, text);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_tip != null) _tip.Hide(this);
            if (_hover != -1 || _pressed != -1 || _chevHover)
            { _hover = -1; _pressed = -1; _chevHover = false; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            if (!_chevRect.IsEmpty && _chevRect.Contains(e.Location))
            {
                if (_overflowOpen) { _overflow.Close(); return; }   // 열린 채 재클릭이면 닫기
                // 바깥 클릭으로 팝업이 방금 닫힌 직후의 재클릭이면 다시 열지 않는다(토글 — 메뉴바와 동일)
                if (unchecked(Environment.TickCount - _justClosedAt) < 300) return;
                OpenOverflow();
                return;
            }

            int i = ItemAt(e.Location);
            if (i >= 0 && _items[i].Enabled) { _pressed = i; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;

            int i = ItemAt(e.Location);
            if (_pressed >= 0 && _pressed == i && _items[i].Enabled)
                Activate(_items[i]);

            if (_pressed != -1) { _pressed = -1; Invalidate(); }
        }

        private void Activate(AdvToolBarItem it)
        {
            if (it.IsToggle) it.Checked = !it.Checked;
            it.PerformClick();
            var h = ItemClicked;
            if (h != null) h(this, new AdvToolBarItemEventArgs(it));
            Invalidate();
            // 토글 상태 변경을 스크린리더에 알린다. childID = 항목 인덱스.
            if (it.IsToggle)
            {
                int idx = _items.IndexOf(it);
                if (idx >= 0) AccessibilityNotifyClients(AccessibleEvents.StateChange, idx);
            }
        }

        // ── 오버플로 메뉴 ─────────────────────────────────────────────

        /// <summary>접힌 항목들로 드롭다운 메뉴를 만든다. 구분선은 앞뒤 생략·연속 접기,
        /// 아이콘 전용 버튼은 툴팁을 텍스트로 쓴다. 토글은 체크 표시로 보인다.</summary>
        internal AdvContextMenu BuildOverflowMenu()
        {
            var m = new AdvContextMenu();
            bool pendingSep = false;
            foreach (var it in _items)
            {
                if (!it.Overflowed) continue;
                if (it.IsSeparator) { pendingSep = m.Items.Count > 0; continue; }
                if (pendingSep) { m.AddSeparator(); pendingSep = false; }

                var src = it;
                var mi = m.Add(!string.IsNullOrEmpty(src.Text) ? src.Text : src.ToolTipText);
                mi.Enabled = src.Enabled;
                mi.Image = src.Image;
                mi.Checked = src.IsToggle && src.Checked;
                mi.Click += (s, e) => Activate(src);
            }
            return m;
        }

        private void OpenOverflow()
        {
            EnsureItemLayout();
            if (_chevRect.IsEmpty) return;

            if (_overflow != null) { _overflow.Dispose(); _overflow = null; }
            var menu = BuildOverflowMenu();
            if (menu.Items.Count == 0) { menu.Dispose(); return; }

            _overflow = menu;
            menu.Closed += (s, e) =>
            {
                _overflowOpen = false;
                _justClosedAt = Environment.TickCount;
                if (!IsDisposed) Invalidate();
            };
            _overflowOpen = true;
            Invalidate();
            menu.Show(this, new Point(_chevRect.Left, Height));
        }

        // ── 키보드 ────────────────────────────────────────────────────

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left: case Keys.Right: case Keys.Home: case Keys.End:
                case Keys.Return: case Keys.Space:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Left: StepFocus(-1); e.Handled = true; break;
                case Keys.Right: StepFocus(+1); e.Handled = true; break;
                case Keys.Home: _focused = -1; StepFocus(+1); e.Handled = true; break;
                case Keys.End:
                    EnsureItemLayout();
                    _focused = _items.Count + (_chevRect.IsEmpty ? 0 : 1);   // 마지막 슬롯(셰브런 포함) 바로 뒤에서 역방향
                    StepFocus(-1); e.Handled = true; break;
                case Keys.Return:
                case Keys.Space:
                    if (_focused == _items.Count && !_chevRect.IsEmpty)
                        OpenOverflow();
                    else if (_focused >= 0 && _focused < _items.Count
                        && !_items[_focused].IsSeparator && _items[_focused].Enabled)
                        Activate(_items[_focused]);
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>구분선·비활성·오버플로 항목을 건너뛰며 다음 포커스 가능한 슬롯으로 이동한다.
        /// 오버플로 중이면 항목들 뒤에 셰브런 슬롯(인덱스 = Items.Count)이 하나 더 있다.</summary>
        private void StepFocus(int dir)
        {
            EnsureItemLayout();   // Overflowed·셰브런 상태가 최신이어야 건너뛰기가 맞다
            int slots = _items.Count + (_chevRect.IsEmpty ? 0 : 1);
            if (slots == 0) return;
            int i = _focused;
            for (int n = 0; n < slots; n++)
            {
                i += dir;
                if (i < 0) i = slots - 1;
                if (i >= slots) i = 0;
                bool ok = i == _items.Count
                    ? true   // 셰브런 슬롯(slots에 포함됐다는 것 자체가 오버플로 중)
                    : !_items[i].IsSeparator && _items[i].Enabled && !_items[i].Overflowed;
                if (ok)
                {
                    _focused = i; Invalidate();
                    // 키보드 포커스 이동을 스크린리더에 알린다. childID = 항목(또는 셰브런) 인덱스.
                    if (Focused) AccessibilityNotifyClients(AccessibleEvents.Focus, i);
                    return;
                }
            }
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            if (_focused < 0) StepFocus(+1);   // 첫 활성 버튼에 포커스
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            _layoutDirty = true;   // 글꼴이 바뀌면 항목 폭 재측정
            Invalidate();
        }

        // ── 접근성(스크린리더/UI Automation) ─────────────────────────

        /// <summary>접근성 Bounds용으로 항목 사각형이 최신인지 보장한다(아직 안 그려졌으면 측정만 수행).
        /// OnPaint의 개수 변화 정리 로직을 건드리지 않도록 _lastItemCount는 갱신하지 않는다.</summary>
        private void EnsureItemLayout()
        {
            if (!_layoutDirty && _lastItemCount == _items.Count) return;
            if (IsHandleCreated)
            {
                using (var g = CreateGraphics()) LayoutItems(g);
            }
            else
            {
                using (var bmp = new Bitmap(1, 1))
                using (var g = Graphics.FromImage(bmp)) LayoutItems(g);
            }
            _layoutDirty = false;
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new ToolBarAccessibleObject(this);
        }

        private sealed class ToolBarAccessibleObject : ControlAccessibleObject
        {
            private readonly AdvToolBar _owner;
            public ToolBarAccessibleObject(AdvToolBar owner) : base(owner) { _owner = owner; }

            public override AccessibleRole Role { get { return AccessibleRole.ToolBar; } }

            private bool HasChevron
            {
                get
                {
                    _owner.EnsureItemLayout();
                    var r = _owner._chevRect;   // CS1690 회피: 필드를 복사한 뒤 멤버 접근
                    return !r.IsEmpty;
                }
            }

            public override int GetChildCount() { return _owner._items.Count + (HasChevron ? 1 : 0); }
            public override AccessibleObject GetChild(int index)
            {
                if (index >= 0 && index < _owner._items.Count) return new ItemAccessibleObject(_owner, index);
                if (index == _owner._items.Count && HasChevron) return new ChevronAccessibleObject(_owner);
                return null;
            }

            private sealed class ItemAccessibleObject : AccessibleObject
            {
                private readonly AdvToolBar _o;
                private readonly int _i;
                public ItemAccessibleObject(AdvToolBar o, int i) { _o = o; _i = i; }

                private AdvToolBarItem It { get { return _o._items[_i]; } }

                public override AccessibleObject Parent { get { return _o.AccessibilityObject; } }

                public override AccessibleRole Role
                {
                    get
                    {
                        var it = It;
                        if (it.IsSeparator) return AccessibleRole.Separator;
                        return it.IsToggle ? AccessibleRole.CheckButton : AccessibleRole.PushButton;
                    }
                }

                public override string Name
                {
                    get
                    {
                        var it = It;
                        if (it.IsSeparator) return null;
                        // 아이콘 전용 버튼은 텍스트가 없으므로 툴팁을 이름으로 쓴다
                        return !string.IsNullOrEmpty(it.Text) ? it.Text : it.ToolTipText;
                    }
                }

                public override AccessibleStates State
                {
                    get
                    {
                        var it = It;
                        if (it.IsSeparator) return AccessibleStates.None;
                        var s = AccessibleStates.Focusable;
                        if (!it.Enabled) s |= AccessibleStates.Unavailable;
                        if (it.IsToggle && it.Checked) s |= AccessibleStates.Checked | AccessibleStates.Pressed;
                        if (_i == _o._focused && _o.Focused) s |= AccessibleStates.Focused;
                        _o.EnsureItemLayout();
                        if (it.Overflowed) s |= AccessibleStates.Invisible | AccessibleStates.Offscreen;   // 셰브런 메뉴로 접힘
                        return s;
                    }
                }

                public override Rectangle Bounds
                {
                    get
                    {
                        _o.EnsureItemLayout();
                        return _o.RectangleToScreen(It.Rect);
                    }
                }

                public override string DefaultAction
                {
                    get
                    {
                        var it = It;
                        if (it.IsSeparator || !it.Enabled) return null;
                        return it.IsToggle ? "전환" : "누르기";
                    }
                }

                public override void DoDefaultAction()
                {
                    var it = It;
                    if (!it.IsSeparator && it.Enabled) _o.Activate(it);
                }
            }

            /// <summary>오버플로 셰브런(») 버튼. childID = Items.Count(항목들 바로 뒤).</summary>
            private sealed class ChevronAccessibleObject : AccessibleObject
            {
                private readonly AdvToolBar _o;
                public ChevronAccessibleObject(AdvToolBar o) { _o = o; }

                public override AccessibleObject Parent { get { return _o.AccessibilityObject; } }
                public override AccessibleRole Role { get { return AccessibleRole.ButtonDropDown; } }
                public override string Name { get { return "더 보기"; } }

                public override AccessibleStates State
                {
                    get
                    {
                        var s = AccessibleStates.Focusable | AccessibleStates.HasPopup;
                        if (_o._overflowOpen) s |= AccessibleStates.Pressed;
                        if (_o._focused == _o._items.Count && _o.Focused) s |= AccessibleStates.Focused;
                        return s;
                    }
                }

                public override Rectangle Bounds
                {
                    get
                    {
                        _o.EnsureItemLayout();
                        var r = _o._chevRect;   // CS1690 회피: 필드를 복사한 뒤 멤버 접근
                        return r.IsEmpty ? Rectangle.Empty : _o.RectangleToScreen(r);
                    }
                }

                public override string DefaultAction { get { return "열기"; } }
                public override void DoDefaultAction() { _o.OpenOverflow(); }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Region != null) Region.Dispose();
                if (_tip != null) _tip.Dispose();
                if (_overflow != null) _overflow.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>AdvToolBar가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvToolBarOptions : AdvOptions
    {
        internal AdvToolBarOptions(AdvToolBar owner) : base(owner.Styling, owner.Palette) { }
    }
}
