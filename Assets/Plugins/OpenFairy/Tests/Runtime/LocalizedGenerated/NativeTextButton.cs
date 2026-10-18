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

namespace OpenFairyTextFixtures.NativeTextFixture
{
    public partial class NativeTextButton : OpenFairy.UGUI.Button<NativeTextButton.button>
    {
        public enum button
        {
            up,
            over,
            down,
            selectedOver,
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
        public OpenFairyTextComponent m_title;
[System.Serializable]
public sealed class LocalizedCaptionTexts
{
    public OpenFairy.UGUI.TextTemplate Text;
    public OpenFairy.UGUI.TextTemplate Selected;
}
[UnityEngine.SerializeField] private LocalizedCaptionTexts _LocalizedCaptionTexts = null;
private readonly OpenFairy.UGUI.TextTemplateState _LocalizedCaptionVariables = new(2);
public enum LocalizedCaptionVariable
{
    name,
    user,
}
public void SetLocalizedCaptionVariable(LocalizedCaptionVariable variable, string value) => _LocalizedCaptionVariables.Set((int)variable, value);
public void ClearLocalizedCaptionVariable(LocalizedCaptionVariable variable) => _LocalizedCaptionVariables.Clear((int)variable);
public void ApplyLocalizedCaptionTexts(LocalizedCaptionTexts texts)
{
    _LocalizedCaptionTexts = texts;
    RefreshLocalizedCaptionText();
}
public void RefreshLocalizedCaptionText()
{
    title = _LocalizedCaptionVariables.Format(_LocalizedCaptionTexts.Text);
    selectedTitle = _LocalizedCaptionVariables.Format(_LocalizedCaptionTexts.Selected);
    RefreshTitle();
}

[System.Serializable] public sealed class LocalizedTexts
{
public LocalizedCaptionTexts LocalizedCaption;
}
public void ApplyLocale(global::OpenFairyTextFixtures.Localization.LocaleTable texts)
{
ApplyLocalizedCaptionTexts(texts.NativeTextFixture_NativeTextButton.LocalizedCaption);
}

    }
}
