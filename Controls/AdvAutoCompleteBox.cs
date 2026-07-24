using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Controls.Internal;

namespace AdvancedControls.Controls
{
    /// <summary>자동완성 제안을 거르는 방식.</summary>
    public enum AdvAutoCompleteFilter
    {
        /// <summary>입력으로 시작하는 항목만.</summary>
        StartsWith,
        /// <summary>입력을 포함하는 항목 전부.</summary>
        Contains
    }

    /// <summary>
    /// 입력하는 동안 아래로 제안 목록이 뜨는 자동완성 입력창. 콤보박스와 달리
    /// 목록에 없는 자유 입력도 그대로 허용한다. <see cref="AdvTextBox"/>의 셸을
    /// 물려받고, 제안 팝업은 콤보 드롭다운 인프라(<see cref="AdvComboPopup"/>)를 재사용한다.
    /// 항목은 Items로 직접 넣거나 DataSource(+DisplayMember)로 바인딩한다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("SuggestionChosen")]
    [DefaultProperty("AdvancedControlOptions")]
    [Description("입력 중 제안 목록이 뜨는 자동완성 입력창입니다.")]
    public class AdvAutoCompleteBox : AdvTextBox
    {
        private readonly List<object> _items = new List<object>();
        private readonly List<object> _visible = new List<object>();   // 팝업이 이 목록을 참조한다
        private ObjectCollection _itemsWrapper;
        private AdvComboPopup _popup;
        private bool _choosing;   // 제안 선택으로 Text를 바꾸는 중(재필터 방지)

        private AdvAutoCompleteFilter _filter = AdvAutoCompleteFilter.StartsWith;
        private int _maxSuggestions = 8;
        private int _minLength = 1;
        private object _selectedItem;

        private object _dataSource;
        private IList _boundList;
        private string _displayMember = string.Empty;
        private PropertyDescriptor _displayProperty;

        private AdvAutoCompleteBoxOptions _acOptions;

        /// <summary>제안 목록에서 항목을 고르면 발생한다(자유 입력에는 발생하지 않는다).</summary>
        [Category("Behavior")]
        [Description("제안 목록에서 항목을 고르면 발생합니다.")]
        public event EventHandler SuggestionChosen;

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new AdvAutoCompleteBoxOptions AdvancedControlOptions
        {
            get { return _acOptions ?? (_acOptions = new AdvAutoCompleteBoxOptions(this)); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [Description("제안할 항목입니다. DataSource를 지정하면 직접 넣을 수 없습니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [MergableProperty(false)]
        public ObjectCollection Items
        {
            get { return _itemsWrapper ?? (_itemsWrapper = new ObjectCollection(this)); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvAutoCompleteFilter.StartsWith)]
        [Description("제안을 거르는 방식입니다. StartsWith=입력으로 시작, Contains=입력 포함.")]
        public AdvAutoCompleteFilter FilterMode
        {
            get { return _filter; }
            set { _filter = value; }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(8)]
        [Description("한 번에 보여줄 최대 제안 수입니다.")]
        public int MaxSuggestions
        {
            get { return _maxSuggestions; }
            set { _maxSuggestions = value < 1 ? 1 : value; }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(1)]
        [Description("제안을 띄우기 시작하는 최소 글자 수입니다.")]
        public int MinLength
        {
            get { return _minLength; }
            set { _minLength = value < 0 ? 0 : value; }
        }

        /// <summary>마지막으로 제안에서 고른 항목. 텍스트를 손으로 고치면 null로 돌아간다.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object SelectedItem
        {
            get { return _selectedItem; }
        }

        /// <summary>제안 팝업이 떠 있는지.</summary>
        [Browsable(false)]
        public bool IsSuggesting
        {
            get { return _popup != null && _popup.Visible; }
        }

        #region 데이터 바인딩

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(null)]
        [RefreshProperties(RefreshProperties.Repaint)]
        [AttributeProvider(typeof(IListSource))]
        [Description("제안 목록을 채울 데이터 원본입니다. DataTable, BindingSource, IList를 받습니다.")]
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

        /// <summary>원본이 바뀌면 통째로 다시 읽는다(콤보 관례).</summary>
        private void ReloadFromBoundList()
        {
            _items.Clear();
            if (_boundList != null)
            {
                foreach (object item in _boundList) _items.Add(item);
            }
            CloseSuggestions();
        }

        private void ResolveMembers()
        {
            _displayProperty = null;
            if (_displayMember.Length == 0) return;

            object source = (object)_boundList ?? _dataSource;
            if (source == null) return;

            var props = ListBindingHelper.GetListItemProperties(source);
            if (props != null) _displayProperty = props.Find(_displayMember, true);
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

        #endregion

        #region 제안 팝업

        /// <summary>현재 입력에 맞는 제안들. 필터 규칙(StartsWith/Contains)·대소문자 무시.</summary>
        internal List<object> ComputeMatches(string text)
        {
            var result = new List<object>();
            text = text ?? string.Empty;

            foreach (var item in _items)
            {
                string s = GetItemText(item);
                bool hit = _filter == AdvAutoCompleteFilter.StartsWith
                    ? s.StartsWith(text, StringComparison.CurrentCultureIgnoreCase)
                    : s.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0;
                if (hit) result.Add(item);
            }
            return result;
        }

        private int ItemHeight
        {
            get { return TextRenderer.MeasureText("가Ay", Font).Height + 8; }
        }

        private void UpdateSuggestions()
        {
            string text = Text;
            if (text.Length < _minLength) { CloseSuggestions(); return; }

            var matches = ComputeMatches(text);

            // 유일한 제안이 이미 입력과 똑같으면 띄울 이유가 없다
            if (matches.Count == 1 && string.Equals(GetItemText(matches[0]), text,
                    StringComparison.CurrentCultureIgnoreCase))
            { CloseSuggestions(); return; }

            if (matches.Count == 0) { CloseSuggestions(); return; }

            _visible.Clear();
            _visible.AddRange(matches);
            OpenPopup();
        }

        private void OpenPopup()
        {
            EnsurePopup();

            _popup.ApplyTheme(EffectiveTheme);
            _popup.List.Font = Font;
            _popup.List.ItemHeight = ItemHeight;
            _popup.List.SelectedIndex = -1;
            _popup.List.HoverIndex = -1;
            _popup.SetSize(FrameBounds.Width, _visible.Count, _maxSuggestions);

            if (!_popup.Visible)
            {
                var anchor = PointToScreen(new Point(FrameBounds.Left, FrameBounds.Bottom));
                _popup.Show(anchor);
            }
            else
            {
                _popup.List.Invalidate();
            }
        }

        private void EnsurePopup()
        {
            if (_popup != null) return;

            _popup = new AdvComboPopup(_visible, EffectiveTheme, Font, ItemHeight);
            _popup.List.TextProvider = GetItemText;
            _popup.ItemChosen += PopupItemChosen;
        }

        /// <summary>제안 팝업을 닫는다.</summary>
        public void CloseSuggestions()
        {
            if (_popup != null && _popup.Visible) _popup.Close();
        }

        private void PopupItemChosen(object sender, AdvDropDownList.ItemEventArgs e)
        {
            if (e.Index >= 0 && e.Index < _visible.Count) Choose(_visible[e.Index]);
        }

        private void Choose(object item)
        {
            _choosing = true;
            try
            {
                Text = GetItemText(item);
                InnerTextBox.SelectionStart = Text.Length;
                InnerTextBox.SelectionLength = 0;
            }
            finally { _choosing = false; }

            _selectedItem = item;
            CloseSuggestions();

            var h = SuggestionChosen;
            if (h != null) h(this, EventArgs.Empty);
        }

        #endregion

        #region 입력 처리

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (_choosing) return;

            _selectedItem = null;   // 손으로 고치면 '고른 항목'이 아니다

            // 포커스가 있을 때만 제안한다(프로그램이 Text를 넣을 때 불쑥 뜨지 않게)
            if (InnerTextBox.Focused) UpdateSuggestions();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (IsSuggesting)
            {
                switch (e.KeyCode)
                {
                    case Keys.Down:
                    case Keys.Up:
                    {
                        int n = _visible.Count;
                        int cur = _popup.List.SelectedIndex;
                        int next = e.KeyCode == Keys.Down
                            ? (cur + 1 >= n ? 0 : cur + 1)
                            : (cur <= 0 ? n - 1 : cur - 1);
                        _popup.List.SelectedIndex = next;
                        _popup.EnsureVisible(next);
                        e.Handled = true;
                        break;
                    }
                    case Keys.Enter:
                    {
                        int cur = _popup.List.SelectedIndex;
                        if (cur >= 0 && cur < _visible.Count) Choose(_visible[cur]);
                        else CloseSuggestions();
                        e.Handled = true;
                        e.SuppressKeyPress = true;   // 딩 소리·다이얼로그 기본 버튼 방지
                        break;
                    }
                    case Keys.Escape:
                        CloseSuggestions();
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        break;
                }
            }
            else if (e.KeyCode == Keys.Down && !e.Alt)
            {
                // 닫힌 상태에서 ↓ → 현재 입력 기준 제안을 연다(빈 입력이면 전체)
                _visible.Clear();
                _visible.AddRange(ComputeMatches(Text.Length >= _minLength ? Text : string.Empty));
                if (_visible.Count > 0) { OpenPopup(); e.Handled = true; }
            }

            base.OnKeyDown(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            // 팝업 자체 클릭은 ToolStripDropDown이 처리하고, 그 외로 포커스가 떠나면 닫는다
            if (!IsSuggesting || !_popup.Focused) CloseSuggestions();
            base.OnLostFocus(e);
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DetachBoundList();
                if (_popup != null)
                {
                    _popup.ItemChosen -= PopupItemChosen;
                    _popup.Dispose();
                    _popup = null;
                }
            }
            base.Dispose(disposing);
        }

        /// <summary>제안 항목 목록. 바인딩 중에는 직접 수정할 수 없다.</summary>
        public class ObjectCollection : IList
        {
            private readonly AdvAutoCompleteBox _owner;

            internal ObjectCollection(AdvAutoCompleteBox owner) { _owner = owner; }

            private List<object> L { get { return _owner._items; } }

            public int Count { get { return L.Count; } }
            public bool IsReadOnly { get { return false; } }
            public bool IsFixedSize { get { return false; } }
            public bool IsSynchronized { get { return false; } }
            public object SyncRoot { get { return this; } }

            public object this[int index]
            {
                get { return L[index]; }
                set { ThrowIfBound(); L[index] = value; }
            }

            public int Add(object value) { ThrowIfBound(); L.Add(value); return L.Count - 1; }

            public void AddRange(IEnumerable<object> values)
            {
                ThrowIfBound();
                if (values == null) return;
                L.AddRange(values);
            }

            public void Insert(int index, object value) { ThrowIfBound(); L.Insert(index, value); }
            public void Remove(object value) { ThrowIfBound(); L.Remove(value); }
            public void RemoveAt(int index) { ThrowIfBound(); L.RemoveAt(index); }
            public void Clear() { ThrowIfBound(); L.Clear(); }
            public bool Contains(object value) { return L.Contains(value); }
            public int IndexOf(object value) { return L.IndexOf(value); }
            public IEnumerator GetEnumerator() { return L.GetEnumerator(); }
            public void CopyTo(Array array, int index) { ((IList)L).CopyTo(array, index); }

            private void ThrowIfBound()
            {
                if (_owner._boundList != null)
                    throw new InvalidOperationException(
                        "DataSource가 지정된 동안에는 Items를 직접 바꿀 수 없습니다. 원본 목록을 수정하세요.");
            }
        }
    }

    /// <summary>AdvAutoCompleteBox가 추가한 속성. AdvTextBox 파사드에 제안 항목을 얹는다.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvAutoCompleteBoxOptions : AdvTextBoxOptions
    {
        private readonly AdvAutoCompleteBox _owner;

        internal AdvAutoCompleteBoxOptions(AdvAutoCompleteBox owner) : base(owner)
        {
            _owner = owner;
        }

        [Description("제안할 항목입니다. DataSource를 지정하면 직접 넣을 수 없습니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [MergableProperty(false)]
        public AdvAutoCompleteBox.ObjectCollection Items
        {
            get { return _owner.Items; }
        }

        [DefaultValue(AdvAutoCompleteFilter.StartsWith)]
        [Description("제안을 거르는 방식입니다. StartsWith=입력으로 시작, Contains=입력 포함.")]
        public AdvAutoCompleteFilter FilterMode
        {
            get { return _owner.FilterMode; }
            set { _owner.FilterMode = value; }
        }

        [DefaultValue(8)]
        [Description("한 번에 보여줄 최대 제안 수입니다.")]
        public int MaxSuggestions
        {
            get { return _owner.MaxSuggestions; }
            set { _owner.MaxSuggestions = value; }
        }

        [DefaultValue(1)]
        [Description("제안을 띄우기 시작하는 최소 글자 수입니다.")]
        public int MinLength
        {
            get { return _owner.MinLength; }
            set { _owner.MinLength = value; }
        }

        [DefaultValue(null)]
        [RefreshProperties(RefreshProperties.Repaint)]
        [AttributeProvider(typeof(IListSource))]
        [Description("제안 목록을 채울 데이터 원본입니다. DataTable, BindingSource, IList를 받습니다.")]
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
    }
}
