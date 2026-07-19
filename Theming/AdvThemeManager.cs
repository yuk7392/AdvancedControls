using System;

namespace AdvancedControls.Theming
{
    /// <summary>
    /// 애플리케이션 전역 테마. 컨트롤은 자기 테마를 지정하지 않으면 여기를 따른다.
    /// </summary>
    public static class AdvThemeManager
    {
        /// <summary>
        /// 컨트롤이 ThemeMode로 직접 고를 수 있는 기본 테마.
        /// 매번 새로 만들면 참조 비교가 깨지고 낭비이므로 하나만 둔다.
        /// </summary>
        public static readonly AdvTheme Light = AdvTheme.CreateLight();
        public static readonly AdvTheme Dark = AdvTheme.CreateDark();

        private static AdvTheme _current = Light;

        public static event EventHandler ThemeChanged;

        public static AdvTheme Current
        {
            get { return _current; }
            set
            {
                if (value == null) throw new ArgumentNullException("value");
                if (ReferenceEquals(_current, value)) return;

                _current = value;

                var handler = ThemeChanged;
                if (handler != null) handler(null, EventArgs.Empty);
            }
        }
    }
}
