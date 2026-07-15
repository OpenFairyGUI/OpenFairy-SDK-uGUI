using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    // 只在选中态按 Enter 时经 EventSystem 派发（不含失焦），复刻 GTextInput onSubmit 仅回车触发、且仅单行。
    // 由 Migrate 挂在输入框上并烘焙进 prefab，故独立成同名文件以便序列化。
    public sealed class InputSubmit : UIBehaviour, ISubmitHandler
    {
        public InputField field;
        public UnityEvent<string> onSubmit = new();

        public void OnSubmit(BaseEventData eventData)
        {
            if (field != null && field.lineType == InputField.LineType.SingleLine)
                onSubmit.Invoke(field.text);
        }
    }
}
