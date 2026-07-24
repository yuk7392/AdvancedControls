using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>다이얼로그 아이콘 종류.</summary>
    public enum AdvDialogIcon { None, Info, Success, Warning, Error, Question }

    /// <summary>다이얼로그 버튼 구성.</summary>
    public enum AdvDialogButtons { OK, OKCancel, YesNo, YesNoCancel }

    /// <summary>
    /// 테마를 따르는 모달 다이얼로그(MessageBox 대체). 제목·본문·아이콘·버튼 세트를 갖고,
    /// <see cref="Show(string, string, AdvDialogButtons, AdvDialogIcon, IWin32Window)"/>가 결과를 돌려준다.
    /// </summary>
    [ToolboxItem(false)]
    public class AdvDialog : Form
    {
        private const int TitleBarH = 44;
        private const int Pad = 20;
        private const int IconSize = 32;
        private const int Gap = 16;
        private const int BtnH = 34;

        private readonly string _caption;
        private readonly string _message;
        private readonly AdvDialogIcon _icon;
        private AdvTheme _theme;

        private Rectangle _iconRect, _messageRect, _closeRect;
        private bool _closeHot;
        private bool _dragging;
        private Point _dragStart;

        private AdvButton _primary;                              // Enter로 누를 기본 버튼
        private DialogResult _cancelResult = DialogResult.Cancel; // Esc·닫기(X)가 돌려줄 결과

        private AdvDialog(string message, string caption, AdvDialogButtons buttons, AdvDialogIcon icon)
        {
            _message = message ?? string.Empty;
            _caption = caption ?? string.Empty;
            _icon = icon;
            _theme = AdvThemeManager.Current;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            KeyPreview = true;
            // 픽셀 단위로 배치하고 Region을 핸들 생성 크기로 한 번 만들므로, 오토스케일이
            // ClientSize를 사후 조정해 Region과 어긋나지 않도록 스케일링을 끈다.
            AutoScaleMode = AutoScaleMode.None;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = _theme.Surface;

            BuildLayout(buttons);
        }

        /// <summary>테마 모달을 띄우고 결과를 돌려준다.</summary>
        public static DialogResult Show(string message, string caption = "",
            AdvDialogButtons buttons = AdvDialogButtons.OK, AdvDialogIcon icon = AdvDialogIcon.None,
            IWin32Window owner = null)
        {
            using (var d = new AdvDialog(message, caption, buttons, icon))
            {
                if (owner == null) d.StartPosition = FormStartPosition.CenterScreen;
                return owner != null ? d.ShowDialog(owner) : d.ShowDialog();
            }
        }

        /// <summary>확인/취소 편의 메서드. 확인이면 true.</summary>
        public static bool Confirm(string message, string caption = "", IWin32Window owner = null)
        {
            return Show(message, caption, AdvDialogButtons.OKCancel, AdvDialogIcon.Question, owner) == DialogResult.OK;
        }

        private void BuildLayout(AdvDialogButtons buttons)
        {
            int clientW = 400;
            bool hasIcon = _icon != AdvDialogIcon.None;
            int bodyLeft = Pad + (hasIcon ? IconSize + Gap : 0);
            int msgW = clientW - bodyLeft - Pad;

            int msgH;
            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
                msgH = TextRenderer.MeasureText(g, _message, Font, new Size(msgW, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;

            int bodyTop = TitleBarH + 18;
            int bodyH = Math.Max(hasIcon ? IconSize : 0, msgH);

            _iconRect = new Rectangle(Pad, bodyTop, IconSize, IconSize);
            _messageRect = new Rectangle(bodyLeft, bodyTop, msgW, bodyH);

            int btnTop = bodyTop + bodyH + 22;
            int clientH = btnTop + BtnH + Pad;
            ClientSize = new Size(clientW, clientH);

            _closeRect = new Rectangle(clientW - 16 - 12, 16, 12, 12);

            CreateButtons(buttons, clientW, btnTop);
        }

        private void CreateButtons(AdvDialogButtons buttons, int clientW, int btnTop)
        {
            var specs = new List<Tuple<string, DialogResult, bool>>();   // 텍스트, 결과, primary
            switch (buttons)
            {
                case AdvDialogButtons.OKCancel:
                    specs.Add(Tuple.Create("확인", DialogResult.OK, true));
                    specs.Add(Tuple.Create("취소", DialogResult.Cancel, false));
                    break;
                case AdvDialogButtons.YesNo:
                    specs.Add(Tuple.Create("예", DialogResult.Yes, true));
                    specs.Add(Tuple.Create("아니오", DialogResult.No, false));
                    _cancelResult = DialogResult.No;   // 취소 버튼이 없으므로 Esc·X는 '아니오'
                    break;
                case AdvDialogButtons.YesNoCancel:
                    specs.Add(Tuple.Create("예", DialogResult.Yes, true));
                    specs.Add(Tuple.Create("아니오", DialogResult.No, false));
                    specs.Add(Tuple.Create("취소", DialogResult.Cancel, false));
                    break;
                default:
                    specs.Add(Tuple.Create("확인", DialogResult.OK, true));
                    break;
            }

            // 각 버튼 폭 측정(최소 76)
            var made = new List<AdvButton>();
            int total = 0, spacing = 8;
            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            {
                foreach (var s in specs)
                {
                    int w = Math.Max(76, TextRenderer.MeasureText(g, s.Item1, Font).Width + 28);
                    var b = new AdvButton
                    {
                        Text = s.Item1,
                        Kind = s.Item3 ? AdvButtonKind.Filled : AdvButtonKind.Outline,
                        DialogResult = s.Item2,
                        Size = new Size(w, BtnH)
                    };
                    b.Styling.ShowFocusGlow = false;   // 다이얼로그 버튼엔 포커스 글로우 링을 그리지 않는다
                    made.Add(b);
                    total += w;
                }
            }
            total += spacing * (made.Count - 1);

            int x = clientW - Pad - total;
            for (int i = 0; i < made.Count; i++)
            {
                made[i].Location = new Point(x, btnTop);
                Controls.Add(made[i]);
                x += made[i].Width + spacing;
            }

            // Enter는 OnKeyDown에서 primary를 직접 누른다. 폼의 AcceptButton으로 지정하면
            // AdvButton이 기본 버튼 링(_isDefault)을 한 겹 더 그려 "이중 테두리"가 생기므로 지정하지 않는다.
            if (made.Count > 0) _primary = made[0];
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            using (var path = AdvGraphics.CreateRoundedRect(new Rectangle(0, 0, Width, Height), new AdvCorners(12)))
                Region = new Region(path);
        }

        // ── 그리기 ────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var theme = _theme;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            AdvFrameRenderer.Draw(g, bounds, theme, new AdvCorners(12), 1,
                                  theme.Surface, Color.Empty, theme.Border, null);

            // 제목
            var titleRect = new Rectangle(Pad, 0, Width - Pad * 2 - 16, TitleBarH);
            using (var tf = new Font(Font.FontFamily, Font.Size + 1.5f, FontStyle.Bold))
                TextRenderer.DrawText(g, _caption, tf, titleRect, theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            // 제목바 아래 옅은 구분선
            using (var pen = new Pen(theme.Border))
                g.DrawLine(pen, Pad, TitleBarH, Width - Pad, TitleBarH);

            // 닫기 ×
            using (var pen = new Pen(_closeHot ? theme.Text : theme.TextMuted, 1.5f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pen, _closeRect.Left, _closeRect.Top, _closeRect.Right, _closeRect.Bottom);
                g.DrawLine(pen, _closeRect.Left, _closeRect.Bottom, _closeRect.Right, _closeRect.Top);
            }

            if (_icon != AdvDialogIcon.None) DrawIcon(g, theme);

            // 본문
            TextRenderer.DrawText(g, _message, Font, _messageRect, theme.Text,
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.Top);
        }

        private void DrawIcon(Graphics g, AdvTheme theme)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color c = IconColor(theme);
            using (var b = new SolidBrush(c)) g.FillEllipse(b, _iconRect);

            var inner = Rectangle.Inflate(_iconRect, -9, -9);
            using (var pen = new Pen(Color.White, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                switch (_icon)
                {
                    case AdvDialogIcon.Success:   // 체크
                        g.DrawLines(pen, new[]
                        {
                            new Point(inner.Left, inner.Top + inner.Height * 3 / 5),
                            new Point(inner.Left + inner.Width * 2 / 5, inner.Bottom),
                            new Point(inner.Right, inner.Top)
                        });
                        break;
                    case AdvDialogIcon.Error:     // ×
                        g.DrawLine(pen, inner.Left, inner.Top, inner.Right, inner.Bottom);
                        g.DrawLine(pen, inner.Left, inner.Bottom, inner.Right, inner.Top);
                        break;
                    default:                       // i / ! / ? 는 글자로
                        string sym = _icon == AdvDialogIcon.Warning ? "!" : _icon == AdvDialogIcon.Question ? "?" : "i";
                        using (var f = new Font(Font.FontFamily, IconSize * 0.5f, FontStyle.Bold))
                            TextRenderer.DrawText(g, sym, f, _iconRect, Color.White,
                                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                        break;
                }
            }
        }

        private Color IconColor(AdvTheme theme)
        {
            switch (_icon)
            {
                case AdvDialogIcon.Success: return theme.Success;
                case AdvDialogIcon.Warning: return theme.Warning;
                case AdvDialogIcon.Error: return theme.Error;
                default: return theme.Accent;   // Info·Question
            }
        }

        // ── 상호작용 ──────────────────────────────────────────────────

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            if (Rectangle.Inflate(_closeRect, 6, 6).Contains(e.Location))
            {
                DialogResult = _cancelResult;   // 닫기(X)와 Esc가 같은 결과를 돌려주도록 통일
                Close();
                return;
            }
            if (e.Y < TitleBarH)   // 제목바 드래그
            {
                _dragging = true;
                _dragStart = e.Location;
                Capture = true;     // 드래그 중 폼 밖에서 버튼을 놓아도 MouseUp을 받도록
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            // 드래그는 왼쪽 버튼이 실제로 눌려 있을 때만(캡처가 풀린 뒤 잔상 이동 방지)
            if (_dragging && (e.Button & MouseButtons.Left) != 0)
            {
                Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
                return;
            }
            bool hot = Rectangle.Inflate(_closeRect, 6, 6).Contains(e.Location);
            if (hot != _closeHot) { _closeHot = hot; Invalidate(_closeRect); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_dragging) { _dragging = false; Capture = false; }
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            _dragging = false;   // 캡처가 강제로 풀리면(모달·Alt+Tab 등) 드래그 상태를 확실히 내린다
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = _cancelResult;
                Close();
                e.Handled = true;
                return;
            }
            if (e.KeyCode == Keys.Enter && _primary != null)
            {
                _primary.PerformClick();
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }
    }
}
