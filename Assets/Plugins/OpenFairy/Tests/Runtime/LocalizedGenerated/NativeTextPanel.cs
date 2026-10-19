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
    public partial class NativeTextPanel : global::OpenFairy.UGUI.Component
    {
        public enum State
        {
            Short,
            Long,
        }
        public Controller<State> m_State;

        protected internal override int GetControllerPage(int controller) => controller switch
        {
            0 => (int)m_State.page,
            _ => -1,
        };

        protected internal override bool IsControllerChanging(int controller) => controller switch
        {
            0 => m_State.changing,
            _ => false,
        };

        protected internal override void SetControllerPage(int controller, int page)
        {
            switch (controller)
            {
                case 0: m_State.page = (State)page; break;
            }
        }

        protected override void InitializeControllers()
        {
            m_State.Initialize();
        }
        public OpenFairyTextComponent m_Notice;
        public global::OpenFairyTextFixtures.NativeTextFixture.NativeTextButton m_Main;
        public global::OpenFairyTextFixtures.NativeTextFixture.NativeTextButton m_Geared;
        public global::OpenFairyTextFixtures.NativeTextFixture.NativeTextInput m_Edit;
        public global::OpenFairyTextFixtures.NativeTextFixture.NativeTextCombo m_Choice;
        public global::OpenFairyTextFixtures.NativeTextFixture.NativeTextButton m_Property;
        public global::UnityEngine.RectTransform m_Items;
[global::UnityEngine.SerializeField] private global::OpenFairyTextFixtures.NativeTextFixture.NativeTextButton _localizedItem5 = null;
[global::UnityEngine.SerializeField] private global::OpenFairyTextFixtures.NativeTextFixture.NativeTextButton _localizedItem6 = null;
[global::UnityEngine.SerializeField] private global::OpenFairy.UGUI.ListSource _localizedList_items = null;
[global::UnityEngine.SerializeField] private global::OpenFairy.UGUI.Tooltip _localizedTooltip1 = null;
[global::UnityEngine.SerializeReference] private global::OpenFairy.UGUI.GearText<State> _localizedGear1_0 = null;
[global::System.Serializable]
public sealed class LocalizedNoticeTexts
{
    public global::OpenFairy.UGUI.TextTemplate Text;
    public global::OpenFairy.UGUI.TextTemplate Tooltip;
    public global::OpenFairy.UGUI.TextTemplate GearDefault1;
    public global::OpenFairy.UGUI.TextTemplate Gear1_0;
    public global::OpenFairy.UGUI.TextTemplate Gear1_2;
}
[global::UnityEngine.SerializeField] private LocalizedNoticeTexts _LocalizedNoticeTexts = null;
private readonly global::OpenFairy.UGUI.TextTemplateState _LocalizedNoticeVariables = new(1);
public enum LocalizedNoticeVariable
{
    count,
}
public void SetLocalizedNoticeVariable(LocalizedNoticeVariable variable, string value) => _LocalizedNoticeVariables.Set((int)variable, value);
public void ClearLocalizedNoticeVariable(LocalizedNoticeVariable variable) => _LocalizedNoticeVariables.Clear((int)variable);
public void ApplyLocalizedNoticeTexts(LocalizedNoticeTexts texts)
{
    _LocalizedNoticeTexts = texts;
    RefreshLocalizedNoticeText();
}
public void RefreshLocalizedNoticeText()
{
    m_Notice.text = _LocalizedNoticeVariables.Format(_LocalizedNoticeTexts.Text);
    _localizedTooltip1.text = _LocalizedNoticeVariables.Format(_LocalizedNoticeTexts.Tooltip);
    _localizedGear1_0.defaultValue = _LocalizedNoticeVariables.Format(_LocalizedNoticeTexts.GearDefault1);
    _localizedGear1_0.values[0] = _LocalizedNoticeVariables.Format(_LocalizedNoticeTexts.Gear1_0);
    _localizedGear1_0.values[1] = _LocalizedNoticeVariables.Format(_LocalizedNoticeTexts.Gear1_2);
    _localizedTooltip1.RefreshText();
    _localizedGear1_0.Apply(m_State.page);
}

[global::UnityEngine.SerializeReference] private global::OpenFairy.UGUI.GearText<State> _localizedGear2_0 = null;
[global::System.Serializable]
public sealed class LocalizedGearedTexts
{
    public string Text;
    public string GearDefault1;
    public string Gear1_0;
    public string Gear1_1;
}
[global::UnityEngine.SerializeField] private LocalizedGearedTexts _LocalizedGearedTexts = null;
public void ApplyLocalizedGearedTexts(LocalizedGearedTexts texts)
{
    _LocalizedGearedTexts = texts;
    RefreshLocalizedGearedText();
}
public void RefreshLocalizedGearedText()
{
    m_Geared.title = _LocalizedGearedTexts.Text;
    _localizedGear2_0.defaultValue = _LocalizedGearedTexts.GearDefault1;
    _localizedGear2_0.values[0] = _LocalizedGearedTexts.Gear1_0;
    _localizedGear2_0.values[1] = _LocalizedGearedTexts.Gear1_1;
    _localizedGear2_0.Apply(m_State.page);
}

[global::System.Serializable] public sealed class LocalizedTexts
{
public LocalizedNoticeTexts LocalizedNotice;
public LocalizedGearedTexts LocalizedGeared;
public global::OpenFairyTextFixtures.NativeTextFixture.NativeTextButton.LocalizedCaptionTexts Override0;
public global::OpenFairyTextFixtures.NativeTextFixture.NativeTextButton.LocalizedCaptionTexts Override1;
public global::OpenFairyTextFixtures.NativeTextFixture.NativeTextInput.LocalizedCaptionTexts Override2;
public global::OpenFairyTextFixtures.NativeTextFixture.NativeTextCombo.LocalizedCaptionTexts Override3;
public global::OpenFairyTextFixtures.NativeTextFixture.NativeTextCombo.LocalizedItemsTexts Override4;
public global::OpenFairyTextFixtures.NativeTextFixture.NativeTextButton.LocalizedCaptionTexts Override5;
public global::OpenFairyTextFixtures.NativeTextFixture.NativeTextButton.LocalizedCaptionTexts Override6;
public global::OpenFairyTextFixtures.NativeTextFixture.NativeTextButton.LocalizedCaptionTexts Override7;
}
public void ApplyLocale(global::OpenFairyTextFixtures.Localization.LocaleTable texts)
{
m_Main.ApplyLocale(texts);
m_Geared.ApplyLocale(texts);
m_Edit.ApplyLocale(texts);
m_Choice.ApplyLocale(texts);
m_Property.ApplyLocale(texts);
if (_localizedList_items.version == 0 && _localizedItem5 != null) _localizedItem5.ApplyLocale(texts);
if (_localizedList_items.version == 0 && _localizedItem6 != null) _localizedItem6.ApplyLocale(texts);
m_Main.ApplyLocalizedCaptionTexts(texts.NativeTextFixture_NativeTextPanel.Override0);
m_Geared.ApplyLocalizedCaptionTexts(texts.NativeTextFixture_NativeTextPanel.Override1);
m_Edit.ApplyLocalizedCaptionTexts(texts.NativeTextFixture_NativeTextPanel.Override2);
m_Choice.ApplyLocalizedCaptionTexts(texts.NativeTextFixture_NativeTextPanel.Override3);
m_Choice.ApplyLocalizedItemsTexts(texts.NativeTextFixture_NativeTextPanel.Override4);
m_Property.ApplyLocalizedCaptionTexts(texts.NativeTextFixture_NativeTextPanel.Override5);
if (_localizedList_items.version == 0 && _localizedItem5 != null) _localizedItem5.ApplyLocalizedCaptionTexts(texts.NativeTextFixture_NativeTextPanel.Override6);
if (_localizedList_items.version == 0 && _localizedItem6 != null) _localizedItem6.ApplyLocalizedCaptionTexts(texts.NativeTextFixture_NativeTextPanel.Override7);
ApplyLocalizedNoticeTexts(texts.NativeTextFixture_NativeTextPanel.LocalizedNotice);
ApplyLocalizedGearedTexts(texts.NativeTextFixture_NativeTextPanel.LocalizedGeared);
}

    }
}
