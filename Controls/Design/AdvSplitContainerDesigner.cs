using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using System.Windows.Forms.Design.Behavior;

namespace AdvancedControls.Controls.Design
{
    /// <summary>
    /// AdvSplitContainer의 디자인 타임 지원. 커스텀 컨트롤은 OnMouseDown/Move가 디자이너 서피스에
    /// 먹혀 런타임 드래그 로직이 디자인 타임에 돌지 않는다. 그래서 표준 SplitContainer처럼
    /// BehaviorService 글리프로 스플리터를 끌 수 있게 한다.
    /// ParentControlDesigner를 상속해 컨테이너로의 자식 드롭(기존 동작)은 그대로 유지한다.
    /// </summary>
    internal sealed class AdvSplitContainerDesigner : ParentControlDesigner
    {
        // Behavior는 드래그 내내 같은 인스턴스여야 한다(캡처 push/pop 대상). GetGlyphs마다 새로 만들지 않는다.
        private SplitterBehavior _behavior;

        /// <summary>
        /// Panel1/Panel2를 디자인 타임 드롭 컨테이너로 만든다(표준 SplitContainer와 같은 방식).
        /// EnableDesignMode를 걸면 각 패널이 자기 디자이너(기본 PanelDesigner)를 얻어 컨트롤을
        /// 그 안으로 끌어다 놓을 수 있고, 자식들이 Panel1/Panel2.Controls로 직렬화된다.
        /// 이게 없으면 드롭한 컨트롤이 패널이 아니라 스플릿 컨테이너 위에 얹혀 나뉘지 않는다.
        /// </summary>
        public override void Initialize(IComponent component)
        {
            base.Initialize(component);

            var split = (AdvSplitContainer)component;
            EnableDesignMode(split.Panel1, "Panel1");
            EnableDesignMode(split.Panel2, "Panel2");
        }

        private SplitterBehavior Behavior
        {
            get { return _behavior ?? (_behavior = new SplitterBehavior(this)); }
        }

        /// <summary>선택됐을 때 기본 글리프(크기 조절 핸들 등)에 스플리터 드래그 글리프를 얹는다.</summary>
        public override GlyphCollection GetGlyphs(GlyphSelectionType selectionType)
        {
            var glyphs = base.GetGlyphs(selectionType);

            // 선택 상태에서만 스플리터 글리프를 노출한다(표준 SplitContainer 관례).
            if (selectionType != GlyphSelectionType.NotSelected)
            {
                var bs = BehaviorService;
                if (bs != null)
                    glyphs.Add(new SplitterGlyph(this, bs, Behavior));
            }
            return glyphs;
        }

        internal AdvSplitContainer Owner { get { return (AdvSplitContainer)Component; } }
        internal BehaviorService Service { get { return BehaviorService; } }

        internal PropertyDescriptor DistanceProperty
        {
            get { return TypeDescriptor.GetProperties(Component)["SplitterDistance"]; }
        }

        internal IDesignerHost Host
        {
            get { return (IDesignerHost)GetService(typeof(IDesignerHost)); }
        }

        internal IComponentChangeService ChangeService
        {
            get { return (IComponentChangeService)GetService(typeof(IComponentChangeService)); }
        }

        /// <summary>스플리터 막대의 컨트롤 클라이언트 좌표 사각형(공개 속성만으로 재계산).</summary>
        internal Rectangle SplitterClientRect()
        {
            var s = Owner;
            int d = s.SplitterDistance, w = s.SplitterWidth;
            return s.Orientation == Orientation.Vertical
                ? new Rectangle(d, 0, w, s.Height)
                : new Rectangle(0, d, s.Width, w);
        }
    }

    /// <summary>스플리터 막대 위에 놓이는 글리프. 히트되면 분할 커서를 돌려주고 드래그를 Behavior로 넘긴다.</summary>
    internal sealed class SplitterGlyph : Glyph
    {
        private readonly AdvSplitContainerDesigner _designer;
        private readonly BehaviorService _service;

        public SplitterGlyph(AdvSplitContainerDesigner designer, BehaviorService service, Behavior behavior)
            : base(behavior)
        {
            _designer = designer;
            _service = service;
        }

        /// <summary>어도너 창 좌표계의 막대 사각형. SplitterDistance가 바뀌면 매번 다시 계산된다.</summary>
        public override Rectangle Bounds
        {
            get
            {
                var owner = _designer.Owner;
                var rc = _designer.SplitterClientRect();
                Point origin = _service.ControlToAdornerWindow(owner);
                return new Rectangle(origin.X + rc.X, origin.Y + rc.Y, rc.Width, rc.Height);
            }
        }

        public override Cursor GetHitTest(Point p)
        {
            if (!Bounds.Contains(p)) return null;
            return _designer.Owner.Orientation == Orientation.Vertical ? Cursors.VSplit : Cursors.HSplit;
        }

        // 막대 자체는 컨트롤이 그리므로 글리프는 아무것도 덧그리지 않는다.
        public override void Paint(PaintEventArgs pe) { }
    }

    /// <summary>
    /// 스플리터 드래그 동작. 마우스 이동을 SplitterDistance 속성 변경으로 옮겨 직렬화·되돌리기가 되게 한다.
    /// 드래그 동안 PushCaptureBehavior로 모든 마우스 입력을 잡아 커서가 막대를 벗어나도 이어지고 MouseUp이 확실히 온다.
    /// </summary>
    internal sealed class SplitterBehavior : Behavior
    {
        private readonly AdvSplitContainerDesigner _designer;
        private bool _dragging;
        private DesignerTransaction _transaction;
        private object _originalValue;

        public SplitterBehavior(AdvSplitContainerDesigner designer)
        {
            _designer = designer;
        }

        public override Cursor Cursor
        {
            get { return _designer.Owner.Orientation == Orientation.Vertical ? Cursors.VSplit : Cursors.HSplit; }
        }

        public override bool OnMouseDown(Glyph g, MouseButtons button, Point mouseLoc)
        {
            if (button != MouseButtons.Left || _dragging) return false;

            var bs = _designer.Service;
            var prop = _designer.DistanceProperty;
            if (bs == null || prop == null || prop.IsReadOnly) return false;

            _dragging = true;
            var host = _designer.Host;
            _transaction = host != null ? host.CreateTransaction("Move splitter") : null;
            _originalValue = prop.GetValue(_designer.Owner);

            var change = _designer.ChangeService;
            if (change != null) change.OnComponentChanging(_designer.Owner, prop);   // 드래그 전체를 한 번의 변경으로

            bs.PushCaptureBehavior(this);   // 드래그 동안 모든 마우스 입력을 이 Behavior가 받는다
            return true;
        }

        public override bool OnMouseMove(Glyph g, MouseButtons button, Point mouseLoc)
        {
            if (!_dragging) return false;

            var owner = _designer.Owner;
            var prop = _designer.DistanceProperty;

            // 어도너 창 좌표 → 컨트롤 클라이언트 좌표로 되돌려 새 거리를 구한다.
            Point origin = _designer.Service.ControlToAdornerWindow(owner);
            int newDistance = owner.Orientation == Orientation.Vertical
                ? mouseLoc.X - origin.X
                : mouseLoc.Y - origin.Y;

            int current = (int)prop.GetValue(owner);
            if (newDistance != current)
            {
                prop.SetValue(owner, newDistance);   // 세터가 유효 범위로 클램프 — 라이브 피드백만, 알림은 끝에서 1회

                // 디자인 타임 캡처 드래그 중엔 메시지 펌프가 캡처 루프에 묶여 Invalidate가 띄운
                // WM_PAINT가 드래그가 끝나야 처리된다. Refresh()로 즉시 동기 리페인트(자식 포함)를 강제한다.
                owner.Refresh();
            }

            return true;
        }

        public override bool OnMouseUp(Glyph g, MouseButtons button)
        {
            if (!_dragging) return false;
            _dragging = false;

            var bs = _designer.Service;
            if (bs != null)
            {
                try { bs.PopBehavior(this); } catch (InvalidOperationException) { }   // 이미 팝됐으면 무시
            }

            var prop = _designer.DistanceProperty;
            var change = _designer.ChangeService;
            var txn = _transaction;
            _transaction = null;
            var original = _originalValue;
            _originalValue = null;

            bool committed = false;
            try
            {
                if (change != null)
                    change.OnComponentChanged(_designer.Owner, prop, original, prop.GetValue(_designer.Owner));
                if (txn != null) { txn.Commit(); committed = true; }
            }
            finally
            {
                // 커밋에 실패해도 트랜잭션을 확실히 닫는다 — 열린 채 남으면 재로드 시 직렬화가 깨진다.
                if (txn != null && !committed) txn.Cancel();
            }
            return true;
        }
    }
}
