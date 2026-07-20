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

        internal AdvOptions(AdvAppearance appearance, AdvColorOverrides palette)
        {
            _appearance = appearance;
            _palette = palette;
        }

        /// <summary>모서리 반경·테두리 두께·전환 시간 등 모양 설정.</summary>
        [Description("모양 설정입니다. 펼쳐서 모서리별 반경 등을 조정합니다.")]
        public AdvAppearance Styling
        {
            get { return _appearance; }
        }

        /// <summary>이 컨트롤에만 적용하는 색. 비워 둔 색은 테마를 따른다.</summary>
        [Description("이 컨트롤에만 적용하는 색입니다. 펼쳐서 색을 지정하면 그 색만 테마 대신 쓰입니다.")]
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
