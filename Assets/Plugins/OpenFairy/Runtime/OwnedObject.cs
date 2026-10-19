using UnityEngine;

namespace OpenFairy.UGUI
{
    // 编辑态预览也会创建隐藏资源，不能等待延迟销毁。
    internal static class OwnedObject
    {
        internal static void Destroy(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }
    }
}
