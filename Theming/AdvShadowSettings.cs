using System;
using System.ComponentModel;
using System.Drawing;

namespace AdvancedControls.Theming
{
    /// <summary>
    /// 컨트롤 하나의 그림자/글로우를 테마 대신 직접 지정하는 설정. 속성 창에서 펼쳐서 쓴다.
    /// <see cref="Custom"/>이 꺼져 있으면(기본) 테마 값을 그대로 쓰고, 켜면 이 객체의
    /// 색·번짐·오프셋으로 그린다. 색/번짐/오프셋을 모두 명시로 받으므로 "테마 따름" 센티널이
    /// 필요 없다 — Custom 하나로 전체를 켜고 끈다.
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class AdvShadowSettings
    {
        private readonly Color _defColor;
        private readonly int _defBlur;
        private readonly int _defOffsetX;
        private readonly int _defOffsetY;

        private bool _custom;
        private Color _color;
        private int _blur;
        private int _offsetX;
        private int _offsetY;

        /// <summary>값이 바뀌어 다시 그리거나 여백을 다시 계산해야 할 때 발생한다.</summary>
        internal event EventHandler Changed;

        internal AdvShadowSettings(Color defColor, int defBlur, int defOffsetX, int defOffsetY)
        {
            _defColor = defColor;
            _defBlur = defBlur;
            _defOffsetX = defOffsetX;
            _defOffsetY = defOffsetY;

            _color = defColor;
            _blur = defBlur;
            _offsetX = defOffsetX;
            _offsetY = defOffsetY;
        }

        [Description("켜면 아래 값으로 직접 그리고, 끄면 테마의 값을 따릅니다.")]
        public bool Custom
        {
            get { return _custom; }
            set { if (_custom == value) return; _custom = value; Raise(); }
        }
        public bool ShouldSerializeCustom() { return _custom; }
        public void ResetCustom() { Custom = false; }

        [Description("그림자/글로우 색입니다. 알파(투명도)로 진하기를 조절합니다. Custom이 켜져 있을 때만 적용됩니다.")]
        public Color Color
        {
            get { return _color; }
            set { if (_color == value) return; _color = value; Raise(); }
        }
        public bool ShouldSerializeColor() { return _color != _defColor; }
        public void ResetColor() { Color = _defColor; }

        [Description("바깥으로 퍼지는 번짐 반경(px)입니다. 0이면 그리지 않습니다. Custom이 켜져 있을 때만 적용됩니다.")]
        public int Blur
        {
            get { return _blur; }
            set { value = Math.Max(0, value); if (_blur == value) return; _blur = value; Raise(); }
        }
        public bool ShouldSerializeBlur() { return _blur != _defBlur; }
        public void ResetBlur() { Blur = _defBlur; }

        [Description("가로 오프셋(px)입니다. 양수는 오른쪽으로 밉니다. Custom이 켜져 있을 때만 적용됩니다.")]
        public int OffsetX
        {
            get { return _offsetX; }
            set { if (_offsetX == value) return; _offsetX = value; Raise(); }
        }
        public bool ShouldSerializeOffsetX() { return _offsetX != _defOffsetX; }
        public void ResetOffsetX() { OffsetX = _defOffsetX; }

        [Description("세로 오프셋(px)입니다. 양수는 아래로 밉니다. Custom이 켜져 있을 때만 적용됩니다.")]
        public int OffsetY
        {
            get { return _offsetY; }
            set { if (_offsetY == value) return; _offsetY = value; Raise(); }
        }
        public bool ShouldSerializeOffsetY() { return _offsetY != _defOffsetY; }
        public void ResetOffsetY() { OffsetY = _defOffsetY; }

        /// <summary>Custom이면 이 설정으로, 아니면 테마 그림자를 그대로 돌려준다.</summary>
        public AdvShadow Resolve(AdvShadow themeShadow)
        {
            return _custom ? new AdvShadow(_color, _blur, _offsetX, _offsetY) : themeShadow;
        }

        /// <summary>펼치기 전 값 칸.</summary>
        public override string ToString()
        {
            return _custom ? "사용자 지정" : "테마";
        }

        private void Raise()
        {
            var handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
