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
    /// <summary>
    /// 테마를 따르는 콤보박스. 편집형(<see cref="AdvComboBoxStyle.DropDown"/>)일 때는
    /// 글자 입력부만 표준 <see cref="TextBox"/>를 안에 올려 한글 IME를 그대로 쓴다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("SelectedIndexChanged")]
    [DefaultProperty("SelectedIndex")]
    [Description("테마를 따르는 콤보박스입니다.")]
    public class AdvComboBox : AdvControlBase
    {
        private const int ArrowAreaWidth = 18;

        private readonly List<object> _items = new List<object>();
        private readonly ObjectCollection _itemsWrapper;
        private readonly AdvDropDownSettings _dropDown = new AdvDropDownSettings();
        private AdvComboBoxOptions _options;

        private AdvComboPopup _popup;
        private TextBox _editor;
        private int _selectedIndex = -1;
        private bool _syncingEditor;

        private object _dataSource;
        private IList _boundList;
        private string _displayMember = string.Empty;
        private string _valueMember = string.Empty;
        private PropertyDescriptor _displayProperty;
        private PropertyDescriptor _valueProperty;

        [Category("Behavior")]
        [Description("선택 항목이 바뀔 때 발생합니다.")]
        public event EventHandler SelectedIndexChanged;

        [Category("Behavior")]
        [Description("드롭다운이 열릴 때 발생합니다.")]
        public event EventHandler DropDownOpened;

        [Category("Behavior")]
        [Description("드롭다운이 닫힐 때 발생합니다.")]
        public event EventHandler DropDownClosed;

        public AdvComboBox()
        {
            TabStop = true;
            _itemsWrapper = new ObjectCollection(this, _items);
            _dropDown.Changed += DropDownChanged;
            _dropDown.StyleChanged += DropDownStyleChanged;
        }

        protected override Size DefaultSize
        {
            get { return new Size(180, 34); }
        }

        protected override Padding DefaultPadding
        {
            get { return new Padding(8, 4, 8, 4); }
        }

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvComboBoxOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvComboBoxOptions(this)); }
        }

        /// <summary>입력 방식과 드롭다운 목록의 크기 설정.</summary>
        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [Description("입력 방식과 드롭다운 목록 설정입니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public AdvDropDownSettings DropDown
        {
            get { return _dropDown; }
        }

        /// <summary>편집형일 때 안에 올라간 표준 TextBox. 목록 선택형이면 null이다.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TextBox Editor
        {
            get { return _editor; }
        }

        protected override bool ShowsFocusVisual
        {
            get
            {
                if (_dropDown.Style == AdvComboBoxStyle.DropDown && _editor != null)
                    return _editor.Focused;
                return Focused;
            }
        }

        [Category("Data")]
        [Description("목록에 표시할 항목입니다. DataSource를 지정하면 직접 넣을 수 없습니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [MergableProperty(false)]
        public ObjectCollection Items
        {
            get { return _itemsWrapper; }
        }

        [Category("Behavior")]
        [DefaultValue(-1)]
        [Description("선택된 항목의 위치입니다. -1이면 선택 없음입니다.")]
        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (value < -1 || value >= _items.Count) value = -1;
                if (_selectedIndex == value) return;

                _selectedIndex = value;
                if (_popup != null) _popup.List.SelectedIndex = value;

                SyncEditorFromSelection();
                Invalidate();
                OnSelectedIndexChanged(EventArgs.Empty);
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object SelectedItem
        {
            get { return _selectedIndex >= 0 ? _items[_selectedIndex] : null; }
            set { SelectedIndex = value == null ? -1 : _items.IndexOf(value); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsDroppedDown
        {
            get { return _popup != null && _popup.Visible; }
        }

        /// <summary>
        /// 목록 선택형이면 선택 항목의 문자열(읽기 전용), 편집형이면 입력창의 내용이다.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override string Text
        {
            get
            {
                if (_dropDown.Style == AdvComboBoxStyle.DropDown && _editor != null)
                    return _editor.Text;

                return GetItemText(SelectedItem);
            }
            set
            {
                if (_dropDown.Style != AdvComboBoxStyle.DropDown || _editor == null) return;
                _editor.Text = value ?? string.Empty;
            }
        }

        protected virtual void OnSelectedIndexChanged(EventArgs e)
        {
            var handler = SelectedIndexChanged;
            if (handler != null) handler(this, e);
        }

        private void DropDownChanged(object sender, EventArgs e)
        {
            HideDropDown();
            Invalidate();
        }

        private void DropDownStyleChanged(object sender, EventArgs e)
        {
            ApplyStyle();
            Invalidate();
        }

        private int ItemHeight
        {
            get { return _dropDown.ResolveItemHeight(TextRenderer.MeasureText("가Ay", Font).Height); }
        }

        #region 데이터 바인딩

        [Category("Data")]
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

        [Category("Data")]
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
                SyncEditorFromSelection();
                Invalidate();
            }
        }

        [Category("Data")]
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
            _boundList = ResolveList(value);

            var bindingList = _boundList as IBindingList;
            if (bindingList != null) bindingList.ListChanged += BoundListChanged;

            ResolveMembers();
            ReloadFromBoundList();
        }

        /// <summary>
        /// DataTable은 IList가 아니라 IListSource라서 GetList()를 한 번 거쳐야 한다.
        /// </summary>
        private static IList ResolveList(object source)
        {
            if (source == null) return null;

            var listSource = source as IListSource;
            if (listSource != null) return listSource.GetList();

            var list = source as IList;
            if (list != null) return list;

            throw new ArgumentException(
                "DataSource는 IList 또는 IListSource여야 합니다. 받은 형식: " + source.GetType().FullName);
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
        /// 조용히 누락되므로 재조회로 맞춘다.
        /// </summary>
        private void ReloadFromBoundList()
        {
            HideDropDown();

            object previousValue = SelectedValue;

            _items.Clear();
            if (_boundList != null)
            {
                foreach (object item in _boundList) _items.Add(item);
            }

            // 다시 읽은 뒤에도 같은 값이 남아 있으면 선택을 유지한다
            int restored = -1;
            if (previousValue != null)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (Equals(GetItemValue(_items[i]), previousValue)) { restored = i; break; }
                }
            }

            _selectedIndex = restored;
            if (_popup != null) _popup.List.SelectedIndex = restored;

            SyncEditorFromSelection();
            Invalidate();
            OnSelectedIndexChanged(EventArgs.Empty);
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
        private string GetItemText(object item)
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

        #region 레이아웃

        /// <summary>테두리와 안쪽 여백을 뺀, 글자와 화살표가 놓이는 영역.</summary>
        /// <summary>
        /// 글자·편집창이 눌리지 않을 만큼의 높이는 지킨다.
        /// 폭은 줄임표가 처리하므로 제한하지 않는다.
        /// </summary>
        protected override Size MinimumContentSize
        {
            get
            {
                int inner = _editor != null ? _editor.PreferredHeight : Font.Height;
                return new Size(0, ChromeSize.Height + inner);
            }
        }

        private Rectangle ArrowBounds
        {
            get
            {
                var c = ContentBounds;
                return new Rectangle(c.Right - ArrowAreaWidth, c.Top, ArrowAreaWidth, c.Height);
            }
        }

        private Rectangle TextBounds
        {
            get
            {
                var c = ContentBounds;
                return new Rectangle(c.Left, c.Top,
                                     Math.Max(0, c.Width - ArrowAreaWidth - 4), c.Height);
            }
        }

        #endregion

        #region 편집형 입력창

        /// <summary>
        /// 스타일이 바뀔 때 입력창을 만들거나 없앤다. 목록 선택형으로 되돌리면
        /// 입력창을 남겨두지 않는다 — 남으면 보이지 않는 컨트롤이 포커스를 가져간다.
        /// </summary>
        private void ApplyStyle()
        {
            if (_dropDown.Style == AdvComboBoxStyle.DropDown)
            {
                if (_editor != null) return;

                _editor = new TextBox();
                _editor.BorderStyle = BorderStyle.None;
                _editor.TabStop = false;
                _editor.AutoSize = false;

                _editor.TextChanged += EditorTextChanged;
                _editor.GotFocus += EditorFocusChanged;
                _editor.LostFocus += EditorFocusChanged;
                _editor.MouseEnter += EditorMouseEnter;
                _editor.MouseLeave += EditorMouseLeave;
                _editor.KeyDown += EditorKeyDown;

                Controls.Add(_editor);

                ApplyEditorAppearance();
                SyncEditorFromSelection();
                LayoutEditor();
            }
            else if (_editor != null)
            {
                DetachEditor();
                Controls.Remove(_editor);
                _editor.Dispose();
                _editor = null;
            }
        }

        private void DetachEditor()
        {
            if (_editor == null) return;

            _editor.TextChanged -= EditorTextChanged;
            _editor.GotFocus -= EditorFocusChanged;
            _editor.LostFocus -= EditorFocusChanged;
            _editor.MouseEnter -= EditorMouseEnter;
            _editor.MouseLeave -= EditorMouseLeave;
            _editor.KeyDown -= EditorKeyDown;
        }

        private void LayoutEditor()
        {
            if (_editor == null) return;

            var area = TextBounds;
            if (area.Width < 1) area.Width = 1;
            if (area.Height < 1) area.Height = 1;

            int h = _editor.PreferredHeight;
            if (h < area.Height)
                area = new Rectangle(area.X, area.Y + (area.Height - h) / 2, area.Width, h);

            _editor.Bounds = area;
        }

        private void ApplyEditorAppearance()
        {
            if (_editor == null) return;

            var theme = EffectiveTheme;
            _editor.Font = Font;
            _editor.BackColor = Enabled ? theme.InputBackground : theme.InputBackgroundDisabled;
            _editor.ForeColor = Enabled ? theme.Text : theme.TextDisabled;
            _editor.Enabled = Enabled;
        }

        /// <summary>
        /// 목록에서 고른 값을 입력창에 옮긴다. 이때 TextChanged가 되돌아오면서
        /// 선택이 다시 바뀌는 순환이 생기므로 플래그로 막는다.
        /// </summary>
        private void SyncEditorFromSelection()
        {
            if (_editor == null || _dropDown.Style != AdvComboBoxStyle.DropDown) return;

            string text = GetItemText(SelectedItem);
            if (_editor.Text == text) return;

            _syncingEditor = true;
            try
            {
                _editor.Text = text;
                _editor.SelectionStart = text.Length;
            }
            finally
            {
                _syncingEditor = false;
            }
        }

        private void EditorTextChanged(object sender, EventArgs e)
        {
            if (!_syncingEditor)
            {
                // 직접 입력한 내용이 목록에 없으면 선택 없음으로 둔다
                int match = _items.FindIndex(o => o != null && GetItemText(o) == _editor.Text);
                if (match != _selectedIndex)
                {
                    _selectedIndex = match;
                    if (_popup != null) _popup.List.SelectedIndex = match;
                    OnSelectedIndexChanged(EventArgs.Empty);
                }
            }

            OnTextChanged(EventArgs.Empty);
        }

        private void EditorFocusChanged(object sender, EventArgs e) { SetFocusVisual(_editor.Focused); }
        private void EditorMouseEnter(object sender, EventArgs e) { SetHovered(true); }

        private void EditorMouseLeave(object sender, EventArgs e)
        {
            SetHovered(MouseStillInside);
        }

        private void EditorKeyDown(object sender, KeyEventArgs e)
        {
            // 목록 이동·열기 키는 콤보박스가 처리하고, 나머지는 입력창이 그대로 받는다
            switch (e.KeyCode)
            {
                case Keys.Down:
                case Keys.Up:
                case Keys.F4:
                case Keys.Escape:
                    OnKeyDown(e);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
            }
        }

        #endregion

        #region 드롭다운

        public void ShowDropDown()
        {
            if (IsDroppedDown || _items.Count == 0 || !Enabled) return;

            EnsurePopup();

            _popup.ApplyTheme(EffectiveTheme);
            _popup.List.Font = Font;
            _popup.List.ItemHeight = ItemHeight;
            _popup.List.SelectedIndex = _selectedIndex;
            _popup.List.HoverIndex = -1;
            _popup.SetSize(FrameBounds.Width, _items.Count, _dropDown.MaxItems);

            // 프레임 왼쪽 아래에 붙인다. 화면 아래가 좁으면 ToolStripDropDown이 위로 뒤집는다
            var anchor = PointToScreen(new Point(FrameBounds.Left, FrameBounds.Bottom));
            _popup.Show(anchor);
            _popup.EnsureVisible(_selectedIndex);

            Invalidate();
            RaiseIfSet(DropDownOpened);
        }

        public void HideDropDown()
        {
            if (_popup != null && _popup.Visible) _popup.Close();
        }

        /// <summary>
        /// 팝업은 처음 열 때 한 번 만들고 재사용한다. 열 때마다 새로 만들면
        /// 닫기 처리가 겹칠 때 이전 인스턴스가 남는다.
        /// </summary>
        private void EnsurePopup()
        {
            if (_popup != null) return;

            _popup = new AdvComboPopup(_items, EffectiveTheme, Font, ItemHeight);
            _popup.List.TextProvider = GetItemText;
            _popup.ItemChosen += PopupItemChosen;
            _popup.Closed += PopupClosed;
        }

        private void PopupItemChosen(object sender, AdvDropDownList.ItemEventArgs e)
        {
            SelectedIndex = e.Index;
            HideDropDown();
        }

        private void PopupClosed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            Invalidate();
            RaiseIfSet(DropDownClosed);
        }

        private void RaiseIfSet(EventHandler handler)
        {
            if (handler != null) handler(this, EventArgs.Empty);
        }

        #endregion

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = FrameBounds;

            Color fill = Enabled ? theme.InputBackground : theme.InputBackgroundDisabled;

            Color border;
            if (!Enabled) border = theme.Border;
            else if (ShowsFocusVisual || IsDroppedDown) border = theme.BorderFocus;
            else border = AdvGraphics.Blend(theme.Border, theme.BorderHover, HoverAmount);

            AdvFrameRenderer.Draw(g, bounds, theme, EffectiveCorners, EffectiveBorderWidth,
                                  fill, Color.Empty, border, CurrentGlow, CurrentElevation,
                                  EffectiveBorderDash);

            // 편집형이면 글자는 안쪽 입력창이 그린다
            if (_dropDown.Style == AdvComboBoxStyle.DropDownList)
            {
                string text = Text;
                if (!string.IsNullOrEmpty(text))
                {
                    TextRenderer.DrawText(g, text, Font, TextBounds,
                        Enabled ? theme.Text : theme.TextDisabled,
                        TextFormatFlags.Left
                      | TextFormatFlags.VerticalCenter
                      | TextFormatFlags.EndEllipsis
                      | TextFormatFlags.NoPrefix);
                }
            }

            DrawArrow(g, ArrowBounds, Enabled ? theme.TextMuted : theme.TextDisabled);

            base.OnPaint(e);
        }

        private static void DrawArrow(Graphics g, Rectangle area, Color color)
        {
            AdvGraphics.DrawChevron(g, area, AdvGraphics.ChevronDirection.Down, color,
                                    9, 5, 1.6f, 0);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 편집형에서 글자 영역을 누른 것은 커서를 놓으려는 것이므로 목록을 열지 않는다
                bool textAreaClick = _dropDown.Style == AdvComboBoxStyle.DropDown
                                  && !ArrowBounds.Contains(e.Location);

                if (textAreaClick)
                {
                    if (_editor != null && !_editor.Focused) _editor.Focus();
                }
                else
                {
                    if (_editor != null) _editor.Focus();
                    else if (!Focused) Focus();

                    // 열려 있을 때 다시 누르면 닫히도록 한다
                    if (IsDroppedDown) HideDropDown();
                    else ShowDropDown();
                }
            }
            base.OnMouseDown(e);
        }

        /// <summary>방향키·Enter를 컨트롤이 직접 받도록 알린다.</summary>
        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Home:
                case Keys.End:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_items.Count > 0)
            {
                switch (e.KeyCode)
                {
                    case Keys.Down:
                        if (e.Alt) ShowDropDown();
                        else Step(1);
                        e.Handled = true;
                        break;

                    case Keys.Up:
                        Step(-1);
                        e.Handled = true;
                        break;

                    case Keys.Home:
                        SelectedIndex = 0;
                        e.Handled = true;
                        break;

                    case Keys.End:
                        SelectedIndex = _items.Count - 1;
                        e.Handled = true;
                        break;

                    case Keys.Space:
                    case Keys.F4:
                        if (IsDroppedDown) HideDropDown(); else ShowDropDown();
                        e.Handled = true;
                        break;

                    case Keys.Escape:
                        if (IsDroppedDown) { HideDropDown(); e.Handled = true; }
                        break;
                }
            }
            base.OnKeyDown(e);
        }

        private void Step(int delta)
        {
            int next = _selectedIndex + delta;
            if (next < 0) next = 0;
            if (next > _items.Count - 1) next = _items.Count - 1;

            SelectedIndex = next;
            if (IsDroppedDown) _popup.EnsureVisible(next);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // 휠 메시지는 버튼 입력과 달리 비활성 컨트롤에도 전달되므로 직접 막아야 한다.
            // 드롭다운이 열려 있으면 목록 쪽 스크롤이 우선이다
            if (Enabled && !IsDroppedDown && _items.Count > 0)
                Step(e.Delta > 0 ? -1 : 1);

            base.OnMouseWheel(e);
        }

        protected override void OnThemeChanged()
        {
            if (_popup != null) _popup.ApplyTheme(EffectiveTheme);
            ApplyEditorAppearance();
            LayoutEditor();
            base.OnThemeChanged();
        }

        protected override void OnResize(EventArgs e)
        {
            LayoutEditor();
            base.OnResize(e);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            ApplyEditorAppearance();
            LayoutEditor();
            base.OnFontChanged(e);
        }

        protected override void OnPaddingChanged(EventArgs e)
        {
            LayoutEditor();
            base.OnPaddingChanged(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            ApplyEditorAppearance();
            base.OnEnabledChanged(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            if (_editor != null) _editor.Focus();
            base.OnGotFocus(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            // 커서가 안쪽 입력창으로 들어갔을 뿐이면 컨트롤을 벗어난 것이 아니다
            if (_editor != null && MouseStillInside) return;
            base.OnMouseLeave(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyEditorAppearance();
            LayoutEditor();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_popup != null)
                {
                    _popup.ItemChosen -= PopupItemChosen;
                    _popup.Closed -= PopupClosed;
                    _popup.Dispose();
                    _popup = null;
                }

                DetachEditor();
                DetachBoundList();

                _dropDown.Changed -= DropDownChanged;
                _dropDown.StyleChanged -= DropDownStyleChanged;
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 항목 목록. 변경되면 선택 위치를 바로잡고 다시 그린다.
        /// </summary>
        public class ObjectCollection : IList
        {
            private readonly AdvComboBox _owner;
            private readonly List<object> _list;

            internal ObjectCollection(AdvComboBox owner, List<object> list)
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
                set { ThrowIfBound(); _list[index] = value; Changed(); }
            }

            public int Add(object value)
            {
                ThrowIfBound();
                _list.Add(value);
                Changed();
                return _list.Count - 1;
            }

            public void AddRange(IEnumerable<object> values)
            {
                ThrowIfBound();
                if (values == null) return;
                _list.AddRange(values);
                Changed();
            }

            public void Insert(int index, object value)
            {
                ThrowIfBound();
                _list.Insert(index, value);

                // 고른 항목은 그대로인데 위치만 밀린다. 값이 바뀐 게 아니므로 알리지 않는다
                if (_owner._selectedIndex >= index) _owner._selectedIndex++;
                Changed();
            }

            public void Remove(object value)
            {
                ThrowIfBound();
                int i = _list.IndexOf(value);
                if (i >= 0) RemoveAt(i);
            }

            public void RemoveAt(int index)
            {
                ThrowIfBound();
                _list.RemoveAt(index);

                if (_owner._selectedIndex == index)
                    _owner.SelectedIndex = -1;              // 고른 항목이 사라졌다 — 정식 경로로 알린다
                else if (_owner._selectedIndex > index)
                    _owner._selectedIndex--;                // 항목은 그대로, 위치만 당겨진다

                Changed();
            }
            public void Clear() { ThrowIfBound(); _list.Clear(); Changed(); }
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
            /// 항목이 줄어 선택 위치가 범위를 벗어나면 조용히 어긋나므로 여기서 바로잡는다.
            /// </summary>
            private void Changed()
            {
                _owner.HideDropDown();

                if (_owner._selectedIndex >= _list.Count)
                    _owner.SelectedIndex = _list.Count - 1;

                _owner.Invalidate();
            }
        }
    }

    /// <summary>AdvComboBox가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvComboBoxOptions : AdvOptions
    {
        private readonly AdvComboBox _owner;

        internal AdvComboBoxOptions(AdvComboBox owner) : base(owner.Styling)
        {
            _owner = owner;
        }

        [Description("입력 방식과 드롭다운 목록 설정입니다.")]
        public AdvDropDownSettings DropDown
        {
            get { return _owner.DropDown; }
        }
    }
}
