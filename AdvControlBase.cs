using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Animation;
using AdvancedControls.Theming;

namespace AdvancedControls
{
    /// <summary>
    /// 라이브러리의 모든 커스텀 그리기 컨트롤이 상속하는 베이스.
    /// 더블 버퍼링, 테마 연결, 마우스 상태, 상태 전환 애니메이션을 담당한다.
    /// </summary>
    [ToolboxItem(false)]
    public abstract class AdvControlBase : Control
    {
        private AdvTheme _theme;
        private bool _hovered;
        private bool _pressed;
        private bool _useHandCursor = true;
        private readonly AdvAppearance _appearance = new AdvAppearance();
        private readonly AdvAnimator _hoverAnim;
        private readonly AdvAnimator _focusAnim;

        protected AdvControlBase()
        {
            _appearance.Changed += OnAppearanceChanged;
            _appearance.LayoutChanged += OnAppearanceLayoutChanged;

            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor, true);

            _hoverAnim = new AdvAnimator(0);
            _focusAnim = new AdvAnimator(0);
            _hoverAnim.ValueChanged += OnAnimationTick;
            _focusAnim.ValueChanged += OnAnimationTick;

            AdvThemeManager.ThemeChanged += OnGlobalThemeChanged;
        }

        /// <summary>
        /// 이 컨트롤만 다른 테마를 쓸 때 지정한다. null이면 <see cref="AdvThemeManager.Current"/>를 따른다.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvTheme Theme
        {
            get { return _theme; }
            set
            {
                if (ReferenceEquals(_theme, value)) return;
                _theme = value;
                SyncAnimationDuration();
                OnThemeChanged();
                Invalidate();
            }
        }

        /// <summary>
        /// 색은 전부 테마에서 가져오므로 이 속성들은 그리기에 아무 영향이 없다.
        /// 속성 창에 남겨 두면 사용자가 값을 바꿔 보고 아무 일도 안 일어나 헤매게 되므로 감춘다.
        /// 색을 바꾸려면 Styling.ThemeMode나 AdvThemeManager.Current를 쓴다.
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

        /// <summary>
        /// 클릭으로 동작하는 컨트롤인지. 손 모양 커서를 쓸지 결정한다.
        /// 입력창·라벨·진행 막대는 클릭 대상이 아니므로 기본은 false다.
        /// </summary>
        protected virtual bool IsClickable
        {
            get { return false; }
        }

        /// <summary>
        /// CSS의 cursor: pointer에 대응한다. 데스크톱 관례는 화살표지만
        /// 이 라이브러리는 웹 감각을 목표로 하므로 기본으로 켠다.
        /// </summary>
        /// <remarks>
        /// 여기서는 감춘다. <see cref="IsClickable"/>이 false인 컨트롤에서는 아무 효과가 없어
        /// 속성 창에 보이면 사용자가 바꿔 보고 헤매기 때문이다.
        /// 클릭으로 동작하는 컨트롤이 <c>new</c>로 다시 노출한다.
        /// </remarks>
        [Browsable(false)]
        [DefaultValue(true)]
        [Description("클릭할 수 있는 컨트롤 위에서 손 모양 커서를 보일지 여부입니다.")]
        public bool UseHandCursor
        {
            get { return _useHandCursor; }
            set
            {
                if (_useHandCursor == value) return;
                _useHandCursor = value;
                ApplyHandCursor();
            }
        }

        private void ApplyHandCursor()
        {
            if (!IsClickable) return;
            Cursor = _useHandCursor ? Cursors.Hand : Cursors.Default;
        }

        /// <summary>
        /// 모서리 반경·테두리 두께·전환 시간 등 이 컨트롤의 모양 설정.
        /// 속성 창에서는 AdvancedControlOptions 안에서만 보인다.
        /// </summary>
        [Browsable(false)]
        [Description("이 컨트롤의 모양 설정입니다. 펼쳐서 모서리별 반경 등을 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public AdvAppearance Styling
        {
            get { return _appearance; }
        }

        /// <summary>
        /// Control.AutoSize는 기본적으로 속성 창에 나오지 않는다. 텍스트가 길면 잘리는
        /// 컨트롤들이라 여기서 다시 노출한다.
        /// </summary>
        [Browsable(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [Category("Layout")]          // 명시하지 않으면 Misc로 떨어져 다른 크기 속성과 떨어진다
        [DefaultValue(false)]
        [RefreshProperties(RefreshProperties.All)]
        [Description("내용에 맞춰 크기를 자동으로 맞출지 여부입니다.")]
        public override bool AutoSize
        {
            get { return base.AutoSize; }
            set
            {
                if (base.AutoSize == value) return;
                base.AutoSize = value;
                AdjustSize();
            }
        }

        /// <summary>AutoSize가 켜져 있으면 내용에 맞는 크기로 다시 맞춘다.</summary>
        protected void AdjustSize()
        {
            if (!AutoSize) return;

            var preferred = GetPreferredSize(Size.Empty);
            if (Size != preferred) Size = preferred;
        }

        /// <summary>그리기에 실제로 적용되는 테마.</summary>
        protected AdvTheme EffectiveTheme
        {
            get { return _theme ?? _appearance.ResolveTheme() ?? AdvThemeManager.Current; }
        }

        /// <summary>네 모서리를 모두 같은 값으로 맞추는 지름길. 디자이너에는 Appearance만 노출한다.</summary>
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

        /// <summary>테두리 선 모양(CSS border-style). 그리기 호출에 그대로 넘긴다.</summary>
        protected AdvBorderDash EffectiveBorderDash
        {
            get { return _appearance.BorderDash; }
        }

        private void OnAppearanceChanged(object sender, EventArgs e)
        {
            SyncAnimationDuration();
            Invalidate();
        }

        private void OnAppearanceLayoutChanged(object sender, EventArgs e)
        {
            OnThemeChanged();
            AdjustSize();
            ReapplyMinimumSize();
            PerformLayout();
        }

        /// <summary>
        /// 내용이 잘리지 않는 최소 크기. <see cref="Size.Empty"/>면 제한하지 않는다.
        /// Styling.Elevated를 켜면 <see cref="FramePadding"/>이 3에서 7로 뛰어
        /// 프레임이 가로세로 8px씩 줄어드는데, AutoSize가 꺼져 있으면 크기를 다시
        /// 유도하는 경로가 없어 내용이 눌린다. 그 몫을 재정의해서 알려 준다.
        /// </summary>
        protected virtual Size MinimumContentSize
        {
            get { return Size.Empty; }
        }

        /// <summary>테두리·프레임 여백·Padding이 차지하는 몫. 파생의 최소 크기 계산에 쓴다.</summary>
        protected Size ChromeSize
        {
            get
            {
                int edge = (EffectiveBorderWidth + FramePadding) * 2;
                return new Size(edge + Padding.Horizontal, edge + Padding.Vertical);
            }
        }

        /// <summary>
        /// 테두리와 Padding을 뺀 안쪽 내용 영역.
        /// 콤보·숫자·날짜 입력이 같은 식을 각자 복사해 갖고 있어 여기로 모았다.
        /// </summary>
        protected Rectangle ContentBounds
        {
            get
            {
                var b = FrameBounds;
                int bw = EffectiveBorderWidth;

                return new Rectangle(
                    b.Left + bw + Padding.Left,
                    b.Top + bw + Padding.Top,
                    Math.Max(0, b.Width - bw * 2 - Padding.Horizontal),
                    Math.Max(0, b.Height - bw * 2 - Padding.Vertical));
            }
        }

        /// <summary>
        /// 마우스가 아직 이 컨트롤 위에 있는지. 안쪽에 자식(TextBox)을 올린 컨트롤은
        /// 자식으로 넘어갈 때도 MouseLeave가 오므로 이 검사로 걸러야 호버가 깜빡이지 않는다.
        /// </summary>
        protected bool MouseStillInside
        {
            get { return ClientRectangle.Contains(PointToClient(MousePosition)); }
        }

        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            var min = MinimumContentSize;
            if (min.Width > 0 && width < min.Width) width = min.Width;
            if (min.Height > 0 && height < min.Height) height = min.Height;

            base.SetBoundsCore(x, y, width, height, specified);
        }

        /// <summary>
        /// 여백이 바뀌면 필요한 최소 크기도 달라진다.
        /// SetBounds는 값이 같으면 SetBoundsCore를 부르지 않고 빠져나가므로 직접 부른다.
        /// </summary>
        protected void ReapplyMinimumSize()
        {
            if (!IsHandleCreated) return;
            SetBoundsCore(Left, Top, Width, Height, BoundsSpecified.Size);
        }


        protected bool IsHovered
        {
            get { return _hovered; }
        }

        /// <summary>키보드로도 눌림 상태가 되므로 파생 클래스에서 설정할 수 있다.</summary>
        protected bool IsPressed
        {
            get { return _pressed; }
            set
            {
                if (_pressed == value) return;
                _pressed = value;
                Invalidate();
            }
        }

        /// <summary>호버 전환 진행도(0~1). 색 보간에 쓴다.</summary>
        protected float HoverAmount
        {
            get { return _hoverAnim.Eased; }
        }

        /// <summary>포커스 전환 진행도(0~1).</summary>
        protected float FocusAmount
        {
            get { return _focusAnim.Eased; }
        }

        /// <summary>
        /// 포커스 글로우가 컨트롤 밖으로 잘리지 않도록 각 변에 비워 두는 여백.
        /// 포커스 여부와 무관하게 항상 확보한다 — 포커스 때만 비우면 내용이 흔들린다.
        /// </summary>
        protected int GlowPadding
        {
            get
            {
                if (!_appearance.ShowFocusGlow) return 0;

                // 포커스를 받지 못하는 컨트롤은 글로우를 그릴 일이 없다.
                // 그런데도 여백을 예약하면 순수 손해다 — 진행 막대는 높이 14 중 6px(43%)을 잃었다
                if (!GetStyle(ControlStyles.Selectable)) return 0;

                var glow = EffectiveTheme.FocusGlow;
                return glow != null && glow.IsVisible ? glow.Blur : 0;
            }
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

        /// <summary>
        /// 테두리 바깥에 비워 두는 여백. 글로우와 그림자 둘 다 바깥으로 퍼지므로
        /// 더하지 않고 큰 쪽만 확보하면 된다.
        /// 크기 계산은 반드시 이 값을 써야 한다 — GlowPadding만 쓰면 Elevated일 때
        /// 실제 테두리 영역보다 크게 잡아 내용이 눌린다.
        /// </summary>
        protected int FramePadding
        {
            get { return Math.Max(GlowPadding, ShadowPadding); }
        }

        /// <summary>테두리를 그릴 영역. 글로우·그림자 여백을 뺀 안쪽이다.</summary>
        protected Rectangle FrameBounds
        {
            get
            {
                int p = FramePadding;
                var r = Rectangle.Inflate(ClientRectangle, -p, -p);
                if (r.Width < 1) r.Width = 1;
                if (r.Height < 1) r.Height = 1;
                return r;
            }
        }

        /// <summary>
        /// 포커스 표시를 켤지 여부. 내부에 자식 컨트롤을 호스팅하는 경우
        /// 포커스는 자식이 갖고 테두리는 이 컨트롤이 그리므로 파생에서 재정의한다.
        /// </summary>
        protected virtual bool ShowsFocusVisual
        {
            get { return Focused; }
        }

        /// <summary>자식 컨트롤 위에서도 호버가 유지되도록 파생에서 직접 갱신한다.</summary>
        protected void SetHovered(bool value)
        {
            if (_hovered == value) return;

            _hovered = value;
            if (!value) _pressed = false;
            _hoverAnim.AnimateTo(value ? 1f : 0f);
            Invalidate();
        }

        /// <summary>포커스 링 전환을 파생에서 직접 구동한다.</summary>
        protected void SetFocusVisual(bool value)
        {
            _focusAnim.AnimateTo(value ? 1f : 0f);
            Invalidate();
        }

        /// <summary>현재 상태에서 그려야 할 포커스 글로우. 포커스가 없으면 null.</summary>
        protected AdvShadow CurrentGlow
        {
            get
            {
                if (!Enabled || !ShowsFocusVisual || !ShowFocusCues) return null;
                if (!_appearance.ShowFocusGlow) return null;

                var glow = EffectiveTheme.FocusGlow;
                if (glow == null || !glow.IsVisible) return null;

                // 전환 중에는 알파를 진행도에 맞춰 줄여 서서히 나타나게 한다
                float t = FocusAmount;
                if (t >= 1f) return glow;

                return new AdvShadow(Color.FromArgb((int)(glow.Color.A * t), glow.Color),
                                     glow.Blur, glow.OffsetX, glow.OffsetY);
            }
        }

        /// <summary>
        /// 실제 적용할 전환 시간. 디자이너 안에서는 타이머가 계속 돌면 편집이 불편하므로 0이다.
        /// 자체 애니메이터를 가진 파생 클래스도 이 값을 쓴다.
        /// </summary>
        protected int EffectiveTransitionDuration
        {
            get { return DesignMode ? 0 : _appearance.ResolveTransitionDuration(EffectiveTheme); }
        }

        private void SyncAnimationDuration()
        {
            int d = EffectiveTransitionDuration;
            _hoverAnim.Duration = d;
            _focusAnim.Duration = d;
        }

        private void OnAnimationTick(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            Invalidate();
        }

        /// <summary>
        /// 디자이너가 만든 InitializeComponent는 폼에 붙이기 전에 속성부터 세팅한다.
        /// 그때는 핸들이 없어 최소 크기 보정이 건너뛰어지므로 핸들이 생길 때 다시 적용한다.
        /// </summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            SyncAnimationDuration();
            base.OnHandleCreated(e);
            ReapplyMinimumSize();
            ApplyHandCursor();
        }

        private void OnGlobalThemeChanged(object sender, EventArgs e)
        {
            if (_theme != null) return;
            SyncAnimationDuration();
            OnThemeChanged();
            Invalidate();
        }

        /// <summary>
        /// 적용 테마가 바뀌었을 때 불린다. 직접 그리지 못하는 부분(호스팅한 자식 컨트롤의
        /// 색 등)을 다시 맞춰야 하는 파생 클래스가 재정의한다.
        /// </summary>
        protected virtual void OnThemeChanged()
        {
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            _hoverAnim.AnimateTo(1f);
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            _pressed = false;
            _hoverAnim.AnimateTo(0f);
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _pressed = false;
                Invalidate();
            }
            base.OnMouseUp(e);
        }

        /// <summary>
        /// 눌린·호버 상태에서 비활성화되면 그 상태가 그대로 남아 다시 활성화됐을 때
        /// 잘못된 모습으로 그려지므로 여기서 초기화한다.
        /// </summary>
        protected override void OnEnabledChanged(EventArgs e)
        {
            if (!Enabled)
            {
                _hovered = false;
                _pressed = false;
                _hoverAnim.SetImmediate(0f);
                _focusAnim.SetImmediate(0f);
            }
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            _focusAnim.AnimateTo(1f);
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            _focusAnim.AnimateTo(0f);
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            AdjustSize();
            Invalidate();
            base.OnTextChanged(e);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            AdjustSize();
            ReapplyMinimumSize();      // 글자 높이가 달라지면 필요한 최소 크기도 달라진다
            Invalidate();
            base.OnFontChanged(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 정적 이벤트라 해제하지 않으면 폼이 닫혀도 컨트롤이 수거되지 않는다
                AdvThemeManager.ThemeChanged -= OnGlobalThemeChanged;

                _appearance.Changed -= OnAppearanceChanged;
                _appearance.LayoutChanged -= OnAppearanceLayoutChanged;

                _hoverAnim.ValueChanged -= OnAnimationTick;
                _focusAnim.ValueChanged -= OnAnimationTick;
                _hoverAnim.Dispose();
                _focusAnim.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
