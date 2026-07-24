using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>
    /// 트리의 한 노드. 자식 노드를 담고 펼침 상태를 갖는다.
    /// <see cref="Add"/>로만 만들어 항상 부모·소유 트리에 연결된 상태를 유지한다.
    /// </summary>
    public class AdvTreeNode
    {
        internal readonly List<AdvTreeNode> ChildNodes = new List<AdvTreeNode>();
        private bool _expanded;
        private bool _checked;

        public string Text { get; set; }
        public object Tag { get; set; }
        public AdvTreeNode Parent { get; internal set; }
        internal AdvTreeView Owner;

        internal AdvTreeNode(string text) { Text = text; }

        /// <summary>펼침 여부. 자식이 없으면 의미 없다.</summary>
        public bool Expanded
        {
            get { return _expanded; }
            set
            {
                if (_expanded == value) return;
                _expanded = value;
                if (Owner != null) Owner.NotifyStructureChanged();
            }
        }

        /// <summary>
        /// 체크 여부(부모-자식 연동 없는 독립 체크). 바꾸면 트리가 AfterCheck를 올린다.
        /// 트리의 CheckBoxes가 꺼져 있으면 화면에 보이지 않을 뿐 값은 유지된다.
        /// </summary>
        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                if (Owner != null) Owner.NotifyNodeChecked(this);
            }
        }

        private Image _icon;

        /// <summary>노드 앞에 그릴 아이콘(16px 논리 크기로 스케일). null이면 없다.</summary>
        public Image Icon
        {
            get { return _icon; }
            set
            {
                if (ReferenceEquals(_icon, value)) return;
                _icon = value;
                if (Owner != null) Owner.Invalidate();
            }
        }

        /// <summary>
        /// 트리에서 이 노드(서브트리 포함)를 제거한다. 이미 제거됐거나 소속 트리가 없으면 false.
        /// </summary>
        public bool Remove()
        {
            return Owner != null && Owner.Remove(this);
        }

        [Browsable(false)]
        public bool HasChildren { get { return ChildNodes.Count > 0; } }

        [Browsable(false)]
        public IList<AdvTreeNode> Children { get { return ChildNodes.AsReadOnly(); } }

        /// <summary>자식 노드를 추가하고 돌려준다.</summary>
        public AdvTreeNode Add(string text)
        {
            var n = new AdvTreeNode(text) { Parent = this, Owner = Owner };
            ChildNodes.Add(n);
            if (Owner != null) Owner.NotifyStructureChanged();
            return n;
        }
    }

    /// <summary>
    /// 밑바닥부터 직접 그리는 테마 트리뷰. 계층 노드·펼침/접힘·선택·키보드 탐색을
    /// 모두 커스텀 그리기로 처리한다. v1: 텍스트 노드, 단일 선택, 세로 스크롤.
    /// </summary>
    [ToolboxItem(true)]
    [Description("밑바닥부터 직접 그리는 테마 트리뷰입니다.")]
    [DefaultEvent("AfterSelect")]
    public class AdvTreeView : AdvControlBase
    {
        private struct VisRow { public AdvTreeNode Node; public int Level; }

        private readonly List<AdvTreeNode> _roots = new List<AdvTreeNode>();
        private readonly List<VisRow> _visible = new List<VisRow>();
        private bool _visibleDirty = true;   // 노드 추가·제거·펼침 변경 시에만 _visible 재구성

        private int _rowHeight = 26;
        private int _indent = 18;
        private bool _checkBoxes;
        private const int ScrollSize = 10;   // AdvScrollBar.DefaultWidth와 통일
        private const int MinThumb = 24;
        private const int ChevronBox = 16;
        private const int CheckBoxSize = 16;   // AdvListBox와 동일 규격
        private const int CheckGap = 4;        // 체크박스와 텍스트 사이
        private const int IconSize = 16;       // 노드 아이콘 크기
        private const int IconGap = 4;         // 아이콘과 텍스트 사이

        private int _scrollY;
        private AdvTreeNode _selected;
        private AdvTreeNode _hover;

        private bool _dragThumb;
        private int _dragThumbOffset;
        private bool _vHot;

        private bool _vBar;
        private Rectangle _viewport, _vBarRect;

        private AdvTreeViewOptions _options;

        /// <summary>선택 노드가 바뀐 뒤 발생한다.</summary>
        [Category("Behavior")]
        [Description("선택 노드가 바뀐 뒤 발생합니다.")]
        public event EventHandler<AdvTreeNodeEventArgs> AfterSelect;

        /// <summary>노드가 펼쳐지거나 접힌 뒤 발생한다.</summary>
        [Category("Behavior")]
        [Description("노드가 펼쳐지거나 접힌 뒤 발생합니다.")]
        public event EventHandler<AdvTreeNodeEventArgs> AfterExpandCollapse;

        /// <summary>노드 체크 상태가 바뀐 뒤 발생한다.</summary>
        [Category("Behavior")]
        [Description("노드의 체크 상태가 바뀐 뒤 발생합니다. CheckBoxes가 켜져 있어야 화면에 보입니다.")]
        public event EventHandler<AdvTreeNodeEventArgs> AfterCheck;

        public AdvTreeView()
        {
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
            Styling.ShowFocusGlow = false;
            Styling.Radius = 8;
            Size = new Size(240, 220);
        }

        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvTreeViewOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvTreeViewOptions(this)); }
        }

        [Category("Layout")]
        [DefaultValue(26)]
        [Description("노드 한 줄의 높이(px)입니다.")]
        public int RowHeight
        {
            get { return _rowHeight; }
            set { value = value < 16 ? 16 : value; if (_rowHeight == value) return; _rowHeight = value; Invalidate(); }
        }

        [Category("Layout")]
        [DefaultValue(18)]
        [Description("한 단계 들여쓰기 폭(px)입니다.")]
        public int Indent
        {
            get { return _indent; }
            set { value = value < 8 ? 8 : value; if (_indent == value) return; _indent = value; Invalidate(); }
        }

        // ── 노드 API ──────────────────────────────────────────────────

        /// <summary>최상위 노드를 추가하고 돌려준다.</summary>
        public AdvTreeNode Add(string text)
        {
            var n = new AdvTreeNode(text) { Owner = this };
            _roots.Add(n);
            NotifyStructureChanged();
            return n;
        }

        /// <summary>
        /// 노드(서브트리 포함)를 제거한다. 이 트리 소속이 아니거나 이미 제거됐으면 false.
        /// 제거된 서브트리 안에 선택 노드가 있었으면 선택을 해제한다(AfterSelect(null) 발생).
        /// </summary>
        public bool Remove(AdvTreeNode node)
        {
            if (node == null || !ReferenceEquals(node.Owner, this)) return false;

            var list = node.Parent != null ? node.Parent.ChildNodes : _roots;
            if (!list.Remove(node)) return false;

            if (_selected != null && IsInSubtree(_selected, node)) SelectNode(null, false);
            if (_hover != null && IsInSubtree(_hover, node)) _hover = null;

            DetachSubtree(node);
            node.Parent = null;
            NotifyStructureChanged();
            return true;
        }

        /// <summary>candidate가 root의 서브트리(root 포함) 안에 있는지.</summary>
        private static bool IsInSubtree(AdvTreeNode candidate, AdvTreeNode root)
        {
            for (var n = candidate; n != null; n = n.Parent)
                if (ReferenceEquals(n, root)) return true;
            return false;
        }

        /// <summary>제거된 서브트리 전체의 소속을 끊어, 남은 참조로 다시 Remove해도 false가 되게 한다.</summary>
        private static void DetachSubtree(AdvTreeNode node)
        {
            node.Owner = null;
            foreach (var c in node.ChildNodes) DetachSubtree(c);
        }

        /// <summary>모든 노드를 지운다.</summary>
        public void Clear()
        {
            _roots.Clear();
            _selected = _hover = null;
            _scrollY = 0;
            NotifyStructureChanged();
        }

        [Browsable(false)]
        public IList<AdvTreeNode> RootNodes { get { return _roots.AsReadOnly(); } }

        /// <summary>현재 선택된 노드. 없으면 null.</summary>
        [Browsable(false)]
        public AdvTreeNode SelectedNode
        {
            get { return _selected; }
            set { SelectNode(value, true); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("각 노드 왼쪽에 체크박스를 표시합니다. 선택과 별개로 여러 노드를 체크할 수 있습니다.")]
        public bool CheckBoxes
        {
            get { return _checkBoxes; }
            set
            {
                if (_checkBoxes == value) return;
                _checkBoxes = value;
                Invalidate();
            }
        }

        /// <summary>체크된 노드들(트리 순서, 접혀서 안 보이는 노드 포함).</summary>
        [Browsable(false)]
        public IList<AdvTreeNode> CheckedNodes
        {
            get
            {
                var list = new List<AdvTreeNode>();
                CollectChecked(_roots, list);
                return list;
            }
        }

        private static void CollectChecked(List<AdvTreeNode> nodes, List<AdvTreeNode> into)
        {
            foreach (var n in nodes)
            {
                if (n.Checked) into.Add(n);
                CollectChecked(n.ChildNodes, into);
            }
        }

        /// <summary>노드 Checked가 바뀌면 다시 그리고 AfterCheck를 올린다.</summary>
        internal void NotifyNodeChecked(AdvTreeNode node)
        {
            Invalidate();
            var h = AfterCheck;
            if (h != null) h(this, new AdvTreeNodeEventArgs(node));

            // 보이는 행이면 상태 변경을 스크린리더에 알린다
            if (IsHandleCreated && _checkBoxes)
            {
                EnsureLayout();
                int idx = IndexOfVisible(node);
                if (idx >= 0) AccessibilityNotifyClients(AccessibleEvents.StateChange, idx);
            }
        }

        public void ExpandAll() { SetExpandedRecursive(_roots, true); }
        public void CollapseAll() { SetExpandedRecursive(_roots, false); }

        private void SetExpandedRecursive(List<AdvTreeNode> nodes, bool expanded)
        {
            foreach (var n in nodes)
            {
                if (n.HasChildren) n.Expanded = expanded;
                SetExpandedRecursive(n.ChildNodes, expanded);
            }
            Invalidate();
        }

        /// <summary>노드가 추가·제거되거나 펼침이 바뀌면 다시 그린다.</summary>
        internal void NotifyStructureChanged()
        {
            _visibleDirty = true;
            ClampScroll();
            Invalidate();
        }

        // ── 레이아웃 ──────────────────────────────────────────────────

        private void RebuildVisible()
        {
            _visible.Clear();
            foreach (var n in _roots) AddVisible(n, 0);
        }

        private void AddVisible(AdvTreeNode n, int level)
        {
            _visible.Add(new VisRow { Node = n, Level = level });
            if (n.Expanded)
                foreach (var c in n.ChildNodes) AddVisible(c, level + 1);
        }

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

        private int TotalHeight { get { return _visible.Count * _rowHeight; } }

        private void EnsureLayout()
        {
            if (_visibleDirty) { RebuildVisible(); _visibleDirty = false; }   // 구조 변경 시에만 재순회
            var inner = InnerBounds;

            _vBar = TotalHeight > inner.Height;
            int vw = _vBar ? ScrollSize : 0;

            _viewport = new Rectangle(inner.Left, inner.Top, Math.Max(0, inner.Width - vw), inner.Height);
            _vBarRect = _vBar
                ? new Rectangle(inner.Right - ScrollSize, inner.Top, ScrollSize, inner.Height)
                : Rectangle.Empty;

            ClampScroll();
        }

        private int MaxScrollY { get { return Math.Max(0, TotalHeight - _viewport.Height); } }

        private void ClampScroll()
        {
            if (_scrollY < 0) _scrollY = 0;
            if (_scrollY > MaxScrollY) _scrollY = MaxScrollY;
        }

        // ── 둥근 모서리 클립 ─────────────────────────────────────────

        private Rectangle _regionClip = Rectangle.Empty;   // 마지막 Region 클립(동일 크기 중복 재생성 방지)

        private void ApplyRoundedRegion()
        {
            if (!IsHandleCreated) return;
            var clip = Rectangle.Inflate(FrameBounds, 1, 1);
            AdvGraphics.UpdateRoundedRegion(this, clip, EffectiveCorners, false, ref _regionClip);
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyRoundedRegion(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); ApplyRoundedRegion(); }
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

            AdvFrameRenderer.Draw(g, frame, theme, EffectiveCorners, EffectiveBorderWidth,
                                  theme.Surface, Color.Empty, theme.Border,
                                  null, CurrentElevation, EffectiveBorderDash);

            var state = g.Save();
            g.SetClip(_viewport);
            DrawNodes(g, theme);
            g.Restore(state);

            if (_vBar) DrawScrollBar(g, theme);
        }

        private void DrawNodes(Graphics g, AdvTheme theme)
        {
            if (_visible.Count == 0) return;

            int first = _scrollY / _rowHeight;
            int y = _viewport.Top - (_scrollY % _rowHeight);

            for (int i = first; i < _visible.Count && y < _viewport.Bottom; i++, y += _rowHeight)
            {
                var vr = _visible[i];
                var node = vr.Node;
                var rowRect = new Rectangle(_viewport.Left, y, _viewport.Width, _rowHeight);

                bool selected = ReferenceEquals(node, _selected);
                if (selected)
                    using (var b = new SolidBrush(theme.Accent)) g.FillRectangle(b, rowRect);
                else if (ReferenceEquals(node, _hover))
                    using (var b = new SolidBrush(theme.SurfaceHover)) g.FillRectangle(b, rowRect);

                int indentX = _viewport.Left + 6 + vr.Level * _indent;
                int chevBox = AdvGraphics.Scale(this, ChevronBox);

                // 펼침 셰브런
                if (node.HasChildren)
                {
                    var box = new Rectangle(indentX, y, chevBox, _rowHeight);
                    AdvGraphics.DrawChevron(g, this, box,
                        node.Expanded ? AdvGraphics.ChevronDirection.Down : AdvGraphics.ChevronDirection.Right,
                        selected ? theme.OnAccent : theme.TextMuted, 8, 5, 1.6f, 0);
                }

                int textX = indentX + chevBox + 2;

                // 체크박스(켜져 있을 때만). 도형은 목록과 공용 헬퍼가 그린다
                if (_checkBoxes)
                {
                    int sz = AdvGraphics.Scale(this, CheckBoxSize);
                    var box = new Rectangle(textX, y + (_rowHeight - sz) / 2, sz, sz);
                    AdvGraphics.DrawItemCheckBox(g, this, box, node.Checked, Enabled, theme);
                    textX += AdvGraphics.Scale(this, CheckBoxSize + CheckGap);
                }

                // 노드 아이콘(있을 때만)
                if (node.Icon != null)
                {
                    int isz = AdvGraphics.Scale(this, IconSize);
                    var ir = new Rectangle(textX, y + (_rowHeight - isz) / 2, isz, isz);
                    g.DrawImage(node.Icon, ir);
                    textX += isz + AdvGraphics.Scale(this, IconGap);
                }

                var textRect = Rectangle.FromLTRB(textX, y, _viewport.Right - 4, y + _rowHeight);
                // TextRenderer(GDI)는 Graphics.Clip을 무시하므로, 썸 드래그로 부분만 걸친 행의
                // 텍스트가 뷰포트 밖(테두리)까지 번지지 않도록 rect를 뷰포트와 직접 교차시킨다.
                textRect.Intersect(_viewport);
                if (textRect.Width > 0 && textRect.Height > 0)
                    TextRenderer.DrawText(g, node.Text, Font, textRect,
                        selected ? theme.OnAccent : theme.Text,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                      | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        private void DrawScrollBar(Graphics g, AdvTheme theme)
        {
            using (var tb = new SolidBrush(theme.SurfaceHover)) g.FillRectangle(tb, _vBarRect);

            var thumb = ThumbRect();
            if (thumb.IsEmpty) return;

            var t = Rectangle.Inflate(thumb, -2, -2);
            if (t.Width <= 0 || t.Height <= 0) return;
            using (var path = AdvGraphics.CreateRoundedRect(t, new AdvCorners(Math.Min(t.Width, t.Height) / 2)))
            using (var b = new SolidBrush(_vHot || _dragThumb ? theme.TextMuted : theme.Border))
                g.FillPath(b, path);
        }

        private Rectangle ThumbRect()
        {
            int total = TotalHeight;
            if (total <= _viewport.Height || _vBarRect.Height <= 0) return Rectangle.Empty;
            int th = Math.Max(MinThumb, (int)((long)_vBarRect.Height * _viewport.Height / total));
            int max = _vBarRect.Height - th;
            int pos = MaxScrollY == 0 ? 0 : (int)((long)max * _scrollY / MaxScrollY);
            return new Rectangle(_vBarRect.Left, _vBarRect.Top + pos, _vBarRect.Width, th);
        }

        // ── 마우스 ────────────────────────────────────────────────────

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            EnsureLayout();
            if (e.Button != MouseButtons.Left) return;

            if (_vBar && _vBarRect.Contains(e.Location))
            {
                var thumb = ThumbRect();
                if (thumb.Contains(e.Location)) { _dragThumb = true; _dragThumbOffset = e.Y - thumb.Top; }
                else { _scrollY += (e.Y < thumb.Top ? -1 : 1) * _viewport.Height; ClampScroll(); Invalidate(); }
                return;
            }

            int idx = RowAt(e.Y);
            if (idx < 0) return;
            var vr = _visible[idx];

            // 셰브런(펼침 화살표) 영역을 눌렀으면 토글, 아니면 선택
            int indentX = _viewport.Left + 6 + vr.Level * _indent;
            if (vr.Node.HasChildren && e.X >= indentX && e.X < indentX + AdvGraphics.Scale(this, ChevronBox))
            {
                ToggleExpand(vr.Node);
                return;
            }

            // 체크박스 영역을 눌렀으면 선택은 그대로 두고 체크만 토글한다(목록과 동일 규칙)
            int checkX = indentX + AdvGraphics.Scale(this, ChevronBox) + 2;
            if (_checkBoxes && e.X >= checkX && e.X < checkX + AdvGraphics.Scale(this, CheckBoxSize))
            {
                vr.Node.Checked = !vr.Node.Checked;   // NotifyNodeChecked가 AfterCheck·다시 그림 처리
                return;
            }

            SelectNode(vr.Node, true);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            EnsureLayout();
            int idx = RowAt(e.Y);
            if (idx >= 0 && _visible[idx].Node.HasChildren) ToggleExpand(_visible[idx].Node);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            // 캡처가 풀린 뒤 버튼을 안 눌러도 스쳐서 이동하는 것을 막는다
            if (_dragThumb && (e.Button & MouseButtons.Left) == 0) _dragThumb = false;
            if (_dragThumb) { DragThumb(e.Y); return; }

            bool vHot = _vBar && ThumbRect().Contains(e.Location);
            if (vHot != _vHot) { _vHot = vHot; Invalidate(); }

            int idx = _viewport.Contains(e.Location) ? RowAt(e.Y) : -1;
            var hoverNode = idx >= 0 ? _visible[idx].Node : null;
            if (!ReferenceEquals(hoverNode, _hover)) { _hover = hoverNode; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _dragThumb = false; }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            // 드래그 중 캡처가 강제로 풀리면(모달·Alt+Tab 등) 썸 드래그 상태를 내린다
            if (_dragThumb) { _dragThumb = false; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover != null || _vHot) { _hover = null; _vHot = false; Invalidate(); }
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

        private void DragThumb(int mouseY)
        {
            var thumb = ThumbRect();
            int max = _vBarRect.Height - thumb.Height;
            int pos = mouseY - _dragThumbOffset - _vBarRect.Top;
            if (pos < 0) pos = 0; else if (pos > max) pos = max;
            _scrollY = max <= 0 ? 0 : (int)((long)MaxScrollY * pos / max);
            ClampScroll();
            Invalidate();
        }

        private int RowAt(int y)
        {
            if (y < _viewport.Top || y >= _viewport.Bottom) return -1;
            int idx = (y - _viewport.Top + _scrollY) / _rowHeight;
            return idx >= 0 && idx < _visible.Count ? idx : -1;
        }

        // ── 선택·펼침 ─────────────────────────────────────────────────

        private void ToggleExpand(AdvTreeNode node)
        {
            if (!node.HasChildren) return;
            node.Expanded = !node.Expanded;   // NotifyStructureChanged로 다시 그림
            RaiseExpandCollapse(node);
            // 펼침/접힘으로 보이는 행 집합이 바뀌었음을 스크린리더에 알린다(펼침 상태 변경 + 자식 재구성)
            if (IsHandleCreated)
            {
                EnsureLayout();
                int idx = IndexOfVisible(node);
                if (idx >= 0) AccessibilityNotifyClients(AccessibleEvents.StateChange, idx);
                AccessibilityNotifyClients(AccessibleEvents.Reorder, -1);   // childID -1 → CHILDID_SELF
            }
        }

        private void RaiseExpandCollapse(AdvTreeNode node)
        {
            var h = AfterExpandCollapse;
            if (h != null) h(this, new AdvTreeNodeEventArgs(node));
        }

        private void SelectNode(AdvTreeNode node, bool ensureVisible)
        {
            if (ReferenceEquals(_selected, node)) return;
            _selected = node;
            if (ensureVisible && node != null) EnsureNodeVisible(node);
            Invalidate();
            var h = AfterSelect;
            if (h != null) h(this, new AdvTreeNodeEventArgs(node));
            NotifyTreeAccFocus();
        }

        private void EnsureNodeVisible(AdvTreeNode node)
        {
            // 조상을 모두 펼쳐 노드가 보이게 한다. 실제로 펼쳐진 조상마다 AfterExpandCollapse를 올린다.
            for (var p = node.Parent; p != null; p = p.Parent)
                if (!p.Expanded) { p.Expanded = true; RaiseExpandCollapse(p); }
            EnsureLayout();
            int idx = IndexOfVisible(node);
            if (idx < 0) return;
            int top = idx * _rowHeight, bottom = top + _rowHeight;
            if (top < _scrollY) _scrollY = top;
            else if (bottom > _scrollY + _viewport.Height) _scrollY = bottom - _viewport.Height;
            ClampScroll();
        }

        private int IndexOfVisible(AdvTreeNode node)
        {
            for (int i = 0; i < _visible.Count; i++)
                if (ReferenceEquals(_visible[i].Node, node)) return i;
            return -1;
        }

        /// <summary>숨겨진 노드의 가장 가까운 보이는 조상 인덱스. 없으면 -1.</summary>
        private int IndexOfNearestVisibleAncestor(AdvTreeNode node)
        {
            for (var p = node.Parent; p != null; p = p.Parent)
            {
                int idx = IndexOfVisible(p);
                if (idx >= 0) return idx;
            }
            return -1;
        }

        // ── 키보드 ────────────────────────────────────────────────────

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up: case Keys.Down: case Keys.Left: case Keys.Right:
                case Keys.Home: case Keys.End: case Keys.Return: case Keys.Space:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            EnsureLayout();
            if (_visible.Count == 0) return;

            int cur = _selected != null ? IndexOfVisible(_selected) : -1;
            // 선택 노드가 조상 접힘으로 숨겨졌으면(_visible에 없음) 가장 가까운 보이는 조상에서 이어간다.
            // (예전엔 -1로 취급돼 방향키가 트리 첫/끝 행으로 튀었다.)
            if (cur < 0 && _selected != null) cur = IndexOfNearestVisibleAncestor(_selected);

            switch (e.KeyCode)
            {
                case Keys.Down: MoveSelection(cur, +1); break;
                case Keys.Up: MoveSelection(cur, -1); break;
                case Keys.Home: SelectByIndex(0); break;
                case Keys.End: SelectByIndex(_visible.Count - 1); break;
                case Keys.Right:
                    if (_selected != null && _selected.HasChildren)
                    {
                        if (!_selected.Expanded) ToggleExpand(_selected);
                        else SelectByIndex(cur + 1);   // 첫 자식으로
                    }
                    break;
                case Keys.Left:
                    if (_selected != null)
                    {
                        if (_selected.HasChildren && _selected.Expanded) ToggleExpand(_selected);
                        else if (_selected.Parent != null) SelectNode(_selected.Parent, true);
                    }
                    break;
                case Keys.Return:
                    if (_selected != null && _selected.HasChildren) ToggleExpand(_selected);
                    break;
                case Keys.Space:
                    // 스페이스로 선택 노드의 체크를 토글한다(목록과 동일 규칙)
                    if (!_checkBoxes || _selected == null) return;
                    _selected.Checked = !_selected.Checked;
                    break;
                default: return;
            }
            e.Handled = true;
        }

        private void MoveSelection(int cur, int delta)
        {
            int next = cur < 0 ? (delta > 0 ? 0 : _visible.Count - 1) : cur + delta;
            SelectByIndex(next);
        }

        private void SelectByIndex(int idx)
        {
            if (idx < 0 || idx >= _visible.Count) return;
            SelectNode(_visible[idx].Node, true);
        }

        // ── 접근성(스크린리더/UI Automation) ─────────────────────────
        // 보이는 행 평면 모델: 루트(Outline)의 직접 자식 = 현재 보이는 노드들(_visible).
        // 이래야 MSAA 단일 childID(= 보이는 행 인덱스)로 라이브 포커스 이벤트를 정확히 지정할 수 있다.
        // 접힌 노드의 하위는 펼치기 전까지 트리에 나타나지 않는다(네이티브 MSAA 트리와 동일). 펼침/접힘
        // 상태는 각 항목의 State(Expanded/Collapsed)로 전달한다.

        private Rectangle RowScreenRectByVisibleIndex(int idx)
        {
            EnsureLayout();
            if (idx < 0 || idx >= _visible.Count) return Rectangle.Empty;
            int y = _viewport.Top + idx * _rowHeight - _scrollY;
            return RectangleToScreen(new Rectangle(_viewport.Left, y, _viewport.Width, _rowHeight));
        }

        /// <summary>선택 노드 이동을 스크린리더에 라이브로 알린다(포커스가 있을 때만). childID = 보이는 행 인덱스.</summary>
        private void NotifyTreeAccFocus()
        {
            if (!Focused || _selected == null) return;
            EnsureLayout();
            int idx = IndexOfVisible(_selected);
            if (idx < 0) return;
            AccessibilityNotifyClients(AccessibleEvents.Focus, idx);
            AccessibilityNotifyClients(AccessibleEvents.Selection, idx);
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new TreeAccessibleObject(this);
        }

        private sealed class TreeAccessibleObject : ControlAccessibleObject
        {
            private readonly AdvTreeView _o;
            public TreeAccessibleObject(AdvTreeView o) : base(o) { _o = o; }

            public override AccessibleRole Role { get { return AccessibleRole.Outline; } }

            public override int GetChildCount() { _o.EnsureLayout(); return _o._visible.Count; }
            public override AccessibleObject GetChild(int index)
            {
                _o.EnsureLayout();
                return index >= 0 && index < _o._visible.Count
                    ? new NodeAccessibleObject(_o, index) : null;
            }

            public override AccessibleObject GetSelected()
            {
                if (_o._selected == null) return null;
                _o.EnsureLayout();
                int idx = _o.IndexOfVisible(_o._selected);
                return idx >= 0 ? new NodeAccessibleObject(_o, idx) : null;
            }

            public override AccessibleObject GetFocused() { return GetSelected(); }

            /// <summary>보이는 행 하나. 접힘/펼침은 State로, 계층 깊이는(MSAA 한계상) 별도 전달하지 않는다.</summary>
            private sealed class NodeAccessibleObject : AccessibleObject
            {
                private readonly AdvTreeView _o;
                private readonly int _visIndex;
                public NodeAccessibleObject(AdvTreeView o, int visIndex) { _o = o; _visIndex = visIndex; }

                private AdvTreeNode Node
                {
                    get { return _visIndex >= 0 && _visIndex < _o._visible.Count ? _o._visible[_visIndex].Node : null; }
                }

                public override AccessibleObject Parent { get { return _o.AccessibilityObject; } }
                public override AccessibleRole Role { get { return AccessibleRole.OutlineItem; } }
                public override string Name { get { var n = Node; return n != null ? n.Text : null; } }

                public override AccessibleStates State
                {
                    get
                    {
                        var node = Node;
                        if (node == null) return AccessibleStates.None;
                        var s = AccessibleStates.Selectable | AccessibleStates.Focusable;
                        if (!_o.Enabled) s |= AccessibleStates.Unavailable;
                        if (ReferenceEquals(node, _o._selected))
                        {
                            s |= AccessibleStates.Selected;
                            if (_o.Focused) s |= AccessibleStates.Focused;
                        }
                        if (node.HasChildren)
                            s |= node.Expanded ? AccessibleStates.Expanded : AccessibleStates.Collapsed;
                        if (_o._checkBoxes && node.Checked) s |= AccessibleStates.Checked;
                        return s;
                    }
                }

                public override Rectangle Bounds { get { return _o.RowScreenRectByVisibleIndex(_visIndex); } }

                // 자식이 있으면 펼치기/접기가 기본 동작이다(표준 트리 관례).
                // 리프는 체크박스 트리에선 체크 토글(Space 키와 일치), 아니면 선택이다.
                public override string DefaultAction
                {
                    get
                    {
                        var n = Node;
                        if (n == null) return null;
                        if (n.HasChildren) return n.Expanded ? "접기" : "펼치기";
                        if (_o._checkBoxes) return n.Checked ? "체크 해제" : "체크";
                        return "선택";
                    }
                }

                public override void DoDefaultAction()
                {
                    var n = Node;
                    if (n == null) return;
                    if (n.HasChildren) _o.ToggleExpand(n);
                    else if (_o._checkBoxes) n.Checked = !n.Checked;
                    else _o.SelectNode(n, true);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Region != null) Region.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>트리 노드 이벤트 인자.</summary>
    public class AdvTreeNodeEventArgs : EventArgs
    {
        public AdvTreeNode Node { get; private set; }
        public AdvTreeNodeEventArgs(AdvTreeNode node) { Node = node; }
    }

    /// <summary>AdvTreeView가 추가한 속성. RowHeight·Indent는 Layout 카테고리에 직접 노출되므로
    /// (AdvDataGrid 관례와 동일) 여기에 중복하지 않는다.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvTreeViewOptions : AdvOptions
    {
        private readonly AdvTreeView _owner;

        internal AdvTreeViewOptions(AdvTreeView owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(false)]
        [Description("각 노드 왼쪽에 체크박스를 표시합니다. 선택과 별개로 여러 노드를 체크할 수 있습니다.")]
        public bool CheckBoxes
        {
            get { return _owner.CheckBoxes; }
            set { _owner.CheckBoxes = value; }
        }
    }
}
