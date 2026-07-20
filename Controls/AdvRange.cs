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
    /// 트랙 위에서 손잡이를 드래그해 값을 고르는 슬라이더. Bootstrap의 <c>&lt;input type="range"&gt;</c>에
    /// 대응한다. 채움은 컨텍스트 색을 따르고, 드래그·키보드·휠로 값을 바꾼다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("ValueChanged")]
    [DefaultProperty("Value")]
    [Description("드래그로 값을 고르는 슬라이더입니다.")]
    public class AdvRange : AdvControlBase
    {
        private int _minimum;
        private int _maximum = 100;
        private int _value;
        private int _increment = 1;
        private AdvContextColor _context = AdvContextColor.Primary;
        private bool _dragging;
        private AdvRangeOptions _options;

        [Category("Behavior")]
        [Description("Value가 바뀔 때 발생합니다.")]
        public event EventHandler ValueChanged;

        public AdvRange()
        {
            TabStop = true;
        }

        protected override Size DefaultSize
        {
            get { return new Size(200, 28); }
        }

        protected override bool IsClickable
        {
            get { return true; }
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
                if (_maximum < _minimum) _maximum = _minimum;
                Value = _value;
                Invalidate();
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
                Invalidate();
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
                Invalidate();

                var h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        [Category("Behavior")]
        [DefaultValue(1)]
        [Description("방향키·휠이 한 번에 더하거나 빼는 값입니다.")]
        public int Increment
        {
            get { return _increment; }
            set { _increment = value <= 0 ? 1 : value; }
        }

        [Category("Appearance")]
        [DefaultValue(AdvContextColor.Primary)]
        [Description("채움(진행 부분)의 컨텍스트 색입니다.")]
        public AdvContextColor Context
        {
            get { return _context; }
            set { if (_context == value) return; _context = value; Invalidate(); }
        }

        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvRangeOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvRangeOptions(this)); }
        }

        private float Ratio
        {
            get { int span = _maximum - _minimum; return span <= 0 ? 0f : (float)(_value - _minimum) / span; }
        }

        private int ThumbDiameter
        {
            get { return Math.Min(FrameBounds.Height, 20); }
        }

        /// <summary>손잡이 중심이 지나는 구간. 손잡이가 밖으로 잘리지 않도록 좌우를 반지름만큼 좁힌다.</summary>
        private Rectangle TrackArea
        {
            get
            {
                var b = FrameBounds;
                int r = ThumbDiameter / 2;
                return new Rectangle(b.Left + r, b.Top, Math.Max(1, b.Width - r * 2), b.Height);
            }
        }

        private int ThumbX
        {
            get { var t = TrackArea; return t.Left + (int)Math.Round(Ratio * t.Width); }
        }

        private void SetValueFromX(int x)
        {
            var t = TrackArea;
            if (t.Width <= 0) return;
            float ratio = (float)(x - t.Left) / t.Width;
            if (ratio < 0f) ratio = 0f; else if (ratio > 1f) ratio = 1f;
            Value = _minimum + (int)Math.Round(ratio * (_maximum - _minimum));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var palette = theme.ResolveContext(_context);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var b = FrameBounds;
            if (b.Width <= 0 || b.Height <= 0) return;

            var t = TrackArea;
            int cy = b.Top + b.Height / 2;
            int trackH = Math.Max(4, ThumbDiameter / 3);
            int tx = ThumbX;

            // 트랙 배경.
            var trackRect = new Rectangle(t.Left, cy - trackH / 2, t.Width, trackH);
            using (var path = AdvGraphics.CreateRoundedRect(trackRect, trackH / 2))
            using (var brush = new SolidBrush(Enabled ? theme.SurfacePressed : theme.DisabledFill))
                g.FillPath(brush, path);

            // 채움(최솟값 → 현재값).
            int fillW = Math.Max(0, tx - t.Left);
            if (fillW > 0)
            {
                var fillRect = new Rectangle(t.Left, cy - trackH / 2, fillW, trackH);
                using (var path = AdvGraphics.CreateRoundedRect(fillRect, trackH / 2))
                using (var brush = new SolidBrush(Enabled ? palette.Solid : theme.TextDisabled))
                    g.FillPath(brush, path);
            }

            // 손잡이(호버 시 살짝 커진다).
            int grow = (int)(HoverAmount * 2f);
            int d = ThumbDiameter + grow * 2;
            var thumb = new Rectangle(tx - d / 2, cy - d / 2, d, d);

            if (Enabled && ShowsFocusVisual)
            {
                var ring = Rectangle.Inflate(thumb, 3, 3);
                using (var pen = new Pen(theme.FocusRing, 2f))
                    g.DrawEllipse(pen, ring);
            }

            using (var brush = new SolidBrush(Enabled ? palette.Solid : theme.TextDisabled))
                g.FillEllipse(brush, thumb);
            using (var pen = new Pen(theme.Surface, 2f))
                g.DrawEllipse(pen, thumb.Left + 1, thumb.Top + 1, thumb.Width - 2, thumb.Height - 2);

            base.OnPaint(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !Enabled) return;

            if (!Focused) Focus();
            SetValueFromX(e.X);
            _dragging = true;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging) SetValueFromX(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _dragging)
            {
                _dragging = false;
                Capture = false;
            }
            base.OnMouseUp(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (Enabled)
            {
                if (e.Delta > 0) Value = _value + _increment;
                else if (e.Delta < 0) Value = _value - _increment;
            }
            base.OnMouseWheel(e);
        }

        /// <summary>방향키가 포커스 이동에 먹히지 않고 이 컨트롤로 오게 한다.</summary>
        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                case Keys.Home:
                case Keys.End:
                    return true;
                default:
                    return base.IsInputKey(keyData);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left:
                case Keys.Down: Value = _value - _increment; e.Handled = true; break;
                case Keys.Right:
                case Keys.Up: Value = _value + _increment; e.Handled = true; break;
                case Keys.Home: Value = _minimum; e.Handled = true; break;
                case Keys.End: Value = _maximum; e.Handled = true; break;
            }
            base.OnKeyDown(e);
        }

        protected override void OnThemeChanged()
        {
            Invalidate();
            base.OnThemeChanged();
        }
    }

    /// <summary>AdvRange가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvRangeOptions : AdvOptions
    {
        private readonly AdvRange _owner;

        internal AdvRangeOptions(AdvRange owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(AdvContextColor.Primary)]
        [Description("채움(진행 부분)의 컨텍스트 색입니다.")]
        public AdvContextColor Context
        {
            get { return _owner.Context; }
            set { _owner.Context = value; }
        }
    }
}
