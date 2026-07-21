using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace AdvancedControls.Theming
{
    /// <summary>머리글을 프레임 상자 안에 둘지(Inside), 상자 위 캡션으로 둘지(Outside).</summary>
    public enum AdvHeaderPlacement
    {
        /// <summary>프레임이 머리글과 내용을 함께 감싼다.</summary>
        Inside,
        /// <summary>머리글이 프레임 상자 위에 캡션처럼 뜨고, 상자는 내용만 감싼다.</summary>
        Outside
    }

    /// <summary>머리글 모양.</summary>
    public enum AdvHeaderStyle
    {
        /// <summary>글자만(과 선택적 구분선).</summary>
        Plain,
        /// <summary>배경을 채운 밴드 위에 글자.</summary>
        Filled
    }

    /// <summary>
    /// 머리글(제목 줄)의 모양과 배치를 한데 묶는다.
    /// 제목 글자 자체는 컨트롤의 Text에 그대로 둔다 — 다들 거기서 찾기 때문이다.
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class AdvHeaderAppearance
    {
        private Font _font;
        private Color _foreColor = Color.Empty;
        private ContentAlignment _alignment = ContentAlignment.MiddleLeft;
        private int _height = -1;
        private bool _showSeparator = true;
        private Padding _padding = new Padding(12, 8, 12, 8);
        private AdvHeaderPlacement _placement = AdvHeaderPlacement.Inside;
        private AdvHeaderStyle _style = AdvHeaderStyle.Plain;
        private Color _fillColor = Color.Empty;

        /// <summary>다시 그리기만 필요할 때.</summary>
        internal event EventHandler Changed;

        /// <summary>내용 영역 높이가 바뀌어 자식 배치까지 다시 잡아야 할 때.</summary>
        internal event EventHandler LayoutChanged;

        [DefaultValue(null)]
        [Description("머리글 글꼴입니다. 비우면 컨트롤 글꼴을 따릅니다.")]
        public Font Font
        {
            get { return _font; }
            set
            {
                if (Equals(_font, value)) return;
                _font = value;
                RaiseLayout();
            }
        }

        [Description("머리글 글자색입니다. 비우면 테마 색을 따릅니다.")]
        public Color ForeColor
        {
            get { return _foreColor; }
            set
            {
                if (_foreColor == value) return;
                _foreColor = value;
                RaiseChanged();
            }
        }

        [DefaultValue(ContentAlignment.MiddleLeft)]
        [Description("머리글 정렬입니다.")]
        public ContentAlignment Alignment
        {
            get { return _alignment; }
            set
            {
                if (_alignment == value) return;
                _alignment = value;
                RaiseChanged();
            }
        }

        [DefaultValue(-1)]
        [Description("머리글 높이입니다. -1이면 글꼴에 맞춰 자동으로 정합니다.")]
        public int Height
        {
            get { return _height; }
            set
            {
                if (value < -1) value = -1;
                if (_height == value) return;
                _height = value;
                RaiseLayout();
            }
        }

        [DefaultValue(true)]
        [Description("머리글과 내용 사이에 구분선을 그릴지 여부입니다.")]
        public bool ShowSeparator
        {
            get { return _showSeparator; }
            set
            {
                if (_showSeparator == value) return;
                _showSeparator = value;
                RaiseChanged();
            }
        }

        [Description("머리글 안쪽 여백입니다.")]
        public Padding Padding
        {
            get { return _padding; }
            set
            {
                if (_padding == value) return;
                _padding = value;
                RaiseLayout();
            }
        }

        [DefaultValue(AdvHeaderPlacement.Inside)]
        [Description("머리글을 프레임 안(Inside)에 둘지, 프레임 위 캡션(Outside)으로 둘지입니다.")]
        public AdvHeaderPlacement Placement
        {
            get { return _placement; }
            set
            {
                if (_placement == value) return;
                _placement = value;
                RaiseLayout();      // 내용 영역과 프레임 위치가 함께 달라진다
            }
        }

        [DefaultValue(AdvHeaderStyle.Plain)]
        [Description("머리글 모양입니다. Plain=글자만, Filled=배경을 채운 밴드.")]
        public AdvHeaderStyle Style
        {
            get { return _style; }
            set
            {
                if (_style == value) return;
                _style = value;
                RaiseChanged();
            }
        }

        [Description("Filled 머리글의 배경색입니다. 비우면 테마 색을 따릅니다.")]
        public Color FillColor
        {
            get { return _fillColor; }
            set
            {
                if (_fillColor == value) return;
                _fillColor = value;
                RaiseChanged();
            }
        }

        public bool ShouldSerializeFillColor() { return _fillColor != Color.Empty; }
        public void ResetFillColor() { FillColor = Color.Empty; }

        public bool ShouldSerializeForeColor() { return _foreColor != Color.Empty; }
        public void ResetForeColor() { ForeColor = Color.Empty; }

        public bool ShouldSerializePadding() { return _padding != new Padding(12, 8, 12, 8); }
        public void ResetPadding() { Padding = new Padding(12, 8, 12, 8); }

        /// <summary>실제 적용할 글꼴. 지정이 없으면 컨트롤 글꼴이다.</summary>
        public Font ResolveFont(Font controlFont)
        {
            return _font ?? controlFont;
        }

        public Color ResolveForeColor(AdvTheme theme, bool enabled)
        {
            if (!enabled) return theme.TextDisabled;
            return _foreColor == Color.Empty ? theme.Text : _foreColor;
        }

        /// <summary>Filled 머리글 배경색. 지정이 없으면 테마의 살짝 눌린 면색을 쓴다.</summary>
        public Color ResolveFillColor(AdvTheme theme)
        {
            return _fillColor == Color.Empty ? theme.SurfaceHover : _fillColor;
        }

        /// <summary>제목이 비어 있으면 머리글 자리를 아예 잡지 않는다.</summary>
        public int ResolveHeight(Font controlFont, bool hasText)
        {
            if (!hasText) return 0;
            if (_height >= 0) return _height;

            return TextRenderer.MeasureText("가Ay", ResolveFont(controlFont)).Height
                 + _padding.Vertical;
        }

        /// <summary>ContentAlignment를 TextRenderer 플래그로 옮긴다.</summary>
        public TextFormatFlags ToTextFlags()
        {
            var flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

            switch (_alignment)
            {
                case ContentAlignment.TopLeft:
                case ContentAlignment.TopCenter:
                case ContentAlignment.TopRight:
                    flags |= TextFormatFlags.Top;
                    break;
                case ContentAlignment.BottomLeft:
                case ContentAlignment.BottomCenter:
                case ContentAlignment.BottomRight:
                    flags |= TextFormatFlags.Bottom;
                    break;
                default:
                    flags |= TextFormatFlags.VerticalCenter;
                    break;
            }

            switch (_alignment)
            {
                case ContentAlignment.TopCenter:
                case ContentAlignment.MiddleCenter:
                case ContentAlignment.BottomCenter:
                    flags |= TextFormatFlags.HorizontalCenter;
                    break;
                case ContentAlignment.TopRight:
                case ContentAlignment.MiddleRight:
                case ContentAlignment.BottomRight:
                    flags |= TextFormatFlags.Right;
                    break;
                default:
                    flags |= TextFormatFlags.Left;
                    break;
            }

            return flags;
        }

        private void RaiseChanged()
        {
            var handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void RaiseLayout()
        {
            var handler = LayoutChanged;
            if (handler != null) handler(this, EventArgs.Empty);
            RaiseChanged();
        }
    }
}
