#if UNITY_2022_2_OR_NEWER
using System;
using NUnit.Framework;
using System.Collections;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Content;
using Unity.IO.Archive;
using Unity.Loading;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEditor.TestTools;
#endif

namespace UnityEditor.Build.Pipeline.Tests.ContentLoad
{
    abstract public class SceneTests : ContentFileFixture
    {
        public static IEnumerator UnloadAllScenesExceptInitTestSceneAsync()
        {
#pragma warning disable 0618
            var allScenes = SceneManager.GetAllScenes();
            var allScenesNoInit = allScenes.Where(x => !x.name.Contains("InitTestScene")).ToList();
            if (allScenes.Length == allScenesNoInit.Count)
                SceneManager.CreateScene("InitTestScene");
            foreach (var allScene in allScenesNoInit)
            {
                yield return SceneManager.UnloadSceneAsync(allScene);
            }
#pragma warning restore 0618
        }

        [UnitySetUp]
        public IEnumerator UnloadAllScenesExceptInitTestScene()
        {
            Assert.AreEqual(1, SceneManager.sceneCount);
            yield return null;
            yield return UnloadAllScenesExceptInitTestSceneAsync();
        }

        // IPostBuildCleanup
        public override void Cleanup()
        {
            base.Cleanup();
#if UNITY_EDITOR
            if (Directory.Exists("Assets/Temp"))
            {
                Directory.Delete("Assets/Temp", true);
                File.Delete("Assets/Temp.meta");
                AssetDatabase.Refresh();
            }
#endif
        }

        public ContentSceneFile LoadSceneHelper(string path, string sceneName, LoadSceneMode mode, ContentFile[] deps,
            bool integrate = true, bool autoIntegrate = false)
        {
            var sceneParams = new ContentSceneParameters();
            sceneParams.loadSceneMode = mode;
            sceneParams.localPhysicsMode = LocalPhysicsMode.None;
            sceneParams.autoIntegrate = autoIntegrate;

            NativeArray<ContentFile> files =
                new NativeArray<ContentFile>(deps.Length, Allocator.Temp, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < deps.Length; i++)
            {
                files[i] = deps[i];
            }

            ContentSceneFile op = ContentLoadInterface.LoadSceneAsync(m_NS, path, sceneName, sceneParams, files);
            files.Dispose();

            if (integrate)
            {
                op.WaitForLoadCompletion(0);
                if (op.Status == SceneLoadingStatus.WaitingForIntegrate)
                    op.IntegrateAtEndOfFrame();
            }

            return op;
        }

        private void AssertNoDepSceneLoaded(ContentSceneFile sceneFile)
        {
            LoadCatalog("nodepscene");
            Assert.AreEqual(SceneLoadingStatus.Complete, sceneFile.Status);

            Scene scene = sceneFile.Scene;
            GameObject[] objs = scene.GetRootGameObjects();
            GameObject test = objs.First(x => x.name == "testobject");
            Assert.IsTrue(SceneManager.GetSceneByName("testscene").IsValid());

            Assert.AreEqual(sceneFile, ContentLoadInterface.GetSceneFiles(m_NS)[0]);
        }

        private ArchiveHandle MountDependentContentArchive(Catalog.ContentFileInfo location)
        {
            ArchiveHandle aHandle = ArchiveFileInterface.MountAsync(ContentNamespace.Default, GetVFSFilename(location.Filename), "b:");
            aHandle.JobHandle.Complete();
            Assert.True(aHandle.JobHandle.IsCompleted);
            Assert.True(aHandle.Status == ArchiveStatus.Complete);
            return aHandle;
        }
        private ContentFile GetDependentContentArchive(Catalog.ContentFileInfo location, ArchiveHandle aHandle) {

            var mountPath = aHandle.GetMountPath();
            var vfsPath = Path.Combine(mountPath, location.Filename);
            ContentFile fileHandle = ContentLoadInterface.LoadContentFileAsync(m_NS, vfsPath, new NativeArray<ContentFile>(){});
            fileHandle.WaitForCompletion(5000);
            return fileHandle;
        }

        // This used to test loading a scene with no dependencies. Scenes, however, depend on unity builtin extras by default, so this
        // test has been changed to be more explicit about what it's testing. The scene has visual elements with a shaded material so
        // you can verify it is actually working. The name has stayed the same for instability tracking.
        [UnityTest]
        public IEnumerator CanLoadSceneWithNoDependencies()
        {
            LoadCatalog("nodepscene");
            Catalog.AddressableLocation p1Loc = m_Catalog.GetLocation("nodepscene");
            ArchiveHandle aHandle = ArchiveFileInterface.MountAsync(ContentNamespace.Default, GetVFSFilename(p1Loc.Filename), "a:");
            aHandle.JobHandle.Complete();
            Assert.True(aHandle.JobHandle.IsCompleted);
            Assert.True(aHandle.Status == ArchiveStatus.Complete);

            Assert.AreEqual(2, m_Catalog.ContentFiles.Count);
            Catalog.ContentFileInfo dependentContentFilePath = null;
            foreach (var file in m_Catalog.ContentFiles)
            {
                if (file.Filename != p1Loc.Filename)
                    dependentContentFilePath = file;
            }
            Assert.IsNotNull(dependentContentFilePath);
            var depHandle = MountDependentContentArchive(dependentContentFilePath);
            try
            {
                var mountPath = aHandle.GetMountPath();
                var vfsPath = Path.Combine(mountPath, p1Loc.Filename);
                var fileHandle = GetDependentContentArchive(dependentContentFilePath, depHandle);

                var sceneFile = LoadSceneHelper(vfsPath, "testscene", LoadSceneMode.Additive,
                    new ContentFile[] {fileHandle, ContentFile.GlobalTableDependency});


                while (sceneFile.Status == SceneLoadingStatus.InProgress)
                    yield return null;

                Assert.AreEqual(SceneLoadingStatus.WillIntegrateNextFrame, sceneFile.Status);
                yield return null;

                AssertNoDepSceneLoaded(sceneFile);
                sceneFile.UnloadAtEndOfFrame();
                yield return null;

                fileHandle.UnloadAsync().WaitForCompletion(0);
            }
            finally
            {
                depHandle.Unmount();
                aHandle.Unmount();
            }
        }

#if UNITY_EDITOR
        protected override void PrepareBuildLayout()
        {
            Directory.CreateDirectory("Assets/Temp");

            // Create a scene with no dependencies
            using (var c = CreateCatalog("nodepscene"))
            {
                Scene scene1 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                RenderSettings.skybox = null;
                SceneManager.SetActiveScene(scene1);
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "testobject";
                go.transform.position = new Vector3(0, 0, 0);
                var renderer = go.GetComponent<Renderer>();
                renderer.material = new Material(Shader.Find("Standard"));
                var camera = new GameObject("Camera", typeof(Camera));
                camera.transform.position = new Vector3(0, 1, -10);
                var light = new GameObject("Light", typeof(Light));
                EditorSceneManager.SaveScene(scene1, "Assets/Temp/nodepscene.unity");
                EditorSceneManager.CloseScene(scene1, true);
                c.Add(
                    new AssetBundleBuild
                    {
                        assetNames = new string[] {"Assets/Temp/nodepscene.unity"},
                        addressableNames = new string[] {"nodepscene"}
                    });
            }
        }
#endif
    }

    [UnityPlatform(exclude = new RuntimePlatform[]
        {RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor, RuntimePlatform.WindowsEditor})]
    class SceneTests_Standalone : SceneTests
    {
    }

#if UNITY_EDITOR
    [UnityPlatform(RuntimePlatform.WindowsEditor)]
    [RequirePlatformSupport(BuildTarget.StandaloneWindows64)]
    class SceneTests_WindowsEditor : SceneTests
    {
    }

    [UnityPlatform(RuntimePlatform.OSXEditor)]
    [RequirePlatformSupport(BuildTarget.StandaloneOSX)]
    class SceneTests_OSXEditor : SceneTests
    {
    }

    [UnityPlatform(RuntimePlatform.LinuxEditor)]
    [RequirePlatformSupport(BuildTarget.StandaloneLinux64)]
    class SceneTests_LinuxEditor : SceneTests
    {
    }
#endif

}
#endif
