using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Controls.Internal;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>선택된 메뉴 항목 정보.</summary>
    public class AdvDropDownItemClickedEventArgs : EventArgs
    {
        public int Index { get; private set; }
        public string Text { get; private set; }

        public AdvDropDownItemClickedEventArgs(int index, string text)
        {
            Index = index;
            Text = text;
        }
    }

    /// <summary>
    /// 누르면 아래로 액션 목록이 떠오르는 드롭다운 메뉴 버튼. 폼 밖까지 뜨는 팝업과
    /// 바깥 클릭·Esc 자동 닫기는 기존 콤보 팝업 인프라(<see cref="AdvComboPopup"/>)를 재사용한다.
    /// Bootstrap의 <c>.dropdown</c>에 대응한다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("ItemClicked")]
    [Description("액션 목록이 떠오르는 드롭다운 메뉴 버튼입니다.")]
    public class AdvDropDownMenu : AdvControlBase
    {
        private readonly List<object> _itemObjects = new List<object>();
        private string[] _items = new string[0];
        private int _maxDropDownItems = 8;
        private AdvComboPopup _popup;
        private int _closedAt = -1000;      // 팝업이 마지막으로 닫힌 시각(틱). 토글 재열림 방지에 쓴다
        private AdvDropDownMenuOptions _options;

        public event EventHandler<AdvDropDownItemClickedEventArgs> ItemClicked;

        public AdvDropDownMenu()
        {
            TabStop = true;
        }

        protected override Size DefaultSize
        {
            get { return new Size(140, 34); }
        }

        protected override Padding DefaultPadding
        {
            get { return new Padding(12, 4, 8, 4); }
        }

        protected override bool IsClickable
        {
            get { return true; }
        }

        [Category("Behavior")]
        [Description("드롭다운에 나열할 메뉴 항목들입니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string[] Items
        {
            get { return (string[])_items.Clone(); }
            set
            {
                _items = value ?? new string[0];
                _itemObjects.Clear();
                foreach (var s in _items) _itemObjects.Add(s);
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(8)]
        [Description("스크롤 없이 한 번에 보여줄 최대 항목 수입니다.")]
        public int MaxDropDownItems
        {
            get { return _maxDropDownItems; }
            set { _maxDropDownItems = Math.Max(1, value); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsDroppedDown
        {
            get { return _popup != null && _popup.Visible; }
        }

        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvDropDownMenuOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvDropDownMenuOptions(this)); }
        }

        private int ItemHeight
        {
            get { return Font.Height + 8; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = FrameBounds;
            var corners = EffectiveCorners;
            int bw = EffectiveBorderWidth;
            float t = HoverAmount;

            Color fill, border, fore;
            if (!Enabled)
            {
                fill = theme.DisabledFill; border = theme.Border; fore = theme.TextDisabled;
            }
            else
            {
                fill = (IsPressed || IsDroppedDown)
                     ? theme.SurfacePressed
                     : AdvGraphics.Blend(theme.Surface, theme.SurfaceHover, t);
                border = (ShowsFocusVisual || IsDroppedDown)
                       ? theme.BorderFocus
                       : AdvGraphics.Blend(theme.Border, theme.BorderHover, t);
                fore = theme.Text;
            }

            AdvFrameRenderer.Draw(g, bounds, theme, corners, bw, fill, theme.SurfaceGradientEnd, border,
                                  CurrentGlow, CurrentElevation, EffectiveBorderDash);

            var content = new Rectangle(
                bounds.Left + bw + Padding.Left, bounds.Top + bw + Padding.Top,
                Math.Max(0, bounds.Width - bw * 2 - Padding.Horizontal),
                Math.Max(0, bounds.Height - bw * 2 - Padding.Vertical));

            const int chevW = 16;
            var chevRect = new Rectangle(content.Right - chevW, content.Top, chevW, content.Height);
            var textRect = new Rectangle(content.Left, content.Top,
                                         Math.Max(0, content.Width - chevW - 4), content.Height);

            if (!string.IsNullOrEmpty(Text))
                TextRenderer.DrawText(g, Text, Font, textRect, fore,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            AdvGraphics.DrawChevron(g, chevRect, AdvGraphics.ChevronDirection.Down, fore, 8, 5, 1.6f, 0);

            base.OnPaint(e);
        }

        private void EnsurePopup()
        {
            if (_popup != null) return;

            _popup = new AdvComboPopup(_itemObjects, EffectiveTheme, Font, ItemHeight);
            _popup.List.TextProvider = ItemToText;
            _popup.ItemChosen += PopupItemChosen;
            _popup.Closed += PopupClosed;
        }

        private static string ItemToText(object o)
        {
            return o == null ? string.Empty : o.ToString();
        }

        public void ShowDropDown()
        {
            if (IsDroppedDown || _itemObjects.Count == 0 || !Enabled) return;

            EnsurePopup();
            _popup.ApplyTheme(EffectiveTheme);
            _popup.List.Font = Font;
            _popup.List.ItemHeight = ItemHeight;
            _popup.List.SelectedIndex = -1;
            _popup.List.HoverIndex = -1;
            _popup.SetSize(Math.Max(FrameBounds.Width, 80), _itemObjects.Count, _maxDropDownItems);
            _popup.Show(this, new Point(0, Height));
            Invalidate();
        }

        public void HideDropDown()
        {
            if (_popup != null && _popup.Visible) _popup.Close();
        }

        private void PopupItemChosen(object sender, AdvDropDownList.ItemEventArgs e)
        {
            int idx = e.Index;
            HideDropDown();

            var h = ItemClicked;
            if (h != null)
            {
                string text = idx >= 0 && idx < _items.Length ? _items[idx] : null;
                h(this, new AdvDropDownItemClickedEventArgs(idx, text));
            }
        }

        private void PopupClosed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            _closedAt = Environment.TickCount;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            if (!Focused) Focus();

            // 팝업이 열려 있을 때 트리거를 누르면 ToolStripDropDown이 바깥 클릭으로 먼저 닫는다.
            // 그 직후의 이 클릭까지 다시 열지 않도록, 방금 닫혔으면 아무 것도 하지 않는다.
            if (Environment.TickCount - _closedAt < 250) return;

            if (IsDroppedDown) HideDropDown();
            else ShowDropDown();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter
                || (e.KeyCode == Keys.Down && e.Alt))
            {
                if (IsDroppedDown) HideDropDown(); else ShowDropDown();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnThemeChanged()
        {
            if (_popup != null) _popup.ApplyTheme(EffectiveTheme);
            base.OnThemeChanged();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _popup != null)
            {
                _popup.ItemChosen -= PopupItemChosen;
                _popup.Closed -= PopupClosed;
                _popup.Dispose();
                _popup = null;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>AdvDropDownMenu가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvDropDownMenuOptions : AdvOptions
    {
        private readonly AdvDropDownMenu _owner;

        internal AdvDropDownMenuOptions(AdvDropDownMenu owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(8)]
        [Description("스크롤 없이 한 번에 보여줄 최대 항목 수입니다.")]
        public int MaxDropDownItems
        {
            get { return _owner.MaxDropDownItems; }
            set { _owner.MaxDropDownItems = value; }
        }
    }
}
