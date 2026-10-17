#if FAIRYGUI_TMPRO
using TMPro;

namespace OpenFairy.UGUI.Tests
{
    public sealed class ContentSizeTestButton : Button<ContentSizeTestButton.button>
    {
        public enum button { up, down }
        public ref Controller<button> m_button => ref controller;
        public TextMeshProUGUI m_title;

        protected override void InitializeControllers() => controller.Initialize();
    }
}
#endif
