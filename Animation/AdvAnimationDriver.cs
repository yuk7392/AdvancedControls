using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AdvancedControls.Animation
{
    /// <summary>
    /// 모든 <see cref="AdvAnimator"/>를 타이머 하나로 함께 굴린다.
    /// 애니메이터마다 WinForms 타이머를 두면 폼 하나에 수십 개가 돌아 무겁다 —
    /// 활성 애니메이터가 있을 때만 타이머 하나가 돌고, 전부 끝나면 멈춘다.
    /// </summary>
    /// <remarks>
    /// 실경과 시간(delta-time)으로 진행시켜, 타이머가 밀려도 전환이 시간에 맞게 끝난다.
    /// 틱은 UI 스레드에서 오므로 <see cref="AdvAnimator.ValueChanged"/>도 항상 UI 스레드다.
    /// </remarks>
    internal static class AdvAnimationDriver
    {
        private const int TickMs = 15;   // 약 66fps

        private static readonly Timer _timer;
        private static readonly List<AdvAnimator> _active = new List<AdvAnimator>();

        // 틱을 도는 도중 콜백이 Add/Remove를 부르면 목록이 흔들린다.
        // 도는 중에는 대기열에 모아 두었다가 틱이 끝난 뒤 반영한다.
        private static readonly List<AdvAnimator> _pendingAdd = new List<AdvAnimator>();
        private static readonly List<AdvAnimator> _pendingRemove = new List<AdvAnimator>();
        private static bool _ticking;
        private static int _lastTick;

        static AdvAnimationDriver()
        {
            _timer = new Timer();
            _timer.Interval = TickMs;
            _timer.Tick += OnTick;
        }

        public static void Add(AdvAnimator a)
        {
            if (_ticking)
            {
                _pendingRemove.Remove(a);
                if (!_active.Contains(a) && !_pendingAdd.Contains(a)) _pendingAdd.Add(a);
            }
            else if (!_active.Contains(a))
            {
                _active.Add(a);
            }

            if (!_timer.Enabled)
            {
                _lastTick = Environment.TickCount;
                _timer.Start();
            }
        }

        public static void Remove(AdvAnimator a)
        {
            if (_ticking)
            {
                _pendingAdd.Remove(a);
                if (!_pendingRemove.Contains(a)) _pendingRemove.Add(a);
            }
            else
            {
                _active.Remove(a);
                if (_active.Count == 0) _timer.Stop();
            }
        }

        private static void OnTick(object sender, EventArgs e)
        {
            int now = Environment.TickCount;
            int dt = now - _lastTick;
            _lastTick = now;

            // 시스템 시계가 되감기거나 처음 틱이면 한 틱 분량으로 본다
            if (dt <= 0 || dt > 1000) dt = TickMs;

            _ticking = true;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var a = _active[i];
                // Advance는 아직 돌아야 하면 true, 끝났으면 false를 준다
                if (!a.Advance(dt)) _active.RemoveAt(i);
            }
            _ticking = false;

            if (_pendingRemove.Count > 0)
            {
                foreach (var a in _pendingRemove) _active.Remove(a);
                _pendingRemove.Clear();
            }
            if (_pendingAdd.Count > 0)
            {
                foreach (var a in _pendingAdd)
                    if (!_active.Contains(a)) _active.Add(a);
                _pendingAdd.Clear();
            }

            if (_active.Count == 0) _timer.Stop();
        }
    }
}
