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
}
