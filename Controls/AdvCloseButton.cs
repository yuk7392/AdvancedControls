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
    /// 작은 X 아이콘 버튼. 알림·모달·토스트 등을 닫는 데 쓴다.
    /// 평소에는 흐린 X만 보이고 호버 시 X가 진해지며 옅은 원형 배경이 생긴다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("Click")]
    [Description("무언가를 닫는 데 쓰는 작은 X 버튼입니다.")]
    public class AdvCloseButton : AdvControlBase
    {
        private AdvCloseButtonOptions _options;

        public AdvCloseButton()
        {
            TabStop = true;
        }

        protected override Size DefaultSize
        {
            // 글로우 여백(각 변 최대 3px)을 감안한 클릭 영역
            get { return new Size(24, 24); }
        }

        /// <summary>손 모양 커서·호버 상태를 쓰는 클릭 컨트롤이다.</summary>
        protected override bool IsClickable
        {
            get { return true; }
        }

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvCloseButtonOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvCloseButtonOptions(this)); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = FrameBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            float t = HoverAmount;

            // 호버 시 옅은 원형 배경이 서서히 나타난다(투명 → SurfaceHover 보간).
            if (Enabled && t > 0f)
            {
                var bg = AdvGraphics.Blend(Color.Transparent, theme.SurfaceHover, t);
                using (var brush = new SolidBrush(bg))
                    g.FillEllipse(brush, bounds);
            }

            // 포커스가 있으면 얇은 링으로 표시한다(키보드 접근성).
            if (Enabled && ShowsFocusVisual && ShowFocusCues)
            {
                using (var pen = new Pen(theme.FocusRing, 1f))
                    g.DrawEllipse(pen, bounds.Left, bounds.Top, bounds.Width - 1, bounds.Height - 1);
            }

            // X 글리프는 프레임 안쪽에 정사각형으로 배치한다.
            int side = Math.Min(bounds.Width, bounds.Height);
            int glyph = Math.Max(4, (int)(side * 0.42f));
            var gb = new Rectangle(
                bounds.Left + (bounds.Width - glyph) / 2,
                bounds.Top + (bounds.Height - glyph) / 2,
                glyph, glyph);

            Color stroke = !Enabled
                ? theme.TextDisabled
                : (IsPressed ? theme.Text : AdvGraphics.Blend(theme.TextMuted, theme.Text, t));

            float pw = Math.Max(1.5f, side * 0.09f);
            using (var pen = new Pen(stroke, pw))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, gb.Left, gb.Top, gb.Right, gb.Bottom);
                g.DrawLine(pen, gb.Left, gb.Bottom, gb.Right, gb.Top);
            }

            // 소비자가 붙인 Paint 핸들러가 위에 덧그릴 수 있도록 마지막에 호출한다.
            base.OnPaint(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !Focused) Focus();
            base.OnMouseDown(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) IsPressed = true;
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && IsPressed)
            {
                IsPressed = false;
                OnClick(EventArgs.Empty);
            }
            base.OnKeyUp(e);
        }

        /// <summary>포커스가 넘어가면 KeyUp이 오지 않아 눌림 상태가 남는다.</summary>
        protected override void OnLostFocus(EventArgs e)
        {
            IsPressed = false;
            base.OnLostFocus(e);
        }
    }

    /// <summary>AdvCloseButton이 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvCloseButtonOptions : AdvOptions
    {
        private readonly AdvCloseButton _owner;

        internal AdvCloseButtonOptions(AdvCloseButton owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(true)]
        [Description("이 버튼 위에서 손 모양 커서를 보일지 여부입니다.")]
        public bool UseHandCursor
        {
            get { return _owner.UseHandCursor; }
            set { _owner.UseHandCursor = value; }
        }
    }
}
