using OpenFairy.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    public partial class SelectedIconTestButton : global::OpenFairy.UGUI.Button<SelectedIconTestButton.button>
    {
        public enum button
        {
            up,
            down,
            over,
            selectedOver,
            disabled,
            selectedDisabled,
        }
        public ref Controller<button> m_button => ref controller;

        protected internal override int GetControllerPage(int controller) => controller switch
        {
            0 => (int)m_button.page,
            _ => -1,
        };

        protected internal override bool IsControllerChanging(int controller) => controller switch
        {
            0 => m_button.changing,
            _ => false,
        };

        protected internal override void SetControllerPage(int controller, int page)
        {
            switch (controller)
            {
                case 0: m_button.page = (button)page; break;
            }
        }

        protected override void InitializeControllers()
        {
            m_button.Initialize();
        }
        public global::OpenFairy.UGUI.Loader m_icon;

        protected override button? StatePage(VisualState state) => state switch
        {
            VisualState.Up => button.up,
            VisualState.Down => button.down,
            VisualState.Over => button.over,
            VisualState.SelectedOver => button.selectedOver,
            VisualState.Disabled => button.disabled,
            VisualState.SelectedDisabled => button.selectedDisabled,
            _ => null,
        };
[global::System.Serializable] public sealed class LocalizedTexts
{
}
public void ApplyLocale(global::OpenFairy.UGUI.Localization.LocaleTable texts)
{
}

    }
}
