using UnityEngine;
using UnityEngine.UI;

namespace OpenFairyPublicationFixtures.Publication
{
    public partial class Root : global::OpenFairy.UGUI.Component
    {
        public global::OpenFairyPublicationFixtures.Publication.Branch m_Body;
[global::System.Serializable] public sealed class LocalizedTexts
{
}
public void ApplyLocale(global::OpenFairyPublicationFixtures.Localization.LocaleTable texts)
{
m_Body.ApplyLocale(texts);
}

    }
}
