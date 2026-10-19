namespace OpenFairy.UGUI.Tests
{
    public sealed class InstanceControllerTestPanel : Component
    {
        public enum Page { First, Second, Third }
        public Controller<Page> first;
        public Controller<Page> second;

        protected override void InitializeControllers()
        {
            first.Initialize();
            second.Initialize();
        }

        protected internal override void SetControllerPage(int controller, int page)
        {
            switch (controller)
            {
                case 0: first.page = (Page)page; break;
                case 1: second.page = (Page)page; break;
            }
        }
    }
}
