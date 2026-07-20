using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>클릭된 경로 항목 정보.</summary>
    public class AdvBreadcrumbClickedEventArgs : EventArgs
    {
        public int Index { get; private set; }
        public string Text { get; private set; }

        public AdvBreadcrumbClickedEventArgs(int index, string text)
        {
            Index = index;
            Text = text;
        }
    }

    /// <summary>
    /// 현재 위치까지의 경로를 구분자로 이은 가로 링크 목록. 마지막 항목은 링크가 아닌
    /// 현재 페이지 글자다. Bootstrap의 <c>.breadcrumb</c>에 대응한다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("ItemClicked")]
    [Description("경로를 구분자로 이어 보여주는 브레드크럼입니다.")]
    public class AdvBreadcrumb : AdvControlBase
    {
        private readonly List<string> _items = new List<string>();
        private readonly List<Rectangle> _itemRects = new List<Rectangle>();
        private string _separator = " / ";
        private int _hover = -1;
        private AdvBreadcrumbOptions _options;

        public event EventHandler<AdvBreadcrumbClickedEventArgs> ItemClicked;

        public AdvBreadcrumb()
        {
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }

        protected override Size DefaultSize
        {
            get { return new Size(240, 28); }
        }

        protected override bool IsClickable
        {
            get { return true; }
        }

        [Category("Behavior")]
        [Description("경로 항목들입니다. 마지막 항목은 현재 위치(비링크)로 표시됩니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string[] Items
        {
            get { return _items.ToArray(); }
            set
            {
                _items.Clear();
                if (value != null) _items.AddRange(value);
                InvalidateLayout();
            }
        }

        [Category("Appearance")]
        [DefaultValue(" / ")]
        [Description("항목 사이에 놓는 구분자입니다.")]
        public string Separator
        {
            get { return _separator; }
            set
            {
                value = value ?? string.Empty;
                if (_separator == value) return;
                _separator = value;
                InvalidateLayout();
            }
        }

        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvBreadcrumbOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvBreadcrumbOptions(this)); }
        }

        private void InvalidateLayout()
        {
            _itemRects.Clear();
            Invalidate();
        }

        private void Layout()
        {
            _itemRects.Clear();
            var frame = FrameBounds;
            if (frame.Width <= 0 || frame.Height <= 0) return;

            int x = frame.Left;
            for (int i = 0; i < _items.Count; i++)
            {
                var size = TextRenderer.MeasureText(_items[i], Font, new Size(int.MaxValue, int.MaxValue),
                                                    TextFormatFlags.NoPrefix);
                _itemRects.Add(new Rectangle(x, frame.Top, size.Width, frame.Height));
                x += size.Width;

                if (i < _items.Count - 1)
                {
                    var sep = TextRenderer.MeasureText(_separator, Font, new Size(int.MaxValue, int.MaxValue),
                                                       TextFormatFlags.NoPrefix);
                    x += sep.Width;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_itemRects.Count != _items.Count) Layout();

            const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;

            for (int i = 0; i < _items.Count; i++)
            {
                var rect = _itemRects[i];
                bool isLast = i == _items.Count - 1;

                Color color = !Enabled
                    ? theme.TextDisabled
                    : (isLast ? theme.Text : theme.Accent);

                TextRenderer.DrawText(g, _items[i], Font, rect, color, flags);

                // 호버된 링크에는 밑줄을 긋는다.
                if (Enabled && !isLast && i == _hover)
                {
                    var size = TextRenderer.MeasureText(_items[i], Font, new Size(int.MaxValue, int.MaxValue),
                                                        TextFormatFlags.NoPrefix);
                    int by = rect.Top + (rect.Height + size.Height) / 2 - 1;
                    using (var pen = new Pen(color, 1f))
                        g.DrawLine(pen, rect.Left, by, rect.Left + size.Width, by);
                }

                if (!isLast)
                {
                    var sepRect = new Rectangle(rect.Right, rect.Top,
                        TextRenderer.MeasureText(_separator, Font, new Size(int.MaxValue, int.MaxValue),
                                                 TextFormatFlags.NoPrefix).Width, rect.Height);
                    TextRenderer.DrawText(g, _separator, Font, sepRect, theme.TextMuted, flags);
                }
            }

            base.OnPaint(e);
        }

        private int HitTestLink(Point p)
        {
            if (_itemRects.Count != _items.Count) Layout();
            // 마지막 항목은 링크가 아니므로 제외한다.
            for (int i = 0; i < _itemRects.Count - 1; i++)
                if (_itemRects[i].Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int hit = HitTestLink(e.Location);
            if (hit != _hover)
            {
                _hover = hit;
                Cursor = hit >= 0 && UseHandCursor ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hover != -1) { _hover = -1; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            int hit = HitTestLink(e.Location);
            if (hit < 0) return;

            var h = ItemClicked;
            if (h != null) h(this, new AdvBreadcrumbClickedEventArgs(hit, _items[hit]));
        }

        protected override void OnResize(EventArgs e)
        {
            InvalidateLayout();
            base.OnResize(e);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            InvalidateLayout();
            base.OnFontChanged(e);
        }

        protected override void OnThemeChanged()
        {
            InvalidateLayout();
            base.OnThemeChanged();
        }
    }

    /// <summary>AdvBreadcrumb가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvBreadcrumbOptions : AdvOptions
    {
        internal AdvBreadcrumbOptions(AdvBreadcrumb owner) : base(owner.Styling, owner.Palette)
        {
        }
    }
}
