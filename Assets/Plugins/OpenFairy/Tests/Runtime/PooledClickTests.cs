using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenFairy.UGUI.Tests
{
    public sealed class PooledClickTests
    {
        private GameObject _root;
        private GameObject _template;

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_template);
        }

        private ListSource Source(ListLayoutType layout, bool virtualized)
        {
            _root = new GameObject("List", typeof(RectTransform), typeof(ListSource), typeof(ListSelection));
            _template = new GameObject("Item", typeof(RectTransform), typeof(PooledClickTestButton));
            _template.SetActive(false);
            _template.GetComponent<PooledClickTestButton>().mode = ButtonMode.Radio;
            var source = _root.GetComponent<ListSource>();
            source.content = (RectTransform)_root.transform;
            source.content.sizeDelta = new Vector2(200, 200);
            source.itemPrefab = _template;
            source.itemSize = new Vector2(40, 20);
            source.layout = layout;
            if (virtualized)
            {
                var viewport = (RectTransform)new GameObject("viewport", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D)).transform;
                viewport.SetParent(_root.transform, false);
                viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0, 1);
                viewport.sizeDelta = new Vector2(200, 200);
                source.SetVirtual();
            }
            return source;
        }

        [UnityTest]
        public IEnumerator Refilled_items_preserve_their_Awake_click_handlers_in_every_layout()
        {
            foreach (var virtualized in new[] { false, true })
            foreach (var layout in new[] { ListLayoutType.SingleColumn, ListLayoutType.SingleRow, ListLayoutType.FlowHorizontal, ListLayoutType.FlowVertical })
            {
                var source = Source(layout, virtualized);
                yield return null;
                foreach (var count in new[] { 3, 1, 4, 0, 2 })
                {
                    source.Fill(count, (_, _) => { });
                    for (var i = 0; i < count; i++)
                    {
                        var button = source.GetItemAt(i).GetComponent<PooledClickTestButton>();
                        var before = button.clicks;
                        button.onClick.Invoke();
                        Assert.That(button.clicks, Is.EqualTo(before + 1), layout + " item " + i);
                    }
                }
                Cleanup();
            }
        }

        [UnityTest]
        public IEnumerator A_click_that_refills_the_list_cancels_its_old_selection_binding()
        {
            foreach (var virtualized in new[] { false, true })
            foreach (var count in new[] { 0, 1, 3 })
            {
                var source = Source(ListLayoutType.SingleColumn, virtualized);
                yield return null;
                source.Fill(2, (_, _) => { });
                var selection = _root.GetComponent<ListSelection>();
                var selected = 0;
                selection.onClickItem.AddListener(_ => selected++);
                var button = source.GetItemAt(0).GetComponent<PooledClickTestButton>();
                button.clicked = () => source.Fill(count, (item, _) => item.GetComponent<PooledClickTestButton>().clicked = null);
                Assert.DoesNotThrow(() => button.onClick.Invoke());
                Assert.That(selected, Is.Zero, "An in-flight callback must not select a recycled item.");
                Assert.That(source.count, Is.EqualTo(count));
                if (count > 0)
                {
                    source.GetItemAt(0).GetComponent<PooledClickTestButton>().onClick.Invoke();
                    Assert.That(selected, Is.EqualTo(1));
                }
                Cleanup();
            }
        }
    }
}
