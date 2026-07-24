using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace AdvancedControls.Controls.Design
{
    /// <summary>
    /// AdvWizard의 디자인 타임 지원. 페이지는 사이트된 Panel이라 자체 디자이너로 드롭을 받으므로,
    /// 여기서는 스마트 태그/컨텍스트 메뉴 동사만 얹는다:
    /// 페이지 추가(트랜잭션·직렬화), 현재 페이지 제거, 이전/다음 페이지(디자인 확인용 전환 — 직렬화 안 됨).
    /// </summary>
    internal sealed class AdvWizardDesigner : ParentControlDesigner
    {
        private DesignerVerbCollection _verbs;

        private AdvancedControls.Controls.AdvWizard Wizard
        {
            get { return (AdvancedControls.Controls.AdvWizard)Component; }
        }

        private IDesignerHost Host
        {
            get { return (IDesignerHost)GetService(typeof(IDesignerHost)); }
        }

        private IComponentChangeService ChangeService
        {
            get { return (IComponentChangeService)GetService(typeof(IComponentChangeService)); }
        }

        public override DesignerVerbCollection Verbs
        {
            get
            {
                if (_verbs == null)
                {
                    _verbs = new DesignerVerbCollection
                    {
                        new DesignerVerb("페이지 추가", OnAddPage),
                        new DesignerVerb("현재 페이지 제거", OnRemovePage),
                        new DesignerVerb("이전 페이지", OnPrevPage),
                        new DesignerVerb("다음 페이지", OnNextPage)
                    };
                }
                return _verbs;
            }
        }

        private void OnAddPage(object sender, EventArgs e)
        {
            var host = Host;
            if (host == null) return;

            using (var txn = host.CreateTransaction("위저드 페이지 추가"))
            {
                var page = (Panel)host.CreateComponent(typeof(Panel));
                Wizard.Controls.Add(page);   // OnControlAdded가 페이지로 채택한다
                Wizard.CurrentStep = Wizard.StepCount - 1;   // 새 페이지를 바로 보여준다(뷰 상태)
                MarkDirty();
                txn.Commit();
            }
        }

        private void OnRemovePage(object sender, EventArgs e)
        {
            var host = Host;
            if (host == null || Wizard.StepCount == 0) return;

            var page = Wizard.GetPage(Wizard.CurrentStep);
            if (page == null) return;

            using (var txn = host.CreateTransaction("위저드 페이지 제거"))
            {
                host.DestroyComponent(page);   // Controls에서 빠지며 OnControlRemoved가 목록을 맞춘다
                MarkDirty();
                txn.Commit();
            }
        }

        private void OnPrevPage(object sender, EventArgs e) { StepBy(-1); }
        private void OnNextPage(object sender, EventArgs e) { StepBy(+1); }

        /// <summary>디자인 확인용 페이지 전환. CurrentStep은 직렬화되지 않으므로 저장물에는 영향 없다.</summary>
        private void StepBy(int delta)
        {
            var w = Wizard;
            if (w.StepCount == 0) return;
            int to = w.CurrentStep + delta;
            if (to < 0 || to >= w.StepCount) return;
            w.CurrentStep = to;
        }

        /// <summary>동사 실행 뒤 디자이너가 변경을 인지(저장 표시·속성 창 갱신)하게 한다.</summary>
        private void MarkDirty()
        {
            var change = ChangeService;
            if (change != null)
            {
                change.OnComponentChanging(Component, null);
                change.OnComponentChanged(Component, null, null, null);
            }
        }
    }
}
