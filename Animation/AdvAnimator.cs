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

        /// <summary>목표치로 전환을 시작한다.</summary>
        public void AnimateTo(float target)
        {
            if (_disposed) return;

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
