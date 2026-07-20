using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>글자의 역할. 색을 직접 정하지 않고 테마의 의미 단위를 고른다.</summary>
    public enum AdvLabelKind
    {
        /// <summary>본문 글자.</summary>
        Normal,
        /// <summary>부가 설명처럼 흐리게 보일 글자.</summary>
        Muted,
        /// <summary>강조색 글자.</summary>
        Accent
    }

    /// <summary>
    /// 테마를 따르는 라벨. 배경·테두리는 그리지 않고 글자만 그린다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultProperty("Text")]
    [Description("테마를 따르는 라벨입니다.")]
    public class AdvLabel : AdvControlBase
    {
        /// <summary>글자가 없어도 디자이너에서 집을 수 있어야 하므로 폭을 0으로 만들지 않는다.</summary>
        private const int EmptyTextWidth = 8;

        private AdvLabelKind _kind = AdvLabelKind.Normal;
        private ContentAlignment _textAlign = ContentAlignment.MiddleLeft;
        private bool _wrap;
        private AdvLabelOptions _options;

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvLabelOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvLabelOptions(this)); }
        }

        public AdvLabel()
        {
            // 라벨은 포커스를 받지 않는다
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            AutoSize = true;
        }

        protected override Size DefaultSize
        {
            get { return new Size(100, 20); }
        }

        /// <summary>
        /// 라벨만 생성자에서 AutoSize를 켠다. 베이스의 DefaultValue(false)를 그대로 두면
        /// 디자이너가 "기본값과 같다"고 판단해 사용자가 끈 설정을 저장하지 않고,
        /// 다음에 폼을 열 때 생성자가 다시 true로 되돌려 설정이 조용히 사라진다.
        /// </summary>
        [Browsable(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [Category("Layout")]
        [DefaultValue(true)]
        [RefreshProperties(RefreshProperties.All)]
        [Description("내용에 맞춰 크기를 자동으로 맞출지 여부입니다.")]
        public override bool AutoSize
        {
            get { return base.AutoSize; }
            set { base.AutoSize = value; }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvLabelKind.Normal)]
        [Description("글자의 역할입니다. 색은 테마에서 결정됩니다.")]
        public AdvLabelKind Kind
        {
            get { return _kind; }
            set
            {
                if (_kind == value) return;
                _kind = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(ContentAlignment.MiddleLeft)]
        [Description("글자 정렬입니다.")]
        public ContentAlignment TextAlign
        {
            get { return _textAlign; }
            set
            {
                if (_textAlign == value) return;
                _textAlign = value;
                Invalidate();
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(false)]
        [Description("폭을 넘는 글자를 줄바꿈할지 여부입니다. 켜면 AutoSize는 높이만 늘립니다.")]
        public bool Wrap
        {
            get { return _wrap; }
            set
            {
                if (_wrap == value) return;
                _wrap = value;
                AdjustSize();
                Invalidate();
            }
        }

        /// <summary>
        /// 줄바꿈을 켜면 폭은 그대로 두고 높이만 내용에 맞춘다.
        /// 둘 다 늘리면 줄바꿈이 일어나지 않아 설정이 무의미해진다.
        /// </summary>
        public override Size GetPreferredSize(Size proposedSize)
        {
            if (_wrap)
            {
                // 폭을 0으로 돌려주면 AutoSize가 그대로 적용해 줄바꿈 기준이 사라지고,
                // 이후 글자마다 줄이 바뀌어 높이가 폭주한다. 폭은 절대 건드리지 않는다.
                int w = Width > 0 ? Width : DefaultSize.Width;

                if (string.IsNullOrEmpty(Text)) return new Size(w, Font.Height);

                var wrapped = TextRenderer.MeasureText(Text, Font,
                                  new Size(w, int.MaxValue), BuildFlags());
                return new Size(w, wrapped.Height);
            }

            // 폭 0을 돌려주면 AutoSize가 그대로 적용해 컨트롤이 화면에서 사라지고,
            // 디자이너에서 다시 클릭해 선택할 수단이 없어져 Text를 되돌릴 수 없다
            if (string.IsNullOrEmpty(Text)) return new Size(EmptyTextWidth, Font.Height);

            return TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue),
                                            TextFormatFlags.NoPrefix);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!string.IsNullOrEmpty(Text))
            {
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
                                      ResolveColor(EffectiveTheme), BuildFlags());
            }

            base.OnPaint(e);
        }

        private Color ResolveColor(AdvTheme theme)
        {
            if (!Enabled) return theme.TextDisabled;

            switch (_kind)
            {
                case AdvLabelKind.Muted: return theme.TextMuted;
                case AdvLabelKind.Accent: return theme.Accent;
                default: return theme.Text;
            }
        }

        private TextFormatFlags BuildFlags()
        {
            var flags = TextFormatFlags.NoPrefix;

            flags |= _wrap ? TextFormatFlags.WordBreak : TextFormatFlags.EndEllipsis;

            switch (_textAlign)
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
                    // 줄바꿈과 세로 가운데 정렬은 함께 쓰면 첫 줄만 기준이 되어 어긋난다
                    if (!_wrap) flags |= TextFormatFlags.VerticalCenter;
                    break;
            }

            switch (_textAlign)
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

        protected override void OnResize(EventArgs e)
        {
            // 줄바꿈일 때는 폭이 바뀌면 필요한 높이도 바뀐다
            if (_wrap) AdjustSize();
            base.OnResize(e);
        }
    }

    /// <summary>AdvLabel이 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvLabelOptions : AdvOptions
    {
        private readonly AdvLabel _owner;

        internal AdvLabelOptions(AdvLabel owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(AdvLabelKind.Normal)]
        [Description("글자의 역할입니다. 색은 테마에서 결정됩니다.")]
        public AdvLabelKind Kind
        {
            get { return _owner.Kind; }
            set { _owner.Kind = value; }
        }

        [DefaultValue(false)]
        [Description("폭을 넘는 글자를 줄바꿈할지 여부입니다. 켜면 AutoSize는 높이만 늘립니다.")]
        public bool Wrap
        {
            get { return _owner.Wrap; }
            set { _owner.Wrap = value; }
        }
    }
}
