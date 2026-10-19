using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Page = OpenFairy.UGUI.Tests.SelectionControllerTestPanel.Page;

namespace OpenFairy.UGUI.Tests
{
    public class SelectionControllerTests
    {
        private readonly List<GameObject> _objects = new();

        private (SelectionControllerTestPanel Panel, ListSelection Selection) Create(int count = 3)
        {
            var root = new GameObject("Panel", typeof(RectTransform));
            _objects.Add(root);
            root.SetActive(false);
            var panel = root.AddComponent<SelectionControllerTestPanel>();
            var items = new GameObject("Items", typeof(RectTransform));
            items.transform.SetParent(root.transform, false);
            panel.m_Items = (RectTransform)items.transform;
            var selection = items.AddComponent<ListSelection>();
            selection.selectionOwner = panel;
            selection.selectionController = 0;
            selection.selectionPageCount = 3;
            for (var i = 0; i < count; i++)
            {
                var item = new GameObject("Item", typeof(RectTransform));
                item.transform.SetParent(items.transform, false);
                item.AddComponent<SoundImportButton>().mode = ButtonMode.Check;
            }
            panel.m_Page.gears = new Gear<Page>[] { new GearListSelection<Page> {
                target = items, selection = selection, pages = new[] { Page.First, Page.Second, Page.Third },
            } };
            panel.m_Page.Bake(Page.Third);
            root.SetActive(true);
            return (panel, selection);
        }

        private static void Click(ListSelection selection, int index)
        {
            var item = selection.transform.GetChild(index).GetComponent<SoundImportButton>();
            item.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects) if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        [UnityTest]
        public IEnumerator Baked_page_programmatic_changes_rebind_and_reenable_keep_one_selection()
        {
            var (panel, selection) = Create();
            yield return null;
            Assert.That(selection.selectedIndex, Is.EqualTo(2));
            panel.m_Page.page = Page.First;
            Assert.That(selection.selectedIndex, Is.Zero);
            selection.Rebind();
            Assert.That(selection.selectedIndex, Is.Zero);
            panel.gameObject.SetActive(false);
            panel.m_Page.page = Page.Second;
            panel.gameObject.SetActive(true);
            yield return null;
            Assert.That(selection.selectedIndex, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Click_and_selectedIndex_update_the_controller_once_but_clear_and_extra_items_do_not()
        {
            var (panel, selection) = Create(5);
            yield return null;
            var changed = 0;
            var clicks = 0;
            panel.m_Page.onChanged += () => changed++;
            selection.onClickItem.AddListener(_ => clicks++);
            Click(selection, 1);
            Assert.That(panel.m_Page.page, Is.EqualTo(Page.Second));
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(clicks, Is.EqualTo(1));
            selection.selectedIndex = 0;
            Assert.That(panel.m_Page.page, Is.EqualTo(Page.First));
            Assert.That(changed, Is.EqualTo(2));
            selection.ClearSelection();
            Assert.That(selection.selectedIndex, Is.EqualTo(-1));
            Assert.That(panel.m_Page.page, Is.EqualTo(Page.First));
            Click(selection, 4);
            Assert.That(selection.selectedIndex, Is.EqualTo(4));
            Assert.That(panel.m_Page.page, Is.EqualTo(Page.First));
            Assert.That(changed, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Outgoing_controller_change_does_not_collapse_multiple_selection_and_unselect_keeps_the_page()
        {
            var (panel, selection) = Create();
            yield return null;
            selection.selectionMode = ListSelectionMode.MultipleSingleClick;
            Click(selection, 1);
            CollectionAssert.AreEqual(new[] { 1, 2 }, selection.GetSelection());
            Assert.That(panel.m_Page.page, Is.EqualTo(Page.Second));
            Click(selection, 1);
            CollectionAssert.AreEqual(new[] { 2 }, selection.GetSelection());
            Assert.That(panel.m_Page.page, Is.EqualTo(Page.Second));
            panel.m_Page.page = Page.First;
            CollectionAssert.AreEqual(new[] { 0 }, selection.GetSelection());
        }

        [UnityTest]
        public IEnumerator Virtual_recycling_and_refill_apply_the_current_controller_page_to_logical_indices()
        {
            var (panel, selection) = Create(0);
            ((RectTransform)selection.transform).sizeDelta = new Vector2(100, 40);
            var viewport = (RectTransform)new GameObject("viewport", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D)).transform;
            viewport.SetParent(selection.transform, false);
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            var template = new GameObject("Template", typeof(RectTransform), typeof(SoundImportButton));
            _objects.Add(template);
            ((RectTransform)template.transform).sizeDelta = new Vector2(100, 20);
            template.GetComponent<SoundImportButton>().mode = ButtonMode.Check;
            template.SetActive(false);
            var source = selection.gameObject.AddComponent<ListSource>();
            source.itemPrefab = template;
            source.itemSize = new Vector2(100, 20);
            source.layout = ListLayoutType.SingleColumn;
            source.SetVirtual();
            source.Fill(12, (_, _) => { });
            yield return null;
            Assert.That(selection.selectedIndex, Is.EqualTo(2));
            source.ScrollToIndex(10);
            Assert.That(selection.selectedIndex, Is.EqualTo(2));
            source.ScrollToIndex(2);
            Assert.That(source.visibleItems[2].GetComponent<SoundImportButton>().selected, Is.True);
            selection.selectedIndex = 1;
            Assert.That(panel.m_Page.page, Is.EqualTo(Page.Second));
            source.Fill(1, (_, _) => { });
            Assert.That(selection.selectedIndex, Is.EqualTo(-1));
            Assert.That(panel.m_Page.page, Is.EqualTo(Page.Second));
            source.Fill(12, (_, _) => { });
            Assert.That(selection.selectedIndex, Is.EqualTo(1));
        }
    }
}
