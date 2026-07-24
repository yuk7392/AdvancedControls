using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    [ToolboxItem(true)]
    [DefaultEvent("CheckedChanged")]
    [DefaultProperty("AdvancedControlOptions")]
    [Description("테마를 따르는 체크박스입니다.")]
    public class AdvCheckBox : AdvToggleBase
    {
        private bool _threeState;
        private CheckState _checkState = CheckState.Unchecked;
        private AdvCheckBoxOptions _options;

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("눌렀을 때 '설정 안 함' 상태를 거칠지 여부입니다.")]
        public bool ThreeState
        {
            get { return _threeState; }
            set
            {
                if (_threeState == value) return;
                _threeState = value;

                // 세 번째 상태를 끄는데 지금 그 상태면 갈 곳이 없다
                if (!_threeState && _checkState == CheckState.Indeterminate)
                    CheckState = CheckState.Checked;
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(CheckState.Unchecked)]
        [Description("체크 상태입니다. Indeterminate는 '설정 안 함'입니다.")]
        public CheckState CheckState
        {
            get { return _checkState; }
            set
            {
                if (_checkState == value) return;

                _checkState = value;

                // 도형 채우기는 Unchecked가 아닌 두 상태에서 모두 켜진다
                bool on = value != CheckState.Unchecked;
                if (base.Checked != on) base.Checked = on;
                else Invalidate();

                OnCheckStateChanged(EventArgs.Empty);
            }
        }

        [Category("Behavior")]
        [Description("CheckState가 바뀔 때 발생합니다.")]
        public event EventHandler CheckStateChanged;

        protected virtual void OnCheckStateChanged(EventArgs e)
        {
            var handler = CheckStateChanged;
            if (handler != null) handler(this, e);
        }

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new AdvCheckBoxOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvCheckBoxOptions(this)); }
        }

        /// <summary>
        /// Checked를 직접 건드렸을 때도 CheckState가 어긋나지 않게 맞춘다.
        /// 둘이 따로 놀면 화면과 값이 달라진다.
        /// </summary>
        protected override void OnCheckedChanged(EventArgs e)
        {
            if (Checked && _checkState == CheckState.Unchecked) _checkState = CheckState.Checked;
            else if (!Checked && _checkState != CheckState.Unchecked) _checkState = CheckState.Unchecked;

            base.OnCheckedChanged(e);
        }

        protected override void Toggle()
        {
            if (!_threeState)
            {
                Checked = !Checked;
                return;
            }

            switch (_checkState)
            {
                case CheckState.Unchecked: CheckState = CheckState.Checked; break;
                case CheckState.Checked: CheckState = CheckState.Indeterminate; break;
                default: CheckState = CheckState.Unchecked; break;
            }
        }

        protected override void DrawGlyph(Graphics g, Rectangle glyph, AdvTheme theme,
                                          Color fill, Color border, Color mark)
        {
            var corners = GlyphCorners(theme);
            int bw = EffectiveBorderWidth;
            var inner = AdvGraphics.Deflate(glyph, bw);

            using (var path = AdvGraphics.CreateRoundedRect(inner, corners))
            {
                using (var brush = new SolidBrush(fill))
                    g.FillPath(brush, path);

                if (bw > 0)
                {
                    using (var pen = new Pen(border, bw))
                        g.DrawPath(pen, path);
                }
            }

            // 표시는 테두리를 뺀 실제 상자(inner) 기준으로 그린다.
            // 축소 전 glyph를 쓰면 테두리 두께의 절반만큼 오른쪽·아래로 밀린다.
            if (_checkState == CheckState.Indeterminate)
                DrawDash(g, inner, mark, CheckAmount);
            else
                DrawCheckMark(g, inner, mark, CheckAmount);
        }

        /// <summary>'설정 안 함' 표시. 가운데에서 좌우로 자라나는 가로줄이다.</summary>
        private static void DrawDash(Graphics g, Rectangle glyph, Color color, float amount)
        {
            if (amount <= 0f) return;

            float full = glyph.Width * 0.44f;
            float half = full * amount / 2f;
            float cx = glyph.X + glyph.Width / 2f;
            float cy = glyph.Y + glyph.Height / 2f;

            using (var pen = new Pen(color, 2f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx - half, cy, cx + half, cy);
            }
        }

        /// <summary>
        /// 체크 표시를 두 획으로 그린다. amount에 따라 왼쪽 획부터 순서대로 자라난다.
        /// </summary>
        private static void DrawCheckMark(Graphics g, Rectangle glyph, Color color, float amount)
        {
            if (amount <= 0f) return;

            // 도형 크기와 무관하게 같은 비율로 보이도록 상대 좌표로 잡는다.
            // 가로 0.24~0.76, 세로 0.32~0.68로 잡아 두 축 모두 중심이 정확히 0.5가 되게 한다.
            float w = glyph.Width, h = glyph.Height;
            var p0 = new PointF(glyph.X + w * 0.24f, glyph.Y + h * 0.50f);
            var p1 = new PointF(glyph.X + w * 0.42f, glyph.Y + h * 0.68f);
            var p2 = new PointF(glyph.X + w * 0.76f, glyph.Y + h * 0.32f);

            // 두 획의 길이 비율에 맞춰 진행도를 나눈다 (짧은 획 0.2546 / 전체 0.7498)
            const float firstLegRatio = 0.34f;

            using (var pen = new Pen(color, 2f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                if (amount <= firstLegRatio)
                {
                    float t = amount / firstLegRatio;
                    g.DrawLine(pen, p0, Lerp(p0, p1, t));
                }
                else
                {
                    float t = (amount - firstLegRatio) / (1f - firstLegRatio);
                    g.DrawLine(pen, p0, p1);
                    g.DrawLine(pen, p1, Lerp(p1, p2, t));
                }
            }
        }

        private static PointF Lerp(PointF a, PointF b, float t)
        {
            return new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }

        /// <summary>3-상태 체크박스는 '설정 안 함'을 접근성 Mixed 상태로 알린다.</summary>
        protected override AccessibleStates AccessibleCheckedState
        {
            get
            {
                switch (_checkState)
                {
                    case CheckState.Checked: return AccessibleStates.Checked;
                    case CheckState.Indeterminate: return AccessibleStates.Mixed;
                    default: return AccessibleStates.None;
                }
            }
        }
    }

    /// <summary>AdvCheckBox가 고유하게 추가한 속성. 공통 속성은 AdvToggleOptions에서 물려받는다.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvCheckBoxOptions : AdvToggleOptions
    {
        private readonly AdvCheckBox _owner;

        internal AdvCheckBoxOptions(AdvCheckBox owner) : base(owner)
        {
            _owner = owner;
        }

        [DefaultValue(false)]
        [Description("눌렀을 때 '설정 안 함' 상태를 거칠지 여부입니다.")]
        public bool ThreeState
        {
            get { return _owner.ThreeState; }
            set { _owner.ThreeState = value; }
        }

        [DefaultValue(CheckState.Unchecked)]
        [Description("체크 상태입니다. Indeterminate는 '설정 안 함'입니다.")]
        public CheckState CheckState
        {
            get { return _owner.CheckState; }
            set { _owner.CheckState = value; }
        }
    }
}
