namespace AdvancedControls.Animation
{
    /// <summary>
    /// 전환 가감속 곡선. CSS transition-timing-function에 대응한다.
    /// 전이(AnimateTo) 모드에서만 적용되고, 무한 반복(StartLoop)에는 쓰지 않는다.
    /// </summary>
    public enum AdvEasing
    {
        /// <summary>smoothstep. 양끝이 부드럽고 가운데가 빠르다. 기본값(기존 동작).</summary>
        Smooth,
        /// <summary>등속. 가감속 없음.</summary>
        Linear,
        /// <summary>천천히 출발해 빨라진다(CSS ease-in).</summary>
        EaseIn,
        /// <summary>빠르게 출발해 느려진다(CSS ease-out). 대부분의 UI 전환에 자연스럽다.</summary>
        EaseOut,
        /// <summary>양끝이 느리고 가운데가 빠르다(CSS ease-in-out).</summary>
        EaseInOut
    }

    /// <summary>선형 진행도(0~1)에 가감속 곡선을 입힌다.</summary>
    internal static class AdvEase
    {
        /// <param name="t">0~1 선형 진행도.</param>
        public static float Apply(AdvEasing easing, float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;

            switch (easing)
            {
                case AdvEasing.Linear:
                    return t;

                case AdvEasing.EaseIn:
                    return t * t * t;

                case AdvEasing.EaseOut:
                {
                    float u = 1f - t;
                    return 1f - u * u * u;
                }

                case AdvEasing.EaseInOut:
                    return t < 0.5f
                        ? 4f * t * t * t
                        : 1f - Pow3(-2f * t + 2f) / 2f;

                default: // Smooth (smoothstep)
                    return t * t * (3f - 2f * t);
            }
        }

        private static float Pow3(float v)
        {
            return v * v * v;
        }
    }
}
