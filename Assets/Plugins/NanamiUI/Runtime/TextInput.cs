using UnityEngine.Events;
using UnityEngine.UI;

namespace NanamiUI
{
    // 复刻 FairyGUI GTextInput：可编辑文本，基于 uGUI InputField。显示用 NanamiUI.TextField，prompt 用 placeholder TextField。
    // 已知限制：InputField 的光标定位依赖 textComponent 的 TextGenerator，而 NanamiUI.TextField 走自绘布局，故光标像素位置可能略偏；
    // 文本读写/占位切换/onChanged 均正常。
    public class TextInput : Component
    {
        public InputField field;
        public InputSubmit submit; // 挂在 field 上的 Enter 提交中继（Migrate 烘焙）

        public string text
        {
            get => field.text;
            set => field.SetTextWithoutNotify(value); // 复刻 FairyGUI：程序化赋值不发 onChanged，避免回写自触发
        }

        public bool password
        {
            get => field.contentType == InputField.ContentType.Password;
            set => field.contentType = value ? InputField.ContentType.Password : InputField.ContentType.Standard;
        }

        public int maxLength
        {
            get => field.characterLimit;
            set => field.characterLimit = value;
        }

        // 复刻 FairyGUI editable=false：只读（仍可聚焦/选择/复制），而非整体禁用。
        public bool editable
        {
            get => !field.readOnly;
            set => field.readOnly = !value;
        }

        public UnityEvent<string> onChanged => field.onValueChanged;
        public UnityEvent<string> onSubmit => submit.onSubmit;
    }
}
