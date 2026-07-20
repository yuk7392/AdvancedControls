using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>알림 앞에 붙는 상황 아이콘.</summary>
    public enum AdvAlertIcon
    {
        /// <summary>아이콘 없음.</summary>
        None,
        /// <summary>정보(ⓘ).</summary>
        Info,
        /// <summary>성공(✓).</summary>
        Success,
        /// <summary>경고(△!).</summary>
        Warning,
        /// <summary>오류(⊗).</summary>
        Error
    }

    /// <summary>
    /// 상황별 알림 메시지 박스. 강조 색의 옅은 배경/테두리/글자를 쓰고, 선택적으로
    /// 닫기 버튼을 단다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultProperty("AdvancedControlOptions")]
    [DefaultEvent("Dismissed")]
    [Description("상황별 알림 메시지 박스입니다.")]
    public class AdvAlert : AdvControlBase
    {
        private Color _context = Color.Empty;
        private bool _dismissible;
        private bool _closeHover;
        private AdvAlertIcon _icon = AdvAlertIcon.None;
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

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [Description("알림 색입니다. 비워 두면 테마 강조색(Accent)을 따릅니다. 옅은 배경/테두리/글자로 파생됩니다.")]
        public Color Context
        {
            get { return _context; }
            set { if (_context == value) return; _context = value; Invalidate(); }
        }
        public bool ShouldSerializeContext() { return !_context.IsEmpty; }
        public void ResetContext() { Context = Color.Empty; }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("닫기(X) 버튼을 표시할지 여부입니다.")]
        public bool Dismissible
        {
            get { return _dismissible; }
            set { if (_dismissible == value) return; _dismissible = value; Invalidate(); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvAlertIcon.None)]
        [Description("알림 앞에 붙는 상황 아이콘입니다. 알림 색으로 그려집니다.")]
        public AdvAlertIcon Icon
        {
            get { return _icon; }
            set { if (_icon == value) return; _icon = value; Invalidate(); }
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
            var palette = AdvContextPalette.Resolve(_context, theme);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = FrameBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            int bw = Math.Max(1, EffectiveBorderWidth);
            AdvFrameRenderer.Draw(g, bounds, theme, EffectiveCorners, bw,
                                  palette.SubtleBg, Color.Empty, palette.SubtleBorder,
                                  null, CurrentElevation, EffectiveBorderDash, EffectiveGradientAngle);

            var content = new Rectangle(
                bounds.Left + bw + Padding.Left, bounds.Top + bw + Padding.Top,
                Math.Max(0, bounds.Width - bw * 2 - Padding.Horizontal),
                Math.Max(0, bounds.Height - bw * 2 - Padding.Vertical));

            // 상황 아이콘을 왼쪽에 그리고 글자 시작 위치를 그만큼 민다
            int textLeft = content.Left;
            if (_icon != AdvAlertIcon.None)
            {
                int gs = Font.Height;
                var iconRect = new Rectangle(content.Left,
                                             content.Top + Math.Max(0, (content.Height - gs) / 2), gs, gs);
                DrawAlertGlyph(g, iconRect, _icon, palette.SubtleText);
                textLeft = iconRect.Right + 8;
            }

            int textRight = content.Right;
            if (_dismissible)
            {
                int cs = Font.Height;
                // 닫기 X도 내용 영역 세로 중앙에 둔다(텍스트와 높이를 맞춘다)
                _closeRect = new Rectangle(content.Right - cs,
                                           content.Top + Math.Max(0, (content.Height - cs) / 2), cs, cs);
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

            var textRect = new Rectangle(textLeft, content.Top,
                                         Math.Max(0, textRight - textLeft), content.Height);

            // 한 줄처럼 내용이 영역 안에 들어오면 세로 중앙, 넘치면 위에서부터(아래를 잘라도 첫 줄은 보이게).
            var flags = TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
            var measured = TextRenderer.MeasureText(g, Text, Font,
                new Size(textRect.Width, int.MaxValue), flags);
            flags |= measured.Height <= textRect.Height
                   ? TextFormatFlags.VerticalCenter
                   : TextFormatFlags.Top;
            TextRenderer.DrawText(g, Text, Font, textRect, palette.SubtleText, flags);

            base.OnPaint(e);
        }

        /// <summary>상황 아이콘을 벡터로 그린다. 색은 알림 글자색(SubtleText)을 따른다.</summary>
        private static void DrawAlertGlyph(Graphics g, Rectangle r, AdvAlertIcon icon, Color color)
        {
            using (var pen = new Pen(color, 1.6f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var brush = new SolidBrush(color))
            {
                int cx = r.Left + r.Width / 2;
                switch (icon)
                {
                    case AdvAlertIcon.Success:
                    {
                        var b = Rectangle.Inflate(r, -r.Width / 6, -r.Height / 6);
                        g.DrawLines(pen, new[]
                        {
                            new Point(b.Left, b.Top + b.Height * 3 / 5),
                            new Point(b.Left + b.Width * 2 / 5, b.Bottom),
                            new Point(b.Right, b.Top)
                        });
                        break;
                    }
                    case AdvAlertIcon.Info:
                        g.DrawEllipse(pen, r.Left, r.Top, r.Width - 1, r.Height - 1);
                        g.FillEllipse(brush, cx - 1, r.Top + r.Height / 4 - 1, 3, 3);
                        g.DrawLine(pen, cx, r.Top + r.Height * 9 / 20, cx, r.Bottom - r.Height / 5);
                        break;

                    case AdvAlertIcon.Error:
                    {
                        g.DrawEllipse(pen, r.Left, r.Top, r.Width - 1, r.Height - 1);
                        var b = Rectangle.Inflate(r, -r.Width / 3, -r.Height / 3);
                        g.DrawLine(pen, b.Left, b.Top, b.Right, b.Bottom);
                        g.DrawLine(pen, b.Left, b.Bottom, b.Right, b.Top);
                        break;
                    }
                    case AdvAlertIcon.Warning:
                        g.DrawPolygon(pen, new[]
                        {
                            new Point(cx, r.Top),
                            new Point(r.Right - 1, r.Bottom - 1),
                            new Point(r.Left, r.Bottom - 1)
                        });
                        g.DrawLine(pen, cx, r.Top + r.Height * 8 / 20, cx, r.Top + r.Height * 13 / 20);
                        g.FillEllipse(brush, cx - 1, r.Bottom - r.Height * 4 / 20 - 1, 3, 3);
                        break;
                }
            }
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

        [Description("알림 색입니다. 비워 두면 테마 강조색(Accent)을 따릅니다.")]
        public Color Context
        {
            get { return _owner.Context; }
            set { _owner.Context = value; }
        }
        public bool ShouldSerializeContext() { return _owner.ShouldSerializeContext(); }
        public void ResetContext() { _owner.ResetContext(); }

        [DefaultValue(false)]
        [Description("닫기(X) 버튼을 표시할지 여부입니다.")]
        public bool Dismissible
        {
            get { return _owner.Dismissible; }
            set { _owner.Dismissible = value; }
        }

        [DefaultValue(AdvAlertIcon.None)]
        [Description("알림 앞에 붙는 상황 아이콘입니다. 알림 색으로 그려집니다.")]
        public AdvAlertIcon Icon
        {
            get { return _owner.Icon; }
            set { _owner.Icon = value; }
        }
    }
}
