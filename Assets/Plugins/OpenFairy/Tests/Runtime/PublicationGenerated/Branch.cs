using UnityEngine;
using UnityEngine.UI;

namespace OpenFairyPublicationFixtures.Publication
{
    public partial class Branch : global::OpenFairy.UGUI.Component
    {
        public global::OpenFairyPublicationFixtures.Publication.LeafA m_First;
        public global::OpenFairyPublicationFixtures.Publication.LeafB m_Second;
[global::System.Serializable] public sealed class LocalizedTexts
{
}
public void ApplyLocale(global::OpenFairyPublicationFixtures.Localization.LocaleTable texts)
{
m_First.ApplyLocale(texts);
m_Second.ApplyLocale(texts);
}

    }
}
