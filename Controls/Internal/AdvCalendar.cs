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
        private const int NavW = 20;                 // 머리글 이동 버튼 폭
        public const int TodayFooterHeight = 34;     // "오늘" 버튼 영역 높이(외부에서 크기 계산에 쓴다)

        private AdvTheme _theme;
        private DateTime _viewMonth;
        private DateTime _selected;
        private DateTime _minDate = new DateTime(1753, 1, 1);
        private DateTime _maxDate = new DateTime(9998, 12, 31);
        private bool _showToday = true;
        private int _hoverDay = -1;
        private int _hoverNav = -1;   // 0 prevYear · 1 prevMonth · 2 nextMonth · 3 nextYear
        private bool _hoverToday;

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

        /// <summary>하단에 "오늘" 버튼을 보일지 여부.</summary>
        public bool ShowToday
        {
            get { return _showToday; }
            set { _showToday = value; Invalidate(); }
        }

        private int FooterArea { get { return _showToday ? TodayFooterHeight : 0; } }

        private int CellWidth { get { return Width / Columns; } }

        private int CellHeight
        {
            get { return Math.Max(1, (Height - HeaderHeight - WeekdayHeight - FooterArea) / Rows); }
        }

        /// <summary>이번 달 1일이 놓이는 칸 번호(일요일 시작).</summary>
        private int LeadingBlanks
        {
            get { return (int)_viewMonth.DayOfWeek; }
        }

        // 머리글: [«년] [‹월] ── 년월 ── [월›] [년»]
        private Rectangle PrevYearBounds { get { return new Rectangle(4, 4, NavW, HeaderHeight - 8); } }
        private Rectangle PrevMonthBounds { get { return new Rectangle(4 + NavW + 2, 4, NavW, HeaderHeight - 8); } }
        private Rectangle NextYearBounds { get { return new Rectangle(Width - 4 - NavW, 4, NavW, HeaderHeight - 8); } }
        private Rectangle NextMonthBounds { get { return new Rectangle(Width - 4 - NavW * 2 - 2, 4, NavW, HeaderHeight - 8); } }

        private Rectangle NavBounds(int i)
        {
            switch (i)
            {
                case 0: return PrevYearBounds;
                case 1: return PrevMonthBounds;
                case 2: return NextMonthBounds;
                default: return NextYearBounds;
            }
        }

        private Rectangle FooterBounds
        {
            get { return new Rectangle(0, Height - TodayFooterHeight, Width, TodayFooterHeight); }
        }

        /// <summary>"오늘" 글자 버튼 영역(푸터 안에서 글자 폭에 맞춰 가운데).</summary>
        private Rectangle TodayButtonBounds
        {
            get
            {
                var f = FooterBounds;
                var size = TextRenderer.MeasureText(TodayLabel, Font);
                int w = size.Width + 24, h = f.Height - 10;
                return new Rectangle(f.Left + (f.Width - w) / 2, f.Top + (f.Height - h) / 2, w, h);
            }
        }

        private string TodayLabel
        {
            get { return "오늘 (" + DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture) + ")"; }
        }

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

            // 머리글: [«년] [‹월] ── 년월 ── [월›] [년»]
            int titleLeft = PrevMonthBounds.Right + 2;
            int titleRight = NextMonthBounds.Left - 2;
            var title = _viewMonth.ToString("yyyy년 M월", CultureInfo.CurrentCulture);
            TextRenderer.DrawText(g, title, Font,
                new Rectangle(titleLeft, 0, Math.Max(0, titleRight - titleLeft), HeaderHeight), _theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
              | TextFormatFlags.NoPrefix);

            DrawNav(g, 0, true, true);    // «  이전 년
            DrawNav(g, 1, true, false);   // ‹  이전 월
            DrawNav(g, 2, false, false);  // ›  다음 월
            DrawNav(g, 3, false, true);   // »  다음 년

            // 요일 줄 (일요일=빨강, 토요일=파랑, 나머지는 흐리게)
            var names = CultureInfo.CurrentCulture.DateTimeFormat.ShortestDayNames;
            for (int c = 0; c < Columns; c++)
            {
                var r = new Rectangle(c * CellWidth, HeaderHeight, CellWidth, WeekdayHeight);
                Color head = c == 0 ? _theme.SundayText
                           : c == Columns - 1 ? _theme.SaturdayText
                           : _theme.TextMuted;
                TextRenderer.DrawText(g, names[c], Font, r, head,
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

                // 주말은 일요일 빨강·토요일 파랑. 범위 밖(비활성)은 흐린 색을 그대로 둔다.
                Color fore;
                if (!enabled) fore = _theme.TextDisabled;
                else
                {
                    int col = cell % Columns;
                    fore = col == 0 ? _theme.SundayText
                         : col == Columns - 1 ? _theme.SaturdayText
                         : _theme.Text;
                }

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

            if (_showToday) DrawFooter(g);

            base.OnPaint(e);
        }

        private void DrawNav(Graphics g, int index, bool left, bool year)
        {
            var area = NavBounds(index);

            if (_hoverNav == index)
            {
                using (var path = AdvGraphics.CreateRoundedRect(area, new AdvCorners(4)))
                using (var b = new SolidBrush(_theme.SurfaceHover))
                    g.FillPath(b, path);
            }

            var dir = left ? AdvGraphics.ChevronDirection.Left : AdvGraphics.ChevronDirection.Right;
            var color = _hoverNav == index ? _theme.Text : _theme.TextMuted;

            if (year)
            {
                // 겹친 셰브론 두 개(«, »)로 년 이동을 나타낸다
                AdvGraphics.DrawChevron(g, this, area, dir, color, 8, 4, 1.5f, -3);
                AdvGraphics.DrawChevron(g, this, area, dir, color, 8, 4, 1.5f, 2);
            }
            else
            {
                AdvGraphics.DrawChevron(g, this, area, dir, color, 8, 5, 1.6f, 0);
            }
        }

        private void DrawFooter(Graphics g)
        {
            var f = FooterBounds;

            // 위쪽에 구분선
            using (var pen = new Pen(_theme.Border))
                g.DrawLine(pen, f.Left + 6, f.Top, f.Right - 6, f.Top);

            var btn = TodayButtonBounds;
            if (_hoverToday)
            {
                using (var path = AdvGraphics.CreateRoundedRect(btn, new AdvCorners(4)))
                using (var b = new SolidBrush(_theme.SurfaceHover))
                    g.FillPath(b, path);
            }

            TextRenderer.DrawText(g, TodayLabel, Font, btn, _theme.Accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
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
            int nav = -1;
            for (int i = 0; i < 4; i++)
                if (NavBounds(i).Contains(e.Location)) { nav = i; break; }

            bool today = _showToday && TodayButtonBounds.Contains(e.Location);

            int cell = CellFromPoint(e.Location);
            if (cell >= 0 && DateFromCell(cell) == DateTime.MinValue) cell = -1;

            if (nav != _hoverNav || today != _hoverToday || cell != _hoverDay)
            {
                _hoverNav = nav; _hoverToday = today; _hoverDay = cell;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hoverDay != -1 || _hoverNav != -1 || _hoverToday)
            {
                _hoverDay = -1; _hoverNav = -1; _hoverToday = false;
                Invalidate();
            }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) { base.OnMouseDown(e); return; }

            if (PrevYearBounds.Contains(e.Location)) { ShiftYear(-1); base.OnMouseDown(e); return; }
            if (PrevMonthBounds.Contains(e.Location)) { ShiftMonth(-1); base.OnMouseDown(e); return; }
            if (NextMonthBounds.Contains(e.Location)) { ShiftMonth(1); base.OnMouseDown(e); return; }
            if (NextYearBounds.Contains(e.Location)) { ShiftYear(1); base.OnMouseDown(e); return; }

            if (_showToday && TodayButtonBounds.Contains(e.Location)) { PickToday(); base.OnMouseDown(e); return; }

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

        /// <summary>년을 옮긴다. 범위를 넘어가면 예외가 나므로 미리 막는다.</summary>
        private void ShiftYear(int delta)
        {
            var next = _viewMonth.AddYears(delta);
            if (next < new DateTime(1753, 1, 1) || next > new DateTime(9998, 12, 1)) return;

            _viewMonth = next;
            _hoverDay = -1;
            Invalidate();
        }

        /// <summary>"오늘"을 선택한다. 범위 밖이면 무시한다. 선택과 동시에 DateChosen을 낸다(팝업 닫힘).</summary>
        private void PickToday()
        {
            var today = DateTime.Today;
            if (!InRange(today)) return;

            _selected = today;
            _viewMonth = new DateTime(today.Year, today.Month, 1);
            Invalidate();

            var handler = DateChosen;
            if (handler != null) handler(this, new DateEventArgs(today));
        }

        internal class DateEventArgs : EventArgs
        {
            public DateTime Date { get; private set; }
            public DateEventArgs(DateTime date) { Date = date; }
        }
    }
}
