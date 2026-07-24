using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows.Forms;

namespace AdvancedControls.Controls
{
    /// <summary>
    /// 형식이 정해진 입력(전화번호·날짜·사업자번호 등)을 받는 마스크 입력창.
    /// <see cref="AdvTextBox"/>의 셸(테마 테두리·아이콘·검증 상태)을 그대로 물려받고
    /// 마스크 엔진만 얹는다. 문법은 표준 MaskedTextBox의 부분집합이다:
    /// 0=숫자, 9=숫자(선택), L=글자, ?=글자(선택), A=글자/숫자, a=글자/숫자(선택),
    /// &amp;=아무 문자, C=아무 문자(선택), \=이스케이프(다음 문자를 리터럴로), 나머지=리터럴.
    /// 입력은 덮어쓰기 방식이며, 지우기는 그 자리만 비운다(뒤 문자를 당기지 않는다 — 표준과 다른 점).
    /// 숫자·리터럴로만 이루어진 마스크에서는 한글 IME를 자동으로 끈다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("TextChanged")]
    [DefaultProperty("AdvancedControlOptions")]
    [Description("형식이 정해진 입력을 받는 마스크 입력창입니다.")]
    public class AdvMaskedTextBox : AdvTextBox
    {
        /// <summary>마스크 한 칸. Kind가 '\0'이면 리터럴, 아니면 자리 표시자다.</summary>
        private struct Slot
        {
            public char Kind;      // '\0'=리터럴, 그 외 0 9 L ? A a & C
            public char Literal;   // 리터럴일 때 표시할 문자
            public char Value;     // 자리 표시자에 채워진 값('\0'=빈 칸)
        }

        private const string PlaceholderKinds = "09L?AaC&";
        private const string RequiredKinds = "0LA&";

        private string _mask = string.Empty;
        private char _prompt = '_';
        private readonly List<Slot> _slots = new List<Slot>();
        private bool _updating;   // 우리가 넣는 텍스트로 재진입하지 않게 막는다

        private AdvMaskedTextBoxOptions _maskedOptions;

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new AdvMaskedTextBoxOptions AdvancedControlOptions
        {
            get { return _maskedOptions ?? (_maskedOptions = new AdvMaskedTextBoxOptions(this)); }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue("")]
        [Description("입력 형식입니다. 0=숫자, 9=숫자(선택), L=글자, A=글자/숫자, &=아무 문자, \\=이스케이프, 나머지=리터럴. 비우면 일반 입력창처럼 동작합니다.")]
        public string Mask
        {
            get { return _mask; }
            set
            {
                value = value ?? string.Empty;
                if (_mask == value) return;
                _mask = value;
                ParseMask();
                UpdateImeBlock();
                if (HasMask) RebuildDisplay(NextPlaceholder(0));
                else { _updating = true; try { InnerTextBox.Text = string.Empty; } finally { _updating = false; } }
            }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue('_')]
        [Description("빈 자리에 표시할 문자입니다.")]
        public char PromptChar
        {
            get { return _prompt; }
            set
            {
                if (_prompt == value) return;
                _prompt = value;
                if (HasMask) RebuildDisplay(InnerTextBox.SelectionStart);
            }
        }

        /// <summary>마스크가 지정돼 있는지. 없으면 일반 입력창처럼 동작한다.</summary>
        [Browsable(false)]
        public bool HasMask
        {
            get { return _slots.Count > 0; }
        }

        private bool _pullOnDelete;

        /// <summary>
        /// 지울 때 뒤 값을 앞으로 당길지. 기본 false(표준 MaskedTextBox처럼 자리만 비움).
        /// 당김은 각 값이 새 자리 종류에 맞을 때까지만 진행한다(숫자 자리에 글자를 당겨 넣지 않는다).
        /// 입력(덮어쓰기)에는 적용되지 않고 Backspace·Delete·선택 삭제에만 적용된다.
        /// </summary>
        [Browsable(false)]
        [DefaultValue(false)]
        public bool PullOnDelete
        {
            get { return _pullOnDelete; }
            set { _pullOnDelete = value; }
        }

        /// <summary>리터럴·빈 칸을 뺀, 사용자가 채운 값만. 대입하면 순서대로 다시 채운다.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string CleanText
        {
            get
            {
                var sb = new StringBuilder();
                foreach (var s in _slots)
                    if (s.Kind != '\0' && s.Value != '\0') sb.Append(s.Value);
                return sb.ToString();
            }
            set
            {
                ClearValues();
                FillSequential(value ?? string.Empty);
                RebuildDisplay(NextEmptyOrEnd());
            }
        }

        /// <summary>필수 자리(0·L·A·&amp;)가 전부 채워졌는지.</summary>
        [Browsable(false)]
        public bool MaskCompleted
        {
            get
            {
                foreach (var s in _slots)
                    if (s.Kind != '\0' && RequiredKinds.IndexOf(s.Kind) >= 0 && s.Value == '\0')
                        return false;
                return true;
            }
        }

        // ── 마스크 파싱 ───────────────────────────────────────────────

        private void ParseMask()
        {
            _slots.Clear();
            for (int i = 0; i < _mask.Length; i++)
            {
                char c = _mask[i];
                if (c == '\\' && i + 1 < _mask.Length)
                {
                    _slots.Add(new Slot { Literal = _mask[++i] });
                }
                else if (PlaceholderKinds.IndexOf(c) >= 0)
                {
                    _slots.Add(new Slot { Kind = c });
                }
                else
                {
                    _slots.Add(new Slot { Literal = c });
                }
            }
        }

        /// <summary>글자 자리가 하나도 없으면(숫자·리터럴뿐) 한글 IME 조합이 낄 이유가 없으므로 끈다.</summary>
        private void UpdateImeBlock()
        {
            bool hasLetterSlot = false;
            foreach (var s in _slots)
                if (s.Kind == 'L' || s.Kind == '?' || s.Kind == 'A' || s.Kind == 'a'
                 || s.Kind == '&' || s.Kind == 'C') { hasLetterSlot = true; break; }

            InnerTextBox.ImeMode = HasMask && !hasLetterSlot ? ImeMode.Disable : ImeMode.NoControl;
        }

        private static bool Matches(char kind, char c)
        {
            switch (kind)
            {
                case '0': case '9': return char.IsDigit(c);
                case 'L': case '?': return char.IsLetter(c);
                case 'A': case 'a': return char.IsLetterOrDigit(c);
                case '&': case 'C': return !char.IsControl(c);
                default: return false;
            }
        }

        // ── 자리 탐색 ─────────────────────────────────────────────────

        /// <summary>from 이후(포함) 첫 자리 표시자 인덱스. 없으면 -1.</summary>
        private int NextPlaceholder(int from)
        {
            for (int i = Math.Max(0, from); i < _slots.Count; i++)
                if (_slots[i].Kind != '\0') return i;
            return -1;
        }

        /// <summary>before 앞(미포함) 마지막 자리 표시자 인덱스. 없으면 -1.</summary>
        private int PrevPlaceholder(int before)
        {
            for (int i = Math.Min(before, _slots.Count) - 1; i >= 0; i--)
                if (_slots[i].Kind != '\0') return i;
            return -1;
        }

        private int NextEmptyOrEnd()
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].Kind != '\0' && _slots[i].Value == '\0') return i;
            return _slots.Count;
        }

        // ── 값 조작 ───────────────────────────────────────────────────

        private void ClearValues()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                s.Value = '\0';
                _slots[i] = s;
            }
        }

        /// <summary>문자들을 순서대로 자리에 채운다. 자리 종류와 맞지 않는 문자는 버린다.</summary>
        private void FillSequential(string chars)
        {
            int si = 0;
            foreach (char c in chars)
            {
                int p = -1;
                for (int i = si; i < _slots.Count; i++)
                    if (_slots[i].Kind != '\0' && Matches(_slots[i].Kind, c)) { p = i; break; }
                if (p < 0) continue;
                var s = _slots[p];
                s.Value = c;
                _slots[p] = s;
                si = p + 1;
            }
        }

        private void ClearRange(int start, int length)
        {
            int end = Math.Min(_slots.Count, start + length);
            for (int i = Math.Max(0, start); i < end; i++)
            {
                if (_slots[i].Kind == '\0') continue;
                var s = _slots[i];
                s.Value = '\0';
                _slots[i] = s;
            }
        }

        /// <summary>
        /// start부터의 빈 자리에 뒤쪽 값을 순서대로 당겨 넣는다(PullOnDelete).
        /// 당길 값이 대상 자리 종류에 안 맞으면 거기서 멈춘다 — "000-LL"에서 글자를 숫자 자리로
        /// 끌어오지 않는다. 리터럴은 제자리 고정이고 값만 자리 표시자 사이를 이동한다.
        /// </summary>
        private void PullLeft(int start)
        {
            int dst = NextPlaceholder(start);
            while (dst >= 0)
            {
                if (_slots[dst].Value == '\0')
                {
                    int src = -1;
                    for (int i = dst + 1; i < _slots.Count; i++)
                        if (_slots[i].Kind != '\0' && _slots[i].Value != '\0') { src = i; break; }
                    if (src < 0) break;                                        // 더 당길 값이 없다
                    if (!Matches(_slots[dst].Kind, _slots[src].Value)) break;  // 형식 불일치: 중단

                    var d = _slots[dst]; d.Value = _slots[src].Value; _slots[dst] = d;
                    var s = _slots[src]; s.Value = '\0'; _slots[src] = s;
                }
                dst = NextPlaceholder(dst + 1);
            }
        }

        // ── 표시 갱신 ─────────────────────────────────────────────────

        private string DisplayText()
        {
            var sb = new StringBuilder(_slots.Count);
            foreach (var s in _slots)
                sb.Append(s.Kind == '\0' ? s.Literal : (s.Value != '\0' ? s.Value : _prompt));
            return sb.ToString();
        }

        private void RebuildDisplay(int caret)
        {
            _updating = true;
            try
            {
                string t = DisplayText();
                if (InnerTextBox.Text != t) InnerTextBox.Text = t;
                if (caret < 0) caret = t.Length;
                InnerTextBox.SelectionStart = Math.Min(Math.Max(0, caret), t.Length);
                InnerTextBox.SelectionLength = 0;
            }
            finally { _updating = false; }
        }

        // ── 입력 처리 ─────────────────────────────────────────────────

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (HasMask && !ReadOnly && !char.IsControl(e.KeyChar))
            {
                HandleTypedChar(e.KeyChar);
                e.Handled = true;   // 실제 삽입은 우리가 한다(무효 문자는 무시)
            }
            base.OnKeyPress(e);
        }

        private void HandleTypedChar(char c)
        {
            int caret = InnerTextBox.SelectionStart;

            // 선택이 있으면 그 구간을 비우고 선택 시작에서 이어 쓴다
            if (InnerTextBox.SelectionLength > 0)
            {
                ClearRange(caret, InnerTextBox.SelectionLength);
            }

            int p = NextPlaceholder(caret);
            if (p < 0 || !Matches(_slots[p].Kind, c)) { RebuildDisplay(caret); return; }

            var s = _slots[p];
            s.Value = c;
            _slots[p] = s;

            // 다음 자리 표시자 앞으로(리터럴은 건너뜀). 마지막이면 그 칸 뒤에 둔다
            int next = NextPlaceholder(p + 1);
            RebuildDisplay(next >= 0 ? next : p + 1);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (HasMask && !ReadOnly && (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete))
            {
                int caret = InnerTextBox.SelectionStart;
                int sel = InnerTextBox.SelectionLength;

                if (sel > 0)
                {
                    ClearRange(caret, sel);
                    if (_pullOnDelete) PullLeft(caret);
                    RebuildDisplay(caret);
                }
                else if (e.KeyCode == Keys.Back)
                {
                    // 캐럿 앞의 가장 가까운 자리(리터럴 건너뜀)를 비우고 그 자리로 이동
                    int p = PrevPlaceholder(caret);
                    if (p >= 0) { ClearRange(p, 1); if (_pullOnDelete) PullLeft(p); RebuildDisplay(p); }
                }
                else
                {
                    // Delete: 캐럿 위치(이후 첫 자리)를 비우고 캐럿은 그대로
                    int p = NextPlaceholder(caret);
                    if (p >= 0) { ClearRange(p, 1); if (_pullOnDelete) PullLeft(p); RebuildDisplay(p); }
                }

                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        /// <summary>
        /// 키 입력은 위에서 다 가로채므로, 여기로 오는 변경은 붙여넣기·실행취소·Text 대입 같은
        /// 외부 경로다. 텍스트에서 유효 문자를 순서대로 추려 마스크에 다시 채운다.
        /// </summary>
        protected override void OnTextChanged(EventArgs e)
        {
            if (HasMask && !_updating)
            {
                string raw = InnerTextBox.Text;
                ClearValues();
                ExtractFrom(raw);
                RebuildDisplay(NextEmptyOrEnd());
            }
            base.OnTextChanged(e);
        }

        /// <summary>
        /// 자유 텍스트에서 값을 추린다. 마스크의 리터럴·빈 칸 표시는 제자리로 인식해 건너뛰므로
        /// "12_-45" 같은 기존 표시 문자열을 넣어도 자리가 밀리지 않는다.
        /// </summary>
        private void ExtractFrom(string raw)
        {
            int si = 0;   // 다음으로 볼 슬롯
            foreach (char c in raw)
            {
                if (si >= _slots.Count) break;

                // 현재 슬롯이 리터럴이고 문자가 그 리터럴이면 제자리 통과
                if (_slots[si].Kind == '\0' && _slots[si].Literal == c) { si++; continue; }

                // 빈 칸 표시 문자는 그 자리(자리 표시자)를 비운 채 건너뛴다
                if (c == _prompt && _slots[si].Kind != '\0') { si++; continue; }

                // 리터럴을 건너뛰며 문자가 맞는 첫 자리에 채운다
                int p = -1;
                for (int i = si; i < _slots.Count; i++)
                    if (_slots[i].Kind != '\0' && Matches(_slots[i].Kind, c)) { p = i; break; }
                if (p < 0) continue;   // 어디에도 안 맞는 문자는 버린다

                var s = _slots[p];
                s.Value = c;
                _slots[p] = s;
                si = p + 1;
            }
        }
    }

    /// <summary>AdvMaskedTextBox가 추가한 속성. AdvTextBox 파사드에 마스크 항목을 얹는다.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvMaskedTextBoxOptions : AdvTextBoxOptions
    {
        private readonly AdvMaskedTextBox _owner;

        internal AdvMaskedTextBoxOptions(AdvMaskedTextBox owner) : base(owner)
        {
            _owner = owner;
        }

        [DefaultValue("")]
        [Description("입력 형식입니다. 0=숫자, 9=숫자(선택), L=글자, A=글자/숫자, &=아무 문자, \\=이스케이프, 나머지=리터럴.")]
        public string Mask
        {
            get { return _owner.Mask; }
            set { _owner.Mask = value; }
        }

        [DefaultValue('_')]
        [Description("빈 자리에 표시할 문자입니다.")]
        public char PromptChar
        {
            get { return _owner.PromptChar; }
            set { _owner.PromptChar = value; }
        }

        [DefaultValue(false)]
        [Description("지울 때 뒤 값을 앞으로 당깁니다(형식이 맞는 자리까지만). 끄면 표준처럼 자리만 비웁니다.")]
        public bool PullOnDelete
        {
            get { return _owner.PullOnDelete; }
            set { _owner.PullOnDelete = value; }
        }
    }
}
