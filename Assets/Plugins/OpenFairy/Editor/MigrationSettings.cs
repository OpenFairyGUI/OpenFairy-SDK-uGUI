using System;
using UnityEditor;

namespace OpenFairy.UGUI.Editor
{
    [FilePath("ProjectSettings/OpenFairySettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class MigrationSettings : ScriptableSingleton<MigrationSettings>
    {
        public string sourceDirectory = "UIProject";
        public string outputDirectory = "Assets/UIProject";
        public string[] dependencyDirectories = Array.Empty<string>();

        public void Save() => Save(true);
    }
}
