using System.IO;
using NUnit.Framework;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests
{
    public class ComponentSchemaTests
    {
        [TestCase("", true)]
        [TestCase(" opaque='false'", false)]
        public void Components_are_opaque_unless_authored_otherwise(string attribute, bool opaque)
        {
            var file = Path.GetTempFileName();
            try
            {
                File.WriteAllText(file, "<component size='100,100' extention='Button'" + attribute + "><Button downEffect='scale' downEffectValue='0.9'/></component>");
                var xml = Schema.Component.Load(file);
                Assert.That(xml.Opaque, Is.EqualTo(opaque));
                Assert.That(xml.Button.DownEffect, Is.EqualTo(ButtonDownEffect.Scale));
                Assert.That(xml.Button.DownEffectValue, Is.EqualTo(.9f));
            }
            finally { File.Delete(file); }
        }
    }
}
