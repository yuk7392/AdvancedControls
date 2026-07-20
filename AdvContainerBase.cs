using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Theming;

namespace AdvancedControls
{
    /// <summary>
    /// 컨테이너 계열의 공통 베이스.
    /// <see cref="AdvControlBase"/>가 아니라 <see cref="Panel"/>을 상속하는 이유는
    /// 디자이너에서 자식 컨트롤을 끌어다 놓으려면 Panel에 딸린 ParentControlDesigner가
    /// 필요하기 때문이다. 직접 Control을 상속하면 디자이너에서 아무것도 담을 수 없다.
    /// </summary>
    [ToolboxItem(false)]
    public abstract class AdvContainerBase : Panel
    {
        private AdvTheme _theme;
        private readonly AdvAppearance _appearance = new AdvAppearance();
        private AdvColorOverrides _colors;
        private AdvTheme _mergedTheme;
        private AdvTheme _mergedBase;
        private bool _colorsDirty;

        protected AdvContainerBase()
        {
            _appearance.Changed += OnAppearanceChanged;
            _appearance.LayoutChanged += OnAppearanceLayoutChanged;

            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw, true);

            base.BorderStyle = BorderStyle.None;

            AdvThemeManager.ThemeChanged += OnGlobalThemeChanged;
        }

        /// <summary>
        /// Panel.BorderStyle은 OnPaint가 아니라 네이티브 창 스타일(WS_BORDER/WS_EX_CLIENTEDGE)로
        /// 그려진다. 켜 두면 직접 그린 둥근 테두리 바깥에 OS가 각진 테두리를 덧그려
        /// 이중 테두리가 보이고, 클라이언트 영역도 그만큼 줄어든다.
        /// 테두리는 Styling.BorderWidth로 조정한다.
        /// </summary>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new BorderStyle BorderStyle
        {
            get { return BorderStyle.None; }
            set { base.BorderStyle = BorderStyle.None; }
        }

        private const int WS_BORDER = 0x00800000;
        private const int WS_EX_CLIENTEDGE = 0x00000200;

        /// <summary>
        /// 위의 속성 가리기는 <c>((Panel)x).BorderStyle = ...</c> 같은 우회를 막지 못한다.
        /// 네이티브 테두리는 창 스타일로 붙으므로 여기서 아예 걷어낸다.
        /// BorderStyle.None이면 두 플래그 모두 원래 꺼져 있어 아무 영향이 없다.
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style &= ~WS_BORDER;
                cp.ExStyle &= ~WS_EX_CLIENTEDGE;
                return cp;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvTheme Theme
        {
            get { return _theme; }
            set
            {
                if (ReferenceEquals(_theme, value)) return;
                _theme = value;
                OnThemeChanged();
                Invalidate();
            }
        }

        /// <summary>
        /// 컨테이너의 BackColor는 자식이 물려받아야 해서 내부적으로는 계속 쓰지만,
        /// OnThemeChanged가 테마 색으로 매번 덮어쓰므로 사용자가 지정한 값은 남지 않는다.
        /// 헛수고하지 않도록 속성 창에서 감춘다.
        /// </summary>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Color BackColor
        {
            get { return base.BackColor; }
            set { base.BackColor = value; }
        }

        /// <inheritdoc cref="BackColor"/>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Color ForeColor
        {
            get { return base.ForeColor; }
            set { base.ForeColor = value; }
        }

        /// <inheritdoc cref="BackColor"/>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Image BackgroundImage
        {
            get { return base.BackgroundImage; }
            set { base.BackgroundImage = value; }
        }

        /// <inheritdoc cref="BackColor"/>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override ImageLayout BackgroundImageLayout
        {
            get { return base.BackgroundImageLayout; }
            set { base.BackgroundImageLayout = value; }
        }

        /// <summary>모서리 반경·테두리 두께 등 이 컨테이너의 모양 설정.</summary>
        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [Description("이 컨테이너의 모양 설정입니다. 펼쳐서 모서리별 반경 등을 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public AdvAppearance Styling
        {
            get { return _appearance; }
        }

        /// <summary>
        /// 이 컨테이너에만 적용하는 색 재정의. 비워 둔 색은 테마를 따른다.
        /// 속성 창에서는 AdvancedControlOptions 안에서만 보인다.
        /// </summary>
        [Browsable(false)]
        [Description("이 컨테이너에만 적용하는 색입니다. 비워 두면 테마 색을 따릅니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public AdvColorOverrides Palette
        {
            get
            {
                if (_colors == null)
                {
                    _colors = new AdvColorOverrides();
                    _colors.Changed += OnColorsChanged;
                }
                return _colors;
            }
        }

        private void OnColorsChanged(object sender, EventArgs e)
        {
            _colorsDirty = true;
            // 컨테이너 배경(BackColor)은 테마 면색에서 오므로 색을 바꾸면 함께 다시 맞춘다
            OnThemeChanged();
            Invalidate();
        }

        protected AdvTheme EffectiveTheme
        {
            get
            {
                var baseTheme = _theme ?? _appearance.ResolveTheme() ?? AdvThemeManager.Current;
                if (_colors == null || !_colors.HasAny) return baseTheme;

                if (_colorsDirty || !ReferenceEquals(_mergedBase, baseTheme))
                {
                    _mergedTheme = _colors.Apply(baseTheme);
                    _mergedBase = baseTheme;
                    _colorsDirty = false;
                }
                return _mergedTheme;
            }
        }

        /// <summary>테두리 선 모양(CSS border-style). 그리기 호출에 그대로 넘긴다.</summary>
        protected AdvBorderDash EffectiveBorderDash
        {
            get { return _appearance.BorderDash; }
        }

        /// <summary>네 모서리를 모두 같은 값으로 맞추는 지름길.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int CornerRadius
        {
            get
            {
                var c = _appearance.Corners;
                return c.TopLeft == c.TopRight && c.TopRight == c.BottomRight
                    && c.BottomRight == c.BottomLeft ? c.TopLeft : -1;
            }
            set { _appearance.Corners = new AdvCorners(value < -1 ? -1 : value); }
        }

        protected AdvCorners EffectiveCorners
        {
            get { return _appearance.ResolveCorners(EffectiveTheme); }
        }

        protected int EffectiveBorderWidth
        {
            get { return _appearance.ResolveBorderWidth(EffectiveTheme); }
        }

        /// <summary>Elevated일 때 그림자가 잘리지 않도록 비워 두는 여백.</summary>
        protected int ShadowPadding
        {
            get { return _appearance.ResolveShadowPadding(EffectiveTheme); }
        }

        /// <summary>현재 상태에서 그려야 할 그림자. Elevated가 아니면 null.</summary>
        protected AdvShadow CurrentElevation
        {
            get { return _appearance.Elevated ? EffectiveTheme.Elevation : null; }
        }

        /// <summary>테두리를 그릴 영역. 그림자 여백을 뺀 안쪽이다.</summary>
        protected Rectangle FrameBounds
        {
            get
            {
                int p = ShadowPadding;
                var r = Rectangle.Inflate(ClientRectangle, -p, -p);
                if (r.Width < 1) r.Width = 1;
                if (r.Height < 1) r.Height = 1;
                return r;
            }
        }

        private void OnAppearanceChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        private void OnAppearanceLayoutChanged(object sender, EventArgs e)
        {
            OnThemeChanged();
            PerformLayout();
        }

        /// <summary>
        /// 배경을 직접 그리더라도 BackColor는 테마에 맞춰야 한다.
        /// 자식 컨트롤이 부모의 BackColor를 물려받기 때문에, 이걸 두면
        /// 투명 배경으로 그리는 자식 뒤에 시스템 회색이 그대로 비친다.
        /// </summary>
        protected virtual void OnThemeChanged()
        {
            BackColor = EffectiveTheme.Surface;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            BackColor = EffectiveTheme.Surface;
            base.OnHandleCreated(e);
        }

        private void OnGlobalThemeChanged(object sender, EventArgs e)
        {
            if (_theme != null) return;
            OnThemeChanged();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 정적 이벤트라 해제하지 않으면 폼이 닫혀도 컨트롤이 수거되지 않는다
                AdvThemeManager.ThemeChanged -= OnGlobalThemeChanged;

                _appearance.Changed -= OnAppearanceChanged;
                _appearance.LayoutChanged -= OnAppearanceLayoutChanged;

                if (_colors != null) _colors.Changed -= OnColorsChanged;
            }
            base.Dispose(disposing);
        }
    }
}
