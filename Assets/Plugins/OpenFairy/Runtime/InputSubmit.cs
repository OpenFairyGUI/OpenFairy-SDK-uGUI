using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    // 原生 InputField 消费聚焦期间的导航事件；提交从其独立的 onSubmit 序列化接入，不把失焦当提交。
    // 由 Migrate 挂在输入框上并烘焙进 prefab，故独立成同名文件以便序列化。
    public sealed class InputSubmit : UIBehaviour
    {
        public InputField field;
        public UnityEvent<string> onSubmit = new();

        public void Submit(string text)
        {
            if (field.lineType == InputField.LineType.SingleLine)
                onSubmit.Invoke(text);
        }
    }
}
