using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI
{
    // 复刻 FairyGUI GLabel：带标题文本（可选图标）的组件，供用户代码 label.Title 收发。
    public class Label : Component
    {
        public Text titleText;
        public Loader iconLoader;

        public string Title
        {
            get => titleText != null ? titleText.text : null;
            set
            {
                if (titleText != null)
                    titleText.text = value;
            }
        }

        public Sprite Icon
        {
            get => iconLoader != null ? iconLoader.sprite : null;
            set
            {
                if (iconLoader == null)
                    return;
                iconLoader.sprite = value;
                iconLoader.enabled = value != null;
            }
        }
    }
}
