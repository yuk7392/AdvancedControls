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
    /// 색 선택 드롭다운. 현재 색 스와치와 헥스 값을 보여주고, 누르면
    /// <see cref="AdvColorPickerPanel"/>을 담은 팝업이 열린다(달력 피커와 같은 구조).
    /// 팝업에서 고르는 동안 실시간으로 <see cref="Value"/>가 따라오고,
    /// 팔레트 클릭·헥스 확정 시 팝업이 닫힌다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("ValueChanged")]
    [DefaultProperty("AdvancedControlOptions")]
    [Description("누르면 컬러 피커 팝업이 열리는 색 선택 드롭다운입니다.")]
    public class AdvColorPicker : AdvControlBase
    {
        private const int ArrowAreaWidth = 18;
        private const int SwatchW = 22;

        private Color _value = Color.Black;
        private ToolStripDropDown _popup;
        private ToolStripControlHost _host;
        private AdvColorPickerPanel _panel;
        private AdvColorPickerOptions _options;

        /// <summary>선택 색이 바뀌면 발생한다(팝업에서 고르는 중에도 실시간).</summary>
        [Category("Behavior")]
        [Description("선택 색이 바뀌면 발생합니다.")]
        public event EventHandler ValueChanged;

        public AdvColorPicker()
        {
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }

        protected override Size DefaultSize
        {
            get { return new Size(140, 34); }
        }

        protected override Padding DefaultPadding
        {
            get { return new Padding(8, 4, 8, 4); }
        }

        protected override bool IsClickable
        {
            get { return true; }
        }

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvColorPickerOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvColorPickerOptions(this)); }
        }

        /// <summary>현재 선택된 색(알파 포함).</summary>
        [Category("Appearance")]
        [Description("현재 선택된 색입니다(알파 포함).")]
        public Color Value
        {
            get { return _value; }
            set
            {
                if (value.IsEmpty) value = Color.Black;
                if (_value.ToArgb() == value.ToArgb()) return;
                _value = value;
                if (_panel != null && _panel.Color.ToArgb() != value.ToArgb()) _panel.Color = value;
                Invalidate();
                var h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }
        public bool ShouldSerializeValue() { return _value.ToArgb() != Color.Black.ToArgb(); }
        public void ResetValue() { Value = Color.Black; }

        /// <summary>팝업이 열려 있는지.</summary>
        [Browsable(false)]
        public bool IsDroppedDown
        {
            get { return _popup != null && _popup.Visible; }
        }

        /// <summary>최소 높이: 글자·스와치가 눌리지 않을 만큼.</summary>
        protected override Size MinimumContentSize
        {
            get { return new Size(0, ChromeSize.Height + Math.Max(Font.Height, AdvGraphics.Scale(this, 18))); }
        }

        // ── 팝업 ──────────────────────────────────────────────────────

        public void ShowDropDown()
        {
            if (IsDroppedDown || !Enabled) return;

            EnsurePopup();
            _panel.Color = _value;
            _popup.BackColor = EffectiveTheme.InputBackground;

            var anchor = PointToScreen(new Point(FrameBounds.Left, FrameBounds.Bottom));
            _popup.Show(anchor);
            Invalidate();
        }

        public void HideDropDown()
        {
            if (_popup != null && _popup.Visible) _popup.Close();
        }

        private void EnsurePopup()
        {
            if (_popup != null) return;

            _panel = new AdvColorPickerPanel();
            _panel.ColorChanged += PanelColorChanged;
            _panel.ColorCommitted += PanelColorCommitted;

            _host = new ToolStripControlHost(_panel);
            _host.Padding = Padding.Empty;
            _host.Margin = Padding.Empty;
            _host.AutoSize = false;
            _host.Size = _panel.Size;

            _popup = new ToolStripDropDown();
            _popup.AutoSize = false;
            _popup.Padding = Padding.Empty;
            _popup.Margin = Padding.Empty;
            _popup.DropShadowEnabled = true;
            _popup.Size = _panel.Size;
            _popup.Items.Add(_host);
            _popup.Closed += PopupClosed;
        }

        private void PanelColorChanged(object sender, EventArgs e) { Value = _panel.Color; }
        private void PanelColorCommitted(object sender, EventArgs e) { HideDropDown(); }
        private void PopupClosed(object sender, ToolStripDropDownClosedEventArgs e) { Invalidate(); }

        // ── 입력 ──────────────────────────────────────────────────────

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            if (IsDroppedDown) HideDropDown(); else ShowDropDown();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Down: case Keys.F4: case Keys.Space: case Keys.Enter:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Down:
                case Keys.F4:
                case Keys.Space:
                case Keys.Enter:
                    if (!IsDroppedDown) { ShowDropDown(); e.Handled = true; }
                    break;
                case Keys.Escape:
                    if (IsDroppedDown) { HideDropDown(); e.Handled = true; }
                    break;
            }
        }

        // ── 그리기 ────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            var bounds = FrameBounds;

            Color border;
            if (!Enabled) border = theme.Border;
            else if (ShowsFocusVisual || IsDroppedDown) border = theme.BorderFocus;
            else border = AdvGraphics.Blend(theme.Border, theme.BorderHover, HoverAmount);

            AdvFrameRenderer.Draw(g, bounds, theme, EffectiveCorners, EffectiveBorderWidth,
                                  Enabled ? theme.InputBackground : theme.InputBackgroundDisabled,
                                  Color.Empty, border, CurrentGlow, CurrentElevation, EffectiveBorderDash);

            var c = ContentBounds;
            int arrowW = AdvGraphics.Scale(this, ArrowAreaWidth);
            int swW = AdvGraphics.Scale(this, SwatchW);
            int swH = Math.Min(c.Height - 2, AdvGraphics.Scale(this, 18));

            // 색 스와치(알파가 있으면 체커보드가 비쳐 보인다)
            var sw = new Rectangle(c.Left, c.Top + (c.Height - swH) / 2, swW, swH);
            DrawSwatch(g, sw, theme);

            // 헥스 텍스트
            var textRect = Rectangle.FromLTRB(sw.Right + 6, c.Top, c.Right - arrowW, c.Bottom);
            if (textRect.Width > 0)
            {
                TextRenderer.DrawText(g, AdvColorPickerPanel.ToHex(_value), Font, textRect,
                    Enabled ? theme.Text : theme.TextDisabled,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                  | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }

            // 펼침 화살표(콤보와 동일한 셰브런)
            var arrowRect = new Rectangle(c.Right - arrowW, c.Top, arrowW, c.Height);
            AdvGraphics.DrawChevron(g, this, arrowRect,
                IsDroppedDown ? AdvGraphics.ChevronDirection.Up : AdvGraphics.ChevronDirection.Down,
                Enabled ? theme.TextMuted : theme.TextDisabled, 8, 5, 1.6f, 0);

            base.OnPaint(e);
        }

        private void DrawSwatch(Graphics g, Rectangle r, AdvTheme theme)
        {
            if (_value.A < 255)
            {
                // 간단 체커보드(두 칸 교차) — 알파가 보이게
                int half = Math.Max(2, r.Height / 2);
                using (var light = new SolidBrush(Color.White)) g.FillRectangle(light, r);
                using (var dark = new SolidBrush(Color.FromArgb(204, 204, 204)))
                {
                    for (int x = r.Left, i = 0; x < r.Right; x += half, i++)
                    {
                        int y = i % 2 == 0 ? r.Top : r.Top + half;
                        dark.Color = Color.FromArgb(204, 204, 204);
                        g.FillRectangle(dark, x, y, Math.Min(half, r.Right - x), Math.Min(half, r.Bottom - y));
                    }
                }
            }

            using (var b = new SolidBrush(_value)) g.FillRectangle(b, r);
            using (var pen = new Pen(theme.Border)) g.DrawRectangle(pen, r.Left, r.Top, r.Width - 1, r.Height - 1);
        }

        protected override void OnThemeChanged()
        {
            base.OnThemeChanged();
            if (_popup != null) _popup.BackColor = EffectiveTheme.InputBackground;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_popup != null)
                {
                    _popup.Closed -= PopupClosed;
                    _popup.Dispose();
                    _popup = null;
                }
                if (_panel != null)
                {
                    _panel.ColorChanged -= PanelColorChanged;
                    _panel.ColorCommitted -= PanelColorCommitted;
                    _panel.Dispose();
                    _panel = null;
                }
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>AdvColorPicker가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvColorPickerOptions : AdvOptions
    {
        private readonly AdvColorPicker _owner;

        internal AdvColorPickerOptions(AdvColorPicker owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [Description("현재 선택된 색입니다(알파 포함).")]
        public Color Value
        {
            get { return _owner.Value; }
            set { _owner.Value = value; }
        }
        public bool ShouldSerializeValue() { return _owner.ShouldSerializeValue(); }
        public void ResetValue() { _owner.ResetValue(); }
    }
}
