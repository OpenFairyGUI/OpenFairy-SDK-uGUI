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
    public partial class NativeTextCombo : OpenFairy.UGUI.ComboBox<NativeTextCombo.button>
    {
        public enum button
        {
            up,
            over,
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
    public string Text;
    public string Selected;
}
[UnityEngine.SerializeField] private LocalizedCaptionTexts _LocalizedCaptionTexts = null;
public void ApplyLocalizedCaptionTexts(LocalizedCaptionTexts texts)
{
    _LocalizedCaptionTexts = texts;
    RefreshLocalizedCaptionText();
}
public void RefreshLocalizedCaptionText()
{
    title = _LocalizedCaptionTexts.Text;
    selectedTitle = _LocalizedCaptionTexts.Selected;
    RefreshTitle();
}

[System.Serializable] public sealed class LocalizedItemsTexts { public string[] Items; }
[UnityEngine.SerializeField] private LocalizedItemsTexts _LocalizedItemsTexts = null;
public void ApplyLocalizedItemsTexts(LocalizedItemsTexts texts) { _LocalizedItemsTexts = texts; RefreshLocalizedItemsText(); }
public void RefreshLocalizedItemsText() { items = _LocalizedItemsTexts.Items; if (selectedIndex >= 0 && selectedIndex < items.Length) text = items[selectedIndex]; }
[System.Serializable] public sealed class LocalizedTexts
{
public LocalizedCaptionTexts LocalizedCaption;
public LocalizedItemsTexts LocalizedItems;
}
public void ApplyLocale(global::OpenFairyTextFixtures.Localization.LocaleTable texts)
{
ApplyLocalizedCaptionTexts(texts.NativeTextFixture_NativeTextCombo.LocalizedCaption);
ApplyLocalizedItemsTexts(texts.NativeTextFixture_NativeTextCombo.LocalizedItems);
}

    }
}
