using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ZLinq;

namespace NanamiUI.Tests
{
    // 系统性转换完整性：枚举 Basics 里每个组件的源 XML，凡 extention 声明了交互面（Button/ComboBox/Slider/ProgressBar/Label）的，
    // 断言烘焙 prefab 根挂了能响应的对应运行时组件——而不是退化成不可交互的 Component。
    // 这是"每个被 FairyGUI 标为可交互的元素都真的可交互"的枚举式保证，防的正是"抽查一个 ComboBox 通过、其余 Dropdown 变体
    // 静默退化成 Component 点了没反应"这类系统性盲区（不是靠人肉挑代表元素测）。
    public class ConverterCompletenessTests
    {
        private static bool IsGeneric(Type type, Type openGeneric)
        {
            for (var t = type; t != null; t = t.BaseType)
                if (t.IsGenericType && t.GetGenericTypeDefinition() == openGeneric)
                    return true;
            return false;
        }

        [UnityTest]
        public IEnumerator Every_interactive_component_bakes_a_working_runtime_face()
        {
#if UNITY_EDITOR
            var xmlRoot = Path.Combine(Directory.GetCurrentDirectory(), "UIProject/assets/Basics");
            Assert.IsTrue(Directory.Exists(xmlRoot), "找不到 Basics 源 XML 目录");
            var failures = new List<string>();
            var checkedCount = 0;

            foreach (var xmlPath in Directory.GetFiles(xmlRoot, "*.xml", SearchOption.AllDirectories))
            {
                XDocument doc;
                try { doc = XDocument.Load(xmlPath); }
                catch { continue; }
                var ext = doc.Root?.Attribute("extention")?.Value;
                if (ext == null)
                    continue;

                var rel = xmlPath.Replace('\\', '/');
                var idx = rel.IndexOf("/UIProject/assets/", StringComparison.Ordinal);
                var sub = rel[(idx + "/UIProject/assets/".Length)..]; // 例 "Basics/components/Dropdown.xml"
                var assetPath = "Assets/UIProject/Assets/" + sub.Replace(".xml", ".prefab");
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                    continue; // 未导出/无 prefab（依赖闭包之外）

                var comp = prefab.GetComponents<NanamiUI.Component>().AsValueEnumerable().FirstOrDefault();
                var type = comp?.GetType();
                var ok = ext switch
                {
                    "ComboBox" => type != null && IsGeneric(type, typeof(ComboBox<>)),
                    "Button" => comp is ButtonBase,
                    "Slider" => comp is Slider,
                    "ProgressBar" => comp is ProgressBar,
                    "Label" => comp is Label,
                    _ => true,
                };
                checkedCount++;
                if (!ok)
                    failures.Add($"{sub}: extention={ext} 但烘焙成 {(comp == null ? "无 NanamiUI 组件" : type.Name)}（该元素点了没反应）");
            }

            Assert.Greater(checkedCount, 0, "应至少扫到若干带交互 extention 的组件");
            Assert.IsEmpty(failures, "以下组件声明了交互面却退化成不可交互类型：\n" + string.Join("\n", failures));
#endif
            yield return null;
        }

        [Test]
        public void ComboBox_item_values_parse_as_strings()
        {
#if UNITY_EDITOR
            var path = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "NanamiUIComboValueTest.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, @"
<component size=""10,10"" extention=""ComboBox"">
  <ComboBox>
    <item title=""A"" value=""alpha""/>
    <item title=""B"" value=""2""/>
  </ComboBox>
</component>");

            var fairyXml = AppDomain.CurrentDomain.GetAssemblies().AsValueEnumerable()
                .Select(assembly => assembly.GetType("NanamiUI.Editor.FairyXml"))
                .FirstOrDefault(type => type != null);
            Assert.IsNotNull(fairyXml, "应能找到编辑器 XML 解析器");
            var component = fairyXml.GetMethod("LoadComponent").Invoke(null, new object[] { path });
            var combo = component.GetType().GetField("ComboBox").GetValue(component);
            var items = (string[])combo.GetType().GetField("Items").GetValue(combo);
            var values = (string[])combo.GetType().GetField("Values").GetValue(combo);

            CollectionAssert.AreEqual(new[] { "A", "B" }, items);
            CollectionAssert.AreEqual(new[] { "alpha", "2" }, values);
            File.Delete(path);
#endif
        }
    }
}
