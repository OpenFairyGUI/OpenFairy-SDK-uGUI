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
    public partial class NativeTextInput : global::OpenFairy.UGUI.Label
    {
        public OpenFairyInputComponent m_title;
[global::UnityEngine.SerializeField] private OpenFairyTextComponent _localizedPrompt1 = null;
private void SetLocalizedCaptionInput(string value)
{
#if FAIRYGUI_TMPRO
m_title.SetTextWithoutNotify(value);
#else
m_title.text = value;
#endif
}
[global::System.Serializable]
public sealed class LocalizedCaptionTexts
{
    public string Text;
    public string Prompt;
}
[global::UnityEngine.SerializeField] private LocalizedCaptionTexts _LocalizedCaptionTexts = null;
public void ApplyLocalizedCaptionTexts(LocalizedCaptionTexts texts, bool updateInput = false)
{
    _LocalizedCaptionTexts = texts;
    RefreshLocalizedCaptionText(updateInput);
}
public void RefreshLocalizedCaptionText(bool updateInput = true)
{
    if (updateInput) SetLocalizedCaptionInput(_LocalizedCaptionTexts.Text);
    _localizedPrompt1.text = _LocalizedCaptionTexts.Prompt;
}

[global::System.Serializable] public sealed class LocalizedTexts
{
public LocalizedCaptionTexts LocalizedCaption;
}
public void ApplyLocale(global::OpenFairyTextFixtures.Localization.LocaleTable texts)
{
ApplyLocalizedCaptionTexts(texts.NativeTextFixture_NativeTextInput.LocalizedCaption);
}

    }
}
