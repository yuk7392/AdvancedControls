using System;
using System.Collections;
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
    /// <summary>목록의 선택 방식.</summary>
    public enum AdvSelectionMode
    {
        /// <summary>한 번에 하나만 고른다.</summary>
        One,
        /// <summary>Ctrl·Shift로 여러 개를 고른다.</summary>
        MultiExtended
    }

    /// <summary>드래그 재배치 이벤트 인자. From이 있던 항목이 To 위치로 옮겨졌다.</summary>
    public class AdvItemsReorderedEventArgs : EventArgs
    {
        public int From { get; private set; }
        public int To { get; private set; }
        public AdvItemsReorderedEventArgs(int from, int to) { From = from; To = to; }
    }

    /// <summary>
    /// 테마를 따르는 목록 상자. 항목은 직접 그리고, 스크롤은 감싼 패널의
    /// AutoScroll에 맡긴다 — 드롭다운과 같은 방식이라 동작이 일관된다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("SelectedIndexChanged")]
    [DefaultProperty("AdvancedControlOptions")]
    [Description("테마를 따르는 목록 상자입니다.")]
    public class AdvListBox : AdvControlBase
    {
        private readonly List<object> _items = new List<object>();
        private readonly ObjectCollection _itemsWrapper;
        private readonly Panel _viewport;
        private readonly ListCore _core;
        private readonly AdvScrollBar _scrollBar;
        private AdvListBoxOptions _options;

        private object _dataSource;
        private IList _boundList;
        private string _displayMember = string.Empty;
        private string _valueMember = string.Empty;
        private PropertyDescriptor _displayProperty;
        private PropertyDescriptor _valueProperty;

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvListBoxOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvListBoxOptions(this)); }
        }

        [Category("Behavior")]
        [Description("선택이 바뀔 때 발생합니다.")]
        public event EventHandler SelectedIndexChanged;

        [Category("Behavior")]
        [Description("항목의 체크 상태가 바뀔 때 발생합니다. CheckBoxes가 켜져 있어야 합니다.")]
        public event EventHandler ItemChecked;

        /// <summary>드래그 재배치로 항목 순서가 바뀐 뒤 발생한다. 선택·체크는 항목을 따라간다.</summary>
        [Category("Behavior")]
        [Description("드래그 재배치로 항목 순서가 바뀐 뒤 발생합니다.")]
        public event EventHandler<AdvItemsReorderedEventArgs> ItemsReordered;

        private bool _allowReorder;

        /// <summary>마우스 드래그로 항목 순서를 바꿀 수 있게 한다. DataSource 바인딩 중에는 동작하지 않는다.</summary>
        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("마우스 드래그로 항목 순서를 바꿀 수 있게 합니다. DataSource 바인딩 중에는 동작하지 않습니다.")]
        public bool AllowItemReorder
        {
            get { return _allowReorder; }
            set { _allowReorder = value; }
        }

        /// <summary>지금 드래그 재배치가 실제로 가능한지(켜져 있고 바인딩 아님).</summary>
        internal bool CanReorderNow
        {
            get { return _allowReorder && _boundList == null; }
        }

        /// <summary>
        /// from 항목을 to 삽입 위치(제거 전 기준, 0~Count)로 옮긴다. 선택·체크 인덱스는 항목을
        /// 따라가고, 항목 자체는 그대로이므로 SelectedIndexChanged는 올리지 않는다.
        /// </summary>
        internal bool MoveItem(int from, int to)
        {
            if (_boundList != null) return false;
            if (from < 0 || from >= _items.Count) return false;
            if (to < 0) to = 0;
            if (to > _items.Count) to = _items.Count;
            int insert = to > from ? to - 1 : to;
            if (insert == from) return false;

            var item = _items[from];
            _items.RemoveAt(from);
            _items.Insert(insert, item);
            _core.RemapAfterMove(from, insert);
            RefreshLayout();
            _core.Invalidate();

            var h = ItemsReordered;
            if (h != null) h(this, new AdvItemsReorderedEventArgs(from, insert));
            return true;
        }

        public AdvListBox()
        {
            _itemsWrapper = new ObjectCollection(this, _items);

            _core = new ListCore(this, _items);
            _core.SelectionChanged += CoreSelectionChanged;
            _core.CheckChanged += CoreCheckChanged;

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

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [Description("목록에 표시할 항목입니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [MergableProperty(false)]
        public ObjectCollection Items
        {
            get { return _itemsWrapper; }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
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

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("각 항목 왼쪽에 체크박스를 표시합니다. 선택과 별개로 여러 항목을 체크할 수 있습니다.")]
        public bool CheckBoxes
        {
            get { return _core.ShowCheckBoxes; }
            set
            {
                if (_core.ShowCheckBoxes == value) return;
                _core.ShowCheckBoxes = value;
                RefreshLayout();
                _core.Invalidate();
            }
        }

        /// <summary>체크된 위치들. 오름차순이다.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int[] CheckedIndices
        {
            get { return _core.GetCheckedIndices(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object[] CheckedItems
        {
            get
            {
                var idx = _core.GetCheckedIndices();
                var result = new object[idx.Length];
                for (int i = 0; i < idx.Length; i++) result[i] = _items[idx[i]];
                return result;
            }
        }

        public bool GetItemChecked(int index) { return _core.IsChecked(index); }
        public void SetItemChecked(int index, bool value) { _core.SetChecked(index, value, true); }

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

        #region 데이터 바인딩

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(null)]
        [RefreshProperties(RefreshProperties.Repaint)]
        [AttributeProvider(typeof(IListSource))]
        [Description("목록을 채울 데이터 원본입니다. DataTable, BindingSource, IList를 받습니다.")]
        public object DataSource
        {
            get { return _dataSource; }
            set
            {
                if (ReferenceEquals(_dataSource, value)) return;
                SetDataSource(value);
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue("")]
        [Description("항목을 표시할 때 쓸 속성(컬럼) 이름입니다. 비우면 ToString()을 씁니다.")]
        public string DisplayMember
        {
            get { return _displayMember; }
            set
            {
                value = value ?? string.Empty;
                if (_displayMember == value) return;
                _displayMember = value;
                ResolveMembers();
                _core.Invalidate();
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue("")]
        [Description("SelectedValue가 돌려줄 속성(컬럼) 이름입니다. 비우면 항목 자체를 돌려줍니다.")]
        public string ValueMember
        {
            get { return _valueMember; }
            set
            {
                value = value ?? string.Empty;
                if (_valueMember == value) return;
                _valueMember = value;
                ResolveMembers();
            }
        }

        /// <summary>선택 항목에서 ValueMember로 뽑은 값. ValueMember가 없으면 항목 자체다.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object SelectedValue
        {
            get
            {
                var item = SelectedItem;
                return item == null ? null : GetItemValue(item);
            }
            set
            {
                if (value == null) { SelectedIndex = -1; return; }

                for (int i = 0; i < _items.Count; i++)
                {
                    if (Equals(GetItemValue(_items[i]), value)) { SelectedIndex = i; return; }
                }

                SelectedIndex = -1;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsBound
        {
            get { return _boundList != null; }
        }

        private void SetDataSource(object value)
        {
            DetachBoundList();

            _dataSource = value;
            _boundList = AdvDataBinding.ResolveList(value);

            var bindingList = _boundList as IBindingList;
            if (bindingList != null) bindingList.ListChanged += BoundListChanged;

            ResolveMembers();
            ReloadFromBoundList();
        }

        private void DetachBoundList()
        {
            var bindingList = _boundList as IBindingList;
            if (bindingList != null) bindingList.ListChanged -= BoundListChanged;

            _boundList = null;
        }

        private void BoundListChanged(object sender, ListChangedEventArgs e)
        {
            ReloadFromBoundList();
        }

        /// <summary>
        /// 원본이 바뀌면 통째로 다시 읽는다. 항목을 하나씩 옮기면 필드가 늘었을 때
        /// 조용히 누락되므로 재조회로 맞춘다. 선택은 값 기준으로 복원하고,
        /// 체크는 옛 인덱스가 새 목록과 무관해지므로 비운다.
        /// </summary>
        private void ReloadFromBoundList()
        {
            object previousValue = SelectedValue;

            _items.Clear();
            if (_boundList != null)
            {
                foreach (object item in _boundList) _items.Add(item);
            }

            _core.ResetStateForReload();

            // 다시 읽은 뒤에도 같은 값이 남아 있으면 선택을 유지한다
            if (previousValue != null)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (Equals(GetItemValue(_items[i]), previousValue)) { _core.SelectSingle(i, false); break; }
                }
            }

            RefreshLayout();
            Invalidate();
            CoreSelectionChanged(this, EventArgs.Empty);
        }

        /// <summary>
        /// DisplayMember·ValueMember 이름을 실제 속성 기술자로 바꾼다.
        /// ListBindingHelper를 쓰면 DataTable의 컬럼도 같은 방식으로 잡힌다.
        /// </summary>
        private void ResolveMembers()
        {
            _displayProperty = null;
            _valueProperty = null;

            if (_displayMember.Length == 0 && _valueMember.Length == 0) return;

            object source = (object)_boundList ?? _dataSource;
            if (source == null) return;

            var props = ListBindingHelper.GetListItemProperties(source);
            if (props == null) return;

            if (_displayMember.Length > 0) _displayProperty = props.Find(_displayMember, true);
            if (_valueMember.Length > 0) _valueProperty = props.Find(_valueMember, true);
        }

        /// <summary>항목을 화면에 표시할 글자.</summary>
        internal string GetItemText(object item)
        {
            if (item == null) return string.Empty;

            if (_displayProperty != null)
            {
                object v = _displayProperty.GetValue(item);
                return v == null ? string.Empty : v.ToString();
            }

            return item.ToString();
        }

        private object GetItemValue(object item)
        {
            if (item == null) return null;
            return _valueProperty != null ? _valueProperty.GetValue(item) : item;
        }

        #endregion

        private void CoreSelectionChanged(object sender, EventArgs e)
        {
            var handler = SelectedIndexChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void CoreCheckChanged(object sender, EventArgs e)
        {
            var handler = ItemChecked;
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

            ApplyRoundedRegion();
        }

        /// <summary>
        /// 사각 자식(뷰포트·스크롤바)의 모서리가 둥근 테두리 밖으로 튀어나오지 않도록
        /// 컨트롤 전체를 둥근 모양으로 잘라 낸다. 테두리(및 그림자)는 OnPaint가 그리므로,
        /// 잘리지 않게 프레임보다 1px 넉넉히 잡는다. Elevated면 그림자가 잘리지 않도록 자르지 않는다.
        /// </summary>
        private Rectangle _regionClip = Rectangle.Empty;   // 마지막으로 Region을 만든 클립(리사이즈 중 재생성 방지)

        private void ApplyRoundedRegion()
        {
            var clip = Rectangle.Inflate(FrameBounds, 1, 1);
            AdvGraphics.UpdateRoundedRegion(this, clip, EffectiveCorners, Styling.Elevated, ref _regionClip);
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
        protected override void OnThemeChanged() { _regionClip = Rectangle.Empty; RefreshLayout(); base.OnThemeChanged(); }

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
                DetachBoundList();
                _core.SelectionChanged -= CoreSelectionChanged;
                _core.CheckChanged -= CoreCheckChanged;
                _scrollBar.ValueChanged -= ScrollBarValueChanged;
                if (Region != null) Region.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 실제 항목을 그리고 마우스·키보드를 받는 안쪽 컨트롤.
        /// 스크롤은 부모 패널이 담당하므로 여기는 전체 높이를 그대로 갖는다.
        /// </summary>
        private class ListCore : Control
        {
            private const int CheckBoxSize = 16;
            private const int CheckLeftPad = 8;

            private readonly AdvListBox _owner;
            private readonly List<object> _items;
            private readonly HashSet<int> _selected = new HashSet<int>();
            private readonly HashSet<int> _checked = new HashSet<int>();

            private int _primary = -1;
            private int _anchor = -1;
            private int _hover = -1;

            // 드래그 재배치 상태
            private int _dragIndex = -1;     // 눌린 항목(아직 드래그 아닐 수 있음)
            private bool _dragActive;
            private Point _dragStart;
            private int _dropIndex = -1;     // 삽입 위치(0~Count)

            public event EventHandler SelectionChanged;
            public event EventHandler CheckChanged;

            public AdvSelectionMode Mode { get; set; }
            public int ItemHeight { get; set; }
            public bool ShowCheckBoxes { get; set; }

            /// <summary>체크박스가 차지하는 왼쪽 폭(체크박스가 없으면 0).</summary>
            private int CheckGutter
            {
                get { return ShowCheckBoxes ? AdvGraphics.Scale(this, CheckLeftPad + CheckBoxSize) : 0; }
            }

            public bool IsChecked(int index)
            {
                return index >= 0 && index < _items.Count && _checked.Contains(index);
            }

            public int[] GetCheckedIndices()
            {
                var list = new List<int>(_checked);
                list.Sort();
                return list.ToArray();
            }

            public void SetChecked(int index, bool value, bool raise)
            {
                if (index < 0 || index >= _items.Count) return;
                bool changed = value ? _checked.Add(index) : _checked.Remove(index);
                if (!changed) return;

                Invalidate();
                if (raise) RaiseCheck();
                NotifyAccStateChange(index);
            }

            private void ToggleCheck(int index)
            {
                if (index < 0 || index >= _items.Count) return;
                if (!_checked.Remove(index)) _checked.Add(index);
                Invalidate();
                RaiseCheck();
                NotifyAccStateChange(index);
            }

            private void RaiseCheck()
            {
                var handler = CheckChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }

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

            /// <summary>
            /// 바인딩 재조회처럼 목록이 통째로 바뀔 때 옛 인덱스 상태를 조용히 비운다.
            /// 이벤트는 호출자가 재조회를 끝낸 뒤 한 번만 올린다.
            /// </summary>
            public void ResetStateForReload()
            {
                _selected.Clear();
                _checked.Clear();
                _primary = -1;
                _anchor = -1;
                _hover = -1;
                Invalidate();
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

                // 체크 위치도 같은 규칙으로 옮긴다 — 두지 않으면 체크가 조용히 다른 항목을 가리킨다
                int beforeChecked = _checked.Count;
                bool checkMoved = false;

                if (delta != 0)
                {
                    var shiftedC = new List<int>();
                    foreach (int i in _checked)
                    {
                        int s = ShiftOne(i, at, delta);
                        if (s != i) checkMoved = true;
                        if (s >= 0) shiftedC.Add(s);
                    }
                    _checked.Clear();
                    foreach (int i in shiftedC) _checked.Add(i);
                }

                if (_checked.RemoveWhere(i => i < 0 || i >= count) > 0) checkMoved = true;

                if (checkMoved || _checked.Count != beforeChecked)
                {
                    Invalidate();
                    RaiseCheck();
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
                NotifyAccFocus(index);
            }

            private void Raise()
            {
                var handler = SelectionChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }

            // 키보드/프로그램 선택 이동을 스크린리더에 라이브로 알린다.
            // AccessibilityNotifyClients는 내부적으로 childID+1을 NotifyWinEvent로 보내고,
            // MSAA는 이를 GetChild(childID)로 되돌리므로 0-based 인덱스를 그대로 넘긴다.
            private void NotifyAccFocus(int index)
            {
                if (index < 0 || !Focused) return;   // 포커스가 없을 땐 알리지 않는다(엉뚱한 낭독 방지)
                AccessibilityNotifyClients(AccessibleEvents.Focus, index);
                AccessibilityNotifyClients(AccessibleEvents.Selection, index);
            }

            private void NotifyAccStateChange(int index)
            {
                if (index >= 0) AccessibilityNotifyClients(AccessibleEvents.StateChange, index);
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

                    if (ShowCheckBoxes)
                        DrawItemCheckBox(g, r, _checked.Contains(i), theme);

                    int gutter = CheckGutter;
                    string text = _owner.GetItemText(_items[i]);

                    TextRenderer.DrawText(g, text, Font,
                        new Rectangle(r.X + 8 + gutter, r.Y, r.Width - 16 - gutter, r.Height), fore,
                        TextFormatFlags.Left
                      | TextFormatFlags.VerticalCenter
                      | TextFormatFlags.EndEllipsis
                      | TextFormatFlags.NoPrefix);
                }

                // 드래그 재배치 중 삽입 위치 표시선
                if (_dragActive && _dropIndex >= 0)
                {
                    int ly = Math.Min(_dropIndex * ItemHeight, Math.Max(0, Height - 1));
                    using (var pen = new Pen(theme.Accent, 2f))
                        g.DrawLine(pen, 0, ly, Width, ly);
                }

                base.OnPaint(e);
            }

            /// <summary>항목 왼쪽에 체크박스를 그린다. 도형 자체는 트리와 공용 헬퍼가 그린다.</summary>
            private void DrawItemCheckBox(Graphics g, Rectangle row, bool isChecked, AdvTheme theme)
            {
                int sz = AdvGraphics.Scale(this, CheckBoxSize);
                int pad = AdvGraphics.Scale(this, CheckLeftPad);
                int y = row.Y + (row.Height - sz) / 2;
                var box = new Rectangle(row.X + pad, y, sz, sz);

                AdvGraphics.DrawItemCheckBox(g, this, box, isChecked, _owner.Enabled, theme);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                // 드래그 재배치: 일정 거리 이상 끌면 시작하고, 이후엔 삽입 위치만 추적한다
                if (_dragIndex >= 0 && (e.Button & MouseButtons.Left) != 0)
                {
                    if (!_dragActive)
                    {
                        var sz = SystemInformation.DragSize;
                        if (Math.Abs(e.X - _dragStart.X) > sz.Width / 2
                         || Math.Abs(e.Y - _dragStart.Y) > sz.Height / 2)
                        {
                            _dragActive = true;
                            Capture = true;
                        }
                    }
                    if (_dragActive)
                    {
                        int drop = e.Y < 0 ? 0 : (e.Y + ItemHeight / 2) / ItemHeight;
                        drop = Math.Max(0, Math.Min(_items.Count, drop));
                        if (drop != _dropIndex) { _dropIndex = drop; Invalidate(); }
                        base.OnMouseMove(e);
                        return;
                    }
                }

                int i = IndexFromPoint(e.Location);
                if (i != _hover) { _hover = i; Invalidate(); }
                base.OnMouseMove(e);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                if (_dragActive)
                {
                    int from = _dragIndex, to = _dropIndex;
                    CancelDrag();
                    _owner.MoveItem(from, to);
                }
                else _dragIndex = -1;
                base.OnMouseUp(e);
            }

            protected override void OnMouseCaptureChanged(EventArgs e)
            {
                base.OnMouseCaptureChanged(e);
                if (_dragActive) CancelDrag();   // 모달·Alt+Tab 등으로 캡처가 풀리면 드래그 취소
            }

            private void CancelDrag()
            {
                _dragActive = false;
                _dragIndex = -1;
                _dropIndex = -1;
                Capture = false;
                Invalidate();
            }

            /// <summary>항목 이동(from→to) 뒤 선택·체크·기준 인덱스를 항목을 따라가게 다시 매긴다.</summary>
            public void RemapAfterMove(int from, int to)
            {
                Func<int, int> map = i =>
                    i == from ? to
                    : from < to ? (i > from && i <= to ? i - 1 : i)
                    : (i >= to && i < from ? i + 1 : i);

                var sel = new List<int>(_selected);
                _selected.Clear();
                foreach (var s in sel) _selected.Add(map(s));

                var chk = new List<int>(_checked);
                _checked.Clear();
                foreach (var c in chk) _checked.Add(map(c));

                if (_primary >= 0) _primary = map(_primary);
                if (_anchor >= 0) _anchor = map(_anchor);
                _hover = -1;
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

                // 체크박스 영역을 누르면 선택은 그대로 두고 체크만 토글한다
                if (ShowCheckBoxes && e.X < CheckGutter)
                {
                    ToggleCheck(i);
                    base.OnMouseDown(e);
                    return;
                }

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
                    NotifyAccFocus(_primary);
                }
                else
                {
                    SelectSingle(i, true);
                }

                // 선택 처리 뒤 드래그 재배치를 무장한다(실제 시작은 이동 거리로 판단)
                if (_owner.CanReorderNow)
                {
                    _dragIndex = i;
                    _dragStart = e.Location;
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
                NotifyAccFocus(to);
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
                    case Keys.Space:
                        return true;
                }
                return base.IsInputKey(keyData);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (_items.Count == 0) { base.OnKeyDown(e); return; }

                // 스페이스로 대표 항목의 체크를 토글한다
                if (e.KeyCode == Keys.Space && ShowCheckBoxes && _primary >= 0)
                {
                    ToggleCheck(_primary);
                    e.Handled = true;
                    base.OnKeyDown(e);
                    return;
                }

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

            // ── 접근성(스크린리더/UI Automation) ─────────────────────────
            // 포커스가 이 안쪽 컨트롤에 오므로 항목 트리도 여기에 붙인다(바깥 AdvListBox가 아니라).

            protected override AccessibleObject CreateAccessibilityInstance()
            {
                return new ListAccessibleObject(this);
            }

            private sealed class ListAccessibleObject : ControlAccessibleObject
            {
                private readonly ListCore _core;
                public ListAccessibleObject(ListCore core) : base(core) { _core = core; }

                public override AccessibleRole Role { get { return AccessibleRole.List; } }

                // 이름은 바깥 호스트(AdvListBox)의 AccessibleName을 따른다
                public override string Name
                {
                    get
                    {
                        var n = _core._owner.AccessibleName;
                        return !string.IsNullOrEmpty(n) ? n : base.Name;
                    }
                    set { base.Name = value; }
                }

                public override int GetChildCount() { return _core._items.Count; }

                public override AccessibleObject GetChild(int index)
                {
                    return index >= 0 && index < _core._items.Count
                        ? new ItemAccessibleObject(_core, index) : null;
                }

                public override AccessibleObject GetSelected()
                {
                    int p = _core._primary;
                    return p >= 0 && p < _core._items.Count ? new ItemAccessibleObject(_core, p) : null;
                }

                private sealed class ItemAccessibleObject : AccessibleObject
                {
                    private readonly ListCore _c;
                    private readonly int _i;
                    public ItemAccessibleObject(ListCore c, int i) { _c = c; _i = i; }

                    public override AccessibleObject Parent { get { return _c.AccessibilityObject; } }
                    public override AccessibleRole Role { get { return AccessibleRole.ListItem; } }

                    public override string Name
                    {
                        get
                        {
                            if (_i < 0 || _i >= _c._items.Count) return null;
                            return _c._owner.GetItemText(_c._items[_i]);
                        }
                    }

                    public override AccessibleStates State
                    {
                        get
                        {
                            var s = AccessibleStates.Selectable | AccessibleStates.Focusable;
                            if (!_c._owner.Enabled) s |= AccessibleStates.Unavailable;
                            if (_c._selected.Contains(_i)) s |= AccessibleStates.Selected;
                            if (_i == _c._primary && _c.Focused) s |= AccessibleStates.Focused;
                            if (_c.ShowCheckBoxes)
                                s |= _c._checked.Contains(_i) ? AccessibleStates.Checked : AccessibleStates.None;
                            return s;
                        }
                    }

                    public override Rectangle Bounds
                    {
                        get
                        {
                            int h = _c.ItemHeight;
                            if (h <= 0) return Rectangle.Empty;
                            // 코어는 스크롤을 위해 Top이 음수로 밀려 있고, RectangleToScreen이 이를 반영한다
                            return _c.RectangleToScreen(new Rectangle(0, _i * h, _c.Width, h));
                        }
                    }

                    // 체크박스 목록은 체크 토글이, 일반 목록은 선택이 기본 동작이다
                    // (컨트롤의 Space 키 동작과 일치)
                    public override string DefaultAction
                    {
                        get
                        {
                            if (_c.ShowCheckBoxes)
                                return _c._checked.Contains(_i) ? "선택 해제" : "선택";
                            return "선택";
                        }
                    }

                    public override void DoDefaultAction()
                    {
                        if (_i < 0 || _i >= _c._items.Count) return;
                        if (_c.ShowCheckBoxes) _c.ToggleCheck(_i);
                        else _c.SelectSingle(_i, true);
                    }
                }
            }
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
                set { ThrowIfBound(); _list[index] = value; Changed(0, 0); }
            }

            public int Add(object value) { ThrowIfBound(); _list.Add(value); Changed(0, 0); return _list.Count - 1; }
            public void AddRange(IEnumerable<object> values)
            {
                ThrowIfBound();
                if (values == null) return;
                _list.AddRange(values);
                Changed(0, 0);
            }
            public void Insert(int index, object value) { ThrowIfBound(); _list.Insert(index, value); Changed(index, 1); }
            public void Remove(object value)
            {
                int i = _list.IndexOf(value);
                if (i >= 0) RemoveAt(i);
            }
            public void RemoveAt(int index) { ThrowIfBound(); _list.RemoveAt(index); Changed(index, -1); }
            public void Clear() { ThrowIfBound(); _list.Clear(); Changed(0, 0); }
            public bool Contains(object value) { return _list.Contains(value); }
            public int IndexOf(object value) { return _list.IndexOf(value); }
            public IEnumerator GetEnumerator() { return _list.GetEnumerator(); }
            public void CopyTo(Array array, int index) { ((IList)_list).CopyTo(array, index); }

            /// <summary>
            /// 바인딩 중에 직접 항목을 건드리면 다음 새로고침에서 조용히 사라진다.
            /// 조용히 무시하지 말고 바로 알린다.
            /// </summary>
            private void ThrowIfBound()
            {
                if (_owner._boundList != null)
                    throw new InvalidOperationException(
                        "DataSource가 지정된 동안에는 Items를 직접 바꿀 수 없습니다. 원본 목록을 수정하세요.");
            }

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

    /// <summary>AdvListBox가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvListBoxOptions : AdvOptions
    {
        private readonly AdvListBox _owner;

        internal AdvListBoxOptions(AdvListBox owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [Description("목록에 표시할 항목입니다. DataSource를 지정하면 직접 넣을 수 없습니다.")]
        public AdvListBox.ObjectCollection Items
        {
            get { return _owner.Items; }
        }

        [DefaultValue(null)]
        [RefreshProperties(RefreshProperties.Repaint)]
        [AttributeProvider(typeof(IListSource))]
        [Description("목록을 채울 데이터 원본입니다. DataTable, BindingSource, IList를 받습니다.")]
        public object DataSource
        {
            get { return _owner.DataSource; }
            set { _owner.DataSource = value; }
        }

        [DefaultValue("")]
        [Description("항목을 표시할 때 쓸 속성(컬럼) 이름입니다. 비우면 ToString()을 씁니다.")]
        public string DisplayMember
        {
            get { return _owner.DisplayMember; }
            set { _owner.DisplayMember = value; }
        }

        [DefaultValue("")]
        [Description("SelectedValue가 돌려줄 속성(컬럼) 이름입니다. 비우면 항목 자체를 돌려줍니다.")]
        public string ValueMember
        {
            get { return _owner.ValueMember; }
            set { _owner.ValueMember = value; }
        }

        [DefaultValue(AdvSelectionMode.One)]
        [Description("한 개만 고를지, 여러 개를 고를지 정합니다.")]
        public AdvSelectionMode SelectionMode
        {
            get { return _owner.SelectionMode; }
            set { _owner.SelectionMode = value; }
        }

        [DefaultValue(false)]
        [Description("마우스 드래그로 항목 순서를 바꿀 수 있게 합니다. DataSource 바인딩 중에는 동작하지 않습니다.")]
        public bool AllowItemReorder
        {
            get { return _owner.AllowItemReorder; }
            set { _owner.AllowItemReorder = value; }
        }

        [DefaultValue(false)]
        [Description("각 항목 왼쪽에 체크박스를 표시합니다. 선택과 별개로 여러 항목을 체크할 수 있습니다.")]
        public bool CheckBoxes
        {
            get { return _owner.CheckBoxes; }
            set { _owner.CheckBoxes = value; }
        }
    }
}
