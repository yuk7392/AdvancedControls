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
    [DefaultProperty("AdvancedControlOptions")]
    [DefaultEvent("ValueChanged")]
    [Description("테마를 따르는 진행 막대입니다.")]
    public class AdvProgressBar : AdvControlBase
    {
        private int _minimum;
        private int _maximum = 100;
        private int _value;
        private bool _showPercentage;
        private Color _context = Color.Empty;
        private bool _striped;
        private bool _stripeAnimated = true;
        private bool _indeterminate;
        private readonly AdvAnimator _fillAnim;
        private readonly AdvAnimator _stripeAnim;
        private readonly AdvAnimator _indetAnim;
        private AdvProgressBarOptions _options;
        private SolidBrush _stripeBrush;                      // 빗금용 캐시 브러시(알파 고정)
        private readonly Point[] _stripePts = new Point[4];   // 빗금 폴리곤 좌표 재사용 버퍼

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

            _stripeAnim = new AdvAnimator(0);
            _stripeAnim.ValueChanged += OnStripeTick;

            _indetAnim = new AdvAnimator(0);
            _indetAnim.ValueChanged += OnStripeTick;   // 같은 재그리기 핸들러를 쓴다
        }

        protected override Size DefaultSize
        {
            get { return new Size(220, 14); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
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

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
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

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
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

        [Browsable(false)]
        [Description("진행 막대의 강조 색입니다. 비워 두면 테마 강조색(Accent)을 따릅니다.")]
        public Color Context
        {
            get { return _context; }
            set { if (_context == value) return; _context = value; Invalidate(); }
        }
        public bool ShouldSerializeContext() { return !_context.IsEmpty; }
        public void ResetContext() { Context = Color.Empty; }

        [Browsable(false)]
        [DefaultValue(false)]
        [Description("채움에 빗금 무늬를 넣을지 여부입니다.")]
        public bool Striped
        {
            get { return _striped; }
            set { if (_striped == value) return; _striped = value; UpdateStripeAnim(); Invalidate(); }
        }

        [Browsable(false)]
        [DefaultValue(true)]
        [Description("빗금이 흐르는 애니메이션을 켤지 여부입니다. Striped가 켜져 있을 때만 적용됩니다.")]
        public bool StripeAnimated
        {
            get { return _stripeAnimated; }
            set { if (_stripeAnimated == value) return; _stripeAnimated = value; UpdateStripeAnim(); Invalidate(); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("진행률을 알 수 없을 때 좌우로 흐르는 블록을 표시합니다. 켜면 Value 대신 애니메이션으로 표시합니다.")]
        public bool Indeterminate
        {
            get { return _indeterminate; }
            set { if (_indeterminate == value) return; _indeterminate = value; UpdateIndetAnim(); Invalidate(); }
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
            _fillAnim.Easing = EffectiveEasing;

            if (animate) _fillAnim.AnimateTo(Ratio);
            else _fillAnim.SetImmediate(Ratio);

            Invalidate();
        }

        private void OnFillTick(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            Invalidate();
        }

        private void OnStripeTick(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            Invalidate();
        }

        private void UpdateStripeAnim()
        {
            bool run = !DesignMode && IsHandleCreated && Visible && _striped && _stripeAnimated;
            if (run && !_stripeAnim.IsLooping) _stripeAnim.StartLoop(1000);
            else if (!run && _stripeAnim.IsLooping) _stripeAnim.StopLoop();
        }

        private void UpdateIndetAnim()
        {
            bool run = !DesignMode && IsHandleCreated && Visible && _indeterminate;
            if (run && !_indetAnim.IsLooping) _indetAnim.StartLoop(1200);
            else if (!run && _indetAnim.IsLooping) _indetAnim.StopLoop();
        }

        /// <summary>진행률을 모를 때 좌우로 흐르는 블록을 둥근 트랙 안에 클립해 그린다.</summary>
        private void DrawIndeterminate(Graphics g, Rectangle track, AdvCorners corners,
                                       AdvContextPalette palette, AdvTheme theme)
        {
            if (track.Width <= 0 || track.Height <= 0) return;

            float phase = DesignMode ? 0.35f : _indetAnim.Value;   // 0~1
            int blockW = Math.Max(track.Height, (int)(track.Width * 0.35f));
            int travel = track.Width + blockW;
            int x = track.Left - blockW + (int)(phase * travel);
            var block = new Rectangle(x, track.Y, blockW, track.Height);

            using (var trackPath = AdvGraphics.CreateRoundedRect(track, corners))
            {
                var state = g.Save();
                g.SetClip(trackPath);
                using (var bpath = AdvGraphics.CreateRoundedRect(block, corners))
                using (var brush = new SolidBrush(Enabled ? palette.Solid : theme.TextDisabled))
                    g.FillPath(brush, bpath);
                g.Restore(state);
            }
        }

        /// <summary>채움 위에 45° 반투명 빗금을 그린다. loop 위상만큼 옆으로 흐른다.</summary>
        private void DrawStripes(Graphics g, Rectangle fillRect)
        {
            int sw = Math.Max(6, fillRect.Height);           // 빗금 폭
            int period = sw * 2;
            int offset = (int)(_stripeAnim.Value * period);   // 0~period 흐름
            int h = fillRect.Height;

            // 라이트/다크 무관하게 고정된 반투명 흰색(rgba(255,255,255,.15))으로 빗금을 얹는다.
            if (_stripeBrush == null)
                _stripeBrush = new SolidBrush(Color.FromArgb(38, 255, 255, 255));

            for (int x = fillRect.Left - h - period + offset; x < fillRect.Right + period; x += period)
            {
                _stripePts[0] = new Point(x, fillRect.Bottom);
                _stripePts[1] = new Point(x + h, fillRect.Top);
                _stripePts[2] = new Point(x + h + sw, fillRect.Top);
                _stripePts[3] = new Point(x + sw, fillRect.Bottom);
                g.FillPolygon(_stripeBrush, _stripePts);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SyncFill(false);
            UpdateStripeAnim();
            UpdateIndetAnim();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdateStripeAnim();
            UpdateIndetAnim();
        }

        protected override void OnThemeChanged()
        {
            _fillAnim.Duration = EffectiveTransitionDuration;
            _fillAnim.Easing = EffectiveEasing;
            base.OnThemeChanged();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var palette = AdvContextPalette.Resolve(_context, theme);
            bool neutral = _context.IsEmpty;
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

            var track = AdvGraphics.Deflate(bounds, bw);

            // 진행률을 모르면 값 채움 대신 흐르는 블록을 그린다(퍼센트도 표시 안 함)
            if (_indeterminate)
            {
                DrawIndeterminate(g, track, corners, palette, theme);
                base.OnPaint(e);
                return;
            }

            float ratio = _fillAnim.Eased;

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
                {
                    using (var brush = AdvGraphics.CreateFillBrush(
                               fillRect,
                               Enabled ? palette.Solid : theme.TextDisabled,
                               Enabled && neutral ? theme.AccentGradientEnd : Color.Empty,
                               EffectiveGradientAngle))
                    {
                        g.FillPath(brush, path);
                    }

                    if (_striped && Enabled)
                    {
                        var state = g.Save();
                        g.SetClip(path);
                        DrawStripes(g, fillRect);
                        g.Restore(state);
                    }
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
                        using (var b = new SolidBrush(AdvContextPalette.Resolve(_context, theme).OnSolid))
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
                _stripeAnim.ValueChanged -= OnStripeTick;
                _stripeAnim.Dispose();
                _indetAnim.ValueChanged -= OnStripeTick;
                _indetAnim.Dispose();
                if (_stripeBrush != null) { _stripeBrush.Dispose(); _stripeBrush = null; }
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>AdvProgressBar가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvProgressBarOptions : AdvOptions
    {
        private readonly AdvProgressBar _owner;

        internal AdvProgressBarOptions(AdvProgressBar owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(0)]
        [Description("최솟값입니다.")]
        public int Minimum
        {
            get { return _owner.Minimum; }
            set { _owner.Minimum = value; }
        }

        [DefaultValue(100)]
        [Description("최댓값입니다.")]
        public int Maximum
        {
            get { return _owner.Maximum; }
            set { _owner.Maximum = value; }
        }

        [DefaultValue(0)]
        [Description("현재 값입니다. 최솟값~최댓값 범위로 잘립니다.")]
        public int Value
        {
            get { return _owner.Value; }
            set { _owner.Value = value; }
        }

        [DefaultValue(false)]
        [Description("가운데에 퍼센트를 표시할지 여부입니다.")]
        public bool ShowPercentage
        {
            get { return _owner.ShowPercentage; }
            set { _owner.ShowPercentage = value; }
        }

        [Description("진행 막대의 강조 색입니다. 비워 두면 테마 강조색(Accent)을 따릅니다.")]
        public Color Context
        {
            get { return _owner.Context; }
            set { _owner.Context = value; }
        }
        public bool ShouldSerializeContext() { return _owner.ShouldSerializeContext(); }
        public void ResetContext() { _owner.ResetContext(); }

        [DefaultValue(false)]
        [Description("채움에 빗금 무늬를 넣을지 여부입니다.")]
        public bool Striped
        {
            get { return _owner.Striped; }
            set { _owner.Striped = value; }
        }

        [DefaultValue(true)]
        [Description("빗금이 흐르는 애니메이션을 켤지 여부입니다.")]
        public bool StripeAnimated
        {
            get { return _owner.StripeAnimated; }
            set { _owner.StripeAnimated = value; }
        }

        [DefaultValue(false)]
        [Description("진행률을 알 수 없을 때 좌우로 흐르는 블록을 표시합니다.")]
        public bool Indeterminate
        {
            get { return _owner.Indeterminate; }
            set { _owner.Indeterminate = value; }
        }
    }
}
