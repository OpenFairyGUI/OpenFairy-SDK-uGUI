using UnityEngine;
using UnityEngine.Pool;

namespace NanamiUI
{
    // list 节点的动态实例化描述（由 Migrate.BuildList 烘焙）：运行时据此从 itemPrefab 建项。
    // 让 PopupMenu/ComboBox 下拉/Window1 列表/Grid 等空/动态列表无需显式传 prefab。
    public sealed class ListSource : MonoBehaviour
    {
        internal const string PoolName = "__listPool";

        public GameObject itemPrefab;
        public Vector2 itemSize;
        public float lineGap;
        public float colGap;
        public string layout;

        private ObjectPool<GameObject> _pool;
        private RectTransform _poolRoot;

        public GameObject GetItem(RectTransform parent)
        {
            EnsurePool();
            var item = _pool.Get();
            var rt = (RectTransform)item.transform;
            rt.SetParent(parent, false);
            ResetButtons(item);
            item.SetActive(true);
            return item;
        }

        public void ReleaseItem(GameObject item)
        {
            EnsurePool();
            _pool.Release(item);
        }

        public void PlacePoolRootLast()
        {
            if (_poolRoot != null)
                _poolRoot.SetAsLastSibling();
        }

        private void EnsurePool()
        {
            _pool ??= new ObjectPool<GameObject>(
                () =>
                {
                    var item = Instantiate(itemPrefab, PoolRoot, false);
                    item.SetActive(false);
                    return item;
                },
                null,
                item =>
                {
                    ResetButtons(item);
                    item.transform.SetParent(PoolRoot, false);
                    item.SetActive(false);
                },
                Destroy,
                false);
        }

        private RectTransform PoolRoot
        {
            get
            {
                if (_poolRoot == null)
                {
                    var go = new GameObject(PoolName, typeof(RectTransform));
                    _poolRoot = (RectTransform)go.transform;
                    _poolRoot.SetParent(transform, false);
                    _poolRoot.anchorMin = _poolRoot.anchorMax = _poolRoot.pivot = new Vector2(0, 1);
                    go.SetActive(false);
                }
                return _poolRoot;
            }
        }

        private static void ResetButtons(GameObject item)
        {
            foreach (var button in item.GetComponentsInChildren<ButtonBase>(true))
            {
                button.onClick.RemoveAllListeners();
                button.SetGrayed(false);
                button.Selected = false;
                button.changeStateOnClick = true;
            }
        }

        private void OnDestroy() => _pool?.Clear();
    }
}
