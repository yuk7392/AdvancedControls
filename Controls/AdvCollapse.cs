using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AdvancedControls.Animation;

namespace AdvancedControls.Controls
{
    /// <summary>
    /// 트리거로 높이가 0과 콘텐츠 높이 사이를 부드럽게 오가며 펼쳐지고 접히는 컨테이너.
    /// 자식은 그대로 두고 컨테이너 높이만 줄여 클리핑한다. Bootstrap의 <c>.collapse</c>에
    /// 대응하며 아코디언의 기반 프리미티브다.
    /// </summary>
    [ToolboxItem(true)]
    [DefaultEvent("CollapsedChanged")]
    [Description("높이 애니메이션으로 펼치고 접는 컨테이너입니다.")]
    public class AdvCollapse : AdvContainerBase
    {
        private readonly AdvAnimator _anim;
        private bool _collapsed;
        private int _expandedHeight;      // 펼쳤을 때의 높이(0이면 첫 접힘 때 현재 높이로 잡는다)
        private bool _settingHeight;      // 애니메이션이 Height를 바꾸는 중임을 표시
        private AdvCollapseOptions _options;

        public event EventHandler CollapsedChanged;

        public AdvCollapse()
        {
            _anim = new AdvAnimator(0);
            _anim.ValueChanged += OnAnimTick;
            _anim.SetImmediate(1f);       // 기본은 펼침. _expandedHeight 가드가 있어 높이를 건드리지 않는다
        }

        protected override Size DefaultSize
        {
            get { return new Size(200, 100); }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("접힌 상태인지 여부입니다. 바꾸면 애니메이션으로 전환됩니다.")]
        public bool Collapsed
        {
            get { return _collapsed; }
            set
            {
                if (_collapsed == value) return;
                _collapsed = value;

                // 처음 접힐 때 현재 높이를 펼침 높이로 기억한다.
                if (value && _expandedHeight <= 0 && Height > 0)
                    _expandedHeight = Height;

                _anim.Duration = DesignMode ? 0 : EffectiveTheme.TransitionDuration;
                _anim.AnimateTo(value ? 0f : 1f);

                var h = CollapsedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 펼쳤을 때의 높이. 0이면 처음 접힐 때의 실제 높이를 자동으로 쓴다.
        /// 디자이너에서 명시적으로 지정할 수도 있다.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(0)]
        [Description("펼쳤을 때의 높이입니다. 0이면 첫 접힘 시점의 높이를 자동으로 사용합니다.")]
        public int ExpandedHeight
        {
            get { return _expandedHeight; }
            set
            {
                value = Math.Max(0, value);
                if (_expandedHeight == value) return;
                _expandedHeight = value;

                // 펼쳐져 있고 애니메이션 중이 아니면 곧바로 반영한다.
                if (!_collapsed && !_anim.IsAnimating && value > 0)
                {
                    _settingHeight = true;
                    Height = value;
                    _settingHeight = false;
                }
            }
        }

        [Category(AdvCategory.Name)]
        [Description("이 라이브러리가 추가한 속성입니다. 펼쳐서 조정합니다.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AdvCollapseOptions AdvancedControlOptions
        {
            get { return _options ?? (_options = new AdvCollapseOptions(this)); }
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (_expandedHeight <= 0) return;      // 관리할 높이가 아직 없다

            int target = (int)Math.Round(_expandedHeight * _anim.Eased);
            _settingHeight = true;
            Height = Math.Max(0, target);
            _settingHeight = false;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            // 사용자가 펼친 상태에서 크기를 바꾸면 그 높이를 펼침 높이로 갱신한다.
            // 애니메이션이 스스로 높이를 바꾸는 동안에는 잡지 않는다.
            if (!_settingHeight && !_collapsed && !_anim.IsAnimating && Height > 0)
                _expandedHeight = Height;
            base.OnResize(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // UserPaint 컨트롤이라 배경을 직접 채워야 자식 뒤에 시스템 색이 비치지 않는다.
            e.Graphics.Clear(EffectiveTheme.Surface);
            base.OnPaint(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _anim.ValueChanged -= OnAnimTick;
                _anim.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>AdvCollapse가 추가한 속성.</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class AdvCollapseOptions : AdvOptions
    {
        internal AdvCollapseOptions(AdvCollapse owner) : base(owner.Styling, owner.Palette)
        {
        }
    }
}
