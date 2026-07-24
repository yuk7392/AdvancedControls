using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using AdvancedControls.Controls.Internal;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>
    /// 테마를 따르는 날짜 선택 컨트롤. 달력까지 직접 그려 다크 테마에서도 어긋나지 않는다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("ValueChanged")]
    [DefaultProperty("AdvancedControlOptions")]
    [Description("테마를 따르는 날짜 선택 컨트롤입니다.")]
    public class AdvDateTimePicker : AdvControlBase
    {
        private const int ArrowAreaWidth = 22;
        private const int CalendarWidth = 252;
        private const int CalendarHeight = 250;

        private DateTime _value = DateTime.Today;
        private DateTime _minDate = new DateTime(1753, 1, 1);
        private DateTime _maxDate = new DateTime(9998, 12, 31);
        private string _format = "yyyy-MM-dd";

        private ToolStripDropDown _popup;
        private AdvDateTimePickerOptions _options;

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvDateTimePickerOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvDateTimePickerOptions(this)); }
        }
        private AdvCalendar _calendar;
        private ToolStripControlHost _host;
        private bool _showTodayButton = true;

        [Category("Behavior")]
        [Description("선택한 날짜가 바뀔 때 발생합니다.")]
        public event EventHandler ValueChanged;

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(true)]
        [Description("달력 하단에 \"오늘\" 버튼을 표시할지 여부입니다.")]
        public bool ShowTodayButton
        {
            get { return _showTodayButton; }
            set { _showTodayButton = value; }
        }

        public AdvDateTimePicker()
        {
            TabStop = true;
        }

        protected override Size DefaultSize
        {
            get { return new Size(160, 34); }
        }

        protected override Padding DefaultPadding
        {
            get { return new Padding(8, 4, 8, 4); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [Description("선택한 날짜입니다.")]
        public DateTime Value
        {
            get { return _value; }
            set
            {
                var v = value.Date;
                if (v < _minDate) v = _minDate;
                if (v > _maxDate) v = _maxDate;
                if (_value == v) return;

                _value = v;
                if (_calendar != null) _calendar.Selected = v;

                Invalidate();

                var handler = ValueChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [Description("고를 수 있는 가장 이른 날짜입니다.")]
        public DateTime MinDate
        {
            get { return _minDate; }
            set
            {
                _minDate = value.Date;

                // 최소가 최대를 넘으면 이후 범위 검사가 항상 실패한다
                if (_maxDate < _minDate) _maxDate = _minDate;
                if (_calendar != null) _calendar.MinDate = _minDate;
                Value = _value;
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [Description("고를 수 있는 가장 늦은 날짜입니다.")]
        public DateTime MaxDate
        {
            get { return _maxDate; }
            set
            {
                _maxDate = value.Date;

                if (_minDate > _maxDate) _minDate = _maxDate;
                if (_calendar != null) _calendar.MaxDate = _maxDate;
                Value = _value;
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue("yyyy-MM-dd")]
        [Description("날짜를 보여줄 형식입니다.")]
        public string Format
        {
            get { return _format; }
            set
            {
                value = string.IsNullOrEmpty(value) ? "yyyy-MM-dd" : value;
                if (_format == value) return;
                _format = value;
                Invalidate();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsDroppedDown
        {
            get { return _popup != null && _popup.Visible; }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override string Text
        {
            get { return _value.ToString(_format, CultureInfo.CurrentCulture); }
            set { /* 형식이 정해진 표시라 직접 넣지 않는다 */ }
        }

        public bool ShouldSerializeValue() { return _value != DateTime.Today; }
        public bool ShouldSerializeMinDate() { return _minDate != new DateTime(1753, 1, 1); }
        public bool ShouldSerializeMaxDate() { return _maxDate != new DateTime(9998, 12, 31); }

        #region 달력 팝업

        public void ShowDropDown()
        {
            if (IsDroppedDown || !Enabled) return;

            EnsurePopup();

            _calendar.Theme = EffectiveTheme;
            _calendar.Font = Font;
            _calendar.MinDate = _minDate;
            _calendar.MaxDate = _maxDate;
            _calendar.ShowToday = _showTodayButton;
            _calendar.Selected = _value;

            // "오늘" 버튼 유무에 따라 높이가 달라지므로 열 때마다 크기를 맞춘다
            int h = CalendarHeight + (_showTodayButton ? AdvCalendar.TodayFooterHeight : 0);
            var size = new Size(CalendarWidth, h);
            _calendar.Size = size;
            _host.Size = size;
            _popup.Size = size;

            var anchor = PointToScreen(new Point(FrameBounds.Left, FrameBounds.Bottom));
            _popup.Show(anchor);

            Invalidate();
        }

        public void HideDropDown()
        {
            if (_popup != null && _popup.Visible) _popup.Close();
        }

        /// <summary>
        /// 팝업은 한 번만 만들고 재사용한다. 열 때마다 새로 만들면 닫기 처리가
        /// 겹칠 때 이전 인스턴스가 남는다.
        /// </summary>
        private void EnsurePopup()
        {
            if (_popup != null) return;

            _calendar = new AdvCalendar(EffectiveTheme, _value);
            _calendar.Size = new Size(CalendarWidth, CalendarHeight);
            _calendar.DateChosen += CalendarDateChosen;

            _host = new ToolStripControlHost(_calendar);
            _host.Padding = Padding.Empty;
            _host.Margin = Padding.Empty;
            _host.AutoSize = false;
            _host.Size = _calendar.Size;

            _popup = new ToolStripDropDown();
            _popup.AutoSize = false;
            _popup.Padding = Padding.Empty;
            _popup.Margin = Padding.Empty;
            _popup.DropShadowEnabled = true;
            _popup.BackColor = EffectiveTheme.InputBackground;
            _popup.Size = _calendar.Size;
            _popup.Items.Add(_host);
            _popup.Closed += PopupClosed;
        }

        private void CalendarDateChosen(object sender, AdvCalendar.DateEventArgs e)
        {
            Value = e.Date;
            HideDropDown();
        }

        private void PopupClosed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            Invalidate();
        }

        #endregion

        private Rectangle IconBounds
        {
            get
            {
                var c = ContentBounds;
                return new Rectangle(c.Right - ArrowAreaWidth, c.Top, ArrowAreaWidth, c.Height);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = FrameBounds;

            Color border;
            if (!Enabled) border = theme.Border;
            else if (ShowsFocusVisual || IsDroppedDown) border = theme.BorderFocus;
            else border = AdvGraphics.Blend(theme.Border, theme.BorderHover, HoverAmount);

            AdvFrameRenderer.Draw(g, bounds, theme, EffectiveCorners, EffectiveBorderWidth,
                                  Enabled ? theme.InputBackground : theme.InputBackgroundDisabled,
                                  Color.Empty, border, CurrentGlow, CurrentElevation,
                                  EffectiveBorderDash);

            var c = ContentBounds;
            var textRect = new Rectangle(c.Left, c.Top,
                                         Math.Max(0, c.Width - ArrowAreaWidth - 4), c.Height);

            TextRenderer.DrawText(g, Text, Font, textRect,
                Enabled ? theme.Text : theme.TextDisabled,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter
              | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            DrawCalendarIcon(g, IconBounds, Enabled ? theme.TextMuted : theme.TextDisabled);

            base.OnPaint(e);
        }

        private static void DrawCalendarIcon(Graphics g, Rectangle area, Color color)
        {
            int w = 14, h = 13;
            int x = area.Left + (area.Width - w) / 2;
            int y = area.Top + (area.Height - h) / 2;

            using (var pen = new Pen(color, 1.3f))
            {
                var body = new Rectangle(x, y + 2, w, h - 2);
                g.DrawRectangle(pen, body);

                // 위쪽 고리 두 개와 머리글 구분선
                g.DrawLine(pen, x + 4, y, x + 4, y + 3);
                g.DrawLine(pen, x + w - 4, y, x + w - 4, y + 3);
                g.DrawLine(pen, x, y + 6, x + w, y + 6);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (!Focused) Focus();

                if (IsDroppedDown) HideDropDown();
                else ShowDropDown();
            }
            base.OnMouseDown(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.PageUp:
                case Keys.PageDown:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Down:
                    if (e.Alt) ShowDropDown();
                    else Value = _value.AddDays(1);
                    e.Handled = true;
                    break;

                case Keys.Up:
                    Value = _value.AddDays(-1);
                    e.Handled = true;
                    break;

                case Keys.PageDown:
                    Value = SafeAddMonths(1);
                    e.Handled = true;
                    break;

                case Keys.PageUp:
                    Value = SafeAddMonths(-1);
                    e.Handled = true;
                    break;

                case Keys.F4:
                    if (IsDroppedDown) HideDropDown(); else ShowDropDown();
                    e.Handled = true;
                    break;

                case Keys.Escape:
                    if (IsDroppedDown) { HideDropDown(); e.Handled = true; }
                    break;
            }

            base.OnKeyDown(e);
        }

        /// <summary>DateTime의 표현 범위를 넘으면 예외가 나므로 미리 막는다.</summary>
        private DateTime SafeAddMonths(int months)
        {
            try { return _value.AddMonths(months); }
            catch (ArgumentOutOfRangeException) { return _value; }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (Enabled && !IsDroppedDown)
                Value = _value.AddDays(e.Delta > 0 ? 1 : -1);

            base.OnMouseWheel(e);
        }

        protected override void OnThemeChanged()
        {
            if (_calendar != null) _calendar.Theme = EffectiveTheme;
            if (_popup != null) _popup.BackColor = EffectiveTheme.InputBackground;
            base.OnThemeChanged();
        }

        // ── 접근성 ────────────────────────────────────────────────────

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new DatePickerAccessibleObject(this);
        }

        private sealed class DatePickerAccessibleObject : ControlAccessibleObject
        {
            private readonly AdvDateTimePicker _owner;
            public DatePickerAccessibleObject(AdvDateTimePicker owner) : base(owner) { _owner = owner; }

            public override AccessibleRole Role { get { return AccessibleRole.ComboBox; } }
            public override string Value { get { return _owner.Text; } set { } }

            public override AccessibleStates State
            {
                get
                {
                    var s = base.State | AccessibleStates.HasPopup;
                    s |= _owner.IsDroppedDown ? AccessibleStates.Expanded : AccessibleStates.Collapsed;
                    if (!_owner.Enabled) s |= AccessibleStates.Unavailable;
                    return s;
                }
            }

            public override string DefaultAction { get { return _owner.IsDroppedDown ? "접기" : "펼치기"; } }
            public override void DoDefaultAction()
            {
                if (_owner.IsDroppedDown) _owner.HideDropDown(); else _owner.ShowDropDown();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _popup != null)
            {
                _calendar.DateChosen -= CalendarDateChosen;
                _popup.Closed -= PopupClosed;
                _popup.Dispose();
                _popup = null;
                _calendar = null;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>AdvDateTimePicker가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvDateTimePickerOptions : AdvOptions
    {
        private readonly AdvDateTimePicker _owner;

        internal AdvDateTimePickerOptions(AdvDateTimePicker owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [Description("선택한 날짜입니다.")]
        public DateTime Value
        {
            get { return _owner.Value; }
            set { _owner.Value = value; }
        }
        public bool ShouldSerializeValue() { return _owner.ShouldSerializeValue(); }

        [DefaultValue(true)]
        [Description("달력 하단에 \"오늘\" 버튼을 표시할지 여부입니다.")]
        public bool ShowTodayButton
        {
            get { return _owner.ShowTodayButton; }
            set { _owner.ShowTodayButton = value; }
        }

        [Description("고를 수 있는 가장 이른 날짜입니다.")]
        public DateTime MinDate
        {
            get { return _owner.MinDate; }
            set { _owner.MinDate = value; }
        }
        public bool ShouldSerializeMinDate() { return _owner.ShouldSerializeMinDate(); }

        [Description("고를 수 있는 가장 늦은 날짜입니다.")]
        public DateTime MaxDate
        {
            get { return _owner.MaxDate; }
            set { _owner.MaxDate = value; }
        }
        public bool ShouldSerializeMaxDate() { return _owner.ShouldSerializeMaxDate(); }

        [DefaultValue("yyyy-MM-dd")]
        [Description("날짜를 보여줄 형식입니다.")]
        public string Format
        {
            get { return _owner.Format; }
            set { _owner.Format = value; }
        }
    }
}
