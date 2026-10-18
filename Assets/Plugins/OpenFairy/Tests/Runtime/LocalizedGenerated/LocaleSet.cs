namespace OpenFairyTextFixtures.Localization
{
public sealed class LocaleSet : UnityEngine.ScriptableObject
{
    public enum Language
    {
        English,
        French,
    }
    [UnityEngine.SerializeField] private global::OpenFairyTextFixtures.Localization.LocaleTable[] tables = new global::OpenFairyTextFixtures.Localization.LocaleTable[2];
    public global::OpenFairyTextFixtures.Localization.LocaleTable Get(Language language) => tables[(int)language];
}
}
