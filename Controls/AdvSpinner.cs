using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Animation;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>스피너 종류.</summary>
    public enum AdvSpinnerStyle
    {
        /// <summary>회전하는 원호(테두리) 스피너.</summary>
        Border,
        /// <summary>커졌다 사라지기를 반복하는 점(grow) 스피너.</summary>
        Grow
    }

    /// <summary>
    /// 로딩 중임을 나타내는 무한 회전/맥동 인디케이터. Bootstrap의 <c>.spinner-border</c>·
    /// <c>.spinner-grow</c>에 대응한다. <see cref="AdvAnimator"/>의 loop 모드로 돈다.
    /// </summary>
    [ToolboxItem(true)]
    [Description("로딩 스피너입니다.")]
    public class AdvSpinner : AdvControlBase
    {
        private readonly AdvAnimator _anim;
        private AdvContextColor _context = AdvContextColor.Primary;
        private AdvSpinnerStyle _style = AdvSpinnerStyle.Border;
        private int _thickness;          // 0이면 크기에서 자동 계산
        private int _periodMs = 800;
        private AdvSpinnerOptions _options;
        private Pen _pen;             // 매 틱 재생성을 피하려 캐싱한다(색·두께만 갱신)
        private SolidBrush _brush;    // Grow 스타일용 캐시 브러시(색만 갱신)

        public AdvSpinner()
        {
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            _anim = new AdvAnimator(0);
            _anim.ValueChanged += OnAnimTick;
        }

        protected override Size DefaultSize
        {
            get { return new Size(32, 32); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvContextColor.Primary)]
        [Description("스피너의 컨텍스트 색입니다.")]
        public AdvContextColor Context
        {
            get { return _context; }
            set { if (_context == value) return; _context = value; Invalidate(); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvSpinnerStyle.Border)]
        [Description("스피너 종류입니다.")]
        public AdvSpinnerStyle Style
        {
            get { return _style; }
            set { if (_style == value) return; _style = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(0)]
        [Description("테두리 스피너의 선 두께입니다. 0이면 크기에서 자동 계산합니다.")]
        public int Thickness
        {
            get { return _thickness; }
            set { value = Math.Max(0, value); if (_thickness == value) return; _thickness = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(800)]
        [Description("한 바퀴(또는 한 맥동)에 걸리는 시간(ms)입니다.")]
        public int PeriodMs
        {
            get { return _periodMs; }
            set
            {
                value = Math.Max(100, value);
                if (_periodMs == value) return;
                _periodMs = value;
                if (_anim.IsLooping) _anim.StartLoop(_periodMs);
            }
        }

        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvSpinnerOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvSpinnerOptions(this)); }
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            if (!IsDisposed && IsHandleCreated) Invalidate();
        }

        private void UpdateSpin()
        {
            bool shouldRun = !DesignMode && IsHandleCreated && Visible;
            if (shouldRun && !_anim.IsLooping) _anim.StartLoop(_periodMs);
            else if (!shouldRun && _anim.IsLooping) _anim.StopLoop();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateSpin();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdateSpin();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var palette = EffectiveTheme.ResolveContext(_context);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var b = FrameBounds;
            int side = Math.Min(b.Width, b.Height);
            if (side <= 2) return;

            var sq = new Rectangle(b.Left + (b.Width - side) / 2, b.Top + (b.Height - side) / 2, side, side);
            float phase = _anim.Value;   // 0~1 선형 위상

            if (_style == AdvSpinnerStyle.Border)
            {
                int th = _thickness > 0 ? _thickness : Math.Max(2, side / 8);
                var arc = Rectangle.Inflate(sq, -th, -th);
                if (arc.Width <= 0 || arc.Height <= 0) return;

                if (_pen == null)
                    _pen = new Pen(palette.Solid, th) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                else
                {
                    _pen.Color = palette.Solid;
                    _pen.Width = th;
                }
                g.DrawArc(_pen, arc.X, arc.Y, arc.Width, arc.Height, phase * 360f, 270f);
            }
            else // Grow
            {
                int r = (int)((side / 2 - 1) * phase);
                int alpha = (int)(255 * (1f - phase));
                if (alpha < 0) alpha = 0; else if (alpha > 255) alpha = 255;
                if (r <= 0) return;

                int cx = sq.Left + side / 2, cy = sq.Top + side / 2;
                var c = Color.FromArgb(alpha, palette.Solid);
                if (_brush == null) _brush = new SolidBrush(c);
                else _brush.Color = c;
                g.FillEllipse(_brush, cx - r, cy - r, r * 2, r * 2);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _anim.ValueChanged -= OnAnimTick;
                _anim.Dispose();
                if (_pen != null) { _pen.Dispose(); _pen = null; }
                if (_brush != null) { _brush.Dispose(); _brush = null; }
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>AdvSpinner가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvSpinnerOptions : AdvOptions
    {
        private readonly AdvSpinner _owner;

        internal AdvSpinnerOptions(AdvSpinner owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(AdvContextColor.Primary)]
        [Description("스피너의 컨텍스트 색입니다.")]
        public AdvContextColor Context
        {
            get { return _owner.Context; }
            set { _owner.Context = value; }
        }

        [DefaultValue(AdvSpinnerStyle.Border)]
        [Description("스피너 종류입니다.")]
        public AdvSpinnerStyle Style
        {
            get { return _owner.Style; }
            set { _owner.Style = value; }
        }
    }
}
