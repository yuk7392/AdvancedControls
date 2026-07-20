using System.ComponentModel;
using System.Windows.Forms;
using AdvancedControls.Rendering;

namespace AdvancedControls.Controls
{
    /// <summary>
    /// 테마를 따르는 패널.
    /// 테두리를 없애려면 Styling.BorderWidth = 0, 띄우려면 Styling.Elevated = true.
    /// </summary>
    [ToolboxItem(true)]
    [Description("테마를 따르는 패널입니다.")]
    public class AdvPanel : AdvContainerBase
    {
        private AdvPanelOptions _options;

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvPanelOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvPanelOptions(this)); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EffectiveTheme;
            var bounds = FrameBounds;

            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            AdvFrameRenderer.Draw(e.Graphics, bounds, theme, EffectiveCorners, EffectiveBorderWidth,
                                  theme.Surface, theme.SurfaceGradientEnd, theme.Border,
                                  null, CurrentElevation, EffectiveBorderDash);

            base.OnPaint(e);
        }
    }

    /// <summary>AdvPanel이 추가한 속성. 다른 컨트롤과 동일하게 Styling/Palette 접근을 노출한다.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvPanelOptions : AdvOptions
    {
        internal AdvPanelOptions(AdvPanel owner) : base(owner.Styling, owner.Palette)
        {
        }
    }
}
