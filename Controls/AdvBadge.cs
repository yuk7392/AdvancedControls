using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>
    /// 작은 알림/카운트 배지. 강조 색으로 채운 알약 또는 둥근 사각형에 짧은 글자를 담는다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultProperty("AdvancedControlOptions")]
    [Description("작은 알림/카운트 배지입니다.")]
    public class AdvBadge : AdvControlBase
    {
        private const int PadH = 8;
        private const int PadV = 3;

        private Color _context = Color.Empty;
        private bool _pill = true;
        private bool _dot;
        private Control _overlayTarget;
        private AdvBadgeOptions _options;

        public AdvBadge()
        {
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            AutoSize = true;
        }

        protected override Size DefaultSize
        {
            get { return new Size(44, 22); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [Description("배지 색입니다. 비워 두면 테마 강조색(Accent)을 따릅니다.")]
        public Color Context
        {
            get { return _context; }
            set { if (_context == value) return; _context = value; Invalidate(); }
        }
        public bool ShouldSerializeContext() { return !_context.IsEmpty; }
        public void ResetContext() { Context = Color.Empty; }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(true)]
        [Description("완전히 둥근 알약 모양으로 그릴지 여부입니다. 끄면 Styling의 모서리 반경을 따릅니다.")]
        public bool Pill
        {
            get { return _pill; }
            set { if (_pill == value) return; _pill = value; Invalidate(); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("글자 없이 작은 점으로만 표시합니다. 알림 유무만 나타낼 때 씁니다.")]
        public bool Dot
        {
            get { return _dot; }
            set { if (_dot == value) return; _dot = value; AdjustSize(); RepositionOverlay(); Invalidate(); }
        }

        /// <summary>
        /// 이 배지를 얹을 대상 컨트롤. 지정하면 대상의 오른쪽 위 모서리에 겹쳐 따라다닌다.
        /// 아이콘 위의 알림 카운트/점 배지에 쓴다.
        /// </summary>
        [Browsable(false)]
        [DefaultValue(null)]
        [Description("이 배지를 얹을 대상 컨트롤입니다. 지정하면 대상의 오른쪽 위 모서리에 겹쳐 따라다닙니다.")]
        public Control OverlayTarget
        {
            get { return _overlayTarget; }
            set
            {
                if (ReferenceEquals(_overlayTarget, value)) return;
                DetachOverlay();
                _overlayTarget = value;
                AttachOverlay();
                RepositionOverlay();
            }
        }

        private void AttachOverlay()
        {
            if (_overlayTarget == null) return;
            _overlayTarget.LocationChanged += OverlayTargetMoved;
            _overlayTarget.SizeChanged += OverlayTargetMoved;
            _overlayTarget.VisibleChanged += OverlayTargetMoved;
            _overlayTarget.Disposed += OverlayTargetDisposed;
        }

        private void DetachOverlay()
        {
            if (_overlayTarget == null) return;
            _overlayTarget.LocationChanged -= OverlayTargetMoved;
            _overlayTarget.SizeChanged -= OverlayTargetMoved;
            _overlayTarget.VisibleChanged -= OverlayTargetMoved;
            _overlayTarget.Disposed -= OverlayTargetDisposed;
        }

        private void OverlayTargetMoved(object sender, EventArgs e) { RepositionOverlay(); }
        private void OverlayTargetDisposed(object sender, EventArgs e) { OverlayTarget = null; }

        /// <summary>대상의 오른쪽 위 모서리에 배지 중심을 맞추고 맨 앞으로 올린다.</summary>
        private void RepositionOverlay()
        {
            var t = _overlayTarget;
            if (t == null || t.IsDisposed) return;

            // 같은 부모여야 좌표가 맞는다. 다르면 대상의 부모로 옮긴다.
            if (t.Parent != null && !ReferenceEquals(Parent, t.Parent))
                t.Parent.Controls.Add(this);

            Visible = t.Visible;
            if (!Visible) return;

            Left = t.Right - Width / 2;
            Top = t.Top - Height / 2;
            BringToFront();
        }

        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvBadgeOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvBadgeOptions(this)); }
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            if (_dot)
            {
                int d = Math.Max(8, Font.Height / 2 + 2);
                return new Size(d, d);
            }

            var text = TextRenderer.MeasureText(Text ?? string.Empty, Font,
                new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix);
            int h = text.Height + PadV * 2;
            int w = Math.Max(h, text.Width + PadH * 2);   // 최소 폭은 높이(원형 점 배지 대비)
            return new Size(w, h);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            AdjustSize();
            base.OnTextChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var palette = AdvContextPalette.Resolve(_context, theme);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = FrameBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            // 점 배지: 글자 없이 작은 원만 그린다
            if (_dot)
            {
                using (var brush = new SolidBrush(palette.Solid))
                    g.FillEllipse(brush, bounds);
                base.OnPaint(e);
                return;
            }

            var path = _pill
                ? AdvGraphics.CreateRoundedRect(bounds, bounds.Height / 2)
                : AdvGraphics.CreateRoundedRect(bounds, EffectiveCorners);

            using (path)
            using (var brush = new SolidBrush(palette.Solid))
                g.FillPath(brush, path);

            TextRenderer.DrawText(g, Text, Font, bounds, palette.OnSolid,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            base.OnPaint(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) DetachOverlay();
            base.Dispose(disposing);
        }

        protected override void OnThemeChanged()
        {
            Invalidate();
            base.OnThemeChanged();
        }
    }

    /// <summary>AdvBadge가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvBadgeOptions : AdvOptions
    {
        private readonly AdvBadge _owner;

        internal AdvBadgeOptions(AdvBadge owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [Description("배지 색입니다. 비워 두면 테마 강조색(Accent)을 따릅니다.")]
        public Color Context
        {
            get { return _owner.Context; }
            set { _owner.Context = value; }
        }
        public bool ShouldSerializeContext() { return _owner.ShouldSerializeContext(); }
        public void ResetContext() { _owner.ResetContext(); }

        [DefaultValue(true)]
        [Description("완전히 둥근 알약 모양으로 그릴지 여부입니다.")]
        public bool Pill
        {
            get { return _owner.Pill; }
            set { _owner.Pill = value; }
        }

        [DefaultValue(false)]
        [Description("글자 없이 작은 점으로만 표시합니다. 알림 유무만 나타낼 때 씁니다.")]
        public bool Dot
        {
            get { return _owner.Dot; }
            set { _owner.Dot = value; }
        }

        [DefaultValue(null)]
        [Description("이 배지를 얹을 대상 컨트롤입니다. 지정하면 대상의 오른쪽 위 모서리에 겹쳐 따라다닙니다.")]
        public Control OverlayTarget
        {
            get { return _owner.OverlayTarget; }
            set { _owner.OverlayTarget = value; }
        }
    }
}
