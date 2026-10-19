using OpenFairy.UGUI;
using UnityEngine;
using UnityEngine.UI;
#if FAIRYGUI_TMPRO
using OpenFairyTextComponent = TMPro.TextMeshProUGUI;
using OpenFairyInputComponent = TMPro.TMP_InputField;
#else
using OpenFairyTextComponent = OpenFairy.UGUI.TextField;
using OpenFairyInputComponent = OpenFairy.UGUI.TextInput;
#endif

namespace OpenFairy.UGUI.Tests
{
    public partial class ContentSizeTestButton : global::OpenFairy.UGUI.Button<ContentSizeTestButton.button>
    {
        public enum button
        {
            up,
            down,
        }
        public ref Controller<button> m_button => ref controller;

        protected internal override int GetControllerPage(int controller) => controller switch
        {
            0 => (int)this.m_button.page,
            _ => -1,
        };

        protected internal override bool IsControllerChanging(int controller) => controller switch
        {
            0 => this.m_button.changing,
            _ => false,
        };

        protected internal override void SetControllerPage(int controller, int page)
        {
            switch (controller)
            {
                case 0: this.m_button.page = (button)page; break;
            }
        }

        protected override void InitializeControllers()
        {
            this.m_button.Initialize();
        }
        public OpenFairyTextComponent m_title;

        protected override button? StatePage(VisualState state) => state switch
        {
            VisualState.Up => button.up,
            VisualState.Down => button.down,
            _ => null,
        };
[global::UnityEngine.SerializeReference] private global::OpenFairy.UGUI.GearText<button> _localizedGear1_0 = null;
[global::System.Serializable]
public sealed class LocalizedCaptionTexts
{
    public string Text;
    public string Selected;
    public string GearDefault1;
    public string Gear1_0;
}
[global::UnityEngine.SerializeField] private LocalizedCaptionTexts _LocalizedCaptionTexts = null;
public void ApplyLocalizedCaptionTexts(LocalizedCaptionTexts texts)
{
    _LocalizedCaptionTexts = texts;
    RefreshLocalizedCaptionText();
}
public void RefreshLocalizedCaptionText()
{
    title = _LocalizedCaptionTexts.Text;
    selectedTitle = _LocalizedCaptionTexts.Selected;
    _localizedGear1_0.defaultValue = _LocalizedCaptionTexts.GearDefault1;
    _localizedGear1_0.values[0] = _LocalizedCaptionTexts.Gear1_0;
    RefreshTitle();
    _localizedGear1_0.Apply(m_button.page);
}

[global::System.Serializable] public sealed class LocalizedTexts
{
public LocalizedCaptionTexts LocalizedCaption;
}
public void ApplyLocale(global::OpenFairy.UGUI.Localization.LocaleTable texts)
{
ApplyLocalizedCaptionTexts(texts.Tests_ContentSizeTestButton.LocalizedCaption);
}

    }
}
