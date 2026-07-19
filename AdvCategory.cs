namespace AdvancedControls
{
    /// <summary>
    /// 속성 창에서 이 라이브러리가 새로 만든 속성을 한 묶음으로 모아 보여줄 카테고리.
    ///
    /// 표준 컨트롤에도 있는 속성(ReadOnly, MaxLength, Minimum, Items 등)은 여기 넣지 않는다.
    /// 사용자가 표준 위치에서 찾기를 기대하기 때문이다. 이 라이브러리가 새로 만든 것만 모은다.
    ///
    /// 이름이 "Adv..."로 시작하는 이유는 속성 창이 카테고리를 알파벳순으로 정렬해
    /// Appearance·Behavior·Data보다 위에 오게 하기 위해서다.
    /// </summary>
    internal static class AdvCategory
    {
        public const string Name = "Advanced Controls";
    }
}
