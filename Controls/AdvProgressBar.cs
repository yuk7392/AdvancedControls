using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Animation;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>
    /// 테마를 따르는 진행 막대. 값이 바뀌면 부드럽게 채워진다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultProperty("Value")]
    [Description("테마를 따르는 진행 막대입니다.")]
    public class AdvProgressBar : AdvControlBase
    {
        private int _minimum;
        private int _maximum = 100;
        private int _value;
        private bool _showPercentage;
        private readonly AdvAnimator _fillAnim;
        private AdvProgressBarOptions _options;

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvProgressBarOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvProgressBarOptions(this)); }
        }

        [Category("Behavior")]
        [Description("Value가 바뀔 때 발생합니다.")]
        public event EventHandler ValueChanged;

        public AdvProgressBar()
        {
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;

            _fillAnim = new AdvAnimator(0);
            _fillAnim.ValueChanged += OnFillTick;
        }

        protected override Size DefaultSize
        {
            get { return new Size(220, 14); }
        }

        [Category("Behavior")]
        [DefaultValue(0)]
        [Description("최솟값입니다.")]
        public int Minimum
        {
            get { return _minimum; }
            set
            {
                if (_minimum == value) return;
                _minimum = value;

                // 최솟값이 최댓값을 넘으면 비율 계산이 음수가 된다
                if (_maximum < _minimum) _maximum = _minimum;
                Value = _value;      // 세터가 범위로 잘라 준다
                SyncFill(false);
            }
        }

        [Category("Behavior")]
        [DefaultValue(100)]
        [Description("최댓값입니다.")]
        public int Maximum
        {
            get { return _maximum; }
            set
            {
                if (_maximum == value) return;
                _maximum = value;

                if (_minimum > _maximum) _minimum = _maximum;
                Value = _value;
                SyncFill(false);
            }
        }

        [Category("Behavior")]
        [DefaultValue(0)]
        [Description("현재 값입니다. 최솟값~최댓값 범위로 잘립니다.")]
        public int Value
        {
            get { return _value; }
            set
            {
                if (value < _minimum) value = _minimum;
                if (value > _maximum) value = _maximum;
                if (_value == value) return;

                _value = value;
                SyncFill(true);

                var handler = ValueChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("가운데에 퍼센트를 표시할지 여부입니다.")]
        public bool ShowPercentage
        {
            get { return _showPercentage; }
            set
            {
                if (_showPercentage == value) return;
                _showPercentage = value;
                Invalidate();
            }
        }

        /// <summary>0~1 사이의 진행 비율.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float Ratio
        {
            get
            {
                int span = _maximum - _minimum;
                return span <= 0 ? 0f : (float)(_value - _minimum) / span;
            }
        }

        private void SyncFill(bool animate)
        {
            _fillAnim.Duration = EffectiveTransitionDuration;

            if (animate) _fillAnim.AnimateTo(Ratio);
            else _fillAnim.SetImmediate(Ratio);

            Invalidate();
        }

        private void OnFillTick(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SyncFill(false);
        }

        protected override void OnThemeChanged()
        {
            _fillAnim.Duration = EffectiveTransitionDuration;
            base.OnThemeChanged();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = FrameBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            // 막대는 기본적으로 완전히 둥근 모양이 자연스럽다
            var corners = EffectiveCorners;
            if (corners.FollowsTheme || corners.Max <= 0)
                corners = new AdvCorners(bounds.Height / 2);

            int bw = EffectiveBorderWidth;

            AdvFrameRenderer.Draw(g, bounds, theme, corners, bw,
                                  Enabled ? theme.SurfacePressed : theme.DisabledFill,
                                  Color.Empty, Color.Transparent, null, CurrentElevation);

            float ratio = _fillAnim.Eased;
            var track = AdvGraphics.Deflate(bounds, bw);

            int fillWidth = 0;
            if (ratio > 0f && track.Width > 0)
            {
                fillWidth = (int)Math.Round(track.Width * ratio);

                // 폭이 반경보다 작으면 둥근 끝이 뭉개지므로 최소한 높이만큼은 확보한다
                if (fillWidth > 0 && fillWidth < track.Height) fillWidth = track.Height;
            }

            if (fillWidth > 0)
            {
                var fillRect = new Rectangle(track.X, track.Y, fillWidth, track.Height);

                using (var path = AdvGraphics.CreateRoundedRect(fillRect, corners))
                using (var brush = AdvGraphics.CreateFillBrush(
                           fillRect,
                           Enabled ? theme.Accent : theme.TextDisabled,
                           Enabled ? theme.AccentGradientEnd : Color.Empty,
                           theme.GradientAngle))
                {
                    g.FillPath(brush, path);
                }
            }

            if (_showPercentage && bounds.Height >= Font.Height)
                DrawPercentage(g, bounds, track.X + fillWidth, theme);

            base.OnPaint(e);
        }

        /// <summary>
        /// 가운데 정렬한 글자는 채움이 절반쯤일 때 반드시 경계를 가로지른다.
        /// 비율 하나로 색을 고르면 글자의 한쪽이 배경과 같은 색이 되어 묻히므로
        /// (50%에서 명암비 1.24:1로 측정됨) 채움 쪽과 트랙 쪽을 각각 클립해 두 번 그린다.
        ///
        /// 이 컨트롤만 TextRenderer가 아니라 Graphics.DrawString을 쓴다.
        /// TextRenderer는 GDI를 직접 호출해 Graphics.Clip을 무시하므로
        /// (100% 채움에서 흰 글자가 통째로 사라지는 것으로 확인) 클리핑이 먹지 않는다.
        /// </summary>
        private void DrawPercentage(Graphics g, Rectangle bounds, int fillEdge, AdvTheme theme)
        {
            string text = (int)Math.Round(Ratio * 100) + "%";

            var saved = g.Clip;
            var savedHint = g.TextRenderingHint;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            try
            {
                using (var fmt = new StringFormat(StringFormatFlags.NoWrap))
                {
                    fmt.Alignment = StringAlignment.Center;
                    fmt.LineAlignment = StringAlignment.Center;

                    if (!Enabled)
                    {
                        using (var b = new SolidBrush(theme.TextDisabled))
                            g.DrawString(text, Font, b, bounds, fmt);
                        return;
                    }

                    int edge = fillEdge;
                    if (edge < bounds.Left) edge = bounds.Left;
                    if (edge > bounds.Right) edge = bounds.Right;

                    if (edge > bounds.Left)
                    {
                        g.SetClip(Rectangle.FromLTRB(bounds.Left, bounds.Top, edge, bounds.Bottom));
                        using (var b = new SolidBrush(theme.OnAccent))
                            g.DrawString(text, Font, b, bounds, fmt);
                    }

                    if (edge < bounds.Right)
                    {
                        g.SetClip(Rectangle.FromLTRB(edge, bounds.Top, bounds.Right, bounds.Bottom));
                        using (var b = new SolidBrush(theme.Text))
                            g.DrawString(text, Font, b, bounds, fmt);
                    }
                }
            }
            finally
            {
                g.Clip = saved;
                saved.Dispose();
                g.TextRenderingHint = savedHint;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fillAnim.ValueChanged -= OnFillTick;
                _fillAnim.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>AdvProgressBar가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvProgressBarOptions : AdvOptions
    {
        private readonly AdvProgressBar _owner;

        internal AdvProgressBarOptions(AdvProgressBar owner) : base(owner.Styling)
        {
            _owner = owner;
        }

        [DefaultValue(false)]
        [Description("가운데에 퍼센트를 표시할지 여부입니다.")]
        public bool ShowPercentage
        {
            get { return _owner.ShowPercentage; }
            set { _owner.ShowPercentage = value; }
        }
    }
}
