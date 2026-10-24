using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public class PooledObjectLifetimeTests
    {
        internal sealed class Fixture : IDisposable
        {
            internal readonly GameObject owner;
            internal readonly ObjectPool<GameObject> pool;
            internal readonly PopupMenu menu;
            internal GameObject popupContent;

            internal Fixture(bool popup)
            {
                owner = new GameObject("Pool fixture", typeof(RectTransform));
                var item = new GameObject("Item template", typeof(RectTransform));
                item.transform.SetParent(owner.transform, false);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                if (popup)
                {
                    var content = new GameObject("Menu template", typeof(RectTransform));
                    content.transform.SetParent(owner.transform, false);
                    var list = new GameObject("list", typeof(RectTransform), typeof(ListSource));
                    list.transform.SetParent(content.transform, false);
                    list.GetComponent<ListSource>().content = (RectTransform)list.transform;
                    var authored = new GameObject("Authored item", typeof(RectTransform));
                    authored.transform.SetParent(list.transform, false);
                    menu = new PopupMenu(content, item);
                    popupContent = menu.ContentPane.gameObject;
                    pool = (ObjectPool<GameObject>)typeof(PopupMenu).GetField("_itemPool", flags).GetValue(menu);
                }
                else
                {
                    var list = new GameObject("List", typeof(RectTransform), typeof(ListSource));
                    list.transform.SetParent(owner.transform, false);
                    var source = list.GetComponent<ListSource>();
                    source.itemPrefab = item;
                    source.content = (RectTransform)list.transform;
                    typeof(ListSource).GetMethod("EnsurePool", flags).Invoke(source, null);
                    pool = (ObjectPool<GameObject>)typeof(ListSource).GetField("_pool", flags).GetValue(source);
                }
            }

            internal GameObject CacheItem()
            {
                var item = pool.Get();
                pool.Release(item);
                return item;
            }

            public void Dispose()
            {
                // Fixture objects are not assets. Avoid calling the method under test during cleanup.
                pool.Clear();
                if (popupContent != null) Object.DestroyImmediate(popupContent);
                if (owner != null) Object.DestroyImmediate(owner);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClearDestroysLiveCachedItemsInEditMode(bool popup)
        {
            Assert.That(Application.isPlaying, Is.False);
            using var fixture = new Fixture(popup);
            var item = fixture.CacheItem();
            fixture.pool.Clear();
            Assert.That(item == null, Is.True);
            Assert.That(fixture.pool.CountInactive, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClearAcceptsItemsAlreadyDestroyedBySceneTeardown(bool popup)
        {
            Assert.That(Application.isPlaying, Is.False);
            using var fixture = new Fixture(popup);
            var item = fixture.CacheItem();
            Object.DestroyImmediate(item);
            fixture.pool.Clear();
            Assert.That(fixture.pool.CountInactive, Is.Zero);
        }

        [Test]
        public void PopupDisposeDestroysOwnedContentInEditMode()
        {
            Assert.That(Application.isPlaying, Is.False);
            using var fixture = new Fixture(true);
            var content = fixture.popupContent;
            fixture.menu.Dispose();
            Assert.That(content == null, Is.True);
            Assert.That(fixture.pool.CountInactive, Is.Zero);
        }
    }
}
