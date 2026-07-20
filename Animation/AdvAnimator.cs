using System;
using System.Windows.Forms;

namespace AdvancedControls.Animation
{
    /// <summary>
    /// 0~1 사이 값을 목표치까지 부드럽게 이동시킨다. CSS의 transition에 대응한다.
    /// UI 스레드 타이머를 쓰므로 <see cref="ValueChanged"/>는 항상 UI 스레드에서 발생한다.
    /// </summary>
    public sealed class AdvAnimator : IDisposable
    {
        private const int TickMs = 15;   // 약 66fps

        private readonly Timer _timer;
        private float _value;
        private float _target;
        private int _duration;
        private bool _disposed;
        private bool _looping;

        public event EventHandler ValueChanged;

        public AdvAnimator(int durationMs)
        {
            _duration = durationMs;
            _timer = new Timer();
            _timer.Interval = TickMs;
            _timer.Tick += OnTick;
        }

        /// <summary>전환에 걸리는 시간(ms). 0 이하면 애니메이션 없이 즉시 반영된다.</summary>
        public int Duration
        {
            get { return _duration; }
            set { _duration = value; }
        }

        /// <summary>선형 진행도(0~1).</summary>
        public float Value
        {
            get { return _value; }
        }

        /// <summary>가감속이 적용된 값. 그리기에는 이쪽을 쓴다.</summary>
        public float Eased
        {
            get { return _value * _value * (3f - 2f * _value); }   // smoothstep
        }

        public bool IsAnimating
        {
            get { return !_disposed && _timer.Enabled; }
        }

        /// <summary>무한 반복(loop) 모드로 도는 중인지.</summary>
        public bool IsLooping
        {
            get { return _looping; }
        }

        /// <summary>
        /// 무한 반복을 시작한다. <see cref="Value"/>가 0→1을 선형으로 계속 돌며 1에서 0으로 wrap한다.
        /// 회전 스피너·시머 등 끝나지 않는 애니메이션에 쓴다. 위상은 <see cref="Value"/>로 읽고
        /// 각도·오프셋에 매핑한다(Eased는 반복에 맞지 않으므로 쓰지 않는다).
        /// </summary>
        /// <param name="periodMs">한 바퀴(0→1)에 걸리는 시간(ms).</param>
        public void StartLoop(int periodMs)
        {
            if (_disposed) return;
            _duration = periodMs;
            _looping = true;
            _value = 0f;
            _timer.Start();
        }

        /// <summary>반복을 멈춘다.</summary>
        public void StopLoop()
        {
            _looping = false;
            _timer.Stop();
        }

        /// <summary>목표치로 전환을 시작한다.</summary>
        public void AnimateTo(float target)
        {
            if (_disposed) return;
            _looping = false;      // 전이 모드로 전환

            target = Clamp01(target);
            if (_target == target && !_timer.Enabled) return;

            _target = target;

            if (_duration <= 0)
            {
                SetImmediate(target);
                return;
            }

            if (_value == _target)
            {
                _timer.Stop();
                return;
            }

            _timer.Start();
        }

        /// <summary>애니메이션 없이 값을 즉시 맞춘다. 비활성화·디자인타임에 쓴다.</summary>
        public void SetImmediate(float value)
        {
            if (_disposed) return;
            _looping = false;

            value = Clamp01(value);
            _timer.Stop();
            _target = value;

            if (_value == value) return;

            _value = value;
            Raise();
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_disposed) return;

            if (_looping)
            {
                float lstep = _duration <= 0 ? 1f : (float)TickMs / _duration;
                _value += lstep;
                while (_value >= 1f) _value -= 1f;   // 1에서 0으로 wrap — 멈추지 않는다
                Raise();
                return;
            }

            float step = _duration <= 0 ? 1f : (float)TickMs / _duration;

            if (_value < _target)
            {
                _value += step;
                if (_value >= _target) { _value = _target; _timer.Stop(); }
            }
            else
            {
                _value -= step;
                if (_value <= _target) { _value = _target; _timer.Stop(); }
            }

            Raise();
        }

        private void Raise()
        {
            var handler = ValueChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _timer.Stop();
            _timer.Tick -= OnTick;
            _timer.Dispose();
            ValueChanged = null;
        }
    }
}
