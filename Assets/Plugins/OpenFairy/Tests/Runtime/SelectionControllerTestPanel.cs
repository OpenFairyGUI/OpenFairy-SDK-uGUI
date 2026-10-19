using UnityEngine;

namespace OpenFairy.UGUI.Tests
{
    public sealed class SelectionControllerTestPanel : Component
    {
        public enum Page { First, Second, Third }
        public Controller<Page> m_Page;
        public RectTransform m_Items;
        [SerializeField] private SoundImportButton _localizedItem0;
        [SerializeField] private SoundImportButton _localizedItem1;
        [SerializeField] private SoundImportButton _localizedItem2;
        [SerializeField] private ListSource _localizedList_items;
        protected override void InitializeControllers() => m_Page.Initialize();
        protected internal override int GetControllerPage(int controller) => controller == 0 ? (int)m_Page.page : -1;
        protected internal override bool IsControllerChanging(int controller) => controller == 0 && m_Page.changing;
        protected internal override void SetControllerPage(int controller, int page)
        {
            if (controller == 0) m_Page.page = (Page)page;
        }
    }
}
