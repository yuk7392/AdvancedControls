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
    /// 작은 알림/카운트 배지. 컨텍스트 색으로 채운 알약 또는 둥근 사각형에 짧은 글자를 담는다.
    /// Bootstrap의 <c>.badge</c>에 대응한다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultProperty("Text")]
    [Description("작은 알림/카운트 배지입니다.")]
    public class AdvBadge : AdvControlBase
    {
        private const int PadH = 8;
        private const int PadV = 3;

        private AdvContextColor _context = AdvContextColor.Secondary;
        private bool _pill = true;
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
        [DefaultValue(AdvContextColor.Secondary)]
        [Description("배지의 컨텍스트 색입니다.")]
        public AdvContextColor Context
        {
            get { return _context; }
            set { if (_context == value) return; _context = value; Invalidate(); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(true)]
        [Description("완전히 둥근 알약 모양으로 그릴지 여부입니다. 끄면 Styling의 모서리 반경을 따릅니다.")]
        public bool Pill
        {
            get { return _pill; }
            set { if (_pill == value) return; _pill = value; Invalidate(); }
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
            var palette = theme.ResolveContext(_context);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = FrameBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

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

        [DefaultValue(AdvContextColor.Secondary)]
        [Description("배지의 컨텍스트 색입니다.")]
        public AdvContextColor Context
        {
            get { return _owner.Context; }
            set { _owner.Context = value; }
        }

        [DefaultValue(true)]
        [Description("완전히 둥근 알약 모양으로 그릴지 여부입니다.")]
        public bool Pill
        {
            get { return _owner.Pill; }
            set { _owner.Pill = value; }
        }
    }
}
