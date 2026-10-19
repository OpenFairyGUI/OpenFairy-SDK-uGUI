#if FAIRYGUI_TMPRO
using OpenFairy.UGUI;
namespace OpenFairy.UGUI.Tests
{
public sealed class TemplateGeneratedButton : Button<TemplateGeneratedButton.Mode>
{
        protected override Mode? StatePage(VisualState state) => state switch
        {
            VisualState.Up => Mode.up,
            VisualState.Down => Mode.down,
            VisualState.Over => Mode.over,
            VisualState.SelectedOver => Mode.selectedOver,
            _ => null,
        };
public enum Mode { up, down, over, selectedOver }
[System.Serializable]
public sealed class ButtonTexts
{
    public OpenFairy.UGUI.TextTemplate Title;
    public OpenFairy.UGUI.TextTemplate Selected;
}
[UnityEngine.SerializeField] private ButtonTexts _ButtonTexts;
private readonly OpenFairy.UGUI.TextTemplateState _ButtonVariables = new(3);
public enum ButtonVariable
{
    name,
    name_key,
    name_key_2,
}
public void SetButtonVariable(ButtonVariable variable, string value) => _ButtonVariables.Set((int)variable, value);
public void ClearButtonVariable(ButtonVariable variable) => _ButtonVariables.Clear((int)variable);
public void ApplyButtonTexts(ButtonTexts texts)
{
    _ButtonTexts = texts;
    RefreshButtonText();
}
public void RefreshButtonText()
{
    title = _ButtonVariables.Format(_ButtonTexts.Title);
    selectedTitle = _ButtonVariables.Format(_ButtonTexts.Selected);
    RefreshTitle();
}
}
}
#endif
