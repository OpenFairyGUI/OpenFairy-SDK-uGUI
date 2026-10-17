using UnityEngine;
using UnityEngine.UI;

#if FAIRYGUI_TMPRO
using TextComponent = TMPro.TextMeshProUGUI;
using InputComponent = TMPro.TMP_InputField;
#else
using TextComponent = OpenFairy.UGUI.TextField;
using InputComponent = OpenFairy.UGUI.TextInput;
#endif

namespace OpenFairy.UGUI
{
    // 复刻 FairyGUI GLabel：带标题文本（可选图标）的组件，供用户代码 label.title 收发。
    public class Label : Component
    {
        [SerializeField] internal TextComponent titleText; // 烘焙接线
        [SerializeField] internal InputComponent titleInput;
#if FAIRYGUI_TMPRO
        [SerializeField] internal InputEditing titleEditing;
#endif
        [SerializeField] internal Loader iconLoader;

        public string title
        {
            get => titleInput != null ? titleInput.text : titleText != null ? titleText.text : null;
            set
            {
                if (titleInput != null)
#if FAIRYGUI_TMPRO
                {
                    if (titleEditing != null) titleEditing.SetTextWithoutNotify(value);
                    else titleInput.SetTextWithoutNotify(value);
                }
#else
                    titleInput.text = value;
#endif
                else if (titleText != null)
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
