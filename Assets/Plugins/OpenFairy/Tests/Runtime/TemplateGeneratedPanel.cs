#if FAIRYGUI_TMPRO
using OpenFairy.UGUI;
namespace OpenFairy.UGUI.Tests
{
public sealed class TemplateGeneratedPanel : OpenFairy.UGUI.Component
{
public enum Page { First, Other }
public TMPro.TextMeshProUGUI caption;
public TemplateGeneratedButton button;
public Label label;
public GearText<Page> gear;
public Controller<Page> mode;
[System.Serializable]
public sealed class CaptionTexts
{
    public OpenFairy.UGUI.TextTemplate Base;
    public OpenFairy.UGUI.TextTemplate Page;
    public OpenFairy.UGUI.TextTemplate Default;
}
[UnityEngine.SerializeField] private CaptionTexts _CaptionTexts;
private readonly OpenFairy.UGUI.TextTemplateState _CaptionVariables = new(1);
public enum CaptionVariable
{
    count,
}
public void SetCaptionVariable(CaptionVariable variable, string value) => _CaptionVariables.Set((int)variable, value);
public void ClearCaptionVariable(CaptionVariable variable) => _CaptionVariables.Clear((int)variable);
public void ApplyCaptionTexts(CaptionTexts texts)
{
    _CaptionTexts = texts;
    RefreshCaptionText();
}
public void RefreshCaptionText()
{
    caption.text = _CaptionVariables.Format(_CaptionTexts.Base);
    gear.values[0] = _CaptionVariables.Format(_CaptionTexts.Page);
    gear.defaultValue = _CaptionVariables.Format(_CaptionTexts.Default);
    gear.Apply(mode.page);
}
[System.Serializable]
public sealed class LabelTexts
{
    public string Title;
}
[UnityEngine.SerializeField] private LabelTexts _LabelTexts;
public void ApplyLabelTexts(LabelTexts texts)
{
    _LabelTexts = texts;
    RefreshLabelText();
}
public void RefreshLabelText()
{
    label.title = _LabelTexts.Title;
}
public void ApplyLocale(TemplateGeneratedLocale texts)
{
    ApplyCaptionTexts(texts.Caption);
    button.ApplyButtonTexts(texts.Button);
}
}
}
#endif
