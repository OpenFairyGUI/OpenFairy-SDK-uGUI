namespace OpenFairyPublicationFixtures.Localization
{
public sealed class LocaleSet : global::UnityEngine.ScriptableObject
{
    public enum Language
    {
    }
    [global::UnityEngine.SerializeField] private global::OpenFairyPublicationFixtures.Localization.LocaleTable[] tables = new global::OpenFairyPublicationFixtures.Localization.LocaleTable[0];
    public global::OpenFairyPublicationFixtures.Localization.LocaleTable Get(Language language) => tables[(int)language];
}
}
