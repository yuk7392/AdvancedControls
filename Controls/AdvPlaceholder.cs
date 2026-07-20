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

    /// <summary>
    /// 콘텐츠 로딩 중 자리를 채우는 스켈레톤 블록. Bootstrap의 <c>.placeholder</c>에 대응한다.
    /// glow/wave 애니메이션은 <see cref="AdvAnimator"/>의 loop 모드로 돈다.
    /// </summary>
    [ToolboxItem(true)]
    [Description("로딩 자리표시자(스켈레톤) 블록입니다.")]
    public class AdvPlaceholder : AdvControlBase
    {
        private readonly AdvAnimator _anim;
        private AdvPlaceholderAnimation _animation = AdvPlaceholderAnimation.Glow;
        private AdvPlaceholderOptions _options;

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

        [Category("Appearance")]
        [DefaultValue(AdvPlaceholderAnimation.Glow)]
        [Description("자리표시자 애니메이션 종류입니다.")]
        public AdvPlaceholderAnimation Animation
        {
            get { return _animation; }
            set { if (_animation == value) return; _animation = value; UpdateAnim(); Invalidate(); }
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

            using (var path = AdvGraphics.CreateRoundedRect(bounds, EffectiveCorners))
            {
                using (var brush = new SolidBrush(baseGray))
                    g.FillPath(brush, path);

                if (_animation == AdvPlaceholderAnimation.Wave)
                    DrawWave(g, path, bounds, theme, phase);
            }

            base.OnPaint(e);
        }

        private void DrawWave(Graphics g, GraphicsPath clip, Rectangle bounds, AdvTheme theme, float phase)
        {
            int bandW = Math.Max(20, bounds.Width / 3);
            int x = (int)(phase * (bounds.Width + bandW)) - bandW;
            var band = new Rectangle(bounds.Left + x, bounds.Top, bandW, bounds.Height);
            if (band.Width <= 0 || band.Height <= 0) return;

            var state = g.Save();
            g.SetClip(clip);
            using (var lg = new LinearGradientBrush(band, Color.Transparent, Color.Transparent, LinearGradientMode.Horizontal))
            {
                var cb = new ColorBlend(3);
                cb.Colors = new[] { Color.Transparent, Color.FromArgb(130, theme.Surface), Color.Transparent };
                cb.Positions = new[] { 0f, 0.5f, 1f };
                lg.InterpolationColors = cb;
                g.FillRectangle(lg, band);
            }
            g.Restore(state);
        }

        protected override void OnThemeChanged()
        {
            Invalidate();
            base.OnThemeChanged();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _anim.ValueChanged -= OnAnimTick;
                _anim.Dispose();
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

        [DefaultValue(AdvPlaceholderAnimation.Glow)]
        [Description("자리표시자 애니메이션 종류입니다.")]
        public AdvPlaceholderAnimation Animation
        {
            get { return _owner.Animation; }
            set { _owner.Animation = value; }
        }
    }
}
