using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Controls.Internal;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>목록의 선택 방식.</summary>
    public enum AdvSelectionMode
    {
        /// <summary>한 번에 하나만 고른다.</summary>
        One,
        /// <summary>Ctrl·Shift로 여러 개를 고른다.</summary>
        MultiExtended
    }

    /// <summary>
    /// 테마를 따르는 목록 상자. 항목은 직접 그리고, 스크롤은 감싼 패널의
    /// AutoScroll에 맡긴다 — 드롭다운과 같은 방식이라 동작이 일관된다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("SelectedIndexChanged")]
    [DefaultProperty("Items")]
    [Description("테마를 따르는 목록 상자입니다.")]
    public class AdvListBox : AdvControlBase
    {
        private readonly List<object> _items = new List<object>();
        private readonly ObjectCollection _itemsWrapper;
        private readonly Panel _viewport;
        private readonly ListCore _core;
        private readonly AdvScrollBar _scrollBar;

        [Category("Behavior")]
        [Description("선택이 바뀔 때 발생합니다.")]
        public event EventHandler SelectedIndexChanged;

        public AdvListBox()
        {
            _itemsWrapper = new ObjectCollection(this, _items);

            _core = new ListCore(this, _items);
            _core.SelectionChanged += CoreSelectionChanged;

            // AutoScroll을 쓰면 OS가 시스템 색 스크롤바를 그려 다크 테마에서 흰 띠로 남는다.
            // 뷰포트는 자르는 역할만 하고 스크롤은 직접 그린 막대가 담당한다.
            _viewport = new Panel();
            _viewport.AutoScroll = false;
            _viewport.Padding = Padding.Empty;
            _viewport.Margin = Padding.Empty;
            _viewport.Controls.Add(_core);

            _scrollBar = new AdvScrollBar(EffectiveTheme);
            _scrollBar.ValueChanged += ScrollBarValueChanged;

            Controls.Add(_viewport);
            Controls.Add(_scrollBar);
            TabStop = true;
        }

        protected override Size DefaultSize
        {
            get { return new Size(200, 140); }
        }

        [Category("Data")]
        [Description("목록에 표시할 항목입니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [MergableProperty(false)]
        public ObjectCollection Items
        {
            get { return _itemsWrapper; }
        }

        [Category("Behavior")]
        [DefaultValue(AdvSelectionMode.One)]
        [Description("한 개만 고를지, 여러 개를 고를지 정합니다.")]
        public AdvSelectionMode SelectionMode
        {
            get { return _core.Mode; }
            set
            {
                if (_core.Mode == value) return;
                _core.Mode = value;

                // 여러 개 고른 상태에서 단일 선택으로 바꾸면 나머지가 남아 화면과 값이 어긋난다
                if (value == AdvSelectionMode.One) _core.CollapseToSingle();
                _core.Invalidate();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedIndex
        {
            get { return _core.PrimaryIndex; }
            set { _core.SelectSingle(value, true); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object SelectedItem
        {
            get
            {
                int i = _core.PrimaryIndex;
                return i >= 0 && i < _items.Count ? _items[i] : null;
            }
            set { SelectedIndex = value == null ? -1 : _items.IndexOf(value); }
        }

        /// <summary>선택된 위치들. 오름차순이다.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int[] SelectedIndices
        {
            get { return _core.GetSelectedIndices(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object[] SelectedItems
        {
            get
            {
                var idx = _core.GetSelectedIndices();
                var result = new object[idx.Length];
                for (int i = 0; i < idx.Length; i++) result[i] = _items[idx[i]];
                return result;
            }
        }

        /// <summary>한 줄의 높이. 글꼴에 맞춰 정해진다.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int ItemHeight
        {
            get { return TextRenderer.MeasureText("가Ay", Font).Height + 8; }
        }

        public void ClearSelection() { _core.ClearSelection(); }

        private void CoreSelectionChanged(object sender, EventArgs e)
        {
            var handler = SelectedIndexChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        internal void EnsureVisible(int index)
        {
            if (index < 0) return;

            int h = ItemHeight;
            int top = _scrollBar.Value;
            int itemTop = index * h;
            int itemBottom = itemTop + h;

            if (itemTop < top)
                _scrollBar.Value = itemTop;
            else if (itemBottom > top + _viewport.ClientSize.Height)
                _scrollBar.Value = itemBottom - _viewport.ClientSize.Height;
        }

        private void ScrollBarValueChanged(object sender, EventArgs e)
        {
            // 내용을 위로 밀어 올려 스크롤을 표현한다
            _core.Top = -_scrollBar.Value;
        }

        internal void RefreshLayout()
        {
            var theme = EffectiveTheme;
            var frame = FrameBounds;
            int bw = EffectiveBorderWidth;

            var inner = new Rectangle(frame.Left + bw, frame.Top + bw,
                                      Math.Max(1, frame.Width - bw * 2),
                                      Math.Max(1, frame.Height - bw * 2));

            int contentHeight = _items.Count * ItemHeight;
            bool needsBar = contentHeight > inner.Height;
            int barWidth = needsBar ? AdvScrollBar.DefaultWidth : 0;

            _viewport.Bounds = new Rectangle(inner.Left, inner.Top,
                                             Math.Max(1, inner.Width - barWidth), inner.Height);
            _viewport.BackColor = theme.InputBackground;

            _scrollBar.Theme = theme;
            _scrollBar.Visible = needsBar;
            if (needsBar)
            {
                _scrollBar.Bounds = new Rectangle(inner.Right - barWidth, inner.Top,
                                                  barWidth, inner.Height);
            }

            _scrollBar.ViewportHeight = _viewport.ClientSize.Height;
            _scrollBar.ContentHeight = Math.Max(contentHeight, _viewport.ClientSize.Height);

            _core.Font = Font;
            _core.ItemHeight = ItemHeight;
            _core.Bounds = new Rectangle(0, -_scrollBar.Value,
                                         _viewport.ClientSize.Width,
                                         Math.Max(_viewport.ClientSize.Height, contentHeight));
            _core.Invalidate();
        }

        /// <summary>휠은 목록 어디에 있든 스크롤로 이어져야 한다.</summary>
        internal void ScrollByWheel(int delta)
        {
            if (!_scrollBar.IsNeeded) return;

            int lines = SystemInformation.MouseWheelScrollLines;
            if (lines <= 0) lines = 3;

            _scrollBar.Value -= Math.Sign(delta) * lines * ItemHeight;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var bounds = FrameBounds;

            Color border;
            if (!Enabled) border = theme.Border;
            else if (ContainsFocus) border = theme.BorderFocus;
            else border = AdvGraphics.Blend(theme.Border, theme.BorderHover, HoverAmount);

            AdvFrameRenderer.Draw(e.Graphics, bounds, theme, EffectiveCorners, EffectiveBorderWidth,
                                  Enabled ? theme.InputBackground : theme.InputBackgroundDisabled,
                                  Color.Empty, border, null, CurrentElevation, EffectiveBorderDash);

            base.OnPaint(e);
        }

        protected override bool ShowsFocusVisual
        {
            get { return ContainsFocus; }
        }

        protected override void OnResize(EventArgs e) { RefreshLayout(); base.OnResize(e); }
        protected override void OnFontChanged(EventArgs e) { RefreshLayout(); base.OnFontChanged(e); }
        protected override void OnThemeChanged() { RefreshLayout(); base.OnThemeChanged(); }

        protected override void OnEnabledChanged(EventArgs e)
        {
            _viewport.Enabled = Enabled;
            _scrollBar.Enabled = Enabled;
            RefreshLayout();
            base.OnEnabledChanged(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollByWheel(e.Delta);
            base.OnMouseWheel(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            _core.Focus();
            base.OnGotFocus(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RefreshLayout();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _core.SelectionChanged -= CoreSelectionChanged;
                _scrollBar.ValueChanged -= ScrollBarValueChanged;
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 실제 항목을 그리고 마우스·키보드를 받는 안쪽 컨트롤.
        /// 스크롤은 부모 패널이 담당하므로 여기는 전체 높이를 그대로 갖는다.
        /// </summary>
        private class ListCore : Control
        {
            private readonly AdvListBox _owner;
            private readonly List<object> _items;
            private readonly HashSet<int> _selected = new HashSet<int>();

            private int _primary = -1;
            private int _anchor = -1;
            private int _hover = -1;

            public event EventHandler SelectionChanged;

            public AdvSelectionMode Mode { get; set; }
            public int ItemHeight { get; set; }

            public ListCore(AdvListBox owner, List<object> items)
            {
                _owner = owner;
                _items = items;
                ItemHeight = 20;

                SetStyle(ControlStyles.UserPaint
                       | ControlStyles.AllPaintingInWmPaint
                       | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.ResizeRedraw
                       | ControlStyles.Selectable, true);

                TabStop = false;
            }

            public int PrimaryIndex { get { return _primary; } }

            public int[] GetSelectedIndices()
            {
                var list = new List<int>(_selected);
                list.Sort();
                return list.ToArray();
            }

            public void ClearSelection()
            {
                if (_selected.Count == 0 && _primary < 0) return;

                _selected.Clear();
                _primary = -1;
                _anchor = -1;
                Invalidate();
                Raise();
            }

            /// <summary>여러 개 선택을 대표 한 개로 줄인다.</summary>
            public void CollapseToSingle()
            {
                if (_selected.Count <= 1) return;

                _selected.Clear();
                if (_primary >= 0) _selected.Add(_primary);
                Invalidate();
                Raise();
            }

            /// <summary>
            /// 항목이 끼워지거나 빠지면 선택 위치도 그만큼 옮긴다.
            /// 옮기지 않으면 선택이 조용히 다른 항목을 가리키거나, 범위 밖에 남아
            /// SelectedItems를 읽는 순간 예외가 난다.
            /// </summary>
            /// <param name="at">끼워지거나 빠진 위치</param>
            /// <param name="delta">끼워졌으면 양수, 빠졌으면 음수, 자리 변화가 없으면 0</param>
            /// <param name="count">바뀐 뒤의 전체 항목 수</param>
            public void SyncSelection(int at, int delta, int count)
            {
                int beforePrimary = _primary;
                int beforeCount = _selected.Count;
                bool moved = false;

                if (delta != 0)
                {
                    var shifted = new List<int>();
                    foreach (int i in _selected)
                    {
                        int s = ShiftOne(i, at, delta);
                        if (s != i) moved = true;
                        if (s >= 0) shifted.Add(s);
                    }

                    _selected.Clear();
                    foreach (int i in shifted) _selected.Add(i);

                    _primary = ShiftOne(_primary, at, delta);
                    _anchor = ShiftOne(_anchor, at, delta);
                }

                // 목록이 통째로 바뀌는 경로(Clear, AddRange, 인덱서)의 안전망
                if (_selected.RemoveWhere(i => i < 0 || i >= count) > 0) moved = true;
                if (_primary >= count) _primary = -1;
                if (_anchor >= count) _anchor = -1;

                // 고른 것이 하나도 없으면 대표도 없어야 한다
                if (_selected.Count == 0) { _primary = -1; _anchor = -1; }

                if (moved || _primary != beforePrimary || _selected.Count != beforeCount)
                {
                    Invalidate();
                    Raise();
                }
            }

            /// <summary>빠진 자리에 걸린 위치는 -1이 되고, 뒤쪽 위치는 그만큼 당겨진다.</summary>
            private static int ShiftOne(int index, int at, int delta)
            {
                if (index < at) return index;
                if (delta < 0 && index < at - delta) return -1;
                return index + delta;
            }

            public void SelectSingle(int index, bool raise)
            {
                if (index < -1 || index >= _items.Count) index = -1;
                if (_primary == index && _selected.Count == (index < 0 ? 0 : 1)) return;

                _selected.Clear();
                _primary = index;
                _anchor = index;
                if (index >= 0) _selected.Add(index);

                Invalidate();
                if (index >= 0) _owner.EnsureVisible(index);
                if (raise) Raise();
            }

            private void Raise()
            {
                var handler = SelectionChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }

            private int IndexFromPoint(Point p)
            {
                if (ItemHeight <= 0) return -1;
                int i = p.Y / ItemHeight;
                return (i >= 0 && i < _items.Count) ? i : -1;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var theme = _owner.EffectiveTheme;
                var g = e.Graphics;

                using (var back = new SolidBrush(_owner.Enabled
                                                 ? theme.InputBackground
                                                 : theme.InputBackgroundDisabled))
                    g.FillRectangle(back, ClientRectangle);

                if (ItemHeight <= 0 || _items.Count == 0) { base.OnPaint(e); return; }

                // 보이는 구간만 그린다. 항목이 많아도 그리기 비용이 늘지 않는다
                int first = Math.Max(0, e.ClipRectangle.Top / ItemHeight);
                int last = Math.Min(_items.Count - 1, e.ClipRectangle.Bottom / ItemHeight);

                for (int i = first; i <= last; i++)
                {
                    var r = new Rectangle(0, i * ItemHeight, Width, ItemHeight);
                    Color fore = _owner.Enabled ? theme.Text : theme.TextDisabled;

                    if (_selected.Contains(i))
                    {
                        using (var b = new SolidBrush(_owner.Enabled ? theme.Accent : theme.DisabledFill))
                            g.FillRectangle(b, r);
                        if (_owner.Enabled) fore = theme.OnAccent;
                    }
                    else if (i == _hover && _owner.Enabled)
                    {
                        using (var b = new SolidBrush(theme.SurfaceHover))
                            g.FillRectangle(b, r);
                    }

                    var item = _items[i];
                    string text = item == null ? string.Empty : item.ToString();

                    TextRenderer.DrawText(g, text, Font,
                        new Rectangle(r.X + 8, r.Y, r.Width - 16, r.Height), fore,
                        TextFormatFlags.Left
                      | TextFormatFlags.VerticalCenter
                      | TextFormatFlags.EndEllipsis
                      | TextFormatFlags.NoPrefix);
                }

                base.OnPaint(e);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                int i = IndexFromPoint(e.Location);
                if (i != _hover) { _hover = i; Invalidate(); }
                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                if (_hover != -1) { _hover = -1; Invalidate(); }
                base.OnMouseLeave(e);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left || !_owner.Enabled) { base.OnMouseDown(e); return; }

                Focus();

                int i = IndexFromPoint(e.Location);
                if (i < 0) { base.OnMouseDown(e); return; }

                if (Mode == AdvSelectionMode.One)
                {
                    SelectSingle(i, true);
                }
                else if ((ModifierKeys & Keys.Shift) == Keys.Shift && _anchor >= 0)
                {
                    SelectRange(_anchor, i);
                }
                else if ((ModifierKeys & Keys.Control) == Keys.Control)
                {
                    if (!_selected.Remove(i)) _selected.Add(i);

                    // 마지막 하나를 해제했는데 대표만 남으면 SelectedIndex가
                    // 선택되지 않은 항목을 계속 가리켜 SelectedIndices와 어긋난다
                    if (_selected.Count == 0) { _primary = -1; _anchor = -1; }
                    else { _primary = i; _anchor = i; }

                    Invalidate();
                    Raise();
                }
                else
                {
                    SelectSingle(i, true);
                }

                base.OnMouseDown(e);
            }

            private void SelectRange(int from, int to)
            {
                int lo = Math.Min(from, to), hi = Math.Max(from, to);

                _selected.Clear();
                for (int i = lo; i <= hi; i++) _selected.Add(i);

                _primary = to;
                Invalidate();
                _owner.EnsureVisible(to);
                Raise();
            }

            protected override bool IsInputKey(Keys keyData)
            {
                switch (keyData & Keys.KeyCode)
                {
                    case Keys.Up:
                    case Keys.Down:
                    case Keys.Home:
                    case Keys.End:
                    case Keys.PageUp:
                    case Keys.PageDown:
                        return true;
                }
                return base.IsInputKey(keyData);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (_items.Count == 0) { base.OnKeyDown(e); return; }

                int page = Math.Max(1, Height / Math.Max(1, ItemHeight));
                int next = _primary;

                switch (e.KeyCode)
                {
                    case Keys.Up: next = Math.Max(0, _primary - 1); break;
                    case Keys.Down: next = Math.Min(_items.Count - 1, _primary + 1); break;
                    case Keys.Home: next = 0; break;
                    case Keys.End: next = _items.Count - 1; break;
                    case Keys.PageUp: next = Math.Max(0, _primary - page); break;
                    case Keys.PageDown: next = Math.Min(_items.Count - 1, _primary + page); break;
                    default: base.OnKeyDown(e); return;
                }

                if (_primary < 0) next = 0;

                if (Mode == AdvSelectionMode.MultiExtended
                    && (e.Modifiers & Keys.Shift) == Keys.Shift && _anchor >= 0)
                    SelectRange(_anchor, next);
                else
                    SelectSingle(next, true);

                e.Handled = true;
                base.OnKeyDown(e);
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                _owner.ScrollByWheel(e.Delta);
                base.OnMouseWheel(e);
            }

            protected override void OnGotFocus(EventArgs e) { _owner.Invalidate(); base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { _owner.Invalidate(); base.OnLostFocus(e); }
        }

        /// <summary>항목 목록. 변경되면 선택과 배치를 바로잡는다.</summary>
        public class ObjectCollection : IList
        {
            private readonly AdvListBox _owner;
            private readonly List<object> _list;

            internal ObjectCollection(AdvListBox owner, List<object> list)
            {
                _owner = owner;
                _list = list;
            }

            public int Count { get { return _list.Count; } }
            public bool IsReadOnly { get { return false; } }
            public bool IsFixedSize { get { return false; } }
            public bool IsSynchronized { get { return false; } }
            public object SyncRoot { get { return this; } }

            public object this[int index]
            {
                get { return _list[index]; }
                set { _list[index] = value; Changed(0, 0); }
            }

            public int Add(object value) { _list.Add(value); Changed(0, 0); return _list.Count - 1; }
            public void AddRange(IEnumerable<object> values)
            {
                if (values == null) return;
                _list.AddRange(values);
                Changed(0, 0);
            }
            public void Insert(int index, object value) { _list.Insert(index, value); Changed(index, 1); }
            public void Remove(object value)
            {
                int i = _list.IndexOf(value);
                if (i >= 0) RemoveAt(i);
            }
            public void RemoveAt(int index) { _list.RemoveAt(index); Changed(index, -1); }
            public void Clear() { _list.Clear(); Changed(0, 0); }
            public bool Contains(object value) { return _list.Contains(value); }
            public int IndexOf(object value) { return _list.IndexOf(value); }
            public IEnumerator GetEnumerator() { return _list.GetEnumerator(); }
            public void CopyTo(Array array, int index) { ((IList)_list).CopyTo(array, index); }

            /// <summary>
            /// 항목이 바뀌면 선택 위치도 따라가야 한다. 그러지 않으면 선택이 조용히
            /// 다른 항목을 가리키거나 범위 밖에 남는다.
            /// </summary>
            /// <param name="at">끼워지거나 빠진 위치</param>
            /// <param name="delta">끼워졌으면 양수, 빠졌으면 음수, 자리 변화가 없으면 0</param>
            private void Changed(int at, int delta)
            {
                _owner._core.SyncSelection(at, delta, _list.Count);

                _owner.RefreshLayout();
                _owner.Invalidate();
            }
        }
    }
}
