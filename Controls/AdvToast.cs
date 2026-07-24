using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Animation;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>토스트 종류. 색은 테마 의미색(Accent·Success·Warning·Error)에서 온다.</summary>
    public enum AdvToastType
    {
        /// <summary>정보(테마 강조색).</summary>
        Info,
        /// <summary>성공.</summary>
        Success,
        /// <summary>경고.</summary>
        Warning,
        /// <summary>오류.</summary>
        Error
    }

    /// <summary>
    /// 화면 우하단에 잠깐 떠올랐다 사라지는 알림(토스트).
    /// <see cref="AdvDialog"/>처럼 정적 Show로 쓰며, 창은 포커스를 뺏지 않는 무활성 팝업이다.
    /// 여러 개가 뜨면 위로 쌓이고, 얼굴은 <see cref="AdvAlert"/>를 재사용해
    /// 상황색·아이콘·닫기 X가 알림 박스와 같은 규칙으로 그려진다.
    /// </summary>
    public static class AdvToast
    {
        private static readonly List<ToastWindow> _open = new List<ToastWindow>();

        private const int Margin = 16;        // 작업영역 가장자리 여백
        private const int Gap = 10;           // 토스트 사이 간격
        private const int ToastWidth = 320;
        private const int MinHeight = 48;
        private const int MaxHeight = 160;    // 긴 메시지는 이 높이에서 잘린다(첫 줄은 항상 보임)
        private const int ToastRadius = 10;
        private const int MaxVisible = 5;     // 초과하면 가장 오래된 것부터 닫는다
        private const int FadeMs = 150;

        /// <summary>기본 자동 닫힘 시간(ms).</summary>
        public const int DefaultDuration = 4000;

        public static void Show(string message) { Show(message, AdvToastType.Info, DefaultDuration, null); }
        public static void Show(string message, AdvToastType type) { Show(message, type, DefaultDuration, null); }
        public static void Show(string message, AdvToastType type, int durationMs) { Show(message, type, durationMs, null); }

        /// <summary>
        /// 토스트를 띄운다. durationMs가 0 이하면 자동으로 닫히지 않고 X로만 닫는다.
        /// owner를 주면 그 폼이 있는 모니터의 작업영역에 뜨고, 없으면 주 모니터에 뜬다.
        /// </summary>
        public static void Show(string message, AdvToastType type, int durationMs, Form owner)
        {
            // 가장 오래된 것(닫히는 중이 아닌)을 밀어내 화면 점유를 제한한다
            if (CountAlive() >= MaxVisible)
            {
                foreach (var t in _open)
                {
                    if (!t.IsDismissing) { t.BeginDismiss(); break; }
                }
            }

            var w = new ToastWindow(message ?? string.Empty, type, durationMs, owner);
            _open.Add(w);
            w.FormClosed += ToastClosed;
            w.Show();
            Reflow();
        }

        /// <summary>지금 떠 있는(닫히는 중 포함) 토스트 수.</summary>
        public static int OpenCount { get { return _open.Count; } }

        /// <summary>모든 토스트를 즉시 닫는다(페이드 없이).</summary>
        public static void ClearAll()
        {
            var copy = _open.ToArray();
            foreach (var t in copy) t.Close();
        }

        private static int CountAlive()
        {
            int n = 0;
            foreach (var t in _open) if (!t.IsDismissing) n++;
            return n;
        }

        private static void ToastClosed(object sender, FormClosedEventArgs e)
        {
            var w = sender as ToastWindow;
            if (w != null) { w.FormClosed -= ToastClosed; _open.Remove(w); }
            Reflow();
        }

        /// <summary>
        /// 작업영역별로 아래에서 위로 다시 쌓는다. 최신이 맨 아래(우하단)이고
        /// 하나가 닫히면 남은 것들이 아래로 내려온다.
        /// </summary>
        private static void Reflow()
        {
            var used = new Dictionary<Rectangle, int>();   // 작업영역 → 아래쪽 누적 높이
            for (int i = _open.Count - 1; i >= 0; i--)
            {
                var t = _open[i];
                int acc;
                used.TryGetValue(t.WorkArea, out acc);
                t.Location = SlotLocation(t.WorkArea, t.Size, acc);
                used[t.WorkArea] = acc + t.Height + Gap;
            }
        }

        /// <summary>작업영역 우하단 기준 슬롯 위치. stackedBelow는 이미 아래에 쌓인 높이 합.</summary>
        internal static Point SlotLocation(Rectangle workArea, Size size, int stackedBelow)
        {
            return new Point(workArea.Right - Margin - size.Width,
                             workArea.Bottom - Margin - stackedBelow - size.Height);
        }

        /// <summary>메시지 길이에 맞는 토스트 높이(48~160). 알림(AdvAlert)의 안쪽 배치와 같은 식으로 잰다.</summary>
        internal static int MeasureHeight(string message, Font font)
        {
            // 알림 기본 여백(14,10) + 테두리 1 + 아이콘(Font.Height+8) + 닫기(Font.Height+6)
            int textWidth = ToastWidth - 2 - 28 - (font.Height + 8) - (font.Height + 6);
            var measured = TextRenderer.MeasureText(message, font,
                new Size(Math.Max(1, textWidth), int.MaxValue),
                TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            int h = measured.Height + 20 + 2;
            return h < MinHeight ? MinHeight : (h > MaxHeight ? MaxHeight : h);
        }

        /// <summary>
        /// 토스트 창 하나. 무활성(포커스 안 뺏음)·작업표시줄 미표시 팝업으로,
        /// 페이드 인으로 나타나 수명이 다하거나 X를 누르면 페이드 아웃 후 닫힌다.
        /// 마우스가 올라가 있는 동안엔 수명 타이머를 멈춘다.
        /// </summary>
        private sealed class ToastWindow : Form
        {
            private const int WS_EX_TOOLWINDOW = 0x00000080;
            private const int WS_EX_NOACTIVATE = 0x08000000;

            private readonly AdvAlert _alert;
            private readonly Timer _life;
            private readonly AdvAnimator _fade;
            private readonly int _durationMs;
            private bool _dismissing;

            internal readonly Rectangle WorkArea;
            internal bool IsDismissing { get { return _dismissing; } }

            /// <summary>클릭해도 앞 창의 포커스를 뺏지 않는다.</summary>
            protected override bool ShowWithoutActivation { get { return true; } }

            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                    return cp;
                }
            }

            internal ToastWindow(string message, AdvToastType type, int durationMs, Form owner)
            {
                _durationMs = durationMs;
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                TopMost = true;
                StartPosition = FormStartPosition.Manual;

                WorkArea = (owner != null ? Screen.FromControl(owner) : Screen.PrimaryScreen).WorkingArea;

                var theme = AdvThemeManager.Current;
                _alert = new AdvAlert
                {
                    Dock = DockStyle.Fill,
                    Dismissible = true,
                    Text = message,
                    Icon = IconOf(type),
                    Context = ContextOf(type, theme),
                    AccessibleName = message,
                    AccessibleRole = AccessibleRole.Alert
                };
                _alert.Styling.Radius = ToastRadius;
                _alert.Dismissed += AlertDismissed;
                Controls.Add(_alert);

                Size = new Size(ToastWidth, MeasureHeight(message, _alert.Font));
                UpdateRegion();

                Opacity = 0;
                _fade = new AdvAnimator(FadeMs);
                _fade.ValueChanged += FadeTick;

                _life = new Timer { Interval = Math.Max(1, durationMs) };
                _life.Tick += LifeExpired;

                // 읽는 중엔 닫히지 않게, 호버 동안 수명을 멈춘다
                _alert.MouseEnter += HoverPause;
                _alert.MouseLeave += HoverResume;
            }

            private static AdvAlertIcon IconOf(AdvToastType t)
            {
                switch (t)
                {
                    case AdvToastType.Success: return AdvAlertIcon.Success;
                    case AdvToastType.Warning: return AdvAlertIcon.Warning;
                    case AdvToastType.Error: return AdvAlertIcon.Error;
                    default: return AdvAlertIcon.Info;
                }
            }

            private static Color ContextOf(AdvToastType t, AdvTheme theme)
            {
                switch (t)
                {
                    case AdvToastType.Success: return theme.Success;
                    case AdvToastType.Warning: return theme.Warning;
                    case AdvToastType.Error: return theme.Error;
                    default: return Color.Empty;   // 알림 기본 = 테마 강조색
                }
            }

            private Rectangle _regionClip = Rectangle.Empty;

            private void UpdateRegion()
            {
                AdvGraphics.UpdateRoundedRegion(this, ClientRectangle, new AdvCorners(ToastRadius),
                                                false, ref _regionClip);
            }

            protected override void OnShown(EventArgs e)
            {
                base.OnShown(e);
                _fade.AnimateTo(1f);
                if (_durationMs > 0) _life.Start();
                // 새 알림이 떴음을 스크린리더에 알린다
                _alert.AccessibilityObject.RaiseAutomationNotification(
                    System.Windows.Forms.Automation.AutomationNotificationKind.Other,
                    System.Windows.Forms.Automation.AutomationNotificationProcessing.MostRecent,
                    _alert.Text);
            }

            private void FadeTick(object sender, EventArgs e)
            {
                if (IsDisposed) return;
                double v = _fade.Eased;
                Opacity = v < 0 ? 0 : (v > 1 ? 1 : v);
                if (_dismissing && _fade.Eased <= 0.001f) Close();
            }

            private void LifeExpired(object sender, EventArgs e) { BeginDismiss(); }
            private void AlertDismissed(object sender, EventArgs e)
            {
                _alert.Visible = true;   // 알림 자체의 숨김은 되돌리고 창 페이드로 닫는다
                BeginDismiss();
            }

            private void HoverPause(object sender, EventArgs e) { _life.Stop(); }
            private void HoverResume(object sender, EventArgs e)
            {
                if (_durationMs > 0 && !_dismissing) _life.Start();
            }

            /// <summary>페이드 아웃을 시작한다. 끝나면 스스로 닫힌다.</summary>
            internal void BeginDismiss()
            {
                if (_dismissing) return;
                _dismissing = true;
                _life.Stop();
                _fade.AnimateTo(0f);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _alert.Dismissed -= AlertDismissed;
                    _alert.MouseEnter -= HoverPause;
                    _alert.MouseLeave -= HoverResume;
                    _fade.ValueChanged -= FadeTick;
                    _fade.Dispose();
                    _life.Tick -= LifeExpired;
                    _life.Dispose();
                    if (Region != null) Region.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
