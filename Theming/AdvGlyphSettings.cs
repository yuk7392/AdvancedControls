using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace AdvancedControls.Theming
{
    /// <summary>
    /// 체크박스·라디오의 표시 도형 크기와 배치를 한데 묶는다.
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class AdvGlyphSettings
    {
        private int _size = 16;
        private int _gap = 8;
        private LeftRightAlignment _alignment = LeftRightAlignment.Left;

        /// <summary>크기·배치가 바뀌어 자식 배치까지 다시 잡아야 할 때.</summary>
        internal event EventHandler LayoutChanged;

        [DefaultValue(16)]
        [Description("표시 도형의 한 변 길이입니다.")]
        public int Size
        {
            get { return _size; }
            set
            {
                // 0 이하면 도형이 사라지고 그리기에서 잘못된 사각형이 나온다
                if (value < 4) value = 4;
                if (_size == value) return;
                _size = value;
                RaiseLayout();
            }
        }

        [DefaultValue(8)]
        [Description("도형과 글자 사이 간격입니다.")]
        public int Gap
        {
            get { return _gap; }
            set
            {
                if (value < 0) value = 0;
                if (_gap == value) return;
                _gap = value;
                RaiseLayout();
            }
        }

        [DefaultValue(LeftRightAlignment.Left)]
        [Description("도형을 글자의 왼쪽에 둘지 오른쪽에 둘지 정합니다.")]
        public LeftRightAlignment Alignment
        {
            get { return _alignment; }
            set
            {
                if (_alignment == value) return;
                _alignment = value;
                RaiseLayout();
            }
        }

        private void RaiseLayout()
        {
            var handler = LayoutChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
