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
    /// 트랙 위에서 손잡이를 드래그해 값을 고르는 슬라이더.
    /// 채움은 강조 색을 따르고, 드래그·키보드·휠로 값을 바꾼다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("ValueChanged")]
    [DefaultProperty("AdvancedControlOptions")]
    [Description("드래그로 값을 고르는 슬라이더입니다.")]
    public class AdvRange : AdvControlBase
    {
        private int _minimum;
        private int _maximum = 100;
        private int _value;
        private int _increment = 1;
        private Color _context = Color.Empty;
        private bool _dragging;
        private int _tickFrequency;
        private bool _showValueTooltip;
        private ToolStripDropDown _valueTip;
        private ToolTipBubble _valueBubble;
        private ToolStripControlHost _valueHost;
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
                if (_maximum < _minimum) _maximum = _minimum;
                Value = _value;
                Invalidate();
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
                Invalidate();
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
                Invalidate();

                var h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(1)]
        [Description("방향키·휠이 한 번에 더하거나 빼는 값입니다.")]
        public int Increment
        {
            get { return _increment; }
            set { _increment = value <= 0 ? 1 : value; }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [Description("채움(진행 부분)의 강조 색입니다. 비워 두면 테마 강조색(Accent)을 따릅니다.")]
        public Color Context
        {
            get { return _context; }
            set { if (_context == value) return; _context = value; Invalidate(); }
        }
        public bool ShouldSerializeContext() { return !_context.IsEmpty; }
        public void ResetContext() { Context = Color.Empty; }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(0)]
        [Description("눈금 간격입니다. 0이면 눈금을 그리지 않습니다. 예: 10이면 10마다 눈금을 긋습니다.")]
        public int TickFrequency
        {
            get { return _tickFrequency; }
            set { value = Math.Max(0, value); if (_tickFrequency == value) return; _tickFrequency = value; Invalidate(); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("드래그하는 동안 손잡이 위에 현재 값을 말풍선으로 표시합니다.")]
        public bool ShowValueTooltip
        {
            get { return _showValueTooltip; }
            set
            {
                if (_showValueTooltip == value) return;
                _showValueTooltip = value;
                if (!value) HideValueTip();
            }
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

        /// <summary>PageUp/PageDown 한 번의 이동량. 범위의 1/10(최소 1).</summary>
        private int LargeChange
        {
            get { return Math.Max(1, (_maximum - _minimum) / 10); }
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
            var palette = AdvContextPalette.Resolve(_context, theme);
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

            // 눈금(트랙 아래).
            if (_tickFrequency > 0)
                DrawTicks(g, t, cy, trackH, Enabled ? theme.TextMuted : theme.TextDisabled);

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

        /// <summary>눈금을 트랙 아래에 짧은 세로선으로 긋는다.</summary>
        private void DrawTicks(Graphics g, Rectangle track, int cy, int trackH, Color color)
        {
            int span = _maximum - _minimum;
            if (span <= 0 || _tickFrequency <= 0) return;
            if (span / _tickFrequency > 500) return;      // 너무 촘촘하면 그리지 않는다

            int y1 = cy + trackH / 2 + 2;
            int len = Math.Min(5, FrameBounds.Bottom - y1);
            if (len < 2) return;
            int y2 = y1 + len;

            using (var pen = new Pen(color, 1f))
            {
                for (int v = _minimum; v <= _maximum; v += _tickFrequency)
                {
                    float ratio = (float)(v - _minimum) / span;
                    int x = track.Left + (int)Math.Round(ratio * track.Width);
                    g.DrawLine(pen, x, y1, x, y2);
                }
                // 최댓값이 간격에 딱 안 맞아도 끝 눈금은 긋는다
                if (span % _tickFrequency != 0)
                    g.DrawLine(pen, track.Right, y1, track.Right, y2);
            }
        }

        #region 드래그 값 말풍선

        private void EnsureValueTip()
        {
            if (_valueTip != null) return;

            _valueBubble = new ToolTipBubble();
            _valueHost = new ToolStripControlHost(_valueBubble)
            { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false };
            _valueTip = new ToolStripDropDown
            { AutoSize = false, Margin = Padding.Empty, Padding = Padding.Empty,
              AutoClose = false, DropShadowEnabled = false };
            _valueTip.Items.Add(_valueHost);
        }

        /// <summary>현재 값을 손잡이 위에 띄우거나, 이미 떠 있으면 위치·내용을 갱신한다.</summary>
        private void ShowOrUpdateValueTip()
        {
            if (!_showValueTooltip || DesignMode || !IsHandleCreated) return;

            EnsureValueTip();
            _valueBubble.SetContent(_value.ToString(), string.Empty, EffectiveTheme);

            var size = _valueBubble.Measure();
            _valueBubble.Size = size; _valueHost.Size = size; _valueTip.Size = size;

            var old = _valueTip.Region;
            using (var rp = AdvGraphics.CreateRoundedRect(new Rectangle(0, 0, size.Width, size.Height), 6))
                _valueTip.Region = new Region(rp);
            if (old != null) old.Dispose();

            int cy = FrameBounds.Top + FrameBounds.Height / 2;
            int thumbTop = cy - ThumbDiameter / 2;
            var screen = PointToScreen(new Point(ThumbX, thumbTop));

            int x = screen.X - size.Width / 2;
            int y = screen.Y - size.Height - 6;
            var wa = Screen.FromControl(this).WorkingArea;
            if (x < wa.Left) x = wa.Left;
            if (x + size.Width > wa.Right) x = wa.Right - size.Width;
            if (y < wa.Top) y = screen.Y + ThumbDiameter + 6;   // 위 공간이 없으면 아래로

            if (_valueTip.Visible) _valueTip.Location = new Point(x, y);
            else _valueTip.Show(x, y);
        }

        private void HideValueTip()
        {
            if (_valueTip != null && _valueTip.Visible) _valueTip.Close();
        }

        #endregion

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !Enabled) return;

            if (!Focused) Focus();
            SetValueFromX(e.X);
            _dragging = true;
            Capture = true;
            ShowOrUpdateValueTip();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            // 캡처가 비정상으로 풀렸을 때를 대비해 왼쪽 버튼이 실제로 눌린 동안만 드래그로 인정한다.
            if (_dragging && (e.Button & MouseButtons.Left) != 0)
            {
                SetValueFromX(e.X);
                ShowOrUpdateValueTip();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            // 드래그 중 캡처가 풀리면(MessageBox·Alt+Tab·Enabled=false 등) 드래그 상태를 확실히 내린다.
            _dragging = false;
            HideValueTip();
            base.OnMouseCaptureChanged(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _dragging)
            {
                _dragging = false;
                Capture = false;
                HideValueTip();
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
                case Keys.PageUp:
                case Keys.PageDown:
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
                case Keys.PageUp: Value = _value + LargeChange; e.Handled = true; break;
                case Keys.PageDown: Value = _value - LargeChange; e.Handled = true; break;
            }
            base.OnKeyDown(e);
        }

        protected override void OnThemeChanged()
        {
            Invalidate();
            base.OnThemeChanged();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _valueTip != null)
            {
                if (_valueTip.Region != null) _valueTip.Region.Dispose();
                _valueTip.Dispose();
                _valueTip = null;
            }
            base.Dispose(disposing);
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

        [DefaultValue(1)]
        [Description("방향키·휠이 한 번에 더하거나 빼는 값입니다.")]
        public int Increment
        {
            get { return _owner.Increment; }
            set { _owner.Increment = value; }
        }

        [Description("채움(진행 부분)의 강조 색입니다. 비워 두면 테마 강조색(Accent)을 따릅니다.")]
        public Color Context
        {
            get { return _owner.Context; }
            set { _owner.Context = value; }
        }
        public bool ShouldSerializeContext() { return _owner.ShouldSerializeContext(); }
        public void ResetContext() { _owner.ResetContext(); }

        [DefaultValue(0)]
        [Description("눈금 간격입니다. 0이면 눈금을 그리지 않습니다. 예: 10이면 10마다 눈금을 긋습니다.")]
        public int TickFrequency
        {
            get { return _owner.TickFrequency; }
            set { _owner.TickFrequency = value; }
        }

        [DefaultValue(false)]
        [Description("드래그하는 동안 손잡이 위에 현재 값을 말풍선으로 표시합니다.")]
        public bool ShowValueTooltip
        {
            get { return _owner.ShowValueTooltip; }
            set { _owner.ShowValueTooltip = value; }
        }
    }
}
