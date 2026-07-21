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
    [DefaultProperty("AdvancedControlOptions")]
    [Description("제목이 있는 테마 컨테이너입니다.")]
    public class AdvGroupBox : AdvContainerBase
    {
        private readonly AdvHeaderAppearance _header = new AdvHeaderAppearance();
        private AdvGroupBoxOptions _options;

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvGroupBoxOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvGroupBoxOptions(this)); }
        }

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
        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
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

        /// <summary>머리글이 프레임 밖(캡션)에 있고, 실제로 표시할 제목이 있는지.</summary>
        private bool HeaderOutside
        {
            get { return _header.Placement == AdvHeaderPlacement.Outside && HeaderHeight > 0; }
        }

        /// <summary>
        /// 테두리 상자를 실제로 그릴 영역. Outside 머리글이면 그 높이만큼 위를 비워
        /// 상자가 캡션 아래에서 시작하게 한다. Inside면 프레임 전체다.
        /// </summary>
        private Rectangle GroupFrameBounds
        {
            get
            {
                var f = FrameBounds;
                if (HeaderOutside)
                    return new Rectangle(f.Left, f.Top + HeaderHeight, f.Width,
                                         Math.Max(1, f.Height - HeaderHeight));
                return f;
            }
        }

        /// <summary>
        /// 자식 컨트롤이 놓이는 영역. Inside면 제목 높이를 뺀 아래쪽, Outside면 상자 안쪽 전체다.
        /// Dock/Anchor가 이 값을 기준으로 계산되므로 반드시 재정의해야 한다.
        /// </summary>
        public override Rectangle DisplayRectangle
        {
            get
            {
                var frame = GroupFrameBounds;
                int bw = EffectiveBorderWidth;

                int top;
                if (HeaderOutside)
                {
                    // 머리글이 상자 밖이므로 상자 안쪽 전체가 내용 영역
                    top = frame.Top + bw;
                }
                else
                {
                    int header = HeaderHeight;
                    // 구분선을 그리면 그 두께만큼 더 내려야 자식이 선 위에 겹치지 않는다
                    top = frame.Top + bw + header + (header > 0 && _header.ShowSeparator ? bw : 0);
                }

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
            var frame = GroupFrameBounds;

            if (frame.Width <= 0 || frame.Height <= 0) return;

            AdvFrameRenderer.Draw(g, frame, theme, EffectiveCorners, EffectiveBorderWidth,
                                  theme.Surface, theme.SurfaceGradientEnd, theme.Border,
                                  null, CurrentElevation, EffectiveBorderDash);

            int header = HeaderHeight;
            if (header <= 0) { base.OnPaint(e); return; }

            DrawHeader(g, theme, header);

            base.OnPaint(e);
        }

        /// <summary>
        /// 제목 밴드·글자·구분선을 그린다. 배치(Inside/Outside)와 모양(Plain/Filled)에 따라
        /// 밴드 위치와 배경 유무가 달라진다.
        /// </summary>
        private void DrawHeader(Graphics g, AdvTheme theme, int header)
        {
            int bw = EffectiveBorderWidth;
            var full = FrameBounds;
            bool outside = HeaderOutside;

            // 제목이 놓이는 밴드. Outside면 상자 위 캡션 자리, Inside면 상자 안쪽 최상단.
            Rectangle band = outside
                ? new Rectangle(full.Left, full.Top, full.Width, header)
                : new Rectangle(full.Left + bw, full.Top + bw,
                                Math.Max(0, full.Width - bw * 2), header);

            if (_header.Style == AdvHeaderStyle.Filled)
            {
                // 위쪽 두 모서리만 상단 반경에 맞춰 둥글게, 아래는 직각으로 상자와 잇는다
                var c = EffectiveCorners;
                var bandCorners = new AdvCorners(c.TopLeft, c.TopRight, 0, 0);
                using (var path = AdvGraphics.CreateRoundedRect(band, bandCorners))
                using (var b = new SolidBrush(_header.ResolveFillColor(theme)))
                    g.FillPath(b, path);
            }

            var pad = _header.Padding;
            var titleRect = new Rectangle(
                band.Left + pad.Left,
                band.Top + pad.Top,
                Math.Max(0, band.Width - pad.Horizontal),
                Math.Max(0, band.Height - pad.Vertical));

            TextRenderer.DrawText(g, Text, _header.ResolveFont(Font), titleRect,
                                  _header.ResolveForeColor(theme, Enabled),
                                  _header.ToTextFlags());

            // 구분선은 Inside에서만. Outside는 상자 상단 테두리가 이미 캡션과 내용을 가른다.
            if (_header.ShowSeparator && !outside)
            {
                int y = full.Top + bw + header;
                using (var pen = new Pen(theme.Border, bw))
                    g.DrawLine(pen, full.Left + bw, y, full.Right - bw, y);
            }
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

    /// <summary>AdvGroupBox가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvGroupBoxOptions : AdvOptions
    {
        private readonly AdvGroupBox _owner;

        internal AdvGroupBoxOptions(AdvGroupBox owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [Description("머리글의 모양과 배치입니다. 제목 글자는 Text 속성에 있습니다.")]
        public AdvHeaderAppearance Header
        {
            get { return _owner.Header; }
        }
    }
}
