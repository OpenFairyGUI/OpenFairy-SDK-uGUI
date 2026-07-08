using UnityEngine.Events;
using UnityEngine.UI;

namespace NanamiUI
{
    // 复刻 FairyGUI GTextInput：可编辑文本，基于 uGUI InputField。显示用 NanamiUI.Text，prompt 用 placeholder Text。
    // 已知限制：InputField 的光标定位依赖 textComponent 的 TextGenerator，而 NanamiUI.Text 走自绘布局，故光标像素位置可能略偏；
    // 文本读写/占位切换/onChanged 均正常。
    public class InputText : Component
    {
        public InputField field;

        public string text
        {
            get => field.text;
            set => field.text = value;
        }

        public UnityEvent<string> onChanged => field.onValueChanged;
    }
}
