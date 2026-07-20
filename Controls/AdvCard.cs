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
    /// 둥근 테두리 박스에 선택적 머리글/바닥글과 본문(자식 컨트롤)을 담는 범용 컨테이너.
    /// Bootstrap의 <c>.card</c>에 대응한다. 머리글/바닥글 글자는 <see cref="HeaderText"/>·
    /// <see cref="FooterText"/>에 있고, 그 사이 본문 영역이 자식 컨트롤이 놓이는 자리다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultProperty("HeaderText")]
    [Description("머리글/바닥글을 가진 테마 카드 컨테이너입니다.")]
    public class AdvCard : AdvContainerBase
    {
        // 머리글/바닥글 글자 주위의 안쪽 여백(좌우, 상하).
        private const int PadH = 12;
        private const int PadV = 8;

        private string _headerText = string.Empty;
        private string _footerText = string.Empty;
        private bool _showHeaderSeparator = true;
        private bool _showFooterSeparator = true;
        private AdvCardOptions _options;

        [Category("Appearance")]
        [DefaultValue("")]
        [Description("머리글에 표시할 제목입니다. 비우면 머리글 영역이 사라집니다.")]
        public string HeaderText
        {
            get { return _headerText; }
            set
            {
                value = value ?? string.Empty;
                if (_headerText == value) return;
                _headerText = value;
                PerformLayout();
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue("")]
        [Description("바닥글에 표시할 글자입니다. 비우면 바닥글 영역이 사라집니다.")]
        public string FooterText
        {
            get { return _footerText; }
            set
            {
                value = value ?? string.Empty;
                if (_footerText == value) return;
                _footerText = value;
                PerformLayout();
                Invalidate();
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(true)]
        [Description("머리글 아래에 구분선을 그릴지 여부입니다.")]
        public bool ShowHeaderSeparator
        {
            get { return _showHeaderSeparator; }
            set { if (_showHeaderSeparator == value) return; _showHeaderSeparator = value; PerformLayout(); Invalidate(); }
        }

        [Browsable(false)]
        [DefaultValue(true)]
        [Description("바닥글 위에 구분선을 그릴지 여부입니다.")]
        public bool ShowFooterSeparator
        {
            get { return _showFooterSeparator; }
            set { if (_showFooterSeparator == value) return; _showFooterSeparator = value; PerformLayout(); Invalidate(); }
        }

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvCardOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvCardOptions(this)); }
        }

        private int HeaderHeight
        {
            get { return string.IsNullOrEmpty(_headerText) ? 0 : Font.Height + PadV * 2; }
        }

        private int FooterHeight
        {
            get { return string.IsNullOrEmpty(_footerText) ? 0 : Font.Height + PadV * 2; }
        }

        private bool HeaderSeparatorVisible
        {
            get { return HeaderHeight > 0 && _showHeaderSeparator; }
        }

        private bool FooterSeparatorVisible
        {
            get { return FooterHeight > 0 && _showFooterSeparator; }
        }

        /// <summary>
        /// 자식 컨트롤이 놓이는 본문 영역. 머리글·바닥글 높이를 뺀 가운데다.
        /// Dock/Anchor가 이 값을 기준으로 계산되므로 반드시 재정의해야 한다.
        /// </summary>
        public override Rectangle DisplayRectangle
        {
            get
            {
                var frame = FrameBounds;
                int bw = EffectiveBorderWidth;

                int top = frame.Top + bw + HeaderHeight + (HeaderSeparatorVisible ? bw : 0);
                int bottomInset = bw + FooterHeight + (FooterSeparatorVisible ? bw : 0);

                return new Rectangle(
                    frame.Left + bw + Padding.Left,
                    top + Padding.Top,
                    Math.Max(0, frame.Width - bw * 2 - Padding.Horizontal),
                    Math.Max(0, frame.Bottom - top - bottomInset - Padding.Vertical));
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
            var textColor = Enabled ? theme.Text : theme.TextDisabled;

            int headerH = HeaderHeight;
            if (headerH > 0)
            {
                var rect = new Rectangle(
                    bounds.Left + bw + PadH, bounds.Top + bw + PadV,
                    Math.Max(0, bounds.Width - bw * 2 - PadH * 2), Font.Height);
                TextRenderer.DrawText(g, _headerText, Font, rect, textColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                if (HeaderSeparatorVisible)
                {
                    int y = bounds.Top + bw + headerH;
                    using (var pen = new Pen(theme.Border, bw))
                        g.DrawLine(pen, bounds.Left + bw, y, bounds.Right - bw, y);
                }
            }

            int footerH = FooterHeight;
            if (footerH > 0)
            {
                var rect = new Rectangle(
                    bounds.Left + bw + PadH, bounds.Bottom - bw - footerH + PadV,
                    Math.Max(0, bounds.Width - bw * 2 - PadH * 2), Font.Height);
                TextRenderer.DrawText(g, _footerText, Font, rect, Enabled ? theme.TextMuted : theme.TextDisabled,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                if (FooterSeparatorVisible)
                {
                    int y = bounds.Bottom - bw - footerH;
                    using (var pen = new Pen(theme.Border, bw))
                        g.DrawLine(pen, bounds.Left + bw, y, bounds.Right - bw, y);
                }
            }

            base.OnPaint(e);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            PerformLayout();
            Invalidate();
            base.OnFontChanged(e);
        }

        protected override void OnThemeChanged()
        {
            PerformLayout();
            base.OnThemeChanged();
        }
    }

    /// <summary>AdvCard가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvCardOptions : AdvOptions
    {
        private readonly AdvCard _owner;

        internal AdvCardOptions(AdvCard owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(true)]
        [Description("머리글 아래에 구분선을 그릴지 여부입니다.")]
        public bool ShowHeaderSeparator
        {
            get { return _owner.ShowHeaderSeparator; }
            set { _owner.ShowHeaderSeparator = value; }
        }

        [DefaultValue(true)]
        [Description("바닥글 위에 구분선을 그릴지 여부입니다.")]
        public bool ShowFooterSeparator
        {
            get { return _owner.ShowFooterSeparator; }
            set { _owner.ShowFooterSeparator = value; }
        }
    }
}
