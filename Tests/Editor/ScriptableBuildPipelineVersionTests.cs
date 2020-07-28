using NUnit.Framework;

namespace UnityEditor.Build.Pipeline
{
    public class ScriptableBuildPipelineVersionTests
    {
        [Test]
        public void TestPackageVersion()
        {
            // Make sure that the version strings in the package and SBP don't get out of sync.
            // Unfortunately, the PackageInfo methods don't exist in earlier versions of the editor.
#if UNITY_2019_3_OR_NEWER
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ContentPipeline).Assembly);
            Assert.AreEqual(ScriptableBuildPipelineVersion.kPackageName, packageInfo.name);
            Assert.AreEqual(ScriptableBuildPipelineVersion.kPackageVersion, packageInfo.version);
#endif
        }
    }
}