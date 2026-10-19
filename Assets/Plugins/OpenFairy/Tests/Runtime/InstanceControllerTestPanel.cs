namespace OpenFairy.UGUI.Tests
{
    public sealed class InstanceControllerTestPanel : Component
    {
        public enum Page { First, Second, Third }
        public Controller<Page> first;
        public Controller<Page> second;

        protected override void InitializeControllers()
        {
            this.first.Initialize();
            this.second.Initialize();
        }

        protected internal override void SetControllerPage(int controller, int page)
        {
            switch (controller)
            {
                case 0: this.first.page = (Page)page; break;
                case 1: this.second.page = (Page)page; break;
            }
        }
    }
}
