using System.IO;
using System.Reflection;
using UnityEditor.AssetImporters;

namespace UnityEditor.Build.Pipeline.Tests
{
    /// <summary>
    /// Minimal non-primary artifact producer for <see cref="PrefabPackedIdentifiersSerializationIndexTests.VirtualArtifactsExtraSerializationIndex"/>.
    /// PreviewImporter paths were empty under batch test runs; scripted importer output is stable for <see cref="UnityEditor.Experimental.AssetDatabaseExperimental.GetArtifactPaths"/>.
    /// Release 6000.x: write via <see cref="AssetImportContext.GetOutputArtifactFilePath"/> so artifacts are listed. Trunk/UDS: <c>SetOutputArtifactFile</c> is required for that listing; it is resolved at runtime so this project still compiles against 6000.x.
    /// </summary>
    [ScriptedImporter(1, "uum131143_virt")]
    public sealed class VirtualArtifactSerializationIndexTestImporter : ScriptedImporter
    {
        static readonly MethodInfo s_SetOutputArtifactFile = typeof(AssetImportContext).GetMethod(
            "SetOutputArtifactFile",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(string), typeof(string) },
            null);

        /// <summary>
        /// Imports the asset and produces a non-primary artifact in VirtualArtifacts/Extra for testing serialization index behavior.
        /// </summary>
        /// <param name="ctx">The asset import context provided by Unity's import pipeline.</param>
        public override void OnImportAsset(AssetImportContext ctx)
        {
            if (s_SetOutputArtifactFile != null)
            {
                s_SetOutputArtifactFile.Invoke(ctx, new object[] { "bin", ctx.assetPath });
                return;
            }

            string outPath = ctx.GetOutputArtifactFilePath("bin");
            if (!string.IsNullOrEmpty(outPath))
                File.Copy(ctx.assetPath, outPath, overwrite: true);
        }
    }
}
