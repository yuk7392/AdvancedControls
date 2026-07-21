using System;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls.Internal
{
    /// <summary>
    /// 테마를 따르는 세로 스크롤바. OS가 그리는 스크롤바는 색을 바꿀 수 없어
    /// 다크 테마에서 흰 띠로 남기 때문에 직접 그린다.
    /// </summary>
    internal class AdvScrollBar : Control
    {
        internal const int DefaultWidth = 10;
        private const int MinThumbHeight = 24;

        private AdvTheme _theme;
        private int _contentHeight;
        private int _viewportHeight;
        private int _value;

        private bool _dragging;
        private int _dragOffset;
        private bool _hot;

        public event EventHandler ValueChanged;

        public AdvScrollBar(AdvTheme theme)
        {
            _theme = theme;

            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw, true);

            TabStop = false;
        }

        public AdvTheme Theme
        {
            get { return _theme; }
            set { _theme = value; Invalidate(); }
        }

        /// <summary>전체 내용 높이.</summary>
        public int ContentHeight
        {
            get { return _contentHeight; }
            set
            {
                if (_contentHeight == value) return;
                _contentHeight = Math.Max(0, value);
                ClampValue();
                Invalidate();
            }
        }

        /// <summary>한 번에 보이는 높이.</summary>
        public int ViewportHeight
        {
            get { return _viewportHeight; }
            set
            {
                if (_viewportHeight == value) return;
                _viewportHeight = Math.Max(0, value);
                ClampValue();
                Invalidate();
            }
        }

        /// <summary>스크롤이 필요한 최대 이동량.</summary>
        public int MaxValue
        {
            get { return Math.Max(0, _contentHeight - _viewportHeight); }
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int v = value;
                if (v < 0) v = 0;
                if (v > MaxValue) v = MaxValue;
                if (_value == v) return;

                _value = v;
                Invalidate();

                var handler = ValueChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        public bool IsNeeded
        {
            get { return MaxValue > 0; }
        }

        private void ClampValue()
        {
            if (_value > MaxValue) Value = MaxValue;
        }

        private Rectangle ThumbBounds
        {
            get
            {
                if (!IsNeeded || Height <= 0) return Rectangle.Empty;

                // 내용이 아주 길어도 잡을 수 있을 만큼은 남긴다
                int h = (int)((long)Height * _viewportHeight / Math.Max(1, _contentHeight));
                if (h < MinThumbHeight) h = MinThumbHeight;
                if (h > Height) h = Height;

                int travel = Height - h;
                int y = MaxValue == 0 ? 0 : (int)((long)travel * _value / MaxValue);

                return new Rectangle(0, y, Width, h);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (var back = new SolidBrush(_theme.InputBackground))
                g.FillRectangle(back, ClientRectangle);

            var thumb = ThumbBounds;
            if (thumb.IsEmpty) { base.OnPaint(e); return; }

            // 좌우로 살짝 여백을 둬 얇게 보이게 한다
            var shape = new Rectangle(thumb.X + 2, thumb.Y + 2,
                                      Math.Max(1, thumb.Width - 4),
                                      Math.Max(1, thumb.Height - 4));

            Color color = _dragging ? _theme.TextMuted
                        : _hot ? _theme.BorderHover
                        : _theme.Border;

            using (var path = AdvGraphics.CreateRoundedRect(shape, new AdvCorners(shape.Width / 2)))
            using (var brush = new SolidBrush(color))
                g.FillPath(brush, path);

            base.OnPaint(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && IsNeeded)
            {
                var thumb = ThumbBounds;

                if (thumb.Contains(e.Location))
                {
                    _dragging = true;
                    _dragOffset = e.Y - thumb.Y;
                    Capture = true;
                }
                else
                {
                    // 트랙을 누르면 한 화면씩 넘긴다
                    Value += e.Y < thumb.Y ? -_viewportHeight : _viewportHeight;
                }

                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            // 캡처가 풀린 뒤 버튼을 안 눌러도 스쳐서 썸이 이동하는 것을 막는다
            if (_dragging && (e.Button & MouseButtons.Left) == 0) _dragging = false;

            if (_dragging)
            {
                var thumb = ThumbBounds;
                int travel = Height - thumb.Height;

                if (travel > 0)
                {
                    int y = e.Y - _dragOffset;
                    if (y < 0) y = 0;
                    if (y > travel) y = travel;

                    Value = (int)((long)y * MaxValue / travel);
                }
            }
            else
            {
                bool hot = ThumbBounds.Contains(e.Location);
                if (hot != _hot) { _hot = hot; Invalidate(); }
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                Capture = false;
                Invalidate();
            }
            base.OnMouseUp(e);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            // 드래그 중 캡처가 강제로 풀리면(모달·Alt+Tab·Enabled=false 등) 드래그 상태를 확실히 내린다
            if (_dragging) { _dragging = false; Invalidate(); }
            base.OnMouseCaptureChanged(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hot && !_dragging) { _hot = false; Invalidate(); }
            base.OnMouseLeave(e);
        }
    }
}
