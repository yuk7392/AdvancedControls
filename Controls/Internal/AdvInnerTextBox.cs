using System.Windows.Forms;

namespace AdvancedControls.Controls.Internal
{
    /// <summary>
    /// 커스텀 컨트롤 안에 올리는 표준 편집창. 포커스가 이 내부창에 오므로 스크린리더는 이 컨트롤을
    /// 읽는데, 자체 이름이 없으면 무명으로 읽힌다. 그래서 바깥 호스트의 AccessibleName을 동적으로
    /// 가져와 이름으로 쓴다(핸들 생성 타이밍에 의존하지 않게 AccessibleObject에서 처리).
    /// </summary>
    internal sealed class AdvInnerTextBox : TextBox
    {
        /// <summary>이름을 가져올 바깥 컨트롤.</summary>
        public Control Host;

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new InnerAccessibleObject(this);
        }

        private sealed class InnerAccessibleObject : ControlAccessibleObject
        {
            private readonly AdvInnerTextBox _tb;
            public InnerAccessibleObject(AdvInnerTextBox tb) : base(tb) { _tb = tb; }

            public override string Name
            {
                get
                {
                    var host = _tb.Host;
                    return host != null && !string.IsNullOrEmpty(host.AccessibleName) ? host.AccessibleName : base.Name;
                }
                set { base.Name = value; }
            }
        }
    }
}
