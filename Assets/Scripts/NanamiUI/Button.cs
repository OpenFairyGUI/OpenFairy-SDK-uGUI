using UnityEditor;
using UnityEngine.UIElements;

namespace NanamiUI
{
    [UxmlElement]
    public partial class Button : VisualElement
    {
        private string _src;
        private string _title;

        [UxmlAttribute]
        public string src
        {
            get => _src;
            set
            {
                _src = value;
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/" + _src).CloneTree(this);
                ApplyTitle();
            }
        }

        [UxmlAttribute]
        public string title
        {
            get => _title;
            set
            {
                _title = value;
                ApplyTitle();
            }
        }

        private void ApplyTitle()
        {
            if (_title == null)
                return;

            var label = this.Q<Text>("title");
            if (label != null)
                label.text = _title;
        }
    }
}
