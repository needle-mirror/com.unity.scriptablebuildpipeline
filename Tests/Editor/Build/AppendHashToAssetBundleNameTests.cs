using UnityEditor;
using NUnit.Framework;
using System.IO;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor.Build.Pipeline;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.TestTools;
using UnityEditor.Build.Pipeline.Tests;

/// <summary>
/// Tests to ensure we are creating the correct file name
/// </summary>
public class AppendHashToAssetBundleNameTests
{
    const string k_outputBundleDirectory = "Assets/MyBundle";
    const string k_scenePath = "Assets/TestScenes.unity";
    const string k_bundleNameWithExtension = "Scenes.bundle";
    const string k_bundleNamewithoutExtension = "Scenes";

    /// <summary>
    /// Test setup
    /// </summary>
    [SetUp]
    public void Setup()
    {
        if (!File.Exists(k_scenePath))
        {
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            EditorSceneManager.SaveScene(newScene, k_scenePath);
        }

        if (!Directory.Exists(k_outputBundleDirectory))
            Directory.CreateDirectory(k_outputBundleDirectory);
    }

    /// <summary>
    /// ADDR-3852 - BuildAssetBundleOptions.AppendHashToAssetBundleName returns correct format when using CompatibilityBuildPipeline.BuildOptions
    /// </summary>
    [Test]
    public void AppendHashToAssetBundleName_Sets_Bundle_Extension_After_HashNumber()
    {
        List<AssetBundleBuild> assetBundleDefinitionList = new List<AssetBundleBuild>();
        {
            AssetBundleBuild assetBundleBuild = new AssetBundleBuild();
            assetBundleBuild.assetBundleName = k_bundleNameWithExtension;
            assetBundleBuild.assetNames = new string[] { k_scenePath };
            assetBundleDefinitionList.Add(assetBundleBuild);
        }

        var buildTarget = EditorUserBuildSettings.activeBuildTarget;
        CompatibilityBuildPipeline.BuildAssetBundles(k_outputBundleDirectory, assetBundleDefinitionList.ToArray(),
              BuildAssetBundleOptions.AppendHashToAssetBundleName, buildTarget);

        string getBundleNameWithExtension = Directory.GetFiles(k_outputBundleDirectory)
            .Select(Path.GetFileName)
            .FirstOrDefault(name => name.StartsWith(k_bundleNamewithoutExtension));

        Assert.IsNotNull(getBundleNameWithExtension, $"No AssetBundle is in the name of {k_bundleNamewithoutExtension}");

        bool isValidBundleName = Regex.IsMatch(getBundleNameWithExtension, @"^Scenes_[A-Za-z0-9]{32}\.bundle$");

        Assert.IsTrue(isValidBundleName, $"Incorrect AssetBundle name. Expected format: 'Scenes_<32-character-hash>.bundle'. But Actual format was: 'Scenes.bundle_<32-character-hash>'");
    }

    /// <summary>
    /// Test tear down
    /// </summary>
    [TearDown]
    public void Cleanup()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); // clear active scene

        if (Directory.Exists(k_outputBundleDirectory))
            Directory.Delete(k_outputBundleDirectory, true);

        File.Delete(k_outputBundleDirectory + ".meta");

        AssetDatabase.DeleteAsset(k_scenePath);
    }
}
