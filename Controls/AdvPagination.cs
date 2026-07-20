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
    /// <summary>
    /// 페이지 번호 버튼이 가로로 나열된 페이지네이션 스트립. 이전/다음 화살표와
    /// 활성·호버·비활성 상태를 가진다. Bootstrap의 <c>.pagination</c>에 대응한다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("CurrentPageChanged")]
    [Description("페이지 번호를 나열하는 페이지네이션 컨트롤입니다.")]
    public class AdvPagination : AdvControlBase
    {
        private enum CellKind { Prev, Page, Next }

        private struct Cell
        {
            public Rectangle Bounds;
            public CellKind Kind;
            public int Page;     // Kind == Page일 때의 페이지 번호(1-based)
            public bool Enabled;
        }

        private const int CellPadH = 10;   // 번호 좌우 여백
        private const int MinCell = 30;     // 셀 최소 폭/높이
        private const int Gap = 4;          // 셀 사이 간격

        private int _pageCount = 1;
        private int _currentPage = 1;
        private int _hover = -1;
        private readonly List<Cell> _cells = new List<Cell>();
        private AdvPaginationOptions _options;

        public event EventHandler CurrentPageChanged;

        public AdvPagination()
        {
            // 스트립 전체는 포커스 대상이 아니다(셀이 클릭 대상). 글로우 여백 예약도 막는다.
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }

        protected override Size DefaultSize
        {
            get { return new Size(240, 34); }
        }

        protected override bool IsClickable
        {
            get { return true; }
        }

        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvPaginationOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvPaginationOptions(this)); }
        }

        [Category("Behavior")]
        [DefaultValue(1)]
        [Description("전체 페이지 수입니다.")]
        public int PageCount
        {
            get { return _pageCount; }
            set
            {
                value = Math.Max(1, value);
                if (_pageCount == value) return;
                _pageCount = value;
                if (_currentPage > _pageCount) _currentPage = _pageCount;
                RebuildCells();
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(1)]
        [Description("현재 선택된 페이지(1부터 시작)입니다.")]
        public int CurrentPage
        {
            get { return _currentPage; }
            set
            {
                value = Math.Min(Math.Max(1, value), _pageCount);
                if (_currentPage == value) return;
                _currentPage = value;
                RebuildCells();     // Prev/Next의 활성 여부가 현재 페이지에 달려 있다
                Invalidate();
                var h = CurrentPageChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        private void RebuildCells()
        {
            _cells.Clear();
            var frame = FrameBounds;
            if (frame.Width <= 0 || frame.Height <= 0) return;

            int x = frame.Left;
            int h = frame.Height;

            AddCell(ref x, CellKind.Prev, 0, MinCell, h, _currentPage > 1);

            for (int p = 1; p <= _pageCount; p++)
            {
                var size = TextRenderer.MeasureText(p.ToString(), Font);
                int w = Math.Max(MinCell, size.Width + CellPadH * 2);
                AddCell(ref x, CellKind.Page, p, w, h, true);
            }

            AddCell(ref x, CellKind.Next, 0, MinCell, h, _currentPage < _pageCount);
        }

        private void AddCell(ref int x, CellKind kind, int page, int w, int h, bool enabled)
        {
            _cells.Add(new Cell
            {
                Bounds = new Rectangle(x, FrameBounds.Top, w, h),
                Kind = kind,
                Page = page,
                Enabled = enabled
            });
            x += w + Gap;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_cells.Count == 0) RebuildCells();

            int bw = EffectiveBorderWidth;

            for (int i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                bool active = cell.Kind == CellKind.Page && cell.Page == _currentPage;
                bool hovered = i == _hover && cell.Enabled && !active;

                Color fill, border, fore;
                if (!cell.Enabled)
                {
                    fill = theme.DisabledFill; border = theme.Border; fore = theme.TextDisabled;
                }
                else if (active)
                {
                    fill = theme.Accent; border = theme.Accent; fore = theme.OnAccent;
                }
                else if (hovered)
                {
                    fill = theme.SurfaceHover; border = theme.BorderHover; fore = theme.Text;
                }
                else
                {
                    fill = theme.Surface; border = theme.Border; fore = theme.Text;
                }

                AdvFrameRenderer.Draw(g, cell.Bounds, theme, EffectiveCorners, bw,
                                      fill, Color.Empty, border, null, null, EffectiveBorderDash);

                if (cell.Kind == CellKind.Page)
                {
                    TextRenderer.DrawText(g, cell.Page.ToString(), Font, cell.Bounds, fore,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
                else
                {
                    var dir = cell.Kind == CellKind.Prev
                            ? AdvGraphics.ChevronDirection.Left
                            : AdvGraphics.ChevronDirection.Right;
                    AdvGraphics.DrawChevron(g, cell.Bounds, dir, fore, 8, 5, 1.6f, 0);
                }
            }

            base.OnPaint(e);
        }

        private int HitTest(Point p)
        {
            for (int i = 0; i < _cells.Count; i++)
                if (_cells[i].Bounds.Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int hit = HitTest(e.Location);
            if (hit != _hover)
            {
                _hover = hit;
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

            int hit = HitTest(e.Location);
            if (hit < 0) return;

            var cell = _cells[hit];
            if (!cell.Enabled) return;

            switch (cell.Kind)
            {
                case CellKind.Prev: CurrentPage = _currentPage - 1; break;
                case CellKind.Next: CurrentPage = _currentPage + 1; break;
                default: CurrentPage = cell.Page; break;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            RebuildCells();
            Invalidate();
            base.OnResize(e);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            RebuildCells();
            Invalidate();
            base.OnFontChanged(e);
        }

        protected override void OnThemeChanged()
        {
            RebuildCells();
            base.OnThemeChanged();
        }
    }

    /// <summary>AdvPagination이 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvPaginationOptions : AdvOptions
    {
        internal AdvPaginationOptions(AdvPagination owner) : base(owner.Styling, owner.Palette)
        {
        }
    }
}
