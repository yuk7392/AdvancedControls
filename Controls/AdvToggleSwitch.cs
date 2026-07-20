using System.ComponentModel;
using System.Drawing;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>
    /// 켬/끔 스위치. 체크박스와 동작은 같고 도형만 다르다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("CheckedChanged")]
    [DefaultProperty("Checked")]
    [Description("테마를 따르는 켬/끔 스위치입니다.")]
    public class AdvToggleSwitch : AdvToggleBase
    {
        /// <summary>손잡이가 트랙 안에서 움직일 여유.</summary>
        private const int KnobInset = 2;

        /// <summary>트랙은 가로로 길다. 세로 길이의 1.8배로 잡는다.</summary>
        protected override int GlyphWidth
        {
            get { return GlyphSize * 9 / 5; }
        }

        /// <summary>스위치는 트랙+손잡이 도형이 핵심이라 버튼형을 지원하지 않는다.</summary>
        protected override bool SupportsButtonStyle
        {
            get { return false; }
        }

        /// <summary>지원하지 않으므로 속성 창에서도 감춰 헷갈리지 않게 한다.</summary>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool ButtonStyle
        {
            get { return false; }
            set { }
        }

        protected override void Toggle()
        {
            Checked = !Checked;
        }

        protected override AdvCorners GlyphCorners(AdvTheme theme)
        {
            return new AdvCorners(GlyphSize / 2);
        }

        /// <summary>
        /// 베이스가 넘겨주는 fill/border/mark는 네모난 체크 상자를 전제로 한 색이라
        /// 스위치에는 맞지 않는다(꺼짐 배경이 흰색이라 흰 손잡이가 묻힌다).
        /// 그래서 트랙 색을 여기서 따로 계산한다.
        /// </summary>
        protected override void DrawGlyph(Graphics g, Rectangle glyph, AdvTheme theme,
                                          Color fill, Color border, Color mark)
        {
            var track = glyph;
            float t = CheckAmount;

            Color trackColor;
            if (!Enabled)
            {
                trackColor = theme.DisabledFill;
            }
            else
            {
                Color on = IsPressed
                         ? theme.AccentPressed
                         : AdvGraphics.Blend(theme.Accent, theme.AccentHover, HoverAmount);

                Color off = AdvGraphics.Blend(theme.SurfacePressed, theme.BorderHover, HoverAmount);

                trackColor = AdvGraphics.Blend(off, on, t);
            }

            using (var path = AdvGraphics.CreateRoundedRect(track, new AdvCorners(track.Height / 2)))
            using (var brush = new SolidBrush(trackColor))
                g.FillPath(brush, path);

            // 손잡이는 진행도에 따라 왼쪽 끝에서 오른쪽 끝으로 미끄러진다
            int diameter = track.Height - KnobInset * 2;
            if (diameter <= 0) return;

            float travel = track.Width - KnobInset * 2 - diameter;
            float x = track.Left + KnobInset + travel * t;

            var knob = new RectangleF(x, track.Top + KnobInset, diameter, diameter);

            // Surface를 쓰면 다크 테마에서 손잡이가 트랙보다 어두워져(1F2937 on 3B82F6)
            // 튀어나온 손잡이가 아니라 파인 구멍처럼 읽힌다. 같은 "켜짐" 의미인 라디오 점과도
            // 규칙이 어긋나므로, 라디오 점과 같은 OnAccent로 통일한다
            using (var brush = new SolidBrush(Enabled ? theme.OnAccent : theme.TextDisabled))
                g.FillEllipse(brush, knob);
        }
    }
}
