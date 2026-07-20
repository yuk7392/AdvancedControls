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
    [DefaultProperty("CurrentPage")]
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
        private const int MaxPages = 10000; // 병적 입력 방어(전 페이지 즉시 생성으로 인한 UI 정지/OOM)

        private int _pageCount = 1;
        private int _currentPage = 1;
        private int _hover = -1;
        private int _focusIndex = -1;
        private readonly List<Cell> _cells = new List<Cell>();
        private AdvPaginationOptions _options;

        public event EventHandler CurrentPageChanged;

        public AdvPagination()
        {
            // 셀 단위로 키보드 포커스를 옮기므로 컨트롤을 포커스 가능하게 한다.
            // 포커스 표시는 셀별 링으로 직접 그리므로 전체 글로우 여백은 예약하지 않는다(레이아웃 밀림 방지).
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
            Styling.ShowFocusGlow = false;
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
                value = Math.Min(Math.Max(1, value), MaxPages);
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

            if (_focusIndex >= _cells.Count) _focusIndex = -1;
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

                // 키보드 포커스가 놓인 셀에 포커스 링을 그린다.
                if (Focused && i == _focusIndex && cell.Enabled)
                {
                    var rr = Rectangle.Inflate(cell.Bounds, -1, -1);
                    using (var path = AdvGraphics.CreateRoundedRect(rr, EffectiveCorners))
                    using (var pen = new Pen(theme.FocusRing, 1.5f))
                        g.DrawPath(pen, path);
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
                // 셀은 여러 개의 독립 클릭 영역이므로 활성 셀 위에서만 손 커서를 켠다.
                Cursor = (hit >= 0 && _cells[hit].Enabled && UseHandCursor) ? Cursors.Hand : Cursors.Default;
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

            if (!Focused) Focus();

            int hit = HitTest(e.Location);
            if (hit < 0) return;

            if (_cells[hit].Enabled) SetFocusIndex(hit);
            ActivateCell(hit);
        }

        private void ActivateCell(int i)
        {
            if (i < 0 || i >= _cells.Count) return;
            var cell = _cells[i];
            if (!cell.Enabled) return;

            switch (cell.Kind)
            {
                case CellKind.Prev: CurrentPage = _currentPage - 1; break;
                case CellKind.Next: CurrentPage = _currentPage + 1; break;
                default: CurrentPage = cell.Page; break;
            }
        }

        private void SetFocusIndex(int i)
        {
            if (_focusIndex == i) return;
            _focusIndex = i;
            Invalidate();
        }

        private void MoveFocus(int dir)
        {
            int n = _cells.Count;
            for (int j = _focusIndex + dir; j >= 0 && j < n; j += dir)
                if (_cells[j].Enabled) { SetFocusIndex(j); return; }
        }

        private void FocusEdge(int dir)   // +1: 첫 활성 셀, -1: 마지막 활성 셀
        {
            int n = _cells.Count;
            if (dir > 0) { for (int j = 0; j < n; j++) if (_cells[j].Enabled) { SetFocusIndex(j); return; } }
            else { for (int j = n - 1; j >= 0; j--) if (_cells[j].Enabled) { SetFocusIndex(j); return; } }
        }

        private int ActivePageCellIndex()
        {
            for (int i = 0; i < _cells.Count; i++)
                if (_cells[i].Kind == CellKind.Page && _cells[i].Page == _currentPage) return i;
            return -1;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Home:
                case Keys.End:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left: MoveFocus(-1); e.Handled = true; break;
                case Keys.Right: MoveFocus(1); e.Handled = true; break;
                case Keys.Home: FocusEdge(1); e.Handled = true; break;
                case Keys.End: FocusEdge(-1); e.Handled = true; break;
                case Keys.Enter:
                case Keys.Space: ActivateCell(_focusIndex); e.Handled = true; break;
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            if (_cells.Count == 0) RebuildCells();
            if (_focusIndex < 0 || _focusIndex >= _cells.Count || !_cells[_focusIndex].Enabled)
            {
                int a = ActivePageCellIndex();
                if (a >= 0) _focusIndex = a; else FocusEdge(1);
            }
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
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
