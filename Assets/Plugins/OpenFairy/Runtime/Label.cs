using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    // 复刻 FairyGUI GLabel：带标题文本（可选图标）的组件，供用户代码 label.title 收发。
    public class Label : Component
    {
        [SerializeField] internal TextField titleText; // 烘焙接线
        [SerializeField] internal Loader iconLoader;

        public string title
        {
            get => titleText != null ? titleText.text : null;
            set
            {
                if (titleText != null)
                    titleText.text = value;
            }
        }

        public Sprite icon
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
