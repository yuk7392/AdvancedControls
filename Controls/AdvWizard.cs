using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Rendering;
using AdvancedControls.Theming;

namespace AdvancedControls.Controls
{
    /// <summary>단계 이동 전 취소할 수 있는 이벤트 인자.</summary>
    public class AdvStepChangingEventArgs : EventArgs
    {
        public int FromStep { get; private set; }
        public int ToStep { get; private set; }
        /// <summary>true로 두면 이동하지 않는다(검증 실패 등).</summary>
        public bool Cancel { get; set; }
        public AdvStepChangingEventArgs(int from, int to) { FromStep = from; ToStep = to; }
    }

    /// <summary>
    /// 다단계 입력 흐름(마법사) 컨테이너. 위(또는 왼쪽)에 <see cref="AdvStepper"/>를 내장하고,
    /// <see cref="AddPage"/>로 만든 페이지 중 현재 단계 하나만 보여준다.
    /// 이동 버튼은 내장하지 않는다 — 소비자가 <see cref="NextStep"/>/<see cref="PreviousStep"/>을
    /// 부르고, <see cref="StepChanging"/>에서 검증 실패 시 취소한다.
    /// 마지막 단계에서 <see cref="NextStep"/>을 부르면 <see cref="Finished"/>가 발생한다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("StepChanged")]
    [DefaultProperty("AdvancedControlOptions")]
    [Designer(typeof(Design.AdvWizardDesigner))]
    [Description("단계 표시기와 페이지 전환을 담은 마법사 컨테이너입니다.")]
    public class AdvWizard : AdvContainerBase
    {
        // 96dpi 논리 치수
        private const int StepperH = 64;    // 가로 스테퍼 높이
        private const int StepperW = 170;   // 세로 스테퍼 폭
        private const int Gap = 8;

        private readonly AdvStepper _stepper;
        private readonly List<Panel> _pages = new List<Panel>();
        private readonly List<string> _titles = new List<string>();   // null이면 Name/"단계 N"으로 대체
        private int _current;
        private Orientation _orientation = Orientation.Horizontal;
        private AdvWizardOptions _options;

        /// <summary>단계가 바뀌기 직전 발생한다. Cancel로 이동을 막을 수 있다.</summary>
        [Category("Behavior")]
        [Description("단계가 바뀌기 직전 발생합니다. Cancel로 이동을 막을 수 있습니다.")]
        public event EventHandler<AdvStepChangingEventArgs> StepChanging;

        /// <summary>단계가 바뀐 뒤 발생한다.</summary>
        [Category("Behavior")]
        [Description("단계가 바뀐 뒤 발생합니다.")]
        public event EventHandler StepChanged;

        /// <summary>마지막 단계에서 NextStep()을 부르면 발생한다.</summary>
        [Category("Behavior")]
        [Description("마지막 단계에서 NextStep()을 부르면 발생합니다.")]
        public event EventHandler Finished;

        public AdvWizard()
        {
            _stepper = new AdvStepper();
            _stepper.StepClicked += StepperStepClicked;
            Controls.Add(_stepper);
        }

        protected override Size DefaultSize
        {
            get { return new Size(400, 280); }
        }

        /// <summary>이 라이브러리가 추가한 속성. 속성 창에서 펼쳐서 쓴다.</summary>
        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvWizardOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvWizardOptions(this)); }
        }

        /// <summary>내장 단계 표시기. 스타일을 바꿀 때 직접 접근한다.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvStepper Stepper
        {
            get { return _stepper; }
        }

        [Browsable(false)]      // 속성 창에는 AdvancedControlOptions 안에서만 보인다
        [DefaultValue(Orientation.Horizontal)]
        [Description("가로면 스테퍼가 위, 세로면 왼쪽에 놓입니다.")]
        public Orientation Orientation
        {
            get { return _orientation; }
            set
            {
                if (_orientation == value) return;
                _orientation = value;
                _stepper.Orientation = value;
                LayoutParts();
            }
        }

        [Browsable(false)]
        public int StepCount { get { return _pages.Count; } }

        /// <summary>현재 단계(0부터). 대입도 StepChanging 검증을 거친다.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int CurrentStep
        {
            get { return _current; }
            set { TryGo(value); }
        }

        /// <summary>단계 i의 페이지 패널. 범위를 벗어나면 null.</summary>
        public Panel GetPage(int index)
        {
            return index >= 0 && index < _pages.Count ? _pages[index] : null;
        }

        /// <summary>
        /// 단계를 추가하고 그 페이지 패널을 돌려준다. 자식 컨트롤은 이 패널에 올린다.
        /// </summary>
        public Panel AddPage(string title)
        {
            var page = new Panel { Visible = _pages.Count == 0 };
            _pages.Add(page);            // Controls.Add 전에 등록해 OnControlAdded 채택 경로와 중복되지 않게
            _titles.Add(title);
            Controls.Add(page);
            _stepper.AddStep(TitleOf(_pages.Count - 1));
            LayoutParts();
            return page;
        }

        /// <summary>모든 페이지를 지운다.</summary>
        public void ClearPages()
        {
            // Controls.Remove가 OnControlRemoved로 _pages를 함께 줄이므로 원본을 직접 순회하지 않는다
            var pages = _pages.ToArray();
            foreach (var p in pages)
            {
                Controls.Remove(p);
                p.Dispose();
            }
            _pages.Clear();
            _titles.Clear();
            _stepper.ClearSteps();
            _current = 0;
        }

        /// <summary>
        /// 디자이너(InitializeComponent 포함)가 Controls.Add로 넣은 패널을 페이지로 받아들인다.
        /// 제목은 패널 Name(없으면 "단계 N")을 쓴다. AddPage 경로는 이미 등록돼 있어 건너뛴다.
        /// </summary>
        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            var p = e.Control as Panel;
            if (p == null || _pages.Contains(p)) return;

            _pages.Add(p);
            _titles.Add(null);
            p.Visible = _pages.Count == 1;
            RebuildStepper();
            LayoutParts();
        }

        /// <summary>페이지 패널이 제거되면(디자이너 삭제 포함) 페이지 목록·스테퍼를 맞춘다.</summary>
        protected override void OnControlRemoved(ControlEventArgs e)
        {
            base.OnControlRemoved(e);
            var p = e.Control as Panel;
            if (p == null) return;
            int i = _pages.IndexOf(p);
            if (i < 0) return;

            _pages.RemoveAt(i);
            _titles.RemoveAt(i);
            if (_current >= _pages.Count) _current = Math.Max(0, _pages.Count - 1);
            for (int k = 0; k < _pages.Count; k++) _pages[k].Visible = k == _current;
            RebuildStepper();
            LayoutParts();
        }

        private string TitleOf(int i)
        {
            if (!string.IsNullOrEmpty(_titles[i])) return _titles[i];
            var name = _pages[i].Name;
            return !string.IsNullOrEmpty(name) ? name : "단계 " + (i + 1);
        }

        private void RebuildStepper()
        {
            _stepper.ClearSteps();
            for (int i = 0; i < _pages.Count; i++) _stepper.AddStep(TitleOf(i));
            _stepper.CurrentStep = _current;
        }

        /// <summary>
        /// 다음 단계로 간다. 마지막 단계였으면 이동 없이 <see cref="Finished"/>를 올린다.
        /// 이동했으면 true.
        /// </summary>
        public bool NextStep()
        {
            if (_pages.Count == 0) return false;
            if (_current >= _pages.Count - 1)
            {
                var h = Finished;
                if (h != null) h(this, EventArgs.Empty);
                return false;
            }
            return TryGo(_current + 1);
        }

        /// <summary>이전 단계로 간다. 이동했으면 true.</summary>
        public bool PreviousStep()
        {
            return TryGo(_current - 1);
        }

        private void StepperStepClicked(object sender, AdvStepEventArgs e)
        {
            TryGo(e.Index);
        }

        /// <summary>단계 이동 공통 경로. StepChanging 취소를 거쳐 페이지를 전환한다.</summary>
        private bool TryGo(int to)
        {
            if (_pages.Count == 0) return false;
            if (to < 0) to = 0;
            if (to > _pages.Count - 1) to = _pages.Count - 1;
            if (to == _current) return false;

            var changing = StepChanging;
            if (changing != null)
            {
                var args = new AdvStepChangingEventArgs(_current, to);
                changing(this, args);
                if (args.Cancel) return false;
            }

            _pages[_current].Visible = false;
            _current = to;
            _pages[_current].Visible = true;
            _stepper.CurrentStep = to;

            var changed = StepChanged;
            if (changed != null) changed(this, EventArgs.Empty);
            return true;
        }

        // ── 배치 ──────────────────────────────────────────────────────

        private void LayoutParts()
        {
            var c = ClientRectangle;
            c = new Rectangle(c.Left + Padding.Left, c.Top + Padding.Top,
                              Math.Max(0, c.Width - Padding.Horizontal),
                              Math.Max(0, c.Height - Padding.Vertical));
            int gap = AdvGraphics.Scale(this, Gap);

            Rectangle pageRect;
            if (_orientation == Orientation.Horizontal)
            {
                int h = AdvGraphics.Scale(this, StepperH);
                _stepper.Bounds = new Rectangle(c.Left, c.Top, c.Width, h);
                pageRect = Rectangle.FromLTRB(c.Left, c.Top + h + gap, c.Right, c.Bottom);
            }
            else
            {
                int w = AdvGraphics.Scale(this, StepperW);
                _stepper.Bounds = new Rectangle(c.Left, c.Top, w, c.Height);
                pageRect = Rectangle.FromLTRB(c.Left + w + gap, c.Top, c.Right, c.Bottom);
            }

            foreach (var p in _pages) p.Bounds = pageRect;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutParts();
        }

        protected override void OnPaddingChanged(EventArgs e)
        {
            base.OnPaddingChanged(e);
            LayoutParts();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stepper.StepClicked -= StepperStepClicked;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>AdvWizard가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvWizardOptions : AdvOptions
    {
        private readonly AdvWizard _owner;

        internal AdvWizardOptions(AdvWizard owner) : base(owner.Styling, owner.Palette)
        {
            _owner = owner;
        }

        [DefaultValue(Orientation.Horizontal)]
        [Description("가로면 스테퍼가 위, 세로면 왼쪽에 놓입니다.")]
        public Orientation Orientation
        {
            get { return _owner.Orientation; }
            set { _owner.Orientation = value; }
        }
    }
}
