using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    [ToolboxItem(true)]
    [DefaultEvent("CheckedChanged")]
    [DefaultProperty("Checked")]
    [Description("테마를 따르는 라디오 버튼입니다. 같은 부모 안의 라디오끼리 한 그룹이 됩니다.")]
    public class AdvRadioButton : AdvToggleBase
    {
        /// <summary>이미 켜져 있으면 다시 눌러도 꺼지지 않는다. 표준 RadioButton과 같다.</summary>
        protected override void Toggle()
        {
            if (Checked) return;
            Checked = true;
        }

        protected override void OnCheckedChanged(System.EventArgs e)
        {
            if (Checked) UncheckSiblings();
            base.OnCheckedChanged(e);
        }

        /// <summary>
        /// 같은 부모에 있는 다른 라디오를 끈다. 형제의 CheckedChanged도 함께 발생시켜
        /// 상태를 구독하는 쪽이 꺼짐을 놓치지 않게 한다.
        /// </summary>
        private void UncheckSiblings()
        {
            if (Parent == null) return;

            foreach (Control c in Parent.Controls)
            {
                var radio = c as AdvRadioButton;
                if (radio == null || ReferenceEquals(radio, this) || !radio.Checked) continue;

                // Checked 세터를 쓰면 이 메서드가 다시 불려 재귀가 되므로 코어를 직접 쓴다
                radio.SetCheckedCore(false);
                radio.RaiseCheckedChanged();
            }
        }

        private void RaiseCheckedChanged()
        {
            base.OnCheckedChanged(System.EventArgs.Empty);
        }

        protected override AdvCorners GlyphCorners(AdvTheme theme)
        {
            return new AdvCorners(GlyphSize / 2);   // 원
        }

        protected override void DrawGlyph(Graphics g, Rectangle glyph, AdvTheme theme,
                                          Color fill, Color border, Color mark)
        {
            int bw = EffectiveBorderWidth;
            var inner = AdvGraphics.Deflate(glyph, bw);

            using (var brush = new SolidBrush(fill))
                g.FillEllipse(brush, inner);

            if (bw > 0)
            {
                using (var pen = new Pen(border, bw))
                    g.DrawEllipse(pen, inner);
            }

            // 가운데 점이 진행도에 따라 커진다.
            // 바깥 원과 같은 inner 기준으로 잡아야 중심이 어긋나지 않는다.
            float t = CheckAmount;
            if (t <= 0f) return;

            float full = inner.Width * 0.34f;
            float d = full * t;
            var dot = new RectangleF(
                inner.X + (inner.Width - d) / 2f,
                inner.Y + (inner.Height - d) / 2f,
                d, d);

            using (var brush = new SolidBrush(mark))
                g.FillEllipse(brush, dot);
        }
    }
}
