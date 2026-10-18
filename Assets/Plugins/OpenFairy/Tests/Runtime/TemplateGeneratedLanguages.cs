#if FAIRYGUI_TMPRO
namespace OpenFairy.UGUI.Tests
{
public sealed class TemplateGeneratedLanguages : UnityEngine.ScriptableObject
{
    public enum Language
    {
        English,
        French,
    }
    [UnityEngine.SerializeField] private TemplateGeneratedLocale[] tables = new TemplateGeneratedLocale[2];
    public TemplateGeneratedLocale Get(Language language) => tables[(int)language];
}
}
#endif
