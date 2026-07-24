using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>별점 단위.</summary>
    /// <summary>별점 글리프 모양.</summary>
    public enum AdvRatingGlyph
    {
        Star,
        Heart,
        Circle
    }

    public enum AdvRatingPrecision
    {
        /// <summary>별 1개 단위.</summary>
        Full,
        /// <summary>별 반 개 단위.</summary>
        Half
    }

    /// <summary>
    /// 별점 컨트롤. 마우스를 올리면 미리보기가 따라오고 클릭으로 확정한다.
    /// 같은 값을 다시 클릭하면 0으로 지워진다. 방향키로도 조정할 수 있고,
    /// <see cref="ReadOnly"/>면 표시 전용이 된다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("ValueChanged")]
    [DefaultProperty("AdvancedControlOptions")]
    [Description("별점을 표시하고 입력받는 컨트롤입니다.")]
    public class AdvRating : AdvControlBase
    {
        private const int StarGap = 4;      // 별 사이 간격(96dpi)
        private const float InnerRatio = 0.42f;   // 별 안쪽 반지름 비율

        private int _maximum = 5;
        private float _value;
        private AdvRatingPrecision _precision = AdvRatingPrecision.Full;
        private AdvRatingGlyph _glyph = AdvRatingGlyph.Star;
        private bool _readOnly;
        private float _hoverValue = -1f;    // 미리보기(-1=없음)
        private AdvRatingOptions _options;

        /// <summary>값이 바뀌면 발생한다.</summary>
        [Category("Behavior")]
        [Description("별점 값이 바뀌면 발생합니다.")]
        public event EventHandler ValueChanged;

        public AdvRating()
        {
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }

        protected override Size DefaultSize
        {
            get { return new Size(150, 28); }
        }

        protected override bool IsClickable
        {
            get { return !_readOnly; }
        }

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvRatingOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvRatingOptions(this)); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(5)]
        [Description("별 개수입니다(1~20).")]
        public int Maximum
        {
            get { return _maximum; }
            set
            {
                value = value < 1 ? 1 : (value > 20 ? 20 : value);
                if (_maximum == value) return;
                _maximum = value;
                if (_value > value) Value = value;
                Invalidate();
            }
        }

        /// <summary>현재 별점(0~Maximum). 단위(Precision)에 맞춰 반올림된다.</summary>
        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(0f)]
        [Description("현재 별점입니다(0~별 개수).")]
        public float Value
        {
            get { return _value; }
            set
            {
                value = Snap(value);
                if (_value == value) return;
                _value = value;
                Invalidate();
                var h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvRatingPrecision.Full)]
        [Description("별 1개 단위인지 반 개 단위인지입니다.")]
        public AdvRatingPrecision Precision
        {
            get { return _precision; }
            set
            {
                if (_precision == value) return;
                _precision = value;
                Value = _value;   // 새 단위로 다시 스냅
                Invalidate();
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvRatingGlyph.Star)]
        [Description("점수 글리프 모양(별/하트/원)입니다.")]
        public AdvRatingGlyph Glyph
        {
            get { return _glyph; }
            set
            {
                if (_glyph == value) return;
                _glyph = value;
                Invalidate();
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("표시 전용으로 만들지 여부입니다.")]
        public bool ReadOnly
        {
            get { return _readOnly; }
            set
            {
                if (_readOnly == value) return;
                _readOnly = value;
                _hoverValue = -1f;
                SetStyle(ControlStyles.Selectable, !value);
                TabStop = !value;
                Invalidate();
            }
        }

        private float Step { get { return _precision == AdvRatingPrecision.Half ? 0.5f : 1f; } }

        /// <summary>단위에 맞춰 반올림하고 0~Maximum으로 자른다.</summary>
        private float Snap(float v)
        {
            float step = Step;
            v = (float)Math.Round(v / step) * step;
            if (v < 0f) v = 0f;
            if (v > _maximum) v = _maximum;
            return v;
        }

        // ── 레이아웃 ──────────────────────────────────────────────────

        private int StarSize
        {
            get { return Math.Max(8, FrameBounds.Height); }
        }

        private Rectangle StarRect(int i)
        {
            var f = FrameBounds;
            int d = StarSize;
            int gap = AdvGraphics.Scale(this, StarGap);
            return new Rectangle(f.Left + i * (d + gap), f.Top + (f.Height - d) / 2, d, d);
        }

        /// <summary>마우스 위치가 뜻하는 값. 별 밖(왼쪽)이면 0, 오른쪽이면 Maximum.</summary>
        private float ValueAt(Point p)
        {
            for (int i = 0; i < _maximum; i++)
            {
                var r = StarRect(i);
                if (p.X < r.Left) return Snap(i);   // 별 사이 틈은 앞 별까지로 본다
                if (p.X <= r.Right)
                {
                    if (_precision == AdvRatingPrecision.Half && p.X < r.Left + r.Width / 2)
                        return i + 0.5f;
                    return i + 1;
                }
            }
            return _maximum;
        }

        /// <summary>현재 글리프 모양의 경로. 호출자가 Dispose 한다.</summary>
        internal GraphicsPath CreateGlyph(Rectangle r)
        {
            switch (_glyph)
            {
                case AdvRatingGlyph.Heart: return CreateHeart(r);
                case AdvRatingGlyph.Circle:
                    var p = new GraphicsPath();
                    p.AddEllipse(r);
                    return p;
                default: return CreateStar(r);
            }
        }

        /// <summary>하트 경로(베지에 4개). 호출자가 Dispose 한다.</summary>
        internal static GraphicsPath CreateHeart(Rectangle r)
        {
            var p = new GraphicsPath();
            float x = r.X, y = r.Y, w = r.Width, h = r.Height;
            var top = new PointF(x + w / 2f, y + h * 0.30f);       // 위 가운데 오목점
            var bottom = new PointF(x + w / 2f, y + h * 0.95f);    // 아래 꼭짓점

            p.AddBezier(top, new PointF(x + w / 2f, y), new PointF(x, y), new PointF(x, y + h * 0.35f));
            p.AddBezier(new PointF(x, y + h * 0.35f), new PointF(x, y + h * 0.60f),
                        new PointF(x + w * 0.28f, y + h * 0.75f), bottom);
            p.AddBezier(bottom, new PointF(x + w * 0.72f, y + h * 0.75f),
                        new PointF(x + w, y + h * 0.60f), new PointF(x + w, y + h * 0.35f));
            p.AddBezier(new PointF(x + w, y + h * 0.35f), new PointF(x + w, y), new PointF(x + w / 2f, y), top);
            p.CloseFigure();
            return p;
        }

        /// <summary>5각 별 경로. 호출자가 Dispose 한다.</summary>
        internal static GraphicsPath CreateStar(Rectangle r)
        {
            var path = new GraphicsPath();
            var pts = new PointF[10];
            float cx = r.Left + r.Width / 2f;
            float cy = r.Top + r.Height / 2f;
            float ro = Math.Min(r.Width, r.Height) / 2f;
            float ri = ro * InnerRatio;

            for (int i = 0; i < 10; i++)
            {
                double angle = Math.PI / 2 + i * Math.PI / 5;   // 위 꼭짓점부터 시계 반대
                float rad = i % 2 == 0 ? ro : ri;
                pts[i] = new PointF(cx + (float)Math.Cos(angle) * rad,
                                    cy - (float)Math.Sin(angle) * rad);
            }
            path.AddPolygon(pts);
            return path;
        }

        // ── 그리기 ────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float shown = _hoverValue >= 0f ? _hoverValue : _value;
            Color fill = Enabled ? theme.Warning : theme.DisabledFill;
            Color line = Enabled ? theme.Border : theme.TextDisabled;

            for (int i = 0; i < _maximum; i++)
            {
                var r = StarRect(i);
                var inset = Rectangle.Inflate(r, -1, -1);
                using (var path = CreateGlyph(inset))
                {
                    float owned = shown - i;   // 이 별이 채워질 몫(0~1)
                    if (owned >= 1f)
                    {
                        using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                    }
                    else if (owned >= 0.5f)
                    {
                        // 왼쪽 절반만 채운다(클립)
                        var state = g.Save();
                        g.SetClip(new RectangleF(inset.Left, inset.Top, inset.Width / 2f, inset.Height));
                        using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                        g.Restore(state);
                    }

                    using (var pen = new Pen(owned >= 0.5f ? fill : line, 1.2f)
                    { LineJoin = LineJoin.Round })
                        g.DrawPath(pen, path);
                }
            }

            // 포커스 시각: 마지막 별 뒤 점선 밑줄 대신 기본 글로우를 쓴다(베이스가 처리)
            base.OnPaint(e);
        }

        // ── 입력 ──────────────────────────────────────────────────────

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_readOnly || !Enabled) return;
            float v = ValueAt(e.Location);
            if (v != _hoverValue) { _hoverValue = v; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverValue >= 0f) { _hoverValue = -1f; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_readOnly || !Enabled || e.Button != MouseButtons.Left) return;
            Focus();

            float v = ValueAt(e.Location);
            Value = v == _value ? 0f : v;   // 같은 값을 다시 누르면 지운다
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left: case Keys.Right: case Keys.Home: case Keys.End:
                    return !_readOnly;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_readOnly || !Enabled) return;

            switch (e.KeyCode)
            {
                case Keys.Right: Value = _value + Step; e.Handled = true; break;
                case Keys.Left: Value = _value - Step; e.Handled = true; break;
                case Keys.Home: Value = 0f; e.Handled = true; break;
                case Keys.End: Value = _maximum; e.Handled = true; break;
            }
        }
    }

    /// <summary>AdvRating이 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvRatingOptions : AdvOptions
    {
        private readonly AdvRating _owner;

        internal AdvRatingOptions(AdvRating owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(5)]
        [Description("별 개수입니다(1~20).")]
        public int Maximum
        {
            get { return _owner.Maximum; }
            set { _owner.Maximum = value; }
        }

        [DefaultValue(0f)]
        [Description("현재 별점입니다(0~별 개수).")]
        public float Value
        {
            get { return _owner.Value; }
            set { _owner.Value = value; }
        }

        [DefaultValue(AdvRatingPrecision.Full)]
        [Description("별 1개 단위인지 반 개 단위인지입니다.")]
        public AdvRatingPrecision Precision
        {
            get { return _owner.Precision; }
            set { _owner.Precision = value; }
        }

        [DefaultValue(false)]
        [Description("표시 전용으로 만들지 여부입니다.")]
        public bool ReadOnly
        {
            get { return _owner.ReadOnly; }
            set { _owner.ReadOnly = value; }
        }

        [DefaultValue(AdvRatingGlyph.Star)]
        [Description("점수 글리프 모양(별/하트/원)입니다.")]
        public AdvRatingGlyph Glyph
        {
            get { return _owner.Glyph; }
            set { _owner.Glyph = value; }
        }
    }
}
