using System;
using System.ComponentModel;

namespace AdvancedControls.Theming
{
    /// <summary>
    /// 모서리별 반경. CSS의 border-radius에 대응한다.
    /// -1은 "테마 값을 따른다"는 뜻이다.
    /// 속성 그리드는 필드를 편집하지 못하므로 네 값은 모두 속성으로 노출한다.
    /// </summary>
    [Serializable]
    [TypeConverter(typeof(AdvCornersConverter))]
    public struct AdvCorners : IEquatable<AdvCorners>
    {
        private int _topLeft;
        private int _topRight;
        private int _bottomRight;
        private int _bottomLeft;

        public AdvCorners(int all)
        {
            _topLeft = _topRight = _bottomRight = _bottomLeft = all;
        }

        public AdvCorners(int topLeft, int topRight, int bottomRight, int bottomLeft)
        {
            _topLeft = topLeft;
            _topRight = topRight;
            _bottomRight = bottomRight;
            _bottomLeft = bottomLeft;
        }

        [Description("좌상단 반경입니다. -1이면 테마 값을 따릅니다.")]
        public int TopLeft
        {
            get { return _topLeft; }
            set { _topLeft = value; }
        }

        [Description("우상단 반경입니다. -1이면 테마 값을 따릅니다.")]
        public int TopRight
        {
            get { return _topRight; }
            set { _topRight = value; }
        }

        [Description("우하단 반경입니다. -1이면 테마 값을 따릅니다.")]
        public int BottomRight
        {
            get { return _bottomRight; }
            set { _bottomRight = value; }
        }

        [Description("좌하단 반경입니다. -1이면 테마 값을 따릅니다.")]
        public int BottomLeft
        {
            get { return _bottomLeft; }
            set { _bottomLeft = value; }
        }

        [Browsable(false)]
        public bool IsZero
        {
            get { return _topLeft == 0 && _topRight == 0 && _bottomRight == 0 && _bottomLeft == 0; }
        }

        /// <summary>네 값이 모두 -1이면 테마를 따른다는 뜻이다.</summary>
        [Browsable(false)]
        public bool FollowsTheme
        {
            get { return _topLeft < 0 && _topRight < 0 && _bottomRight < 0 && _bottomLeft < 0; }
        }

        [Browsable(false)]
        public int Max
        {
            get { return Math.Max(Math.Max(_topLeft, _topRight), Math.Max(_bottomRight, _bottomLeft)); }
        }

        /// <summary>
        /// 테마를 따르는(-1) 모서리를 실제 값으로 채운다.
        /// 일부만 -1인 경우도 있으므로 모서리마다 따로 본다.
        /// </summary>
        public AdvCorners ResolveAgainst(AdvCorners theme)
        {
            return new AdvCorners(
                _topLeft < 0 ? theme.TopLeft : _topLeft,
                _topRight < 0 ? theme.TopRight : _topRight,
                _bottomRight < 0 ? theme.BottomRight : _bottomRight,
                _bottomLeft < 0 ? theme.BottomLeft : _bottomLeft);
        }

        /// <summary>음수 반경은 그리기에서 도형을 깨뜨리므로 여기서 잘라낸다.</summary>
        public AdvCorners Clamp(int min, int max)
        {
            return new AdvCorners(
                Clamp(_topLeft, min, max),
                Clamp(_topRight, min, max),
                Clamp(_bottomRight, min, max),
                Clamp(_bottomLeft, min, max));
        }

        private static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public bool Equals(AdvCorners other)
        {
            return _topLeft == other._topLeft
                && _topRight == other._topRight
                && _bottomRight == other._bottomRight
                && _bottomLeft == other._bottomLeft;
        }

        public override bool Equals(object obj)
        {
            return obj is AdvCorners && Equals((AdvCorners)obj);
        }

        public override int GetHashCode()
        {
            return _topLeft ^ (_topRight << 8) ^ (_bottomRight << 16) ^ (_bottomLeft << 24);
        }

        public override string ToString()
        {
            return _topLeft + ", " + _topRight + ", " + _bottomRight + ", " + _bottomLeft;
        }

        public static bool operator ==(AdvCorners a, AdvCorners b) { return a.Equals(b); }
        public static bool operator !=(AdvCorners a, AdvCorners b) { return !a.Equals(b); }
    }
}
