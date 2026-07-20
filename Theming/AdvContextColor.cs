namespace AdvancedControls.Theming
{
    /// <summary>
    /// 컨텍스트 색. Bootstrap의 상황별 색(primary/secondary/success/danger/warning/info)에 대응한다.
    /// 컨트롤은 이 값으로 테마의 <see cref="AdvContextPalette"/>를 골라 색을 결정한다.
    /// </summary>
    public enum AdvContextColor
    {
        /// <summary>컨트롤 기본색(대개 Primary/Accent)을 따른다.</summary>
        Default,
        /// <summary>주 강조색. 테마의 Accent 세트에서 합성한다.</summary>
        Primary,
        Secondary,
        Success,
        Danger,
        Warning,
        Info
    }
}
