using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>행 선택 방식.</summary>
    public enum AdvGridSelectionMode
    {
        /// <summary>한 번에 한 행만 선택.</summary>
        Single,
        /// <summary>Ctrl·Shift로 여러 행 선택.</summary>
        MultiExtended
    }

    /// <summary>그리드의 한 열. 머리글·폭·정렬·정렬가능 여부를 담는다.</summary>
    public class AdvGridColumn
    {
        /// <summary>열 최소 폭(px). 음수·0 폭이면 좌표·스크롤이 어긋나므로 하한을 둔다.</summary>
        public const int MinWidth = 20;

        private int _width;

        public string Header { get; set; }

        /// <summary>열 폭(px). <see cref="MinWidth"/> 미만은 자동으로 올려 붙인다(직접 대입·드래그 공통).</summary>
        public int Width
        {
            get { return _width; }
            set { _width = value < MinWidth ? MinWidth : value; }
        }

        public HorizontalAlignment Alignment { get; set; }
        public bool Sortable { get; set; }

        public AdvGridColumn(string header, int width, HorizontalAlignment alignment)
        {
            Header = header;
            Width = width;      // 세터가 하한 클램프
            Alignment = alignment;
            Sortable = true;
        }
    }

    /// <summary>열 관련 그리드 이벤트 인자.</summary>
    public class AdvGridColumnEventArgs : EventArgs
    {
        public int ColumnIndex { get; private set; }
        public AdvGridColumn Column { get; private set; }
        public AdvGridColumnEventArgs(int index, AdvGridColumn column) { ColumnIndex = index; Column = column; }
    }

    /// <summary>셀 관련 그리드 이벤트 인자.</summary>
    public class AdvGridCellEventArgs : EventArgs
    {
        public int RowIndex { get; private set; }
        public int ColumnIndex { get; private set; }
        public AdvGridCellEventArgs(int row, int col) { RowIndex = row; ColumnIndex = col; }
    }

    /// <summary>
    /// 밑바닥부터 직접 그리는 테마 데이터 그리드. 표준 DataGridView를 감싸지 않고
    /// 헤더·행·스크롤·선택·정렬을 모두 커스텀 그리기로 처리한다.
    /// v1: 문자열 셀, 세로·가로 스크롤, 행 선택(단일/다중), 호버, 교차행, 헤더 정렬, 열 폭 조절.
    /// </summary>
    [ToolboxItem(true)]
    [Description("밑바닥부터 직접 그리는 테마 데이터 그리드입니다.")]
    [DefaultEvent("SelectionChanged")]
    public class AdvDataGrid : AdvControlBase
    {
        private sealed class GridRow
        {
            public readonly string[] Cells;
            public bool Selected;
            public GridRow(string[] cells) { Cells = cells; }
        }

        private readonly List<AdvGridColumn> _columns = new List<AdvGridColumn>();
        private readonly List<GridRow> _rows = new List<GridRow>();

        private int _rowHeight = 30;
        private int _headerHeight = 34;
        private const int ScrollSize = 11;          // 스크롤바 두께
        private const int ResizeGrip = 4;           // 열 경계 잡는 폭
        private const int MinThumb = 24;

        private int _scrollX, _scrollY;
        private int _hoverRow = -1;
        private int _sortCol = -1;
        private bool _sortAsc = true;
        private int _anchor = -1;
        private int _focusRow = -1;   // 키보드 탐색 기준 행(Shift 범위선택의 이동 끝)

        private AdvGridSelectionMode _selectionMode = AdvGridSelectionMode.Single;
        private bool _showGridLines = true;
        private bool _alternatingRows = true;

        // 상호작용 상태
        private int _resizeCol = -1;                // 드래그로 폭 조절 중인 열
        private int _resizeStartX, _resizeStartW;
        private int _dragThumb;                     // 0=없음, 1=세로, 2=가로
        private int _dragThumbOffset;
        private bool _vHot, _hHot;                  // 스크롤바 호버

        // 매 그리기 직전에 계산되는 레이아웃 값
        private bool _vBar, _hBar;
        private Rectangle _viewport, _headerRect, _vBarRect, _hBarRect;

        private AdvDataGridOptions _options;

        /// <summary>선택된 행이 바뀌면 발생한다.</summary>
        [Category("Behavior")]
        [Description("선택된 행이 바뀌면 발생합니다.")]
        public event EventHandler SelectionChanged;

        /// <summary>정렬 열이나 방향이 바뀌면 발생한다.</summary>
        [Category("Behavior")]
        [Description("정렬 열이나 방향이 바뀌면 발생합니다.")]
        public event EventHandler SortChanged;

        /// <summary>열 폭 드래그가 끝나면 발생한다.</summary>
        [Category("Behavior")]
        [Description("열 폭 드래그가 끝나면 발생합니다.")]
        public event EventHandler<AdvGridColumnEventArgs> ColumnWidthChanged;

        /// <summary>셀을 더블클릭하면 발생한다.</summary>
        [Category("Behavior")]
        [Description("셀을 더블클릭하면 발생합니다.")]
        public event EventHandler<AdvGridCellEventArgs> CellDoubleClick;

        public AdvDataGrid()
        {
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
            Styling.ShowFocusGlow = false;   // 큰 컨테이너라 글로우 여백을 잡지 않는다
            Styling.Radius = 8;
            Size = new Size(360, 220);
        }

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvDataGridOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvDataGridOptions(this)); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvGridSelectionMode.Single)]
        [Description("행 선택 방식입니다. Single=한 행, MultiExtended=Ctrl·Shift로 여러 행.")]
        public AdvGridSelectionMode SelectionMode
        {
            get { return _selectionMode; }
            set
            {
                if (_selectionMode == value) return;
                _selectionMode = value;
                // Single로 바꾸며 선택이 실제로 축소되면 통지한다(예전엔 조용히 접었다)
                if (value == AdvGridSelectionMode.Single && KeepSingleSelection()) RaiseSelectionChanged();
                Invalidate();
            }
        }

        [Browsable(false)]
        [DefaultValue(true)]
        [Description("셀 사이 격자선을 그릴지 여부입니다.")]
        public bool ShowGridLines
        {
            get { return _showGridLines; }
            set { if (_showGridLines == value) return; _showGridLines = value; Invalidate(); }
        }

        [Browsable(false)]
        [DefaultValue(true)]
        [Description("짝수·홀수 행 배경을 다르게 칠할지 여부입니다.")]
        public bool AlternatingRowColors
        {
            get { return _alternatingRows; }
            set { if (_alternatingRows == value) return; _alternatingRows = value; Invalidate(); }
        }

        [Category("Layout")]
        [DefaultValue(30)]
        [Description("데이터 행 높이(px)입니다.")]
        public int RowHeight
        {
            get { return _rowHeight; }
            set
            {
                value = value < 16 ? 16 : value;
                if (_rowHeight == value) return;
                _rowHeight = value;
                Invalidate();
            }
        }

        [Category("Layout")]
        [DefaultValue(34)]
        [Description("머리글 행 높이(px)입니다.")]
        public int HeaderHeight
        {
            get { return _headerHeight; }
            set
            {
                value = value < 16 ? 16 : value;
                if (_headerHeight == value) return;
                _headerHeight = value;
                Invalidate();
            }
        }

        // ── 데이터 API ────────────────────────────────────────────────

        /// <summary>열을 추가한다.</summary>
        public AdvGridColumn AddColumn(string header, int width, HorizontalAlignment alignment)
        {
            var col = new AdvGridColumn(header, width, alignment);
            _columns.Add(col);
            Invalidate();
            return col;
        }

        public AdvGridColumn AddColumn(string header, int width)
        {
            return AddColumn(header, width, HorizontalAlignment.Left);
        }

        /// <summary>행을 추가한다. 셀 개수가 열 수와 달라도 남거나 빈 칸으로 처리한다.</summary>
        public void AddRow(params string[] cells)
        {
            _rows.Add(new GridRow(cells ?? new string[0]));
            ClampScroll();
            Invalidate();
        }

        /// <summary>행 하나를 지운다. 선택된 행이었으면 SelectionChanged를 올린다.</summary>
        public void RemoveRowAt(int index)
        {
            if (index < 0 || index >= _rows.Count) return;
            bool wasSelected = _rows[index].Selected;
            _rows.RemoveAt(index);
            if (_hoverRow >= _rows.Count) _hoverRow = -1;
            if (_focusRow >= _rows.Count) _focusRow = -1;
            _anchor = -1;
            ClampScroll();
            Invalidate();
            if (wasSelected) RaiseSelectionChanged();
        }

        /// <summary>한 셀 값을 갱신한다(전체 재구성 없이).</summary>
        public void SetCell(int row, int col, string value)
        {
            if (row < 0 || row >= _rows.Count) return;
            var cells = _rows[row].Cells;
            if (col < 0 || col >= cells.Length) return;
            cells[col] = value;
            Invalidate();
        }

        /// <summary>모든 행을 지운다(열은 유지).</summary>
        public void ClearRows()
        {
            _rows.Clear();
            _hoverRow = _anchor = -1;
            _scrollY = 0;
            Invalidate();
            RaiseSelectionChanged();
        }

        [Browsable(false)]
        public int RowCount { get { return _rows.Count; } }

        [Browsable(false)]
        public int ColumnCount { get { return _columns.Count; } }

        /// <summary>선택된 행 인덱스들(현재 정렬 순서 기준, 오름차순).</summary>
        [Browsable(false)]
        public int[] SelectedIndices
        {
            get
            {
                var list = new List<int>();
                for (int i = 0; i < _rows.Count; i++)
                    if (_rows[i].Selected) list.Add(i);
                return list.ToArray();
            }
        }

        /// <summary>단일 선택 모드의 선택 행. 없으면 -1.</summary>
        [Browsable(false)]
        public int SelectedIndex
        {
            get
            {
                for (int i = 0; i < _rows.Count; i++)
                    if (_rows[i].Selected) return i;
                return -1;
            }
            set
            {
                SelectOnlyRow(value);
                _anchor = value;
                _focusRow = value;   // 키보드 탐색이 프로그램 선택 위치에서 이어지도록
                EnsureRowVisible(value);
                Invalidate();
                RaiseSelectionChanged();
            }
        }

        /// <summary>행 i의 셀 값(열 j). 범위를 벗어나면 빈 문자열.</summary>
        public string GetCell(int row, int col)
        {
            if (row < 0 || row >= _rows.Count) return string.Empty;
            var cells = _rows[row].Cells;
            return col >= 0 && col < cells.Length ? (cells[col] ?? string.Empty) : string.Empty;
        }

        // ── 레이아웃 계산 ─────────────────────────────────────────────

        private int TotalColumnsWidth
        {
            get { int w = 0; foreach (var c in _columns) w += c.Width; return w; }
        }

        private int TotalRowsHeight { get { return _rows.Count * _rowHeight; } }

        /// <summary>테두리 안쪽(프레임에서 테두리 두께를 뺀) 영역.</summary>
        private Rectangle InnerBounds
        {
            get
            {
                var f = FrameBounds;
                int bw = EffectiveBorderWidth;
                return new Rectangle(f.Left + bw, f.Top + bw,
                                     Math.Max(0, f.Width - bw * 2), Math.Max(0, f.Height - bw * 2));
            }
        }

        /// <summary>매 그리기·히트테스트 전에 스크롤바 유무와 각 영역을 다시 잡는다.</summary>
        private void EnsureLayout()
        {
            var inner = InnerBounds;
            int bodyH = inner.Height - _headerHeight;

            // 스크롤바 유무는 서로 영향을 준다. 한 번 재확인하면 충분하다.
            bool v = TotalRowsHeight > bodyH;
            bool h = TotalColumnsWidth > inner.Width - (v ? ScrollSize : 0);
            if (h) v = TotalRowsHeight > bodyH - ScrollSize;
            _vBar = v; _hBar = h;

            int vw = v ? ScrollSize : 0;
            int hh = h ? ScrollSize : 0;

            _headerRect = new Rectangle(inner.Left, inner.Top, Math.Max(0, inner.Width - vw), _headerHeight);
            _viewport = new Rectangle(inner.Left, inner.Top + _headerHeight,
                                      Math.Max(0, inner.Width - vw), Math.Max(0, bodyH - hh));
            _vBarRect = v
                ? new Rectangle(inner.Right - ScrollSize, inner.Top + _headerHeight, ScrollSize, Math.Max(0, bodyH - hh))
                : Rectangle.Empty;
            _hBarRect = h
                ? new Rectangle(inner.Left, inner.Bottom - ScrollSize, Math.Max(0, inner.Width - vw), ScrollSize)
                : Rectangle.Empty;

            ClampScroll();
        }

        private int MaxScrollY { get { return Math.Max(0, TotalRowsHeight - _viewport.Height); } }
        private int MaxScrollX { get { return Math.Max(0, TotalColumnsWidth - _viewport.Width); } }

        private void ClampScroll()
        {
            if (_scrollY < 0) _scrollY = 0;
            if (_scrollY > MaxScrollY) _scrollY = MaxScrollY;
            if (_scrollX < 0) _scrollX = 0;
            if (_scrollX > MaxScrollX) _scrollX = MaxScrollX;
        }

        private int ColumnLeft(int index)
        {
            int x = _viewport.Left - _scrollX;
            for (int i = 0; i < index && i < _columns.Count; i++) x += _columns[i].Width;
            return x;
        }

        // ── 둥근 모서리 클립 ─────────────────────────────────────────

        /// <summary>
        /// 헤더·스크롤바 등 사각 요소의 모서리가 둥근 테두리 밖으로 튀지 않도록
        /// 컨트롤 전체를 둥근 모양으로 잘라 낸다. 테두리는 OnPaint가 그리므로 1px 넉넉히 잡는다.
        /// </summary>
        private Rectangle _regionClip = Rectangle.Empty;   // 마지막으로 Region을 만든 사각형(리사이즈 중 재생성 방지)

        private void ApplyRoundedRegion()
        {
            if (!IsHandleCreated) return;
            var clip = Rectangle.Inflate(FrameBounds, 1, 1);
            if (Region != null && clip == _regionClip) return;   // 크기·위치가 그대로면 GDI Region을 다시 만들지 않는다
            _regionClip = clip;
            var old = Region;
            using (var path = AdvGraphics.CreateRoundedRect(clip, EffectiveCorners))
                Region = new Region(path);
            if (old != null) old.Dispose();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyRoundedRegion();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRoundedRegion();
        }

        protected override void OnThemeChanged()
        {
            base.OnThemeChanged();
            _regionClip = Rectangle.Empty;   // 반경·테마가 바뀌면 크기가 같아도 모서리가 달라지므로 강제 재생성
            ApplyRoundedRegion();
        }

        // ── 그리기 ────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            var frame = FrameBounds;
            if (frame.Width <= 0 || frame.Height <= 0) return;

            EnsureLayout();

            // 프레임(면 + 둥근 1px 테두리)
            AdvFrameRenderer.Draw(g, frame, theme, EffectiveCorners, EffectiveBorderWidth,
                                  theme.Surface, Color.Empty, theme.Border,
                                  null, CurrentElevation, EffectiveBorderDash);

            var state = g.Save();
            g.SetClip(_viewport);
            DrawRows(g, theme);
            g.Restore(state);

            DrawGridLinesAndColumns(g, theme);   // 자체적으로 뷰포트/헤더로 클립
            DrawHeader(g, theme);

            if (_vBar) DrawScrollBar(g, theme, _vBarRect, true);
            if (_hBar) DrawScrollBar(g, theme, _hBarRect, false);

            // 세로·가로 스크롤바가 만나는 우하단 빈 칸을 면색으로 덮는다
            if (_vBar && _hBar)
                using (var b = new SolidBrush(theme.SurfaceHover))
                    g.FillRectangle(b, new Rectangle(_vBarRect.Left, _hBarRect.Top, ScrollSize, ScrollSize));
        }

        private void DrawRows(Graphics g, AdvTheme theme)
        {
            if (_rows.Count == 0 || _columns.Count == 0) return;

            int first = _scrollY / _rowHeight;
            int y = _viewport.Top - (_scrollY % _rowHeight);

            Color altColor = AdvGraphics.Blend(theme.Surface, theme.SurfaceHover, 0.5f);

            for (int r = first; r < _rows.Count && y < _viewport.Bottom; r++, y += _rowHeight)
            {
                var rowRect = new Rectangle(_viewport.Left, y, _viewport.Width, _rowHeight);
                var row = _rows[r];

                Color bg;
                if (row.Selected) bg = theme.Accent;
                else if (r == _hoverRow) bg = theme.SurfaceHover;
                else if (_alternatingRows && (r & 1) == 1) bg = altColor;
                else bg = theme.Surface;

                if (bg != theme.Surface)
                    using (var b = new SolidBrush(bg))
                        g.FillRectangle(b, rowRect);

                Color fg = row.Selected ? theme.OnAccent : theme.Text;

                int cx = _viewport.Left - _scrollX;
                for (int c = 0; c < _columns.Count; c++)
                {
                    var col = _columns[c];
                    var cellRect = new Rectangle(cx, y, col.Width, _rowHeight);
                    cx += col.Width;

                    if (cellRect.Right <= _viewport.Left || cellRect.Left >= _viewport.Right) continue;

                    var textRect = Rectangle.Intersect(Rectangle.Inflate(cellRect, -8, 0), _viewport);
                    if (textRect.Width <= 0) continue;

                    TextRenderer.DrawText(g, GetCell(r, c), Font, textRect, fg,
                        AlignFlags(col.Alignment) | TextFormatFlags.VerticalCenter
                      | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
            }
        }

        private void DrawGridLinesAndColumns(Graphics g, AdvTheme theme)
        {
            if (!_showGridLines || _columns.Count == 0) return;

            var state = g.Save();
            g.SetClip(_viewport);
            using (var pen = new Pen(theme.Border))
            {
                // 가로선(행 경계)
                int first = _scrollY / _rowHeight;
                int y = _viewport.Top - (_scrollY % _rowHeight);
                for (int r = first; r <= _rows.Count && y <= _viewport.Bottom; r++, y += _rowHeight)
                    g.DrawLine(pen, _viewport.Left, y, _viewport.Right, y);

                // 세로선(열 경계)
                int cx = _viewport.Left - _scrollX;
                for (int c = 0; c < _columns.Count; c++)
                {
                    cx += _columns[c].Width;
                    if (cx > _viewport.Left && cx < _viewport.Right)
                        g.DrawLine(pen, cx, _viewport.Top, cx, _viewport.Bottom);
                }
            }
            g.Restore(state);
        }

        private void DrawHeader(Graphics g, AdvTheme theme)
        {
            using (var b = new SolidBrush(theme.SurfaceHover))
                g.FillRectangle(b, _headerRect);

            var state = g.Save();
            g.SetClip(_headerRect);

            using (var headerFont = new Font(Font, FontStyle.Bold))
            using (var pen = new Pen(theme.Border))
            {
                int cx = _headerRect.Left - _scrollX;
                for (int c = 0; c < _columns.Count; c++)
                {
                    var col = _columns[c];
                    var cellRect = new Rectangle(cx, _headerRect.Top, col.Width, _headerRect.Height);
                    cx += col.Width;

                    if (cellRect.Right <= _headerRect.Left || cellRect.Left >= _headerRect.Right) continue;

                    // 정렬 화살표 자리를 오른쪽에 비운다
                    int arrow = (_sortCol == c) ? 14 : 0;
                    var textRect = Rectangle.Intersect(
                        new Rectangle(cellRect.Left + 8, cellRect.Top, Math.Max(0, col.Width - 16 - arrow), cellRect.Height),
                        _headerRect);
                    if (textRect.Width > 0)
                        TextRenderer.DrawText(g, col.Header, headerFont, textRect, theme.Text,
                            AlignFlags(col.Alignment) | TextFormatFlags.VerticalCenter
                          | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                    if (_sortCol == c)
                    {
                        var arrowArea = new Rectangle(cellRect.Right - 16, cellRect.Top, 12, cellRect.Height);
                        AdvGraphics.DrawChevron(g, arrowArea,
                            _sortAsc ? AdvGraphics.ChevronDirection.Up : AdvGraphics.ChevronDirection.Down,
                            theme.TextMuted, 8, 4, 1.4f, 0);
                    }

                    // 열 경계선
                    if (cellRect.Right > _headerRect.Left && cellRect.Right < _headerRect.Right)
                        g.DrawLine(pen, cellRect.Right, _headerRect.Top, cellRect.Right, _headerRect.Bottom);
                }
                // 헤더 아래 구분선
                g.DrawLine(pen, _headerRect.Left, _headerRect.Bottom - 1, _headerRect.Right, _headerRect.Bottom - 1);
            }
            g.Restore(state);
        }

        private void DrawScrollBar(Graphics g, AdvTheme theme, Rectangle track, bool vertical)
        {
            using (var tb = new SolidBrush(theme.SurfaceHover))
                g.FillRectangle(tb, track);

            var thumb = ThumbRect(track, vertical);
            if (thumb.IsEmpty) return;

            bool hot = vertical ? _vHot : _hHot;
            var t = Rectangle.Inflate(thumb, -2, -2);
            if (t.Width <= 0 || t.Height <= 0) return;
            using (var path = AdvGraphics.CreateRoundedRect(t, new AdvCorners(Math.Min(t.Width, t.Height) / 2)))
            using (var b = new SolidBrush(hot || _dragThumb != 0 ? theme.TextMuted : theme.Border))
                g.FillPath(b, path);
        }

        /// <summary>스크롤바 썸(thumb) 사각형. 비례 크기·위치.</summary>
        private Rectangle ThumbRect(Rectangle track, bool vertical)
        {
            if (vertical)
            {
                int total = TotalRowsHeight;
                if (total <= _viewport.Height || track.Height <= 0) return Rectangle.Empty;
                int th = Math.Max(MinThumb, (int)((long)track.Height * _viewport.Height / total));
                int max = track.Height - th;
                int pos = MaxScrollY == 0 ? 0 : (int)((long)max * _scrollY / MaxScrollY);
                return new Rectangle(track.Left, track.Top + pos, track.Width, th);
            }
            else
            {
                int total = TotalColumnsWidth;
                if (total <= _viewport.Width || track.Width <= 0) return Rectangle.Empty;
                int tw = Math.Max(MinThumb, (int)((long)track.Width * _viewport.Width / total));
                int max = track.Width - tw;
                int pos = MaxScrollX == 0 ? 0 : (int)((long)max * _scrollX / MaxScrollX);
                return new Rectangle(track.Left + pos, track.Top, tw, track.Height);
            }
        }

        private static TextFormatFlags AlignFlags(HorizontalAlignment a)
        {
            switch (a)
            {
                case HorizontalAlignment.Center: return TextFormatFlags.HorizontalCenter;
                case HorizontalAlignment.Right: return TextFormatFlags.Right;
                default: return TextFormatFlags.Left;
            }
        }

        // ── 마우스 ────────────────────────────────────────────────────

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            EnsureLayout();

            if (e.Button != MouseButtons.Left) return;

            // 1) 스크롤바 썸 드래그
            if (_vBar && _vBarRect.Contains(e.Location))
            {
                var thumb = ThumbRect(_vBarRect, true);
                if (thumb.Contains(e.Location)) { _dragThumb = 1; _dragThumbOffset = e.Y - thumb.Top; }
                else PageScroll(true, e.Y < thumb.Top);
                return;
            }
            if (_hBar && _hBarRect.Contains(e.Location))
            {
                var thumb = ThumbRect(_hBarRect, false);
                if (thumb.Contains(e.Location)) { _dragThumb = 2; _dragThumbOffset = e.X - thumb.Left; }
                else PageScroll(false, e.X < thumb.Left);
                return;
            }

            // 2) 헤더: 열 경계면 리사이즈, 아니면 정렬
            if (_headerRect.Contains(e.Location))
            {
                int edge = HitColumnEdge(e.X);
                if (edge >= 0) { _resizeCol = edge; _resizeStartX = e.X; _resizeStartW = _columns[edge].Width; }
                else { int col = HitColumn(e.X); if (col >= 0) SortByColumn(col); }
                return;
            }

            // 3) 데이터 행 선택
            if (_viewport.Contains(e.Location))
            {
                int row = RowAt(e.Y);
                if (row >= 0) HandleRowClick(row);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_dragThumb == 1) { DragThumb(e.Y, true); return; }
            if (_dragThumb == 2) { DragThumb(e.X, false); return; }
            if (_resizeCol >= 0)
            {
                int w = _resizeStartW + (e.X - _resizeStartX);
                _columns[_resizeCol].Width = w;   // 세터가 AdvGridColumn.MinWidth로 클램프
                EnsureLayout();
                Invalidate();
                return;
            }

            // 커서·호버 갱신
            bool overEdge = _headerRect.Contains(e.Location) && HitColumnEdge(e.X) >= 0;
            Cursor = overEdge ? Cursors.VSplit : Cursors.Default;

            bool vHot = _vBar && ThumbRect(_vBarRect, true).Contains(e.Location);
            bool hHot = _hBar && ThumbRect(_hBarRect, false).Contains(e.Location);
            if (vHot != _vHot || hHot != _hHot) { _vHot = vHot; _hHot = hHot; Invalidate(); }

            int row = _viewport.Contains(e.Location) ? RowAt(e.Y) : -1;
            if (row != _hoverRow) { _hoverRow = row; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragThumb = 0;
            if (_resizeCol >= 0)
            {
                int col = _resizeCol;
                _resizeCol = -1;
                var h = ColumnWidthChanged;
                if (h != null && col < _columns.Count) h(this, new AdvGridColumnEventArgs(col, _columns[col]));
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (!_viewport.Contains(e.Location)) return;
            int row = RowAt(e.Y);
            int col = HitColumn(e.X);
            if (row >= 0 && col >= 0)
            {
                var h = CellDoubleClick;
                if (h != null) h(this, new AdvGridCellEventArgs(row, col));
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Cursor = Cursors.Default;
            if (_hoverRow != -1 || _vHot || _hHot)
            {
                _hoverRow = -1; _vHot = _hHot = false;
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            EnsureLayout();
            if (_vBar)
            {
                int before = _scrollY;
                _scrollY -= Math.Sign(e.Delta) * _rowHeight * 3;
                ClampScroll();
                if (_scrollY != before) Invalidate();
            }
        }

        private void DragThumb(int mouse, bool vertical)
        {
            var track = vertical ? _vBarRect : _hBarRect;
            var thumb = ThumbRect(track, vertical);
            if (vertical)
            {
                int max = track.Height - thumb.Height;
                int pos = mouse - _dragThumbOffset - track.Top;
                _scrollY = max <= 0 ? 0 : (int)((long)MaxScrollY * Clamp(pos, 0, max) / max);
            }
            else
            {
                int max = track.Width - thumb.Width;
                int pos = mouse - _dragThumbOffset - track.Left;
                _scrollX = max <= 0 ? 0 : (int)((long)MaxScrollX * Clamp(pos, 0, max) / max);
            }
            ClampScroll();
            Invalidate();
        }

        private void PageScroll(bool vertical, bool up)
        {
            if (vertical) _scrollY += (up ? -1 : 1) * _viewport.Height;
            else _scrollX += (up ? -1 : 1) * _viewport.Width;
            ClampScroll();
            Invalidate();
        }

        private static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        /// <summary>세로 위치 y가 가리키는 행 인덱스. 없으면 -1.</summary>
        private int RowAt(int y)
        {
            if (!_viewport.Contains(new Point(_viewport.Left + 1, y))) return -1;
            int row = (y - _viewport.Top + _scrollY) / _rowHeight;
            return row >= 0 && row < _rows.Count ? row : -1;
        }

        private int HitColumn(int x)
        {
            int cx = _headerRect.Left - _scrollX;
            for (int c = 0; c < _columns.Count; c++)
            {
                if (x >= cx && x < cx + _columns[c].Width) return c;
                cx += _columns[c].Width;
            }
            return -1;
        }

        /// <summary>x가 어떤 열의 오른쪽 경계(잡는 폭 이내)에 걸리면 그 열 인덱스.</summary>
        private int HitColumnEdge(int x)
        {
            int cx = _headerRect.Left - _scrollX;
            for (int c = 0; c < _columns.Count; c++)
            {
                cx += _columns[c].Width;
                if (Math.Abs(x - cx) <= ResizeGrip) return c;
            }
            return -1;
        }

        // ── 선택·정렬 ─────────────────────────────────────────────────

        private void HandleRowClick(int row)
        {
            var mod = ModifierKeys;
            if (_selectionMode == AdvGridSelectionMode.Single)
            {
                SelectOnlyRow(row);
                _anchor = row;
            }
            else
            {
                bool ctrl = (mod & Keys.Control) != 0;
                bool shift = (mod & Keys.Shift) != 0;
                if (shift && _anchor >= 0) SelectRange(_anchor, row);
                else if (ctrl) { _rows[row].Selected = !_rows[row].Selected; _anchor = row; }
                else { SelectOnlyRow(row); _anchor = row; }
            }
            _focusRow = row;
            EnsureRowVisible(row);
            Invalidate();
            RaiseSelectionChanged();
        }

        private void SelectOnlyRow(int row)
        {
            for (int i = 0; i < _rows.Count; i++) _rows[i].Selected = (i == row);
        }

        private void SelectRange(int a, int b)
        {
            int lo = Math.Min(a, b), hi = Math.Max(a, b);
            for (int i = 0; i < _rows.Count; i++) _rows[i].Selected = (i >= lo && i <= hi);
        }

        /// <summary>맨 앞 선택 하나만 남기고 나머지를 해제한다. 실제로 해제가 있었으면 true.</summary>
        private bool KeepSingleSelection()
        {
            bool seen = false, changed = false;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Selected)
                {
                    if (seen) { _rows[i].Selected = false; changed = true; }
                    else seen = true;
                }
            }
            return changed;
        }

        private void SortByColumn(int col)
        {
            if (col < 0 || col >= _columns.Count || !_columns[col].Sortable) return;

            if (_sortCol == col) _sortAsc = !_sortAsc;
            else { _sortCol = col; _sortAsc = true; }

            bool numeric = true;
            foreach (var r in _rows)
            {
                string s = col < r.Cells.Length ? r.Cells[col] : null;
                if (string.IsNullOrEmpty(s)) continue;
                double d;
                if (!double.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out d))
                { numeric = false; break; }
            }

            int dir = _sortAsc ? 1 : -1;
            _rows.Sort((x, y) =>
            {
                string sx = col < x.Cells.Length ? (x.Cells[col] ?? "") : "";
                string sy = col < y.Cells.Length ? (y.Cells[col] ?? "") : "";
                int cmp;
                if (numeric)
                {
                    double dx, dy;
                    double.TryParse(sx, NumberStyles.Any, CultureInfo.CurrentCulture, out dx);
                    double.TryParse(sy, NumberStyles.Any, CultureInfo.CurrentCulture, out dy);
                    cmp = dx.CompareTo(dy);
                }
                else cmp = string.Compare(sx, sy, StringComparison.CurrentCultureIgnoreCase);
                return cmp * dir;
            });

            _anchor = -1;
            _hoverRow = -1;   // 정렬로 행 순서가 바뀌었으니 옛 인덱스 호버를 지운다
            _focusRow = -1;
            Invalidate();
            var h = SortChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        private void EnsureRowVisible(int row)
        {
            if (row < 0) return;
            EnsureLayout();
            int top = row * _rowHeight;
            int bottom = top + _rowHeight;
            if (top < _scrollY) _scrollY = top;
            else if (bottom > _scrollY + _viewport.Height) _scrollY = bottom - _viewport.Height;
            ClampScroll();
        }

        private void RaiseSelectionChanged()
        {
            var h = SelectionChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        // ── 키보드 ────────────────────────────────────────────────────

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.PageUp:
                case Keys.PageDown:
                case Keys.Home:
                case Keys.End:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_rows.Count == 0) return;

            int cur = (_focusRow >= 0 && _focusRow < _rows.Count) ? _focusRow : SelectedIndex;
            int page = Math.Max(1, _viewport.Height / _rowHeight);
            int next = cur;

            switch (e.KeyCode)
            {
                case Keys.Up: next = cur < 0 ? _rows.Count - 1 : Math.Max(0, cur - 1); break;
                case Keys.Down: next = cur < 0 ? 0 : Math.Min(_rows.Count - 1, cur + 1); break;
                case Keys.PageUp: next = Math.Max(0, (cur < 0 ? 0 : cur) - page); break;
                case Keys.PageDown: next = Math.Min(_rows.Count - 1, (cur < 0 ? 0 : cur) + page); break;
                case Keys.Home: next = 0; break;
                case Keys.End: next = _rows.Count - 1; break;
                default: return;
            }

            if (next != cur)
            {
                // MultiExtended + Shift면 anchor를 유지한 범위 선택, 아니면 단일 선택
                bool shift = (e.Modifiers & Keys.Shift) != 0;
                if (_selectionMode == AdvGridSelectionMode.MultiExtended && shift && _anchor >= 0)
                    SelectRange(_anchor, next);
                else { SelectOnlyRow(next); _anchor = next; }
                _focusRow = next;
                EnsureRowVisible(next);
                Invalidate();
                RaiseSelectionChanged();
            }
            e.Handled = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Region != null) Region.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>AdvDataGrid이 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvDataGridOptions : AdvOptions
    {
        private readonly AdvDataGrid _owner;

        internal AdvDataGridOptions(AdvDataGrid owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(AdvGridSelectionMode.Single)]
        [Description("행 선택 방식입니다. Single=한 행, MultiExtended=Ctrl·Shift로 여러 행.")]
        public AdvGridSelectionMode SelectionMode
        {
            get { return _owner.SelectionMode; }
            set { _owner.SelectionMode = value; }
        }

        [DefaultValue(true)]
        [Description("셀 사이 격자선을 그릴지 여부입니다.")]
        public bool ShowGridLines
        {
            get { return _owner.ShowGridLines; }
            set { _owner.ShowGridLines = value; }
        }

        [DefaultValue(true)]
        [Description("짝수·홀수 행 배경을 다르게 칠할지 여부입니다.")]
        public bool AlternatingRowColors
        {
            get { return _owner.AlternatingRowColors; }
            set { _owner.AlternatingRowColors = value; }
        }
    }
}
