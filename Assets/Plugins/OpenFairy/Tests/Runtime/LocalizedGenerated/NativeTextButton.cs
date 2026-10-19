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
    public partial class NativeTextButton : global::OpenFairy.UGUI.Button<NativeTextButton.button>
    {
        protected override button? StatePage(VisualState state) => state switch
        {
            VisualState.Up => button.up,
            VisualState.Over => button.over,
            VisualState.Down => button.down,
            _ => null,
        };
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
        public global::UnityEngine.RectTransform m_FirstGroup;
        public global::UnityEngine.RectTransform m_SecondGroup;
[global::System.Serializable]
public sealed class LocalizedCaptionTexts
{
    public global::OpenFairy.UGUI.TextTemplate Text;
    public global::OpenFairy.UGUI.TextTemplate Selected;
}
[global::UnityEngine.SerializeField] private LocalizedCaptionTexts _LocalizedCaptionTexts = null;
private readonly global::OpenFairy.UGUI.TextTemplateState _LocalizedCaptionVariables = new(2);
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

[global::UnityEngine.SerializeField] private OpenFairyTextComponent _localizedText2 = null;
[global::System.Serializable]
public sealed class LocalizedCaption_2Texts
{
    public string Text;
}
[global::UnityEngine.SerializeField] private LocalizedCaption_2Texts _LocalizedCaption_2Texts = null;
public void ApplyLocalizedCaption_2Texts(LocalizedCaption_2Texts texts)
{
    _LocalizedCaption_2Texts = texts;
    RefreshLocalizedCaption_2Text();
}
public void RefreshLocalizedCaption_2Text()
{
    _localizedText2.text = _LocalizedCaption_2Texts.Text;
}

[global::UnityEngine.SerializeField] private OpenFairyTextComponent _localizedText3 = null;
[global::System.Serializable]
public sealed class LocalizedCaption_3Texts
{
    public string Text;
}
[global::UnityEngine.SerializeField] private LocalizedCaption_3Texts _LocalizedCaption_3Texts = null;
public void ApplyLocalizedCaption_3Texts(LocalizedCaption_3Texts texts)
{
    _LocalizedCaption_3Texts = texts;
    RefreshLocalizedCaption_3Text();
}
public void RefreshLocalizedCaption_3Text()
{
    _localizedText3.text = _LocalizedCaption_3Texts.Text;
}

[global::UnityEngine.SerializeField] private OpenFairyTextComponent _localizedText4 = null;
[global::System.Serializable]
public sealed class LocalizedCaption_4Texts
{
    public string Text;
}
[global::UnityEngine.SerializeField] private LocalizedCaption_4Texts _LocalizedCaption_4Texts = null;
public void ApplyLocalizedCaption_4Texts(LocalizedCaption_4Texts texts)
{
    _LocalizedCaption_4Texts = texts;
    RefreshLocalizedCaption_4Text();
}
public void RefreshLocalizedCaption_4Text()
{
    _localizedText4.text = _LocalizedCaption_4Texts.Text;
}

[global::UnityEngine.SerializeField] private OpenFairyTextComponent _localizedText5 = null;
[global::System.Serializable]
public sealed class LocalizedCaption_5Texts
{
    public string Text;
}
[global::UnityEngine.SerializeField] private LocalizedCaption_5Texts _LocalizedCaption_5Texts = null;
public void ApplyLocalizedCaption_5Texts(LocalizedCaption_5Texts texts)
{
    _LocalizedCaption_5Texts = texts;
    RefreshLocalizedCaption_5Text();
}
public void RefreshLocalizedCaption_5Text()
{
    _localizedText5.text = _LocalizedCaption_5Texts.Text;
}

[global::System.Serializable] public sealed class LocalizedTexts
{
public LocalizedCaptionTexts LocalizedCaption;
public LocalizedCaption_2Texts LocalizedCaption_2;
public LocalizedCaption_3Texts LocalizedCaption_3;
public LocalizedCaption_4Texts LocalizedCaption_4;
public LocalizedCaption_5Texts LocalizedCaption_5;
}
public void ApplyLocale(global::OpenFairyTextFixtures.Localization.LocaleTable texts)
{
ApplyLocalizedCaptionTexts(texts.NativeTextFixture_NativeTextButton.LocalizedCaption);
ApplyLocalizedCaption_2Texts(texts.NativeTextFixture_NativeTextButton.LocalizedCaption_2);
ApplyLocalizedCaption_3Texts(texts.NativeTextFixture_NativeTextButton.LocalizedCaption_3);
ApplyLocalizedCaption_4Texts(texts.NativeTextFixture_NativeTextButton.LocalizedCaption_4);
ApplyLocalizedCaption_5Texts(texts.NativeTextFixture_NativeTextButton.LocalizedCaption_5);
}

    }
}
