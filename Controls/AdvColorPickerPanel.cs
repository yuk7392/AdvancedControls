using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>
    /// 색을 고르는 피커 패널. 채도/명도 사각형 + 색상 바 + 알파 바 + 프리셋 팔레트 +
    /// 헥스 입력으로 구성된다. 폼에 바로 올려 쓰거나, <see cref="AdvColorPicker"/>가
    /// 드롭다운 팝업에 호스팅한다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("ColorChanged")]
    [DefaultProperty("AdvancedControlOptions")]
    [Description("채도/명도·색상·알파·팔레트·헥스로 색을 고르는 피커 패널입니다.")]
    public class AdvColorPickerPanel : AdvControlBase
    {
        // 96dpi 논리 치수
        private const int HueBarW = 18;
        private const int BarH = 14;
        private const int Gap = 8;
        private const int SwatchGap = 4;
        private const int PreviewW = 26;
        private const int CheckerCell = 5;    // 알파 체커보드 칸 크기

        /// <summary>드래그 중인 영역.</summary>
        private enum DragZone { None, Sv, Hue, Alpha }

        private float _h;            // 0~360
        private float _s = 1f;       // 0~1
        private float _v = 1f;       // 0~1
        private int _a = 255;        // 0~255
        private DragZone _drag;
        private int _hoverSwatch = -1;

        private Color[] _palette;
        private readonly TextBox _hex;
        private bool _hexUpdating;

        private Rectangle _svRect, _hueRect, _alphaRect, _paletteRect, _previewRect, _hexRect;
        private int _swatchSize;

        private AdvColorPickerPanelOptions _options;

        /// <summary>색이 바뀔 때마다(드래그 중 포함) 발생한다.</summary>
        [Category("Behavior")]
        [Description("색이 바뀔 때마다 발생합니다.")]
        public event EventHandler ColorChanged;

        /// <summary>
        /// 색 선택을 확정했을 때(팔레트 클릭·헥스 입력 확정) 발생한다.
        /// 드롭다운 호스트(<see cref="AdvColorPicker"/>)가 이 시점에 팝업을 닫는다.
        /// </summary>
        [Category("Behavior")]
        [Description("팔레트 클릭이나 헥스 입력으로 색을 확정하면 발생합니다.")]
        public event EventHandler ColorCommitted;

        /// <summary>기본 프리셋 팔레트(2×8).</summary>
        private static readonly Color[] DefaultPalette =
        {
            FromRgb(0x000000), FromRgb(0x424242), FromRgb(0x9E9E9E), FromRgb(0xFFFFFF),
            FromRgb(0xE53935), FromRgb(0xFB8C00), FromRgb(0xFDD835), FromRgb(0x43A047),
            FromRgb(0x00ACC1), FromRgb(0x1E88E5), FromRgb(0x3949AB), FromRgb(0x8E24AA),
            FromRgb(0xD81B60), FromRgb(0x6D4C41), FromRgb(0x546E7A), FromRgb(0xB0BEC5)
        };

        private static Color FromRgb(int rgb) { return Color.FromArgb(unchecked((int)0xFF000000) | rgb); }

        public AdvColorPickerPanel()
        {
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
            Styling.ShowFocusGlow = false;
            Styling.Radius = 8;

            _hex = new TextBox
            {
                BorderStyle = BorderStyle.None,
                TabStop = false,
                CharacterCasing = CharacterCasing.Upper,
                TextAlign = HorizontalAlignment.Center
            };
            _hex.KeyDown += HexKeyDown;
            _hex.LostFocus += HexLostFocus;
            Controls.Add(_hex);
            SyncHexText();
        }

        protected override Size DefaultSize
        {
            get { return new Size(220, 288); }
        }

        protected override Padding DefaultPadding
        {
            get { return new Padding(12); }
        }

        protected override Size MinimumContentSize
        {
            get { return new Size(180, 240); }
        }

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvColorPickerPanelOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvColorPickerPanelOptions(this)); }
        }

        /// <summary>현재 색(알파 포함).</summary>
        [Category("Appearance")]
        [Description("현재 선택된 색입니다(알파 포함).")]
        public Color Color
        {
            get { return Color.FromArgb(_a, FromHsv(_h, _s, _v)); }
            set
            {
                if (value.IsEmpty) value = Color.Black;
                if (Color.ToArgb() == value.ToArgb()) return;

                _a = value.A;
                float h, s, v;
                ToHsv(value, out h, out s, out v);
                // 무채색(채도 0)이나 검정(명도 0)은 색상이 정의되지 않는다 — 커서가 튀지 않게 유지한다
                if (s > 0f && v > 0f) _h = h;
                if (v > 0f) _s = s;
                _v = v;

                SyncHexText();
                Invalidate();
                RaiseColorChanged();
            }
        }
        public bool ShouldSerializeColor() { return Color.ToArgb() != System.Drawing.Color.Black.ToArgb(); }
        public void ResetColor() { Color = Color.Black; }

        /// <summary>프리셋 팔레트 색들. null이면 기본 16색(2×8)을 쓴다.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color[] PaletteColors
        {
            get { return _palette ?? (Color[])DefaultPalette.Clone(); }
            set { _palette = value; Invalidate(); }
        }

        // ── HSV·헥스 변환(정적, 회귀 스위트가 직접 검증) ──────────────

        /// <summary>HSV → RGB. h는 0~360, s·v는 0~1.</summary>
        internal static Color FromHsv(float h, float s, float v)
        {
            h = ((h % 360f) + 360f) % 360f;
            float c = v * s;
            float x = c * (1f - Math.Abs(h / 60f % 2f - 1f));
            float m = v - c;

            float r, g, b;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return Color.FromArgb(
                (int)Math.Round((r + m) * 255f),
                (int)Math.Round((g + m) * 255f),
                (int)Math.Round((b + m) * 255f));
        }

        /// <summary>RGB → HSV. 무채색이면 h=0이다.</summary>
        internal static void ToHsv(Color c, out float h, out float s, out float v)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float d = max - min;

            v = max;
            s = max <= 0f ? 0f : d / max;

            if (d <= 0f) { h = 0f; return; }
            if (max == r) h = 60f * (((g - b) / d) % 6f);
            else if (max == g) h = 60f * ((b - r) / d + 2f);
            else h = 60f * ((r - g) / d + 4f);
            if (h < 0f) h += 360f;
        }

        /// <summary>#RRGGBB·#AARRGGBB(# 생략 가능)를 색으로 푼다.</summary>
        internal static bool TryParseHex(string text, out Color color)
        {
            color = Color.Empty;
            if (text == null) return false;
            text = text.Trim();
            if (text.StartsWith("#")) text = text.Substring(1);
            if (text.Length != 6 && text.Length != 8) return false;

            uint value;
            if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                return false;

            if (text.Length == 6) value |= 0xFF000000;
            color = Color.FromArgb(unchecked((int)value));
            return true;
        }

        /// <summary>#RRGGBB, 알파가 있으면 #AARRGGBB.</summary>
        internal static string ToHex(Color c)
        {
            return c.A == 255
                ? string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B)
                : string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B);
        }

        // ── 레이아웃 ──────────────────────────────────────────────────

        private void ComputeLayout()
        {
            var c = ContentBounds;
            int gap = AdvGraphics.Scale(this, Gap);
            int hueW = AdvGraphics.Scale(this, HueBarW);
            int barH = AdvGraphics.Scale(this, BarH);
            int swGap = AdvGraphics.Scale(this, SwatchGap);
            int hexH = _hex.PreferredHeight + 6;

            _swatchSize = Math.Max(10, (c.Width - swGap * 7) / 8);
            int paletteH = _swatchSize * 2 + swGap;

            int svH = Math.Max(60, c.Height - gap * 3 - barH - paletteH - hexH);

            _svRect = new Rectangle(c.Left, c.Top, Math.Max(40, c.Width - hueW - gap), svH);
            _hueRect = new Rectangle(c.Right - hueW, c.Top, hueW, svH);
            _alphaRect = new Rectangle(c.Left, _svRect.Bottom + gap, c.Width, barH);
            _paletteRect = new Rectangle(c.Left, _alphaRect.Bottom + gap, c.Width, paletteH);

            int hexTop = _paletteRect.Bottom + gap;
            _previewRect = new Rectangle(c.Left, hexTop, AdvGraphics.Scale(this, PreviewW), hexH);
            _hexRect = new Rectangle(_previewRect.Right + gap, hexTop,
                                     Math.Max(20, c.Right - _previewRect.Right - gap), hexH);

            // 헥스 입력창을 자리에 앉힌다(세로 중앙)
            int innerH = _hex.PreferredHeight;
            _hex.Bounds = new Rectangle(_hexRect.Left + 4, _hexRect.Top + (_hexRect.Height - innerH) / 2,
                                        _hexRect.Width - 8, innerH);
        }

        private Rectangle SwatchRect(int index)
        {
            int swGap = AdvGraphics.Scale(this, SwatchGap);
            int col = index % 8, row = index / 8;
            return new Rectangle(
                _paletteRect.Left + col * (_swatchSize + swGap),
                _paletteRect.Top + row * (_swatchSize + swGap),
                _swatchSize, _swatchSize);
        }

        // ── 그리기 ────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            var frame = FrameBounds;
            if (frame.Width <= 0 || frame.Height <= 0) return;

            ComputeLayout();

            AdvFrameRenderer.Draw(g, frame, theme, EffectiveCorners, EffectiveBorderWidth,
                                  theme.Surface, Color.Empty, theme.Border,
                                  null, CurrentElevation, EffectiveBorderDash);

            DrawSvSquare(g, theme);
            DrawHueBar(g, theme);
            DrawAlphaBar(g, theme);
            DrawPalette(g, theme);
            DrawPreview(g, theme);

            using (var pen = new Pen(theme.Border))
                g.DrawRectangle(pen, _hexRect.Left, _hexRect.Top, _hexRect.Width - 1, _hexRect.Height - 1);

            base.OnPaint(e);
        }

        private void DrawSvSquare(Graphics g, AdvTheme theme)
        {
            var r = _svRect;
            if (r.Width <= 1 || r.Height <= 1) return;

            // 흰색→순색 가로 + 투명→검정 세로, 두 그라데이션 합성으로 SV 평면을 만든다
            using (var b = new LinearGradientBrush(r, Color.White, FromHsv(_h, 1f, 1f), 0f))
                g.FillRectangle(b, r);
            using (var b = new LinearGradientBrush(r, Color.FromArgb(0, Color.Black), Color.Black, 90f))
                g.FillRectangle(b, r);

            using (var pen = new Pen(theme.Border))
                g.DrawRectangle(pen, r.Left, r.Top, r.Width - 1, r.Height - 1);

            // 선택 커서: 흰 링 + 검정 외곽(밝은 배경·어두운 배경 모두에서 보이게)
            int cx = r.Left + (int)Math.Round(_s * (r.Width - 1));
            int cy = r.Top + (int)Math.Round((1f - _v) * (r.Height - 1));
            int cr = AdvGraphics.Scale(this, 5);
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.Black, 3f)) g.DrawEllipse(pen, cx - cr, cy - cr, cr * 2, cr * 2);
            using (var pen = new Pen(Color.White, 1.5f)) g.DrawEllipse(pen, cx - cr, cy - cr, cr * 2, cr * 2);
            g.SmoothingMode = old;
        }

        private void DrawHueBar(Graphics g, AdvTheme theme)
        {
            var r = _hueRect;
            if (r.Width <= 1 || r.Height <= 1) return;

            using (var b = new LinearGradientBrush(r, Color.Red, Color.Red, 90f))
            {
                var blend = new ColorBlend(7);
                for (int i = 0; i <= 6; i++)
                {
                    blend.Colors[i] = FromHsv(i * 60f, 1f, 1f);
                    blend.Positions[i] = i / 6f;
                }
                b.InterpolationColors = blend;
                g.FillRectangle(b, r);
            }

            using (var pen = new Pen(theme.Border))
                g.DrawRectangle(pen, r.Left, r.Top, r.Width - 1, r.Height - 1);

            DrawBarIndicator(g, r, _h / 360f, true);
        }

        private void DrawAlphaBar(Graphics g, AdvTheme theme)
        {
            var r = _alphaRect;
            if (r.Width <= 1 || r.Height <= 1) return;

            DrawCheckerboard(g, r);

            var solid = FromHsv(_h, _s, _v);
            using (var b = new LinearGradientBrush(r, Color.FromArgb(0, solid), solid, 0f))
                g.FillRectangle(b, r);

            using (var pen = new Pen(theme.Border))
                g.DrawRectangle(pen, r.Left, r.Top, r.Width - 1, r.Height - 1);

            DrawBarIndicator(g, r, _a / 255f, false);
        }

        /// <summary>체커보드 배경 — 알파가 있는 색 아래에 깔려 투명함을 보여준다.</summary>
        private void DrawCheckerboard(Graphics g, Rectangle r)
        {
            int cell = AdvGraphics.Scale(this, CheckerCell);
            using (var light = new SolidBrush(Color.White))
            using (var dark = new SolidBrush(Color.FromArgb(204, 204, 204)))
            {
                g.FillRectangle(light, r);
                for (int y = r.Top, row = 0; y < r.Bottom; y += cell, row++)
                {
                    for (int x = r.Left + (row % 2 == 0 ? cell : 0); x < r.Right; x += cell * 2)
                    {
                        int w = Math.Min(cell, r.Right - x);
                        int h = Math.Min(cell, r.Bottom - y);
                        g.FillRectangle(dark, x, y, w, h);
                    }
                }
            }
        }

        /// <summary>바 위 선택 위치 표시. 흰 선 + 검정 외곽으로 어느 색 위에서도 보인다.</summary>
        private void DrawBarIndicator(Graphics g, Rectangle bar, float t, bool vertical)
        {
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;

            if (vertical)
            {
                int y = bar.Top + (int)Math.Round(t * (bar.Height - 1));
                using (var pen = new Pen(Color.Black, 3f)) g.DrawLine(pen, bar.Left, y, bar.Right - 1, y);
                using (var pen = new Pen(Color.White, 1f)) g.DrawLine(pen, bar.Left, y, bar.Right - 1, y);
            }
            else
            {
                int x = bar.Left + (int)Math.Round(t * (bar.Width - 1));
                using (var pen = new Pen(Color.Black, 3f)) g.DrawLine(pen, x, bar.Top, x, bar.Bottom - 1);
                using (var pen = new Pen(Color.White, 1f)) g.DrawLine(pen, x, bar.Top, x, bar.Bottom - 1);
            }
        }

        private void DrawPalette(Graphics g, AdvTheme theme)
        {
            var colors = PaletteColors;
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int current = Color.ToArgb();
            for (int i = 0; i < colors.Length && i < 16; i++)
            {
                var r = SwatchRect(i);
                using (var path = AdvGraphics.CreateRoundedRect(r, AdvGraphics.Scale(this, 3)))
                {
                    using (var b = new SolidBrush(colors[i])) g.FillPath(b, path);

                    Color line = colors[i].ToArgb() == current || i == _hoverSwatch
                        ? theme.Accent : theme.Border;
                    using (var pen = new Pen(line, i == _hoverSwatch ? 2f : 1f))
                        g.DrawPath(pen, path);
                }
            }
            g.SmoothingMode = old;
        }

        private void DrawPreview(Graphics g, AdvTheme theme)
        {
            var r = _previewRect;
            DrawCheckerboard(g, r);
            using (var b = new SolidBrush(Color)) g.FillRectangle(b, r);
            using (var pen = new Pen(theme.Border))
                g.DrawRectangle(pen, r.Left, r.Top, r.Width - 1, r.Height - 1);
        }

        // ── 마우스 ────────────────────────────────────────────────────

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            ComputeLayout();

            if (_svRect.Contains(e.Location)) { _drag = DragZone.Sv; ApplyDrag(e.Location); }
            else if (_hueRect.Contains(e.Location)) { _drag = DragZone.Hue; ApplyDrag(e.Location); }
            else if (_alphaRect.Contains(e.Location)) { _drag = DragZone.Alpha; ApplyDrag(e.Location); }
            else
            {
                var colors = PaletteColors;
                for (int i = 0; i < colors.Length && i < 16; i++)
                {
                    if (SwatchRect(i).Contains(e.Location))
                    {
                        // 팔레트는 RGB만 고르고 알파는 유지한다
                        Color = Color.FromArgb(_a, colors[i]);
                        RaiseCommitted();
                        return;
                    }
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // 캡처가 풀린 뒤 버튼 없이 스치는 이동은 드래그가 아니다(캡처 상실 안전장치)
            if (_drag != DragZone.None && (e.Button & MouseButtons.Left) == 0) _drag = DragZone.None;

            if (_drag != DragZone.None) { ApplyDrag(e.Location); return; }

            ComputeLayout();
            int hover = -1;
            var colors = PaletteColors;
            for (int i = 0; i < colors.Length && i < 16; i++)
                if (SwatchRect(i).Contains(e.Location)) { hover = i; break; }
            if (hover != _hoverSwatch) { _hoverSwatch = hover; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _drag = DragZone.None; }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            _drag = DragZone.None;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverSwatch != -1) { _hoverSwatch = -1; Invalidate(); }
        }

        private void ApplyDrag(Point p)
        {
            switch (_drag)
            {
                case DragZone.Sv:
                {
                    float s = (p.X - _svRect.Left) / (float)Math.Max(1, _svRect.Width - 1);
                    float v = 1f - (p.Y - _svRect.Top) / (float)Math.Max(1, _svRect.Height - 1);
                    _s = Clamp01(s);
                    _v = Clamp01(v);
                    break;
                }
                case DragZone.Hue:
                    _h = Clamp01((p.Y - _hueRect.Top) / (float)Math.Max(1, _hueRect.Height - 1)) * 360f;
                    break;
                case DragZone.Alpha:
                    _a = (int)Math.Round(Clamp01((p.X - _alphaRect.Left) / (float)Math.Max(1, _alphaRect.Width - 1)) * 255f);
                    break;
                default: return;
            }

            SyncHexText();
            Invalidate();
            RaiseColorChanged();
        }

        private static float Clamp01(float f) { return f < 0f ? 0f : (f > 1f ? 1f : f); }

        // ── 헥스 입력 ─────────────────────────────────────────────────

        private void SyncHexText()
        {
            if (_hex.Focused) return;   // 입력 중엔 덮어쓰지 않는다
            _hexUpdating = true;
            try { _hex.Text = ToHex(Color); }
            finally { _hexUpdating = false; }
        }

        private void HexKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommitHex(true);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                _hexUpdating = true;
                try { _hex.Text = ToHex(Color); }
                finally { _hexUpdating = false; }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void HexLostFocus(object sender, EventArgs e) { CommitHex(false); }

        private void CommitHex(bool raiseCommitted)
        {
            if (_hexUpdating) return;

            Color parsed;
            if (TryParseHex(_hex.Text, out parsed))
            {
                Color = parsed;
                if (raiseCommitted) RaiseCommitted();
            }
            _hexUpdating = true;
            try { _hex.Text = ToHex(Color); }   // 실패했으면 원래 값으로 되돌린다
            finally { _hexUpdating = false; }
        }

        private void RaiseColorChanged()
        {
            var h = ColorChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        private void RaiseCommitted()
        {
            var h = ColorCommitted;
            if (h != null) h(this, EventArgs.Empty);
        }

        // ── 테마 ──────────────────────────────────────────────────────

        protected override void OnThemeChanged()
        {
            base.OnThemeChanged();
            if (_hex == null) return;   // 생성자의 Styling 변경이 헥스 입력창 생성보다 먼저 이 경로를 탄다
            var theme = EffectiveTheme;
            _hex.BackColor = theme.InputBackground;
            _hex.ForeColor = theme.Text;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            OnThemeChanged();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hex.KeyDown -= HexKeyDown;
                _hex.LostFocus -= HexLostFocus;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>AdvColorPickerPanel이 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvColorPickerPanelOptions : AdvOptions
    {
        private readonly AdvColorPickerPanel _owner;

        internal AdvColorPickerPanelOptions(AdvColorPickerPanel owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [Description("현재 선택된 색입니다(알파 포함).")]
        public Color Color
        {
            get { return _owner.Color; }
            set { _owner.Color = value; }
        }
        public bool ShouldSerializeColor() { return _owner.ShouldSerializeColor(); }
        public void ResetColor() { _owner.ResetColor(); }
    }
}
