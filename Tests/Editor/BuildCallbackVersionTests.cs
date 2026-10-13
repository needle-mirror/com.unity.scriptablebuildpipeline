using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityEditor.Build.Pipeline.Tests
{
    class BuildCallbackVersionTests
    {
        const string k_CallbackScriptDir = "Assets/BuildCallbackVersionTestScripts";

        // Static callback version hashes are reset by the domain reload, serialized fields survive it
        [SerializeField]
        Hash128 m_SavedShaderHash;

        [SerializeField]
        Hash128 m_SavedSceneHash;

        // The generated callbacks are only used after RecompileScripts, which compiles them and reloads the domain
        static void WriteCallbacks(uint shaderVersion, uint computeShaderVersion, uint sceneVersion)
        {
            Directory.CreateDirectory(k_CallbackScriptDir);
            File.WriteAllText(k_CallbackScriptDir + "/BuildCallbackVersionTestScripts.asmdef",
                @"{ ""name"": ""BuildCallbackVersionTestScripts"", ""includePlatforms"": [ ""Editor"" ] }");
            File.WriteAllText(k_CallbackScriptDir + "/BuildCallbacks.cs", $@"using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.SceneManagement;

[BuildCallbackVersion({shaderVersion})]
class TestShaderCallback : IPreprocessShaders
{{
    public int callbackOrder => 0;
    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data) {{}}
}}

[BuildCallbackVersion({computeShaderVersion})]
class TestComputeShaderCallback : IPreprocessComputeShaders
{{
    public int callbackOrder => 0;
    public void OnProcessComputeShader(ComputeShader shader, string kernelName, IList<ShaderCompilerData> data) {{}}
}}

[BuildCallbackVersion({sceneVersion})]
#pragma warning disable CS0618 // Type or member is obsolete
class TestSceneCallback : IProcessSceneWithReport
#pragma warning restore CS0618 // Type or member is obsolete
{{
    public int callbackOrder => 0;
    public void OnProcessScene(Scene scene, BuildReport report) {{}}
}}
");
        }

        void SaveCallbackVersionHashes()
        {
            using var wrapper = new BuildInterfacesWrapper();
            m_SavedShaderHash = BuildInterfacesWrapper.ShaderCallbackVersionHash;
            m_SavedSceneHash = BuildInterfacesWrapper.SceneCallbackVersionHash;
        }

        // Removes the generated callbacks so they don't run during the builds of other tests
        [UnityOneTimeTearDown]
        public IEnumerator UnityOneTimeTearDown()
        {
            if (AssetDatabase.IsValidFolder(k_CallbackScriptDir))
            {
                AssetDatabase.DeleteAsset(k_CallbackScriptDir);
                yield return new RecompileScripts(true);
            }
        }

        [UnityTest]
        public IEnumerator IncrementingShaderCallbackVersion_ChangesShaderCallbackVersionHash()
        {
            WriteCallbacks(shaderVersion: 1, computeShaderVersion: 1, sceneVersion: 1);
            yield return new RecompileScripts(true);
            SaveCallbackVersionHashes();

            WriteCallbacks(shaderVersion: 2, computeShaderVersion: 1, sceneVersion: 1);
            yield return new RecompileScripts(true);

            using var wrapper = new BuildInterfacesWrapper();
            Assert.AreNotEqual(m_SavedShaderHash, BuildInterfacesWrapper.ShaderCallbackVersionHash, "Incrementing the shader callback version must change the shader callback version hash");
            Assert.AreEqual(m_SavedSceneHash, BuildInterfacesWrapper.SceneCallbackVersionHash, "Incrementing the shader callback version must not change the scene callback version hash");
        }

        [UnityTest]
        public IEnumerator IncrementingComputeShaderCallbackVersion_ChangesShaderCallbackVersionHash()
        {
            WriteCallbacks(shaderVersion: 1, computeShaderVersion: 1, sceneVersion: 1);
            yield return new RecompileScripts(true);
            SaveCallbackVersionHashes();

            WriteCallbacks(shaderVersion: 1, computeShaderVersion: 2, sceneVersion: 1);
            yield return new RecompileScripts(true);

            using var wrapper = new BuildInterfacesWrapper();
            Assert.AreNotEqual(m_SavedShaderHash, BuildInterfacesWrapper.ShaderCallbackVersionHash, "Incrementing the compute shader callback version must change the shader callback version hash");
            Assert.AreEqual(m_SavedSceneHash, BuildInterfacesWrapper.SceneCallbackVersionHash, "Incrementing the compute shader callback version must not change the scene callback version hash");
        }

        [UnityTest]
        public IEnumerator IncrementingSceneCallbackVersion_ChangesSceneCallbackVersionHash()
        {
            WriteCallbacks(shaderVersion: 1, computeShaderVersion: 1, sceneVersion: 1);
            yield return new RecompileScripts(true);
            SaveCallbackVersionHashes();

            WriteCallbacks(shaderVersion: 1, computeShaderVersion: 1, sceneVersion: 2);
            yield return new RecompileScripts(true);

            using var wrapper = new BuildInterfacesWrapper();
            Assert.AreNotEqual(m_SavedSceneHash, BuildInterfacesWrapper.SceneCallbackVersionHash, "Incrementing the scene callback version must change the scene callback version hash");
            Assert.AreEqual(m_SavedShaderHash, BuildInterfacesWrapper.ShaderCallbackVersionHash, "Incrementing the scene callback version must not change the shader callback version hash");
        }
    }
}
