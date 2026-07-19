using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls.Internal
{
    /// <summary>
    /// 드롭다운 안에 들어가는 달력. 표준 MonthCalendar는 Win32가 그려 테마가
    /// 먹지 않으므로 직접 그린다.
    /// </summary>
    internal class AdvCalendar : Control
    {
        private const int Columns = 7;
        private const int Rows = 6;
        private const int HeaderHeight = 32;
        private const int WeekdayHeight = 22;

        private AdvTheme _theme;
        private DateTime _viewMonth;
        private DateTime _selected;
        private DateTime _minDate = new DateTime(1753, 1, 1);
        private DateTime _maxDate = new DateTime(9998, 12, 31);
        private int _hoverDay = -1;

        public event EventHandler<DateEventArgs> DateChosen;

        public AdvCalendar(AdvTheme theme, DateTime selected)
        {
            _theme = theme;
            _selected = selected.Date;
            _viewMonth = new DateTime(_selected.Year, _selected.Month, 1);

            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw, true);
        }

        public AdvTheme Theme
        {
            get { return _theme; }
            set { _theme = value; Invalidate(); }
        }

        public DateTime MinDate
        {
            get { return _minDate; }
            set { _minDate = value.Date; Invalidate(); }
        }

        public DateTime MaxDate
        {
            get { return _maxDate; }
            set { _maxDate = value.Date; Invalidate(); }
        }

        public DateTime Selected
        {
            get { return _selected; }
            set
            {
                _selected = value.Date;
                _viewMonth = new DateTime(_selected.Year, _selected.Month, 1);
                Invalidate();
            }
        }

        private int CellWidth { get { return Width / Columns; } }

        private int CellHeight
        {
            get { return Math.Max(1, (Height - HeaderHeight - WeekdayHeight) / Rows); }
        }

        /// <summary>이번 달 1일이 놓이는 칸 번호(일요일 시작).</summary>
        private int LeadingBlanks
        {
            get { return (int)_viewMonth.DayOfWeek; }
        }

        private Rectangle PrevBounds { get { return new Rectangle(4, 4, 24, HeaderHeight - 8); } }
        private Rectangle NextBounds { get { return new Rectangle(Width - 28, 4, 24, HeaderHeight - 8); } }

        private Rectangle CellBounds(int cell)
        {
            int col = cell % Columns, row = cell / Columns;
            return new Rectangle(col * CellWidth, HeaderHeight + WeekdayHeight + row * CellHeight,
                                 CellWidth, CellHeight);
        }

        /// <summary>칸 번호를 날짜로. 이번 달이 아니면 null을 뜻하는 DateTime.MinValue.</summary>
        private DateTime DateFromCell(int cell)
        {
            int day = cell - LeadingBlanks + 1;
            int daysInMonth = DateTime.DaysInMonth(_viewMonth.Year, _viewMonth.Month);

            if (day < 1 || day > daysInMonth) return DateTime.MinValue;
            return new DateTime(_viewMonth.Year, _viewMonth.Month, day);
        }

        private bool InRange(DateTime d)
        {
            return d >= _minDate && d <= _maxDate;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;

            using (var back = new SolidBrush(_theme.InputBackground))
                g.FillRectangle(back, ClientRectangle);

            // 머리글: 이전 · 년월 · 다음
            var title = _viewMonth.ToString("yyyy년 M월", CultureInfo.CurrentCulture);
            TextRenderer.DrawText(g, title, Font,
                new Rectangle(28, 0, Width - 56, HeaderHeight), _theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
              | TextFormatFlags.NoPrefix);

            DrawChevron(g, PrevBounds, true);
            DrawChevron(g, NextBounds, false);

            // 요일 줄
            var names = CultureInfo.CurrentCulture.DateTimeFormat.ShortestDayNames;
            for (int c = 0; c < Columns; c++)
            {
                var r = new Rectangle(c * CellWidth, HeaderHeight, CellWidth, WeekdayHeight);
                TextRenderer.DrawText(g, names[c], Font, r, _theme.TextMuted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                  | TextFormatFlags.NoPrefix);
            }

            // 날짜 칸
            var today = DateTime.Today;

            for (int cell = 0; cell < Columns * Rows; cell++)
            {
                var date = DateFromCell(cell);
                if (date == DateTime.MinValue) continue;

                var r = CellBounds(cell);
                bool enabled = InRange(date);
                bool isSelected = date == _selected;
                Color fore = enabled ? _theme.Text : _theme.TextDisabled;

                if (isSelected)
                {
                    using (var path = AdvGraphics.CreateRoundedRect(
                               Rectangle.Inflate(r, -3, -2), new AdvCorners(4)))
                    using (var b = new SolidBrush(_theme.Accent))
                        g.FillPath(b, path);

                    fore = _theme.OnAccent;
                }
                else if (cell == _hoverDay && enabled)
                {
                    using (var path = AdvGraphics.CreateRoundedRect(
                               Rectangle.Inflate(r, -3, -2), new AdvCorners(4)))
                    using (var b = new SolidBrush(_theme.SurfaceHover))
                        g.FillPath(b, path);
                }
                else if (date == today)
                {
                    // 오늘은 테두리만 둘러 선택과 구분한다
                    using (var path = AdvGraphics.CreateRoundedRect(
                               Rectangle.Inflate(r, -3, -2), new AdvCorners(4)))
                    using (var pen = new Pen(_theme.Accent))
                        g.DrawPath(pen, path);
                }

                TextRenderer.DrawText(g, date.Day.ToString(CultureInfo.CurrentCulture), Font, r, fore,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                  | TextFormatFlags.NoPrefix);
            }

            base.OnPaint(e);
        }

        private void DrawChevron(Graphics g, Rectangle area, bool left)
        {
            // 남는 1px을 어느 쪽에 줄지 정해 좌우 여백을 맞춘다(정수 나눗셈이라 그냥 두면 1px 치우친다)
            var centered = new Rectangle(area.Left + 1, area.Top, area.Width, area.Height);

            AdvGraphics.DrawChevron(g, centered,
                left ? AdvGraphics.ChevronDirection.Left : AdvGraphics.ChevronDirection.Right,
                _theme.TextMuted, 9, 5, 1.6f, 0);
        }

        private int CellFromPoint(Point p)
        {
            if (p.Y < HeaderHeight + WeekdayHeight) return -1;

            int col = p.X / Math.Max(1, CellWidth);
            int row = (p.Y - HeaderHeight - WeekdayHeight) / Math.Max(1, CellHeight);

            if (col < 0 || col >= Columns || row < 0 || row >= Rows) return -1;
            return row * Columns + col;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int cell = CellFromPoint(e.Location);
            if (cell >= 0 && DateFromCell(cell) == DateTime.MinValue) cell = -1;

            if (cell != _hoverDay) { _hoverDay = cell; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hoverDay != -1) { _hoverDay = -1; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) { base.OnMouseDown(e); return; }

            if (PrevBounds.Contains(e.Location)) { ShiftMonth(-1); base.OnMouseDown(e); return; }
            if (NextBounds.Contains(e.Location)) { ShiftMonth(1); base.OnMouseDown(e); return; }

            int cell = CellFromPoint(e.Location);
            if (cell < 0) { base.OnMouseDown(e); return; }

            var date = DateFromCell(cell);
            if (date == DateTime.MinValue || !InRange(date)) { base.OnMouseDown(e); return; }

            _selected = date;
            Invalidate();

            var handler = DateChosen;
            if (handler != null) handler(this, new DateEventArgs(date));

            base.OnMouseDown(e);
        }

        /// <summary>
        /// 달을 옮긴다. DateTime의 최소·최대 달을 넘어가면 예외가 나므로 미리 막는다.
        /// </summary>
        private void ShiftMonth(int delta)
        {
            var next = _viewMonth.AddMonths(delta);
            if (next < new DateTime(1753, 1, 1) || next > new DateTime(9998, 12, 1)) return;

            _viewMonth = next;
            _hoverDay = -1;
            Invalidate();
        }

        internal class DateEventArgs : EventArgs
        {
            public DateTime Date { get; private set; }
            public DateEventArgs(DateTime date) { Date = date; }
        }
    }
}
