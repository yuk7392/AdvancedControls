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
    /// <summary>
    /// 테마를 따르는 툴팁(SuperTip). 표준 <see cref="ToolTip"/>과 같은 방식으로 각 컨트롤에
    /// <c>ToolTipText</c> 속성을 붙여 쓴다. 텍스트의 첫 줄은 제목(굵게), 나머지는 본문으로 그린다.
    /// </summary>
    /// <remarks>
    /// OS 툴팁 대신 직접 그린 둥근 말풍선을 <see cref="ToolStripDropDown"/>에 띄운다 —
    /// 표준 툴팁은 테마 색·둥근 모서리가 먹지 않기 때문이다. 콤보·달력 팝업과 같은 방식이다.
    /// </remarks>
    [ProvideProperty("ToolTipText", typeof(Control))]
    [ToolboxItem(true)]
    [Description("테마를 따르는 툴팁입니다. 첫 줄은 제목(굵게), 나머지는 본문으로 그립니다.")]
    public class AdvToolTip : Component, IExtenderProvider
    {
        private readonly Dictionary<Control, string> _texts = new Dictionary<Control, string>();
        private readonly Timer _showTimer;   // 마우스를 올린 뒤 뜨기까지의 지연
        private readonly Timer _hideTimer;   // 저절로 사라지기까지의 시간

        private ToolStripDropDown _popup;
        private ToolTipBubble _bubble;
        private ToolStripControlHost _host;
        private Control _target;             // 지금 타이머가 겨냥한 컨트롤
        private AdvTheme _theme;
        private int _initialDelay = 600;
        private int _autoPopDelay = 5000;

        public AdvToolTip()
        {
            _showTimer = new Timer();
            _showTimer.Tick += OnShowTick;
            _hideTimer = new Timer();
            _hideTimer.Tick += OnHideTick;
        }

        public AdvToolTip(IContainer container) : this()
        {
            if (container != null) container.Add(this);
        }

        [DefaultValue(600)]
        [Category(AdvCategory.Name)]
        [Description("마우스를 올린 뒤 툴팁이 뜨기까지 지연(ms)입니다.")]
        public int InitialDelay
        {
            get { return _initialDelay; }
            set { _initialDelay = Math.Max(0, value); }
        }

        [DefaultValue(5000)]
        [Category(AdvCategory.Name)]
        [Description("툴팁이 저절로 사라지기까지 시간(ms)입니다. 0이면 마우스가 벗어날 때까지 유지됩니다.")]
        public int AutoPopDelay
        {
            get { return _autoPopDelay; }
            set { _autoPopDelay = Math.Max(0, value); }
        }

        /// <summary>이 툴팁만 다른 테마를 쓸 때 지정한다. null이면 전역 테마를 따른다.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvTheme Theme
        {
            get { return _theme; }
            set { _theme = value; }
        }

        private AdvTheme EffectiveTheme
        {
            get { return _theme ?? AdvThemeManager.Current; }
        }

        bool IExtenderProvider.CanExtend(object extendee)
        {
            return extendee is Control && !(extendee is Form) && !(extendee is AdvToolTip);
        }

        [DefaultValue("")]
        [Category(AdvCategory.Name)]
        [Description("이 컨트롤에 표시할 툴팁입니다. 첫 줄은 제목(굵게)으로 그려집니다. 비우면 툴팁이 없습니다.")]
        [Editor("System.ComponentModel.Design.MultilineStringEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
                "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        public string GetToolTipText(Control control)
        {
            string s;
            return control != null && _texts.TryGetValue(control, out s) ? s : string.Empty;
        }

        public void SetToolTipText(Control control, string value)
        {
            if (control == null) return;

            if (string.IsNullOrEmpty(value))
            {
                if (_texts.Remove(control)) Unhook(control);
            }
            else
            {
                bool isNew = !_texts.ContainsKey(control);
                _texts[control] = value;
                if (isNew) Hook(control);
            }
        }

        private void Hook(Control c)
        {
            c.MouseEnter += OnTargetMouseEnter;
            c.MouseLeave += OnTargetMouseLeave;
            c.MouseDown += OnTargetMouseDown;
            c.HandleDestroyed += OnTargetHandleDestroyed;
        }

        private void Unhook(Control c)
        {
            c.MouseEnter -= OnTargetMouseEnter;
            c.MouseLeave -= OnTargetMouseLeave;
            c.MouseDown -= OnTargetMouseDown;
            c.HandleDestroyed -= OnTargetHandleDestroyed;
            if (ReferenceEquals(_target, c)) { _showTimer.Stop(); _target = null; }
        }

        private bool DesignModeActive
        {
            get { return LicenseManager.UsageMode == LicenseUsageMode.Designtime; }
        }

        private void OnTargetMouseEnter(object sender, EventArgs e)
        {
            if (DesignModeActive) return;
            _target = sender as Control;
            _showTimer.Stop();
            _showTimer.Interval = Math.Max(1, _initialDelay);
            _showTimer.Start();
        }

        private void OnTargetMouseLeave(object sender, EventArgs e)
        {
            _showTimer.Stop();
            _target = null;
            HidePopup();
        }

        private void OnTargetMouseDown(object sender, MouseEventArgs e)
        {
            _showTimer.Stop();
            HidePopup();
        }

        // 핸들이 사라지면(폼 닫힘 등) 후크를 떼 누수를 막는다. 텍스트 매핑은 유지한다.
        private void OnTargetHandleDestroyed(object sender, EventArgs e)
        {
            var c = sender as Control;
            if (c != null && ReferenceEquals(_target, c))
            {
                _showTimer.Stop();
                _target = null;
                HidePopup();
            }
        }

        private void OnShowTick(object sender, EventArgs e)
        {
            _showTimer.Stop();

            var c = _target;
            if (c == null || !c.IsHandleCreated) return;

            string text;
            if (!_texts.TryGetValue(c, out text) || string.IsNullOrEmpty(text)) return;

            // 지연 사이에 마우스가 실제로 그 컨트롤을 벗어났으면 띄우지 않는다
            if (!c.ClientRectangle.Contains(c.PointToClient(Cursor.Position))) return;

            ShowBubble(text, Cursor.Position);
        }

        private void OnHideTick(object sender, EventArgs e)
        {
            _hideTimer.Stop();
            HidePopup();
        }

        private void EnsurePopup()
        {
            if (_popup != null) return;

            _bubble = new ToolTipBubble();
            _host = new ToolStripControlHost(_bubble);
            _host.Margin = Padding.Empty;
            _host.Padding = Padding.Empty;
            _host.AutoSize = false;

            _popup = new ToolStripDropDown();
            _popup.AutoSize = false;
            _popup.Margin = Padding.Empty;
            _popup.Padding = Padding.Empty;
            _popup.AutoClose = false;          // 마우스 이동으로 저절로 닫히지 않게 우리가 제어한다
            _popup.DropShadowEnabled = false;  // 둥근 Region과 함께 쓰면 그림자가 어긋나므로 끈다
            _popup.Items.Add(_host);
        }

        private void ShowBubble(string text, Point screenPt)
        {
            EnsurePopup();
            var theme = EffectiveTheme;

            string title, body;
            SplitTitleBody(text, out title, out body);
            _bubble.SetContent(title, body, theme);

            var size = _bubble.Measure();
            _bubble.Size = size;
            _host.Size = size;
            _popup.Size = size;

            // 커서 우하단에 띄우고, 화면(작업 영역) 밖으로 나가면 반대쪽으로 접는다
            var wa = Screen.FromPoint(screenPt).WorkingArea;
            int x = screenPt.X + 14;
            int y = screenPt.Y + 20;
            if (x + size.Width > wa.Right) x = screenPt.X - size.Width - 4;
            if (x < wa.Left) x = wa.Left;
            if (y + size.Height > wa.Bottom) y = screenPt.Y - size.Height - 6;
            if (y < wa.Top) y = wa.Top;

            // 팝업 전체를 둥근 영역으로 깎아 진짜 둥근 모서리를 만든다
            var old = _popup.Region;
            using (var rp = AdvGraphics.CreateRoundedRect(new Rectangle(0, 0, size.Width, size.Height), 6))
                _popup.Region = new Region(rp);
            if (old != null) old.Dispose();

            _popup.Show(x, y);

            _hideTimer.Stop();
            if (_autoPopDelay > 0)
            {
                _hideTimer.Interval = _autoPopDelay;
                _hideTimer.Start();
            }
        }

        private void HidePopup()
        {
            _hideTimer.Stop();
            if (_popup != null && _popup.Visible) _popup.Close();
        }

        /// <summary>첫 줄을 제목, 나머지를 본문으로 나눈다. 한 줄뿐이면 제목만 있고 본문은 빈 문자열.</summary>
        private static void SplitTitleBody(string text, out string title, out string body)
        {
            text = text.Replace("\r\n", "\n");
            int nl = text.IndexOf('\n');
            if (nl < 0) { title = text; body = string.Empty; return; }
            title = text.Substring(0, nl);
            body = text.Substring(nl + 1).TrimStart('\n');
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _showTimer.Tick -= OnShowTick;
                _hideTimer.Tick -= OnHideTick;
                _showTimer.Dispose();
                _hideTimer.Dispose();

                // 남아 있는 후크를 모두 뗀다
                foreach (var c in new List<Control>(_texts.Keys))
                {
                    c.MouseEnter -= OnTargetMouseEnter;
                    c.MouseLeave -= OnTargetMouseLeave;
                    c.MouseDown -= OnTargetMouseDown;
                    c.HandleDestroyed -= OnTargetHandleDestroyed;
                }
                _texts.Clear();

                if (_popup != null)
                {
                    if (_popup.Region != null) _popup.Region.Dispose();
                    _popup.Dispose();
                    _popup = null;
                }
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>툴팁 말풍선의 내용을 직접 그리는 컨트롤. <see cref="AdvToolTip"/>이 팝업에 올린다.</summary>
    internal sealed class ToolTipBubble : Control
    {
        private const int PadH = 10;
        private const int PadV = 8;
        private const int TitleGap = 3;
        private const int MaxWidth = 360;

        private string _title = string.Empty;
        private string _body = string.Empty;
        private AdvTheme _theme;
        private Font _boldFont;

        public ToolTipBubble()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer, true);
        }

        public void SetContent(string title, string body, AdvTheme theme)
        {
            _title = title ?? string.Empty;
            _body = body ?? string.Empty;
            _theme = theme;
            EnsureBoldFont();
            Invalidate();
        }

        private void EnsureBoldFont()
        {
            if (_boldFont == null || !_boldFont.FontFamily.Equals(Font.FontFamily)
                || _boldFont.SizeInPoints != Font.SizeInPoints)
            {
                if (_boldFont != null) _boldFont.Dispose();
                _boldFont = new Font(Font, FontStyle.Bold);
            }
        }

        private const TextFormatFlags TitleFlags =
            TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.Left;
        private const TextFormatFlags BodyFlags =
            TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.Left;

        /// <summary>내용에 맞는 말풍선 크기. 폭은 <see cref="MaxWidth"/>에서 줄바꿈된다.</summary>
        public Size Measure()
        {
            EnsureBoldFont();
            var cap = new Size(MaxWidth - PadH * 2, int.MaxValue);

            Size t = _title.Length > 0
                ? TextRenderer.MeasureText(_title, _boldFont, cap, TitleFlags)
                : Size.Empty;
            Size b = _body.Length > 0
                ? TextRenderer.MeasureText(_body, Font, cap, BodyFlags)
                : Size.Empty;

            int w = Math.Max(t.Width, b.Width) + PadH * 2;
            int h = PadV * 2 + t.Height
                  + (t.Height > 0 && b.Height > 0 ? TitleGap : 0) + b.Height;

            return new Size(Math.Max(28, w), Math.Max(24, h));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = _theme ?? AdvThemeManager.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = AdvGraphics.CreateRoundedRect(r, 6))
            {
                using (var fill = new SolidBrush(theme.Surface))
                    g.FillPath(fill, path);
                using (var pen = new Pen(theme.Border, 1f))
                    g.DrawPath(pen, path);
            }

            int x = PadH, y = PadV;
            int textW = Width - PadH * 2;

            if (_title.Length > 0)
            {
                var ts = TextRenderer.MeasureText(_title, _boldFont, new Size(textW, int.MaxValue), TitleFlags);
                TextRenderer.DrawText(g, _title, _boldFont,
                    new Rectangle(x, y, textW, ts.Height), theme.Text, TitleFlags);
                y += ts.Height + (_body.Length > 0 ? TitleGap : 0);
            }

            if (_body.Length > 0)
            {
                TextRenderer.DrawText(g, _body, Font,
                    new Rectangle(x, y, textW, Height - y - PadV), theme.TextMuted, BodyFlags);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _boldFont != null) { _boldFont.Dispose(); _boldFont = null; }
            base.Dispose(disposing);
        }
    }
}
