using System.ComponentModel;
using AdvancedControls.Theming;

namespace AdvancedControls
{
    /// <summary>
    /// 이 라이브러리가 추가한 속성을 모아 놓은 루트. 속성 창에서 Font처럼 펼쳐서 쓴다.
    /// 컨트롤마다 더 내놓을 것이 있으면 이 클래스를 상속해 늘린다.
    /// </summary>
    /// <remarks>
    /// 값을 여기에 담지 않고 컨트롤의 실제 속성으로 넘기기만 한다.
    /// 그래서 <c>btn.AdvancedControlOptions.Kind</c>와 <c>btn.Kind</c>가 같은 값이고,
    /// 이미 저장된 디자이너 파일도 그대로 동작한다.
    /// </remarks>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class AdvOptions
    {
        private readonly AdvAppearance _appearance;
        private readonly AdvColorOverrides _palette;

        private AdvBorderGroup _border;
        private AdvEffectsGroup _effects;
        private AdvTransitionGroup _transition;
        private AdvThemeGroup _theme;

        internal AdvOptions(AdvAppearance appearance, AdvColorOverrides palette)
        {
            _appearance = appearance;
            _palette = palette;
        }

        // 아래 네 그룹은 표시·코드 진입용 파사드다(예: options.Border.Width).
        // 직렬화하지 않는다 — 실제 값은 Styling(=AdvAppearance)이 그대로 직렬화한다.

        /// <summary>테두리 관련: 두께·모서리·선 모양.</summary>
        [Description("테두리 관련 설정입니다. 펼쳐서 두께·모서리·선 모양을 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvBorderGroup Border
        {
            get { return _border ?? (_border = new AdvBorderGroup(_appearance)); }
        }

        /// <summary>효과 관련: 그림자·글로우·그라데이션.</summary>
        [Description("효과 관련 설정입니다. 펼쳐서 그림자·글로우·그라데이션을 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvEffectsGroup Effects
        {
            get { return _effects ?? (_effects = new AdvEffectsGroup(_appearance)); }
        }

        /// <summary>전환 관련: 전환 시간·가감속 곡선.</summary>
        [Description("전환 관련 설정입니다. 펼쳐서 전환 시간·가감속을 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvTransitionGroup Transition
        {
            get { return _transition ?? (_transition = new AdvTransitionGroup(_appearance)); }
        }

        /// <summary>테마 관련: 테마 모드·색 재정의.</summary>
        [Description("테마 관련 설정입니다. 펼쳐서 테마 모드·색 재정의를 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvThemeGroup Theme
        {
            get { return _theme ?? (_theme = new AdvThemeGroup(_appearance, _palette)); }
        }

        /// <summary>
        /// 모양 설정 원본. 그리드에는 그룹(Border·Effects·Animation·Theme)으로 나오므로 감추고,
        /// 코드 호환을 위해 남겨 둔다. 직렬화는 컨트롤의 Styling이 담당한다.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvAppearance Styling
        {
            get { return _appearance; }
        }

        /// <summary>이 컨트롤에만 적용하는 색. 그리드에는 Theme 그룹 안에서 나오므로 감춘다.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvColorOverrides Palette
        {
            get { return _palette; }
        }

        /// <summary>펼치기 전 값 칸. 비우지 않으면 타입 이름이 그대로 나온다.</summary>
        public override string ToString()
        {
            return string.Empty;
        }
    }
}
