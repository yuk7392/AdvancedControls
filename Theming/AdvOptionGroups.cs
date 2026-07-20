using System.ComponentModel;
using AdvancedControls.Animation;

namespace AdvancedControls.Theming
{
    // AdvancedControlOptions 아래에서 관련 속성을 묶어 펼쳐 보이는 그룹들.
    // 값은 담지 않고 전부 AdvAppearance(=컨트롤의 Styling)의 실제 속성으로 포워딩한다.
    // 그래서 코드에서는 advCtrl.AdvancedControlOptions.Border.Width 처럼 진입하고,
    // 직렬화·기존 경로(advCtrl.Styling.BorderWidth)는 그대로다 — 그룹은 직렬화하지 않는다.

    /// <summary>테두리 관련: 두께·모서리·선 모양.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvBorderGroup
    {
        private readonly AdvAppearance _a;
        internal AdvBorderGroup(AdvAppearance appearance) { _a = appearance; }

        [DefaultValue(-1)]
        [Description("테두리 두께입니다. -1이면 테마 값을 따릅니다. 0이면 테두리를 그리지 않습니다.")]
        public int Width { get { return _a.BorderWidth; } set { _a.BorderWidth = value; } }

        [DefaultValue(-1)]
        [RefreshProperties(RefreshProperties.All)]
        [Description("네 모서리를 한꺼번에 맞추는 반경입니다. -1이면 테마 값을 따릅니다. 모서리마다 다르면 -1로 보입니다.")]
        public int Radius { get { return _a.Radius; } set { _a.Radius = value; } }

        [Description("모서리별 반경입니다. -1이면 테마 값을 따릅니다. 0이면 각진 모서리가 됩니다.")]
        public AdvCorners Corners { get { return _a.Corners; } set { _a.Corners = value; } }
        public bool ShouldSerializeCorners() { return _a.ShouldSerializeCorners(); }
        public void ResetCorners() { _a.ResetCorners(); }

        [DefaultValue(AdvBorderDash.Solid)]
        [Description("테두리 선 모양입니다.")]
        public AdvBorderDash Dash { get { return _a.BorderDash; } set { _a.BorderDash = value; } }

        public override string ToString() { return string.Empty; }
    }

    /// <summary>효과 관련: 그림자·글로우·그라데이션.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvEffectsGroup
    {
        private readonly AdvAppearance _a;
        internal AdvEffectsGroup(AdvAppearance appearance) { _a = appearance; }

        [DefaultValue(false)]
        [Description("떠 있는 카드처럼 그림자를 그릴지 여부입니다. 그림자만큼 각 변에 여백을 확보합니다.")]
        public bool Elevated { get { return _a.Elevated; } set { _a.Elevated = value; } }

        [DefaultValue(true)]
        [Description("포커스를 받았을 때 바깥으로 퍼지는 빛을 그릴지 여부입니다.")]
        public bool FocusGlow { get { return _a.ShowFocusGlow; } set { _a.ShowFocusGlow = value; } }

        [DefaultValue(-1f)]
        [Description("채움 그라데이션의 각도(도)입니다. -1이면 테마 값을 따릅니다.")]
        public float GradientAngle { get { return _a.GradientAngle; } set { _a.GradientAngle = value; } }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Description("그림자 색·번짐·오프셋을 직접 지정합니다. 펼쳐서 Custom을 켜면 Elevated일 때 적용됩니다.")]
        public AdvShadowSettings Shadow { get { return _a.Shadow; } }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Description("포커스 글로우 색·번짐·오프셋을 직접 지정합니다. 펼쳐서 Custom을 켜면 포커스 시 적용됩니다.")]
        public AdvShadowSettings Glow { get { return _a.Glow; } }

        public override string ToString() { return string.Empty; }
    }

    /// <summary>전환 관련: 전환 시간·가감속 곡선(CSS transition).</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvTransitionGroup
    {
        private readonly AdvAppearance _a;
        internal AdvTransitionGroup(AdvAppearance appearance) { _a = appearance; }

        [DefaultValue(-1)]
        [Description("호버·포커스 전환에 걸리는 시간(ms)입니다. -1이면 테마 값, 0이면 애니메이션 없음입니다.")]
        public int Duration { get { return _a.TransitionDuration; } set { _a.TransitionDuration = value; } }

        [DefaultValue(AdvEasing.Smooth)]
        [Description("호버·포커스 전환의 가감속 곡선입니다. CSS transition-timing-function에 대응합니다.")]
        public AdvEasing Easing { get { return _a.Easing; } set { _a.Easing = value; } }

        public override string ToString() { return string.Empty; }
    }

    /// <summary>테마 관련: 테마 모드·색 재정의.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvThemeGroup
    {
        private readonly AdvAppearance _a;
        private readonly AdvColorOverrides _palette;
        internal AdvThemeGroup(AdvAppearance appearance, AdvColorOverrides palette)
        {
            _a = appearance;
            _palette = palette;
        }

        [DefaultValue(AdvThemeMode.Inherit)]
        [Description("이 컨트롤이 따를 테마입니다. Inherit이면 전역 테마를 따릅니다.")]
        public AdvThemeMode Mode { get { return _a.ThemeMode; } set { _a.ThemeMode = value; } }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Description("이 컨트롤에만 적용하는 색입니다. 비워 두면 테마 색을 따릅니다.")]
        public AdvColorOverrides Palette { get { return _palette; } }

        public override string ToString() { return string.Empty; }
    }
}
