using UnityEngine;
using UnityEngine.Build.Pipeline;
using Unity.ScriptableBuildPipelineTests;
using NUnit.Framework;
using System.IO;
using System.Linq;

namespace UnityEditor.Build.Pipeline.Tests
{
    [TestFixture]
    class BundleDependencyTests
    {
        const string k_TmpAssetPath = "Assets/TempAssets";
        const string k_BuildFolder = "TestBuild";
        const int k_CntPrefabChain = 5;

        [OneTimeSetUp]
        public void Setup()
        {
            Directory.CreateDirectory(k_TmpAssetPath);

            // Create scenario similar to BPSBP-740

            var prefabRoots = new GameObject[k_CntPrefabChain];

            for (int i = k_CntPrefabChain - 1; i >= 0; i--)
            {
                var gameObject = new GameObject();
                var mb = gameObject.AddComponent<MonoBehaviourWithReference>();
                var prefabPath = $"{k_TmpAssetPath}/prefab{i}.prefab";


                if (i != k_CntPrefabChain - 1)
                {
                    // Point to the next prefab in the chain
                    mb.Reference = prefabRoots[i+1];
                    Assert.IsNotNull(mb.Reference);
                }
                else
                {
                    mb.Reference = gameObject; // Pointer to self, like in the original repro
                }

                prefabRoots[i] = PrefabUtility.SaveAsPrefabAsset(gameObject, prefabPath);
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport & ImportAssetOptions.ForceUpdate);
            }
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            if (Directory.Exists(k_BuildFolder))
                Directory.Delete(k_BuildFolder, true);

            AssetDatabase.DeleteAsset(k_TmpAssetPath);
        }

        static CompatibilityAssetBundleManifest BuildPrefabBundles(bool recurseDeps)
        {
            // Put each prefab into its own AssetBundle
            var bundleDefinitions = new AssetBundleBuild[k_CntPrefabChain];
            for (int i = 0; i < bundleDefinitions.Length; i++)
            {
                bundleDefinitions[i].assetBundleName = $"{i}";
                bundleDefinitions[i].assetNames = new string[] { $"{k_TmpAssetPath}/prefab{i}.prefab" };
            };

            if (Directory.Exists(k_BuildFolder))
                Directory.Delete(k_BuildFolder, true);

            Directory.CreateDirectory(k_BuildFolder);

            // Todo, confirm that the NonRecursive Mode is enabled, the test assumes that it is and i think that is the default but its not exposed in this API

            var manifest = default(CompatibilityAssetBundleManifest);

#if BUILD_OPTIONS_RECURSE_DEPENDENCIES_2022_3 || BUILD_OPTIONS_RECURSE_DEPENDENCIES_2023_3 || UNITY_6000_0_OR_NEWER
            if (recurseDeps)
            {
                manifest = CompatibilityBuildPipeline.BuildAssetBundles(
                    k_BuildFolder,
                    bundleDefinitions,
                    BuildAssetBundleOptions.AppendHashToAssetBundleName | BuildAssetBundleOptions.RecurseDependencies,
                    EditorUserBuildSettings.activeBuildTarget);
            }
            else
#endif
            {
                manifest = CompatibilityBuildPipeline.BuildAssetBundles(
                    k_BuildFolder,
                    bundleDefinitions,
                    BuildAssetBundleOptions.AppendHashToAssetBundleName,
                    EditorUserBuildSettings.activeBuildTarget);
            }

            Assert.IsNotNull(manifest);

            return manifest;
        }

#if BUILD_OPTIONS_RECURSE_DEPENDENCIES_2022_3 || BUILD_OPTIONS_RECURSE_DEPENDENCIES_2023_3 || UNITY_6000_0_OR_NEWER
        [Test, Description("BPSBP-737 / ADDR-3262")]
        public void MonoScriptsAreNotNullInChainedBundles()
        {
            // Note: Test could also do variations, with MonoScript bundle enabled, maybe also NonRecursive=false
            CompatibilityAssetBundleManifest manifest = BuildPrefabBundles(true);

            string prefabBundleMatch = "*_*"; // Match bundle names like 0_f5b4234bbd5a5a599bd740802cc6f9cf and ignore other build output

            // All the prefabs as loaded from the assetbundles should have valid MonoBehaviour with non-null reference, matching
            // how we created them in the project.
            var builtBundlePaths = Directory.EnumerateFiles(k_BuildFolder, prefabBundleMatch, SearchOption.TopDirectoryOnly).ToArray();
            LoadBundlesAndCheckMonoScript(builtBundlePaths);
        }
#endif

        static void LoadBundlesAndCheckMonoScript(string[] bundleNames)
        {
            var bundleCount = bundleNames.Length;
            var bundles = new AssetBundle[bundleCount];
            for (int i = 0; i < bundleCount; i++)
            {
                bundles[i] = AssetBundle.LoadFromFile(bundleNames[i]);
            }

            try
            {
                for (int i = 0; i < bundleCount; i++)
                {
                    if (bundles[i].name == "UnityMonoScripts.bundle")
                        continue;

                    var prefab = bundles[i].LoadAllAssets<GameObject>()[0];
                    var monoBehaviour = prefab.GetComponent<MonoBehaviourWithReference>();

                    Assert.IsNotNull(monoBehaviour, "Missing MonoScript or MonoBehaviourWithReference on " + bundleNames[i]);

                    var monoScript = MonoScript.FromMonoBehaviour(monoBehaviour);
                    Assert.IsNotNull(monoScript);
                }
            }
            finally
            {
                for (int i = 0; i < bundleCount; i++)
                    bundles[i].Unload(true);
            }
        }
    }
}
