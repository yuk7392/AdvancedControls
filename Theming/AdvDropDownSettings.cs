using System;
using System.ComponentModel;

namespace AdvancedControls.Theming
{
    /// <summary>콤보박스의 입력 방식.</summary>
    public enum AdvComboBoxStyle
    {
        /// <summary>목록에서 고르기만 한다. 직접 입력은 안 된다.</summary>
        DropDownList,
        /// <summary>목록에서 고르거나 직접 입력할 수 있다.</summary>
        DropDown
    }

    /// <summary>
    /// 드롭다운 목록의 동작·크기 설정을 한데 묶는다.
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class AdvDropDownSettings
    {
        private AdvComboBoxStyle _style = AdvComboBoxStyle.DropDownList;
        private int _maxItems = 8;
        private int _itemHeight = -1;

        /// <summary>다시 그리기만 필요할 때.</summary>
        internal event EventHandler Changed;

        /// <summary>입력 방식이 바뀌어 편집창을 만들거나 없애야 할 때.</summary>
        internal event EventHandler StyleChanged;

        [DefaultValue(AdvComboBoxStyle.DropDownList)]
        [Description("목록에서 고르기만 할지, 직접 입력도 허용할지 정합니다.")]
        public AdvComboBoxStyle Style
        {
            get { return _style; }
            set
            {
                if (_style == value) return;
                _style = value;

                var handler = StyleChanged;
                if (handler != null) handler(this, EventArgs.Empty);
                RaiseChanged();
            }
        }

        [DefaultValue(8)]
        [Description("드롭다운에 한 번에 보여줄 최대 항목 수입니다. 넘으면 스크롤됩니다.")]
        public int MaxItems
        {
            get { return _maxItems; }
            set
            {
                if (value < 1) value = 1;
                if (_maxItems == value) return;
                _maxItems = value;
                RaiseChanged();
            }
        }

        [DefaultValue(-1)]
        [Description("목록 한 줄의 높이입니다. -1이면 글꼴에 맞춰 자동으로 정합니다.")]
        public int ItemHeight
        {
            get { return _itemHeight; }
            set
            {
                if (value < -1) value = -1;
                if (_itemHeight == value) return;
                _itemHeight = value;
                RaiseChanged();
            }
        }

        /// <summary>자동(-1)이면 글꼴 높이에 여백을 더해 정한다.</summary>
        public int ResolveItemHeight(int fontHeight)
        {
            return _itemHeight >= 0 ? _itemHeight : fontHeight + 10;
        }

        private void RaiseChanged()
        {
            var handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
