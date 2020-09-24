using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor.Build.Pipeline.Utilities;

namespace UnityEditor.Build.Pipeline.Tests
{
    [TestFixture]
    public class LinkXMLGeneratorTests
    {
        const string k_LinkFile = "link.xml";

        [TearDown]
        public void OnTearDown()
        {
            if (File.Exists(k_LinkFile))
                File.Delete(k_LinkFile);
        }

        public static string ReadLinkXML(string linkFile, out int assemblyCount, out int typeCount)
        {
            FileAssert.Exists(linkFile);
            var fileText = File.ReadAllText(linkFile);
            assemblyCount = Regex.Matches(fileText, "<assembly").Count;
            typeCount = Regex.Matches(fileText, "<type").Count;
            return fileText;
        }

        public static void AssertTypePreserved(string input, Type t)
        {
            StringAssert.IsMatch($"type.*?{t.FullName}.*?preserve=\"all\"", input);
        }

        public static void AssertTypeWithAttributePreserved(string input, string fullName)
        {
            StringAssert.IsMatch($"type.*?{fullName}.*? preserve=\"nothing\" serialized=\"true\"", input);
        }

        public static void AssertAssemblyPreserved(string input, Assembly a)
        {
            StringAssert.IsMatch($"assembly.*?{a.FullName}.*?preserve=\"all\"", input);
        }

        [Test]
        public void CreateDefault_Converts_ExpectedUnityEditorTypes()
        {
            var types = LinkXmlGenerator.GetEditorTypeConversions();
            var editorTypes = types.Select(x => x.Key).ToArray();
            var runtimeTypes = types.Select(x => x.Value).ToArray();
            var assemblies = runtimeTypes.Select(x => x.Assembly).Distinct().ToArray();

            var link = LinkXmlGenerator.CreateDefault();
            link.AddTypes(editorTypes);
            link.Save(k_LinkFile);

            var xml = ReadLinkXML(k_LinkFile, out int assemblyCount, out int typeCount);
            Assert.AreEqual(assemblyCount, assemblies.Length);
            Assert.AreEqual(typeCount, runtimeTypes.Length);
            foreach (var t in runtimeTypes)
                AssertTypePreserved(xml, t);
        }

        [Test]
        public void CreateDefault_DoesNotConvert_UnexpectedUnityEditorTypes()
        {
            var unexpectedType = typeof(UnityEditor.BuildPipeline);

            var link = LinkXmlGenerator.CreateDefault();
            link.AddTypes(new[] { unexpectedType });
            link.Save(k_LinkFile);

            var xml = ReadLinkXML(k_LinkFile, out int assemblyCount, out int typeCount);
            Assert.AreEqual(assemblyCount, 1);
            Assert.AreEqual(typeCount, 1);
            AssertTypePreserved(xml, unexpectedType);
        }

        [Test]
        public void LinkXML_Preserves_MultipleTypes_FromMultipleAssemblies()
        {
            var types = new[] { typeof(UnityEngine.MonoBehaviour), typeof(UnityEngine.Build.Pipeline.CompatibilityAssetBundleManifest) };

            var link = new LinkXmlGenerator();
            link.AddTypes(types);
            link.Save(k_LinkFile);

            var xml = ReadLinkXML(k_LinkFile, out int assemblyCount, out int typeCount);
            Assert.AreEqual(assemblyCount, 2);
            Assert.AreEqual(typeCount, types.Length);
            foreach (var t in types)
                AssertTypePreserved(xml, t);
        }

        [Test]
        public void LinkXML_Preserves_Assemblies()
        {
            var assemblies = new[] { typeof(UnityEngine.MonoBehaviour).Assembly, typeof(UnityEngine.Build.Pipeline.CompatibilityAssetBundleManifest).Assembly };

            var link = new LinkXmlGenerator();
            link.AddAssemblies(assemblies);
            link.Save(k_LinkFile);

            var xml = ReadLinkXML(k_LinkFile, out int assemblyCount, out int typeCount);
            Assert.AreEqual(assemblyCount, assemblies.Length);
            Assert.AreEqual(typeCount, 0);
            foreach (var a in assemblies)
                AssertAssemblyPreserved(xml, a);
        }


        [Test]
        public void LinkXML_Preserves_SerializeClasses()
        {
            var serializedRefClasses = new[] { "FantasticAssembly:AwesomeNS.Foo", "FantasticAssembly:AwesomeNS.Bar", "SuperFantasticAssembly:SuperAwesomeNS.Bar"};

            var link = new LinkXmlGenerator();
            link.AddSerializedClass(serializedRefClasses);
            link.Save(k_LinkFile);

            var xml = ReadLinkXML(k_LinkFile, out int assemblyCount, out int typeCount);
            Assert.AreEqual(assemblyCount, 2);
            Assert.AreEqual(typeCount,3);
            AssertTypeWithAttributePreserved(xml, "AwesomeNS.Foo");
            AssertTypeWithAttributePreserved(xml, "AwesomeNS.Bar");
            AssertTypeWithAttributePreserved(xml, "SuperAwesomeNS.Bar");
        }
    }
}
