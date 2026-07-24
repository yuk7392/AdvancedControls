using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>아바타 모양.</summary>
    public enum AdvAvatarShape
    {
        /// <summary>원형.</summary>
        Circle,
        /// <summary>둥근 사각형.</summary>
        Rounded
    }

    /// <summary>아바타 우하단 상태 점.</summary>
    public enum AdvAvatarStatus
    {
        None,
        /// <summary>온라인(초록).</summary>
        Online,
        /// <summary>자리 비움(주황).</summary>
        Away,
        /// <summary>다른 용무(빨강).</summary>
        Busy,
        /// <summary>오프라인(회색).</summary>
        Offline
    }

    /// <summary>
    /// 사용자 아바타. 이미지가 있으면 모양대로 잘라 그리고, 없으면 이름(Text) 이니셜을,
    /// 그것도 없으면 사람 실루엣을 그린다. 이니셜 배경색은 지정하지 않으면 이름에서
    /// 해시로 골라 같은 이름이 항상 같은 색을 갖는다. 우하단에 상태 점을 달 수 있다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultProperty("AdvancedControlOptions")]
    [Description("이미지·이니셜·상태 점을 보여주는 아바타입니다.")]
    public class AdvAvatar : AdvControlBase
    {
        private Image _image;
        private AdvAvatarShape _shape = AdvAvatarShape.Circle;
        private AdvAvatarStatus _status = AdvAvatarStatus.None;
        private Color _fill = Color.Empty;
        private AdvAvatarOptions _options;

        /// <summary>이니셜 배경 자동 색(이름 해시로 선택). 어느 테마에서도 흰 글자가 읽히는 톤.</summary>
        private static readonly Color[] AutoFills =
        {
            Color.FromArgb(0xE5, 0x39, 0x35), Color.FromArgb(0xFB, 0x8C, 0x00),
            Color.FromArgb(0x43, 0xA0, 0x47), Color.FromArgb(0x00, 0xAC, 0xC1),
            Color.FromArgb(0x1E, 0x88, 0xE5), Color.FromArgb(0x39, 0x49, 0xAB),
            Color.FromArgb(0x8E, 0x24, 0xAA), Color.FromArgb(0x6D, 0x4C, 0x41)
        };

        public AdvAvatar()
        {
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Styling.ShowFocusGlow = false;
        }

        protected override Size DefaultSize
        {
            get { return new Size(40, 40); }
        }

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvAvatarOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvAvatarOptions(this)); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(null)]
        [Description("아바타 이미지입니다. 비우면 이름 이니셜(그것도 없으면 실루엣)을 그립니다.")]
        public Image Image
        {
            get { return _image; }
            set { if (ReferenceEquals(_image, value)) return; _image = value; Invalidate(); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvAvatarShape.Circle)]
        [Description("원형인지 둥근 사각형인지입니다.")]
        public AdvAvatarShape Shape
        {
            get { return _shape; }
            set { if (_shape == value) return; _shape = value; Invalidate(); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvAvatarStatus.None)]
        [Description("우하단 상태 점입니다.")]
        public AdvAvatarStatus Status
        {
            get { return _status; }
            set { if (_status == value) return; _status = value; Invalidate(); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [Description("이니셜 배경색입니다. 비워 두면 이름에서 자동으로 고릅니다.")]
        public Color FillColor
        {
            get { return _fill; }
            set { if (_fill == value) return; _fill = value; Invalidate(); }
        }
        public bool ShouldSerializeFillColor() { return !_fill.IsEmpty; }
        public void ResetFillColor() { FillColor = Color.Empty; }

        /// <summary>이름에서 뽑은 이니셜. 공백으로 나뉘면 각 단어 첫 글자(최대 2), 아니면 첫 글자.</summary>
        internal static string InitialsOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            name = name.Trim();
            if (name.Length == 0) return string.Empty;

            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return char.ToUpper(parts[0][0]).ToString() + char.ToUpper(parts[1][0]);
            return char.ToUpper(name[0]).ToString();
        }

        /// <summary>이름 해시로 자동 배경색을 고른다. 같은 이름은 항상 같은 색.</summary>
        internal static Color AutoFillOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return AutoFills[0];
            int hash = 0;
            foreach (char c in name) hash = (hash * 31 + c) & 0x7FFFFFFF;
            return AutoFills[hash % AutoFills.Length];
        }

        private GraphicsPath ShapePath(Rectangle r)
        {
            if (_shape == AdvAvatarShape.Circle)
            {
                var p = new GraphicsPath();
                p.AddEllipse(r);
                return p;
            }
            return AdvGraphics.CreateRoundedRect(r, Math.Max(2, r.Height / 5));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var f = FrameBounds;
            int d = Math.Min(f.Width, f.Height);
            if (d <= 2) return;
            var box = new Rectangle(f.Left + (f.Width - d) / 2, f.Top + (f.Height - d) / 2, d, d);

            using (var path = ShapePath(box))
            {
                if (_image != null)
                {
                    // 모양대로 잘라 꽉 채운다(비율 유지, 넘치는 쪽은 잘림)
                    var state = g.Save();
                    g.SetClip(path);
                    var src = _image.Size;
                    float scale = Math.Max((float)box.Width / src.Width, (float)box.Height / src.Height);
                    int w = (int)Math.Ceiling(src.Width * scale);
                    int h = (int)Math.Ceiling(src.Height * scale);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(_image, new Rectangle(box.Left + (box.Width - w) / 2,
                                                      box.Top + (box.Height - h) / 2, w, h));
                    g.Restore(state);

                    if (!Enabled)
                        using (var dim = new SolidBrush(Color.FromArgb(128, theme.Surface)))
                            g.FillPath(dim, path);
                }
                else
                {
                    string initials = InitialsOf(Text);
                    Color fill = !Enabled ? theme.DisabledFill
                        : (!_fill.IsEmpty ? _fill
                        : (initials.Length > 0 ? AutoFillOf(Text) : theme.DisabledFill));

                    using (var b = new SolidBrush(fill)) g.FillPath(b, path);

                    if (initials.Length > 0)
                    {
                        Color fore = Enabled ? AdvContextPalette.ReadableOn(fill) : theme.TextDisabled;
                        TextRenderer.DrawText(g, initials, Font, box, fore,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                          | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                    }
                    else
                    {
                        DrawSilhouette(g, box, Enabled ? theme.TextMuted : theme.TextDisabled);
                    }
                }

                using (var pen = new Pen(theme.Border)) g.DrawPath(pen, path);
            }

            DrawStatusDot(g, theme, box);
            base.OnPaint(e);
        }

        /// <summary>이미지도 이름도 없을 때의 사람 실루엣(머리 원 + 어깨 호).</summary>
        private static void DrawSilhouette(Graphics g, Rectangle box, Color color)
        {
            using (var b = new SolidBrush(color))
            {
                int headD = box.Height * 3 / 8;
                g.FillEllipse(b, box.Left + (box.Width - headD) / 2,
                              box.Top + box.Height / 5, headD, headD);

                int bodyW = box.Width * 3 / 4;
                int bodyH = box.Height / 2;
                g.FillEllipse(b, box.Left + (box.Width - bodyW) / 2,
                              box.Bottom - bodyH * 2 / 3, bodyW, bodyH);
            }
        }

        private void DrawStatusDot(Graphics g, AdvTheme theme, Rectangle box)
        {
            if (_status == AdvAvatarStatus.None) return;

            Color c;
            switch (_status)
            {
                case AdvAvatarStatus.Online: c = theme.Success; break;
                case AdvAvatarStatus.Away: c = theme.Warning; break;
                case AdvAvatarStatus.Busy: c = theme.Error; break;
                default: c = theme.TextMuted; break;
            }

            int d = Math.Max(6, box.Width / 4);
            // 원형은 우하단 45도 접점 근처, 사각형은 모서리에 붙인다
            var dot = new Rectangle(box.Right - d, box.Bottom - d, d, d);

            // 배경 링으로 아바타와 분리해 어느 그림 위에서도 또렷하다
            using (var ring = new SolidBrush(theme.Surface))
                g.FillEllipse(ring, dot.Left - 2, dot.Top - 2, d + 4, d + 4);
            using (var b = new SolidBrush(Enabled ? c : theme.TextDisabled))
                g.FillEllipse(b, dot);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }
    }

    /// <summary>AdvAvatar가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvAvatarOptions : AdvOptions
    {
        private readonly AdvAvatar _owner;

        internal AdvAvatarOptions(AdvAvatar owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(null)]
        [Description("아바타 이미지입니다. 비우면 이름 이니셜(그것도 없으면 실루엣)을 그립니다.")]
        public Image Image
        {
            get { return _owner.Image; }
            set { _owner.Image = value; }
        }

        [DefaultValue(AdvAvatarShape.Circle)]
        [Description("원형인지 둥근 사각형인지입니다.")]
        public AdvAvatarShape Shape
        {
            get { return _owner.Shape; }
            set { _owner.Shape = value; }
        }

        [DefaultValue(AdvAvatarStatus.None)]
        [Description("우하단 상태 점입니다.")]
        public AdvAvatarStatus Status
        {
            get { return _owner.Status; }
            set { _owner.Status = value; }
        }

        [Description("이니셜 배경색입니다. 비워 두면 이름에서 자동으로 고릅니다.")]
        public Color FillColor
        {
            get { return _owner.FillColor; }
            set { _owner.FillColor = value; }
        }
        public bool ShouldSerializeFillColor() { return _owner.ShouldSerializeFillColor(); }
        public void ResetFillColor() { _owner.ResetFillColor(); }
    }
}
