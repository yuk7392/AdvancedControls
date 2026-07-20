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
    /// 상황별 알림 메시지 박스. 컨텍스트 색의 옅은 배경/테두리/글자를 쓰고, 선택적으로
    /// 닫기 버튼을 단다. Bootstrap의 <c>.alert</c>에 대응한다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultProperty("Text")]
    [DefaultEvent("Dismissed")]
    [Description("상황별 알림 메시지 박스입니다.")]
    public class AdvAlert : AdvControlBase
    {
        private AdvContextColor _context = AdvContextColor.Info;
        private bool _dismissible;
        private bool _closeHover;
        private Rectangle _closeRect = Rectangle.Empty;
        private AdvAlertOptions _options;

        /// <summary>닫기 버튼을 눌러 사라졌을 때 발생한다.</summary>
        public event EventHandler Dismissed;

        public AdvAlert()
        {
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }

        protected override Size DefaultSize
        {
            get { return new Size(320, 48); }
        }

        protected override Padding DefaultPadding
        {
            get { return new Padding(14, 10, 14, 10); }
        }

        [Category("Appearance")]
        [DefaultValue(AdvContextColor.Info)]
        [Description("알림의 컨텍스트 색입니다.")]
        public AdvContextColor Context
        {
            get { return _context; }
            set { if (_context == value) return; _context = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("닫기(X) 버튼을 표시할지 여부입니다.")]
        public bool Dismissible
        {
            get { return _dismissible; }
            set { if (_dismissible == value) return; _dismissible = value; Invalidate(); }
        }

        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvAlertOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvAlertOptions(this)); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var palette = theme.ResolveContext(_context);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = FrameBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            int bw = Math.Max(1, EffectiveBorderWidth);
            AdvFrameRenderer.Draw(g, bounds, theme, EffectiveCorners, bw,
                                  palette.SubtleBg, Color.Empty, palette.SubtleBorder,
                                  null, CurrentElevation, EffectiveBorderDash);

            var content = new Rectangle(
                bounds.Left + bw + Padding.Left, bounds.Top + bw + Padding.Top,
                Math.Max(0, bounds.Width - bw * 2 - Padding.Horizontal),
                Math.Max(0, bounds.Height - bw * 2 - Padding.Vertical));

            int textRight = content.Right;
            if (_dismissible)
            {
                int cs = Font.Height;
                _closeRect = new Rectangle(content.Right - cs, content.Top, cs, cs);
                textRight = _closeRect.Left - 6;

                Color x = _closeHover ? AdvContextPalette.Shade(palette.SubtleText, 0.25f) : palette.SubtleText;
                var box = Rectangle.Inflate(_closeRect, -cs / 5, -cs / 5);
                using (var pen = new Pen(x, 1.5f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, box.Left, box.Top, box.Right, box.Bottom);
                    g.DrawLine(pen, box.Left, box.Bottom, box.Right, box.Top);
                }
            }
            else
            {
                _closeRect = Rectangle.Empty;
            }

            var textRect = new Rectangle(content.Left, content.Top,
                                         Math.Max(0, textRight - content.Left), content.Height);
            TextRenderer.DrawText(g, Text, Font, textRect, palette.SubtleText,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

            base.OnPaint(e);
        }

        private void Dismiss()
        {
            var h = Dismissed;
            if (h != null) h(this, EventArgs.Empty);
            Visible = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool over = _dismissible && _closeRect.Contains(e.Location);
            if (over != _closeHover)
            {
                _closeHover = over;
                Cursor = over ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_closeHover) { _closeHover = false; Cursor = Cursors.Default; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && _dismissible && _closeRect.Contains(e.Location))
                Dismiss();
        }

        protected override void OnThemeChanged()
        {
            Invalidate();
            base.OnThemeChanged();
        }
    }

    /// <summary>AdvAlert가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvAlertOptions : AdvOptions
    {
        private readonly AdvAlert _owner;

        internal AdvAlertOptions(AdvAlert owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(AdvContextColor.Info)]
        [Description("알림의 컨텍스트 색입니다.")]
        public AdvContextColor Context
        {
            get { return _owner.Context; }
            set { _owner.Context = value; }
        }

        [DefaultValue(false)]
        [Description("닫기(X) 버튼을 표시할지 여부입니다.")]
        public bool Dismissible
        {
            get { return _owner.Dismissible; }
            set { _owner.Dismissible = value; }
        }
    }
}
