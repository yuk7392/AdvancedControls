using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>스테퍼 단계 이벤트 인자.</summary>
    public class AdvStepEventArgs : EventArgs
    {
        public int Index { get; private set; }
        public AdvStepEventArgs(int index) { Index = index; }
    }

    /// <summary>
    /// 진행 단계 표시기(①→②→③). 지난 단계는 체크, 현재는 강조색, 남은 단계는 번호로
    /// 그린다. 가로(위쪽 배치)·세로(왼쪽 배치)를 지원하며, 단계 원을 누르면
    /// <see cref="StepClicked"/>가 발생한다(이동 여부는 소비자·마법사가 정한다).
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("StepClicked")]
    [DefaultProperty("AdvancedControlOptions")]
    [Description("진행 단계를 원과 연결선으로 보여주는 스테퍼입니다.")]
    public class AdvStepper : AdvControlBase
    {
        // 96dpi 논리 치수
        private const int CircleD = 26;
        private const int EdgePad = 6;
        private const int LabelGap = 6;
        private const int LineGap = 4;     // 연결선과 원 사이 틈

        private readonly List<string> _labels = new List<string>();
        private int _current;
        private Orientation _orientation = Orientation.Horizontal;
        private int _hover = -1;
        private AdvStepperOptions _options;

        /// <summary>단계 원을 클릭하면 발생한다. 실제 이동은 소비자가 정한다.</summary>
        [Category("Behavior")]
        [Description("단계 원을 클릭하면 발생합니다.")]
        public event EventHandler<AdvStepEventArgs> StepClicked;

        /// <summary>현재 단계가 바뀌면 발생한다.</summary>
        [Category("Behavior")]
        [Description("현재 단계가 바뀌면 발생합니다.")]
        public event EventHandler CurrentStepChanged;

        public AdvStepper()
        {
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Styling.ShowFocusGlow = false;
        }

        protected override Size DefaultSize
        {
            get { return new Size(320, 56); }
        }

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvStepperOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvStepperOptions(this)); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(Orientation.Horizontal)]
        [Description("가로(위쪽 배치)인지 세로(왼쪽 배치)인지입니다.")]
        public Orientation Orientation
        {
            get { return _orientation; }
            set { if (_orientation == value) return; _orientation = value; Invalidate(); }
        }

        /// <summary>현재 단계(0부터). 단계가 없으면 0이다.</summary>
        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(0)]
        [Description("현재 단계입니다(0부터).")]
        public int CurrentStep
        {
            get { return _current; }
            set
            {
                int max = Math.Max(0, _labels.Count - 1);
                if (value < 0) value = 0; else if (value > max) value = max;
                if (_current == value) return;
                _current = value;
                Invalidate();
                var h = CurrentStepChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        [Browsable(false)]
        public int StepCount { get { return _labels.Count; } }

        [Browsable(false)]
        public IList<string> Labels { get { return _labels.AsReadOnly(); } }

        /// <summary>단계를 추가하고 위치를 돌려준다.</summary>
        public int AddStep(string text)
        {
            _labels.Add(text ?? string.Empty);
            Invalidate();
            return _labels.Count - 1;
        }

        public void ClearSteps()
        {
            _labels.Clear();
            _current = 0;
            Invalidate();
        }

        // ── 레이아웃 ──────────────────────────────────────────────────

        /// <summary>단계 i 원의 중심. 원들은 양 끝 여백을 빼고 고르게 분배된다.</summary>
        private Point CircleCenter(int i)
        {
            var f = FrameBounds;
            int d = AdvGraphics.Scale(this, CircleD);
            int pad = AdvGraphics.Scale(this, EdgePad);
            int r = d / 2;
            int n = Math.Max(1, _labels.Count);

            if (_orientation == Orientation.Horizontal)
            {
                int left = f.Left + pad + r;
                int right = f.Right - pad - r;
                int x = n == 1 ? (left + right) / 2 : left + (int)((long)(right - left) * i / (n - 1));
                return new Point(x, f.Top + pad + r);
            }
            else
            {
                int top = f.Top + pad + r;
                int bottom = f.Bottom - pad - r;
                int y = n == 1 ? (top + bottom) / 2 : top + (int)((long)(bottom - top) * i / (n - 1));
                return new Point(f.Left + pad + r, y);
            }
        }

        private Rectangle CircleRect(int i)
        {
            int d = AdvGraphics.Scale(this, CircleD);
            var c = CircleCenter(i);
            return new Rectangle(c.X - d / 2, c.Y - d / 2, d, d);
        }

        /// <summary>점이 놓인 단계 원 인덱스(여유 4px). 없으면 -1.</summary>
        private int HitStep(Point p)
        {
            int pad = AdvGraphics.Scale(this, 4);
            for (int i = 0; i < _labels.Count; i++)
                if (Rectangle.Inflate(CircleRect(i), pad, pad).Contains(p)) return i;
            return -1;
        }

        // ── 그리기 ────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_labels.Count == 0) { base.OnPaint(e); return; }

            var theme = EffectiveTheme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int d = AdvGraphics.Scale(this, CircleD);
            int lineGap = AdvGraphics.Scale(this, LineGap);
            float lineW = AdvGraphics.Scale(this, 2f);

            // 연결선: i-1 → i. 완료 구간(i <= current)은 강조색
            for (int i = 1; i < _labels.Count; i++)
            {
                var a = CircleCenter(i - 1);
                var b = CircleCenter(i);
                Color lc = i <= _current ? theme.Accent : theme.Border;
                using (var pen = new Pen(lc, lineW))
                {
                    if (_orientation == Orientation.Horizontal)
                        g.DrawLine(pen, a.X + d / 2 + lineGap, a.Y, b.X - d / 2 - lineGap, b.Y);
                    else
                        g.DrawLine(pen, a.X, a.Y + d / 2 + lineGap, b.X, b.Y - d / 2 - lineGap);
                }
            }

            // 원 + 내용(체크/번호) + 레이블
            for (int i = 0; i < _labels.Count; i++)
            {
                var r = CircleRect(i);
                bool done = i < _current;
                bool now = i == _current;

                Color fill = done || now ? theme.Accent : theme.InputBackground;
                Color line = done || now ? theme.Accent : theme.Border;

                using (var b = new SolidBrush(fill)) g.FillEllipse(b, r);
                using (var pen = new Pen(line, AdvGraphics.Scale(this, now ? 1.6f : 1f)))
                    g.DrawEllipse(pen, r);

                if (done)
                {
                    // 완료 체크(목록 체크박스와 같은 폴리라인)
                    int ins = r.Width * 3 / 10;
                    var inner = Rectangle.Inflate(r, -ins, -ins);
                    var pts = new[]
                    {
                        new Point(inner.Left, inner.Top + inner.Height / 2),
                        new Point(inner.Left + inner.Width * 2 / 5, inner.Bottom),
                        new Point(inner.Right, inner.Top)
                    };
                    using (var pen = new Pen(theme.OnAccent, AdvGraphics.Scale(this, 1.8f))
                    { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                        g.DrawLines(pen, pts);
                }
                else
                {
                    TextRenderer.DrawText(g, (i + 1).ToString(), Font, r,
                        now ? theme.OnAccent : theme.TextMuted,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                      | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                }

                DrawLabel(g, theme, i, r, now);
            }

            base.OnPaint(e);
        }

        private void DrawLabel(Graphics g, AdvTheme theme, int i, Rectangle circle, bool now)
        {
            string text = _labels[i];
            if (string.IsNullOrEmpty(text)) return;

            Color fore = now ? theme.Accent : (i < _current ? theme.Text : theme.TextMuted);
            int gap = AdvGraphics.Scale(this, LabelGap);
            var f = FrameBounds;

            if (_orientation == Orientation.Horizontal)
            {
                // 원 아래 가운데 정렬. 이웃과 반씩 나눠 갖는 폭 안에서 줄임표 처리
                int n = Math.Max(1, _labels.Count);
                int slot = f.Width / n;
                var rect = new Rectangle(circle.Left + circle.Width / 2 - slot / 2, circle.Bottom + gap,
                                         slot, f.Bottom - circle.Bottom - gap);
                if (rect.Width > 0 && rect.Height > 0)
                    TextRenderer.DrawText(g, text, Font, rect, fore,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.Top
                      | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            else
            {
                // 원 오른쪽 세로 중앙
                var rect = Rectangle.FromLTRB(circle.Right + gap, circle.Top, f.Right - 2, circle.Bottom);
                if (rect.Width > 0)
                    TextRenderer.DrawText(g, text, Font, rect, fore,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                      | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        // ── 마우스 ────────────────────────────────────────────────────

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hit = HitStep(e.Location);
            if (hit != _hover)
            {
                _hover = hit;
                Cursor = hit >= 0 ? Cursors.Hand : Cursors.Default;
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover != -1) { _hover = -1; Cursor = Cursors.Default; }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            int hit = HitStep(e.Location);
            if (hit < 0) return;
            var h = StepClicked;
            if (h != null) h(this, new AdvStepEventArgs(hit));
        }
    }

    /// <summary>AdvStepper가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvStepperOptions : AdvOptions
    {
        private readonly AdvStepper _owner;

        internal AdvStepperOptions(AdvStepper owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(Orientation.Horizontal)]
        [Description("가로(위쪽 배치)인지 세로(왼쪽 배치)인지입니다.")]
        public Orientation Orientation
        {
            get { return _owner.Orientation; }
            set { _owner.Orientation = value; }
        }

        [DefaultValue(0)]
        [Description("현재 단계입니다(0부터).")]
        public int CurrentStep
        {
            get { return _owner.CurrentStep; }
            set { _owner.CurrentStep = value; }
        }
    }
}
