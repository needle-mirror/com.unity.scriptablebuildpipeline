using System.Reflection;
using NUnit.Framework;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnityEditor.Build.Pipeline.Tests
{
    class SceneProcessorVersionTests
    {
        private static uint s_SceneProcessorVersionTestValue = 1;

        [InitializeOnLoadMethod]
        static void InitializeUnityVersionIgnore()
        {
#if !UNITY_6000_7_OR_NEWER
            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping("IgnoreBeforeUnity6000.7", true);
#endif
        }

        class SceneProcessorVersionTestsProcessor : AssetPostprocessor
        {
            public override uint GetVersion() => s_SceneProcessorVersionTestValue;

#if UNITY_6000_7_OR_NEWER
            void OnProcessScene(Scene scene, UnityEditor.Build.Content.SceneImportContext sceneImportContext) {}
#endif
        }

        /// <summary>
        /// Force resets the AssetDatabase cached value for the AssetPostprocessor.OnProcessScene version.
        /// This is needed in the test because it's usually only updated on an assembly reload.
        /// Forcing it back to null here avoid a full assembly reload during the test.
        /// </summary>
        static void ResetSceneProcessorsHashStringCache()
        {
            var internalType = typeof(AssetPostprocessor).Assembly.GetType("UnityEditor.AssetPostprocessingInternal");
            var field = internalType?.GetField("m_SinglePassBuildSceneProcessorsHashString", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "AssetPostprocessingInternal.m_SinglePassBuildSceneProcessorsHashString not found.");
            field.SetValue(null, null);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // Making sure the values are reset after the test to avoid impacting other tests.
            s_SceneProcessorVersionTestValue = 1;
            ResetSceneProcessorsHashStringCache();
        }

        [Test]
        [ConditionalIgnore("IgnoreBeforeUnity6000.7", "AssetPostprocessor.OnProcessScene doesn't exist on this version of Unity")]
        public void ChangingOnProcessSceneVersion_UpdatesSceneCallbackVersionHash()
        {
            s_SceneProcessorVersionTestValue = 1;
            ResetSceneProcessorsHashStringCache();

            using var wrapper = new BuildInterfacesWrapper();
            var sceneHash = BuildInterfacesWrapper.SceneCallbackVersionHash;

            s_SceneProcessorVersionTestValue = 2;
            ResetSceneProcessorsHashStringCache();

            // This is what actually updates the BuildInterfacesWrapper.SceneCallbackVersionHash value.
            wrapper.GatherCallbackVersions();

            Assert.That(BuildInterfacesWrapper.SceneCallbackVersionHash, Is.Not.EqualTo(sceneHash));
        }
    }
}
