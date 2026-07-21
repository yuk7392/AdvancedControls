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
        internal int TextW;   // LayoutItems가 한 번 측정해 저장(그리기 루프가 재사용)

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

        private readonly List<AdvToolBarItem> _items = new List<AdvToolBarItem>();
        private int _hover = -1;
        private int _pressed = -1;
        private int _focused = -1;   // 키보드 포커스 항목
        private bool _layoutDirty = true;   // 항목·글꼴이 바뀔 때만 레이아웃 재계산
        private int _lastItemCount = -1;    // Items를 외부에서 직접 변형(RemoveAt/Clear)한 경우 감지용
        private ToolTip _tip;               // 아이콘 버튼 등의 툴팁(지연 생성)

        // 비활성 아이콘용 반투명 매트릭스는 항상 같은 값이라 한 번만 만들어 재사용한다(매 프레임 할당 방지)
        private static readonly System.Drawing.Imaging.ImageAttributes _disabledAttr = CreateDisabledAttr();
        private static System.Drawing.Imaging.ImageAttributes CreateDisabledAttr()
        {
            var ia = new System.Drawing.Imaging.ImageAttributes();
            ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.4f });
            return ia;
        }

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
            int x = SideInset;
            foreach (var it in _items)
            {
                int w;
                if (it.IsSeparator) { it.TextW = 0; w = SepWidth; }
                else
                {
                    int tw = string.IsNullOrEmpty(it.Text) ? 0 : TextRenderer.MeasureText(g, it.Text, Font).Width;
                    it.TextW = tw;   // 그리기 루프가 재사용(매 페인트 재측정 방지)
                    int iw = it.Image != null ? IconSize : 0;
                    int mid = iw + (iw > 0 && tw > 0 ? Gap : 0) + tw;
                    w = PadX * 2 + mid;
                }
                it.Rect = new Rectangle(x, 3, w, Height - 6);
                x += w;
            }
        }

        // ── 그리기 ────────────────────────────────────────────────────

        /// <summary>
        /// 모서리 반경(Styling.Radius)이 0이면 풀폭 도킹 사각 바(아래 구분선만),
        /// 0보다 크면 둥근 '플로팅 필'(전체 둥근 테두리)로 그린다. 둥글면 아래 Region으로 코너를 자른다.
        /// </summary>
        private void ApplyRoundedRegion()
        {
            if (!IsHandleCreated) return;
            var old = Region;
            if (EffectiveCorners.IsZero) Region = null;
            else
            {
                var clip = Rectangle.Inflate(FrameBounds, 1, 1);
                using (var path = AdvGraphics.CreateRoundedRect(clip, EffectiveCorners))
                    Region = new Region(path);
            }
            if (old != null) old.Dispose();
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyRoundedRegion(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); ApplyRoundedRegion(); }
        protected override void OnThemeChanged() { base.OnThemeChanged(); ApplyRoundedRegion(); }

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

                int iw = it.Image != null ? IconSize : 0;
                int tw = it.TextW;   // LayoutItems에서 이미 측정
                int mid = iw + (iw > 0 && tw > 0 ? Gap : 0) + tw;
                int startX = it.Rect.Left + (it.Rect.Width - mid) / 2;

                if (it.Image != null)
                {
                    var ir = new Rectangle(startX, it.Rect.Top + (it.Rect.Height - IconSize) / 2, IconSize, IconSize);
                    if (it.Enabled) g.DrawImage(it.Image, ir);
                    else DrawDisabledImage(g, it.Image, ir);
                    startX += IconSize + (tw > 0 ? Gap : 0);
                }

                if (tw > 0)
                {
                    var tr = new Rectangle(startX, it.Rect.Top, tw + 4, it.Rect.Height);
                    TextRenderer.DrawText(g, it.Text, Font, tr, fg,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            }
        }

        private static void DrawDisabledImage(Graphics g, Image img, Rectangle r)
        {
            g.DrawImage(img, r, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, _disabledAttr);
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
            if (_hover != -1 || _pressed != -1) { _hover = -1; _pressed = -1; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
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
                case Keys.End: _focused = _items.Count; StepFocus(-1); e.Handled = true; break;
                case Keys.Return:
                case Keys.Space:
                    if (_focused >= 0 && _focused < _items.Count
                        && !_items[_focused].IsSeparator && _items[_focused].Enabled)
                        Activate(_items[_focused]);
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>구분선·비활성을 건너뛰며 다음 포커스 가능한 항목으로 이동한다.</summary>
        private void StepFocus(int dir)
        {
            if (_items.Count == 0) return;
            int i = _focused;
            for (int n = 0; n < _items.Count; n++)
            {
                i += dir;
                if (i < 0) i = _items.Count - 1;
                if (i >= _items.Count) i = 0;
                if (!_items[i].IsSeparator && _items[i].Enabled) { _focused = i; Invalidate(); return; }
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Region != null) Region.Dispose();
                if (_tip != null) _tip.Dispose();
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
