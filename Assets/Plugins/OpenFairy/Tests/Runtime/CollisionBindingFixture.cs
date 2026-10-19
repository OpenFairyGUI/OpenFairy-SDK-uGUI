using OpenFairy.UGUI;
using UnityEngine;

namespace BindingFixtures
{
    public class CollisionBindingFixture : OpenFairy.UGUI.Component
    {
        public enum Status { Idle, Active }
        public Controller<Status> m_Status;
        public RectTransform m_Status_2;
        public RectTransform m_Status_2_2;

        protected internal override int GetControllerPage(int controller) => (int)m_Status.page;
        protected internal override bool IsControllerChanging(int controller) => m_Status.changing;
        protected internal override void SetControllerPage(int controller, int page) => m_Status.page = (Status)page;
        protected override void InitializeControllers() => m_Status.Initialize();
    }
}
