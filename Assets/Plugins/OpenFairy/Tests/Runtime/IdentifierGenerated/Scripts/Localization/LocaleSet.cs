namespace Example._class.Localization
{
public sealed class LocaleSet : global::UnityEngine.ScriptableObject
{
    public enum Language
    {
    }
    [global::UnityEngine.SerializeField] private global::Example._class.Localization.LocaleTable[] tables = new global::Example._class.Localization.LocaleTable[0];
    public global::Example._class.Localization.LocaleTable Get(Language language) => tables[(int)language];
}
}
