using System;
using System.IO;
using NUnit.Framework;
using UnityEditor.Modules;
using UnityEngine;

namespace UnityEditor.Build.Pipeline.Tests
{
    /// <summary>
    /// ContentPipelineTests
    /// </summary>
    [TestFixture]
    public class ContentPipelineTests
    {

        private const string k_TempBuildFolder = "TempBuildFolder";

        /// <summary>
        /// Setup
        /// </summary>
        [SetUp]
        public void Setup()
        {
            Directory.CreateDirectory(k_TempBuildFolder);
        }

        /// <summary>
        /// Teardown
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Directory.Delete(k_TempBuildFolder, true);
        }

        /// <summary>
        /// TestCanBuildPlayer
        /// </summary>
        [Test]
        public void TestCanBuildPlayer()
        {
#if UNITY_2021_3_OR_NEWER
            // this will always return false for IsBuildTargetSupported, so it tests that pathway
            var caughtException = false;
            try
            {
                Assert.IsNotNull(ContentPipeline.CanBuildPlayer(BuildTarget.NoTarget, BuildTargetGroup.Unknown, k_TempBuildFolder));
            }
            catch (Exception e)
            {
                caughtException = true;
#if UNITY_2023_3_OR_NEWER
                Assert.AreEqual("target must be valid", e.Message);
#else
                Assert.AreEqual("targetGroup must be valid", e.Message);
#endif
            }
            Assert.True(caughtException, "Did not catch exception for no build target.");
            // this can happen if the player is not installed like in yamato, it will always return true
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows))
            {
                Assert.AreEqual("Module StandaloneWindows is not installed.", ContentPipeline.CanBuildPlayer(BuildTarget.StandaloneWindows, BuildTargetGroup.Standalone, k_TempBuildFolder));
            }
            else
            {
                Assert.IsNull(ContentPipeline.CanBuildPlayer(BuildTarget.StandaloneWindows, BuildTargetGroup.Standalone, k_TempBuildFolder));
            }

            // this can happen if the player is not installed like in yamato, it will always return true
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
            {
                Assert.AreEqual("Module StandaloneOSX is not installed.", ContentPipeline.CanBuildPlayer(BuildTarget.StandaloneOSX, BuildTargetGroup.Standalone, k_TempBuildFolder));
            } else {
                Assert.IsNull(ContentPipeline.CanBuildPlayer(BuildTarget.StandaloneOSX, BuildTargetGroup.Standalone, k_TempBuildFolder));
            }

#if UNITY_EDITOR_LINUX
            // scripting backend compatability seems like it might make this not work on all platforms
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
            {
                Assert.AreEqual("Module StandaloneLinux64 is not installed.", ContentPipeline.CanBuildPlayer(BuildTarget.StandaloneLinux64, BuildTargetGroup.Standalone, k_TempBuildFolder));
            } else {
                Assert.IsNull(ContentPipeline.CanBuildPlayer(BuildTarget.StandaloneLinux64, BuildTargetGroup.Standalone, k_TempBuildFolder));
            }
#endif
#if UNITY_2023_1_OR_NEWER
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.QNX))
            {
                Assert.AreEqual("Module QNX is not installed.", ContentPipeline.CanBuildPlayer(BuildTarget.QNX, BuildTargetGroup.Standalone, k_TempBuildFolder));
            } else {
                Assert.IsNull(ContentPipeline.CanBuildPlayer(BuildTarget.QNX, BuildTargetGroup.Standalone, k_TempBuildFolder));
            }
#endif

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.EmbeddedLinux))
            {
                Assert.AreEqual("Module EmbeddedLinux is not installed.", ContentPipeline.CanBuildPlayer(BuildTarget.EmbeddedLinux, BuildTargetGroup.Standalone, k_TempBuildFolder));
            } else {
                Assert.IsNull(ContentPipeline.CanBuildPlayer(BuildTarget.EmbeddedLinux, BuildTargetGroup.Standalone, k_TempBuildFolder));
            }
#endif
        }
    }
}

