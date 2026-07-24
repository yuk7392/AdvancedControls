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

        /// <summary>선택 체크박스 열이면 true. 텍스트 대신 체크박스를 그리고 클릭하면 행 선택을 토글한다.</summary>
        internal bool IsCheckBox { get; set; }

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
    /// 셀을 그리기 직전에 값에 따라 배경·글자색·폰트·표시 텍스트를 지정할 수 있게 하는 인자.
    /// 한 행 전체를 칠하려면 <see cref="ColumnIndex"/>와 무관하게 매 셀에서 같은 색을 지정한다.
    /// (성능을 위해 그리드가 인스턴스를 재사용하므로 핸들러 밖으로 참조를 보관하지 말 것.)
    /// </summary>
    public class AdvGridCellFormattingEventArgs : EventArgs
    {
        public int RowIndex { get; internal set; }
        public int ColumnIndex { get; internal set; }
        public bool Selected { get; internal set; }
        /// <summary>표시할 텍스트. 바꾸면 원본 데이터는 그대로 두고 화면 표시만 바뀐다(정렬·자동맞춤은 원본 사용).</summary>
        public string Value { get; set; }
        public Color BackColor { get; set; }
        public Color ForeColor { get; set; }
        /// <summary>null이면 그리드 기본 Font.</summary>
        public Font Font { get; set; }
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
        private const int ScrollSize = 10;          // 스크롤바 두께(AdvScrollBar.DefaultWidth와 통일)
        private const int ResizeGrip = 4;           // 열 경계 잡는 폭
        private const int MinThumb = 24;
        private const int HScrollStep = 48;         // 가로 방향키/Shift+휠 한 번의 이동 폭
        private const int CellPadX = 8;             // 셀 좌우 안쪽 여백(텍스트·자동맞춤 공통)

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

        // 인라인 편집
        private bool _readOnly = true;
        private TextBox _editor;
        private int _editRow = -1, _editCol = -1;

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

        /// <summary>셀을 그리기 직전에 발생한다. 값에 따라 배경·글자색·폰트·표시값을 조정할 수 있다.</summary>
        [Category("Behavior")]
        [Description("셀을 그리기 직전에 발생합니다. 조건부 서식에 사용합니다.")]
        public event EventHandler<AdvGridCellFormattingEventArgs> CellFormatting;

        /// <summary>인라인 편집으로 셀 값이 바뀌면 발생한다.</summary>
        [Category("Behavior")]
        [Description("인라인 편집으로 셀 값이 바뀌면 발생합니다.")]
        public event EventHandler<AdvGridCellEventArgs> CellValueChanged;

        // 셀당 새로 만들지 않고 재사용(구독 시 매 페인트·매 셀 호출되므로 할당 억제)
        private readonly AdvGridCellFormattingEventArgs _fmtArgs = new AdvGridCellFormattingEventArgs();

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
        [Description("읽기 전용 여부입니다. false면 더블클릭·F2로 셀을 인라인 편집할 수 있습니다.")]
        public bool ReadOnly
        {
            get { return _readOnly; }
            set { if (_readOnly == value) return; _readOnly = value; if (value) CancelEdit(); }
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

        /// <summary>선택 체크박스 열을 추가한다. 셀 체크박스는 행 선택 상태를 반영하고, 머리글은 전체선택 토글이다.</summary>
        public AdvGridColumn AddCheckBoxColumn(int width)
        {
            var col = new AdvGridColumn(string.Empty, width, HorizontalAlignment.Center) { Sortable = false, IsCheckBox = true };
            _columns.Add(col);
            Invalidate();
            return col;
        }

        public AdvGridColumn AddCheckBoxColumn() { return AddCheckBoxColumn(34); }

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

        /// <summary>
        /// 열 인덱스를 데이터 셀 배열 인덱스로 변환한다. 체크박스 같은 비데이터 열은 셀을
        /// 차지하지 않으므로 건너뛴다(비데이터 열이면 -1). 비데이터 열이 없으면 col과 동일.
        /// </summary>
        private int DataIndex(int col)
        {
            if (col < 0 || col >= _columns.Count || _columns[col].IsCheckBox) return -1;
            int di = 0;
            for (int i = 0; i < col; i++) if (!_columns[i].IsCheckBox) di++;
            return di;
        }

        /// <summary>한 셀 값을 갱신한다(전체 재구성 없이).</summary>
        public void SetCell(int row, int col, string value)
        {
            if (row < 0 || row >= _rows.Count) return;
            int di = DataIndex(col);
            if (di < 0) return;
            var cells = _rows[row].Cells;
            if (di >= cells.Length) return;
            cells[di] = value;
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

        /// <summary>행 i의 셀 값(열 j). 비데이터 열(체크박스)이거나 범위를 벗어나면 빈 문자열.</summary>
        public string GetCell(int row, int col)
        {
            if (row < 0 || row >= _rows.Count) return string.Empty;
            int di = DataIndex(col);
            if (di < 0) return string.Empty;
            var cells = _rows[row].Cells;
            return di < cells.Length ? (cells[di] ?? string.Empty) : string.Empty;
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
            AdvGraphics.UpdateRoundedRegion(this, clip, EffectiveCorners, false, ref _regionClip);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyRoundedRegion();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            CommitEdit();   // 리사이즈로 셀 위치가 바뀌므로 편집을 먼저 마친다
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
            var handler = CellFormatting;   // 구독자 없으면 셀별 서식 비용 0(빠른 경로)

            int first = _scrollY / _rowHeight;
            int y = _viewport.Top - (_scrollY % _rowHeight);

            Color altColor = AdvGraphics.Blend(theme.Surface, theme.SurfaceHover, 0.5f);

            for (int r = first; r < _rows.Count && y < _viewport.Bottom; r++, y += _rowHeight)
            {
                var rowRect = new Rectangle(_viewport.Left, y, _viewport.Width, _rowHeight);
                var row = _rows[r];

                Color rowBg;
                if (row.Selected) rowBg = theme.Accent;
                else if (r == _hoverRow) rowBg = theme.SurfaceHover;
                else if (_alternatingRows && (r & 1) == 1) rowBg = altColor;
                else rowBg = theme.Surface;

                if (rowBg != theme.Surface)
                    using (var b = new SolidBrush(rowBg))
                        g.FillRectangle(b, rowRect);

                Color rowFg = row.Selected ? theme.OnAccent : theme.Text;

                int cx = _viewport.Left - _scrollX;
                for (int c = 0; c < _columns.Count; c++)
                {
                    var col = _columns[c];
                    var cellRect = new Rectangle(cx, y, col.Width, _rowHeight);
                    cx += col.Width;

                    if (cellRect.Right <= _viewport.Left || cellRect.Left >= _viewport.Right) continue;

                    if (col.IsCheckBox) { DrawCheck(g, cellRect, row.Selected, theme); continue; }

                    string text = GetCell(r, c);
                    Color cellFg = rowFg;
                    Font cellFont = Font;

                    if (handler != null)
                    {
                        _fmtArgs.RowIndex = r; _fmtArgs.ColumnIndex = c; _fmtArgs.Selected = row.Selected;
                        _fmtArgs.Value = text; _fmtArgs.BackColor = rowBg; _fmtArgs.ForeColor = rowFg; _fmtArgs.Font = null;
                        handler(this, _fmtArgs);
                        text = _fmtArgs.Value ?? string.Empty;
                        cellFg = _fmtArgs.ForeColor;
                        cellFont = _fmtArgs.Font ?? Font;

                        // 셀 배경이 행 배경과 다르면 그 셀만 덮어 칠한다(뷰포트로 클립)
                        if (_fmtArgs.BackColor != rowBg)
                        {
                            var fillRect = Rectangle.Intersect(cellRect, _viewport);
                            if (fillRect.Width > 0)
                                using (var b = new SolidBrush(_fmtArgs.BackColor))
                                    g.FillRectangle(b, fillRect);
                        }
                    }

                    var inner = Rectangle.Inflate(cellRect, -CellPadX, 0);
                    DrawClippedText(g, text, inner, _viewport, cellFont, cellFg, col.Alignment);
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

        private Font _headerFont;   // 볼드 머리글 폰트 캐시
        private Font HeaderFont { get { return _headerFont ?? (_headerFont = new Font(Font, FontStyle.Bold)); } }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (_headerFont != null) { _headerFont.Dispose(); _headerFont = null; }
            Invalidate();
        }

        private void DrawHeader(Graphics g, AdvTheme theme)
        {
            using (var b = new SolidBrush(theme.SurfaceHover))
                g.FillRectangle(b, _headerRect);

            var state = g.Save();
            g.SetClip(_headerRect);

            var headerFont = HeaderFont;   // 매 페인트 new Font 대신 캐시(폰트 변경 시에만 재생성)
            using (var pen = new Pen(theme.Border))
            {
                int cx = _headerRect.Left - _scrollX;
                for (int c = 0; c < _columns.Count; c++)
                {
                    var col = _columns[c];
                    var cellRect = new Rectangle(cx, _headerRect.Top, col.Width, _headerRect.Height);
                    cx += col.Width;

                    if (cellRect.Right <= _headerRect.Left || cellRect.Left >= _headerRect.Right) continue;

                    // 체크박스 열 머리글 = 전체선택 체크박스(빈칸/체크/부분선택 대시)
                    if (col.IsCheckBox)
                    {
                        const int hbox = 16;
                        var hr = new Rectangle(cellRect.Left + (cellRect.Width - hbox) / 2,
                                               cellRect.Top + (cellRect.Height - hbox) / 2, hbox, hbox);
                        if (hr.Right > _headerRect.Left && hr.Left < _headerRect.Right)
                        {
                            int st = SelectAllState();
                            if (st == 0) DrawCheckBox(g, hr, 0, theme.Surface, theme.Border, theme.Border);
                            else DrawCheckBox(g, hr, st, theme.Accent, theme.OnAccent, theme.Accent);
                        }
                        if (cellRect.Right > _headerRect.Left && cellRect.Right < _headerRect.Right)
                            g.DrawLine(pen, cellRect.Right, _headerRect.Top, cellRect.Right, _headerRect.Bottom);
                        continue;
                    }

                    // 정렬 화살표 자리를 오른쪽에 비운다. 데이터 셀과 같은 부분열 처리로 헤더도 함께 스크롤.
                    int arrow = (_sortCol == c) ? 14 : 0;
                    var hInner = new Rectangle(cellRect.Left + CellPadX, cellRect.Top,
                                               Math.Max(0, col.Width - CellPadX * 2 - arrow), cellRect.Height);
                    DrawClippedText(g, col.Header, hInner, _headerRect, headerFont, theme.Text, col.Alignment);

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

        /// <summary>
        /// 셀/머리글 텍스트를 그린다. clip(뷰포트·헤더) 안에 완전히 들어오면 TextRenderer(선명 + 넘침 …),
        /// 가로 스크롤로 변을 넘은 부분열은 GDI+ DrawString으로 활성 클립을 따라 잘림표 없이 부드럽게 클립한다.
        /// </summary>
        private void DrawClippedText(Graphics g, string text, Rectangle inner, Rectangle clip, Font font,
                                     Color fg, HorizontalAlignment align)
        {
            if (string.IsNullOrEmpty(text) || inner.Width <= 0) return;
            if (inner.Left >= clip.Left && inner.Right <= clip.Right)
                TextRenderer.DrawText(g, text, font, inner, fg,
                    AlignFlags(align) | TextFormatFlags.VerticalCenter
                  | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            else
                DrawCellString(g, text, inner, font, fg, align);
        }

        private void DrawCellString(Graphics g, string text, Rectangle rect, Font font, Color fg, HorizontalAlignment align)
        {
            var prev = g.TextRenderingHint;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (var sf = new StringFormat(StringFormatFlags.NoWrap))
            using (var br = new SolidBrush(fg))
            {
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.None;   // 스크롤 중이므로 잘림표 없이 클립
                sf.Alignment = align == HorizontalAlignment.Center ? StringAlignment.Center
                             : align == HorizontalAlignment.Right ? StringAlignment.Far : StringAlignment.Near;
                g.DrawString(text, font, br, (RectangleF)rect, sf);
            }
            g.TextRenderingHint = prev;
        }

        /// <summary>체크박스 하나. state: 0=빈칸, 1=체크, 2=대시(부분선택).</summary>
        private static void DrawCheckBox(Graphics g, Rectangle box, int state, Color fill, Color mark, Color border)
        {
            var sm = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = AdvGraphics.CreateRoundedRect(box, new AdvCorners(4)))
            {
                if (fill.A > 0) using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                using (var pen = new Pen(border)) g.DrawPath(pen, path);
            }
            var ci = Rectangle.Inflate(box, -4, -4);
            if (state == 1)
                using (var pen = new Pen(mark, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLines(pen, new[]
                    {
                        new Point(ci.Left, ci.Top + ci.Height * 3 / 5),
                        new Point(ci.Left + ci.Width * 2 / 5, ci.Bottom),
                        new Point(ci.Right, ci.Top)
                    });
            else if (state == 2)
                using (var pen = new Pen(mark, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(pen, ci.Left, box.Top + box.Height / 2, ci.Right, box.Top + box.Height / 2);
            g.SmoothingMode = sm;
        }

        /// <summary>셀 체크박스. 선택 행은 강조 배경 위라 외곽선·체크를 OnAccent로 그린다.</summary>
        private void DrawCheck(Graphics g, Rectangle cellRect, bool selected, AdvTheme theme)
        {
            const int box = 16;
            var r = new Rectangle(cellRect.Left + (cellRect.Width - box) / 2,
                                  cellRect.Top + (cellRect.Height - box) / 2, box, box);
            if (r.Right <= _viewport.Left || r.Left >= _viewport.Right) return;   // 가로 스크롤로 벗어나면 스킵
            if (selected)
                DrawCheckBox(g, r, 1, Color.Empty, theme.OnAccent, theme.OnAccent);
            else
                DrawCheckBox(g, r, 0, theme.Surface, theme.Border, theme.Border);
        }

        /// <summary>전체선택 상태. 0=하나도 선택 안 됨, 1=전부, 2=일부.</summary>
        private int SelectAllState()
        {
            if (_rows.Count == 0) return 0;
            int sel = 0;
            foreach (var r in _rows) if (r.Selected) sel++;
            return sel == 0 ? 0 : (sel == _rows.Count ? 1 : 2);
        }

        /// <summary>머리글 체크박스를 눌렀을 때: 전부 선택돼 있으면 해제, 아니면 전부 선택.</summary>
        private void ToggleAll()
        {
            bool target = SelectAllState() != 1;   // 전부 아니면 전부 선택, 전부면 해제
            foreach (var r in _rows) r.Selected = target;
            _anchor = -1;
            Invalidate();
            RaiseSelectionChanged();
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

            // 2) 헤더: 열 경계면 리사이즈, 체크박스 열이면 전체선택, 아니면 정렬
            if (_headerRect.Contains(e.Location))
            {
                int edge = HitColumnEdge(e.X);
                if (edge >= 0) { _resizeCol = edge; _resizeStartX = e.X; _resizeStartW = _columns[edge].Width; }
                else
                {
                    int col = HitColumn(e.X);
                    if (col >= 0)
                    {
                        if (_columns[col].IsCheckBox) ToggleAll();
                        else SortByColumn(col);
                    }
                }
                return;
            }

            // 3) 데이터: 체크박스 열이면 그 행 선택 토글, 아니면 일반 행 선택
            if (_viewport.Contains(e.Location))
            {
                int row = RowAt(e.Y);
                if (row < 0) return;
                int col = HitColumn(e.X);
                if (col >= 0 && _columns[col].IsCheckBox)
                {
                    _rows[row].Selected = !_rows[row].Selected;
                    _anchor = row; _focusRow = row;
                    EnsureRowVisible(row);
                    Invalidate();
                    RaiseSelectionChanged();
                }
                else HandleRowClick(row);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // 캡처가 풀린 뒤 버튼을 안 눌러도 스쳐서 썸/열폭이 이동하는 것을 막는다
            if ((_dragThumb != 0 || _resizeCol >= 0) && (e.Button & MouseButtons.Left) == 0)
            { _dragThumb = 0; _resizeCol = -1; }

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

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            // 드래그 중 캡처가 강제로 풀리면(모달·Alt+Tab 등) 썸·열폭 드래그 상태를 내린다
            if (_dragThumb != 0 || _resizeCol >= 0) { _dragThumb = 0; _resizeCol = -1; Invalidate(); }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            EnsureLayout();

            // 헤더의 열 경계를 더블클릭하면 내용에 맞춰 폭을 자동 조정한다
            if (_headerRect.Contains(e.Location))
            {
                int edge = HitColumnEdge(e.X);
                if (edge >= 0) { AutoFitColumn(edge); return; }
            }

            if (!_viewport.Contains(e.Location)) return;
            int row = RowAt(e.Y);
            int col = HitColumn(e.X);
            if (row >= 0 && col >= 0)
            {
                // 편집 가능하면 인라인 편집 시작, 아니면 더블클릭 이벤트
                if (!_readOnly && DataIndex(col) >= 0) { BeginEdit(row, col); return; }
                var h = CellDoubleClick;
                if (h != null) h(this, new AdvGridCellEventArgs(row, col));
            }
        }

        /// <summary>열 폭을 머리글+모든 셀 내용 중 가장 넓은 것에 맞춘다(사용자 조작이라 전 행 측정 허용).</summary>
        public void AutoFitColumn(int col)
        {
            if (col < 0 || col >= _columns.Count) return;
            int arrow = (_sortCol == col) ? 14 : 0;
            int w = TextRenderer.MeasureText(_columns[col].Header ?? string.Empty, HeaderFont).Width + CellPadX * 2 + arrow;
            for (int r = 0; r < _rows.Count; r++)
            {
                int cw = TextRenderer.MeasureText(GetCell(r, col), Font).Width + CellPadX * 2;
                if (cw > w) w = cw;
            }
            _columns[col].Width = w;   // 세터가 MinWidth로 하한 클램프
            EnsureLayout();
            Invalidate();
            var h = ColumnWidthChanged;
            if (h != null) h(this, new AdvGridColumnEventArgs(col, _columns[col]));
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
            CommitEdit();   // 스크롤하면 편집기가 셀에서 벗어나므로 먼저 반영
            EnsureLayout();
            // Shift를 누르거나 세로바 없이 가로바만 있으면 가로로 스크롤한다
            bool horizontal = (ModifierKeys & Keys.Shift) != 0 || (!_vBar && _hBar);
            if (horizontal && _hBar)
            {
                int before = _scrollX;
                _scrollX -= Math.Sign(e.Delta) * HScrollStep;
                ClampScroll();
                if (_scrollX != before) Invalidate();
            }
            else if (_vBar)
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
            int di = DataIndex(col);
            if (di < 0) return;   // 비데이터 열은 정렬 대상 아님

            if (_sortCol == col) _sortAsc = !_sortAsc;
            else { _sortCol = col; _sortAsc = true; }

            bool numeric = true;
            foreach (var r in _rows)
            {
                string s = di < r.Cells.Length ? r.Cells[di] : null;
                if (string.IsNullOrEmpty(s)) continue;
                double d;
                if (!double.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out d))
                { numeric = false; break; }
            }

            int dir = _sortAsc ? 1 : -1;
            _rows.Sort((x, y) =>
            {
                string sx = di < x.Cells.Length ? (x.Cells[di] ?? "") : "";
                string sy = di < y.Cells.Length ? (y.Cells[di] ?? "") : "";
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

        // ── 인라인 편집 ───────────────────────────────────────────────

        private TextBox EnsureEditor()
        {
            if (_editor == null)
            {
                _editor = new TextBox { BorderStyle = BorderStyle.None, AutoSize = false, Visible = false };
                _editor.KeyDown += Editor_KeyDown;
                _editor.LostFocus += (s, e) => CommitEdit();
                Controls.Add(_editor);
            }
            return _editor;
        }

        /// <summary>보이는 셀의 클라이언트 사각형. 스크롤로 화면 밖이면 Empty.</summary>
        private Rectangle CellRectClient(int row, int col)
        {
            if (row < 0 || col < 0 || col >= _columns.Count) return Rectangle.Empty;
            int y = _viewport.Top + row * _rowHeight - _scrollY;
            if (y + _rowHeight <= _viewport.Top || y >= _viewport.Bottom) return Rectangle.Empty;
            int cx = _viewport.Left - _scrollX;
            for (int c = 0; c < col; c++) cx += _columns[c].Width;
            return Rectangle.Intersect(new Rectangle(cx, y, _columns[col].Width, _rowHeight), _viewport);
        }

        /// <summary>셀 인라인 편집을 시작한다. ReadOnly이거나 비데이터(체크박스) 열이면 무시.</summary>
        public void BeginEdit(int row, int col)
        {
            if (_readOnly || row < 0 || row >= _rows.Count) return;
            if (col < 0 || col >= _columns.Count || DataIndex(col) < 0) return;
            CommitEdit();
            EnsureLayout();
            var rect = CellRectClient(row, col);
            if (rect.Width <= 2 || rect.Height <= 2) return;

            _editRow = row; _editCol = col;
            var theme = EffectiveTheme;
            var ed = EnsureEditor();
            ed.Font = Font;
            ed.BackColor = theme.Surface;
            ed.ForeColor = theme.Text;
            ed.TextAlign = _columns[col].Alignment;   // 열 정렬에 맞춤(숫자 열은 우측 등)
            ed.Bounds = Rectangle.Inflate(rect, -2, -2);   // 격자선 안쪽
            ed.Text = GetCell(row, col);
            ed.Visible = true;
            ed.BringToFront();
            ed.Focus();
            ed.SelectAll();
        }

        /// <summary>편집 중이면 편집기 값을 셀에 반영하고 편집을 끝낸다.</summary>
        public void CommitEdit()
        {
            if (_editRow < 0 || _editor == null || !_editor.Visible) return;
            int r = _editRow, c = _editCol;
            string val = _editor.Text;
            bool changed = GetCell(r, c) != val;
            EndEdit();
            if (changed)
            {
                SetCell(r, c, val);
                var h = CellValueChanged;
                if (h != null) h(this, new AdvGridCellEventArgs(r, c));
            }
        }

        /// <summary>편집 중이면 변경을 버리고 끝낸다.</summary>
        public void CancelEdit()
        {
            if (_editRow < 0) return;
            EndEdit();
            Invalidate();
        }

        private void EndEdit()
        {
            _editRow = _editCol = -1;
            if (_editor != null) _editor.Visible = false;
        }

        private int NextEditableColumn(int from, bool forward)
        {
            int step = forward ? 1 : -1;
            for (int c = from + step; c >= 0 && c < _columns.Count; c += step)
                if (DataIndex(c) >= 0) return c;
            return -1;
        }

        private void Editor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommitEdit(); Focus();
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                CancelEdit(); Focus();
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Tab)
            {
                int r = _editRow, c = _editCol;
                CommitEdit();
                int nc = NextEditableColumn(c, !e.Shift);
                if (nc >= 0) BeginEdit(r, nc); else Focus();
                e.Handled = e.SuppressKeyPress = true;
            }
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
                case Keys.Left:
                case Keys.Right:
                case Keys.F2:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            EnsureLayout();

            // 좌우 방향키는 열이 넘칠 때 가로 스크롤한다(행 선택과 무관)
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
            {
                if (_hBar)
                {
                    int before = _scrollX;
                    _scrollX += (e.KeyCode == Keys.Left ? -1 : 1) * HScrollStep;
                    ClampScroll();
                    if (_scrollX != before) Invalidate();
                }
                e.Handled = true;
                return;
            }

            // F2 = 현재 행의 첫 편집 가능 열을 편집
            if (e.KeyCode == Keys.F2 && !_readOnly)
            {
                int r = (_focusRow >= 0 && _focusRow < _rows.Count) ? _focusRow : SelectedIndex;
                int c = NextEditableColumn(-1, true);
                if (r >= 0 && c >= 0) BeginEdit(r, c);
                e.Handled = true;
                return;
            }

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
            if (disposing)
            {
                if (Region != null) Region.Dispose();
                if (_headerFont != null) _headerFont.Dispose();
            }
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
        [Description("읽기 전용 여부입니다. false면 더블클릭·F2로 셀을 인라인 편집할 수 있습니다.")]
        public bool ReadOnly
        {
            get { return _owner.ReadOnly; }
            set { _owner.ReadOnly = value; }
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
