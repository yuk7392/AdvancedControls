using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>버튼의 시각적 강조 단계.</summary>
    public enum AdvButtonKind
    {
        /// <summary>강조색으로 채운다. 화면의 주 동작에 쓴다.</summary>
        Filled,
        /// <summary>테두리와 텍스트만 표시한다.</summary>
        Outline,
        /// <summary>평소에는 텍스트만 보이고 호버 시에만 배경이 생긴다.</summary>
        Ghost
    }

    [ToolboxItem(true)]
    [DefaultEvent("Click")]
    [Description("테마를 따르는 커스텀 그리기 버튼입니다.")]
    public class AdvButton : AdvControlBase, IButtonControl
    {
        private const int ImageTextGap = 6;

        private AdvButtonKind _kind = AdvButtonKind.Filled;
        private AdvContextColor _context = AdvContextColor.Default;
        private DialogResult _dialogResult = DialogResult.None;
        private bool _isDefault;
        private Image _image;
        private TextImageRelation _textImageRelation = TextImageRelation.ImageBeforeText;
        private AdvButtonOptions _options;

        public AdvButton()
        {
            TabStop = true;
        }

        protected override Size DefaultSize
        {
            // 글로우 여백(각 변 3px)을 감안한 크기
            get { return new Size(100, 34); }
        }

        protected override Padding DefaultPadding
        {
            get { return new Padding(14, 4, 14, 4); }
        }

        #region IButtonControl — 폼의 AcceptButton / CancelButton 지원

        [Category("Behavior")]
        [DefaultValue(DialogResult.None)]
        [Description("이 버튼을 눌렀을 때 대화 상자가 돌려줄 결과입니다.")]
        public DialogResult DialogResult
        {
            get { return _dialogResult; }
            set { _dialogResult = value; }
        }

        /// <summary>폼이 이 버튼을 기본 버튼으로 지정하거나 해제할 때 호출한다.</summary>
        public void NotifyDefault(bool value)
        {
            if (_isDefault == value) return;
            _isDefault = value;
            Invalidate();
        }

        /// <summary>이 버튼이 폼의 AcceptButton인지 여부. 기본 버튼은 테두리를 한 겹 더 그린다.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsDefault
        {
            get { return _isDefault; }
        }

        /// <summary>코드에서 클릭과 같은 동작을 일으킨다. 표준 Button과 같다.</summary>
        public void PerformClick()
        {
            if (!Enabled || !Visible) return;
            OnClick(EventArgs.Empty);
        }

        /// <summary>
        /// 대화 상자 안의 버튼이면 누를 때 폼의 DialogResult를 설정한다.
        /// 표준 Button과 같은 동작이며, 이게 없으면 ShowDialog가 닫히지 않는다.
        /// </summary>
        protected override void OnClick(EventArgs e)
        {
            if (_dialogResult != DialogResult.None)
            {
                var form = FindForm();
                if (form != null) form.DialogResult = _dialogResult;
            }
            base.OnClick(e);
        }

        #endregion

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvButtonKind.Filled)]
        [Description("버튼의 시각적 강조 단계입니다.")]
        public AdvButtonKind Kind
        {
            get { return _kind; }
            set
            {
                if (_kind == value) return;
                _kind = value;
                Invalidate();
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvContextColor.Default)]
        [Description("버튼의 컨텍스트 색입니다. Default는 테마 강조색(Accent)을 따릅니다.")]
        public AdvContextColor Context
        {
            get { return _context; }
            set
            {
                if (_context == value) return;
                _context = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        [Description("버튼에 표시할 이미지입니다.")]
        public Image Image
        {
            get { return _image; }
            set
            {
                if (ReferenceEquals(_image, value)) return;
                _image = value;
                AdjustSize();
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(TextImageRelation.ImageBeforeText)]
        [Description("이미지와 글자를 어떻게 배치할지 정합니다.")]
        public TextImageRelation TextImageRelation
        {
            get { return _textImageRelation; }
            set
            {
                if (_textImageRelation == value) return;
                _textImageRelation = value;
                AdjustSize();
                Invalidate();
            }
        }

        /// <summary>
        /// 내용(이미지+글자)에 여백·테두리·글로우를 더한 크기.
        /// </summary>
        protected override bool IsClickable
        {
            get { return true; }
        }

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvButtonOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvButtonOptions(this)); }
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var content = MeasureContent();
            var chrome = ChromeSize;

            return new Size(
                content.Width + chrome.Width,
                content.Height + chrome.Height);
        }

        /// <summary>
        /// 글자가 통째로 사라지지 않을 만큼의 높이는 지킨다.
        /// 폭은 줄임표가 처리하므로 제한하지 않는다.
        /// </summary>
        protected override Size MinimumContentSize
        {
            get { return new Size(0, ChromeSize.Height + Font.Height); }
        }

        private Size MeasureContent()
        {
            var text = string.IsNullOrEmpty(Text)
                     ? Size.Empty
                     : TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue),
                                                TextFormatFlags.NoPrefix);

            var img = _image == null ? Size.Empty : _image.Size;

            if (img.IsEmpty) return text;
            if (text.IsEmpty) return img;

            switch (_textImageRelation)
            {
                case TextImageRelation.ImageAboveText:
                case TextImageRelation.TextAboveImage:
                    return new Size(Math.Max(img.Width, text.Width),
                                    img.Height + ImageTextGap + text.Height);

                case TextImageRelation.Overlay:
                    return new Size(Math.Max(img.Width, text.Width),
                                    Math.Max(img.Height, text.Height));

                default: // ImageBeforeText / TextBeforeImage
                    return new Size(img.Width + ImageTextGap + text.Width,
                                    Math.Max(img.Height, text.Height));
            }
        }

        /// <summary>내용 영역 안에서 이미지와 글자가 놓일 자리를 함께 계산한다.</summary>
        private void LayoutContent(Rectangle content, out Rectangle imageRect, out Rectangle textRect)
        {
            imageRect = Rectangle.Empty;
            textRect = content;

            if (_image == null) return;

            var img = _image.Size;

            if (string.IsNullOrEmpty(Text))
            {
                imageRect = Center(content, img);
                textRect = Rectangle.Empty;
                return;
            }

            var text = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue),
                                                TextFormatFlags.NoPrefix);

            switch (_textImageRelation)
            {
                case TextImageRelation.ImageAboveText:
                {
                    int total = img.Height + ImageTextGap + text.Height;
                    int top = content.Top + (content.Height - total) / 2;
                    imageRect = new Rectangle(content.Left + (content.Width - img.Width) / 2, top,
                                              img.Width, img.Height);
                    textRect = new Rectangle(content.Left, top + img.Height + ImageTextGap,
                                             content.Width, text.Height);
                    break;
                }

                case TextImageRelation.TextAboveImage:
                {
                    int total = text.Height + ImageTextGap + img.Height;
                    int top = content.Top + (content.Height - total) / 2;
                    textRect = new Rectangle(content.Left, top, content.Width, text.Height);
                    imageRect = new Rectangle(content.Left + (content.Width - img.Width) / 2,
                                              top + text.Height + ImageTextGap, img.Width, img.Height);
                    break;
                }

                case TextImageRelation.TextBeforeImage:
                {
                    int total = text.Width + ImageTextGap + img.Width;
                    int left = content.Left + (content.Width - total) / 2;
                    textRect = new Rectangle(left, content.Top, text.Width, content.Height);
                    imageRect = Center(new Rectangle(left + text.Width + ImageTextGap, content.Top,
                                                     img.Width, content.Height), img);
                    break;
                }

                case TextImageRelation.Overlay:
                    imageRect = Center(content, img);
                    textRect = content;
                    break;

                default: // ImageBeforeText
                {
                    int total = img.Width + ImageTextGap + text.Width;
                    int left = content.Left + (content.Width - total) / 2;
                    imageRect = Center(new Rectangle(left, content.Top, img.Width, content.Height), img);
                    textRect = new Rectangle(left + img.Width + ImageTextGap, content.Top,
                                             text.Width, content.Height);
                    break;
                }
            }
        }

        private static Rectangle Center(Rectangle area, Size size)
        {
            return new Rectangle(
                area.Left + (area.Width - size.Width) / 2,
                area.Top + (area.Height - size.Height) / 2,
                size.Width, size.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = FrameBounds;
            var corners = EffectiveCorners;

            Color fill, fillEnd, border, foreColor;
            ResolveColors(theme, out fill, out fillEnd, out border, out foreColor);

            int bw = EffectiveBorderWidth;
            AdvFrameRenderer.Draw(g, bounds, theme, corners, bw, fill, fillEnd, border,
                                  CurrentGlow, CurrentElevation, EffectiveBorderDash);

            // 기본 버튼(Enter로 눌리는 버튼)은 테두리를 한 겹 더 그려 구분한다
            if (_isDefault && Enabled)
            {
                var ring = Rectangle.Inflate(bounds, -(bw + 1), -(bw + 1));
                if (ring.Width > 0 && ring.Height > 0)
                {
                    var ctxPal = theme.ResolveContext(_context);
                    using (var path = AdvGraphics.CreateRoundedRect(ring, corners.Clamp(0, int.MaxValue)))
                    using (var pen = new Pen(_kind == AdvButtonKind.Filled ? ctxPal.OnSolid : ctxPal.Solid, 1))
                        g.DrawPath(pen, path);
                }
            }

            var content = new Rectangle(
                bounds.Left + bw + Padding.Left,
                bounds.Top + bw + Padding.Top,
                Math.Max(0, bounds.Width - bw * 2 - Padding.Horizontal),
                Math.Max(0, bounds.Height - bw * 2 - Padding.Vertical));

            Rectangle imageRect, textRect;
            LayoutContent(content, out imageRect, out textRect);

            if (_image != null && imageRect.Width > 0)
            {
                if (Enabled) g.DrawImage(_image, imageRect);
                else ControlPaint.DrawImageDisabled(g, _image, imageRect.X, imageRect.Y, Color.Transparent);
            }

            if (!string.IsNullOrEmpty(Text) && textRect.Width > 0)
            {
                // 이미지가 없으면 내용 영역 전체에 가운데 정렬, 있으면 계산된 자리에 그린다
                var flags = TextFormatFlags.VerticalCenter
                          | TextFormatFlags.EndEllipsis
                          | TextFormatFlags.NoPrefix;

                if (_image == null || _textImageRelation == TextImageRelation.Overlay
                                   || _textImageRelation == TextImageRelation.ImageAboveText
                                   || _textImageRelation == TextImageRelation.TextAboveImage)
                    flags |= TextFormatFlags.HorizontalCenter;

                TextRenderer.DrawText(g, Text, Font, textRect, foreColor, flags);
            }

            // 소비자가 붙인 Paint 핸들러가 위에 덧그릴 수 있도록 마지막에 호출한다
            base.OnPaint(e);
        }

        private void ResolveColors(AdvTheme theme, out Color fill, out Color fillEnd,
                                   out Color border, out Color foreColor)
        {
            fillEnd = Color.Empty;

            if (!Enabled)
            {
                // Ghost는 활성 상태에서도 면·테두리가 없으므로 비활성이라고 생기면 안 된다
                fill = _kind == AdvButtonKind.Ghost ? Color.Transparent : theme.DisabledFill;
                border = _kind == AdvButtonKind.Outline ? theme.Border : fill;
                foreColor = theme.TextDisabled;
                return;
            }

            var p = theme.ResolveContext(_context);
            // Default는 ResolveContext가 Accent 세트를 그대로 돌려주므로 기존 동작과 동일하다.
            bool neutral = _context == AdvContextColor.Default;
            float t = HoverAmount;

            switch (_kind)
            {
                case AdvButtonKind.Filled:
                    fill = IsPressed
                         ? p.SolidPressed
                         : AdvGraphics.Blend(p.Solid, p.SolidHover, t);
                    // 그라데이션 끝색은 Accent(Primary/Default)에만 있다.
                    fillEnd = neutral || _context == AdvContextColor.Primary ? theme.AccentGradientEnd : Color.Empty;
                    border = fill;
                    foreColor = p.OnSolid;
                    break;

                case AdvButtonKind.Outline:
                    if (neutral)
                    {
                        // 기존 중립 아웃라인 동작 유지(회색 테두리 + 본문색).
                        fill = IsPressed
                             ? theme.SurfacePressed
                             : AdvGraphics.Blend(theme.Surface, theme.SurfaceHover, t);
                        fillEnd = theme.SurfaceGradientEnd;
                        border = IsPressed
                               ? theme.BorderHover
                               : AdvGraphics.Blend(theme.Border, theme.BorderHover, t);
                        foreColor = theme.Text;
                    }
                    else
                    {
                        // 컨텍스트 아웃라인 — 평소엔 색 테두리+글자, 호버/누름에 채운다.
                        fill = IsPressed ? p.SolidPressed : AdvGraphics.Blend(theme.Surface, p.Solid, t);
                        fillEnd = Color.Empty;
                        border = p.Solid;
                        foreColor = (IsPressed || t >= 0.5f) ? p.OnSolid : p.Solid;
                    }
                    break;

                default: // Ghost
                    fill = IsPressed
                         ? theme.SurfacePressed
                         : AdvGraphics.Blend(Color.Transparent, theme.SurfaceHover, t);
                    border = Color.Transparent;
                    foreColor = neutral ? theme.Text : p.Solid;
                    break;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !Focused) Focus();
            base.OnMouseDown(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) IsPressed = true;
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && IsPressed)
            {
                IsPressed = false;
                OnClick(EventArgs.Empty);
            }
            base.OnKeyUp(e);
        }

        /// <summary>
        /// 포커스가 다른 곳으로 넘어가면 KeyUp이 오지 않아 눌림 상태가 남는다.
        /// </summary>
        protected override void OnLostFocus(EventArgs e)
        {
            IsPressed = false;
            base.OnLostFocus(e);
        }
    }

    /// <summary>AdvButton이 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvButtonOptions : AdvOptions
    {
        private readonly AdvButton _owner;

        internal AdvButtonOptions(AdvButton owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(AdvButtonKind.Filled)]
        [Description("버튼의 시각적 강조 단계입니다.")]
        public AdvButtonKind Kind
        {
            get { return _owner.Kind; }
            set { _owner.Kind = value; }
        }

        [DefaultValue(AdvContextColor.Default)]
        [Description("버튼의 컨텍스트 색입니다. Default는 테마 강조색(Accent)을 따릅니다.")]
        public AdvContextColor Context
        {
            get { return _owner.Context; }
            set { _owner.Context = value; }
        }

        [DefaultValue(true)]
        [Description("이 버튼 위에서 손 모양 커서를 보일지 여부입니다.")]
        public bool UseHandCursor
        {
            get { return _owner.UseHandCursor; }
            set { _owner.UseHandCursor = value; }
        }
    }
}
