namespace OpenFairy.UGUI.Tests
{
    public sealed class ControllerActionTestPanel : Component
    {
        public enum Page { First, Second, Third }
        public Controller<Page> first;
        public Controller<Page> second;

        protected override void InitializeControllers()
        {
            first.Initialize();
            second.Initialize();
        }

        protected internal override int GetControllerPage(int index) => index == 0 ? (int)first.page : (int)second.page;
        protected internal override bool IsControllerChanging(int index) => index == 0 ? first.changing : second.changing;
        protected internal override void SetControllerPage(int index, int page)
        {
            if (index == 0) first.page = (Page)page;
            else second.page = (Page)page;
        }
    }
}
