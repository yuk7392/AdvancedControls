using System.Drawing;

namespace AdvancedControls.Theming
{
    /// <summary>
    /// CSS의 box-shadow에 대응한다. GDI+에는 블러가 없어 반투명 레이어를 겹쳐 근사한다.
    /// 바깥 그림자(outset)만 지원한다 — CSS의 inset은 아직 구현하지 않았다.
    /// </summary>
    public class AdvShadow
    {
        public Color Color { get; set; }
        public int Blur { get; set; }
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }

        public AdvShadow() { }

        public AdvShadow(Color color, int blur, int offsetX, int offsetY)
        {
            Color = color;
            Blur = blur;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }

        public bool IsVisible
        {
            get { return Blur > 0 && Color.A > 0; }
        }
    }
}
