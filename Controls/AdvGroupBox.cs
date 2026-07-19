using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>
    /// 제목이 붙은 테마 컨테이너. 제목 아래로 구분선을 긋고 그 아래를 내용 영역으로 쓴다.
    /// 제목 글자는 <see cref="Control.Text"/>, 제목의 모양·배치는 <see cref="Header"/>에 있다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultProperty("Text")]
    [Description("제목이 있는 테마 컨테이너입니다.")]
    public class AdvGroupBox : AdvContainerBase
    {
        private readonly AdvHeaderAppearance _header = new AdvHeaderAppearance();

        public AdvGroupBox()
        {
            _header.Changed += HeaderChanged;
            _header.LayoutChanged += HeaderLayoutChanged;
        }

        /// <summary>
        /// 머리글에 표시할 제목.
        /// Panel.Text는 Browsable(false)라 그대로 두면 디자이너에서 제목을 입력할 수 없다.
        /// </summary>
        [Browsable(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        [Category("Appearance")]
        [Description("머리글에 표시할 제목입니다. 모양은 Header에서 조정합니다.")]
        public override string Text
        {
            get { return base.Text; }
            set { base.Text = value; }
        }

        /// <summary>머리글의 글꼴·색·정렬·높이·구분선·여백을 한데 모은 설정.</summary>
        [Category("Appearance")]
        [Description("머리글의 모양과 배치입니다. 제목 글자는 Text 속성에 있습니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public AdvHeaderAppearance Header
        {
            get { return _header; }
        }

        private int HeaderHeight
        {
            get { return _header.ResolveHeight(Font, !string.IsNullOrEmpty(Text)); }
        }

        /// <summary>
        /// 자식 컨트롤이 놓이는 영역. 제목 높이를 뺀 아래쪽이다.
        /// Dock/Anchor가 이 값을 기준으로 계산되므로 반드시 재정의해야 한다.
        /// </summary>
        public override Rectangle DisplayRectangle
        {
            get
            {
                var frame = FrameBounds;
                int bw = EffectiveBorderWidth;

                // 구분선을 그리면 그 두께만큼 더 내려야 자식이 선 위에 겹치지 않는다
                int top = frame.Top + bw + HeaderHeight + (_header.ShowSeparator ? bw : 0);

                return new Rectangle(
                    frame.Left + bw + Padding.Left,
                    top + Padding.Top,
                    Math.Max(0, frame.Width - bw * 2 - Padding.Horizontal),
                    // 세로도 가로처럼 양변을 빼야 한다. Bottom만 빼면 Y에 더한 Top이
                    // 상쇄되지 않아 아래끝이 Padding.Top만큼 프레임 밖으로 넘친다
                    Math.Max(0, frame.Bottom - top - bw - Padding.Vertical));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            var bounds = FrameBounds;

            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            AdvFrameRenderer.Draw(g, bounds, theme, EffectiveCorners, EffectiveBorderWidth,
                                  theme.Surface, theme.SurfaceGradientEnd, theme.Border,
                                  null, CurrentElevation, EffectiveBorderDash);

            int bw = EffectiveBorderWidth;
            int header = HeaderHeight;
            if (header <= 0) { base.OnPaint(e); return; }

            var pad = _header.Padding;
            var titleRect = new Rectangle(
                bounds.Left + bw + pad.Left,
                bounds.Top + bw + pad.Top,
                Math.Max(0, bounds.Width - bw * 2 - pad.Horizontal),
                Math.Max(0, header - pad.Vertical));

            TextRenderer.DrawText(g, Text, _header.ResolveFont(Font), titleRect,
                                  _header.ResolveForeColor(theme, Enabled),
                                  _header.ToTextFlags());

            if (_header.ShowSeparator)
            {
                int y = bounds.Top + bw + header;
                using (var pen = new Pen(theme.Border, bw))
                    g.DrawLine(pen, bounds.Left + bw, y, bounds.Right - bw, y);
            }

            base.OnPaint(e);
        }

        private void HeaderChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        private void HeaderLayoutChanged(object sender, EventArgs e)
        {
            PerformLayout();
            Invalidate();
        }

        /// <summary>제목이 바뀌면 내용 영역 높이가 달라지므로 자식 배치를 다시 계산한다.</summary>
        protected override void OnTextChanged(EventArgs e)
        {
            PerformLayout();
            Invalidate();
            base.OnTextChanged(e);
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _header.Changed -= HeaderChanged;
                _header.LayoutChanged -= HeaderLayoutChanged;
            }
            base.Dispose(disposing);
        }
    }
}
