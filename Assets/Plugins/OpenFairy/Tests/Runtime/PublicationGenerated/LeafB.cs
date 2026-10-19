using UnityEngine;
using UnityEngine.UI;
#if FAIRYGUI_TMPRO
using OpenFairyTextComponent = TMPro.TextMeshProUGUI;
using OpenFairyInputComponent = TMPro.TMP_InputField;
#else
using OpenFairyTextComponent = OpenFairy.UGUI.TextField;
using OpenFairyInputComponent = OpenFairy.UGUI.TextInput;
#endif

namespace OpenFairyPublicationFixtures.Publication
{
    public partial class LeafB : global::OpenFairy.UGUI.Component
    {
        public OpenFairyTextComponent m_Caption;
        public global::UnityEngine.UI.Image m_Picture;
[global::System.Serializable]
public sealed class LocalizedCaptionTexts
{
    public string Text;
}
[global::UnityEngine.SerializeField] private LocalizedCaptionTexts _LocalizedCaptionTexts = null;
public void ApplyLocalizedCaptionTexts(LocalizedCaptionTexts texts)
{
    _LocalizedCaptionTexts = texts;
    RefreshLocalizedCaptionText();
}
public void RefreshLocalizedCaptionText()
{
    m_Caption.text = _LocalizedCaptionTexts.Text;
}

[global::System.Serializable] public sealed class LocalizedTexts
{
public LocalizedCaptionTexts LocalizedCaption;
}
public void ApplyLocale(global::OpenFairyPublicationFixtures.Localization.LocaleTable texts)
{
ApplyLocalizedCaptionTexts(texts.Publication_LeafB.LocalizedCaption);
}

    }
}
