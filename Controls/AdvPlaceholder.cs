using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Animation;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>자리표시자 애니메이션 종류.</summary>
    public enum AdvPlaceholderAnimation
    {
        /// <summary>정적 회색 블록.</summary>
        None,
        /// <summary>밝기가 오르내리는 glow.</summary>
        Glow,
        /// <summary>좌에서 우로 흐르는 밝은 띠(wave/shimmer).</summary>
        Wave
    }

    /// <summary>스켈레톤 모양 템플릿. 실제 콘텐츠의 윤곽을 흉내 낸다.</summary>
    public enum AdvPlaceholderTemplate
    {
        /// <summary>단일 블록.</summary>
        Block,
        /// <summary>여러 줄의 글자 막대(마지막 줄은 짧게).</summary>
        Text,
        /// <summary>원형 아바타 + 옆 두 줄(미디어 오브젝트).</summary>
        Avatar,
        /// <summary>이미지 블록 + 아래 제목·본문 줄(카드).</summary>
        Card
    }

    /// <summary>
    /// 콘텐츠 로딩 중 자리를 채우는 스켈레톤 블록.
    /// glow/wave 애니메이션은 <see cref="AdvAnimator"/>의 loop 모드로 돈다.
    /// </summary>
    [ToolboxItem(true)]
    [Description("로딩 자리표시자(스켈레톤) 블록입니다.")]
    public class AdvPlaceholder : AdvControlBase
    {
        private readonly AdvAnimator _anim;
        private AdvPlaceholderAnimation _animation = AdvPlaceholderAnimation.Glow;
        private AdvPlaceholderTemplate _template = AdvPlaceholderTemplate.Block;
        private AdvPlaceholderOptions _options;
        private ColorBlend _waveBlend;   // 색은 테마에서만 오므로 프레임마다 재생성하지 않고 캐싱한다
        private SolidBrush _fillBrush;   // glow/wave는 66fps로 도므로 채움 브러시도 캐싱(색만 갱신)

        public AdvPlaceholder()
        {
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            _anim = new AdvAnimator(0);
            _anim.ValueChanged += OnAnimTick;
        }

        protected override Size DefaultSize
        {
            get { return new Size(160, 16); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvPlaceholderAnimation.Glow)]
        [Description("자리표시자 애니메이션 종류입니다.")]
        public AdvPlaceholderAnimation Animation
        {
            get { return _animation; }
            set { if (_animation == value) return; _animation = value; UpdateAnim(); Invalidate(); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(AdvPlaceholderTemplate.Block)]
        [Description("스켈레톤 모양입니다. Text·Avatar·Card는 실제 콘텐츠 윤곽을 흉내 냅니다.")]
        public AdvPlaceholderTemplate Template
        {
            get { return _template; }
            set { if (_template == value) return; _template = value; Invalidate(); }
        }

        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvPlaceholderOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvPlaceholderOptions(this)); }
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            if (!IsDisposed && IsHandleCreated) Invalidate();
        }

        private void UpdateAnim()
        {
            bool shouldRun = !DesignMode && IsHandleCreated && Visible
                           && _animation != AdvPlaceholderAnimation.None;
            if (shouldRun && !_anim.IsLooping) _anim.StartLoop(1200);
            else if (!shouldRun && _anim.IsLooping) _anim.StopLoop();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateAnim();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdateAnim();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = FrameBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            float phase = _anim.Value;
            Color baseGray = theme.DisabledFill;

            if (_animation == AdvPlaceholderAnimation.Glow)
            {
                float tri = 1f - Math.Abs(2f * phase - 1f);   // 0→1→0 삼각파
                Color light = AdvContextPalette.Lerp(baseGray, theme.Surface, 0.6f);
                baseGray = AdvContextPalette.Lerp(baseGray, light, tri);
            }

            using (var path = BuildShapePath(bounds))
            {
                if (_fillBrush == null) _fillBrush = new SolidBrush(baseGray);
                else _fillBrush.Color = baseGray;
                g.FillPath(_fillBrush, path);

                if (_animation == AdvPlaceholderAnimation.Wave)
                    DrawWave(g, path, bounds, theme, phase);   // 시머가 모든 도형에 걸쳐 흐른다
            }

            base.OnPaint(e);
        }

        /// <summary>선택한 템플릿의 모든 도형을 하나의 경로로 모은다. 채움·시머 클립에 함께 쓴다.</summary>
        private GraphicsPath BuildShapePath(Rectangle b)
        {
            var path = new GraphicsPath();
            switch (_template)
            {
                case AdvPlaceholderTemplate.Text: AddTextLines(path, b, 3); break;
                case AdvPlaceholderTemplate.Avatar: AddAvatar(path, b); break;
                case AdvPlaceholderTemplate.Card: AddCard(path, b); break;
                default: AddBlock(path, b); break;   // Block: 전체(기존과 동일)
            }
            return path;
        }

        /// <summary>한 줄 막대의 높이. 글꼴에 맞춘다.</summary>
        private int BarHeight
        {
            get { return Math.Max(8, Font.Height); }
        }

        private void AddBlock(GraphicsPath path, Rectangle r)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (var rp = AdvGraphics.CreateRoundedRect(r, EffectiveCorners))
                path.AddPath(rp, false);
        }

        private static void AddBar(GraphicsPath path, Rectangle r, int radius)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (var rp = AdvGraphics.CreateRoundedRect(r, radius))
                path.AddPath(rp, false);
        }

        private void AddTextLines(GraphicsPath path, Rectangle b, int lines)
        {
            int bar = BarHeight;
            int gap = Math.Max(4, (int)(bar * 0.6f));

            // 높이에 실제로 들어가는 줄 수로 보정한 뒤 세로 중앙에 모은다
            int fit = Math.Max(1, (b.Height + gap) / (bar + gap));
            int n = Math.Min(lines, fit);
            int totalH = n * bar + (n - 1) * gap;
            int y = b.Top + Math.Max(0, (b.Height - totalH) / 2);

            float[] widths = { 1f, 0.95f, 0.6f };
            for (int i = 0; i < n; i++)
            {
                int w = (int)(b.Width * widths[Math.Min(i, widths.Length - 1)]);
                AddBar(path, new Rectangle(b.Left, y, w, bar), bar / 2);
                y += bar + gap;
            }
        }

        private void AddAvatar(GraphicsPath path, Rectangle b)
        {
            int d = Math.Max(8, Math.Min(b.Height, (int)(b.Width * 0.35f)));
            path.AddEllipse(new Rectangle(b.Left, b.Top + (b.Height - d) / 2, d, d));

            int bar = BarHeight;
            int tx = b.Left + d + Math.Max(8, bar / 2);
            int tw = b.Right - tx;
            if (tw <= 0) return;

            int gap = Math.Max(4, (int)(bar * 0.7f));
            int totalH = bar * 2 + gap;
            int y = b.Top + Math.Max(0, (b.Height - totalH) / 2);
            AddBar(path, new Rectangle(tx, y, (int)(tw * 0.7f), bar), bar / 2);
            AddBar(path, new Rectangle(tx, y + bar + gap, (int)(tw * 0.45f), bar), bar / 2);
        }

        private void AddCard(GraphicsPath path, Rectangle b)
        {
            int bar = BarHeight;
            int gap = Math.Max(4, (int)(bar * 0.6f));
            int imageH = Math.Max(bar, (int)(b.Height * 0.55f));
            AddBlock(path, new Rectangle(b.Left, b.Top, b.Width, imageH));

            int y = b.Top + imageH + gap;
            if (y + bar <= b.Bottom)
                AddBar(path, new Rectangle(b.Left, y, (int)(b.Width * 0.8f), bar), bar / 2);
            y += bar + gap;
            if (y + bar <= b.Bottom)
                AddBar(path, new Rectangle(b.Left, y, (int)(b.Width * 0.55f), bar), bar / 2);
        }

        private void DrawWave(Graphics g, GraphicsPath clip, Rectangle bounds, AdvTheme theme, float phase)
        {
            int bandW = Math.Max(20, bounds.Width / 3);
            int x = (int)(phase * (bounds.Width + bandW)) - bandW;
            var band = new Rectangle(bounds.Left + x, bounds.Top, bandW, bounds.Height);
            if (band.Width <= 0 || band.Height <= 0) return;

            if (_waveBlend == null)
            {
                _waveBlend = new ColorBlend(3);
                _waveBlend.Colors = new[] { Color.Transparent, Color.FromArgb(130, theme.Surface), Color.Transparent };
                _waveBlend.Positions = new[] { 0f, 0.5f, 1f };
            }

            var state = g.Save();
            g.SetClip(clip);
            // LinearGradientBrush는 band 위치가 프레임마다 바뀌어 재사용이 불가하지만, 색 배열(ColorBlend)은 캐시본을 쓴다.
            using (var lg = new LinearGradientBrush(band, Color.Transparent, Color.Transparent, LinearGradientMode.Horizontal))
            {
                lg.InterpolationColors = _waveBlend;
                g.FillRectangle(lg, band);
            }
            g.Restore(state);
        }

        protected override void OnThemeChanged()
        {
            _waveBlend = null;   // 색이 theme.Surface에서 오므로 테마가 바뀌면 다시 만든다
            Invalidate();
            base.OnThemeChanged();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _anim.ValueChanged -= OnAnimTick;
                _anim.Dispose();
                if (_fillBrush != null) { _fillBrush.Dispose(); _fillBrush = null; }
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>AdvPlaceholder가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvPlaceholderOptions : AdvOptions
    {
        private readonly AdvPlaceholder _owner;

        internal AdvPlaceholderOptions(AdvPlaceholder owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(AdvPlaceholderTemplate.Block)]
        [Description("스켈레톤 모양입니다. Text·Avatar·Card는 실제 콘텐츠 윤곽을 흉내 냅니다.")]
        public AdvPlaceholderTemplate Template
        {
            get { return _owner.Template; }
            set { _owner.Template = value; }
        }

        [DefaultValue(AdvPlaceholderAnimation.Glow)]
        [Description("자리표시자 애니메이션 종류입니다.")]
        public AdvPlaceholderAnimation Animation
        {
            get { return _owner.Animation; }
            set { _owner.Animation = value; }
        }
    }
}
