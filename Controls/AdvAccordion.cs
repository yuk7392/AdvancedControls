using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Animation;
using AdvancedControls.Rendering;

namespace AdvancedControls.Controls
{
    /// <summary>
    /// 아코디언의 한 섹션. 머리글을 눌러 본문(자식 컨트롤)을 높이 애니메이션으로 펼치고 접는다.
    /// 본문 높이는 <see cref="BodyHeight"/>로 정하며, 접히면 자식은 그대로 두고 컨테이너 높이만 줄여 클리핑한다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultProperty("Title")]
    [Description("아코디언의 한 섹션입니다.")]
    public class AdvAccordionItem : AdvContainerBase
    {
        private const int ChevW = 28;
        private const int HeaderPadH = 12;

        private readonly AdvAnimator _anim;
        private string _title = string.Empty;
        private bool _expanded;
        private int _bodyHeight = 120;
        private bool _headerHover;
        private Pen _sepPen;   // 전환 중 매 틱 재생성을 피하려 캐싱한다(색·두께만 갱신)
        private AdvAccordionItemOptions _options;

        /// <summary>Expanded가 바뀌면 발생한다(부모 아코디언이 하나만 열기 위해 듣는다).</summary>
        public event EventHandler ExpandedChanged;

        public AdvAccordionItem()
        {
            // 머리글에 키보드 포커스를 주어 Enter/Space로 펼치고 접을 수 있게 한다.
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;

            _anim = new AdvAnimator(0);
            _anim.ValueChanged += OnAnimTick;
            _anim.SetImmediate(0f);
        }

        protected override Size DefaultSize
        {
            get { return new Size(320, 40); }
        }

        [Category("Appearance")]
        [DefaultValue("")]
        [Description("머리글에 표시할 제목입니다.")]
        public string Title
        {
            get { return _title; }
            set
            {
                value = value ?? string.Empty;
                if (_title == value) return;
                _title = value;
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("펼쳐진 상태인지 여부입니다. 바꾸면 애니메이션으로 전환됩니다.")]
        public bool Expanded
        {
            get { return _expanded; }
            set
            {
                if (_expanded == value) return;
                _expanded = value;
                // 펼치면 감췄던 본문 자식을 즉시 되살려 열리는 동안 보이게 한다(접힘 완료 시 다시 숨긴다).
                if (value && !DesignMode) RestoreChildrenAfterCollapse();
                _anim.Duration = DesignMode ? 0 : EffectiveTheme.TransitionDuration;
                _anim.AnimateTo(value ? 1f : 0f);
                ApplyHeight();
                Invalidate();

                var h = ExpandedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        [Category("Behavior")]
        [DefaultValue(120)]
        [Description("펼쳤을 때의 본문 높이입니다.")]
        public int BodyHeight
        {
            get { return _bodyHeight; }
            set
            {
                value = Math.Max(0, value);
                if (_bodyHeight == value) return;
                _bodyHeight = value;
                ApplyHeight();
            }
        }

        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvAccordionItemOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvAccordionItemOptions(this)); }
        }

        private int HeaderHeight
        {
            get { return Font.Height + 16; }
        }

        /// <summary>현재 애니메이션 진행에 맞춰 컨트롤 높이를 맞춘다(머리글 + 본문×진행).</summary>
        private void ApplyHeight()
        {
            if (!IsHandleCreated) return;

            int target = HeaderHeight + (int)Math.Round(_bodyHeight * _anim.Eased);
            if (Height == target) return;

            Height = target;      // Dock=Top이면 부모가 형제들을 다시 배치한다
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            ApplyHeight();
            // 접힘이 끝나면 본문 자식을 숨겨 탭 순서에서 뺀다(안 보이는데 포커스가 가는 것 방지).
            if (!DesignMode && !_expanded && _anim.Eased <= 0.001f) HideChildrenForCollapse();
            Invalidate();
        }

        /// <summary>자식 컨트롤이 놓이는 본문 영역. 머리글 아래다.</summary>
        public override Rectangle DisplayRectangle
        {
            get
            {
                var frame = FrameBounds;
                int bw = EffectiveBorderWidth;
                int top = frame.Top + HeaderHeight;
                return new Rectangle(
                    frame.Left + bw, top,
                    Math.Max(0, frame.Width - bw * 2),
                    Math.Max(0, frame.Bottom - top - bw));
            }
        }

        private Rectangle HeaderRect
        {
            get
            {
                var b = FrameBounds;
                int bw = EffectiveBorderWidth;
                return new Rectangle(b.Left + bw, b.Top + bw, Math.Max(0, b.Width - bw * 2), Math.Max(0, HeaderHeight - bw));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = FrameBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            AdvFrameRenderer.Draw(g, bounds, theme, EffectiveCorners, EffectiveBorderWidth,
                                  theme.Surface, theme.SurfaceGradientEnd, theme.Border,
                                  null, CurrentElevation, EffectiveBorderDash);

            int bw = EffectiveBorderWidth;
            var header = HeaderRect;

            if (_headerHover && Enabled)
                using (var b = new SolidBrush(theme.SurfaceHover))
                    g.FillRectangle(b, header);

            var titleRect = new Rectangle(header.Left + HeaderPadH, header.Top,
                                          Math.Max(0, header.Width - HeaderPadH - ChevW), header.Height);
            TextRenderer.DrawText(g, _title, Font, titleRect, Enabled ? theme.Text : theme.TextDisabled,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            var chev = new Rectangle(header.Right - ChevW, header.Top, ChevW, header.Height);
            AdvGraphics.DrawChevron(g, chev,
                _expanded ? AdvGraphics.ChevronDirection.Down : AdvGraphics.ChevronDirection.Right,
                theme.TextMuted, 9, 5, 1.6f, 0);

            // 본문이 조금이라도 열려 있으면 머리글 아래 구분선.
            if (_anim.Eased > 0.01f)
            {
                int y = bounds.Top + bw + HeaderHeight;
                if (_sepPen == null) _sepPen = new Pen(theme.Border, bw);
                else { _sepPen.Color = theme.Border; _sepPen.Width = bw; }
                g.DrawLine(_sepPen, bounds.Left + bw, y, bounds.Right - bw, y);
            }

            // 머리글에 키보드 포커스가 있으면 포커스 링을 그린다.
            if (Focused && Enabled)
            {
                var fr = Rectangle.Inflate(header, -2, -2);
                using (var pen = new Pen(theme.FocusRing, 1.5f))
                    g.DrawRectangle(pen, fr.Left, fr.Top, fr.Width - 1, fr.Height - 1);
            }

            base.OnPaint(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool over = HeaderRect.Contains(e.Location);
            if (over != _headerHover)
            {
                _headerHover = over;
                Cursor = over ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_headerHover) { _headerHover = false; Cursor = Cursors.Default; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && HeaderRect.Contains(e.Location))
            {
                if (!Focused) Focus();
                Expanded = !Expanded;
            }
        }

        /// <summary>Enter/Space가 포커스 이동에 먹히지 않고 이 컨트롤로 오게 한다.</summary>
        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Return:
                case Keys.Space:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                Expanded = !Expanded;
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyHeight();
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            // 접힌 상태에서 추가된 자식은 곧바로 숨겨 탭 순서에 남지 않게 한다.
            if (!DesignMode && !_expanded) HideChildForCollapse(e.Control);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            ApplyHeight();
            Invalidate();
            base.OnFontChanged(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _anim.ValueChanged -= OnAnimTick;
                _anim.Dispose();
                if (_sepPen != null) { _sepPen.Dispose(); _sepPen = null; }
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// <see cref="AdvAccordionItem"/> 섹션들을 세로로 쌓아 펼치고 접는 아코디언.
    /// 항목은 Dock=Top으로 배치되어 한 항목이 펼쳐질 때 아래 항목들이 자동으로 밀린다.
    /// Bootstrap의 <c>.accordion</c>에 대응한다.
    /// </summary>
    [ToolboxItem(true)]
    [Description("여러 섹션을 펼치고 접는 아코디언입니다.")]
    public class AdvAccordion : AdvContainerBase
    {
        private bool _singleExpand = true;
        private bool _syncing;
        private AdvAccordionOptions _options;

        protected override Size DefaultSize
        {
            get { return new Size(340, 220); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(true)]
        [Description("한 번에 하나의 섹션만 펼칠지 여부입니다.")]
        public bool SingleExpand
        {
            get { return _singleExpand; }
            set { _singleExpand = value; }
        }

        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvAccordionOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvAccordionOptions(this)); }
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            var item = e.Control as AdvAccordionItem;
            if (item != null)
            {
                item.Dock = DockStyle.Top;
                // Dock=Top은 나중에 도킹된 항목을 위쪽에 앉혀, 그대로 두면 추가 순서의 역순으로 쌓인다.
                // 새 항목을 z-순서 맨 앞으로 보내(가장 늦게 도킹되게) 추가 순서를 위→아래로 유지한다.
                item.BringToFront();
                item.ExpandedChanged += ItemExpandedChanged;
            }
        }

        protected override void OnControlRemoved(ControlEventArgs e)
        {
            var item = e.Control as AdvAccordionItem;
            if (item != null) item.ExpandedChanged -= ItemExpandedChanged;
            base.OnControlRemoved(e);
        }

        private void ItemExpandedChanged(object sender, EventArgs e)
        {
            if (_syncing || !_singleExpand) return;

            var opened = sender as AdvAccordionItem;
            if (opened == null || !opened.Expanded) return;

            // 하나만 열기 — 방금 펼친 것을 뺀 나머지를 접는다.
            _syncing = true;
            try
            {
                foreach (Control c in Controls)
                {
                    var it = c as AdvAccordionItem;
                    if (it != null && it != opened && it.Expanded) it.Expanded = false;
                }
            }
            finally { _syncing = false; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // 항목 사이·주변 배경을 테마 면색으로 채운다.
            e.Graphics.Clear(EffectiveTheme.Surface);
            base.OnPaint(e);
        }
    }

    /// <summary>AdvAccordionItem이 추가한 속성. 다른 컨트롤과 동일하게 Styling/Palette 접근을 노출한다.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvAccordionItemOptions : AdvOptions
    {
        internal AdvAccordionItemOptions(AdvAccordionItem owner) : base(owner.Styling, owner.Palette)
        {
        }
    }

    /// <summary>AdvAccordion이 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvAccordionOptions : AdvOptions
    {
        private readonly AdvAccordion _owner;

        internal AdvAccordionOptions(AdvAccordion owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(true)]
        [Description("한 번에 하나의 섹션만 펼칠지 여부입니다.")]
        public bool SingleExpand
        {
            get { return _owner.SingleExpand; }
            set { _owner.SingleExpand = value; }
        }
    }
}
