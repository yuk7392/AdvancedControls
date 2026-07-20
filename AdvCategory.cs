namespace AdvancedControls
{
    /// <summary>
    /// 속성 창에서 이 라이브러리가 추가한 속성을 한 묶음으로 모아 보여줄 카테고리.
    ///
    /// 각 컨트롤은 자신이 추가한 속성을 개별 [Category]로 흩지 않고, 이 카테고리에 놓인
    /// 확장 객체 AdvancedControlOptions 안에 모두 담는다(그 안에서 다시 Styling·Palette 등으로
    /// 펼친다). 값·상태 속성(Value, Collapsed, Items 등)도 이 안에 들어간다 — 컨트롤 쪽 실제
    /// 속성은 [Browsable(false)]로 감추고 래퍼가 위임해 노출한다.
    /// 표준 컨트롤에도 있는 속성(Text, AutoSize, Padding, Font 등)은 사용자가 표준 위치에서
    /// 찾기를 기대하므로 옮기지 않고 최상위에 그대로 둔다.
    ///
    /// 이름이 "Advanced Controls"라 속성 창의 알파벳 정렬에서 Appearance·Behavior·Data보다 위에 온다.
    /// </summary>
    internal static class AdvCategory
    {
        public const string Name = "Advanced Controls";
    }
}
